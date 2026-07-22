using UnityEngine;

public sealed class CampaignGameMode : GameModeBase
{
    private static readonly string[] Missions =
    {
        "Defend the outpost from the enemy assault",
        "Destroy the communications station in the forest",
        "Rescue the captured soldiers from the prison camp",
        "Escape the ambush through the ruined city",
        "Infiltrate the research facility and steal the intelligence",
        "Assault the capital and destroy the command center"
    };

    public override GameModeManager.Mode Type => GameModeManager.Mode.Campaign;
    public override string Objective => $"CAMPAIGN {mission + 1}/6  •  {Missions[Mathf.Clamp(mission, 0, 5)]}";
    private int mission;
    private bool transitioning;
    private float nextMissionAt;
    private DestructibleObjective objective;

    public override void Begin(GameModeManager manager)
    {
        base.Begin(manager);
        mission = 0;
        StartMission();
    }

    private void Update()
    {
        if (transitioning)
        {
            if (Time.time >= nextMissionAt) StartMission();
            return;
        }

        bool cleared = LivingEnemies() == 0;
        bool objectiveDone = objective == null || objective.IsDestroyed;
        if (cleared && objectiveDone) CompleteMission();
    }

    private void StartMission()
    {
        transitioning = false;
        Spawner.ClearEnemies();
        if (objective != null) Destroy(objective.gameObject);
        objective = null;

        int enemyCount = 8 + mission * 3;
        for (int i = 0; i < enemyCount; i++)
        {
            float angle = i * 2f * Mathf.PI / enemyCount;
            TrainingTarget.EnemyArchetype type = mission == 5 && i == 0 ? TrainingTarget.EnemyArchetype.Tank
                : i % 6 == 0 ? TrainingTarget.EnemyArchetype.Rifle : TrainingTarget.EnemyArchetype.Normal;
            Spawn(type, new Vector3(Mathf.Sin(angle) * (30f + mission * 5f), 0f, Mathf.Cos(angle) * (30f + mission * 5f)), mission == 5 && i == 0 ? 3f : 1f);
        }

        if (mission == 1) objective = CreateObjective("Communications Station", new Vector3(50f, 3f, 50f), 900f, new Color(0.5f, 0.2f, 0.08f));
        else if (mission == 4) objective = CreateObjective("Research Intelligence Vault", new Vector3(-55f, 2f, 55f), 700f, new Color(0.12f, 0.35f, 0.55f));
        else if (mission == 5) objective = CreateObjective("Enemy Command Center", new Vector3(0f, 4f, 65f), 1800f, new Color(0.65f, 0.08f, 0.06f));
    }

    private void CompleteMission()
    {
        if (mission >= 5)
        {
            Manager.Finish(true, "the war is over");
            return;
        }
        mission++;
        transitioning = true;
        nextMissionAt = Time.time + 4f;
    }

    private static DestructibleObjective CreateObjective(string name, Vector3 position, float health, Color color)
    {
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = name;
        target.transform.position = position;
        target.transform.localScale = new Vector3(10f, 6f, 10f);
        target.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        DestructibleObjective destructible = target.AddComponent<DestructibleObjective>();
        destructible.Configure(health);
        return destructible;
    }

    public override void EndMode()
    {
        if (objective != null) Destroy(objective.gameObject);
        base.EndMode();
    }
}
