using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Smartie.Application.Abstractions;
using Smartie.Application.Automation;
using Smartie.Domain.Entities;

namespace Smartie.Application.Services;

public sealed class AutomationActionExecutor
{
    private readonly IServiceProvider _services;

    public AutomationActionExecutor(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<string> ExecuteAsync(
        Guid userId,
        AutomationRule rule,
        AutomationConfig config,
        AutomationEventContext? context,
        CancellationToken cancellationToken)
    {
        return rule.ActionType switch
        {
            AutomationActionType.AskAi => await ExecuteAskAiAsync(userId, config, cancellationToken).ConfigureAwait(false),
            AutomationActionType.RunPrompt => await ExecuteAskAiAsync(userId, config, cancellationToken).ConfigureAwait(false),
            AutomationActionType.SummarizeDocument => await ExecuteSummarizeDocumentAsync(userId, config, context, cancellationToken).ConfigureAwait(false),
            AutomationActionType.CreateTask => await ExecuteCreateTaskAsync(userId, config, cancellationToken).ConfigureAwait(false),
            AutomationActionType.MoveFile => await ExecuteMoveFileAsync(config, cancellationToken).ConfigureAwait(false),
            AutomationActionType.ImportDocument => await ExecuteImportDocumentAsync(userId, config, cancellationToken).ConfigureAwait(false),
            AutomationActionType.GenerateNotes => await ExecuteGenerateNotesAsync(userId, config, cancellationToken).ConfigureAwait(false),
            AutomationActionType.ExportConversation => await ExecuteExportConversationAsync(userId, config, context, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported action '{rule.ActionType}'.")
        };
    }

    private async Task<string> ExecuteAskAiAsync(
        Guid userId,
        AutomationConfig config,
        CancellationToken cancellationToken)
    {
        var conversations = _services.GetRequiredService<IConversationService>();
        var memory = _services.GetRequiredService<IMemoryService>();
        var prompt = config.Action.Prompt?.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException("Ask AI requires a prompt in the action config.");
        }

        var conversation = await conversations.CreateAsync(userId, config.Action.Title ?? "Automation", cancellationToken).ConfigureAwait(false);
        var reply = await conversations.SendAsync(conversation.Id, prompt, cancellationToken).ConfigureAwait(false);

        if (config.Action.SaveAsNote)
        {
            await memory.StoreMemoryAsync(
                userId,
                reply.Content,
                MemoryCategory.Custom,
                MemoryImportance.Medium,
                cancellationToken).ConfigureAwait(false);
            return $"AI reply saved as note ({reply.Content.Length} chars).";
        }

        return $"AI reply generated ({reply.Content.Length} chars).";
    }

    private async Task<string> ExecuteSummarizeDocumentAsync(
        Guid userId,
        AutomationConfig config,
        AutomationEventContext? context,
        CancellationToken cancellationToken)
    {
        var documents = _services.GetRequiredService<IDocumentService>();
        var extraction = _services.GetRequiredService<IDocumentExtractionService>();
        var chunking = _services.GetRequiredService<IDocumentChunkingService>();
        var embedding = _services.GetRequiredService<IDocumentEmbeddingService>();
        var conversations = _services.GetRequiredService<IConversationService>();
        var memory = _services.GetRequiredService<IMemoryService>();

        var documentId = config.Action.DocumentId ?? context?.DocumentId;
        if (documentId is null)
        {
            throw new InvalidOperationException("Summarize Document requires a document id.");
        }

        var document = await documents.GetAsync(userId, documentId.Value, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Document not found.");

        if (config.Action.RunFullIndex)
        {
            await extraction.ExtractAndPersistAsync(document.Id, userId, cancellationToken).ConfigureAwait(false);
            await chunking.ChunkAndPersistAsync(document.Id, userId, cancellationToken).ConfigureAwait(false);
            await embedding.GenerateAndPersistAsync(document.Id, userId, cancellationToken).ConfigureAwait(false);
        }

        var prompt = config.Action.Prompt?.Trim()
            ?? $"Summarize the document '{document.Name}' in a concise overview.";
        var conversation = await conversations.CreateAsync(userId, $"Summary · {document.Name}", cancellationToken).ConfigureAwait(false);
        var reply = await conversations.SendAsync(conversation.Id, prompt, cancellationToken).ConfigureAwait(false);

        if (config.Action.SaveAsNote)
        {
            await memory.StoreMemoryAsync(
                userId,
                reply.Content,
                MemoryCategory.Custom,
                MemoryImportance.Medium,
                cancellationToken).ConfigureAwait(false);
        }

        return config.Action.RunFullIndex
            ? $"Indexed and summarized '{document.Name}'."
            : $"Summarized '{document.Name}'.";
    }

    private async Task<string> ExecuteCreateTaskAsync(
        Guid userId,
        AutomationConfig config,
        CancellationToken cancellationToken)
    {
        var title = config.Action.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException("Create Task requires a title.");
        }

        TaskPriority? priority = Enum.TryParse<TaskPriority>(config.Action.TaskPriority, true, out var parsed)
            ? parsed
            : null;

        var tasks = _services.GetRequiredService<ITaskService>();
        var task = await tasks.CreateAsync(
            userId,
            title,
            config.Action.Description,
            priority,
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        return $"Created task '{task.Title}'.";
    }

    private static Task<string> ExecuteMoveFileAsync(AutomationConfig config, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = config.Action.SourcePath?.Trim();
        var destination = config.Action.DestinationPath?.Trim();
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
        {
            throw new InvalidOperationException("Move File requires source and destination paths.");
        }

        if (!File.Exists(source))
        {
            throw new FileNotFoundException("Source file not found.", source);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Move(source, destination, overwrite: true);
        return Task.FromResult($"Moved file to {destination}.");
    }

    private async Task<string> ExecuteImportDocumentAsync(
        Guid userId,
        AutomationConfig config,
        CancellationToken cancellationToken)
    {
        var documents = _services.GetRequiredService<IDocumentService>();
        var extraction = _services.GetRequiredService<IDocumentExtractionService>();
        var chunking = _services.GetRequiredService<IDocumentChunkingService>();
        var embedding = _services.GetRequiredService<IDocumentEmbeddingService>();

        var filePath = config.Action.FilePath?.Trim();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new InvalidOperationException("Import Document requires a valid local file path.");
        }

        await using var stream = File.OpenRead(filePath);
        var fileName = Path.GetFileName(filePath);
        var document = await documents.UploadAsync(userId, fileName, stream, stream.Length, cancellationToken).ConfigureAwait(false);

        if (config.Action.RunFullIndex)
        {
            await extraction.ExtractAndPersistAsync(document.Id, userId, cancellationToken).ConfigureAwait(false);
            await chunking.ChunkAndPersistAsync(document.Id, userId, cancellationToken).ConfigureAwait(false);
            await embedding.GenerateAndPersistAsync(document.Id, userId, cancellationToken).ConfigureAwait(false);
            return $"Imported and indexed '{document.Name}'.";
        }

        return $"Imported '{document.Name}'.";
    }

    private async Task<string> ExecuteGenerateNotesAsync(
        Guid userId,
        AutomationConfig config,
        CancellationToken cancellationToken)
    {
        var conversations = _services.GetRequiredService<IConversationService>();
        var memory = _services.GetRequiredService<IMemoryService>();
        var content = config.Action.Prompt?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Generate Notes requires note content or a prompt.");
        }

        if (config.Action.Prompt!.Contains("{{", StringComparison.Ordinal))
        {
            var conversation = await conversations.CreateAsync(userId, config.Action.Title ?? "Automation Notes", cancellationToken).ConfigureAwait(false);
            var reply = await conversations.SendAsync(conversation.Id, config.Action.Prompt, cancellationToken).ConfigureAwait(false);
            content = reply.Content;
        }

        await memory.StoreMemoryAsync(
            userId,
            content,
            MemoryCategory.Custom,
            MemoryImportance.Medium,
            cancellationToken).ConfigureAwait(false);

        return "Saved generated note to memory.";
    }

    private async Task<string> ExecuteExportConversationAsync(
        Guid userId,
        AutomationConfig config,
        AutomationEventContext? context,
        CancellationToken cancellationToken)
    {
        var conversationRepository = _services.GetRequiredService<IConversationRepository>();
        var conversationId = config.Action.ConversationId ?? context?.ConversationId;
        if (conversationId is null)
        {
            throw new InvalidOperationException("Export Conversation requires a conversation id.");
        }

        var conversation = await conversationRepository.FindAsync(conversationId.Value, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Conversation not found.");

        var messages = conversation.Messages.OrderBy(m => m.CreatedAt).ToList();
        var builder = new StringBuilder();
        builder.AppendLine($"# {conversation.Title}");
        builder.AppendLine($"Exported {DateTimeOffset.UtcNow:u}");
        builder.AppendLine();

        foreach (var message in messages)
        {
            builder.AppendLine($"## {message.Role}");
            builder.AppendLine(message.Content);
            builder.AppendLine();
        }

        var safeName = string.Join("_", conversation.Title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = conversation.Id.ToString("N");
        }

        var exportRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Smartie",
            "AutomationExports");
        Directory.CreateDirectory(exportRoot);
        var exportPath = Path.Combine(exportRoot, $"{safeName}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.md");
        await File.WriteAllTextAsync(exportPath, builder.ToString(), cancellationToken).ConfigureAwait(false);
        return $"Exported conversation to {exportPath}.";
    }
}
