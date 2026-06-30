using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Smartie.Maui.Hosting;
using Smartie.Shared.Services;

namespace Smartie.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
#if WINDOWS
		WebView2Bootstrapper.ApplyProcessDefaults();
#endif

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				// OpenSans font file is optional; system fonts are used by the Blazor UI.
			})
			.ConfigureLifecycleEvents(ConfigureLifecycleEvents);

		using (var configStream = Assembly.GetExecutingAssembly()
			.GetManifestResourceStream("Smartie.Maui.appsettings.json"))
		{
			if (configStream is not null)
			{
				builder.Configuration.AddJsonStream(configStream);
			}
		}

		var hostEmbedded = builder.Configuration.GetValue("SmartieApi:HostEmbedded", true);

		builder.Services.AddSingleton<IDocumentFilePickerService, Services.MauiDocumentFilePickerService>();
		builder.Services.AddSingleton<INativeKnowledgeDropBridge, Services.MauiNativeKnowledgeDropBridge>();
		builder.Services.AddSingleton<CommandPaletteHost>();
		builder.Services.AddSingleton<ILocalFileSystemService, Services.MauiLocalFileSystemService>();
		builder.Services.AddScoped<ThemeApplicator>();

		if (hostEmbedded)
		{
			builder.Services.AddSingleton<LocalSmartieApiHost>();
			builder.Services.AddSingleton<ISmartieApiEndpointProvider, Services.EmbeddedSmartieApiEndpointProvider>();
			builder.Services.AddHttpClient<SmartieApiClient>();
		}
		else
		{
			var apiBaseUrl = builder.Configuration["SmartieApi:BaseUrl"] ?? "http://localhost:5220";
			builder.Services.AddSingleton<ISmartieApiEndpointProvider>(_ =>
				new FixedSmartieApiEndpointProvider(new Uri(apiBaseUrl.TrimEnd('/') + "/")));
			builder.Services.AddHttpClient<SmartieApiClient>();
		}

		builder.Services.AddMauiBlazorWebView();

#if DEBUG
		// Developer tools can prevent WebView2 from finishing initialization on some machines.
		// builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

#if WINDOWS
	private static void ConfigureLifecycleEvents(ILifecycleBuilder events)
	{
		// Native drag/drop is attached from MainPage after BlazorWebView initializes.
	}
#else
	private static void ConfigureLifecycleEvents(ILifecycleBuilder events)
	{
	}
#endif
}
