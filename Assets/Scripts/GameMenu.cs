using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GameMenu : MonoBehaviour
{
    private FirstPersonController movement;
    private SimpleRifle weapons;
    private PlayerVitals vitals;
    private bool menuOpen = true;
    private bool loadoutOpen;

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
        if (!menuOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            OpenMenu();
    }

    private void OpenMenu()
    {
        menuOpen = true;
        loadoutOpen = false;
        Time.timeScale = 0f;
        movement.enabled = false;
        weapons.enabled = false;
        vitals.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void StartGame()
    {
        menuOpen = false;
        Time.timeScale = 1f;
        vitals.enabled = true;
        movement.enabled = true;
        weapons.enabled = true;
        movement.RestoreControls();
        weapons.EquipLoadoutSlot(0);
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
        GUI.Label(new Rect(Screen.width * 0.5f - 300f, loadoutOpen ? 18f : 70f, 600f, 70f), "PROTOTYPE FPS", title);

        if (loadoutOpen)
            DrawLoadout();
        else
            DrawMainMenu();
    }

    private void DrawMainMenu()
    {
        float x = Screen.width * 0.5f - 120f;
        float y = Screen.height * 0.5f - 25f;
        if (GUI.Button(new Rect(x, y, 240f, 48f), "START TRAINING")) StartGame();
        if (GUI.Button(new Rect(x, y + 62f, 240f, 48f), "LOADOUT")) loadoutOpen = true;
        GUI.Label(new Rect(x - 80f, y + 130f, 400f, 30f), "Press Escape during play to return here", CenteredStyle(14));
    }

    private void DrawLoadout()
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
