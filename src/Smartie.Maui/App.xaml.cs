using Smartie.Maui.Hosting;

namespace Smartie.Maui;

public partial class App : Microsoft.Maui.Controls.Application
{
	private static bool _shutdownHooked;

	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		EnsureShutdownHooked();

		var window = new Window(new MainPage()) { Title = "Smartie" };
		window.Destroying += (_, _) => StopEmbeddedApi();
		return window;
	}

	private static void EnsureShutdownHooked()
	{
		if (_shutdownHooked)
		{
			return;
		}

		_shutdownHooked = true;
		AppDomain.CurrentDomain.ProcessExit += (_, _) => StopEmbeddedApi();
	}

	private static void StopEmbeddedApi()
	{
		var services = IPlatformApplication.Current?.Services;
		if (services?.GetService<LocalSmartieApiHost>() is { } apiHost)
		{
			// Never block the UI thread here — WebView must tear down before Kestrel
			// can finish draining active connections (e.g. SSE streams).
			apiHost.StopInBackground();
		}
	}
}
