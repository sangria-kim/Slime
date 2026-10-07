using Slime.Core;

var screens = new[] { new DesktopArea(0, 0, 1920, 1040) };
var passed = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    passed++;
    Console.WriteLine($"PASS: {message}");
}

var slime = new SlimeModel(screens, 42);
slime.Update(SlimeModel.SpawnDuration, screens);
Check(slime.State == SlimeState.Active, "Initial spawn becomes active");
Check(slime.AngerLevel == 0, "Initial spawn starts calm and green");
for (var i = 0; i < 29; i++) slime.Click();
Check(slime.State == SlimeState.Active && slime.ClickCount == 29, "Twenty-nine clicks do not melt the slime");
slime.Click();
Check(slime.State == SlimeState.Melting && slime.ClickCount == 30, "Exactly the thirtieth click begins melting");
Check(!slime.Click() && slime.ClickCount == 30, "Clicks during melting are ignored");
slime.Update(SlimeModel.MeltDuration, screens);
Check(slime.State == SlimeState.Hidden, "Hidden only after the melt animation finishes");
Check(!slime.Click(), "Hidden slime cannot be clicked");
slime.Update(9.99, screens);
Check(slime.State == SlimeState.Hidden, "Still hidden 9.99 seconds after disappearance");
slime.Update(0.02, screens);
Check(slime.State == SlimeState.Spawning && slime.ClickCount == 0, "Respawns at ten seconds with a reset click count");
slime.Update(SlimeModel.SpawnDuration, screens);
Check(slime.State == SlimeState.Active, "Respawn completes");

var quick = new SlimeModel(screens, 1);
for (var i = 0; i < SlimeModel.RequiredClicks; i++) quick.Click();
Check(quick.State == SlimeState.Melting, "Thirty rapid clicks during spawn also melt the slime");
quick.Update(SlimeModel.MeltDuration + SlimeModel.RespawnDelay + SlimeModel.SpawnDuration + 0.1, screens);
Check(quick.State == SlimeState.Active && quick.ClickCount == 0, "Elapsed time crossing multiple phases is preserved");

var exact = new SlimeModel(screens, 2);
for (var i = 0; i < SlimeModel.RequiredClicks; i++) exact.Click();
exact.Update(SlimeModel.MeltDuration, screens);
exact.Update(10, screens);
Check(exact.State == SlimeState.Spawning && exact.StateTime == 0, "Exactly ten seconds after hiding starts the spawn");

var beforeX = slime.X;
var beforeY = slime.Y;
slime.Update(0.6, screens);
Check(slime.X != beforeX || slime.Y != beforeY, "An active slime moves after its short personality pause");
for (var i = 0; i < 30000; i++)
{
    slime.Update(1.0 / 30, screens);
    if (slime.X < 0 || slime.Y < 0 || slime.X > 1920 - SlimeModel.WindowWidth || slime.Y > 1040 - SlimeModel.WindowHeight)
        throw new Exception("Slime escaped the desktop working area");
}
Check(true, "Long-running movement stays inside the desktop working area");

var replacement = new[] { new DesktopArea(-1280, -200, 1280, 900) };
slime.Update(0.1, replacement);
Check(slime.X >= -1280 && slime.X <= -SlimeModel.WindowWidth && slime.Y >= -200 && slime.Y <= 700 - SlimeModel.WindowHeight,
    "A disconnected monitor relocates the slime, including negative coordinates");
var tiny = new[] { new DesktopArea(0, 0, 100, 100) };
slime.Update(0.1, tiny);
Check(slime.X == 0 && slime.Y == 0, "Small working areas do not cause invalid random ranges");
for (var cycle = 0; cycle < 20; cycle++)
{
    for (var click = 0; click < SlimeModel.RequiredClicks; click++) slime.Click();
    slime.Update(SlimeModel.MeltDuration + SlimeModel.RespawnDelay + SlimeModel.SpawnDuration + 0.1, tiny);
    if (slime.State != SlimeState.Active || slime.ClickCount != 0) throw new Exception("Repeated lifecycle failed");
}
Check(true, "Twenty repeated melt/respawn cycles reset correctly");

var dragged = new SlimeModel(screens, 7);
dragged.Update(SlimeModel.SpawnDuration, screens);
var pointer = new PointerInteraction(dragged);
var originX = dragged.X;
var originY = dragged.Y;
Check(pointer.Press(originX + 80, originY + 150), "Visible slime can be grabbed");
dragged.Update(0.1, screens);
Check(dragged.X == originX && dragged.Y == originY, "Holding the mouse pauses crawling");
pointer.Move(originX + 82, originY + 152, screens);
Check(!pointer.IsDragging, "Small mouse jitter remains a click");
Check(!dragged.IsDragging && dragged.Struggle == new ReactionPose(1, 1, 0, 0),
    "Holding and small mouse jitter do not start the struggle animation");
Check(pointer.Release(originX + 82, originY + 152, screens) && dragged.ClickCount == 1,
    "Release without dragging counts one click");
dragged.Update(0.1, screens);
Check(dragged.Reaction.Width > 1.3 && dragged.Reaction.Height < 0.75, "Click produces a large squash");
dragged.Update(0.18, screens);
Check(dragged.Reaction.Lift > 20 && dragged.Reaction.Height > 1.2, "Squash springs into a visible jump");
dragged.Update(SlimeModel.ReactionDuration, screens);
Check(dragged.Reaction == new ReactionPose(1, 1, 0, 0), "Reaction settles back to normal");
pointer.Press(dragged.X + 80, dragged.Y + 150);
pointer.Move(500, 450, screens);
Check(pointer.IsDragging && dragged.X == 420 && dragged.Y == 300, "Dragging preserves the grab offset");
dragged.Update(0.1, screens);
var firstStruggle = dragged.Struggle;
dragged.Update(0.1, screens);
Check(dragged.IsDragging && firstStruggle != dragged.Struggle && dragged.X == 420 && dragged.Y == 300,
    "Struggling continues while held without drifting away from the drag position");
pointer.Move(501, 451, screens);
Check(dragged.X == 421 && dragged.Y == 301, "Repeated drag motion follows the pointer without drift");
Check(!pointer.Release(501, 451, screens) && dragged.ClickCount == 1 && !dragged.IsHeld,
    "Dragging never increments the melt click count");
Check(!dragged.IsDragging && dragged.DragTime == 0 && dragged.Struggle == new ReactionPose(1, 1, 0, 0),
    "Releasing a drag returns immediately to the normal pose");
var multi = new[] { screens[0], new DesktopArea(-1280, -200, 1280, 900) };
pointer.Press(dragged.X + 80, dragged.Y + 150);
pointer.Move(-500, 300, multi);
Check(dragged.X == -580 && dragged.Y == 150, "Dragging can move to a monitor with negative coordinates");
pointer.Move(-3000, -3000, multi);
Check(dragged.X == -1280 && dragged.Y == -200, "Dragging clamps at the target monitor edge");
pointer.Cancel();
Check(!pointer.IsPressed && !dragged.IsHeld && dragged.ClickCount == 1, "Losing mouse capture cancels without clicking");
Check(!dragged.IsDragging && dragged.Struggle == new ReactionPose(1, 1, 0, 0),
    "Losing mouse capture also stops struggling");
dragged.Update(0.1, multi);
Check(dragged.X != -1280 || dragged.Y != -200, "Crawling resumes after the grab ends");
for (var i = 0; i < SlimeModel.RequiredClicks - 1; i++) dragged.Click();
Check(dragged.State == SlimeState.Melting && !pointer.Press(0, 0), "Melting slime cannot be grabbed");
dragged.Update(SlimeModel.MeltDuration, multi);
Check(!pointer.Press(0, 0), "Hidden slime cannot be grabbed");
dragged.Update(SlimeModel.RespawnDelay, multi);
Check(dragged.ClickCount == 0 && pointer.Press(dragged.X + 80, dragged.Y + 150),
    "A respawned slime can be grabbed again");
pointer.Cancel();
var evolving = new SlimeModel(screens, 19);
evolving.Update(SlimeModel.SpawnDuration, screens);
var initialSpeed = evolving.MovementSpeed;
Check(evolving.Mood == SlimeMood.Green && evolving.SizeMultiplier == 1 && evolving.AngerLevel == 0,
    "Initial slime is full-size, green and happy");
var moods = new[] { SlimeMood.Yellow, SlimeMood.Orange, SlimeMood.Red };
var expectedSizes = new[] { 0.9, 0.81, 0.729 };
var expectedSpeeds = new[] { 1.5, 2.0, 2.5 };
for (var stage = 0; stage < 3; stage++)
{
    var oldSize = evolving.SizeMultiplier;
    var oldAnger = evolving.AngerLevel;
    for (var click = 0; click < SlimeModel.RequiredClicks; click++) evolving.Click();
    evolving.Update(SlimeModel.MeltDuration, screens);
    Check(evolving.Generation == stage, "Generation stays unchanged while hidden");
    evolving.Update(SlimeModel.RespawnDelay, screens);
    Check(evolving.Mood == moods[stage] && evolving.ClickCount == 0,
        $"Respawn {stage + 1} has the expected color and a reset click count");
    Check(Math.Abs(evolving.SizeMultiplier - expectedSizes[stage]) < 1e-10 &&
          Math.Abs(evolving.SizeMultiplier / oldSize - 0.9) < 1e-10,
        $"Respawn {stage + 1} shrinks exactly ten percent from the previous size");
    Check(Math.Abs(evolving.MovementSpeed / initialSpeed - expectedSpeeds[stage]) < 1e-10 &&
          Math.Abs(evolving.SpeedMultiplier - expectedSpeeds[stage]) < 1e-10,
        $"Respawn {stage + 1} uses the requested movement speed multiplier");
    Check(evolving.AngerLevel > oldAnger, $"Respawn {stage + 1} is angrier than the previous stage");
    evolving.Update(30, screens);
    Check(evolving.Mood == moods[stage] && evolving.AngerLevel > 0,
        $"Stage {stage + 1} stays in its new mood instead of calming back to green");
    for (var tick = 0; tick < 200; tick++) evolving.Update(0.05, screens);
    Check(Math.Abs(evolving.MovementSpeed / initialSpeed - expectedSpeeds[stage]) < 1e-10,
        $"Stage {stage + 1} keeps its speed when changing direction or bouncing");
}
for (var click = 0; click < SlimeModel.RequiredClicks; click++) evolving.Click();
evolving.Update(SlimeModel.MeltDuration + SlimeModel.RespawnDelay + SlimeModel.SpawnDuration + 0.1, screens);
Check(evolving.Generation == 0 && evolving.Mood == SlimeMood.Green && evolving.AngerLevel == 0 &&
      evolving.SizeMultiplier == 1 && evolving.SpeedMultiplier == 1 && evolving.ClickCount == 0 &&
      Math.Abs(evolving.MovementSpeed - initialSpeed) < 1e-10,
    "Red respawns as happy green with original size, speed and reset clicks");
for (var cycle = 0; cycle < 3; cycle++)
{
    for (var stage = 1; stage <= 4; stage++)
    {
        for (var click = 0; click < SlimeModel.RequiredClicks; click++) evolving.Click();
        evolving.Update(SlimeModel.MeltDuration + SlimeModel.RespawnDelay + SlimeModel.SpawnDuration + 0.1, screens);
        if (evolving.Generation != stage % 4 || evolving.ClickCount != 0)
            throw new Exception("Repeated color cycle failed");
    }
}
Check(evolving.Mood == SlimeMood.Green && evolving.SizeMultiplier == 1 && evolving.SpeedMultiplier == 1,
    "Three more complete green/yellow/orange/red cycles return to the original green state");
var fresh = new SlimeModel(screens, 19);
Check(fresh.Generation == 0 && fresh.Mood == SlimeMood.Green, "Restarting begins again at the happy green stage");
var red = new SlimeModel(screens, 73);
for (var stage = 0; stage < 3; stage++)
{
    for (var click = 0; click < SlimeModel.RequiredClicks; click++) red.Click();
    red.Update(SlimeModel.MeltDuration, screens);
    red.Update(SlimeModel.RespawnDelay, screens);
}
Check(red.Mood == SlimeMood.Red && red.RedElapsed == 0 && red.TeleportCount == 0,
    "Red teleport clock starts at reappearance");
Check(red.TeleportDepartureProgress == 0 && red.TeleportArrivalProgress == 1,
    "First red spawn does not show a false teleport effect");
red.Update(0.975, screens);
Check(Math.Abs(red.HopHeight - 44) < 1e-10, "Red continuously hops to 44 pixels without a click");
red.Click();
red.Update(4.024, screens);
Check(red.TeleportCount == 0, "Red does not teleport before five seconds");
Check(red.TeleportDepartureProgress > 0.99 && red.TeleportArrivalProgress == 1,
    "Departure spell builds before relocation");
red.Update(0.002, screens);
Check(red.TeleportCount == 1 && red.BlinkOpacity == 0 && red.Mood == SlimeMood.Red && red.ClickCount == 1,
    "Five-second teleport briefly disappears without changing color or click count");
Check(red.TeleportDepartureProgress == 0 && red.TeleportArrivalProgress < 0.01,
    "Arrival spell remains visible while the slime disappears");
Check(!red.Click() && !red.BeginHold(), "Invisible red slime cannot be clicked or grabbed");
var teleportX = red.X;
var teleportY = red.Y;
red.Update(0.3, screens);
Check(red.BlinkRemaining == 0 && red.BlinkOpacity == 1, "Red reappears after the brief blink");
Check(red.TeleportArrivalProgress > 0 && red.TeleportArrivalProgress < 1,
    "Arrival light fades after the slime reappears");
red.Update(4.7, screens);
Check(red.TeleportCount == 2 && (red.X != teleportX || red.Y != teleportY),
    "Next five-second teleport moves to another location");
red.Update(0.3, screens);
red.BeginHold();
Check(red.TeleportDepartureProgress == 0 && red.TeleportArrivalProgress == 1,
    "Grabbing interrupts teleport visuals");
var heldCount = red.TeleportCount;
var heldElapsed = red.RedElapsed;
red.Update(10, screens);
Check(red.TeleportCount == heldCount && red.RedElapsed == heldElapsed && red.HopHeight == 0,
    "Dragging pauses red hopping and teleporting");
red.EndHold();
red.Update(15, screens);
Check(red.TeleportCount == heldCount + 3 && red.BlinkRemaining == 0,
    "Long elapsed updates preserve teleport intervals without a stale invisible state");
for (var i = red.ClickCount; i < SlimeModel.RequiredClicks; i++) red.Click();
var meltTeleportCount = red.TeleportCount;
red.Update(SlimeModel.MeltDuration, screens);
Check(red.State == SlimeState.Hidden && red.TeleportCount == meltTeleportCount,
    "Thirty clicks still melt red normally and stop automatic teleporting");
Check(red.TeleportDepartureProgress == 0 && red.TeleportArrivalProgress == 1,
    "Melting and hidden slime do not display teleport light");
red.Update(SlimeModel.RespawnDelay, screens);
Check(red.Mood == SlimeMood.Green && red.BlinkRemaining == 0 && red.TeleportCount == 0 && red.HopHeight == 0,
    "Red-to-green respawn resets hopping and teleport state");
for (var stage = 0; stage <= 3; stage++)
{
    var pet = new SlimeModel(screens, 101 + stage);
    for (var generation = 0; generation < stage; generation++)
    {
        for (var click = 0; click < SlimeModel.RequiredClicks; click++) pet.Click();
        pet.Update(SlimeModel.MeltDuration + SlimeModel.RespawnDelay, screens);
    }
    pet.Update(SlimeModel.SpawnDuration, screens);
    if (stage == 0)
    {
        pet.Update(4.9, screens);
        var pauseX = pet.X;
        var pauseY = pet.Y;
        pet.Update(0.3, screens);
        Check(pet.CrawlMultiplier == 0 && pet.PersonalityPose.Tilt > 0 && pet.X == pauseX && pet.Y == pauseY,
            "Green pauses in place to tilt its head");
        pet.Update(1, screens);
        Check(pet.CrawlMultiplier == 1 && (pet.X != pauseX || pet.Y != pauseY),
            "Green resumes crawling after looking around");
    }
    else if (stage == 1)
    {
        pet.Update(0.2, screens);
        Check(pet.CrawlMultiplier == 0 && pet.PersonalityPose.Tilt != 0,
            "Yellow stops to tremble before hopping");
        pet.Update(0.6, screens);
        Check(pet.PersonalityPose.Lift > 10 && pet.CrawlMultiplier == 1, "Yellow makes its first small moving hop");
        pet.Update(0.45, screens);
        Check(pet.PersonalityPose.Lift > 10, "Yellow makes its second small hop");
        pet.Update(0.4, screens);
        Check(pet.PersonalityPose.Lift == 0, "Yellow lands after the pair of hops");
    }
    else if (stage == 2)
    {
        pet.Update(3.4, screens);
        Check(pet.CrawlMultiplier == 0 && pet.PersonalityPose.Width > 1 && pet.PersonalityPose.Height < 1,
            "Orange squashes in place to charge a dash");
        pet.Update(0.4, screens);
        Check(pet.CrawlMultiplier == 3 && pet.PersonalityPose.Tilt != 0,
            "Orange releases its charge into a fast leaning dash");
        pet.Update(0.5, screens);
        Check(pet.CrawlMultiplier == 1, "Orange returns to normal speed after the dash");
    }
    var personalityTime = pet.PersonalityTime;
    pet.BeginHold();
    pet.Update(0.2, screens);
    Check(pet.PersonalityTime == personalityTime && pet.CrawlMultiplier == 0
        && pet.PersonalityPose == new ReactionPose(1, 1, 0, 0), $"Stage {stage} pauses its personality while held");
    pet.EndHold();
    for (var click = 0; click < SlimeModel.RequiredClicks; click++) pet.Click();
    Check(pet.IsCrying && pet.CrawlMultiplier == 0, $"Stage {stage} starts crying at the thirtieth click");
    pet.Update(0.6, screens);
    Check(pet.IsCrying, $"Stage {stage} keeps crying throughout melting");
    pet.Update(SlimeModel.MeltDuration - 0.6 + SlimeModel.RespawnDelay, screens);
    Check(!pet.IsCrying && pet.PersonalityTime == 0, $"Stage {stage} respawns without crying and resets movement timing");
}
SlimeModel Hungry(int stage, int seed = 207)
{
    var pet = new SlimeModel(screens, seed);
    for (var i = 0; i < stage; i++)
    {
        for (var click = 0; click < SlimeModel.RequiredClicks; click++) pet.Click();
        pet.Update(SlimeModel.MeltDuration + SlimeModel.RespawnDelay, screens);
    }
    pet.Update(SlimeModel.SpawnDuration, screens);
    return pet;
}
void FinishMeal(SlimeModel pet)
{
    var expected = pet.TotalMeals + 1;
    for (var frame = 0; frame < 600 && pet.TotalMeals < expected; frame++) pet.Update(1.0 / 30, screens);
    Check(pet.TotalMeals == expected && !pet.IsEating && pet.Food is null, "Dropped food is approached and eaten exactly once");
}
var feeding = Hungry(3);
for (var click = 0; click < 7; click++) feeding.Click();
feeding.Update(SlimeModel.ReactionDuration, screens);
for (var stage = 3; stage >= 0; stage--)
{
    for (var meal = 0; meal < 3; meal++)
    {
        Check(feeding.OfferFood(), "An available slime accepts one random snack");
        Check(!feeding.OfferFood(), "Another snack cannot replace pending food");
        var food = feeding.Food!.Value;
        Check(food.X >= 30 && food.X <= screens[0].Width - 30 && food.Y >= 30 && food.Y <= screens[0].Height - 30,
            "Food stays inside the desktop working area");
        var oldClock = feeding.RedElapsed;
        feeding.Update(0.01, screens);
        Check(feeding.RedElapsed == oldClock && feeding.HopHeight == 0, "Feeding pauses automatic red teleport and hopping");
        FinishMeal(feeding);
        var expectedStage = meal < 2 ? stage : Math.Max(0, stage - 1);
        Check(feeding.Generation == expectedStage && feeding.FeedCount == (meal + 1) % 3,
            "Only every third completed meal calms one stage, with green as the lower limit");
        Check(feeding.ClickCount == 7 && feeding.HappyRemaining > 0, "A meal shows happiness without clearing melt clicks");
    }
    Check(Math.Abs(feeding.SizeMultiplier - Math.Pow(0.9, Math.Max(0, stage - 1))) < 1e-10
        && feeding.SpeedMultiplier == 1 + Math.Max(0, stage - 1) * 0.5, "Calming restores the stage size and speed");
}
var snacks = new HashSet<FoodKind>();
for (var meal = 0; meal < 70; meal++)
{
    feeding.OfferFood();
    snacks.Add(feeding.Food!.Value.Kind);
    feeding.Update(100, screens);
}
Check(snacks.Count == 5 && feeding.TotalMeals == 82, "Random snacks include all five kinds and a delayed frame consumes only one meal");
var interrupted = Hungry(2);
interrupted.OfferFood();
interrupted.BeginHold();
interrupted.Update(10, screens);
Check(interrupted.Food is not null && interrupted.TotalMeals == 0, "Holding pauses feeding without awarding a meal");
interrupted.DragTo(interrupted.X, interrupted.Y, screens);
interrupted.EndHold();
Check(interrupted.Food is null && interrupted.FeedCount == 0, "Dragging cancels food without counting it");
interrupted.OfferFood();
for (var frame = 0; frame < 600 && !interrupted.IsEating; frame++) interrupted.Update(1.0 / 30, screens);
Check(interrupted.IsEating && !interrupted.CanOfferFood && interrupted.FeedCount == 0, "Reaching food begins chewing before it counts");
for (var click = 0; click < SlimeModel.RequiredClicks; click++) interrupted.Click();
Check(interrupted.IsCrying && !interrupted.IsEating && interrupted.Food is null && !interrupted.OfferFood(),
    "Melting cancels an unfinished meal and refuses new food");
interrupted.Update(SlimeModel.MeltDuration + SlimeModel.RespawnDelay, screens);
Check(interrupted.FeedCount == 0 && interrupted.Food is null && !interrupted.OfferFood(), "Respawn starts with clean feeding progress and refuses food until active");
var disconnectedFood = Hungry(1);
disconnectedFood.OfferFood();
disconnectedFood.Update(0.01, replacement);
Check(disconnectedFood.Food is null && disconnectedFood.TotalMeals == 0, "Disconnected food monitor cancels the snack without credit");
Console.WriteLine($"\nAll {passed} checks passed.");
