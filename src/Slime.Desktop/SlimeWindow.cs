using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Slime.Core;

namespace Slime.Desktop;

internal sealed class SlimeWindow : Form
{
    private readonly SlimeModel model;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 33 };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly ContextMenuStrip menu = new();
    private readonly NotifyIcon tray;
    private readonly Icon petIcon;
    private readonly Bitmap frame = new(SlimeModel.WindowWidth, SlimeModel.WindowHeight, PixelFormat.Format32bppPArgb);
    private GraphicsPath? hitShape;
    private IReadOnlyList<DesktopArea> screens;
    private double lastFrame;
    private double lastScreenCheck;

    public SlimeWindow()
    {
        screens = ReadScreens();
        model = new SlimeModel(screens);
        Text = "Slime";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(SlimeModel.WindowWidth, SlimeModel.WindowHeight);
        StartPosition = FormStartPosition.Manual;
        Location = new Point((int)model.X, (int)model.Y);
        menu.Items.Add("슬라임 종료", null, (_, _) => Close());
        petIcon = CreatePetIcon();
        Icon = petIcon;
        tray = new NotifyIcon { Icon = petIcon, Text = "슬라임 · 5번 클릭하면 녹아요", ContextMenuStrip = menu, Visible = true };
        MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) model.Click();
            else if (e.Button == MouseButtons.Right) menu.Show(Cursor.Position);
        };
        timer.Tick += (_, _) => TickFrame();
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x80000 | 0x80 | 0x08000000; // Layered, tool window, no activation.
            return parameters;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        lastFrame = clock.Elapsed.TotalSeconds;
        Render();
        timer.Start();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x21) // WM_MOUSEACTIVATE: pet clicks never steal keyboard focus.
        {
            message.Result = (IntPtr)3;
            return;
        }
        if (message.Msg == 0x84) // WM_NCHITTEST: empty space and shadow pass clicks through.
        {
            var packed = message.LParam.ToInt64();
            var position = PointToClient(new Point((short)(packed & 0xffff), (short)((packed >> 16) & 0xffff)));
            message.Result = (IntPtr)(model.State != SlimeState.Hidden && hitShape?.IsVisible(position) == true ? 1 : -1);
            return;
        }
        base.WndProc(ref message);
    }

    private static IReadOnlyList<DesktopArea> ReadScreens() => Screen.AllScreens.Select(screen =>
    {
        var bounds = screen.WorkingArea;
        return new DesktopArea(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
    }).ToArray();

    private void TickFrame()
    {
        var now = clock.Elapsed.TotalSeconds;
        if (now - lastScreenCheck >= 1)
        {
            screens = ReadScreens();
            lastScreenCheck = now;
        }
        model.Update(now - lastFrame, screens);
        lastFrame = now;
        if (model.State == SlimeState.Hidden)
        {
            if (Visible) Hide();
            timer.Interval = 100;
            return;
        }
        timer.Interval = 33;
        if (!Visible) Show();
        Render();
    }

    private void Render()
    {
        using var graphics = Graphics.FromImage(frame);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        hitShape?.Dispose();
        hitShape = SlimePainter.Draw(graphics, model);
        LayeredWindow.Present(Handle, frame, new Point((int)Math.Round(model.X), (int)Math.Round(model.Y)));
    }

    private static Icon CreatePetIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var body = new SolidBrush(Color.FromArgb(155, 222, 73));
            using var eyes = new SolidBrush(Color.FromArgb(22, 33, 11));
            using var outline = new Pen(Color.FromArgb(30, 48, 16), 1);
            using var sparkle = new Pen(Color.FromArgb(255, 245, 104), 1);
            graphics.DrawBezier(outline, 13, 6, 5, -2, 3, 5, 4, 20);
            graphics.FillEllipse(body, 1, 19, 4, 4);
            using var shape = new GraphicsPath();
            shape.AddBezier(5, 26, 1, 18, 10, 10, 13, 5);
            shape.AddBezier(13, 5, 18, 12, 27, 10, 30, 21);
            shape.AddBezier(30, 21, 34, 33, 9, 33, 5, 26);
            graphics.FillPath(body, shape);
            graphics.DrawPath(outline, shape);
            graphics.FillEllipse(eyes, 11, 17, 5, 5);
            graphics.FillEllipse(eyes, 23, 16, 5, 5);
            graphics.DrawLine(sparkle, 13.5f, 18, 13.5f, 21);
            graphics.DrawLine(sparkle, 12, 19.5f, 15, 19.5f);
            graphics.DrawLine(sparkle, 25.5f, 17, 25.5f, 20);
            graphics.DrawLine(sparkle, 24, 18.5f, 27, 18.5f);
            graphics.DrawArc(outline, 17, 21, 4, 3, 0, 180);
        }
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Stop();
            timer.Dispose();
            tray.Visible = false;
            tray.Dispose();
            menu.Dispose();
            hitShape?.Dispose();
            frame.Dispose();
            petIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
