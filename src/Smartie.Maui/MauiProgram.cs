using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Smartie.Maui.Hosting;
using Smartie.Shared.Services;

#if WINDOWS
using Microsoft.Maui.Platform;
using Smartie.Maui.Platform;
#endif

namespace Smartie.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
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
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

#if WINDOWS
	private static void ConfigureLifecycleEvents(ILifecycleBuilder events)
	{
		events.AddWindows(windows => windows.OnWindowCreated(window =>
		{
			if (window is MauiWinUIWindow mauiWindow)
			{
				NativeWindowDropHook.Install(mauiWindow);
			}
		}));
	}
#else
	private static void ConfigureLifecycleEvents(ILifecycleBuilder events)
	{
	}
#endif
}
