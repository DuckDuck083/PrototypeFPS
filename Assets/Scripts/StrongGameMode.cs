using UnityEngine;

public sealed class StrongGameMode : GameModeBase
{
    public override GameModeManager.Mode Type => GameModeManager.Mode.Strong;
    public override string Objective => $"STRONG  •  Eliminate the enemy team  •  Round {round}/3  •  Wins {wins}";
    private int round;
    private int wins;
    private int losses;
    private bool resolving;
    private float nextRoundAt;
    private PlayerVitals player;

    public override void Begin(GameModeManager manager)
    {
        base.Begin(manager);
        player = FindAnyObjectByType<PlayerVitals>();
        player?.SetModeModifiers(6f, 3f);
        SimpleRifle rifle = FindAnyObjectByType<SimpleRifle>();
        if (rifle != null) rifle.ModeDamageMultiplier = 3f;
        StartRound();
    }

    private void Update()
    {
        if (resolving)
        {
            if (Time.time >= nextRoundAt) StartRound();
            return;
        }
        if (LivingEnemies() == 0) ResolveRound(true);
        else if (player != null && player.IsDead) ResolveRound(false);
    }

    private void StartRound()
    {
        Spawner.ClearEnemies();
        round++;
        resolving = false;
        int count = Random.Range(20, 31);
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            TrainingTarget.EnemyArchetype type = i % 8 == 0 ? TrainingTarget.EnemyArchetype.Rifle : TrainingTarget.EnemyArchetype.Normal;
            Spawn(type, new Vector3(Mathf.Sin(angle) * 58f, 0f, Mathf.Cos(angle) * 58f));
        }
    }

    private void ResolveRound(bool won)
    {
        resolving = true;
        if (won) wins++; else losses++;
        Spawner.ClearEnemies();
        if (wins >= 2) Manager.Finish(true, "best-of-three match won");
        else if (losses >= 2) Manager.Finish(false, "best-of-three match lost");
        else nextRoundAt = Time.time + 6f;
    }
}
