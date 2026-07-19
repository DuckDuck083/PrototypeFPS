using UnityEngine;

public sealed class TrainingArenaSetup : MonoBehaviour
{
    private void Start()
    {
        ImproveSceneLighting();
        BuildArena();
        CreateTarget("Training Dummy", new Vector3(0f, 0f, 9f), new Color(0.15f, 0.45f, 0.9f), false);
        CreateTarget("Enemy 1", new Vector3(10f, 0f, 10f), new Color(0.85f, 0.12f, 0.1f), true);
        CreateTarget("Enemy 2", new Vector3(-14f, 0f, 15f), new Color(0.9f, 0.22f, 0.08f), true);
        CreateTarget("Enemy 3", new Vector3(19f, 0f, -12f), new Color(0.72f, 0.08f, 0.14f), true);
        CreateTarget("Enemy 4", new Vector3(-22f, 0f, -18f), new Color(0.82f, 0.13f, 0.3f), true);
        CreateTarget("Enemy 5", new Vector3(2f, 0f, 28f), new Color(0.95f, 0.3f, 0.06f), true);
    }

    private static void ImproveSceneLighting()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.12f, 0.17f, 0.22f);
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.25f, 0.34f, 0.45f);
        RenderSettings.ambientEquatorColor = new Color(0.12f, 0.16f, 0.2f);
        RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.06f);

        Light sun = FindAnyObjectByType<Light>();
        if (sun != null)
        {
            sun.color = new Color(1f, 0.84f, 0.68f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
        }

        GameObject floor = GameObject.Find("Floor");
        if (floor != null)
            floor.GetComponent<Renderer>().material = CreateArenaMaterial(new Color(0.11f, 0.13f, 0.14f), 0.05f, 0.25f);
    }

    private static void BuildArena()
    {
        Material wall = CreateArenaMaterial(new Color(0.18f, 0.21f, 0.24f), 0.15f, 0.35f);
        Material cover = CreateArenaMaterial(new Color(0.24f, 0.27f, 0.29f), 0.3f, 0.45f);
        Material accent = CreateArenaMaterial(new Color(0.12f, 0.32f, 0.42f), 0.2f, 0.55f);

        CreateBlock("North Wall", new Vector3(0f, 3f, 39f), new Vector3(80f, 6f, 1f), wall);
        CreateBlock("South Wall", new Vector3(0f, 3f, -39f), new Vector3(80f, 6f, 1f), wall);
        CreateBlock("East Wall", new Vector3(39f, 3f, 0f), new Vector3(1f, 6f, 80f), wall);
        CreateBlock("West Wall", new Vector3(-39f, 3f, 0f), new Vector3(1f, 6f, 80f), wall);

        Vector3[] coverPositions =
        {
            new Vector3(7f, 1f, 14f), new Vector3(-9f, 1f, 18f), new Vector3(16f, 1f, 2f),
            new Vector3(-17f, 1f, -4f), new Vector3(8f, 1f, -17f), new Vector3(-6f, 1f, -24f)
        };
        for (int i = 0; i < coverPositions.Length; i++)
            CreateBlock($"Cover {i + 1}", coverPositions[i], new Vector3(4f, 2f, 1.3f), i % 2 == 0 ? cover : accent);

        CreateBlock("Center Platform", new Vector3(0f, 0.35f, 0f), new Vector3(10f, 0.7f, 10f), cover);
        CreateBlock("Long Range Platform", new Vector3(0f, 0.6f, 31f), new Vector3(14f, 1.2f, 6f), accent);
    }

    private static void CreateBlock(string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.position = position;
        block.transform.localScale = scale;
        block.GetComponent<Renderer>().material = material;
    }

    private static Material CreateArenaMaterial(Color color, float metallic, float smoothness)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    private static void CreateTarget(string targetName, Vector3 position, Color color, bool followsPlayer)
    {
        GameObject root = new GameObject(targetName);
        root.transform.position = position;

        CharacterController controller = root.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.45f;
        controller.center = Vector3.up;

        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;

        AddPart(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), new Vector3(0.75f, 0.9f, 0.75f), material);
        AddPart(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.85f, 0f), Vector3.one * 0.55f, material);

        if (!followsPlayer)
        {
            AddPart(root.transform, "Stand", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), new Vector3(1.4f, 0.16f, 1.4f), material);
        }

        TrainingTarget target = root.AddComponent<TrainingTarget>();
        target.Configure(followsPlayer);
    }

    private static void AddPart(Transform parent, string partName, PrimitiveType shape, Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().material = material;
        Destroy(part.GetComponent<Collider>());
    }
}
