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
        var struggle = model.Struggle;
        var reactionStrength = model.State == SlimeState.Melting
            ? 1 - Smooth(Math.Clamp(model.StateTime / 0.35, 0, 1)) : 1;
        width *= 1 + (reaction.Width - 1) * reactionStrength;
        height *= 1 + (reaction.Height - 1) * reactionStrength;
        width *= struggle.Width;
        height *= struggle.Height;
        width *= model.SizeMultiplier;
        height *= model.SizeMultiplier;
        var lift = (reaction.Lift * reactionStrength + struggle.Lift) * model.SizeMultiplier + model.HopHeight;
        if (model.HopHeight > 0)
        {
            var stretch = model.HopHeight / SlimeModel.RedHopHeight;
            width *= 1 - stretch * 0.10;
            height *= 1 + stretch * 0.12;
        }
        opacity *= model.BlinkOpacity;

        Color Tint(int red, int green, int blue, double alpha = 1) =>
            Color.FromArgb((int)Math.Clamp(255 * alpha * opacity, 0, 255), red, green, blue);
        var anger = model.AngerLevel;
        Color BodyTint(int red, int green, int blue, int angryRed, int angryGreen, int angryBlue, double alpha = 1) =>
            model.Mood switch
            {
                SlimeMood.Yellow => Tint(Math.Min(255, red + 85), Math.Min(245, green), blue, alpha),
                SlimeMood.Orange => Tint(Math.Min(255, angryRed + 18), (green + angryGreen) / 2, blue / 2, alpha),
                SlimeMood.Red => Tint(angryRed, angryGreen, angryBlue, alpha),
                _ => Tint(red, green, blue, alpha)
            };

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
        var sway = (float)(Math.Sin(time * 3.2) * 3 + reaction.Tilt * reactionStrength + struggle.Tilt * 0.6);
        using var antenna = new GraphicsPath();
        antenna.AddBezier(-8, -85, -23 + sway, -110, -40 + sway, -95, -38, -65);
        antenna.AddBezier(-38, -65, -39, -43, -38 + sway, -22, -49 + sway, -17);

        using var transform = new Matrix();
        transform.Translate(98, 181 - (float)lift);
        transform.Rotate((float)(reaction.Tilt * reactionStrength + struggle.Tilt));
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
        if (model.IsDragging || model.HitAge < 0.44)
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
        if (model.IsDragging || model.HitAge < 0.44)
        {
            graphics.FillEllipse(face, 3 + look, -33, 10, 12);
            using var tongue = new SolidBrush(Tint(250, 156, 170));
            graphics.FillEllipse(tongue, 5 + look, -26, 6, 4);
        }
        else if (anger > 0)
        {
            graphics.DrawArc(facePen, 2 + look, -28, 12, 3 + (float)anger * 7, 180, 180);
            if (model.Mood == SlimeMood.Red)
            {
                graphics.DrawLine(facePen, 3 + look, -24, 7 + look, -21);
                graphics.DrawLine(facePen, 7 + look, -21, 12 + look, -24);
            }
        }
        else
        {
            graphics.DrawArc(facePen, 2 + look, -36, 6, 7, 0, 160);
            graphics.DrawArc(facePen, 8 + look, -37, 6, 7, 20, 160);
        }
        if (anger > 0 && model.HitAge >= 0.44 && !model.IsDragging)
        {
            using var brows = new Pen(Tint(70, 26, 20, 0.45 + anger * 0.55), 1.5f + (float)anger * 1.5f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawLine(brows, -22 + look, -49 - (float)anger * 4, -5 + look, -49 + (float)anger * 3);
            graphics.DrawLine(brows, 18 + look, -53 + (float)anger * 3, 33 + look, -53 - (float)anger * 5);
            if (model.Mood == SlimeMood.Red)
            {
                using var fury = new Pen(Tint(117, 28, 22), 2.2f)
                    { StartCap = LineCap.Round, EndCap = LineCap.Round };
                graphics.DrawLines(fury, [new PointF(20, -70), new PointF(25, -67), new PointF(28, -72)]);
                graphics.DrawLines(fury, [new PointF(30, -66), new PointF(26, -63), new PointF(29, -59)]);
            }
        }
        if (model.IsDragging)
        {
            using var motion = new Pen(Tint(239, 255, 212, 0.65), 2)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var flutter = (float)(Math.Sin(model.DragTime * 22) * 3);
            graphics.DrawArc(motion, -67, -46 + flutter, 13, 24, 130, 100);
            graphics.DrawArc(motion, 56, -48 - flutter, 13, 24, -50, 100);
        }
        graphics.Restore(saved);

        if (model.HitAge < 0.9)
        {
            var effectAlpha = 1 - Smooth(Math.Clamp((model.HitAge - 0.55) / 0.35, 0, 1));
            using var burst = new Pen(Tint(255, 220, 109, effectAlpha), 2.5f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var radius = (58 + Math.Min(model.HitAge / 0.5, 1) * 15) * model.SizeMultiplier;
            for (var i = 0; i < 7; i++)
            {
                var angle = (i + 0.5) * Math.PI * 2 / 7;
                var cx = 98.0;
                var cy = 181 - 45 * model.SizeMultiplier - lift;
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
                var x = 98 + side * (40 + travel * (24 + i * 3)) * model.SizeMultiplier;
                var y = 175 - Math.Sin(travel * Math.PI) * (12 + i * 3) * model.SizeMultiplier;
                var size = (1 - travel) * 6 * model.SizeMultiplier;
                graphics.FillEllipse(droplet, (float)x, (float)y, (float)size, (float)(size * 0.7));
            }
        }
        return hitShape;
    }

    private static double Smooth(double value) => value * value * (3 - 2 * value);
}
