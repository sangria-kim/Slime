namespace Slime.Core;

public enum SlimeState { Spawning, Active, Melting, Hidden }
public enum SlimeMood { Green, Yellow, Orange, Red }
public enum FoodKind { Apple, Cheese, Meat, Cake, Doughnut }
public readonly record struct FoodPlacement(FoodKind Kind, double X, double Y);

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
    public const int FinalGeneration = 3;
    public const double RedTeleportInterval = 5;
    public const double RedBlinkDuration = 0.3;
    public const double RedHopHeight = 44;
    public const double TeleportDepartureDuration = 0.35;
    public const double TeleportArrivalDuration = 0.65;
    public const double EatingDuration = 1.2;

    private readonly Random random;
    private readonly double baseSpeed;
    private DesktopArea area;
    private double directionTime;
    private double velocityX;
    private double velocityY;

    public SlimeModel(IReadOnlyList<DesktopArea> screens, int? seed = null)
    {
        random = seed is int value ? new Random(value) : new Random();
        baseSpeed = 20 + random.NextDouble() * 18;
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
    public bool IsDragging { get; private set; }
    public double DragTime { get; private set; }
    public ReactionPose Struggle => IsDragging
        ? new(1 + Math.Sin(DragTime * 22) * 0.12,
            1 - Math.Sin(DragTime * 22) * 0.10,
            4 + Math.Abs(Math.Sin(DragTime * 17)) * 6,
            Math.Sin(DragTime * 18) * 12)
        : new(1, 1, 0, 0);
    public int Generation { get; private set; }
    public SlimeMood Mood => (SlimeMood)Generation;
    public double SizeMultiplier => Math.Pow(0.9, Generation);
    public double SpeedMultiplier => 1 + Generation * 0.5;
    public double AngerLevel => Generation / (double)FinalGeneration;
    public double MovementSpeed => Math.Sqrt(velocityX * velocityX + velocityY * velocityY);
    public double PersonalityTime { get; private set; }
    public bool IsCrying => State == SlimeState.Melting;
    public FoodPlacement? Food { get; private set; }
    public bool IsEating { get; private set; }
    public double EatingTime { get; private set; }
    public int FeedCount { get; private set; }
    public int TotalMeals { get; private set; }
    public double HappyRemaining { get; private set; }
    public bool CanOfferFood => State == SlimeState.Active && !IsHeld && Food is null && !IsEating && BlinkRemaining == 0;
    private bool CanMoveFreely => State == SlimeState.Active && !IsHeld && HitAge >= ReactionDuration && Food is null && !IsEating;
    public double CrawlMultiplier => !CanMoveFreely ? 0 : Mood switch
    {
        SlimeMood.Green => PersonalityTime % 6 is >= 4.8 and < 5.8 ? 0 : 1,
        SlimeMood.Yellow => PersonalityTime % 4 < 0.55 ? 0 : 1,
        SlimeMood.Orange => (PersonalityTime % 4.5) switch
        {
            >= 3.1 and < 3.7 => 0,
            >= 3.7 and < 4.15 => 3,
            _ => 1
        },
        _ => 1
    };
    public ReactionPose PersonalityPose
    {
        get
        {
            if (!CanMoveFreely) return new(1, 1, 0, 0);
            var t = PersonalityTime;
            switch (Mood)
            {
                case SlimeMood.Green:
                    var green = t % 6;
                    return green is >= 4.8 and < 5.8
                        ? new(1, 1, 0, Math.Sin((green - 4.8) * Math.PI) * 10)
                        : new(1 + Math.Sin(t * 4) * 0.04, 1 - Math.Sin(t * 4) * 0.03, 0, 0);
                case SlimeMood.Yellow:
                    var yellow = t % 4;
                    if (yellow < 0.55) return new(1.06, 0.94, 0, Math.Sin(yellow * 65) * 5);
                    var hop = yellow is >= 0.65 and < 1 ? (yellow - 0.65) / 0.35
                        : yellow is >= 1.1 and < 1.45 ? (yellow - 1.1) / 0.35 : 0;
                    var bounce = Math.Sin(hop * Math.PI);
                    return new(1 - bounce * 0.08, 1 + bounce * 0.1, bounce * 14, 0);
                case SlimeMood.Orange:
                    var orange = t % 4.5;
                    if (orange is >= 3.1 and < 3.7)
                    {
                        var charge = (orange - 3.1) / 0.6;
                        return new(1 + charge * 0.28, 1 - charge * 0.25, 0, 0);
                    }
                    if (orange is >= 3.7 and < 4.15)
                        return new(1.22, 0.84, 3, FacingRight ? 7 : -7);
                    return new(1, 1, 0, 0);
                default: return new(1, 1, 0, 0);
            }
        }
    }
    public double RedElapsed { get; private set; }
    public double BlinkRemaining { get; private set; }
    public int TeleportCount { get; private set; }
    public double TeleportDepartureProgress => CanShowTeleport && RedElapsed >= RedTeleportInterval - TeleportDepartureDuration
        ? (RedElapsed - (RedTeleportInterval - TeleportDepartureDuration)) / TeleportDepartureDuration : 0;
    public double TeleportArrivalProgress => CanShowTeleport && TeleportCount > 0 && RedElapsed < TeleportArrivalDuration
        ? RedElapsed / TeleportArrivalDuration : 1;
    private bool CanShowTeleport => Mood == SlimeMood.Red && !IsHeld && Food is null && !IsEating
        && (State is SlimeState.Active or SlimeState.Spawning);
    public double BlinkOpacity => BlinkRemaining > RedBlinkDuration / 2 ? 0
        : 1 - BlinkRemaining / (RedBlinkDuration / 2);
    public double HopHeight => Mood == SlimeMood.Red && State == SlimeState.Active && !IsHeld && Food is null && !IsEating
        ? Math.Abs(Math.Sin(RedElapsed * Math.PI / 0.65)) * RedHopHeight : 0;
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
        if (State is SlimeState.Melting or SlimeState.Hidden || BlinkOpacity == 0) return false;
        ClickCount++;
        HitAge = 0;
        if (ClickCount == RequiredClicks)
        {
            State = SlimeState.Melting;
            StateTime = 0;
            CancelFood();
        }
        return true;
    }

    public bool BeginHold()
    {
        if (State is SlimeState.Hidden or SlimeState.Melting || BlinkOpacity == 0) return false;
        IsHeld = true;
        return true;
    }

    public void EndHold()
    {
        IsHeld = false;
        IsDragging = false;
        DragTime = 0;
    }

    public void DragTo(double x, double y, IReadOnlyList<DesktopArea> screens)
    {
        if (!IsHeld || !double.IsFinite(x) || !double.IsFinite(y)) return;
        if (screens.Count == 0) throw new ArgumentException("At least one screen is required.", nameof(screens));
        IsDragging = true;
        CancelFood();
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
        HappyRemaining = Math.Max(0, HappyRemaining - seconds);
        if (IsDragging) DragTime += seconds;
        // A disconnected monitor must never strand the pet outside the visible desktop.
        if (!screens.Contains(area)) { CancelFood(); Place(screens); }
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
            if ((State is SlimeState.Spawning or SlimeState.Active) && Mood == SlimeMood.Red && !IsHeld && Food is null && !IsEating)
                UpdateRedTiming(step, screens);
            if (State == SlimeState.Active && !IsHeld && HitAge >= ReactionDuration && (Food is not null || IsEating))
                UpdateFeeding(step);
            else if (CanMoveFreely)
            {
                PersonalityTime += step;
                Move(Math.Min(step, 0.1) * CrawlMultiplier);
            }
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
                    FeedCount = 0;
                    HappyRemaining = 0;
                    CancelFood();
                    HitAge = 100;
                    Generation = (Generation + 1) % (FinalGeneration + 1);
                    RedElapsed = 0;
                    PersonalityTime = 0;
                    BlinkRemaining = 0;
                    TeleportCount = 0;
                    Place(screens);
                    State = SlimeState.Spawning;
                    break;
            }
        }
    }

    public bool OfferFood()
    {
        if (!CanOfferFood) return false;
        var angle = random.NextDouble() * Math.PI * 2;
        var x = Math.Clamp(X + 98 + Math.Cos(angle) * 140, area.Left + 30,
            area.Left + Math.Max(30, area.Width - 30));
        var y = Math.Clamp(Y + 165 + Math.Sin(angle) * 90, area.Top + 30,
            area.Top + Math.Max(30, area.Height - 30));
        Food = new((FoodKind)random.Next(5), x, y);
        return true;
    }

    private void CancelFood()
    {
        Food = null;
        IsEating = false;
        EatingTime = 0;
    }

    private void UpdateFeeding(double seconds)
    {
        if (!IsEating && Food is FoodPlacement food)
        {
            var targetX = Math.Clamp(food.X - 98, area.Left, area.Left + Math.Max(0, area.Width - WindowWidth));
            var targetY = Math.Clamp(food.Y - 165, area.Top, area.Top + Math.Max(0, area.Height - WindowHeight));
            var dx = targetX - X;
            var dy = targetY - Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            var speed = baseSpeed * SpeedMultiplier * 2;
            var travelTime = distance / speed;
            if (distance > 0)
            {
                var travel = Math.Min(distance, speed * seconds);
                X += dx / distance * travel;
                Y += dy / distance * travel;
                velocityX = dx / distance * baseSpeed * SpeedMultiplier;
                velocityY = dy / distance * baseSpeed * SpeedMultiplier;
            }
            if (seconds < travelTime) return;
            seconds -= travelTime;
            Food = null;
            IsEating = true;
            EatingTime = 0;
        }
        if (!IsEating) return;
        EatingTime += seconds;
        if (EatingTime < EatingDuration) return;
        IsEating = false;
        EatingTime = 0;
        TotalMeals++;
        FeedCount++;
        HappyRemaining = 1.5;
        if (FeedCount == 3)
        {
            FeedCount = 0;
            Generation = Math.Max(0, Generation - 1);
            RedElapsed = 0;
            BlinkRemaining = 0;
            TeleportCount = 0;
            PersonalityTime = 0;
            HappyRemaining = 2.5;
            ChooseDirection();
        }
    }

    private void UpdateRedTiming(double seconds, IReadOnlyList<DesktopArea> screens)
    {
        BlinkRemaining = Math.Max(0, BlinkRemaining - seconds);
        RedElapsed += seconds;
        if (RedElapsed < RedTeleportInterval) return;
        var events = Math.Floor(RedElapsed / RedTeleportInterval);
        RedElapsed %= RedTeleportInterval;
        TeleportCount = (int)Math.Min(int.MaxValue, TeleportCount + events);
        var previousX = X;
        var previousY = Y;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            Place(screens);
            if (Math.Abs(X - previousX) + Math.Abs(Y - previousY) >= 120) break;
        }
        // Preserve the elapsed remainder when a delayed frame crosses several five-second intervals.
        BlinkRemaining = Math.Max(0, RedBlinkDuration - RedElapsed);
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
        var speed = baseSpeed * SpeedMultiplier;
        var dx = Math.Cos(angle);
        var dy = Math.Sin(angle) * 0.55;
        var length = Math.Sqrt(dx * dx + dy * dy);
        velocityX = dx / length * speed;
        velocityY = dy / length * speed;
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
