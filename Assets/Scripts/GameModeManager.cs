using UnityEngine;

public sealed class GameModeManager : MonoBehaviour
{
    public enum Mode { Classic, Fortress, Strong, Convoy, Campaign }

    public WaveManager Spawner { get; private set; }
    public GameModeBase ActiveMode { get; private set; }
    public bool MatchRunning => ActiveMode != null;
    public string Result { get; private set; }

    private void Awake()
    {
        Spawner = GetComponent<WaveManager>();
        AddMode<ClassicGameMode>();
        AddMode<FortressGameMode>();
        AddMode<StrongGameMode>();
        AddMode<ConvoyGameMode>();
        AddMode<CampaignGameMode>();
    }

    private void AddMode<T>() where T : GameModeBase
    {
        T mode = gameObject.AddComponent<T>();
        mode.enabled = false;
    }

    public void StartMode(Mode type)
    {
        if (ActiveMode != null) ActiveMode.EndMode();
        Spawner.StopMode();
        Spawner.ClearEnemies();
        ResetPlayerModifiers();
        Result = string.Empty;
        foreach (GameModeBase mode in GetComponents<GameModeBase>())
            if (mode.Type == type) ActiveMode = mode;
        ActiveMode?.Begin(this);
    }

    public void Finish(bool victory, string message)
    {
        Result = victory ? $"VICTORY — {message}" : $"DEFEAT — {message}";
        if (ActiveMode != null) ActiveMode.enabled = false;
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
