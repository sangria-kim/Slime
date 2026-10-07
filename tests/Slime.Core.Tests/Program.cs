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
for (var i = 0; i < 4; i++) slime.Click();
Check(slime.State == SlimeState.Active && slime.ClickCount == 4, "Four clicks do not melt the slime");
slime.Click();
Check(slime.State == SlimeState.Melting && slime.ClickCount == 5, "Exactly the fifth click begins melting");
Check(!slime.Click() && slime.ClickCount == 5, "Clicks during melting are ignored");
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
for (var i = 0; i < 5; i++) quick.Click();
Check(quick.State == SlimeState.Melting, "Five rapid clicks during spawn also melt the slime");
quick.Update(SlimeModel.MeltDuration + SlimeModel.RespawnDelay + SlimeModel.SpawnDuration + 0.1, screens);
Check(quick.State == SlimeState.Active && quick.ClickCount == 0, "Elapsed time crossing multiple phases is preserved");

var exact = new SlimeModel(screens, 2);
for (var i = 0; i < 5; i++) exact.Click();
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
    for (var click = 0; click < 5; click++) slime.Click();
    slime.Update(SlimeModel.MeltDuration + SlimeModel.RespawnDelay + SlimeModel.SpawnDuration + 0.1, tiny);
    if (slime.State != SlimeState.Active || slime.ClickCount != 0) throw new Exception("Repeated lifecycle failed");
}
Check(true, "Twenty repeated melt/respawn cycles reset correctly");
Console.WriteLine($"\nAll {passed} checks passed.");
