using System.Drawing.Drawing2D;
using Slime.Core;

namespace Slime.Desktop;

internal static class SlimePainter
{
    // The pet is drawn directly, so there are no external sprites or image files to install.
    public static GraphicsPath Draw(Graphics graphics, SlimeModel model)
    {
        var time = model.Time;
        var wave = Math.Sin(time * 5.2);
        var width = 1 + wave * 0.045;
        var height = 1 - wave * 0.035;
        var opacity = 1.0;
        var melt = model.State == SlimeState.Melting
            ? Smooth(Math.Clamp((model.StateTime - 0.2) / (SlimeModel.MeltDuration - 0.2), 0, 1)) : 0;

        if (model.State == SlimeState.Spawning)
        {
            var p = model.Progress;
            var bounce = 1 + 2.70158 * Math.Pow(p - 1, 3) + 1.70158 * Math.Pow(p - 1, 2);
            width *= Math.Max(0.02, bounce);
            height *= Math.Max(0.02, bounce);
            opacity = Math.Min(1, p * 3);
        }
        else if (model.State == SlimeState.Melting)
        {
            width = 1 + melt * 0.63;
            height = 1 - melt * 0.93;
            opacity = 1 - Smooth(Math.Clamp((model.Progress - 0.68) / 0.32, 0, 1));
        }
        var reaction = model.Reaction;
        var reactionStrength = model.State == SlimeState.Melting
            ? 1 - Smooth(Math.Clamp(model.StateTime / 0.35, 0, 1)) : 1;
        width *= 1 + (reaction.Width - 1) * reactionStrength;
        height *= 1 + (reaction.Height - 1) * reactionStrength;
        var lift = reaction.Lift * reactionStrength;

        Color Tint(int red, int green, int blue, double alpha = 1) =>
            Color.FromArgb((int)Math.Clamp(255 * alpha * opacity, 0, 255), red, green, blue);
        var anger = Smooth(model.AngerLevel);
        Color BodyTint(int red, int green, int blue, int angryRed, int angryGreen, int angryBlue, double alpha = 1) =>
            Tint((int)(red + (angryRed - red) * anger), (int)(green + (angryGreen - green) * anger),
                (int)(blue + (angryBlue - blue) * anger), alpha);

        for (var i = 4; i >= 0; i--)
        {
            using var shadow = new SolidBrush(Tint(20, 57, 47, 0.028));
            graphics.FillEllipse(shadow, (float)(98 - 44 * width - i * 2), 178 - i,
                (float)(88 * width + i * 4), 13 + i * 2);
        }

        using var body = new GraphicsPath();
        body.StartFigure();
        body.AddBezier(-46, -9, -54, -36, -30, -58, -14, -77);
        body.AddBezier(-14, -77, -8, -91, -5, -89, 4, -76);
        body.AddBezier(4, -76, 16, -62, 34, -68, 46, -48);
        body.AddBezier(46, -48, 63, -20, 44, 1, 25, 4);
        body.AddBezier(25, 4, 6, 9, -31, 10, -46, -9);
        body.CloseFigure();

        // MapleStory's long curled feeler ends in a small green bead.
        var sway = (float)(Math.Sin(time * 3.2) * 3 + reaction.Tilt * reactionStrength);
        using var antenna = new GraphicsPath();
        antenna.AddBezier(-8, -85, -23 + sway, -110, -40 + sway, -95, -38, -65);
        antenna.AddBezier(-38, -65, -39, -43, -38 + sway, -22, -49 + sway, -17);

        using var transform = new Matrix();
        transform.Translate(98, 181 - (float)lift);
        transform.Rotate((float)(reaction.Tilt * reactionStrength));
        transform.Scale((float)width, (float)height);
        var hitShape = (GraphicsPath)body.Clone();
        hitShape.FillMode = FillMode.Winding;
        using (var antennaHit = (GraphicsPath)antenna.Clone())
        {
            using var hitPen = new Pen(Color.Black, 5);
            antennaHit.Widen(hitPen);
            hitShape.AddPath(antennaHit, false);
        }
        hitShape.AddEllipse(-55 + sway, -21, 10, 10);
        hitShape.Transform(transform);

        var saved = graphics.Save();
        graphics.MultiplyTransform(transform);
        using var darkOutline = new Pen(BodyTint(30, 48, 16, 93, 29, 22), 1.8f) { LineJoin = LineJoin.Round };
        graphics.DrawPath(darkOutline, antenna);
        using (var bead = new LinearGradientBrush(new PointF(-50 + sway, -21), new PointF(-50 + sway, -11),
                   BodyTint(207, 252, 126, 255, 185, 151), BodyTint(102, 177, 38, 210, 56, 43)))
            graphics.FillEllipse(bead, -55 + sway, -21, 10, 10);
        graphics.DrawEllipse(darkOutline, -55 + sway, -21, 10, 10);

        using (var fill = new LinearGradientBrush(new PointF(-20, -88), new PointF(20, 8),
                   BodyTint(225, 255, 162, 255, 201, 173), BodyTint(107, 193, 44, 218, 56, 44)))
        {
            fill.InterpolationColors = new ColorBlend
            {
                Colors = [BodyTint(224, 254, 159, 255, 201, 173), BodyTint(161, 224, 75, 255, 108, 83),
                    BodyTint(124, 207, 48, 226, 60, 47), BodyTint(157, 224, 89, 255, 131, 104)],
                Positions = [0, 0.35f, 0.75f, 1]
            };
            graphics.FillPath(fill, body);
        }
        graphics.DrawPath(darkOutline, body);

        using (var shine = new SolidBrush(Tint(251, 255, 232, 0.92)))
        {
            graphics.FillEllipse(shine, -32, -52, 12, 7);
            graphics.FillEllipse(shine, -37, -38, 7, 5);
        }
        using (var belly = new SolidBrush(BodyTint(224, 255, 167, 255, 199, 177, 0.24)))
            graphics.FillEllipse(belly, -29, -19, 67, 21);

        var look = model.FacingRight ? 3 : -3;
        var blink = time % 4.6 > 4.43;
        using var face = new SolidBrush(Tint(22, 33, 11));
        using var facePen = new Pen(Tint(22, 33, 11), 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        if (model.HitAge < 0.44)
        {
            graphics.DrawLines(facePen, [new PointF(-17 + look, -44), new PointF(-10 + look, -38), new PointF(-17 + look, -32)]);
            graphics.DrawLines(facePen, [new PointF(29 + look, -48), new PointF(22 + look, -42), new PointF(29 + look, -36)]);
        }
        else if (blink)
        {
            graphics.DrawLine(facePen, -17 + look, -38, -8 + look, -38);
            graphics.DrawLine(facePen, 20 + look, -42, 29 + look, -42);
        }
        else
        {
            graphics.FillEllipse(face, -18 + look, -44, 12, 12);
            graphics.FillEllipse(face, 19 + look, -48, 11, 12);
            using var glint = new Pen(Tint(255, 245, 104), 2.2f);
            graphics.DrawLine(glint, -12 + look, -42, -12 + look, -34);
            graphics.DrawLine(glint, -16 + look, -38, -8 + look, -38);
            graphics.DrawLine(glint, 24.5f + look, -46, 24.5f + look, -38);
            graphics.DrawLine(glint, 21 + look, -42, 28 + look, -42);
        }
        if (model.HitAge < 0.44)
        {
            graphics.FillEllipse(face, 3 + look, -33, 10, 12);
            using var tongue = new SolidBrush(Tint(250, 156, 170));
            graphics.FillEllipse(tongue, 5 + look, -26, 6, 4);
        }
        else if (anger > 0.15)
        {
            graphics.DrawArc(facePen, 2 + look, -28, 12, 8, 180, 180);
        }
        else
        {
            graphics.DrawArc(facePen, 2 + look, -36, 6, 7, 0, 160);
            graphics.DrawArc(facePen, 8 + look, -37, 6, 7, 20, 160);
        }
        if (anger > 0 && model.HitAge >= 0.44)
        {
            using var brows = new Pen(Tint(70, 26, 20, anger), 2.5f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawLine(brows, -22 + look, -53, -5 + look, -46);
            graphics.DrawLine(brows, 18 + look, -50, 33 + look, -58);
        }
        graphics.Restore(saved);

        if (model.HitAge < 0.9)
        {
            var effectAlpha = 1 - Smooth(Math.Clamp((model.HitAge - 0.55) / 0.35, 0, 1));
            using var burst = new Pen(Tint(255, 220, 109, effectAlpha), 2.5f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var radius = 58 + Math.Min(model.HitAge / 0.5, 1) * 15;
            for (var i = 0; i < 7; i++)
            {
                var angle = (i + 0.5) * Math.PI * 2 / 7;
                var cx = 98.0;
                var cy = 136 - lift;
                graphics.DrawLine(burst,
                    (float)(cx + Math.Cos(angle) * radius), (float)(cy + Math.Sin(angle) * radius * 0.6),
                    (float)(cx + Math.Cos(angle) * (radius + 8)), (float)(cy + Math.Sin(angle) * (radius + 8) * 0.6));
            }

        }

        if (model.ClickCount > 0 && model.State != SlimeState.Spawning)
        {
            const int columns = 10;
            for (var i = 0; i < SlimeModel.RequiredClicks; i++)
            {
                using var dot = new SolidBrush(i < model.ClickCount ? Tint(255, 213, 109) : Tint(238, 255, 241, 0.5));
                using var edge = new Pen(Tint(79, 132, 103, 0.7), 1.2f);
                var x = 55 + i % columns * 9;
                var y = 204 + i / columns * 9;
                graphics.FillEllipse(dot, x, y, 5, 5);
                graphics.DrawEllipse(edge, x, y, 5, 5);
            }
        }

        if (model.State == SlimeState.Melting)
        {
            using var droplet = new SolidBrush(BodyTint(149, 220, 65, 244, 91, 67));
            for (var i = 0; i < 4; i++)
            {
                var side = i < 2 ? -1 : 1;
                var travel = model.Progress;
                var x = 98 + side * (40 + travel * (24 + i * 3));
                var y = 175 - Math.Sin(travel * Math.PI) * (12 + i * 3);
                var size = (1 - travel) * 6;
                graphics.FillEllipse(droplet, (float)x, (float)y, (float)size, (float)(size * 0.7));
            }
        }
        return hitShape;
    }

    private static double Smooth(double value) => value * value * (3 - 2 * value);
}
