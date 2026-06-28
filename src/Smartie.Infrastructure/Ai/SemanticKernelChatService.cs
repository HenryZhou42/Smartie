using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Smartie.Application.Abstractions;
using Smartie.Domain.Entities;

namespace Smartie.Infrastructure.Ai;

/// <summary>
/// <see cref="IChatAiService"/> backed by Semantic Kernel. The concrete provider and
/// credentials are resolved per request from the current user's settings, so the user
/// can switch providers / bring their own key at runtime without a restart.
/// </summary>
public sealed class SemanticKernelChatService : IChatAiService
{
    private readonly ICurrentUser _currentUser;
    private readonly IAiSettingsService _settings;
    private readonly IChatCompletionProvider _providerFactory;

    public SemanticKernelChatService(
        ICurrentUser currentUser,
        IAiSettingsService settings,
        IChatCompletionProvider providerFactory)
    {
        _currentUser = currentUser;
        _settings = settings;
        _providerFactory = providerFactory;
    }

    public async IAsyncEnumerable<string> StreamReplyAsync(
        IReadOnlyList<Message> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var resolved = await _settings.ResolveAsync(_currentUser.UserId, cancellationToken).ConfigureAwait(false);
        var chat = _providerFactory.Get(resolved);

        var chatHistory = new ChatHistory();
        if (!string.IsNullOrWhiteSpace(resolved.SystemPrompt))
        {
            chatHistory.AddSystemMessage(resolved.SystemPrompt);
        }

        foreach (var message in history)
        {
            switch (message.Role)
            {
                case MessageRole.User:
                    chatHistory.AddUserMessage(message.Content);
                    break;
                case MessageRole.Assistant:
                    chatHistory.AddAssistantMessage(message.Content);
                    break;
                case MessageRole.System:
                    chatHistory.AddSystemMessage(message.Content);
                    break;
                // Tool turns are reintroduced when tool-calling returns as SK plugins.
            }
        }

        // Manual enumeration so provider/transport failures can be translated into
        // clean application exceptions without breaking the iterator (yield cannot
        // appear inside a catch block).
        await using var enumerator = chat
            .GetStreamingChatMessageContentsAsync(chatHistory, cancellationToken: cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            string? content;
            try
            {
                if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    break;
                }

                content = enumerator.Current.Content;
            }
            catch (HttpOperationException ex)
            {
                throw Translate(ex);
            }

            if (!string.IsNullOrEmpty(content))
            {
                yield return content!;
            }
        }
    }

    private static AiServiceException Translate(HttpOperationException ex) =>
        ex.StatusCode == HttpStatusCode.TooManyRequests
            ? new AiRateLimitedException("The AI provider is rate-limited or out of quota.", ex)
            : new AiServiceException($"The AI provider returned an error ({ex.StatusCode}).", ex);
}
