using UnityEngine;

public sealed class TrainingArenaSetup : MonoBehaviour
{
    private void Start()
    {
        ImproveSceneLighting();
        BuildArena();
        CreateTarget("Training Dummy", new Vector3(0f, 0f, 9f), new Color(0.15f, 0.45f, 0.9f), false, 150f, 0f, 0f);
        GameObject modeDirector = new GameObject("Game Mode Director");
        modeDirector.AddComponent<WaveManager>();
        modeDirector.AddComponent<GameModeManager>();
        modeDirector.AddComponent<EconomyManager>();
        modeDirector.AddComponent<GameSettingsManager>();

        CreatePickup(new Vector3(5f, 0.7f, 5f), ArenaPickup.PickupType.Health);
        CreatePickup(new Vector3(-18f, 0.7f, 10f), ArenaPickup.PickupType.Health);
        CreatePickup(new Vector3(22f, 0.7f, -20f), ArenaPickup.PickupType.Health);
        CreatePickup(new Vector3(-6f, 0.7f, -12f), ArenaPickup.PickupType.Ammo);
        CreatePickup(new Vector3(18f, 0.7f, 18f), ArenaPickup.PickupType.Ammo);
        CreatePickup(new Vector3(-25f, 0.7f, -25f), ArenaPickup.PickupType.Ammo);
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

        CreateBlock("Foundation", new Vector3(0f, -1.55f, 0f), new Vector3(170f, 3f, 170f), wall);
        CreateBlock("North Wall", new Vector3(0f, 3f, 83f), new Vector3(168f, 6f, 2f), wall);
        CreateBlock("South Wall", new Vector3(0f, 3f, -83f), new Vector3(168f, 6f, 2f), wall);
        CreateBlock("East Wall", new Vector3(83f, 3f, 0f), new Vector3(2f, 6f, 168f), wall);
        CreateBlock("West Wall", new Vector3(-83f, 3f, 0f), new Vector3(2f, 6f, 168f), wall);

        // Repeated lane markings break up the giant flat floor and make routes
        // readable without adding collision or navigation clutter.
        Material lane = CreateArenaMaterial(new Color(0.72f, 0.55f, 0.12f), 0.05f, 0.2f);
        for (int z = -68; z <= 68; z += 8)
            CreateDecoration("Center Lane " + z, new Vector3(0f, 0.025f, z), new Vector3(0.18f, 0.03f, 3.8f), lane);
        for (int z = -64; z <= 64; z += 16)
        {
            CreateDecoration("West Route " + z, new Vector3(-26f, 0.022f, z), new Vector3(0.12f, 0.025f, 7f), accent);
            CreateDecoration("East Route " + z, new Vector3(26f, 0.022f, z), new Vector3(0.12f, 0.025f, 7f), accent);
        }

        Vector3[] coverPositions =
        {
            new Vector3(13f, 0.65f, 16f), new Vector3(-15f, 0.65f, 18f),
            new Vector3(17f, 0.65f, -13f), new Vector3(-18f, 0.65f, -16f)
        };
        for (int i = 0; i < coverPositions.Length; i++)
            CreateBlock($"Jumpable Cover {i + 1}", coverPositions[i], new Vector3(4f, 1.3f, 1.1f), i % 2 == 0 ? cover : accent);

        CreateBlock("Long Range Platform", new Vector3(0f, 0.6f, 31f), new Vector3(14f, 1.2f, 6f), accent);
        CreateBlock("West Tower", new Vector3(-30f, 2f, 22f), new Vector3(7f, 4f, 7f), wall);
        CreateBlock("East Tower", new Vector3(30f, 2f, -22f), new Vector3(7f, 4f, 7f), wall);
        CreateRamp("West Ramp", new Vector3(-25f, 1f, 22f), new Vector3(7f, 0.7f, 4f), -18f, cover);
        CreateRamp("East Ramp", new Vector3(25f, 1f, -22f), new Vector3(7f, 0.7f, 4f), 18f, cover);
        BuildMilitaryBase(wall, cover, accent);
    }

    private static void BuildMilitaryBase(Material wall, Material cover, Material accent)
    {
        Material sandbag = CreateArenaMaterial(new Color(0.38f, 0.34f, 0.22f), 0.02f, 0.18f);
        Material army = CreateArenaMaterial(new Color(0.12f, 0.23f, 0.13f), 0.18f, 0.25f);
        Material warning = CreateArenaMaterial(new Color(0.82f, 0.52f, 0.04f), 0.1f, 0.32f);

        CreateBlock("Command Center", new Vector3(0f, 2.5f, -43f), new Vector3(20f, 5f, 11f), wall);
        CreateBlock("Command Roof", new Vector3(0f, 5.25f, -43f), new Vector3(21f, 0.5f, 12f), accent);
        CreateBlock("Command Entrance Left", new Vector3(-4.5f, 1.5f, -37.3f), new Vector3(8f, 3f, 0.6f), cover);
        CreateBlock("Command Entrance Right", new Vector3(4.5f, 1.5f, -37.3f), new Vector3(8f, 3f, 0.6f), cover);

        // Shorter buildings preserve the base silhouette while opening broad lanes
        // around both sides for groups pursuing the player.
        CreateBlock("West Barracks", new Vector3(-42f, 1.8f, -14f), new Vector3(10f, 3.6f, 14f), wall);
        CreateBlock("East Barracks", new Vector3(42f, 1.8f, 14f), new Vector3(10f, 3.6f, 14f), wall);

        for (int side = -1; side <= 1; side += 2)
        for (int i = 0; i < 4; i++)
        {
            CreateBlock($"Perimeter Post {side} {i}", new Vector3(side * 51f, 1.6f, -30f + i * 20f), new Vector3(0.35f, 3.2f, 0.35f), cover);
        }

        for (int i = 0; i < 6; i++)
        {
            float x = -20f + i * 8f;
            CreateBlock($"Sandbag North {i}", new Vector3(x, 0.45f, 35f), new Vector3(3.4f, 0.65f, 0.8f), sandbag);
            if (i < 7) CreateBlock($"Sandbag Checkpoint {i}", new Vector3(-12f + i * 4f, 0.45f, -29f), new Vector3(3.4f, 0.65f, 0.8f), sandbag);
        }

        Vector3[] cratePositions =
        {
            new Vector3(-29f, 0.6f, 7f), new Vector3(29f, 0.6f, -7f),
            new Vector3(-10f, 0.6f, 27f), new Vector3(12f, 0.6f, -25f)
        };
        for (int i = 0; i < cratePositions.Length; i++)
        {
            CreateBlock($"Supply Crate {i}", cratePositions[i], new Vector3(1.4f, 1.2f, 1.4f), i % 2 == 0 ? army : warning);
        }

        // Leave a wide lane through the checkpoint so groups can pass without
        // their CharacterControllers forming a traffic jam in the old narrow gap.
        CreateBlock("Checkpoint Barrier Left", new Vector3(-11f, 0.75f, 47f), new Vector3(10f, 1.2f, 0.5f), warning);
        CreateBlock("Checkpoint Barrier Right", new Vector3(11f, 0.75f, 47f), new Vector3(10f, 1.2f, 0.5f), warning);
        CreateBlock("Armored Truck Body", new Vector3(22f, 1.25f, 34f), new Vector3(6f, 2.1f, 3.2f), army);
        CreateBlock("Armored Truck Cab", new Vector3(24.7f, 1.65f, 34f), new Vector3(2.2f, 2.8f, 3f), army);
        for (int i = -1; i <= 1; i += 2)
        {
            CreateBlock($"Truck Wheel Front {i}", new Vector3(24f, 0.55f, 34f + i * 1.7f), new Vector3(1.1f, 1.1f, 0.45f), cover);
            CreateBlock($"Truck Wheel Rear {i}", new Vector3(20f, 0.55f, 34f + i * 1.7f), new Vector3(1.1f, 1.1f, 0.45f), cover);
        }

        GameObject helipad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        helipad.name = "Helipad";
        helipad.transform.position = new Vector3(-29f, 0.12f, 35f);
        helipad.transform.localScale = new Vector3(7f, 0.12f, 7f);
        helipad.GetComponent<Renderer>().material = cover;
        CreateBlock("Helipad H Vertical", new Vector3(-29f, 0.3f, 35f), new Vector3(1f, 0.08f, 8f), warning);
        CreateBlock("Helipad H Cross", new Vector3(-29f, 0.31f, 35f), new Vector3(6f, 0.08f, 1f), warning);

        CreateBlock("North Hangar", new Vector3(48f, 3.5f, 61f), new Vector3(28f, 7f, 22f), army);
        CreateBlock("North Hangar Door Left", new Vector3(41f, 2.3f, 49.7f), new Vector3(12f, 4.6f, 0.5f), cover);
        CreateBlock("North Hangar Door Right", new Vector3(55f, 2.3f, 49.7f), new Vector3(12f, 4.6f, 0.5f), cover);
        CreateBlock("Fuel Depot Wall North", new Vector3(-52f, 1.2f, -61f), new Vector3(31f, 2.4f, 0.7f), warning);
        CreateBlock("Fuel Depot Wall South", new Vector3(-52f, 1.2f, -76f), new Vector3(31f, 2.4f, 0.7f), warning);
        CreateBlock("Fuel Depot Wall West", new Vector3(-67f, 1.2f, -68.5f), new Vector3(0.7f, 2.4f, 16f), warning);
        for (int i = 0; i < 6; i++)
        {
            GameObject fuelTank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fuelTank.name = $"Fuel Tank {i}";
            fuelTank.transform.position = new Vector3(-62f + (i % 3) * 10f, 1.6f, -72f + (i / 3) * 7f);
            fuelTank.transform.localScale = new Vector3(2.4f, 1.6f, 2.4f);
            fuelTank.GetComponent<Renderer>().material = i % 2 == 0 ? army : cover;
        }
        CreateBlock("Radio Tower Base", new Vector3(64f, 1f, -58f), new Vector3(8f, 2f, 8f), cover);
        CreateBlock("Radio Mast", new Vector3(64f, 10f, -58f), new Vector3(0.7f, 19f, 0.7f), warning);
        CreateBlock("Radio Crossbar", new Vector3(64f, 15f, -58f), new Vector3(8f, 0.5f, 0.5f), warning);
        CreateBlock("East Forward Bunker", new Vector3(67f, 2f, 18f), new Vector3(17f, 4f, 14f), wall);
        CreateBlock("West Forward Bunker", new Vector3(-67f, 2f, 18f), new Vector3(17f, 4f, 14f), wall);
        for (int i = 0; i < 12; i++)
        {
            float z = -48f + i * 8f;
            CreateBlock($"Outer Cover East {i}", new Vector3(57f + (i % 2) * 5f, 0.8f, z), new Vector3(3.5f, 1.6f, 1.2f), i % 3 == 0 ? sandbag : cover);
            CreateBlock($"Outer Cover West {i}", new Vector3(-57f - (i % 2) * 5f, 0.8f, -z), new Vector3(3.5f, 1.6f, 1.2f), i % 3 == 0 ? sandbag : cover);
        }
    }

    private static void CreateRamp(string name, Vector3 position, Vector3 scale, float zRotation, Material material)
    {
        GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = name;
        ramp.transform.position = position;
        ramp.transform.localScale = scale;
        ramp.transform.rotation = Quaternion.Euler(0f, 0f, zRotation);
        ramp.GetComponent<Renderer>().material = material;
    }

    private static void CreatePickup(Vector3 position, ArenaPickup.PickupType type)
    {
        GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pickup.transform.position = position;
        pickup.transform.localScale = type == ArenaPickup.PickupType.Health ? new Vector3(0.8f, 0.5f, 0.8f) : new Vector3(0.65f, 0.65f, 0.65f);
        pickup.GetComponent<Renderer>().material = CreateArenaMaterial(type == ArenaPickup.PickupType.Health ? new Color(0.1f, 0.9f, 0.25f) : new Color(1f, 0.65f, 0.08f), 0.2f, 0.65f);
        pickup.GetComponent<Collider>().isTrigger = true;
        Rigidbody body = pickup.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        ArenaPickup arenaPickup = pickup.AddComponent<ArenaPickup>();
        arenaPickup.Configure(type);
    }

    private static void CreateBlock(string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.position = position;
        block.transform.localScale = scale;
        block.GetComponent<Renderer>().material = material;
    }

    private static void CreateDecoration(string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject decoration = GameObject.CreatePrimitive(PrimitiveType.Cube);
        decoration.name = name;
        decoration.transform.position = position;
        decoration.transform.localScale = scale;
        decoration.GetComponent<Renderer>().material = material;
        Destroy(decoration.GetComponent<Collider>());
    }

    private static Material CreateArenaMaterial(Color color, float metallic, float smoothness)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    private static void CreateTarget(string targetName, Vector3 position, Color color, bool followsPlayer, float health, float speed, float damage)
    {
        GameObject root = new GameObject(targetName);
        root.transform.position = position;

        CharacterController controller = root.AddComponent<CharacterController>();
        controller.height = 1.55f;
        controller.radius = 0.45f;
        controller.center = Vector3.up * 0.775f;

        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;

        AddPart(root.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), new Vector3(0.75f, 0.9f, 0.75f), material, true);
        AddPart(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.85f, 0f), Vector3.one * 0.55f, material, true);

        if (!followsPlayer)
        {
            AddPart(root.transform, "Stand", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), new Vector3(1.4f, 0.16f, 1.4f), material);
        }

        TrainingTarget target = root.AddComponent<TrainingTarget>();
        target.Configure(followsPlayer, health, speed, damage);
    }

    private static void AddPart(Transform parent, string partName, PrimitiveType shape, Vector3 localPosition, Vector3 scale, Material material, bool keepCollider = false)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().material = material;
        if (!keepCollider)
            Destroy(part.GetComponent<Collider>());
    }
}
