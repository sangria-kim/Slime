namespace Slime.Core;

public enum SlimeState { Spawning, Active, Melting, Hidden }

public readonly record struct DesktopArea(double Left, double Top, double Width, double Height);

/// <summary>Animation and lifecycle use elapsed seconds, independently of frame rate.</summary>
public sealed class SlimeModel
{
    public const int WindowWidth = 196;
    public const int WindowHeight = 156;
    public const int RequiredClicks = 5;
    public const double MeltDuration = 1.15;
    public const double RespawnDelay = 10;
    public const double SpawnDuration = 0.65;

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
            if (State == SlimeState.Active) Move(Math.Min(step, 0.1));
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
