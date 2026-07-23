using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GameMenu : MonoBehaviour
{
    private FirstPersonController movement;
    private SimpleRifle weapons;
    private PlayerVitals vitals;
    private bool menuOpen = true;
    private bool loadoutOpen;
    private bool playModeOpen;
    private bool shopOpen;
    private bool questsOpen;
    private bool promoOpen;
    private bool adminOpen;
    private bool settingsOpen;
    private bool pausedMatch;
    private bool reportOpen;
    private bool confirmProgressReset;
    private int shopCategory;
    private int weaponShopSlot;
    private string promoCode = string.Empty;
    private string promoStatus = "Enter a code to redeem a special reward.";
    private Vector2 pageScroll;
    private int selectedLoadoutSlot = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureMenuExists()
    {
        PlayerVitals player = FindAnyObjectByType<PlayerVitals>();
        if (player != null && player.GetComponent<GameMenu>() == null)
            player.gameObject.AddComponent<GameMenu>();
    }

    private void Start()
    {
        movement = GetComponent<FirstPersonController>();
        weapons = GetComponent<SimpleRifle>();
        vitals = GetComponent<PlayerVitals>();
        OpenMenu();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        GameModeManager manager = FindAnyObjectByType<GameModeManager>();
        if (!menuOpen && manager != null && !string.IsNullOrEmpty(manager.LastReport))
        {
            OpenMenu();
            reportOpen = true;
            return;
        }
        if (!menuOpen && Keyboard.current.pKey.wasPressedThisFrame)
        {
            pausedMatch = true;
            OpenMenu();
        }
        else if (menuOpen && pausedMatch && Keyboard.current.pKey.wasPressedThisFrame)
            ResumeMatch();
        else if (Keyboard.current.escapeKey.wasPressedThisFrame && manager != null && manager.MatchRunning)
        {
            manager.ExitMatchEarly();
            pausedMatch = false;
            OpenMenu();
            reportOpen = true;
        }
    }

    private void OpenMenu()
    {
        menuOpen = true;
        loadoutOpen = false;
        playModeOpen = false;
        shopOpen = false;
        questsOpen = false;
        promoOpen = false;
        adminOpen = false;
        settingsOpen = false;
        reportOpen = false;
        confirmProgressReset = false;
        selectedLoadoutSlot = -1;
        Time.timeScale = 0f;
        movement.enabled = false;
        weapons.enabled = false;
        vitals.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void StartGame(GameModeManager.Mode mode)
    {
        GameModeManager manager = FindAnyObjectByType<GameModeManager>();
        if (manager == null) return;
        manager.StartMode(mode);
        pausedMatch = false;
        menuOpen = false;
        Time.timeScale = 1f;
        vitals.enabled = true;
        movement.enabled = true;
        weapons.enabled = true;
        movement.RestoreControls();
        weapons.EquipLoadoutSlot(0);
    }

    private void ResumeMatch()
    {
        menuOpen = false;
        pausedMatch = false;
        Time.timeScale = 1f;
        vitals.enabled = true;
        movement.enabled = true;
        weapons.enabled = true;
        movement.RestoreControls();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    private void OnGUI()
    {
        if (!menuOpen)
            return;

        GUI.color = new Color(0.015f, 0.025f, 0.04f, 0.94f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUIStyle title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 44, fontStyle = FontStyle.Bold };
        title.normal.textColor = new Color(0.3f, 0.78f, 1f);
        GUI.Label(new Rect(Screen.width * 0.5f - 300f, loadoutOpen || playModeOpen || shopOpen || questsOpen || promoOpen || adminOpen || settingsOpen || reportOpen ? 18f : 70f, 600f, 70f), "PROTOTYPE FPS", title);

        bool subPage = loadoutOpen || playModeOpen || shopOpen || questsOpen || promoOpen || adminOpen || settingsOpen || reportOpen;
        if (!subPage)
        {
            DrawMainMenu();
            return;
        }

        float contentHeight = shopOpen && shopCategory == 2 ? 840f : shopOpen && shopCategory == 3 ? 1050f : shopOpen ? 720f : loadoutOpen ? 640f : 610f;
        Rect viewport = new Rect(0f, 0f, Screen.width, Screen.height);
        Rect content = new Rect(0f, 0f, Mathf.Max(760f, Screen.width - 18f), Mathf.Max(contentHeight, Screen.height));
        pageScroll = GUI.BeginScrollView(viewport, pageScroll, content, false, true);
        if (loadoutOpen) DrawLoadout();
        else if (playModeOpen) DrawModeSelection();
        else if (shopOpen) DrawShop();
        else if (questsOpen) DrawQuests();
        else if (promoOpen) DrawPromoCodes();
        else if (adminOpen) DrawAdminPanel();
        else if (settingsOpen) DrawSettings();
        else if (reportOpen) DrawMatchReport();
        GUI.EndScrollView();

        if (pageScroll.y > 12f && GUI.Button(new Rect(Screen.width - 118f, 18f, 92f, 36f), "TOP"))
            pageScroll.y = 0f;
    }

    private void DrawMainMenu()
    {
        float dashboardWidth = Mathf.Min(720f, Screen.width - 40f);
        float tileGap = 14f;
        float tileWidth = (dashboardWidth - tileGap) * 0.5f;
        float tileHeight = Mathf.Clamp((Screen.height - 260f) / 3f, 76f, 112f);
        float x = (Screen.width - dashboardWidth) * 0.5f;
        float y = Mathf.Max(145f, Screen.height * 0.5f - tileHeight * 1.35f);

        DrawHomeTile(new Rect(x, y, tileWidth, tileHeight), pausedMatch ? "RESUME" : "PLAY", pausedMatch ? "Return to the current match" : "Choose a game mode", new Color(0.15f, 0.65f, 0.95f), () => { if (pausedMatch) ResumeMatch(); else { pageScroll = Vector2.zero; playModeOpen = true; } });
        DrawHomeTile(new Rect(x + tileWidth + tileGap, y, tileWidth, tileHeight), "LOADOUT", "Classes and equipment", new Color(0.2f, 0.8f, 0.5f), () => { pageScroll = Vector2.zero; loadoutOpen = true; selectedLoadoutSlot = -1; });
        DrawHomeTile(new Rect(x, y + tileHeight + tileGap, tileWidth, tileHeight), "BLACK MARKET", "Modes, weapons and perks", new Color(0.95f, 0.48f, 0.14f), () => { pageScroll = Vector2.zero; shopOpen = true; });
        DrawHomeTile(new Rect(x + tileWidth + tileGap, y + tileHeight + tileGap, tileWidth, tileHeight), "QUEST BOARD", "Contracts and rewards", new Color(0.72f, 0.35f, 0.95f), () => { pageScroll = Vector2.zero; questsOpen = true; });
        DrawHomeTile(new Rect(x, y + (tileHeight + tileGap) * 2f, tileWidth, tileHeight), "PROMO CODES", "Redeem special access", new Color(0.95f, 0.74f, 0.18f), () => { pageScroll = Vector2.zero; promoOpen = true; });
        bool dev = EconomyManager.Instance != null && EconomyManager.Instance.DevModeUnlocked;
        DrawHomeTile(new Rect(x + tileWidth + tileGap, y + (tileHeight + tileGap) * 2f, tileWidth, tileHeight), "SETTINGS", "Audio, controls and display", new Color(0.48f, 0.68f, 0.9f), () => { pageScroll = Vector2.zero; settingsOpen = true; });

        if (dev)
        {
            GUI.backgroundColor = new Color(0.72f, 0.12f, 0.08f);
            if (GUI.Button(new Rect(Screen.width - 178f, 88f, 150f, 38f), "ADMIN PANEL")) { pageScroll = Vector2.zero; adminOpen = true; }
            GUI.backgroundColor = Color.white;
        }

        GUI.Label(new Rect(0f, Mathf.Min(Screen.height - 38f, y + (tileHeight + tileGap) * 3f + 8f), Screen.width, 28f), pausedMatch ? "P resumes  •  ESC exits the match for 0.5x credits" : "P pauses during a match  •  ESC exits the match", CenteredStyle(13));
    }

    private static void DrawHomeTile(Rect rect, string title, string subtitle, Color accent, System.Action action)
    {
        GUI.backgroundColor = new Color(0.08f, 0.12f, 0.16f);
        if (GUI.Button(rect, "")) action();
        GUI.backgroundColor = Color.white;
        GUI.color = accent;
        GUI.DrawTexture(new Rect(rect.x, rect.y, 5f, rect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle heading = CenteredStyle(19);
        heading.fontStyle = FontStyle.Bold;
        heading.normal.textColor = accent;
        GUI.Label(new Rect(rect.x + 16f, rect.y + 15f, rect.width - 32f, 28f), title, heading);
        GUIStyle detail = CenteredStyle(13);
        detail.normal.textColor = new Color(0.72f, 0.8f, 0.85f);
        GUI.Label(new Rect(rect.x + 16f, rect.y + 47f, rect.width - 32f, 25f), subtitle, detail);
    }

    private void DrawModeSelection()
    {
        string[] names = { "CLASSIC", "FORTRESS", "STRONG", "CONVOY", "CAMPAIGN", "HARDCORE" };
        string[] descriptions =
        {
            "Standard wave survival.\nEnemies grow stronger every wave.",
            "1 vs 10. Attack the enemy fortress,\ndefend yours, and stop reinforcements.",
            "1 vs 20–30 with boosted health, damage,\nand regeneration. Best of three rounds.",
            "Escort a top-secret briefcase with four\nAI bodyguards and reach extraction.",
            "A six-mission story across outposts, forests,\nprisons, cities, laboratories, and the capital.",
            "One life, scarce supplies, endless enemies.\nYour survival time is the score."
        };
        Color[] accents =
        {
            new Color(0.18f, 0.65f, 0.95f), new Color(0.95f, 0.42f, 0.16f), new Color(0.68f, 0.28f, 0.95f),
            new Color(0.16f, 0.78f, 0.52f), new Color(0.95f, 0.72f, 0.18f), new Color(0.95f, 0.18f, 0.12f)
        };

        GUI.Label(new Rect(0f, 86f, Screen.width, 42f), "SELECT GAME MODE", CenteredStyle(26));
        float cardWidth = Mathf.Min(330f, (Screen.width - 80f) / 3f);
        float cardHeight = 160f;
        float gap = 18f;
        for (int i = 0; i < names.Length; i++)
        {
            int columns = 3;
            int column = i % columns;
            int row = i / columns;
            float rowWidth = columns * cardWidth + (columns - 1) * gap;
            Rect card = new Rect((Screen.width - rowWidth) * 0.5f + column * (cardWidth + gap), 150f + row * (cardHeight + gap), cardWidth, cardHeight);
            bool unlocked = EconomyManager.Instance == null || EconomyManager.Instance.IsModeUnlocked(i);
            GUI.backgroundColor = new Color(0.1f, 0.15f, 0.19f);
            GUI.enabled = unlocked;
            if (GUI.Button(card, "")) StartGame((GameModeManager.Mode)i);
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;
            GUI.color = accents[i];
            GUI.DrawTexture(new Rect(card.x, card.y, 5f, card.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUIStyle modeTitle = CenteredStyle(21);
            modeTitle.fontStyle = FontStyle.Bold;
            modeTitle.normal.textColor = accents[i];
            GUI.Label(new Rect(card.x + 14f, card.y + 18f, card.width - 28f, 30f), names[i], modeTitle);
            GUIStyle description = CenteredStyle(14);
            description.wordWrap = true;
            description.normal.textColor = new Color(0.78f, 0.84f, 0.88f);
            GUI.Label(new Rect(card.x + 18f, card.y + 58f, card.width - 36f, 76f), descriptions[i], description);
            if (!unlocked)
            {
                GUIStyle locked = CenteredStyle(14);
                locked.fontStyle = FontStyle.Bold;
                locked.normal.textColor = new Color(1f, 0.72f, 0.2f);
                GUI.Label(new Rect(card.x, card.y + 130f, card.width, 24f), $"LOCKED  ◆ {EconomyManager.ModePrices[i]}", locked);
            }
        }

        if (GUI.Button(new Rect(18f, 18f, 120f, 40f), "BACK")) playModeOpen = false;
    }

    private void DrawSettings()
    {
        GameSettingsManager settings = FindAnyObjectByType<GameSettingsManager>();
        if (settings == null) return;
        GUI.Label(new Rect(0f, 82f, Screen.width, 42f), "SETTINGS", CenteredStyle(28));
        Rect panel = new Rect(Screen.width * 0.5f - 350f, 145f, 700f, 405f);
        GUI.color = new Color(0.045f, 0.065f, 0.085f, 0.97f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(0.3f, 0.72f, 1f);
        GUI.DrawTexture(new Rect(panel.x, panel.y, 5f, panel.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float labelX = panel.x + 35f;
        float sliderX = panel.x + 275f;
        float sliderWidth = 330f;
        DrawSettingLabel(new Rect(labelX, panel.y + 28f, 220f, 28f), "MASTER VOLUME", $"{Mathf.RoundToInt(settings.MasterVolume * 100f)}%");
        float volume = GUI.HorizontalSlider(new Rect(sliderX, panel.y + 40f, sliderWidth, 20f), settings.MasterVolume, 0f, 1f);
        if (!Mathf.Approximately(volume, settings.MasterVolume)) settings.SetVolume(volume);

        DrawSettingLabel(new Rect(labelX, panel.y + 88f, 220f, 28f), "MOUSE SENSITIVITY", settings.MouseSensitivity.ToString("0.00"));
        float sensitivity = GUI.HorizontalSlider(new Rect(sliderX, panel.y + 100f, sliderWidth, 20f), settings.MouseSensitivity, 0.03f, 0.4f);
        if (!Mathf.Approximately(sensitivity, settings.MouseSensitivity)) settings.SetSensitivity(sensitivity);

        DrawSettingLabel(new Rect(labelX, panel.y + 148f, 220f, 28f), "FIELD OF VIEW", Mathf.RoundToInt(settings.FieldOfView).ToString());
        float fieldOfView = GUI.HorizontalSlider(new Rect(sliderX, panel.y + 160f, sliderWidth, 20f), settings.FieldOfView, 60f, 110f);
        if (!Mathf.Approximately(fieldOfView, settings.FieldOfView)) settings.SetFieldOfView(fieldOfView);

        GUI.Label(new Rect(labelX, panel.y + 205f, 220f, 30f), "GRAPHICS QUALITY", SettingsLabelStyle());
        string[] qualityNames = QualitySettings.names;
        float qualityButtonWidth = sliderWidth / Mathf.Max(1, qualityNames.Length);
        for (int i = 0; i < qualityNames.Length; i++)
        {
            GUI.backgroundColor = settings.QualityLevel == i ? new Color(0.18f, 0.68f, 0.95f) : new Color(0.16f, 0.2f, 0.24f);
            if (GUI.Button(new Rect(sliderX + i * qualityButtonWidth, panel.y + 200f, qualityButtonWidth - 4f, 36f), qualityNames[i])) settings.SetQuality(i);
        }
        GUI.backgroundColor = settings.Fullscreen ? new Color(0.15f, 0.75f, 0.42f) : new Color(0.22f, 0.25f, 0.28f);
        if (GUI.Button(new Rect(labelX, panel.y + 265f, 210f, 42f), settings.Fullscreen ? "FULLSCREEN: ON" : "FULLSCREEN: OFF")) settings.SetFullscreen(!settings.Fullscreen);
        GUI.backgroundColor = new Color(0.18f, 0.55f, 0.85f);
        if (GUI.Button(new Rect(sliderX + 80f, panel.y + 265f, 170f, 42f), "SAVE SETTINGS")) settings.Save();
        GUI.backgroundColor = Color.white;

        GUI.color = new Color(0.16f, 0.02f, 0.02f, 0.95f);
        GUI.DrawTexture(new Rect(labelX, panel.y + 330f, panel.width - 70f, 52f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (!confirmProgressReset)
        {
            if (GUI.Button(new Rect(panel.x + 230f, panel.y + 337f, 240f, 38f), "RESET ALL PROGRESS")) confirmProgressReset = true;
        }
        else
        {
            GUIStyle confirm = CenteredStyle(12);
            confirm.normal.textColor = new Color(1f, 0.72f, 0.2f);
            GUI.Label(new Rect(labelX + 8f, panel.y + 337f, 250f, 36f), "This cannot be undone.", confirm);
            GUI.backgroundColor = new Color(0.82f, 0.12f, 0.08f);
            if (GUI.Button(new Rect(panel.x + 300f, panel.y + 337f, 155f, 38f), "CONFIRM RESET")) ResetProgress();
            GUI.backgroundColor = Color.white;
            if (GUI.Button(new Rect(panel.x + 465f, panel.y + 337f, 100f, 38f), "CANCEL")) confirmProgressReset = false;
        }
        if (GUI.Button(new Rect(18f, 18f, 120f, 40f), "BACK")) { settingsOpen = false; confirmProgressReset = false; }
    }

    private static void DrawSettingLabel(Rect rect, string label, string value)
    {
        GUI.Label(rect, $"{label}   <color=#55C8FF>{value}</color>", SettingsLabelStyle());
    }

    private static GUIStyle SettingsLabelStyle()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 14, fontStyle = FontStyle.Bold, richText = true };
        style.normal.textColor = Color.white;
        return style;
    }

    private void ResetProgress()
    {
        FindAnyObjectByType<GameModeManager>()?.AbandonActiveMode();
        EconomyManager.Instance?.ResetAllProgress();
        vitals.InfiniteHealth = false;
        weapons.InfiniteAmmo = false;
        weapons.SetPlayerClass(SimpleRifle.PlayerClass.Soldier);
        weapons.SetLoadoutSlot(0, 0);
        weapons.SetLoadoutSlot(1, 1);
        weapons.SetLoadoutSlot(2, 0);
        weapons.SetLoadoutSlot(3, 1);
        vitals.RestoreForNewMode();
        weapons.RestoreSpawnAmmo();
        confirmProgressReset = false;
    }

    private void DrawPromoCodes()
    {
        EconomyManager economy = EconomyManager.Instance;
        GUI.Label(new Rect(0f, 100f, Screen.width, 42f), "PROMO CODE TERMINAL", CenteredStyle(28));
        Rect panel = new Rect(Screen.width * 0.5f - 300f, 175f, 600f, 270f);
        GUI.color = new Color(0.025f, 0.055f, 0.07f, 0.97f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(0.15f, 0.85f, 0.7f);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 4f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle terminal = CenteredStyle(15);
        terminal.normal.textColor = new Color(0.4f, 0.95f, 0.78f);
        GUI.Label(new Rect(panel.x + 30f, panel.y + 35f, panel.width - 60f, 30f), "SECURE REDEMPTION CHANNEL // ONLINE", terminal);
        GUIStyle field = new GUIStyle(GUI.skin.textField) { alignment = TextAnchor.MiddleCenter, fontSize = 21, fontStyle = FontStyle.Bold };
        promoCode = GUI.TextField(new Rect(panel.x + 75f, panel.y + 88f, panel.width - 150f, 48f), promoCode, 32, field);
        GUI.backgroundColor = new Color(0.12f, 0.7f, 0.52f);
        if (GUI.Button(new Rect(panel.x + 180f, panel.y + 153f, 240f, 44f), "REDEEM CODE"))
        {
            promoStatus = economy == null ? "SYSTEM OFFLINE" : economy.RedeemPromoCode(promoCode);
            promoCode = string.Empty;
        }
        GUI.backgroundColor = Color.white;
        GUIStyle status = CenteredStyle(14);
        status.normal.textColor = promoStatus.Contains("INVALID") ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.78f, 0.2f);
        GUI.Label(new Rect(panel.x + 25f, panel.y + 211f, panel.width - 50f, 36f), promoStatus, status);
        if (GUI.Button(new Rect(18f, 18f, 120f, 40f), "BACK")) promoOpen = false;
    }

    private void DrawAdminPanel()
    {
        EconomyManager economy = EconomyManager.Instance;
        if (economy == null || !economy.DevModeUnlocked) { adminOpen = false; return; }
        GUI.Label(new Rect(0f, 82f, Screen.width, 42f), "DEVELOPER COMMAND CENTER", CenteredStyle(28));
        GUIStyle warning = CenteredStyle(13);
        warning.normal.textColor = new Color(1f, 0.62f, 0.15f);
        GUI.Label(new Rect(0f, 120f, Screen.width, 28f), "AUTHORIZED PERSONNEL ONLY // CHANGES SAVE IMMEDIATELY", warning);

        Rect panel = new Rect(Screen.width * 0.5f - 390f, 165f, 780f, 360f);
        GUI.color = new Color(0.035f, 0.045f, 0.06f, 0.98f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(0.95f, 0.24f, 0.16f);
        GUI.DrawTexture(new Rect(panel.x, panel.y, 6f, panel.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        PlayerVitals playerVitals = GetComponent<PlayerVitals>();
        SimpleRifle rifle = GetComponent<SimpleRifle>();
        GUI.Label(new Rect(panel.x + 35f, panel.y + 22f, 330f, 30f), "CURRENCY COMMANDS", CenteredStyle(17));
        if (GUI.Button(new Rect(panel.x + 35f, panel.y + 65f, 100f, 42f), "+100")) economy.AdminGrantMoney(100);
        if (GUI.Button(new Rect(panel.x + 145f, panel.y + 65f, 100f, 42f), "+1,000")) economy.AdminGrantMoney(1000);
        if (GUI.Button(new Rect(panel.x + 255f, panel.y + 65f, 110f, 42f), "+10,000")) economy.AdminGrantMoney(10000);

        GUI.Label(new Rect(panel.x + 410f, panel.y + 22f, 330f, 30f), "PLAYER OVERRIDES", CenteredStyle(17));
        GUI.backgroundColor = playerVitals.InfiniteHealth ? new Color(0.12f, 0.8f, 0.4f) : new Color(0.25f, 0.28f, 0.32f);
        if (GUI.Button(new Rect(panel.x + 420f, panel.y + 65f, 145f, 42f), playerVitals.InfiniteHealth ? "GOD MODE: ON" : "GOD MODE: OFF")) playerVitals.InfiniteHealth = !playerVitals.InfiniteHealth;
        GUI.backgroundColor = rifle.InfiniteAmmo ? new Color(0.12f, 0.8f, 0.4f) : new Color(0.25f, 0.28f, 0.32f);
        if (GUI.Button(new Rect(panel.x + 575f, panel.y + 65f, 145f, 42f), rifle.InfiniteAmmo ? "INF AMMO: ON" : "INF AMMO: OFF")) rifle.InfiniteAmmo = !rifle.InfiniteAmmo;
        GUI.backgroundColor = Color.white;

        GUI.Label(new Rect(panel.x + 35f, panel.y + 145f, panel.width - 70f, 30f), "WORLD & PROGRESSION", CenteredStyle(17));
        if (GUI.Button(new Rect(panel.x + 55f, panel.y + 195f, 200f, 46f), "FULL HEAL + RESTOCK"))
        {
            playerVitals.RestoreForNewMode();
            rifle.RestoreSpawnAmmo();
        }
        if (GUI.Button(new Rect(panel.x + 290f, panel.y + 195f, 200f, 46f), "UNLOCK ALL CONTENT")) economy.AdminUnlockAll();
        if (GUI.Button(new Rect(panel.x + 525f, panel.y + 195f, 200f, 46f), "CLEAR ALL ENEMIES")) FindAnyObjectByType<WaveManager>()?.ClearEnemies();
        GUIStyle active = CenteredStyle(13);
        active.normal.textColor = new Color(0.45f, 0.9f, 1f);
        GUI.Label(new Rect(panel.x + 30f, panel.y + 285f, panel.width - 60f, 35f), $"DEV MODE ACTIVE  •  CREDITS {economy.Money:N0}  •  HEALTH {(playerVitals.InfiniteHealth ? "∞" : Mathf.CeilToInt(playerVitals.Health).ToString())}  •  AMMO {(rifle.InfiniteAmmo ? "∞" : "NORMAL")}", active);
        if (GUI.Button(new Rect(18f, 18f, 120f, 40f), "BACK")) adminOpen = false;
    }

    private void DrawShop()
    {
        EconomyManager economy = EconomyManager.Instance;
        if (economy == null) return;
        GUI.Label(new Rect(0f, 82f, Screen.width, 38f), "BLACK MARKET", CenteredStyle(27));
        GUIStyle balance = CenteredStyle(18);
        balance.normal.textColor = new Color(1f, 0.78f, 0.18f);
        GUI.Label(new Rect(Screen.width - 270f, 28f, 240f, 38f), $"◆ {economy.Money:N0} CREDITS", balance);

        string[] categories = { "MODES", "CLASSES", "WEAPONS", "PERKS", "STARTING LOOT" };
        float tabWidth = 145f;
        float tabStart = (Screen.width - tabWidth * categories.Length) * 0.5f;
        for (int i = 0; i < categories.Length; i++)
        {
            GUI.backgroundColor = shopCategory == i ? new Color(0.16f, 0.65f, 0.9f) : new Color(0.12f, 0.18f, 0.22f);
            if (GUI.Button(new Rect(tabStart + i * tabWidth, 124f, tabWidth - 6f, 40f), categories[i])) shopCategory = i;
        }
        GUI.backgroundColor = Color.white;

        if (shopCategory == 0) DrawModeShop(economy);
        else if (shopCategory == 1) DrawClassShop(economy);
        else if (shopCategory == 2) DrawWeaponShop(economy);
        else if (shopCategory == 3) DrawPerkShop(economy);
        else DrawLootShop(economy);
        if (GUI.Button(new Rect(18f, 18f, 120f, 40f), "BACK")) shopOpen = false;
    }

    private static void DrawModeShop(EconomyManager economy)
    {
        string[] names = { "CLASSIC", "FORTRESS", "STRONG", "CONVOY", "CAMPAIGN", "HARDCORE" };
        string[] subtitles = { "Wave survival", "Base warfare", "One-person army", "Escort operation", "Six-mission story", "One-life survival" };
        int columns = Screen.width < 950 ? 3 : 5;
        float width = Mathf.Min(220f, (Screen.width - 50f) / columns);
        for (int i = 0; i < names.Length; i++)
        {
            int rowColumns = i < columns ? columns : names.Length - columns;
            int row = i / columns;
            int column = i % columns;
            float start = (Screen.width - width * rowColumns) * 0.5f;
            Rect card = new Rect(start + column * width, 195f + row * 205f, width - 12f, 190f);
            bool owned = economy.IsModeUnlocked(i);
            DrawStoreCard(card, names[i], subtitles[i], owned, EconomyManager.ModePrices[i]);
            if (!owned && GUI.Button(new Rect(card.x + 18f, card.y + 136f, card.width - 36f, 38f), $"BUY  ◆ {EconomyManager.ModePrices[i]}")) economy.BuyMode(i);
        }
    }

    private static void DrawClassShop(EconomyManager economy)
    {
        string[] names = { "SOLDIER", "TANK", "ENGINEER", "SNIPER", "DEMOMAN", "SPECIAL FORCE", "PIRATE" };
        string[] roles = { "Balanced fighter", "Heavy frontline", "Build and defend", "Long-range expert", "Explosives master", "Fast tactical agent", "Black-powder bruiser" };
        float width = Mathf.Min(210f, (Screen.width - 36f) / 3f);
        int maxColumns = Screen.width < 760 ? 3 : 4;
        for (int i = 0; i < names.Length; i++)
        {
            int row = i / maxColumns;
            int column = i % maxColumns;
            int columns = Mathf.Min(maxColumns, names.Length - row * maxColumns);
            float start = (Screen.width - columns * width) * 0.5f;
            Rect card = new Rect(start + column * width, 185f + row * 185f, width - 12f, 165f);
            bool owned = economy.IsClassUnlocked(i);
            DrawStoreCard(card, names[i], roles[i], owned, EconomyManager.ClassPrices[i]);
            if (!owned && GUI.Button(new Rect(card.x + 16f, card.y + 112f, card.width - 32f, 36f), $"BUY  ◆ {EconomyManager.ClassPrices[i]}")) economy.BuyClass(i);
        }
    }

    private void DrawWeaponShop(EconomyManager economy)
    {
        string[] slots = { "PRIMARY", "SECONDARY", "MELEE", "UTILITY" };
        float start = Screen.width * 0.5f - 260f;
        for (int i = 0; i < 4; i++)
        {
            GUI.backgroundColor = weaponShopSlot == i ? new Color(0.75f, 0.34f, 0.12f) : new Color(0.14f, 0.18f, 0.21f);
            if (GUI.Button(new Rect(start + i * 130f, 178f, 124f, 36f), slots[i])) weaponShopSlot = i;
        }
        GUI.backgroundColor = Color.white;
        int count = weapons.GetLoadoutOptionCount(weaponShopSlot);
        float width = 210f;
        float height = 98f;
        int columns = Mathf.Min(Mathf.Max(3, Mathf.FloorToInt((Screen.width - 30f) / width)), count);
        float gridStart = (Screen.width - columns * width) * 0.5f;
        for (int i = 0; i < count; i++)
        {
            int row = i / columns;
            int column = i % columns;
            string name = weapons.GetLoadoutOptionName(weaponShopSlot, i);
            Rect card = new Rect(gridStart + column * width, 230f + row * (height + 10f), width - 10f, height);
            bool owned = economy.IsWeaponUnlocked(weaponShopSlot, i);
            GUI.color = owned ? new Color(0.08f, 0.22f, 0.18f, 0.95f) : new Color(0.08f, 0.11f, 0.14f, 0.95f);
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(card.x + 8f, card.y + 7f, card.width - 16f, 38f), name, CenteredStyle(13));
            if (owned) GUI.Label(new Rect(card.x, card.y + 55f, card.width, 28f), "OWNED", CenteredStyle(12));
            else if (GUI.Button(new Rect(card.x + 15f, card.y + 52f, card.width - 30f, 32f), $"BUY  ◆ {economy.WeaponPrice(weaponShopSlot, i)}")) economy.BuyWeapon(weaponShopSlot, i, name);
        }
    }

    private static void DrawPerkShop(EconomyManager economy)
    {
        int columns = Screen.width < 900 ? 2 : 4;
        float width = Mathf.Min(245f, (Screen.width - 40f) / columns);
        for (int i = 0; i < EconomyManager.PerkNames.Length; i++)
        {
            int row = i / columns;
            int column = i % columns;
            float start = (Screen.width - width * Mathf.Min(columns, EconomyManager.PerkNames.Length - row * columns)) * 0.5f;
            Rect card = new Rect(start + column * width, 200f + row * 205f, width - 14f, 190f);
            bool owned = economy.IsPerkUnlocked(i);
            DrawStoreCard(card, EconomyManager.PerkNames[i], EconomyManager.PerkDescriptions[i], owned, EconomyManager.PerkPrices[i]);
            if (!owned && GUI.Button(new Rect(card.x + 16f, card.y + 136f, card.width - 32f, 38f), $"BUY  ◆ {EconomyManager.PerkPrices[i]}")) economy.BuyPerk(i);
        }
    }

    private static void DrawLootShop(EconomyManager economy)
    {
        GUIStyle info = CenteredStyle(14);
        info.normal.textColor = new Color(1f, 0.45f, 0.22f);
        GUI.Label(new Rect(0f, 172f, Screen.width, 28f), "HARDCORE STARTING LOADOUT — OWNED ITEMS APPLY TO EVERY RUN", info);
        int columns = Screen.width < 900 ? 2 : 4;
        float width = Mathf.Min(245f, (Screen.width - 40f) / columns);
        for (int i = 0; i < EconomyManager.LootNames.Length; i++)
        {
            int row = i / columns;
            int column = i % columns;
            float start = (Screen.width - width * Mathf.Min(columns, EconomyManager.LootNames.Length - row * columns)) * 0.5f;
            Rect card = new Rect(start + column * width, 215f + row * 205f, width - 14f, 190f);
            bool owned = economy.IsLootUnlocked(i);
            DrawStoreCard(card, EconomyManager.LootNames[i], EconomyManager.LootDescriptions[i], owned, EconomyManager.LootPrices[i]);
            if (!owned && GUI.Button(new Rect(card.x + 16f, card.y + 136f, card.width - 32f, 38f), $"BUY  ◆ {EconomyManager.LootPrices[i]}")) economy.BuyLoot(i);
        }
    }

    private void DrawMatchReport()
    {
        GameModeManager manager = FindAnyObjectByType<GameModeManager>();
        GUI.Label(new Rect(0f, 82f, Screen.width, 42f), "AFTER ACTION REPORT", CenteredStyle(28));
        Rect panel = new Rect(Screen.width * 0.5f - 290f, 145f, 580f, 390f);
        GUI.color = new Color(0.035f, 0.055f, 0.075f, 0.98f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = new Color(0.25f, 0.75f, 1f);
        GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 5f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle report = CenteredStyle(18);
        report.fontStyle = FontStyle.Bold;
        report.normal.textColor = new Color(0.82f, 0.9f, 0.96f);
        GUI.Label(new Rect(panel.x + 35f, panel.y + 28f, panel.width - 70f, 285f), manager == null ? "NO REPORT AVAILABLE" : manager.LastReport, report);
        if (GUI.Button(new Rect(panel.x + 170f, panel.y + 325f, 240f, 44f), "RETURN TO COMMAND")) reportOpen = false;
    }

    private static void DrawStoreCard(Rect card, string title, string description, bool owned, int price)
    {
        GUI.color = owned ? new Color(0.05f, 0.24f, 0.18f, 0.96f) : new Color(0.07f, 0.1f, 0.14f, 0.96f);
        GUI.DrawTexture(card, Texture2D.whiteTexture);
        GUI.color = owned ? new Color(0.2f, 1f, 0.62f) : new Color(0.3f, 0.78f, 1f);
        GUI.DrawTexture(new Rect(card.x, card.y, card.width, 4f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle titleStyle = CenteredStyle(17);
        titleStyle.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(card.x + 8f, card.y + 18f, card.width - 16f, 30f), title, titleStyle);
        GUIStyle descriptionStyle = CenteredStyle(13);
        descriptionStyle.wordWrap = true;
        descriptionStyle.normal.textColor = new Color(0.72f, 0.8f, 0.84f);
        GUI.Label(new Rect(card.x + 12f, card.y + 54f, card.width - 24f, 58f), description, descriptionStyle);
        if (owned) GUI.Label(new Rect(card.x, card.y + card.height - 42f, card.width, 28f), "OWNED", CenteredStyle(13));
    }

    private void DrawQuests()
    {
        EconomyManager economy = EconomyManager.Instance;
        if (economy == null) return;
        GUI.Label(new Rect(0f, 82f, Screen.width, 42f), "QUEST BOARD", CenteredStyle(27));
        GUI.Label(new Rect(0f, 118f, Screen.width, 28f), "Complete contracts. Claim credits. Expand your arsenal.", CenteredStyle(14));
        float width = Mathf.Min(720f, Screen.width - 50f);
        float x = (Screen.width - width) * 0.5f;
        for (int i = 0; i < economy.QuestCount; i++)
        {
            Rect card = new Rect(x, 165f + i * 105f, width, 88f);
            int progress = economy.QuestProgress(i);
            int goal = economy.QuestGoal(i);
            bool claimed = economy.IsQuestClaimed(i);
            GUI.color = claimed ? new Color(0.05f, 0.2f, 0.14f, 0.92f) : new Color(0.07f, 0.11f, 0.15f, 0.96f);
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUIStyle questTitle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
            questTitle.normal.textColor = new Color(0.3f, 0.78f, 1f);
            GUI.Label(new Rect(card.x + 18f, card.y + 10f, 250f, 26f), economy.QuestName(i), questTitle);
            GUI.Label(new Rect(card.x + 18f, card.y + 38f, 400f, 24f), economy.QuestDescription(i));
            GUI.Label(new Rect(card.x + 430f, card.y + 14f, 105f, 26f), $"{progress} / {goal}", CenteredStyle(15));
            GUI.Label(new Rect(card.x + 430f, card.y + 43f, 105f, 24f), $"◆ {economy.QuestReward(i)}", CenteredStyle(14));
            if (claimed) GUI.Label(new Rect(card.x + width - 165f, card.y + 24f, 140f, 38f), "CLAIMED", CenteredStyle(14));
            else
            {
                GUI.enabled = progress >= goal;
                if (GUI.Button(new Rect(card.x + width - 165f, card.y + 24f, 140f, 38f), "CLAIM REWARD")) economy.ClaimQuest(i);
                GUI.enabled = true;
            }
        }
        if (GUI.Button(new Rect(18f, 18f, 120f, 40f), "BACK")) questsOpen = false;
    }

    private void DrawLoadout()
    {
        float panelWidth = Mathf.Min(900f, Screen.width - 30f);
        float startX = (Screen.width - panelWidth) * 0.5f;
        DrawClassSelector(startX, panelWidth, 128f);
        if (selectedLoadoutSlot < 0) DrawLoadoutCards(startX, panelWidth);
        else DrawWeaponPicker(startX, panelWidth, selectedLoadoutSlot);

        if (GUI.Button(new Rect(18f, 18f, 120f, 40f), selectedLoadoutSlot < 0 ? "BACK" : "LOADOUT"))
        {
            if (selectedLoadoutSlot >= 0) selectedLoadoutSlot = -1;
            else loadoutOpen = false;
        }
    }

    private void DrawClassSelector(float startX, float panelWidth, float y)
    {
        string[] classNames = { "SOLDIER", "TANK", "ENGINEER", "SNIPER", "DEMOMAN", "SPECIAL FORCE", "PIRATE" };
        float classWidth = panelWidth / classNames.Length;
        GUI.color = new Color(0.04f, 0.07f, 0.09f, 0.95f);
        GUI.DrawTexture(new Rect(startX - 8f, y, panelWidth + 16f, 76f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(startX, y - 29f, panelWidth, 28f), "SELECT CLASS", CenteredStyle(20));
        GUIStyle classStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
        for (int classIndex = 0; classIndex < classNames.Length; classIndex++)
        {
            bool active = (int)weapons.CurrentClass == classIndex;
            bool unlocked = EconomyManager.Instance == null || EconomyManager.Instance.IsClassUnlocked(classIndex);
            GUI.backgroundColor = active ? new Color(0.2f, 0.75f, 0.3f) : new Color(0.16f, 0.2f, 0.23f);
            GUI.enabled = unlocked;
            string classLabel = unlocked ? classNames[classIndex] : $"LOCKED\n{classNames[classIndex]}";
            if (GUI.Button(new Rect(startX + classIndex * classWidth + 3f, y + 10f, classWidth - 7f, 54f), classLabel, classStyle))
            {
                weapons.SetPlayerClass((SimpleRifle.PlayerClass)classIndex);
                selectedLoadoutSlot = -1;
            }
            GUI.enabled = true;
        }
        GUI.backgroundColor = Color.white;
    }

    private void DrawLoadoutCards(float startX, float panelWidth)
    {
        GUI.Label(new Rect(startX, 218f, panelWidth, 42f), $"{weapons.CurrentClass.ToString().ToUpper()} LOADOUT", CenteredStyle(28));
        string[] slotLabels = { "SLOT 1", "SLOT 2", "MELEE", "UTILITY" };
        float gap = 12f;
        float cardWidth = (panelWidth - gap * 3f) / 4f;
        for (int slot = 0; slot < 4; slot++)
        {
            Rect card = new Rect(startX + slot * (cardWidth + gap), 275f, cardWidth, 230f);
            GUI.backgroundColor = new Color(0.12f, 0.18f, 0.22f);
            if (GUI.Button(card, "")) selectedLoadoutSlot = slot;
            GUI.backgroundColor = Color.white;
            GUI.Label(new Rect(card.x, card.y + 10f, card.width, 28f), slotLabels[slot], CenteredStyle(18));
            DrawWeaponIcon(new Rect(card.x + 20f, card.y + 52f, card.width - 40f, 95f), slot, weapons.GetLoadoutSlotName(slot));
            GUI.Label(new Rect(card.x + 8f, card.y + 158f, card.width - 16f, 42f), weapons.GetLoadoutSlotName(slot), CenteredStyle(15));
            GUI.Label(new Rect(card.x + 8f, card.y + 204f, card.width - 16f, 20f), "CLICK TO CHANGE", CenteredStyle(11));
        }
    }

    private void DrawWeaponPicker(float startX, float panelWidth, int slot)
    {
        string[] slotLabels = { "SLOT 1", "SLOT 2", "MELEE", "UTILITY" };
        GUI.Label(new Rect(startX, 218f, panelWidth, 42f), $"SELECT {slotLabels[slot]} WEAPON", CenteredStyle(28));
        int count = weapons.GetClassOptionCount(slot);
        float gap = 16f;
        float cardWidth = Mathf.Min(250f, (panelWidth - gap * (count - 1)) / count);
        float totalWidth = cardWidth * count + gap * (count - 1);
        float x = (Screen.width - totalWidth) * 0.5f;
        for (int option = 0; option < count; option++)
        {
            int weapon = weapons.GetClassOptionIndex(slot, option);
            bool selected = weapons.IsLoadoutSelection(slot, weapon);
            bool unlocked = EconomyManager.Instance == null || EconomyManager.Instance.IsWeaponUnlocked(slot, weapon);
            Rect card = new Rect(x + option * (cardWidth + gap), 285f, cardWidth, 220f);
            GUI.backgroundColor = selected ? new Color(0.15f, 0.65f, 0.9f) : new Color(0.14f, 0.19f, 0.22f);
            GUI.enabled = unlocked;
            if (GUI.Button(card, ""))
            {
                weapons.SetLoadoutSlot(slot, weapon);
                selectedLoadoutSlot = -1;
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;
            string weaponName = weapons.GetLoadoutOptionName(slot, weapon);
            DrawWeaponIcon(new Rect(card.x + 22f, card.y + 30f, card.width - 44f, 105f), slot, weaponName);
            GUI.Label(new Rect(card.x + 8f, card.y + 145f, card.width - 16f, 48f), weaponName, CenteredStyle(16));
            if (selected) GUI.Label(new Rect(card.x, card.y + 194f, card.width, 20f), "EQUIPPED", CenteredStyle(12));
            else if (!unlocked) GUI.Label(new Rect(card.x, card.y + 194f, card.width, 20f), "BUY IN BLACK MARKET", CenteredStyle(11));
        }
    }

    private static void DrawWeaponIcon(Rect rect, int slot, string weaponName)
    {
        GUI.color = new Color(0.025f, 0.035f, 0.04f, 0.9f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = slot == 2 ? new Color(0.75f, 0.78f, 0.8f) : slot == 3 ? new Color(0.25f, 0.75f, 0.45f) : new Color(0.28f, 0.58f, 0.72f);
        float length = weaponName.Contains("PISTOL") || weaponName.Contains("HANDGUN") ? 0.5f : weaponName.Contains("SHIELD") ? 0.7f : 0.82f;
        GUI.DrawTexture(new Rect(rect.x + rect.width * (1f - length) * 0.5f, rect.y + rect.height * 0.38f, rect.width * length, rect.height * 0.24f), Texture2D.whiteTexture);
        if (slot == 2) GUI.DrawTexture(new Rect(rect.center.x - 5f, rect.y + 12f, 10f, rect.height - 24f), Texture2D.whiteTexture);
        else GUI.DrawTexture(new Rect(rect.x + rect.width * 0.35f, rect.y + rect.height * 0.56f, rect.width * 0.13f, rect.height * 0.28f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void DrawLegacyLoadout()
    {
        float panelWidth = Mathf.Min(900f, Screen.width - 30f);
        float startX = (Screen.width - panelWidth) * 0.5f;
        float startY = Mathf.Clamp(Screen.height * 0.36f, 245f, 285f);
        float availableHeight = Mathf.Max(180f, Screen.height - startY - 120f);
        float rowHeight = Mathf.Clamp(availableHeight / 4f, 42f, 58f);
        float labelWidth = Mathf.Clamp(panelWidth * 0.14f, 65f, 90f);
        float buttonGap = 5f;
        GUIStyle optionStyle = new GUIStyle(GUI.skin.button) { fontSize = Screen.width < 800 ? 10 : 12, wordWrap = true };

        GUI.Label(new Rect(Screen.width * 0.5f - 250f, startY - 48f, 500f, 38f), $"{weapons.CurrentClass.ToString().ToUpper()} LOADOUT", CenteredStyle(Screen.height < 650 ? 21 : 26));

        string[] classNames = { "SOLDIER", "TANK", "ENGINEER", "SNIPER", "DEMOMAN" };
        float classWidth = panelWidth / classNames.Length;
        Rect classPanel = new Rect(startX - 8f, startY - 132f, panelWidth + 16f, 76f);
        GUI.color = new Color(0.04f, 0.07f, 0.09f, 0.95f);
        GUI.DrawTexture(classPanel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(startX, startY - 160f, panelWidth, 28f), "SELECT CLASS", CenteredStyle(20));
        GUIStyle classStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
        for (int classIndex = 0; classIndex < classNames.Length; classIndex++)
        {
            bool active = (int)weapons.CurrentClass == classIndex;
            GUI.backgroundColor = active ? new Color(0.2f, 0.75f, 0.3f) : new Color(0.16f, 0.2f, 0.23f);
            if (GUI.Button(new Rect(startX + classIndex * classWidth + 3f, startY - 122f, classWidth - 7f, 54f), classNames[classIndex], classStyle))
                weapons.SetPlayerClass((SimpleRifle.PlayerClass)classIndex);
        }
        GUI.backgroundColor = Color.white;

        for (int slot = 0; slot < 4; slot++)
        {
            float rowY = startY + slot * rowHeight;
            GUI.Label(new Rect(startX, rowY, labelWidth, rowHeight - 8f), $"SLOT {slot + 1}", CenteredStyle(15));
            int optionCount = weapons.GetClassOptionCount(slot);
            float buttonWidth = (panelWidth - labelWidth - buttonGap * optionCount) / optionCount;
            for (int classOption = 0; classOption < optionCount; classOption++)
            {
                int weapon = weapons.GetClassOptionIndex(slot, classOption);
                bool selected = weapons.IsLoadoutSelection(slot, weapon);
                GUI.backgroundColor = selected ? new Color(0.15f, 0.7f, 1f) : new Color(0.2f, 0.25f, 0.3f);
                Rect button = new Rect(startX + labelWidth + buttonGap + classOption * (buttonWidth + buttonGap), rowY, buttonWidth, rowHeight - 8f);
                string optionName = weapons.GetLoadoutOptionName(slot, weapon);
                if (GUI.Button(button, selected ? $"✓ {optionName}" : optionName, optionStyle))
                    weapons.SetLoadoutSlot(slot, weapon);
            }
            GUI.backgroundColor = Color.white;
            GUI.color = Color.white;
        }

        if (GUI.Button(new Rect(18f, 18f, 120f, 40f), "← BACK")) loadoutOpen = false;
    }

    private static GUIStyle CenteredStyle(int size)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = size };
        style.normal.textColor = Color.white;
        return style;
    }
}
