namespace Slime.Core;

/// <summary>Mouse capture is managed by the UI; click/drag decisions are shared and testable.</summary>
public sealed class PointerInteraction(SlimeModel model, double dragThreshold = 6)
{
    private double startX;
    private double startY;
    private double offsetX;
    private double offsetY;
    public bool IsPressed { get; private set; }
    public bool IsDragging { get; private set; }

    public bool Press(double x, double y)
    {
        if (IsPressed || !model.BeginHold()) return false;
        IsPressed = true;
        IsDragging = false;
        startX = x;
        startY = y;
        offsetX = x - model.X;
        offsetY = y - model.Y;
        return true;
    }

    public void Move(double x, double y, IReadOnlyList<DesktopArea> screens)
    {
        if (!IsPressed) return;
        if (Math.Abs(x - startX) >= dragThreshold || Math.Abs(y - startY) >= dragThreshold)
            IsDragging = true;
        if (IsDragging) model.DragTo(x - offsetX, y - offsetY, screens);
    }

    public bool Release(double x, double y, IReadOnlyList<DesktopArea> screens)
    {
        if (!IsPressed) return false;
        Move(x, y, screens);
        var wasDrag = IsDragging;
        Cancel();
        return !wasDrag && model.Click();
    }

    public void Cancel()
    {
        IsPressed = false;
        IsDragging = false;
        model.EndHold();
    }
}
