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
        services.AddScoped<IDocumentChunkingService, DocumentChunkingService>();
        services.AddScoped<IDocumentEmbeddingService, DocumentEmbeddingService>();
        services.AddScoped<ISemanticSearchService, SemanticSearchService>();
        services.AddScoped<IMemoryService, MemoryService>();
        services.AddScoped<IMemoryPromptBuilder, MemoryPromptBuilder>();
        services.AddSingleton<IMemoryExtractor, MemoryExtractor>();
        services.AddScoped<ICommandPaletteService, CommandPaletteService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IFileIntegrationService, FileIntegrationService>();
        services.AddScoped<IAppearanceService, AppearanceService>();
        services.AddScoped<IPluginService, PluginService>();
        services.AddScoped<AutomationActionExecutor>();
        services.AddScoped<IAutomationService, AutomationService>();
        services.AddScoped<IAutomationEventPublisher>(sp =>
            (IAutomationEventPublisher)sp.GetRequiredService<IAutomationService>());
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddSingleton<IAppMetricsService, AppMetricsService>();
        services.AddScoped<IChatAttachmentService, ChatAttachmentService>();
        services.AddScoped<IAttachedDocumentPromptBuilder, AttachedDocumentPromptBuilder>();
        return services;
    }
}
