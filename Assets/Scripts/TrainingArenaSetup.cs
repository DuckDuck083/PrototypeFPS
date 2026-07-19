using UnityEngine;

public sealed class TrainingArenaSetup : MonoBehaviour
{
    private void Start()
    {
        CreateTarget("Training Dummy", new Vector3(0f, 0f, 9f), new Color(0.15f, 0.45f, 0.9f), false);
        CreateTarget("Chasing Enemy", new Vector3(7f, 0f, 7f), new Color(0.85f, 0.12f, 0.1f), true);
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
