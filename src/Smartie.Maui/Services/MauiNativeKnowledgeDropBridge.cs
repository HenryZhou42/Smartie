using Smartie.Shared.Services;

namespace Smartie.Maui.Services;

public sealed class MauiNativeKnowledgeDropBridge : INativeKnowledgeDropBridge
{
    private Func<IReadOnlyList<string>, Task>? _handler;
    private bool _accepting;

    public bool IsNativeDropAvailable => true;

    public bool IsAcceptingDrops => _accepting;

    public event EventHandler<bool>? DragStateChanged;

    public void SetAcceptingDrops(bool accepting) => _accepting = accepting;

    public void SetFilesDroppedHandler(Func<IReadOnlyList<string>, Task>? handler) => _handler = handler;

    public void NotifyDragState(bool isDragging) => DragStateChanged?.Invoke(this, isDragging);

    public async Task RaiseFilesDroppedAsync(IReadOnlyList<string> filePaths)
    {
        if (!_accepting || _handler is null || filePaths.Count == 0)
        {
            return;
        }

        await _handler(filePaths).ConfigureAwait(false);
    }
}
