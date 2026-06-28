using Smartie.Maui.Hosting;

namespace Smartie.Maui;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = WarmUpEmbeddedApiAsync();
    }

    private static async Task WarmUpEmbeddedApiAsync()
    {
        var services = IPlatformApplication.Current?.Services;
        if (services?.GetService<LocalSmartieApiHost>() is not { } apiHost)
        {
            return;
        }

        try
        {
            await apiHost.EnsureStartedAsync().ConfigureAwait(false);
        }
        catch
        {
            // UI should still render; pages show API-offline states until retry.
        }
    }
}
