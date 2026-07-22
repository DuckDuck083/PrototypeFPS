using UnityEngine;

public sealed class FortressGameMode : GameModeBase
{
    public override GameModeManager.Mode Type => GameModeManager.Mode.Fortress;
    public override string Objective => "FORTRESS  •  Destroy the enemy fortress while defending your own";
    private DestructibleObjective playerFortress;
    private DestructibleObjective enemyFortress;
    private float nextRespawn;
    private float nextSiegeTick;

    public override void Begin(GameModeManager manager)
    {
        base.Begin(manager);
        playerFortress = CreateFortress("Player Fortress", new Vector3(0f, 3f, -60f), new Color(0.08f, 0.45f, 0.9f));
        enemyFortress = CreateFortress("Enemy Fortress", new Vector3(0f, 3f, 60f), new Color(0.85f, 0.12f, 0.08f));
        playerFortress.Destroyed += _ => Manager.Finish(false, "your fortress was destroyed");
        enemyFortress.Destroyed += _ => Manager.Finish(true, "enemy fortress destroyed");
        for (int i = 0; i < 10; i++) SpawnDefender(i);
    }

    private void Update()
    {
        if (playerFortress == null || enemyFortress == null || playerFortress.IsDestroyed || enemyFortress.IsDestroyed) return;
        if (LivingEnemies() < 10 && Time.time >= nextRespawn)
        {
            nextRespawn = Time.time + 5f;
            SpawnDefender(Random.Range(0, 10));
        }
        if (Time.time >= nextSiegeTick)
        {
            nextSiegeTick = Time.time + 1f;
            foreach (TrainingTarget enemy in FindObjectsByType<TrainingTarget>(FindObjectsSortMode.None))
                if (enemy.IsHostile && enemy.IsAlive && Vector3.Distance(enemy.transform.position, playerFortress.transform.position) < 16f)
                    playerFortress.TakeDamage(12f);
        }
    }

    private void SpawnDefender(int index)
    {
        float x = -18f + (index % 5) * 9f;
        Spawn(index % 4 == 0 ? TrainingTarget.EnemyArchetype.Rifle : TrainingTarget.EnemyArchetype.Normal, new Vector3(x, 0f, 42f + (index / 5) * 8f));
    }

    private static DestructibleObjective CreateFortress(string name, Vector3 position, Color color)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.name = name;
        root.transform.position = position;
        root.transform.localScale = new Vector3(22f, 6f, 10f);
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        root.GetComponent<Renderer>().material = material;
        DestructibleObjective objective = root.AddComponent<DestructibleObjective>();
        objective.Configure(1800f);
        return objective;
    }

    public override void EndMode()
    {
        if (playerFortress != null) Destroy(playerFortress.gameObject);
        if (enemyFortress != null) Destroy(enemyFortress.gameObject);
        base.EndMode();
    }
}
