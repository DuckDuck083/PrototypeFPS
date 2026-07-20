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
        GUI.Label(new Rect(Screen.width * 0.5f - 300f, 70f, 600f, 70f), "PROTOTYPE FPS", title);

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
        GUI.Label(new Rect(Screen.width * 0.5f - 250f, 155f, 500f, 38f), "ACTIVE LOADOUT", CenteredStyle(26));
        float startX = Screen.width * 0.5f - 310f;
        float startY = 215f;

        for (int slot = 0; slot < 4; slot++)
        {
            GUI.Label(new Rect(startX, startY + slot * 58f, 90f, 42f), $"SLOT {slot + 1}", CenteredStyle(17));
            for (int weapon = 0; weapon < 4; weapon++)
            {
                bool selected = weapons.IsLoadoutSelection(slot, weapon);
                GUI.backgroundColor = selected ? new Color(0.15f, 0.7f, 1f) : new Color(0.2f, 0.25f, 0.3f);
                Rect button = new Rect(startX + 100f + weapon * 130f, startY + slot * 58f, 120f, 42f);
                string optionName = weapons.GetLoadoutOptionName(slot, weapon);
                if (GUI.Button(button, selected ? $"✓ {optionName}" : optionName))
                    weapons.SetLoadoutSlot(slot, weapon);
            }
            GUI.backgroundColor = Color.white;
            GUI.color = Color.white;
        }

        GUI.Label(new Rect(Screen.width * 0.5f - 300f, 465f, 600f, 30f), "Click a weapon in each row. Blue buttons are equipped. Duplicates are allowed.", CenteredStyle(15));
        if (GUI.Button(new Rect(Screen.width * 0.5f - 120f, 510f, 240f, 45f), "BACK")) loadoutOpen = false;
    }

    private static GUIStyle CenteredStyle(int size)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = size };
        style.normal.textColor = Color.white;
        return style;
    }
}
