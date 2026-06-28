using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Smartie.Application.Abstractions;
using Smartie.Application.Configuration;
using Smartie.Infrastructure.Ai;
using Smartie.Infrastructure.Documents;
using Smartie.Infrastructure.Persistence;
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
        services.AddScoped<IDocumentStorage, LocalDocumentStorage>();
        services.AddScoped<IChatAttachmentStorage, LocalChatAttachmentStorage>();

        services.AddScoped<TxtDocumentTextExtractor>();
        services.AddScoped<MarkdownDocumentTextExtractor>();
        services.AddScoped<PdfDocumentTextExtractor>();
        services.AddScoped<DocxDocumentTextExtractor>();
        services.AddScoped<IAttachmentTextExtractor, ChatFileTextExtractor>();
        services.AddScoped<IDocumentTextExtractionRouter, DocumentTextExtractionRouter>();
        services.AddScoped<IDocumentTextExtractor>(sp => new CompositeDocumentTextExtractor(
        [
            sp.GetRequiredService<TxtDocumentTextExtractor>(),
            sp.GetRequiredService<MarkdownDocumentTextExtractor>(),
            sp.GetRequiredService<PdfDocumentTextExtractor>(),
            sp.GetRequiredService<DocxDocumentTextExtractor>()
        ]));

        RegisterSecretProtector(services);

        services.AddSingleton<IChatCompletionProvider, ChatCompletionProviderFactory>();
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
