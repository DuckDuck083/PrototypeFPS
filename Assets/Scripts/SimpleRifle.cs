using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SimpleRifle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Camera playerCamera;

    [Header("Rifle")]
    [SerializeField, Min(1)] private int magazineSize = 30;
    [SerializeField, Min(0)] private int reserveAmmo = 90;
    [SerializeField, Min(0.01f)] private float secondsBetweenShots = 0.12f;
    [SerializeField, Min(0.1f)] private float reloadTime = 1.4f;
    [SerializeField, Min(1f)] private float range = 100f;
    [SerializeField, Min(0f)] private float hitForce = 8f;

    private InputAction attackAction;
    private InputAction reloadAction;
    private Transform rifleModel;
    private Vector3 rifleRestPosition;
    private int ammoInMagazine;
    private float nextShotTime;
    private bool isReloading;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        attackAction = inputActions.FindAction("Player/Attack", true);
        reloadAction = new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");
        ammoInMagazine = magazineSize;
        CreateSimpleRifleModel();
    }

    private void OnEnable()
    {
        reloadAction.Enable();
    }

    private void OnDisable()
    {
        reloadAction.Disable();
        StopAllCoroutines();
        isReloading = false;
    }

    private void OnDestroy()
    {
        reloadAction.Dispose();
    }

    private void Update()
    {
        if (reloadAction.WasPressedThisFrame())
            TryReload();

        if (!isReloading && attackAction.IsPressed() && Time.time >= nextShotTime)
            Shoot();

        rifleModel.localPosition = Vector3.Lerp(
            rifleModel.localPosition,
            rifleRestPosition,
            18f * Time.deltaTime);
    }

    private void Shoot()
    {
        if (ammoInMagazine <= 0)
        {
            TryReload();
            return;
        }

        ammoInMagazine--;
        nextShotTime = Time.time + secondsBetweenShots;
        rifleModel.localPosition = rifleRestPosition + Vector3.back * 0.06f;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.rigidbody != null)
                hit.rigidbody.AddForceAtPosition(ray.direction * hitForce, hit.point, ForceMode.Impulse);

            CreateImpactMarker(hit.point, hit.normal);
        }

        if (ammoInMagazine == 0)
            TryReload();
    }

    private void TryReload()
    {
        if (!isReloading && ammoInMagazine < magazineSize && reserveAmmo > 0)
            StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadTime);

        int roundsNeeded = magazineSize - ammoInMagazine;
        int roundsLoaded = Mathf.Min(roundsNeeded, reserveAmmo);
        ammoInMagazine += roundsLoaded;
        reserveAmmo -= roundsLoaded;
        isReloading = false;
    }

    private void CreateSimpleRifleModel()
    {
        GameObject root = new GameObject("Simple Rifle");
        rifleModel = root.transform;
        rifleModel.SetParent(playerCamera.transform, false);
        rifleRestPosition = new Vector3(0.32f, -0.3f, 0.55f);
        rifleModel.localPosition = rifleRestPosition;

        Material darkMaterial = CreateMaterial(new Color(0.08f, 0.09f, 0.1f));
        Material metalMaterial = CreateMaterial(new Color(0.2f, 0.22f, 0.24f));
        AddPart("Body", new Vector3(0f, 0f, 0.2f), new Vector3(0.16f, 0.16f, 0.55f), metalMaterial);
        AddPart("Barrel", new Vector3(0f, 0.02f, 0.62f), new Vector3(0.06f, 0.06f, 0.45f), darkMaterial);
        AddPart("Stock", new Vector3(0f, 0f, -0.17f), new Vector3(0.14f, 0.17f, 0.22f), darkMaterial);
        AddPart("Grip", new Vector3(0f, -0.16f, 0.08f), new Vector3(0.09f, 0.25f, 0.11f), darkMaterial, 12f);
        AddPart("Magazine", new Vector3(0f, -0.16f, 0.27f), new Vector3(0.11f, 0.24f, 0.14f), metalMaterial, -8f);
        AddPart("Sight", new Vector3(0f, 0.12f, 0.32f), new Vector3(0.07f, 0.07f, 0.12f), darkMaterial);
    }

    private void AddPart(string partName, Vector3 position, Vector3 scale, Material material, float xRotation = 0f)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(rifleModel, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().material = material;
        Destroy(part.GetComponent<Collider>());
    }

    private static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material material = new Material(shader);
        material.color = color;
        return material;
    }

    private static void CreateImpactMarker(Vector3 point, Vector3 normal)
    {
        GameObject impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        impact.name = "Bullet Impact";
        impact.transform.position = point + normal * 0.01f;
        impact.transform.localScale = Vector3.one * 0.05f;
        Destroy(impact.GetComponent<Collider>());
        Destroy(impact, 0.15f);
    }

    private void OnGUI()
    {
        GUIStyle centered = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            normal = { textColor = Color.white }
        };

        GUI.Label(new Rect(Screen.width * 0.5f - 15f, Screen.height * 0.5f - 15f, 30f, 30f), "+", centered);

        string ammoText = isReloading ? "RELOADING..." : $"{ammoInMagazine} / {reserveAmmo}";
        GUI.Label(new Rect(Screen.width - 190f, Screen.height - 70f, 160f, 40f), ammoText, centered);
    }
}
