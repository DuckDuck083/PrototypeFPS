using UnityEngine;

public sealed class WaveManager : MonoBehaviour
{
    private const string SavedWaveKey = "PrototypeFPS.CurrentWave";
    public int CurrentWave { get; private set; }
    public int EnemiesRemaining { get; private set; }
    private float nextWaveTime;
    private bool waitingForNextWave;
    private int spawnSequence;
    private float nextReconcileTime;
    private bool running;

    public void BeginClassic()
    {
        ClearEnemies();
        running = true;
        StartWave(Mathf.Max(1, PlayerPrefs.GetInt(SavedWaveKey, 1)));
    }

    public void StopMode()
    {
        running = false;
        waitingForNextWave = false;
    }

    private void Update()
    {
        if (!running) return;
        if (Time.time >= nextReconcileTime)
        {
            nextReconcileTime = Time.time + 1f;
            ReconcileEnemyCount();
        }
        if (waitingForNextWave && Time.time >= nextWaveTime)
            StartWave(CurrentWave + 1);
    }

    private void ReconcileEnemyCount()
    {
        int liveEnemies = 0;
        foreach (TrainingTarget target in FindObjectsByType<TrainingTarget>())
            if (target.IsWaveEnemy && target.IsHostile && target.IsAlive) liveEnemies++;
        EnemiesRemaining = liveEnemies;
        if (EnemiesRemaining == 0 && !waitingForNextWave)
        {
            waitingForNextWave = true;
            nextWaveTime = Time.time + 4f;
        }
    }

    private void StartWave(int wave)
    {
        CurrentWave = wave;
        SaveProgress();
        waitingForNextWave = false;
        int normalCount = wave == 1 ? 5 : wave == 2 ? 8 : 7 + wave;
        Spawn(TrainingTarget.EnemyArchetype.Normal, normalCount);
        if (wave == 3) Spawn(TrainingTarget.EnemyArchetype.Handgun, 1);
        if (wave == 4) { Spawn(TrainingTarget.EnemyArchetype.Handgun, 1); Spawn(TrainingTarget.EnemyArchetype.Rifle, 1); }
        if (wave >= 5) Spawn(TrainingTarget.EnemyArchetype.Rifle, 2 + Mathf.Max(0, (wave - 8) / 3));
        if (wave >= 6) Spawn(TrainingTarget.EnemyArchetype.Sniper, 1 + Mathf.Max(0, (wave - 10) / 5));
        if (wave >= 7) Spawn(TrainingTarget.EnemyArchetype.Knife, 2 + wave / 5);
        if (wave >= 8) Spawn(TrainingTarget.EnemyArchetype.Demolition, 1 + (wave - 8) / 4);
        if (wave >= 9) Spawn(TrainingTarget.EnemyArchetype.Tank, 1 + (wave - 9) / 6);
        if (wave >= 4 && wave % 3 == 1) Spawn(TrainingTarget.EnemyArchetype.Medic, 1);
        if (wave >= 5 && wave % 4 == 1) Spawn(TrainingTarget.EnemyArchetype.Engineer, 1);
        if (wave >= 6) Spawn(TrainingTarget.EnemyArchetype.Pyro, 1 + wave / 8);
        if (wave >= 7) Spawn(TrainingTarget.EnemyArchetype.Scout, 1 + wave / 7);
        if (wave >= 8 && wave % 3 == 2) Spawn(TrainingTarget.EnemyArchetype.Officer, 1);
    }

    private void Spawn(TrainingTarget.EnemyArchetype type, int count)
    {
        EnemiesRemaining += count;
        for (int i = 0; i < count; i++)
            CreateEnemy(type, FindSpawnPosition(spawnSequence++));
    }

    private static Vector3 FindSpawnPosition(int index)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float angle = (index * 83f + attempt * 31f + Random.Range(-18f, 18f)) * Mathf.Deg2Rad;
            float distance = Random.Range(26f, 70f);
            Vector3 position = new Vector3(Mathf.Sin(angle) * distance, 0f, Mathf.Cos(angle) * distance);
            if (!Physics.CheckCapsule(position + Vector3.up * 0.7f, position + Vector3.up * 2f, 0.55f, ~0, QueryTriggerInteraction.Ignore))
                return position;
        }
        return new Vector3(0f, 0f, 22f + index % 5 * 3f);
    }

    public TrainingTarget SpawnEnemy(TrainingTarget.EnemyArchetype type, Vector3 position, float healthMultiplier = 1f, float damageMultiplier = 1f, bool waveEnemy = false)
    {
        GameObject root = new GameObject(type + " Enemy");
        root.transform.position = position;
        CharacterController controller = root.AddComponent<CharacterController>();
        bool tank = type == TrainingTarget.EnemyArchetype.Tank;
        controller.height = tank ? 2.15f : 1.7f;
        controller.radius = tank ? 0.62f : 0.43f;
        controller.center = Vector3.up * controller.height * 0.5f;

        Color uniformColor = type == TrainingTarget.EnemyArchetype.Normal ? new Color(0.95f, 0.1f, 0.06f)
            : type == TrainingTarget.EnemyArchetype.Knife ? new Color(0.75f, 0.3f, 0.08f)
            : type == TrainingTarget.EnemyArchetype.Demolition ? new Color(0.68f, 0.12f, 0.82f)
            : type == TrainingTarget.EnemyArchetype.Tank ? new Color(0.35f, 0.68f, 0.08f)
            : type == TrainingTarget.EnemyArchetype.Sniper ? new Color(0.05f, 0.55f, 0.8f)
            : type == TrainingTarget.EnemyArchetype.Medic ? new Color(0.9f, 0.9f, 0.92f)
            : type == TrainingTarget.EnemyArchetype.Engineer ? new Color(0.9f, 0.55f, 0.08f)
            : type == TrainingTarget.EnemyArchetype.Pyro ? new Color(0.95f, 0.18f, 0.02f)
            : type == TrainingTarget.EnemyArchetype.Scout ? new Color(0.1f, 0.8f, 0.9f)
            : type == TrainingTarget.EnemyArchetype.Officer ? new Color(0.55f, 0.12f, 0.72f)
            : new Color(0.08f, 0.68f, 0.2f);
        if (EconomyManager.Instance != null)
            uniformColor = Color.Lerp(uniformColor, EconomyManager.Instance.ActiveEnemySkinColor, 0.72f);
        Material uniform = MakeMaterial(uniformColor);
        Material gear = MakeMaterial(new Color(0.025f, 0.035f, 0.03f));
        float scale = tank ? 1.25f : 1f;
        AddPart(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 1f * scale, 0f), new Vector3(0.72f, 0.9f, 0.72f) * scale, uniform, true);
        AddPart(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.86f * scale, 0f), Vector3.one * 0.52f * scale, uniform, true);
        AddPart(root.transform, "Vest", PrimitiveType.Cube, new Vector3(0f, 1.15f * scale, 0.05f), new Vector3(0.78f, 0.58f, 0.42f) * scale, gear);
        AddPart(root.transform, "Enemy Beacon", PrimitiveType.Sphere, new Vector3(0f, 2.55f * scale, 0f), Vector3.one * 0.2f, MakeUnlitMaterial(uniformColor));

        if (type != TrainingTarget.EnemyArchetype.Normal)
        {
            string weaponName = type == TrainingTarget.EnemyArchetype.Knife ? "Knife" : type == TrainingTarget.EnemyArchetype.Sniper ? "Sniper Rifle"
                : type == TrainingTarget.EnemyArchetype.Demolition ? "Grenade Launcher" : type == TrainingTarget.EnemyArchetype.Tank ? "Minigun"
                : type == TrainingTarget.EnemyArchetype.Engineer ? "Builder Tool" : type == TrainingTarget.EnemyArchetype.Medic ? "Med Rifle"
                : type == TrainingTarget.EnemyArchetype.Pyro ? "Flamethrower" : type == TrainingTarget.EnemyArchetype.Scout ? "Scattergun"
                : type == TrainingTarget.EnemyArchetype.Officer ? "Officer Rifle"
                : type == TrainingTarget.EnemyArchetype.Rifle ? "Rifle" : "Handgun";
            Vector3 weaponScale = type == TrainingTarget.EnemyArchetype.Knife ? new Vector3(0.06f, 0.06f, 0.5f)
                : type == TrainingTarget.EnemyArchetype.Handgun ? new Vector3(0.12f, 0.14f, 0.42f)
                : type == TrainingTarget.EnemyArchetype.Tank ? new Vector3(0.32f, 0.3f, 1.15f)
                : new Vector3(0.13f, 0.14f, 0.95f);
            AddPart(root.transform, weaponName, PrimitiveType.Cube, new Vector3(0.28f, 1.28f * scale, 0.48f), weaponScale, gear);
        }

        float health = type == TrainingTarget.EnemyArchetype.Tank ? 450f : type == TrainingTarget.EnemyArchetype.Sniper ? 110f : type == TrainingTarget.EnemyArchetype.Demolition ? 180f : type == TrainingTarget.EnemyArchetype.Engineer ? 175f : type == TrainingTarget.EnemyArchetype.Pyro ? 125f : type == TrainingTarget.EnemyArchetype.Handgun ? 100f : type == TrainingTarget.EnemyArchetype.Medic ? 105f : type == TrainingTarget.EnemyArchetype.Officer ? 145f : type == TrainingTarget.EnemyArchetype.Scout ? 110f : type == TrainingTarget.EnemyArchetype.Knife ? 75f : 85f;
        float speed = type == TrainingTarget.EnemyArchetype.Tank ? 1.2f : type == TrainingTarget.EnemyArchetype.Knife ? 4.1f : type == TrainingTarget.EnemyArchetype.Scout ? 5.8f : type == TrainingTarget.EnemyArchetype.Sniper ? 1.5f : 2.35f;
        float damage = type == TrainingTarget.EnemyArchetype.Sniper ? 45f : type == TrainingTarget.EnemyArchetype.Demolition ? 62f : type == TrainingTarget.EnemyArchetype.Tank ? 6.5f : type == TrainingTarget.EnemyArchetype.Pyro ? 9f : type == TrainingTarget.EnemyArchetype.Scout ? 22f : type == TrainingTarget.EnemyArchetype.Engineer ? 10f : type == TrainingTarget.EnemyArchetype.Officer ? 5f : type == TrainingTarget.EnemyArchetype.Knife ? 14f : type == TrainingTarget.EnemyArchetype.Handgun ? 14f : 6f;
        TrainingTarget target = root.AddComponent<TrainingTarget>();
        target.Configure(true, (health + (waveEnemy ? CurrentWave * (tank ? 10f : 3f) : 0f)) * healthMultiplier, speed, damage * damageMultiplier);
        target.ConfigureWave(waveEnemy ? this : null, type);
        return target;
    }

    private void CreateEnemy(TrainingTarget.EnemyArchetype type, Vector3 position) => SpawnEnemy(type, position, 1f, 1f, true);

    public void ClearEnemies()
    {
        foreach (TrainingTarget target in FindObjectsByType<TrainingTarget>())
            if (target.IsHostile) Destroy(target.gameObject);
        EnemiesRemaining = 0;
    }

    public void NotifyEnemyDefeated(TrainingTarget target)
    {
        EnemiesRemaining = Mathf.Max(0, EnemiesRemaining - 1);
        if (EnemiesRemaining == 0)
        {
            waitingForNextWave = true;
            nextWaveTime = Time.time + 4f;
        }
    }

    public void SaveProgress()
    {
        if (CurrentWave <= 0) return;
        PlayerPrefs.SetInt(SavedWaveKey, Mathf.Max(1, CurrentWave));
        PlayerPrefs.Save();
    }

    private void OnGUI()
    {
        if (!running) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.white;
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - 155f, 42f, 310f, 58f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        string status = waitingForNextWave ? $"WAVE {CurrentWave} CLEARED" : $"WAVE {CurrentWave}   ENEMIES REMAINING: {EnemiesRemaining}";
        GUI.Label(new Rect(Screen.width * 0.5f - 150f, 46f, 300f, 48f), status, style);
    }

    private static Material MakeMaterial(Color color)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        return material;
    }

    private static Material MakeUnlitMaterial(Color color)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.color = color;
        return material;
    }

    private static void AddPart(Transform parent, string partName, PrimitiveType shape, Vector3 position, Vector3 scale, Material material, bool collider = false)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().material = material;
        if (!collider) Destroy(part.GetComponent<Collider>());
    }
}
