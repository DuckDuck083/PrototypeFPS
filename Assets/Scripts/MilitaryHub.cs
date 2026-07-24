using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class MilitaryHub : MonoBehaviour
{
    private sealed class Station
    {
        public string Name;
        public string Page;
        public Vector3 Terminal;
    }

    private readonly List<Station> stations = new List<Station>();
    private GameObject hubRoot;
    private PlayerVitals player;
    private GameMenu menu;
    private Station nearest;

    private void Start()
    {
        player = FindAnyObjectByType<PlayerVitals>();
        menu = player != null ? player.GetComponent<GameMenu>() : null;
        BuildHub();
    }

    private void Update()
    {
        GameModeManager manager = GetComponent<GameModeManager>();
        bool available = manager == null || !manager.MatchRunning;
        if (hubRoot != null && hubRoot.activeSelf != available) hubRoot.SetActive(available);
        nearest = null;
        if (!available || player == null || menu == null || menu.IsMenuOpen) return;
        float best = 3.2f;
        foreach (Station station in stations)
        {
            float distance = Vector3.Distance(player.transform.position, station.Terminal);
            if (distance < best) { best = distance; nearest = station; }
        }
        if (nearest != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && nearest.Page != "RANGE")
            menu.OpenHubPage(nearest.Page);
    }

    private void OnGUI()
    {
        if (nearest == null || menu == null || menu.IsMenuOpen) return;
        GUIStyle prompt = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
        prompt.normal.textColor = new Color(0.4f, 0.9f, 1f);
        string text = nearest.Page == "RANGE" ? "SHOOTING RANGE — TEST YOUR CURRENT LOADOUT" : $"PRESS E — {nearest.Name}";
        GUI.color = new Color(0.01f, 0.025f, 0.04f, 0.9f);
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - 245f, Screen.height - 155f, 490f, 48f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(Screen.width * 0.5f - 235f, Screen.height - 149f, 470f, 36f), text, prompt);
    }

    private void BuildHub()
    {
        hubRoot = new GameObject("Walkable Military Hub");
        Material[] themes =
        {
            MakeMaterial(new Color(0.12f, 0.32f, 0.45f)), MakeMaterial(new Color(0.42f, 0.22f, 0.08f)),
            MakeMaterial(new Color(0.1f, 0.38f, 0.25f)), MakeMaterial(new Color(0.28f, 0.18f, 0.42f)),
            MakeMaterial(new Color(0.42f, 0.35f, 0.08f)), MakeMaterial(new Color(0.18f, 0.3f, 0.42f)),
            MakeMaterial(new Color(0.4f, 0.12f, 0.08f))
        };
        CreateHut("OPERATIONS", "PLAY", new Vector3(-26f, 0f, -22f), themes[0]);
        CreateHut("SUPPLY SHOP", "SHOP", new Vector3(0f, 0f, -28f), themes[1]);
        CreateHut("ARMORY INVENTORY", "INVENTORY", new Vector3(26f, 0f, -22f), themes[2]);
        CreateHut("LOADOUT QUARTERS", "LOADOUT", new Vector3(-26f, 0f, 22f), themes[3]);
        CreateHut("MISSION BOARD", "QUESTS", new Vector3(0f, 0f, 28f), themes[4]);
        CreateHut("COMMS & SETTINGS", "SETTINGS", new Vector3(26f, 0f, 22f), themes[5]);
        CreateHut("SHOOTING RANGE", "RANGE", new Vector3(-55f, 0f, 52f), themes[6]);
        CreateRangeTargets();
    }

    private void CreateHut(string label, string page, Vector3 center, Material theme)
    {
        Transform hut = new GameObject(label + " Hut").transform;
        hut.SetParent(hubRoot.transform);
        hut.position = center;
        Vector3 facing = -center.normalized;
        if (facing.sqrMagnitude < 0.1f) facing = Vector3.forward;
        hut.rotation = Quaternion.LookRotation(facing);
        CreateBlock(hut, "Floor", new Vector3(0f, 0.12f, 0f), new Vector3(9f, 0.24f, 8f), theme);
        CreateBlock(hut, "Back Wall", new Vector3(0f, 2.2f, 3.8f), new Vector3(9f, 4.4f, 0.35f), theme);
        CreateBlock(hut, "Left Wall", new Vector3(-4.35f, 2.2f, 0f), new Vector3(0.35f, 4.4f, 8f), theme);
        CreateBlock(hut, "Right Wall", new Vector3(4.35f, 2.2f, 0f), new Vector3(0.35f, 4.4f, 8f), theme);
        CreateBlock(hut, "Roof", new Vector3(0f, 4.5f, 0f), new Vector3(9.4f, 0.35f, 8.4f), theme);
        CreateBlock(hut, "Terminal Table", new Vector3(0f, 0.8f, -1.1f), new Vector3(4.2f, 1.5f, 1.3f), MakeMaterial(Color.Lerp(theme.color, Color.white, 0.2f)));
        CreateBlock(hut, label + " Sign", new Vector3(0f, 3.25f, -3.95f), new Vector3(5.8f, 0.8f, 0.16f), MakeMaterial(new Color(0.08f, 0.75f, 1f)));
        stations.Add(new Station { Name = label, Page = page, Terminal = hut.TransformPoint(new Vector3(0f, 0f, -2.2f)) });
    }

    private void CreateRangeTargets()
    {
        for (int i = 0; i < 5; i++)
        {
            GameObject target = new GameObject("Hub Range Target");
            target.transform.SetParent(hubRoot.transform);
            target.transform.position = new Vector3(-63f + i * 4f, 0f, 68f);
            CharacterController controller = target.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.center = Vector3.up * 0.9f;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(target.transform, false);
            body.transform.localPosition = Vector3.up;
            body.GetComponent<Renderer>().material = MakeMaterial(new Color(0.85f, 0.14f, 0.08f));
            target.AddComponent<TrainingTarget>().Configure(false, 500f, 0f, 0f);
        }
    }

    private static void CreateBlock(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = localPosition;
        block.transform.localScale = scale;
        block.GetComponent<Renderer>().material = material;
    }

    private static Material MakeMaterial(Color color)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        return material;
    }
}
