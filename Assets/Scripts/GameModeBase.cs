using UnityEngine;

public abstract class GameModeBase : MonoBehaviour
{
    protected GameModeManager Manager { get; private set; }
    protected WaveManager Spawner => Manager.Spawner;
    public abstract GameModeManager.Mode Type { get; }
    public abstract string Objective { get; }

    public virtual void Begin(GameModeManager manager)
    {
        Manager = manager;
        enabled = true;
    }

    public virtual void EndMode() => enabled = false;

    protected int LivingEnemies()
    {
        int count = 0;
        foreach (TrainingTarget target in FindObjectsByType<TrainingTarget>(FindObjectsSortMode.None))
            if (target.IsHostile && target.IsAlive) count++;
        return count;
    }

    protected TrainingTarget Spawn(TrainingTarget.EnemyArchetype type, Vector3 position, float health = 1f, float damage = 1f)
        => Spawner.SpawnEnemy(type, position, health, damage);
}
