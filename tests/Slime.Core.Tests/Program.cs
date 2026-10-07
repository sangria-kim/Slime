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
slime.Update(0.1, screens);
Check(slime.X != beforeX || slime.Y != beforeY, "An active slime crawls");
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
Console.WriteLine($"\nAll {passed} checks passed.");
