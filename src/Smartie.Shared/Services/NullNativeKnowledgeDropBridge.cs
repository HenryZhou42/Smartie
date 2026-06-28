namespace Smartie.Shared.Services;

public sealed class NullNativeKnowledgeDropBridge : INativeKnowledgeDropBridge
{
    public bool IsNativeDropAvailable => false;

    public bool IsAcceptingDrops => false;

    public event EventHandler<bool>? DragStateChanged { add { } remove { } }

    public void SetAcceptingDrops(bool accepting)
    {
    }

    public void SetFilesDroppedHandler(Func<IReadOnlyList<string>, Task>? handler)
    {
    }

    public void NotifyDragState(bool isDragging)
    {
    }

    public Task RaiseFilesDroppedAsync(IReadOnlyList<string> filePaths) => Task.CompletedTask;
}
