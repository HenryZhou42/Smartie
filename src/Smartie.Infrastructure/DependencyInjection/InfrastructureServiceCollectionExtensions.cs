using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Infrastructure.Ai;
using Smartie.Infrastructure.Chunking;
using Smartie.Infrastructure.Documents;
using Smartie.Infrastructure.Persistence;
using Smartie.Infrastructure.Automation;
using Smartie.Infrastructure.Plugins;
using Smartie.Infrastructure.Startup;
using Smartie.Infrastructure.Security;
using Smartie.Infrastructure.Storage;

namespace Smartie.Infrastructure.DependencyInjection;

/// <summary>
/// Registers the infrastructure layer: EF Core persistence, secret protection, and
/// the Semantic Kernel based AI provider (resolved per request from user settings).
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddSmartieInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<KnowledgeBaseOptions>(configuration.GetSection(KnowledgeBaseOptions.SectionName));
        services.Configure<ChunkingOptions>(configuration.GetSection(ChunkingOptions.SectionName));
        services.Configure<SemanticSearchOptions>(configuration.GetSection(SemanticSearchOptions.SectionName));
        services.Configure<MemoryOptions>(configuration.GetSection(MemoryOptions.SectionName));
        services.Configure<FileIntegrationOptions>(configuration.GetSection(FileIntegrationOptions.SectionName));
        services.Configure<AttachedDocumentContextOptions>(configuration.GetSection(AttachedDocumentContextOptions.SectionName));

        var configured = configuration.GetConnectionString("Smartie");
        var connectionString = string.IsNullOrWhiteSpace(configured)
            ? $"Data Source={SmartiePaths.DefaultDatabasePath()}"
            : configured;

        services.AddDbContext<SmartieDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IAiSettingsRepository, AiSettingsRepository>();
        services.AddScoped<IMessageAttachmentRepository, MessageAttachmentRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
        services.AddScoped<IMemoryRepository, MemoryRepository>();
        services.AddScoped<IRecentCommandRepository, RecentCommandRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IFileIntegrationRepository, FileIntegrationRepository>();
        services.AddScoped<IAppearanceRepository, AppearanceRepository>();
        services.AddScoped<IPluginRepository, PluginRepository>();
        services.AddScoped<IAutomationRepository, AutomationRepository>();
        services.AddScoped<IOnboardingRepository, OnboardingRepository>();
        services.AddSingleton<PluginRegistry>();
        services.AddSingleton<IPluginRegistry>(sp => sp.GetRequiredService<PluginRegistry>());
        services.AddSingleton<IPluginLoader, PluginLoader>();
        services.AddHostedService<PluginBootstrapHostedService>();
        services.AddHostedService<AutomationSchedulerHostedService>();
        services.AddHostedService<AppStartupMetricsHostedService>();
        services.AddScoped<IDocumentStorage, LocalDocumentStorage>();
        services.AddScoped<IChatAttachmentStorage, LocalChatAttachmentStorage>();

        services.AddScoped<TxtDocumentTextExtractor>();
        services.AddScoped<MarkdownDocumentTextExtractor>();
        services.AddScoped<PdfDocumentTextExtractor>();
        services.AddScoped<DocxDocumentTextExtractor>();
        services.AddScoped<IAttachmentTextExtractor, ChatFileTextExtractor>();
        services.AddScoped<IDocumentTextExtractionRouter, DocumentTextExtractionRouter>();
        services.AddScoped<IDocumentChunker, BasicDocumentChunker>();
        services.AddScoped<IDocumentTextExtractor>(sp => new CompositeDocumentTextExtractor(
        [
            sp.GetRequiredService<TxtDocumentTextExtractor>(),
            sp.GetRequiredService<MarkdownDocumentTextExtractor>(),
            sp.GetRequiredService<PdfDocumentTextExtractor>(),
            sp.GetRequiredService<DocxDocumentTextExtractor>()
        ]));

        RegisterSecretProtector(services);

        services.AddSingleton<IChatCompletionProvider, ChatCompletionProviderFactory>();
        services.AddSingleton<IEmbeddingProviderFactory, EmbeddingProviderFactory>();
        services.AddScoped<IChatAiService, SemanticKernelChatService>();

        return services;
    }

    private static void RegisterSecretProtector(IServiceCollection services)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        }
        else
        {
            // DPAPI is Windows-only; elsewhere fall back to a no-op so the app still runs.
            services.AddSingleton<ISecretProtector, PassthroughSecretProtector>();
        }
    }
}
