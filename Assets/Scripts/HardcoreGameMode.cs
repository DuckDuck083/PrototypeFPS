using UnityEngine;

public sealed class HardcoreGameMode : GameModeBase
{
    public override GameModeManager.Mode Type => GameModeManager.Mode.Hardcore;
    public override string Objective => "HARDCORE  •  One life. Survive as long as possible.";
    private float startedAt;
    private float nextRespawn;
    private readonly Vector3 spawnPoint = new Vector3(0f, 0f, 48f);

    public override void Begin(GameModeManager manager)
    {
        base.Begin(manager);
        startedAt = Time.time;
        PlayerVitals vitals = FindAnyObjectByType<PlayerVitals>();
        bool adrenaline = EconomyManager.Instance != null && EconomyManager.Instance.IsLootUnlocked(3);
        vitals.SetModeModifiers(0.55f, adrenaline ? 1.5f : 0f);
        vitals.HardcoreRules = true;
        if (EconomyManager.Instance != null && EconomyManager.Instance.IsLootUnlocked(0)) vitals.AddMaximumHealth(25f);
        if (EconomyManager.Instance != null && EconomyManager.Instance.IsLootUnlocked(2)) vitals.AddMaximumHealth(20f);
        SimpleRifle rifle = FindAnyObjectByType<SimpleRifle>();
        rifle.HardcoreAmmoRules = true;
        rifle.RestoreSpawnAmmo();
        if (EconomyManager.Instance != null && EconomyManager.Instance.IsLootUnlocked(1)) rifle.AddStartingAmmoFraction(0.35f);
        for (int i = 0; i < 10; i++) SpawnReplacement(i);
    }

    private void Update()
    {
        int living = LivingEnemies();
        if (living < 10 && nextRespawn <= 0f) nextRespawn = Time.time + 5f;
        if (living < 10 && Time.time >= nextRespawn)
        {
            nextRespawn = Time.time + 5f;
            SpawnReplacement(Random.Range(0, 100));
        }
        else if (living >= 10) nextRespawn = 0f;
    }

    private void SpawnReplacement(int index)
    {
        TrainingTarget.EnemyArchetype type = index % 10 == 0 ? TrainingTarget.EnemyArchetype.Tank
            : index % 5 == 0 ? TrainingTarget.EnemyArchetype.Sniper
            : index % 3 == 0 ? TrainingTarget.EnemyArchetype.Rifle
            : TrainingTarget.EnemyArchetype.Normal;
        Spawn(type, spawnPoint + new Vector3((index % 3 - 1) * 1.5f, 0f, (index % 2) * 1.5f));
    }

    private void OnGUI()
    {
        float elapsed = Mathf.Max(0f, Time.time - startedAt);
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - 105f, 24f, 210f, 60f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 27, fontStyle = FontStyle.Bold };
        style.normal.textColor = new Color(1f, 0.3f, 0.18f);
        GUI.Label(new Rect(Screen.width * 0.5f - 100f, 28f, 200f, 50f), $"{Mathf.FloorToInt(elapsed / 60f):00}:{Mathf.FloorToInt(elapsed % 60f):00.0}", style);
    }

    public override void EndMode()
    {
        PlayerVitals vitals = FindAnyObjectByType<PlayerVitals>();
        if (vitals != null) vitals.HardcoreRules = false;
        SimpleRifle rifle = FindAnyObjectByType<SimpleRifle>();
        if (rifle != null) rifle.HardcoreAmmoRules = false;
        base.EndMode();
    }
}
