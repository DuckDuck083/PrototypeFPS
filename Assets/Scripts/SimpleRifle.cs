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
    private float reloadProgress;
    private float hitMarkerUntil;
    private float lastDamageAmount;
    private bool lastHitWasCritical;
    private bool isReloading;
    private bool isChargingSniper;
    private bool sniperScopeToggled;

    private bool IsAiming => currentWeapon != WeaponType.Melee && !isReloading && (currentWeapon == WeaponType.Sniper ? sniperScopeToggled : aimAction.IsPressed());
    public bool IsShieldBlocking => currentWeapon == WeaponType.Handgun
        && attackAction.IsPressed()
        && !isReloading;
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

        if (!isReloading && Time.time >= nextShotTime)
        {
            if (currentWeapon == WeaponType.Rifle && attackAction.WasPressedThisFrame()) LaunchRocket();
            else if (currentWeapon == WeaponType.Sniper && attackAction.WasPressedThisFrame()) ThrowGrenade();
            else if (currentWeapon == WeaponType.Melee && attackAction.IsPressed()) Shoot();
        }

        UpdateAimingVisuals();
    }

    private void UpdateSniperCharge()
    {
        if (!IsAiming)
        {
            isChargingSniper = false;
            sniperCharge = 0f;
            return;
        }

        if (CurrentAmmo <= 0)
        {
            if (attackAction.WasPressedThisFrame()) TryReload();
            return;
        }

        isChargingSniper = true;
        sniperCharge = Mathf.Min(MaximumSniperChargeTime, sniperCharge + Time.deltaTime);

        if (attackAction.WasPressedThisFrame() && Time.time >= nextShotTime)
        {
            float chargeRatio = sniperCharge / MaximumSniperChargeTime;
            Shoot(chargeRatio);
            sniperCharge = 0f;
        }
    }

    private void SelectWeapon(WeaponType weapon)
    {
        isChargingSniper = false;
        sniperCharge = 0f;
        sniperScopeToggled = false;
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

        Vector3 shieldRaisedPosition = new Vector3(0f, -0.08f, 0.38f);
        Vector3 targetPosition = IsShieldBlocking ? shieldRaisedPosition : currentRestPosition;
        currentModel.localPosition = Vector3.Lerp(currentModel.localPosition, targetPosition, 14f * Time.deltaTime);
        currentModel.localRotation = Quaternion.Slerp(currentModel.localRotation, Quaternion.identity, 18f * Time.deltaTime);
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, normalFieldOfView, 12f * Time.deltaTime);
        SetCurrentModelVisible(true);
    }

    private void LaunchRocket()
    {
        if (rifleAmmo <= 0)
        {
            TryReload();
            return;
        }

        rifleAmmo--;
        nextShotTime = Time.time + 0.8f;
        GameObject rocket = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        rocket.name = "Rocket";
        rocket.transform.position = playerCamera.transform.position + playerCamera.transform.forward * 1.2f;
        rocket.transform.rotation = Quaternion.LookRotation(playerCamera.transform.forward) * Quaternion.Euler(90f, 0f, 0f);
        rocket.transform.localScale = new Vector3(0.16f, 0.35f, 0.16f);
        Rigidbody body = rocket.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = playerCamera.transform.forward * 32f;
        ExplosiveProjectile explosive = rocket.AddComponent<ExplosiveProjectile>();
        explosive.Configure(110f, 5f, 5f, true, this);
        gunshotAudio.pitch = 0.65f;
        gunshotAudio.PlayOneShot(gunshotClip, 1f);
        currentModel.localPosition += Vector3.back * 0.12f;

        if (rifleAmmo == 0)
            TryReload();
    }

    private void ThrowGrenade()
    {
        if (sniperAmmo <= 0)
            return;

        sniperAmmo--;
        nextShotTime = Time.time + 0.55f;
        GameObject grenade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        grenade.name = "Grenade";
        grenade.transform.position = playerCamera.transform.position + playerCamera.transform.forward * 0.8f;
        grenade.transform.localScale = Vector3.one * 0.28f;
        Rigidbody body = grenade.AddComponent<Rigidbody>();
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = playerCamera.transform.forward * 13f + Vector3.up * 3.5f;
        ExplosiveProjectile explosive = grenade.AddComponent<ExplosiveProjectile>();
        explosive.Configure(90f, 4.5f, 2.2f, false, this);
    }

    public void ReportExplosiveHit(float totalDamage)
    {
        lastDamageAmount = totalDamage;
        lastHitWasCritical = false;
        hitMarkerUntil = Time.time + 0.45f;
    }

    private void SetCurrentModelVisible(bool visible)
    {
        Renderer[] modelRenderers = currentModel.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer modelRenderer in modelRenderers)
            modelRenderer.enabled = visible;
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

            ApplyDamage(hit, damage);
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
            ApplyDamage(hit, 45f);
            CreateBulletHole(hit.point, hit.normal, hit.transform);
        }
    }

    private void ApplyDamage(RaycastHit hit, float baseDamage)
    {
        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
        if (damageable == null)
            return;

        bool critical = false;
        float finalDamage = baseDamage;
        damageable.TakeDamage(finalDamage);

        lastDamageAmount = finalDamage;
        lastHitWasCritical = critical;
        hitMarkerUntil = Time.time + 0.35f;
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
        if (currentWeapon == WeaponType.Rifle && !isReloading && CurrentAmmo < CurrentMagazineSize && CurrentReserve > 0)
            StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        isReloading = true;
        sniperScopeToggled = false;
        SetCurrentModelVisible(true);
        playerCamera.fieldOfView = normalFieldOfView;
        float reloadDuration = 0.9f;
        float elapsed = 0f;
        while (elapsed < reloadDuration)
        {
            elapsed += Time.deltaTime;
            reloadProgress = Mathf.Clamp01(elapsed / reloadDuration);
            float progress = reloadProgress;
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
        reloadProgress = 0f;
        isReloading = false;
    }

    private void CreateWeaponModels()
    {
        Material dark = CreateMaterial(new Color(0.08f, 0.09f, 0.1f));
        Material metal = CreateMaterial(new Color(0.2f, 0.22f, 0.24f));

        rifleModel = CreateModelRoot("Rocket Launcher");
        AddPart(rifleModel, "Tube", new Vector3(0f, 0f, 0.35f), new Vector3(0.22f, 0.22f, 1.15f), metal);
        AddPart(rifleModel, "Muzzle", new Vector3(0f, 0f, 0.96f), new Vector3(0.31f, 0.31f, 0.12f), dark);
        AddPart(rifleModel, "Grip", new Vector3(0f, -0.2f, 0.2f), new Vector3(0.11f, 0.3f, 0.13f), dark, 8f);
        AddPart(rifleModel, "Sight", new Vector3(0f, 0.16f, 0.36f), new Vector3(0.08f, 0.08f, 0.2f), dark);

        handgunModel = CreateModelRoot("Riot Shield");
        AddPart(handgunModel, "Shield", new Vector3(0f, 0.05f, 0.2f), new Vector3(0.75f, 0.95f, 0.08f), metal);
        AddPart(handgunModel, "Viewport", new Vector3(0f, 0.25f, 0.14f), new Vector3(0.38f, 0.16f, 0.04f), dark);

        meleeModel = CreateModelRoot("Training Baton");
        AddPart(meleeModel, "Handle", new Vector3(0f, -0.16f, 0.02f), new Vector3(0.1f, 0.32f, 0.1f), dark, 18f);
        AddPart(meleeModel, "Baton", new Vector3(0f, 0.18f, 0.18f), new Vector3(0.09f, 0.65f, 0.09f), metal, 35f);

        sniperModel = CreateModelRoot("Grenade");
        AddPart(sniperModel, "Body", Vector3.zero, new Vector3(0.25f, 0.32f, 0.25f), metal);
        AddPart(sniperModel, "Lever", new Vector3(0f, 0.2f, 0f), new Vector3(0.08f, 0.16f, 0.08f), dark);
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
        GUIStyle centered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
        centered.normal.textColor = Color.white;
        GUI.Label(new Rect(Screen.width * 0.5f - 15f, Screen.height * 0.5f - 15f, 30f, 30f), "+", centered);
        string weaponName = currentWeapon == WeaponType.Rifle ? "ROCKET LAUNCHER [1]" : currentWeapon == WeaponType.Handgun ? "RIOT SHIELD [2]" : currentWeapon == WeaponType.Melee ? "BATON [3]" : "GRENADES [4]";
        string ammoText = currentWeapon == WeaponType.Melee || currentWeapon == WeaponType.Handgun ? weaponName : isReloading ? $"{weaponName}  RELOADING..." : $"{weaponName}  {CurrentAmmo} / {CurrentReserve}";
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(Screen.width - 300f, Screen.height - 92f, 275f, 65f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(Screen.width - 290f, Screen.height - 84f, 255f, 40f), ammoText, centered);
        GUIStyle help = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 13 };
        help.normal.textColor = new Color(0.75f, 0.82f, 0.88f);
        GUI.Label(new Rect(Screen.width * 0.5f - 300f, 12f, 600f, 28f), "[1] Rocket Launcher   [2] Riot Shield   [3] Baton   [4] Grenade    R Reload", help);

        if (Time.time < hitMarkerUntil)
            DrawHitMarker(centered);

        if (isReloading)
        {
            Rect reloadBar = new Rect(Screen.width - 285f, Screen.height - 34f, 245f, 8f);
            GUI.color = new Color(0.05f, 0.06f, 0.07f, 0.9f);
            GUI.DrawTexture(reloadBar, Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.7f, 1f);
            GUI.DrawTexture(new Rect(reloadBar.x, reloadBar.y, reloadBar.width * reloadProgress, reloadBar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

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

    private void DrawHitMarker(GUIStyle centered)
    {
        Color markerColor = lastHitWasCritical ? new Color(1f, 0.2f, 0.12f) : Color.white;
        GUIStyle marker = new GUIStyle(centered) { fontSize = lastHitWasCritical ? 30 : 25, fontStyle = FontStyle.Bold };
        marker.normal.textColor = markerColor;
        string symbol = lastHitWasCritical ? "✦" : "×";
        GUI.Label(new Rect(Screen.width * 0.5f - 25f, Screen.height * 0.5f - 25f, 50f, 50f), symbol, marker);

        GUIStyle damage = new GUIStyle(centered) { fontSize = 16, fontStyle = FontStyle.Bold };
        damage.normal.textColor = markerColor;
        string label = lastHitWasCritical ? $"CRITICAL  {Mathf.RoundToInt(lastDamageAmount)}" : $"{Mathf.RoundToInt(lastDamageAmount)} DAMAGE";
        GUI.Label(new Rect(Screen.width * 0.5f - 100f, Screen.height * 0.5f + 24f, 200f, 28f), label, damage);
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
