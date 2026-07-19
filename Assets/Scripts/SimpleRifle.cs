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
    private AudioSource gunshotAudio;
    private AudioClip gunshotClip;
    private Material tracerMaterial;
    private Material bulletHoleMaterial;
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
        CreateShotEffects();
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

        if (!isReloading)
        {
            rifleModel.localPosition = Vector3.Lerp(
                rifleModel.localPosition,
                rifleRestPosition,
                18f * Time.deltaTime);
            rifleModel.localRotation = Quaternion.Slerp(
                rifleModel.localRotation,
                Quaternion.identity,
                18f * Time.deltaTime);
        }
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
        gunshotAudio.PlayOneShot(gunshotClip, 0.7f);

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        Vector3 tracerEnd = ray.GetPoint(range);
        if (Physics.Raycast(ray, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
        {
            tracerEnd = hit.point;

            if (hit.rigidbody != null)
                hit.rigidbody.AddForceAtPosition(ray.direction * hitForce, hit.point, ForceMode.Impulse);

            CreateBulletHole(hit.point, hit.normal, hit.transform);
        }

        CreateTracer(tracerEnd);
        CreateMuzzleFlash();

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

        float elapsed = 0f;
        while (elapsed < reloadTime)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / reloadTime);
            float lowerAmount = Mathf.Sin(progress * Mathf.PI);
            rifleModel.localPosition = rifleRestPosition + new Vector3(0f, -0.28f * lowerAmount, -0.08f * lowerAmount);
            rifleModel.localRotation = Quaternion.Euler(18f * lowerAmount, 0f, -28f * lowerAmount);
            yield return null;
        }

        int roundsNeeded = magazineSize - ammoInMagazine;
        int roundsLoaded = Mathf.Min(roundsNeeded, reserveAmmo);
        ammoInMagazine += roundsLoaded;
        reserveAmmo -= roundsLoaded;
        rifleModel.localPosition = rifleRestPosition;
        rifleModel.localRotation = Quaternion.identity;
        isReloading = false;
    }

    private void CreateShotEffects()
    {
        gunshotAudio = gameObject.AddComponent<AudioSource>();
        gunshotAudio.playOnAwake = false;
        gunshotAudio.spatialBlend = 0f;
        gunshotClip = CreateGunshotClip();

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        tracerMaterial = new Material(unlitShader);
        tracerMaterial.color = new Color(1f, 0.75f, 0.25f);
        bulletHoleMaterial = new Material(unlitShader);
        bulletHoleMaterial.color = new Color(0.025f, 0.02f, 0.015f);
    }

    private static AudioClip CreateGunshotClip()
    {
        const int sampleRate = 44100;
        const int sampleCount = 4410;
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float fade = Mathf.Exp(-time * 45f);
            float noise = Random.Range(-1f, 1f) * fade;
            float thump = Mathf.Sin(2f * Mathf.PI * 95f * time) * Mathf.Exp(-time * 30f);
            samples[i] = Mathf.Clamp((noise * 0.75f) + (thump * 0.5f), -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Procedural Rifle Shot", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
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

    private void CreateTracer(Vector3 endPoint)
    {
        GameObject tracer = new GameObject("Bullet Tracer");
        LineRenderer line = tracer.AddComponent<LineRenderer>();
        line.material = tracerMaterial;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = 0.012f;
        line.endWidth = 0.002f;
        line.startColor = new Color(1f, 0.8f, 0.35f, 0.8f);
        line.endColor = new Color(1f, 0.5f, 0.1f, 0f);
        line.SetPosition(0, rifleModel.TransformPoint(new Vector3(0f, 0.02f, 0.86f)));
        line.SetPosition(1, endPoint);
        Destroy(tracer, 0.06f);
    }

    private void CreateMuzzleFlash()
    {
        GameObject flash = new GameObject("Muzzle Flash");
        flash.transform.position = rifleModel.TransformPoint(new Vector3(0f, 0.02f, 0.86f));
        Light flashLight = flash.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = new Color(1f, 0.55f, 0.15f);
        flashLight.intensity = 3f;
        flashLight.range = 3f;
        Destroy(flash, 0.04f);
    }

    private void CreateBulletHole(Vector3 point, Vector3 normal, Transform hitTransform)
    {
        GameObject hole = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hole.name = "Bullet Hole";
        hole.transform.position = point + normal * 0.006f;
        hole.transform.rotation = Quaternion.LookRotation(normal);
        hole.transform.localScale = new Vector3(0.075f, 0.075f, 0.006f);
        hole.GetComponent<Renderer>().material = bulletHoleMaterial;
        Destroy(hole.GetComponent<Collider>());

        if (hitTransform != null)
            hole.transform.SetParent(hitTransform, true);

        Destroy(hole, 20f);
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
