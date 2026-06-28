namespace Smartie.Shared.Services;

/// <summary>
/// Bridges OS-level file drops into Blazor on platforms where WebView2 does not
/// deliver Explorer drag-and-drop to HTML (MAUI Windows).
/// </summary>
public interface INativeKnowledgeDropBridge
{
    bool IsNativeDropAvailable { get; }

    bool IsAcceptingDrops { get; }

    void SetAcceptingDrops(bool accepting);

    void SetFilesDroppedHandler(Func<IReadOnlyList<string>, Task>? handler);

    void NotifyDragState(bool isDragging);

    event EventHandler<bool>? DragStateChanged;

    Task RaiseFilesDroppedAsync(IReadOnlyList<string> filePaths);
}
