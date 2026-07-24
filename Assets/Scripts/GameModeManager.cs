using UnityEngine;

public sealed class GameModeManager : MonoBehaviour
{
    public enum Mode { Classic, Fortress, Strong, Convoy, Campaign, Hardcore }

    public WaveManager Spawner { get; private set; }
    public GameModeBase ActiveMode { get; private set; }
    public bool MatchRunning => ActiveMode != null && string.IsNullOrEmpty(Result);
    public string Result { get; private set; }
    public string LastReport { get; private set; }
    public bool IsHardcore => ActiveMode != null && ActiveMode.Type == Mode.Hardcore;
    private float matchStartedAt;
    private int enemiesKilled;
    private int shotsFired;
    private int shotsHit;
    private float damageDone;
    private float healthLost;

    private void Awake()
    {
        Spawner = GetComponent<WaveManager>();
        AddMode<ClassicGameMode>();
        AddMode<FortressGameMode>();
        AddMode<StrongGameMode>();
        AddMode<ConvoyGameMode>();
        AddMode<CampaignGameMode>();
        AddMode<HardcoreGameMode>();
    }

    private void AddMode<T>() where T : GameModeBase
    {
        T mode = gameObject.AddComponent<T>();
        mode.enabled = false;
    }

    public void StartMode(Mode type)
    {
        if (EconomyManager.Instance != null && !EconomyManager.Instance.IsModeUnlocked((int)type))
            return;
        if (ActiveMode != null && ActiveMode.Type == type && string.IsNullOrEmpty(Result))
        {
            ActiveMode.enabled = true;
            return;
        }
        if (ActiveMode != null) ActiveMode.EndMode();
        Spawner.StopMode();
        Spawner.ClearEnemies();
        ResetPlayerModifiers();
        RestorePlayerForNewMatch();
        Result = string.Empty;
        LastReport = string.Empty;
        matchStartedAt = Time.time;
        enemiesKilled = shotsFired = shotsHit = 0;
        damageDone = healthLost = 0f;
        EconomyManager.Instance?.BeginMatch();
        SetBoundaryWalls(true);
        foreach (GameModeBase mode in GetComponents<GameModeBase>())
            if (mode.Type == type) ActiveMode = mode;
        ActiveMode?.Begin(this);
    }

    private static void RestorePlayerForNewMatch()
    {
        PlayerVitals vitals = FindAnyObjectByType<PlayerVitals>();
        if (vitals != null) vitals.RestoreForNewMode();
        SimpleRifle rifle = FindAnyObjectByType<SimpleRifle>();
        if (rifle != null) rifle.RestoreSpawnAmmo();
    }

    public void AbandonActiveMode()
    {
        if (ActiveMode != null) ActiveMode.EndMode();
        Spawner.StopMode();
        Spawner.ClearEnemies();
        ActiveMode = null;
        Result = string.Empty;
    }

    public void ExitMatchEarly()
    {
        if (ActiveMode == null) return;
        BuildReport("MATCH ABANDONED", 0.5f);
        EconomyManager.Instance?.SettleMatch(0.5f);
        AbandonActiveMode();
    }

    public void Finish(bool victory, string message)
    {
        Result = victory ? $"VICTORY — {message}" : $"DEFEAT — {message}";
        if (victory && ActiveMode != null) EconomyManager.Instance?.RewardVictory(ActiveMode.Type);
        BuildReport(victory ? "VICTORY" : "DEFEAT", 1f);
        EconomyManager.Instance?.SettleMatch(1f);
        if (ActiveMode != null) ActiveMode.enabled = false;
    }

    public void RecordEnemyKill() => enemiesKilled++;
    public void RecordShot(bool hit) { shotsFired++; if (hit) shotsHit++; }
    public void RecordDamage(float amount) => damageDone += Mathf.Max(0f, amount);
    public void RecordHealthLost(float amount) => healthLost += Mathf.Max(0f, amount);

    private void BuildReport(string outcome, float payoutMultiplier)
    {
        float elapsed = Mathf.Max(0f, Time.time - matchStartedAt);
        LastReport = $"{outcome}\nTIME  {Mathf.FloorToInt(elapsed / 60f):00}:{Mathf.FloorToInt(elapsed % 60f):00}\nENEMIES KILLED  {enemiesKilled}\nSHOTS FIRED  {shotsFired}\nBULLETS MISSED  {Mathf.Max(0, shotsFired - shotsHit)}\nACCURACY  {(shotsFired == 0 ? 0f : shotsHit * 100f / shotsFired):0}%\nDAMAGE DONE  {damageDone:0}\nHEALTH LOST  {healthLost:0}\nCREDIT PAYOUT  x{payoutMultiplier:0.0}";
    }

    private static void SetBoundaryWalls(bool visible)
    {
        string[] names = { "North Wall", "South Wall", "East Wall", "West Wall" };
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (System.Array.IndexOf(names, candidate.name) >= 0)
                candidate.gameObject.SetActive(visible);
        }
    }

    private static void ResetPlayerModifiers()
    {
        PlayerVitals vitals = FindAnyObjectByType<PlayerVitals>();
        if (vitals != null) vitals.SetModeModifiers(1f, 0f);
        SimpleRifle rifle = FindAnyObjectByType<SimpleRifle>();
        if (rifle != null) rifle.ModeDamageMultiplier = 1f;
    }

    private void OnGUI()
    {
        if (ActiveMode == null) return;
        GUI.color = new Color(0.015f, 0.025f, 0.04f, 0.82f);
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - 260f, 108f, 520f, 48f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15, fontStyle = FontStyle.Bold };
        style.normal.textColor = new Color(0.72f, 0.9f, 1f);
        GUI.Label(new Rect(Screen.width * 0.5f - 250f, 112f, 500f, 40f), string.IsNullOrEmpty(Result) ? ActiveMode.Objective : Result, style);
    }
}
