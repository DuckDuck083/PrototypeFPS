using System.Collections.Generic;
using UnityEngine;

public sealed class CampaignGameMode : GameModeBase
{
    private static readonly string[] Missions =
    {
        "DEFEND THE OUTPOST",
        "SILENCE THE FOREST",
        "PRISON BREAK",
        "ESCAPE THE RUINED CITY",
        "STEAL THE INTELLIGENCE",
        "ASSAULT THE CAPITAL"
    };

    public override GameModeManager.Mode Type => GameModeManager.Mode.Campaign;
    public override string Objective => transitioning
        ? $"CAMPAIGN {mission + 1}/6  •  MISSION COMPLETE"
        : $"CAMPAIGN {mission + 1}/6  •  {CurrentDirective}";

    private int mission;
    private bool transitioning;
    private float nextMissionAt;
    private DestructibleObjective objective;
    private GameObject interactionGoal;
    private readonly List<GameObject> prisoners = new List<GameObject>();
    private int rescued;
    private Transform player;

    private string CurrentDirective
    {
        get
        {
            int enemies = LivingEnemies();
            if (mission == 0) return $"Hold the outpost — eliminate {enemies} attackers";
            if (mission == 1) return objective != null && !objective.IsDestroyed
                ? $"Destroy the orange communications station — {objective.Health:0} HP"
                : $"Communications offline — eliminate {enemies} remaining guards";
            if (mission == 2) return rescued < 3
                ? $"Reach the blue prisoner markers — rescued {rescued}/3"
                : $"All prisoners rescued — eliminate {enemies} remaining guards";
            if (mission == 3) return enemies > 0
                ? $"Break the ambush — eliminate {enemies} enemies"
                : "Follow the green marker to the city escape route";
            if (mission == 4) return enemies > 0
                ? $"Clear research security — eliminate {enemies} enemies"
                : "Reach the cyan intelligence marker and steal the files";
            return objective != null && !objective.IsDestroyed
                ? $"Destroy the red command center — {objective.Health:0} HP"
                : $"Command center destroyed — defeat {enemies} remaining enemies";
        }
    }

    public override void Begin(GameModeManager manager)
    {
        base.Begin(manager);
        player = FindAnyObjectByType<PlayerVitals>()?.transform;
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

        if (mission == 2) UpdatePrisonerRescue();
        if ((mission == 3 || mission == 4) && LivingEnemies() == 0 && IsPlayerAt(interactionGoal))
            CompleteMission();
        else if (mission == 0 && LivingEnemies() == 0)
            CompleteMission();
        else if (mission == 1 && LivingEnemies() == 0 && objective != null && objective.IsDestroyed)
            CompleteMission();
        else if (mission == 2 && LivingEnemies() == 0 && rescued >= 3)
            CompleteMission();
        else if (mission == 5 && LivingEnemies() == 0 && objective != null && objective.IsDestroyed)
            CompleteMission();
    }

    private void StartMission()
    {
        transitioning = false;
        CleanupObjectives();
        Spawner.ClearEnemies();

        int enemyCount = 8 + mission * 3;
        for (int i = 0; i < enemyCount; i++)
        {
            float angle = i * 2f * Mathf.PI / enemyCount;
            TrainingTarget.EnemyArchetype type = mission == 5 && i == 0 ? TrainingTarget.EnemyArchetype.Tank
                : i % 7 == 0 ? TrainingTarget.EnemyArchetype.Sniper
                : i % 5 == 0 ? TrainingTarget.EnemyArchetype.Rifle
                : TrainingTarget.EnemyArchetype.Normal;
            float radius = 28f + mission * 5f;
            Spawn(type, new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius), mission == 5 && i == 0 ? 3f : 1f);
        }

        if (mission == 1)
            objective = CreateObjective("COMMUNICATIONS STATION — DESTROY", new Vector3(50f, 3f, 50f), 900f, new Color(1f, 0.28f, 0.04f));
        else if (mission == 2)
            CreatePrisoners();
        else if (mission == 3)
            interactionGoal = CreateMarker("CITY ESCAPE ROUTE", new Vector3(0f, 0.25f, 70f), new Color(0.12f, 1f, 0.35f));
        else if (mission == 4)
            interactionGoal = CreateMarker("SECRET INTELLIGENCE", new Vector3(-55f, 0.25f, 55f), new Color(0.05f, 0.85f, 1f));
        else if (mission == 5)
            objective = CreateObjective("ENEMY COMMAND CENTER — DESTROY", new Vector3(0f, 4f, 65f), 1800f, new Color(0.9f, 0.04f, 0.02f));
    }

    private void CreatePrisoners()
    {
        Vector3[] positions = { new Vector3(-45f, 1f, 30f), new Vector3(-35f, 1f, 38f), new Vector3(-25f, 1f, 30f) };
        foreach (Vector3 position in positions)
        {
            GameObject prisoner = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            prisoner.name = "CAPTURED SOLDIER — RESCUE";
            prisoner.transform.position = position;
            prisoner.GetComponent<Renderer>().material = MakeMaterial(new Color(0.08f, 0.45f, 1f));
            Destroy(prisoner.GetComponent<Collider>());
            prisoners.Add(prisoner);
        }
    }

    private void UpdatePrisonerRescue()
    {
        if (player == null) return;
        for (int i = prisoners.Count - 1; i >= 0; i--)
        {
            GameObject prisoner = prisoners[i];
            if (prisoner != null && Vector3.Distance(player.position, prisoner.transform.position) <= 3.5f)
            {
                Destroy(prisoner);
                prisoners.RemoveAt(i);
                rescued++;
            }
        }
    }

    private bool IsPlayerAt(GameObject marker)
        => player != null && marker != null && Vector3.Distance(player.position, marker.transform.position) <= 4.5f;

    private void CompleteMission()
    {
        EconomyManager.Instance?.NotifyMissionCompleted();
        if (mission >= 5)
        {
            Manager.Finish(true, "the war is over");
            return;
        }
        CleanupObjectives();
        mission++;
        transitioning = true;
        nextMissionAt = Time.time + 4f;
    }

    private void OnGUI()
    {
        if (transitioning) return;
        Rect panel = new Rect(18f, 112f, 390f, 76f);
        GUI.color = new Color(0.015f, 0.03f, 0.05f, 0.88f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
        title.normal.textColor = new Color(0.3f, 0.78f, 1f);
        GUI.Label(new Rect(panel.x + 12f, panel.y + 7f, panel.width - 24f, 24f), $"MISSION {mission + 1}/6 — {Missions[mission]}", title);
        GUIStyle directive = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        directive.normal.textColor = Color.white;
        GUI.Label(new Rect(panel.x + 12f, panel.y + 33f, panel.width - 24f, 38f), CurrentDirective, directive);

        if (interactionGoal != null) DrawWorldMarker(interactionGoal.transform.position, interactionGoal.name, new Color(0.2f, 1f, 0.45f));
        foreach (GameObject prisoner in prisoners)
            if (prisoner != null) DrawWorldMarker(prisoner.transform.position, "RESCUE", new Color(0.2f, 0.65f, 1f));
        if (objective != null && !objective.IsDestroyed) DrawWorldMarker(objective.transform.position, "DESTROY", new Color(1f, 0.25f, 0.12f));
    }

    private static void DrawWorldMarker(Vector3 position, string label, Color color)
    {
        if (Camera.main == null) return;
        Vector3 screen = Camera.main.WorldToScreenPoint(position + Vector3.up * 3f);
        if (screen.z <= 0f) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold };
        style.normal.textColor = color;
        GUI.Label(new Rect(screen.x - 80f, Screen.height - screen.y - 15f, 160f, 30f), $"▼ {label}\n{Vector3.Distance(Camera.main.transform.position, position):0}m", style);
    }

    private static DestructibleObjective CreateObjective(string name, Vector3 position, float health, Color color)
    {
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = name;
        target.transform.position = position;
        target.transform.localScale = new Vector3(10f, 6f, 10f);
        target.GetComponent<Renderer>().material = MakeMaterial(color);
        DestructibleObjective destructible = target.AddComponent<DestructibleObjective>();
        destructible.Configure(health);
        return destructible;
    }

    private static GameObject CreateMarker(string name, Vector3 position, Color color)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = name;
        marker.transform.position = position;
        marker.transform.localScale = new Vector3(3.5f, 0.12f, 3.5f);
        marker.GetComponent<Renderer>().material = MakeMaterial(color);
        Destroy(marker.GetComponent<Collider>());
        return marker;
    }

    private static Material MakeMaterial(Color color)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.color = color;
        return material;
    }

    private void CleanupObjectives()
    {
        if (objective != null) Destroy(objective.gameObject);
        if (interactionGoal != null) Destroy(interactionGoal);
        foreach (GameObject prisoner in prisoners) if (prisoner != null) Destroy(prisoner);
        objective = null;
        interactionGoal = null;
        prisoners.Clear();
        rescued = 0;
    }

    public override void EndMode()
    {
        CleanupObjectives();
        base.EndMode();
    }
}
