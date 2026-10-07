namespace Slime.Core;

public enum SlimeState { Spawning, Active, Melting, Hidden }

public readonly record struct DesktopArea(double Left, double Top, double Width, double Height);
public readonly record struct ReactionPose(double Width, double Height, double Lift, double Tilt);

/// <summary>Animation and lifecycle use elapsed seconds, independently of frame rate.</summary>
public sealed class SlimeModel
{
    public const int WindowWidth = 196;
    public const int WindowHeight = 236;
    public const int RequiredClicks = 30;
    public const double MeltDuration = 1.15;
    public const double RespawnDelay = 10;
    public const double SpawnDuration = 0.65;
    public const double ReactionDuration = 0.75;
    public const double AngryDuration = 4;
    public const double CalmDuration = 2.5;

    private readonly Random random;
    private DesktopArea area;
    private double directionTime;
    private double velocityX;
    private double velocityY;

    public SlimeModel(IReadOnlyList<DesktopArea> screens, int? seed = null)
    {
        random = seed is int value ? new Random(value) : new Random();
        Place(screens);
    }

    public SlimeState State { get; private set; } = SlimeState.Spawning;
    public int ClickCount { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public double Time { get; private set; }
    public double StateTime { get; private set; }
    public double HitAge { get; private set; } = 100;
    public bool IsHeld { get; private set; }
    public double AngerRemaining { get; private set; }
    public double AngerLevel => Math.Clamp(AngerRemaining / CalmDuration, 0, 1);
    public ReactionPose Reaction
    {
        get
        {
            if (HitAge >= ReactionDuration) return new(1, 1, 0, 0);
            if (HitAge < 0.12)
            {
                var press = Math.Sin(HitAge / 0.12 * Math.PI / 2);
                return new(1 + press * 0.36, 1 - press * 0.30, 0, 0);
            }
            if (HitAge < 0.44)
            {
                var p = (HitAge - 0.12) / 0.32;
                var bounce = Math.Sin(p * Math.PI);
                // Begin stretched from the squash, then spring into the air.
                var release = Math.Max(0, 1 - p * 5);
                return new(1 + release * 0.36 - bounce * 0.22,
                    1 - release * 0.30 + bounce * 0.26,
                    bounce * 22, Math.Sin(p * Math.PI * 2) * 6);
            }
            var settle = (HitAge - 0.44) / (ReactionDuration - 0.44);
            var wobble = Math.Sin(settle * Math.PI * 3) * (1 - settle);
            return new(1 + wobble * 0.09, 1 - wobble * 0.07, 0, wobble * 3);
        }
    }
    public bool FacingRight => velocityX >= 0;
    public double Progress => State switch
    {
        SlimeState.Melting => Math.Clamp(StateTime / MeltDuration, 0, 1),
        SlimeState.Spawning => Math.Clamp(StateTime / SpawnDuration, 0, 1),
        _ => 0
    };

    public bool Click()
    {
        if (State is SlimeState.Melting or SlimeState.Hidden) return false;
        ClickCount++;
        HitAge = 0;
        if (ClickCount == RequiredClicks)
        {
            State = SlimeState.Melting;
            StateTime = 0;
        }
        return true;
    }

    public bool BeginHold()
    {
        if (State is SlimeState.Hidden or SlimeState.Melting) return false;
        IsHeld = true;
        return true;
    }

    public void EndHold() => IsHeld = false;

    public void DragTo(double x, double y, IReadOnlyList<DesktopArea> screens)
    {
        if (!IsHeld || !double.IsFinite(x) || !double.IsFinite(y)) return;
        if (screens.Count == 0) throw new ArgumentException("At least one screen is required.", nameof(screens));
        // Pick the nearest monitor using the body center, including monitors separated by gaps.
        var centerX = x + WindowWidth / 2.0;
        var centerY = y + 150;
        area = screens.MinBy(screen =>
        {
            var dx = centerX - Math.Clamp(centerX, screen.Left, screen.Left + screen.Width);
            var dy = centerY - Math.Clamp(centerY, screen.Top, screen.Top + screen.Height);
            return dx * dx + dy * dy;
        });
        X = Math.Clamp(x, area.Left, area.Left + Math.Max(0, area.Width - WindowWidth));
        Y = Math.Clamp(y, area.Top, area.Top + Math.Max(0, area.Height - WindowHeight));
    }

    public void Update(double seconds, IReadOnlyList<DesktopArea> screens)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        Time += seconds;
        HitAge += seconds;
        // A disconnected monitor must never strand the pet outside the visible desktop.
        if (!screens.Contains(area)) Place(screens);
        var remaining = seconds;
        while (remaining > 0)
        {
            var duration = State switch
            {
                SlimeState.Spawning => SpawnDuration,
                SlimeState.Melting => MeltDuration,
                SlimeState.Hidden => RespawnDelay,
                _ => double.PositiveInfinity
            };
            var step = Math.Min(remaining, duration - StateTime);
            if (State is SlimeState.Spawning or SlimeState.Active)
                AngerRemaining = Math.Max(0, AngerRemaining - step);
            if (State == SlimeState.Active && !IsHeld && HitAge >= ReactionDuration) Move(Math.Min(step, 0.1));
            StateTime += step;
            remaining -= step;
            if (StateTime < duration) break;
            StateTime = 0;
            switch (State)
            {
                case SlimeState.Spawning: State = SlimeState.Active; break;
                case SlimeState.Melting: State = SlimeState.Hidden; break;
                case SlimeState.Hidden:
                    ClickCount = 0;
                    HitAge = 100;
                    AngerRemaining = AngryDuration;
                    Place(screens);
                    State = SlimeState.Spawning;
                    break;
            }
        }
    }

    private void Place(IReadOnlyList<DesktopArea> screens)
    {
        if (screens.Count == 0) throw new ArgumentException("At least one screen is required.", nameof(screens));
        area = screens[random.Next(screens.Count)];
        X = area.Left + random.NextDouble() * Math.Max(0, area.Width - WindowWidth);
        Y = area.Top + random.NextDouble() * Math.Max(0, area.Height - WindowHeight);
        ChooseDirection();
    }

    private void ChooseDirection()
    {
        var angle = random.NextDouble() * Math.PI * 2;
        var speed = 20 + random.NextDouble() * 18;
        velocityX = Math.Cos(angle) * speed;
        velocityY = Math.Sin(angle) * speed * 0.55;
        directionTime = 2 + random.NextDouble() * 4;
    }

    private void Move(double seconds)
    {
        directionTime -= seconds;
        if (directionTime <= 0) ChooseDirection();
        X += velocityX * seconds;
        Y += velocityY * seconds;
        var right = area.Left + Math.Max(0, area.Width - WindowWidth);
        var bottom = area.Top + Math.Max(0, area.Height - WindowHeight);
        if (X < area.Left || X > right) velocityX = -velocityX;
        if (Y < area.Top || Y > bottom) velocityY = -velocityY;
        X = Math.Clamp(X, area.Left, right);
        Y = Math.Clamp(Y, area.Top, bottom);
    }
}
