using System.Drawing.Imaging;
using Slime.Core;

namespace Slime.Desktop;

/// <summary>A small click-through layer keeps a dropped snack in its desktop position.</summary>
internal sealed class FoodWindow : Form
{
    private readonly Bitmap frame = new(60, 60, PixelFormat.Format32bppPArgb);
    public FoodWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(60, 60);
        StartPosition = FormStartPosition.Manual;
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var p = base.CreateParams;
            p.ExStyle |= 0x80000 | 0x80 | 0x08000000 | 0x20;
            return p;
        }
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x84) { message.Result = (IntPtr)(-1); return; }
        if (message.Msg == 0x21) { message.Result = (IntPtr)3; return; }
        base.WndProc(ref message);
    }
    public void Render(FoodPlacement? food, double time)
    {
        if (food is not FoodPlacement snack) { if (Visible) Hide(); return; }
        var position = new Point((int)Math.Round(snack.X - 30), (int)Math.Round(snack.Y - 30));
        using var g = Graphics.FromImage(frame);
        g.Clear(Color.Transparent);
        using (var shadow = new SolidBrush(Color.FromArgb(55, 30, 40, 30)))
            g.FillEllipse(shadow, 13, 49, 34, 5);
        FoodSprites.Draw(g, snack.Kind, 6, 4 + (int)(Math.Sin(time * 4) * 2), 3);
        if (!Visible) { Location = position; Show(); }
        LayeredWindow.Present(Handle, frame, position);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) frame.Dispose();
        base.Dispose(disposing);
    }
}
