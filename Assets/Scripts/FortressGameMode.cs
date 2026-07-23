using UnityEngine;

public sealed class FortressGameMode : GameModeBase
{
    public override GameModeManager.Mode Type => GameModeManager.Mode.Fortress;
    public override string Objective => "FORTRESS  •  Destroy the enemy fortress while defending your own";
    private DestructibleObjective playerFortress;
    private DestructibleObjective enemyFortress;
    private float nextRespawn;
    private int respawnSlot;

    public override void Begin(GameModeManager manager)
    {
        base.Begin(manager);
        playerFortress = CreateFortress("Player Fortress", new Vector3(0f, 2f, -55f), new Color(0.08f, 0.45f, 0.9f));
        enemyFortress = CreateFortress("Enemy Fortress", new Vector3(0f, 2f, 55f), new Color(0.85f, 0.12f, 0.08f));
        playerFortress.Destroyed += _ => Manager.Finish(false, "your fortress was destroyed");
        enemyFortress.Destroyed += _ => Manager.Finish(true, "enemy fortress destroyed");
        for (int i = 0; i < 10; i++) SpawnDefender(i);
    }

    private void Update()
    {
        if (playerFortress == null || enemyFortress == null || playerFortress.IsDestroyed || enemyFortress.IsDestroyed) return;
        int living = LivingEnemies();
        if (living < 10 && nextRespawn <= 0f)
            nextRespawn = Time.time + 5f;
        if (living < 10 && Time.time >= nextRespawn)
        {
            nextRespawn = Time.time + 5f;
            SpawnDefender(respawnSlot++ % 10);
        }
        else if (living >= 10) nextRespawn = 0f;
    }

    private void SpawnDefender(int index)
    {
        TrainingTarget.EnemyArchetype type;
        Vector3 position;
        bool guard;
        if (index == 0)
        {
            type = TrainingTarget.EnemyArchetype.Tank;
            position = new Vector3(0f, 4.2f, 55f);
            guard = true;
        }
        else if (index < 4)
        {
            type = TrainingTarget.EnemyArchetype.Rifle;
            position = new Vector3(-10f + (index - 1) * 10f, 0f, 43f);
            guard = true;
        }
        else if (index < 7)
        {
            type = TrainingTarget.EnemyArchetype.Sniper;
            position = new Vector3(-12f + (index - 4) * 12f, 0f, 49f);
            guard = true;
        }
        else
        {
            TrainingTarget.EnemyArchetype[] scattered =
            {
                TrainingTarget.EnemyArchetype.Handgun, TrainingTarget.EnemyArchetype.Normal, TrainingTarget.EnemyArchetype.Knife
            };
            type = scattered[index - 7];
            position = new Vector3(Random.Range(-22f, 23f), 0f, Random.Range(28f, 48f));
            guard = false;
        }
        TrainingTarget defender = Spawn(type, position);
        if (guard) defender.ConfigureGuardPost(position);
        else defender.ConfigureAttackObjective(playerFortress);
    }

    private void OnGUI()
    {
        if (playerFortress == null || enemyFortress == null) return;
        DrawFortBar(new Rect(24f, 24f, 310f, 28f), playerFortress, new Color(0.08f, 0.55f, 1f), "YOUR FORTRESS");
        DrawFortBar(new Rect(Screen.width - 334f, 24f, 310f, 28f), enemyFortress, new Color(0.95f, 0.15f, 0.08f), "ENEMY FORTRESS");
    }

    private static void DrawFortBar(Rect rect, DestructibleObjective fortress, Color color, string label)
    {
        float fill = fortress.MaximumHealth <= 0f ? 0f : fortress.Health / fortress.MaximumHealth;
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x + 3f, rect.y + 3f, (rect.width - 6f) * Mathf.Clamp01(fill), rect.height - 6f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 };
        style.normal.textColor = Color.white;
        GUI.Label(rect, $"{label}  {Mathf.CeilToInt(fortress.Health)} / {Mathf.CeilToInt(fortress.MaximumHealth)}", style);
    }

    private static DestructibleObjective CreateFortress(string name, Vector3 position, Color color)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.name = name;
        root.transform.position = position;
        root.transform.localScale = new Vector3(14f, 4f, 8f);
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
