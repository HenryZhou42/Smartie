using Microsoft.JSInterop;
using Smartie.Contracts;

namespace Smartie.Shared.Services;

public sealed class ThemeApplicator
{
    private readonly IJSRuntime _js;

    public ThemeApplicator(IJSRuntime js)
    {
        _js = js;
    }

    public AppearanceSettingsDto? Current { get; private set; }

    public event Action? Changed;

    public async Task ApplyAsync(AppearanceSettingsDto settings, CancellationToken cancellationToken = default)
    {
        Current = settings;
        var entries = AppearanceThemeMapper.Map(settings)
            .Select(v => new ThemeEntry(v.Name, v.Value))
            .ToArray();

        await _js.InvokeVoidAsync("smartieTheme.apply", cancellationToken, entries).ConfigureAwait(false);
        Changed?.Invoke();
    }

    private sealed record ThemeEntry(string Name, string Value);
}
