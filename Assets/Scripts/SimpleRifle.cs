using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SimpleRifle : MonoBehaviour
{
    private enum WeaponType { Rifle, Handgun, Melee, Sniper }

    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Camera playerCamera;

    [Header("Rifle")]
    [SerializeField, Min(1)] private int rifleMagazineSize = 30;
    [SerializeField, Min(0)] private int rifleReserveAmmo = 90;

    [Header("Handgun")]
    [SerializeField, Min(1)] private int handgunMagazineSize = 12;
    [SerializeField, Min(0)] private int handgunReserveAmmo = 48;

    [Header("Sniper")]
    [SerializeField, Min(1)] private int sniperMagazineSize = 5;
    [SerializeField, Min(0)] private int sniperReserveAmmo = 20;

    [Header("Shared")]
    [SerializeField, Min(0.1f)] private float reloadTime = 1.4f;
    [SerializeField, Min(1f)] private float range = 100f;
    [SerializeField, Min(0f)] private float hitForce = 8f;

    private InputAction attackAction;
    private InputAction reloadAction;
    private InputAction aimAction;
    private InputAction rifleSelectAction;
    private InputAction handgunSelectAction;
    private InputAction meleeSelectAction;
    private InputAction sniperSelectAction;
    private Transform rifleModel;
    private Transform handgunModel;
    private Transform meleeModel;
    private Transform sniperModel;
    private Transform currentModel;
    private Vector3 currentRestPosition;
    private AudioSource gunshotAudio;
    private AudioClip gunshotClip;
    private Material tracerMaterial;
    private Material bulletHoleMaterial;
    private WeaponType currentWeapon;
    private int rifleAmmo;
    private int handgunAmmo;
    private int sniperAmmo;
    private float nextShotTime;
    private float normalFieldOfView;
    private float sniperCharge;
    private const float MaximumSniperChargeTime = 1.5f;
    private bool isReloading;
    private bool isChargingSniper;

    private bool IsAiming => currentWeapon != WeaponType.Melee && aimAction.IsPressed() && !isReloading;
    private int CurrentAmmo => currentWeapon == WeaponType.Rifle ? rifleAmmo : currentWeapon == WeaponType.Handgun ? handgunAmmo : sniperAmmo;
    private int CurrentReserve => currentWeapon == WeaponType.Rifle ? rifleReserveAmmo : currentWeapon == WeaponType.Handgun ? handgunReserveAmmo : sniperReserveAmmo;
    private int CurrentMagazineSize => currentWeapon == WeaponType.Rifle ? rifleMagazineSize : currentWeapon == WeaponType.Handgun ? handgunMagazineSize : sniperMagazineSize;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        attackAction = inputActions.FindAction("Player/Attack", true);
        reloadAction = new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");
        aimAction = new InputAction("Aim", InputActionType.Button, "<Mouse>/rightButton");
        rifleSelectAction = new InputAction("Select Rifle", InputActionType.Button, "<Keyboard>/1");
        handgunSelectAction = new InputAction("Select Handgun", InputActionType.Button, "<Keyboard>/2");
        meleeSelectAction = new InputAction("Select Melee", InputActionType.Button, "<Keyboard>/3");
        sniperSelectAction = new InputAction("Select Sniper", InputActionType.Button, "<Keyboard>/4");

        rifleAmmo = rifleMagazineSize;
        handgunAmmo = handgunMagazineSize;
        sniperAmmo = sniperMagazineSize;
        normalFieldOfView = playerCamera.fieldOfView;
        CreateWeaponModels();
        CreateShotEffects();
        SelectWeapon(WeaponType.Rifle);
    }

    private void OnEnable()
    {
        reloadAction.Enable();
        aimAction.Enable();
        rifleSelectAction.Enable();
        handgunSelectAction.Enable();
        meleeSelectAction.Enable();
        sniperSelectAction.Enable();
    }

    private void OnDisable()
    {
        reloadAction.Disable();
        aimAction.Disable();
        rifleSelectAction.Disable();
        handgunSelectAction.Disable();
        meleeSelectAction.Disable();
        sniperSelectAction.Disable();
        StopAllCoroutines();
        isReloading = false;
        if (playerCamera != null)
            playerCamera.fieldOfView = normalFieldOfView;
    }

    private void OnDestroy()
    {
        reloadAction.Dispose();
        aimAction.Dispose();
        rifleSelectAction.Dispose();
        handgunSelectAction.Dispose();
        meleeSelectAction.Dispose();
        sniperSelectAction.Dispose();
    }

    private void Update()
    {
        if (!isReloading)
        {
            if (rifleSelectAction.WasPressedThisFrame()) SelectWeapon(WeaponType.Rifle);
            if (handgunSelectAction.WasPressedThisFrame()) SelectWeapon(WeaponType.Handgun);
            if (meleeSelectAction.WasPressedThisFrame()) SelectWeapon(WeaponType.Melee);
            if (sniperSelectAction.WasPressedThisFrame()) SelectWeapon(WeaponType.Sniper);
        }

        if (reloadAction.WasPressedThisFrame())
            TryReload();

        if (!isReloading && currentWeapon == WeaponType.Sniper)
            UpdateSniperCharge();
        else if (!isReloading && attackAction.IsPressed() && Time.time >= nextShotTime)
            Shoot();

        UpdateAimingVisuals();
    }

    private void UpdateSniperCharge()
    {
        if (CurrentAmmo <= 0)
        {
            if (attackAction.WasPressedThisFrame()) TryReload();
            return;
        }

        if (attackAction.WasPressedThisFrame() && Time.time >= nextShotTime)
        {
            isChargingSniper = true;
            sniperCharge = 0f;
        }

        if (isChargingSniper && attackAction.IsPressed())
            sniperCharge = Mathf.Min(MaximumSniperChargeTime, sniperCharge + Time.deltaTime);

        if (isChargingSniper && attackAction.WasReleasedThisFrame())
        {
            float chargeRatio = sniperCharge / MaximumSniperChargeTime;
            isChargingSniper = false;
            Shoot(chargeRatio);
            sniperCharge = 0f;
        }
    }

    private void SelectWeapon(WeaponType weapon)
    {
        isChargingSniper = false;
        sniperCharge = 0f;
        currentWeapon = weapon;
        rifleModel.gameObject.SetActive(weapon == WeaponType.Rifle);
        handgunModel.gameObject.SetActive(weapon == WeaponType.Handgun);
        meleeModel.gameObject.SetActive(weapon == WeaponType.Melee);
        sniperModel.gameObject.SetActive(weapon == WeaponType.Sniper);
        currentModel = weapon == WeaponType.Rifle ? rifleModel : weapon == WeaponType.Handgun ? handgunModel : weapon == WeaponType.Melee ? meleeModel : sniperModel;
        currentRestPosition = weapon == WeaponType.Rifle ? new Vector3(0.32f, -0.3f, 0.55f)
            : weapon == WeaponType.Handgun ? new Vector3(0.3f, -0.27f, 0.5f)
            : weapon == WeaponType.Melee ? new Vector3(0.36f, -0.32f, 0.48f)
            : new Vector3(0.34f, -0.3f, 0.58f);
        currentModel.localPosition = currentRestPosition;
        currentModel.localRotation = Quaternion.identity;
    }

    private void UpdateAimingVisuals()
    {
        if (isReloading)
            return;

        Vector3 aimedPosition = currentWeapon == WeaponType.Sniper ? new Vector3(0f, -0.14f, 0.52f)
            : currentWeapon == WeaponType.Rifle ? new Vector3(0f, -0.13f, 0.48f)
            : new Vector3(0f, -0.12f, 0.43f);
        Vector3 targetPosition = IsAiming ? aimedPosition : currentRestPosition;
        currentModel.localPosition = Vector3.Lerp(currentModel.localPosition, targetPosition, 14f * Time.deltaTime);
        currentModel.localRotation = Quaternion.Slerp(currentModel.localRotation, Quaternion.identity, 18f * Time.deltaTime);
        float aimedFov = currentWeapon == WeaponType.Sniper ? 25f : 48f;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, IsAiming ? aimedFov : normalFieldOfView, 12f * Time.deltaTime);
    }

    private void Shoot(float sniperChargeRatio = 0f)
    {
        if (currentWeapon == WeaponType.Melee)
        {
            SwingMelee();
            return;
        }

        if (CurrentAmmo <= 0)
        {
            TryReload();
            return;
        }

        bool aimedShot = IsAiming;
        float baseDelay = currentWeapon == WeaponType.Rifle ? 0.12f : currentWeapon == WeaponType.Handgun ? 0.28f : 1.1f;
        float damage = currentWeapon == WeaponType.Rifle ? 25f : currentWeapon == WeaponType.Handgun ? 18f : Mathf.Lerp(70f, 160f, sniperChargeRatio);
        if (aimedShot)
        {
            baseDelay *= 0.78f;
            damage *= 1.2f;
        }

        SetCurrentAmmo(CurrentAmmo - 1);
        nextShotTime = Time.time + baseDelay;
        currentModel.localPosition += Vector3.back * 0.055f;
        gunshotAudio.pitch = currentWeapon == WeaponType.Rifle ? Random.Range(0.96f, 1.04f) : currentWeapon == WeaponType.Handgun ? Random.Range(1.15f, 1.22f) : Random.Range(0.72f, 0.78f);
        gunshotAudio.PlayOneShot(gunshotClip, currentWeapon == WeaponType.Sniper ? 1f : currentWeapon == WeaponType.Rifle ? 0.7f : 0.55f);

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        Vector3 tracerEnd = ray.GetPoint(range);
        if (Physics.Raycast(ray, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
        {
            tracerEnd = hit.point;
            if (hit.rigidbody != null)
                hit.rigidbody.AddForceAtPosition(ray.direction * hitForce, hit.point, ForceMode.Impulse);

            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(damage);
            CreateBulletHole(hit.point, hit.normal, hit.transform);
        }

        CreateTracer(tracerEnd);
        CreateMuzzleFlash();
        if (CurrentAmmo == 0)
            TryReload();
    }

    private void SwingMelee()
    {
        nextShotTime = Time.time + 0.65f;
        currentModel.localRotation = Quaternion.Euler(0f, 0f, -55f);
        currentModel.localPosition = currentRestPosition + new Vector3(-0.18f, 0.08f, 0.18f);

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.SphereCast(ray, 0.35f, out RaycastHit hit, 2.4f, ~0, QueryTriggerInteraction.Ignore))
        {
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(45f);
            CreateBulletHole(hit.point, hit.normal, hit.transform);
        }
    }

    private void SetCurrentAmmo(int amount)
    {
        if (currentWeapon == WeaponType.Rifle) rifleAmmo = amount;
        else if (currentWeapon == WeaponType.Handgun) handgunAmmo = amount;
        else sniperAmmo = amount;
    }

    private void SetCurrentReserve(int amount)
    {
        if (currentWeapon == WeaponType.Rifle) rifleReserveAmmo = amount;
        else if (currentWeapon == WeaponType.Handgun) handgunReserveAmmo = amount;
        else sniperReserveAmmo = amount;
    }

    public void AddReserveAmmo(int rifleRounds, int handgunRounds, int sniperRounds)
    {
        rifleReserveAmmo += rifleRounds;
        handgunReserveAmmo += handgunRounds;
        sniperReserveAmmo += sniperRounds;
    }

    private void TryReload()
    {
        if (currentWeapon != WeaponType.Melee && !isReloading && CurrentAmmo < CurrentMagazineSize && CurrentReserve > 0)
            StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        isReloading = true;
        playerCamera.fieldOfView = normalFieldOfView;
        float elapsed = 0f;
        while (elapsed < reloadTime)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / reloadTime);
            float motion = Mathf.Sin(progress * Mathf.PI);
            currentModel.localPosition = currentRestPosition + new Vector3(0f, -0.28f * motion, -0.08f * motion);
            currentModel.localRotation = Quaternion.Euler(18f * motion, 0f, -28f * motion);
            yield return null;
        }

        int loaded = Mathf.Min(CurrentMagazineSize - CurrentAmmo, CurrentReserve);
        SetCurrentAmmo(CurrentAmmo + loaded);
        SetCurrentReserve(CurrentReserve - loaded);
        currentModel.localPosition = currentRestPosition;
        currentModel.localRotation = Quaternion.identity;
        isReloading = false;
    }

    private void CreateWeaponModels()
    {
        Material dark = CreateMaterial(new Color(0.08f, 0.09f, 0.1f));
        Material metal = CreateMaterial(new Color(0.2f, 0.22f, 0.24f));

        rifleModel = CreateModelRoot("Simple Rifle");
        AddPart(rifleModel, "Body", new Vector3(0f, 0f, 0.2f), new Vector3(0.16f, 0.16f, 0.55f), metal);
        AddPart(rifleModel, "Barrel", new Vector3(0f, 0.02f, 0.62f), new Vector3(0.06f, 0.06f, 0.45f), dark);
        AddPart(rifleModel, "Stock", new Vector3(0f, 0f, -0.17f), new Vector3(0.14f, 0.17f, 0.22f), dark);
        AddPart(rifleModel, "Grip", new Vector3(0f, -0.16f, 0.08f), new Vector3(0.09f, 0.25f, 0.11f), dark, 12f);
        AddPart(rifleModel, "Magazine", new Vector3(0f, -0.16f, 0.27f), new Vector3(0.11f, 0.24f, 0.14f), metal, -8f);
        AddPart(rifleModel, "Sight", new Vector3(0f, 0.12f, 0.32f), new Vector3(0.07f, 0.07f, 0.12f), dark);

        handgunModel = CreateModelRoot("Simple Handgun");
        AddPart(handgunModel, "Slide", new Vector3(0f, 0.02f, 0.2f), new Vector3(0.14f, 0.13f, 0.48f), metal);
        AddPart(handgunModel, "Barrel", new Vector3(0f, 0.02f, 0.49f), new Vector3(0.055f, 0.055f, 0.17f), dark);
        AddPart(handgunModel, "Grip", new Vector3(0f, -0.19f, 0.03f), new Vector3(0.13f, 0.32f, 0.15f), dark, 10f);
        AddPart(handgunModel, "Sight", new Vector3(0f, 0.105f, 0.23f), new Vector3(0.045f, 0.045f, 0.08f), dark);

        meleeModel = CreateModelRoot("Training Baton");
        AddPart(meleeModel, "Handle", new Vector3(0f, -0.16f, 0.02f), new Vector3(0.1f, 0.32f, 0.1f), dark, 18f);
        AddPart(meleeModel, "Baton", new Vector3(0f, 0.18f, 0.18f), new Vector3(0.09f, 0.65f, 0.09f), metal, 35f);

        sniperModel = CreateModelRoot("Simple Sniper Rifle");
        AddPart(sniperModel, "Receiver", new Vector3(0f, 0f, 0.23f), new Vector3(0.17f, 0.17f, 0.65f), metal);
        AddPart(sniperModel, "Long Barrel", new Vector3(0f, 0.01f, 0.88f), new Vector3(0.055f, 0.055f, 0.75f), dark);
        AddPart(sniperModel, "Stock", new Vector3(0f, -0.02f, -0.25f), new Vector3(0.17f, 0.2f, 0.34f), dark);
        AddPart(sniperModel, "Scope", new Vector3(0f, 0.15f, 0.3f), new Vector3(0.12f, 0.12f, 0.38f), dark);
        AddPart(sniperModel, "Magazine", new Vector3(0f, -0.17f, 0.27f), new Vector3(0.12f, 0.24f, 0.16f), metal, -6f);
    }

    private Transform CreateModelRoot(string modelName)
    {
        Transform model = new GameObject(modelName).transform;
        model.SetParent(playerCamera.transform, false);
        return model;
    }

    private static void AddPart(Transform model, string name, Vector3 position, Vector3 scale, Material material, float xRotation = 0f)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(model, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().material = material;
        Destroy(part.GetComponent<Collider>());
    }

    private void CreateShotEffects()
    {
        gunshotAudio = gameObject.AddComponent<AudioSource>();
        gunshotAudio.playOnAwake = false;
        gunshotAudio.spatialBlend = 0f;
        gunshotClip = CreateGunshotClip();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        tracerMaterial = new Material(shader) { color = new Color(1f, 0.75f, 0.25f) };
        bulletHoleMaterial = new Material(shader) { color = new Color(0.025f, 0.02f, 0.015f) };
    }

    private static AudioClip CreateGunshotClip()
    {
        const int sampleRate = 44100;
        const int sampleCount = 4410;
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float noise = Random.Range(-1f, 1f) * Mathf.Exp(-time * 45f);
            float thump = Mathf.Sin(2f * Mathf.PI * 95f * time) * Mathf.Exp(-time * 30f);
            samples[i] = Mathf.Clamp(noise * 0.75f + thump * 0.5f, -1f, 1f);
        }
        AudioClip clip = AudioClip.Create("Procedural Gunshot", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static Material CreateMaterial(Color color)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        return material;
    }

    private Vector3 MuzzlePosition => currentModel.TransformPoint(new Vector3(0f, 0.02f, currentWeapon == WeaponType.Sniper ? 1.27f : currentWeapon == WeaponType.Rifle ? 0.86f : 0.59f));

    private void CreateTracer(Vector3 end)
    {
        GameObject tracer = new GameObject("Bullet Tracer");
        LineRenderer line = tracer.AddComponent<LineRenderer>();
        line.material = tracerMaterial;
        line.positionCount = 2;
        line.startWidth = 0.012f;
        line.endWidth = 0.002f;
        line.startColor = new Color(1f, 0.8f, 0.35f, 0.8f);
        line.endColor = new Color(1f, 0.5f, 0.1f, 0f);
        line.SetPosition(0, MuzzlePosition);
        line.SetPosition(1, end);
        Destroy(tracer, 0.06f);
    }

    private void CreateMuzzleFlash()
    {
        GameObject flash = new GameObject("Muzzle Flash");
        flash.transform.position = MuzzlePosition;
        Light light = flash.AddComponent<Light>();
        light.color = new Color(1f, 0.55f, 0.15f);
        light.intensity = 3f;
        light.range = 3f;
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
        if (hitTransform != null) hole.transform.SetParent(hitTransform, true);
        Destroy(hole, 20f);
    }

    private void OnGUI()
    {
        if (currentWeapon == WeaponType.Sniper && IsAiming)
            DrawSniperScope();

        GUIStyle centered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
        centered.normal.textColor = Color.white;
        if (!(currentWeapon == WeaponType.Sniper && IsAiming))
            GUI.Label(new Rect(Screen.width * 0.5f - 15f, Screen.height * 0.5f - 15f, 30f, 30f), "+", centered);
        string weaponName = currentWeapon == WeaponType.Rifle ? "RIFLE [1]" : currentWeapon == WeaponType.Handgun ? "HANDGUN [2]" : currentWeapon == WeaponType.Melee ? "BATON [3]" : "SNIPER [4]";
        string ammoText = currentWeapon == WeaponType.Melee ? weaponName : isReloading ? $"{weaponName}  RELOADING..." : $"{weaponName}  {CurrentAmmo} / {CurrentReserve}";
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(Screen.width - 300f, Screen.height - 92f, 275f, 65f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(Screen.width - 290f, Screen.height - 84f, 255f, 40f), ammoText, centered);
        GUIStyle help = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 13 };
        help.normal.textColor = new Color(0.75f, 0.82f, 0.88f);
        GUI.Label(new Rect(Screen.width * 0.5f - 250f, 12f, 500f, 28f), "[1] Rifle   [2] Handgun   [3] Baton   [4] Sniper    RMB Aim    R Reload", help);

        if (isChargingSniper)
        {
            float fill = sniperCharge / MaximumSniperChargeTime;
            Rect bar = new Rect(Screen.width * 0.5f - 120f, Screen.height - 115f, 240f, 18f);
            GUI.color = Color.black;
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = Color.Lerp(new Color(0.2f, 0.65f, 1f), Color.white, fill);
            GUI.DrawTexture(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * fill, bar.height - 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(bar.x, bar.y - 22f, bar.width, 20f), "SNIPER CHARGE", help);
        }
    }

    private static void DrawSniperScope()
    {
        float size = Mathf.Min(Screen.width, Screen.height) * 0.72f;
        float left = (Screen.width - size) * 0.5f;
        float top = (Screen.height - size) * 0.5f;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, left, Screen.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(left + size, 0f, Screen.width - left - size, Screen.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(left, 0f, size, top), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(left, top + size, size, Screen.height - top - size), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - 1f, top, 2f, size), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(left, Screen.height * 0.5f - 1f, size, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
