using Microsoft.Extensions.DependencyInjection;
using Smartie.Application.Abstractions;
using Smartie.Application.Services;

namespace Smartie.Application.DependencyInjection;

/// <summary>
/// Registers the application layer (use cases / orchestration) services.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddSmartieApplication(this IServiceCollection services)
    {
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IAiSettingsService, AiSettingsService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDocumentExtractionService, DocumentExtractionService>();
        services.AddScoped<IChatAttachmentService, ChatAttachmentService>();
        services.AddScoped<IAttachedDocumentPromptBuilder, AttachedDocumentPromptBuilder>();
        return services;
    }
}
