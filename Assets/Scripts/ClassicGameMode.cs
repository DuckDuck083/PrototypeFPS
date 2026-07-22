public sealed class ClassicGameMode : GameModeBase
{
    public override GameModeManager.Mode Type => GameModeManager.Mode.Classic;
    public override string Objective => "CLASSIC  •  Survive the escalating enemy waves";

    public override void Begin(GameModeManager manager)
    {
        base.Begin(manager);
        Spawner.BeginClassic();
    }

    public override void EndMode()
    {
        Spawner.StopMode();
        base.EndMode();
    }
}
