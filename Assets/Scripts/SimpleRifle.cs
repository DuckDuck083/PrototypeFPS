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
    private readonly int[] slotSelections = { 0, 0, 0, 0 };
    private readonly int[] builtVariants = { -1, -1, -1, -1 };
    private int currentSlot;
    private static readonly string[][] SlotWeaponNames =
    {
        new[] { "ASSAULT RIFLE", "ROCKET LAUNCHER", "SHOTGUN", "MINIGUN" },
        new[] { "RIOT SHIELD", "HANDGUN", "REVOLVER", "MEDPACK" },
        new[] { "BATON", "FISTS", "KNIFE", "SCYTHE" },
        new[] { "SNIPER RIFLE", "FRAG GRENADE", "SMOKE GRENADE", "PROXIMITY MINE" }
    };
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
    private bool isPrimingGrenade;
    private float grenadePrimeTime;
    private const float GrenadeFuseTime = 2.2f;
    private const int MaximumRocketReserve = 12;
    private const int MaximumGrenades = 8;

    private bool IsAiming => currentWeapon != WeaponType.Melee && !isReloading && (currentWeapon == WeaponType.Sniper ? sniperScopeToggled : aimAction.IsPressed());
    public bool IsShieldBlocking => currentSlot == 1 && slotSelections[1] == 0
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
        SelectSlot(0);
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
        isPrimingGrenade = false;
        grenadePrimeTime = 0f;
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
        if (currentSlot == 3 && slotSelections[3] == 0 && !isReloading && aimAction.WasPressedThisFrame())
            sniperScopeToggled = !sniperScopeToggled;

        if (!isReloading)
        {
            if (rifleSelectAction.WasPressedThisFrame()) SelectSlot(0);
            if (handgunSelectAction.WasPressedThisFrame()) SelectSlot(1);
            if (meleeSelectAction.WasPressedThisFrame()) SelectSlot(2);
            if (sniperSelectAction.WasPressedThisFrame()) SelectSlot(3);
        }

        if (reloadAction.WasPressedThisFrame())
            TryReload();

        if (!isReloading && Time.time >= nextShotTime)
        {
            HandleCurrentWeapon();
        }

        UpdateAimingVisuals();
    }

    public void SetLoadoutSlot(int slotIndex, int weaponIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4 || weaponIndex < 0 || weaponIndex > 3)
            return;
        slotSelections[slotIndex] = weaponIndex;
        if (slotIndex == 0)
        {
            int[] magazines = { 30, 4, 8, 100 };
            int[] reserves = { 90, 12, 32, 200 };
            rifleMagazineSize = magazines[weaponIndex];
            rifleAmmo = rifleMagazineSize;
            rifleReserveAmmo = reserves[weaponIndex];
        }
        else if (slotIndex == 1)
        {
            int[] magazines = { 0, 12, 6, 3 };
            int[] reserves = { 0, 48, 30, 0 };
            handgunMagazineSize = Mathf.Max(1, magazines[weaponIndex]);
            handgunAmmo = magazines[weaponIndex];
            handgunReserveAmmo = reserves[weaponIndex];
        }
        else if (slotIndex == 3)
        {
            int[] ammunition = { 5, 4, 3, 2 };
            sniperMagazineSize = ammunition[weaponIndex];
            sniperAmmo = ammunition[weaponIndex];
            sniperReserveAmmo = weaponIndex == 0 ? 20 : 0;
        }
    }

    public bool IsLoadoutSelection(int slotIndex, int weaponIndex)
    {
        return slotIndex >= 0 && slotIndex < 4 && slotSelections[slotIndex] == weaponIndex;
    }

    public void EquipLoadoutSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < 4)
            SelectSlot(slotIndex);
    }

    public string GetLoadoutSlotName(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4)
            return "EMPTY";
        return SlotWeaponNames[slotIndex][slotSelections[slotIndex]];
    }

    public string GetLoadoutOptionName(int slotIndex, int optionIndex)
    {
        return slotIndex >= 0 && slotIndex < 4 && optionIndex >= 0 && optionIndex < 4
            ? SlotWeaponNames[slotIndex][optionIndex]
            : "EMPTY";
    }

    private void SelectSlot(int slotIndex)
    {
        currentSlot = slotIndex;
        SelectWeapon((WeaponType)slotIndex);
        BuildSelectedVariantModel(slotIndex);
    }

    private void BuildSelectedVariantModel(int slotIndex)
    {
        int option = slotSelections[slotIndex];
        if (builtVariants[slotIndex] == option) return;
        builtVariants[slotIndex] = option;
        Transform model = slotIndex == 0 ? rifleModel : slotIndex == 1 ? handgunModel : slotIndex == 2 ? meleeModel : sniperModel;
        foreach (Transform child in model) Destroy(child.gameObject);
        Material dark = CreateMaterial(new Color(0.07f, 0.08f, 0.09f));
        Material metal = CreateMaterial(new Color(0.2f, 0.23f, 0.26f));

        if (slotIndex == 0)
        {
            float length = option == 1 ? 1.15f : option == 2 ? 0.62f : option == 3 ? 0.9f : 0.72f;
            float width = option == 1 ? 0.24f : option == 3 ? 0.2f : 0.15f;
            AddPart(model, SlotWeaponNames[0][option], new Vector3(0f, 0f, 0.3f), new Vector3(width, width, length), metal);
            AddPart(model, "Barrel", new Vector3(0f, 0.02f, 0.3f + length * 0.55f), new Vector3(width * 0.35f, width * 0.35f, length * 0.45f), dark);
            AddPart(model, "Grip", new Vector3(0f, -0.18f, 0.12f), new Vector3(0.1f, 0.28f, 0.13f), dark, 10f);
        }
        else if (slotIndex == 1 && option == 0)
        {
            AddPart(model, "Shield Left", new Vector3(-0.28f, 0.05f, 0.2f), new Vector3(0.19f, 0.95f, 0.08f), metal);
            AddPart(model, "Shield Right", new Vector3(0.28f, 0.05f, 0.2f), new Vector3(0.19f, 0.95f, 0.08f), metal);
            AddPart(model, "Shield Top", new Vector3(0f, 0.43f, 0.2f), new Vector3(0.75f, 0.19f, 0.08f), metal);
            AddPart(model, "Shield Bottom", new Vector3(0f, -0.25f, 0.2f), new Vector3(0.75f, 0.57f, 0.08f), metal);
        }
        else if (slotIndex == 1)
        {
            Color supportColor = option == 3 ? new Color(0.15f, 0.8f, 0.3f) : new Color(0.25f, 0.27f, 0.3f);
            Material support = CreateMaterial(supportColor);
            AddPart(model, SlotWeaponNames[1][option], new Vector3(0f, 0f, 0.2f), option == 3 ? new Vector3(0.32f, 0.24f, 0.28f) : new Vector3(0.14f, 0.14f, option == 2 ? 0.58f : 0.45f), support);
            AddPart(model, "Grip", new Vector3(0f, -0.2f, 0.03f), new Vector3(0.13f, 0.32f, 0.15f), dark, 10f);
        }
        else if (slotIndex == 2)
        {
            float length = option == 1 ? 0.28f : option == 2 ? 0.5f : option == 3 ? 0.9f : 0.65f;
            AddPart(model, SlotWeaponNames[2][option], new Vector3(0f, 0.15f, 0.18f), new Vector3(option == 3 ? 0.06f : 0.1f, length, option == 2 ? 0.03f : 0.09f), option == 2 ? metal : dark, 35f);
        }
        else
        {
            float length = option == 0 ? 1.1f : 0.3f;
            AddPart(model, SlotWeaponNames[3][option], new Vector3(0f, 0f, 0.25f), new Vector3(option == 0 ? 0.16f : 0.28f, option == 0 ? 0.16f : 0.3f, length), option == 2 ? CreateMaterial(new Color(0.35f, 0.4f, 0.42f)) : metal);
            if (option == 0) AddPart(model, "Scope", new Vector3(0f, 0.15f, 0.3f), new Vector3(0.12f, 0.12f, 0.38f), dark);
        }
    }

    private void HandleCurrentWeapon()
    {
        int option = slotSelections[currentSlot];
        if (currentSlot == 0)
        {
            if (option == 0 && attackAction.IsPressed()) FireHitscan(24f, 0.12f, 1, 0.002f, false);
            else if (option == 1 && attackAction.WasPressedThisFrame()) LaunchRocket();
            else if (option == 2 && attackAction.WasPressedThisFrame()) FireHitscan(11f, 0.72f, 8, 0.055f, false);
            else if (option == 3 && attackAction.IsPressed()) FireHitscan(10f, 0.065f, 1, 0.018f, false);
        }
        else if (currentSlot == 1)
        {
            if (option == 1 && attackAction.WasPressedThisFrame()) FireHitscan(22f, 0.24f, 1, 0.008f, false);
            else if (option == 2 && attackAction.WasPressedThisFrame()) FireHitscan(48f, 0.48f, 1, 0.004f, false);
            else if (option == 3 && attackAction.WasPressedThisFrame())
            {
                if (handgunAmmo > 0 && GetComponent<PlayerVitals>().Heal(35f))
                {
                    handgunAmmo--;
                    nextShotTime = Time.time + 3f;
                }
            }
        }
        else if (currentSlot == 2 && attackAction.IsPressed())
        {
            float[] damages = { 45f, 25f, 60f, 85f };
            float[] delays = { 0.65f, 0.32f, 0.48f, 0.9f };
            SwingMelee(damages[option], delays[option], option == 3 ? 3.2f : 2.4f);
        }
        else if (currentSlot == 3)
        {
            if (option == 0) UpdateSniperCharge();
            else if (option == 1) UpdateGrenadePriming();
            else if (option == 2 && attackAction.WasPressedThisFrame()) ThrowSmokeGrenade();
            else if (option == 3 && attackAction.WasPressedThisFrame()) PlaceMine();
        }
    }

    private void FireHitscan(float damage, float delay, int pellets, float spread, bool sniperHeadshots)
    {
        if (CurrentAmmo <= 0) { TryReload(); return; }
        SetCurrentAmmo(CurrentAmmo - 1);
        nextShotTime = Time.time + delay;
        bool registeredHit = false;
        for (int i = 0; i < pellets; i++)
        {
            Vector3 direction = playerCamera.transform.forward
                + playerCamera.transform.right * Random.Range(-spread, spread)
                + playerCamera.transform.up * Random.Range(-spread, spread);
            if (!Physics.Raycast(playerCamera.transform.position, direction.normalized, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore)) continue;
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target == null) continue;
            bool critical = sniperHeadshots && hit.collider.gameObject.name == "Head";
            float dealt = critical ? damage * 3f : damage;
            target.TakeDamage(dealt);
            lastDamageAmount = dealt;
            lastHitWasCritical = critical;
            registeredHit = true;
        }
        if (registeredHit) hitMarkerUntil = Time.time + 0.35f;
        gunshotAudio.pitch = Random.Range(0.92f, 1.08f);
        gunshotAudio.PlayOneShot(gunshotClip, 0.65f);
    }

    private void ThrowSmokeGrenade()
    {
        if (sniperAmmo <= 0) return;
        sniperAmmo--;
        nextShotTime = Time.time + 1f;
        GameObject smoke = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        smoke.name = "Smoke Cloud";
        smoke.transform.position = playerCamera.transform.position + playerCamera.transform.forward * 8f;
        smoke.transform.localScale = Vector3.one * 9f;
        Destroy(smoke.GetComponent<Collider>());
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = new Color(0.3f, 0.34f, 0.38f);
        smoke.GetComponent<Renderer>().material = material;
        Destroy(smoke, 8f);
    }

    private void PlaceMine()
    {
        if (sniperAmmo <= 0) return;
        sniperAmmo--;
        nextShotTime = Time.time + 1f;
        GameObject mine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        mine.name = "Proximity Mine";
        mine.transform.position = transform.position + transform.forward * 1.2f + Vector3.up * 0.08f;
        mine.transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
        ExplosiveProjectile explosive = mine.AddComponent<ExplosiveProjectile>();
        explosive.Configure(120f, 5f, 20f, false, this, true);
    }

    private static string GetWeaponName(WeaponType weapon)
    {
        return weapon == WeaponType.Rifle ? "ROCKET LAUNCHER"
            : weapon == WeaponType.Handgun ? "RIOT SHIELD"
            : weapon == WeaponType.Melee ? "BATON"
            : "GRENADE";
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
        isPrimingGrenade = false;
        grenadePrimeTime = 0f;
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

        if (isPrimingGrenade)
        {
            float primeMotion = Mathf.Clamp01(grenadePrimeTime / 0.45f);
            currentModel.localPosition = Vector3.Lerp(currentRestPosition, currentRestPosition + new Vector3(-0.18f, 0.12f, 0.18f), primeMotion);
            currentModel.localRotation = Quaternion.Euler(-20f * primeMotion, 0f, -35f * primeMotion);
            return;
        }

        Vector3 shieldRaisedPosition = new Vector3(0f, -0.08f, 0.38f);
        Vector3 targetPosition = IsShieldBlocking ? shieldRaisedPosition : currentRestPosition;
        currentModel.localPosition = Vector3.Lerp(currentModel.localPosition, targetPosition, 14f * Time.deltaTime);
        currentModel.localRotation = Quaternion.Slerp(currentModel.localRotation, Quaternion.identity, 18f * Time.deltaTime);
        bool sniperScoped = currentSlot == 3 && slotSelections[3] == 0 && IsAiming;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, sniperScoped ? 25f : normalFieldOfView, 12f * Time.deltaTime);
        SetCurrentModelVisible(!sniperScoped);
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
        float remainingFuse = Mathf.Max(0.35f, GrenadeFuseTime - grenadePrimeTime);
        explosive.Configure(90f, 4.5f, remainingFuse, false, this);
        isPrimingGrenade = false;
        grenadePrimeTime = 0f;
    }

    private void UpdateGrenadePriming()
    {
        if (sniperAmmo <= 0)
            return;

        if (attackAction.WasPressedThisFrame())
        {
            isPrimingGrenade = true;
            grenadePrimeTime = 0f;
        }

        if (!isPrimingGrenade)
            return;

        if (attackAction.IsPressed())
            grenadePrimeTime = Mathf.Min(1.85f, grenadePrimeTime + Time.deltaTime);

        if (attackAction.WasReleasedThisFrame())
            ThrowGrenade();
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

            ApplyDamage(hit, damage, currentSlot == 3 && slotSelections[3] == 0);
            CreateBulletHole(hit.point, hit.normal, hit.transform);
        }

        CreateTracer(tracerEnd);
        CreateMuzzleFlash();
        if (CurrentAmmo == 0)
            TryReload();
    }

    private void SwingMelee(float meleeDamage = 45f, float meleeDelay = 0.65f, float meleeRange = 2.4f)
    {
        nextShotTime = Time.time + meleeDelay;
        currentModel.localRotation = Quaternion.Euler(0f, 0f, -55f);
        currentModel.localPosition = currentRestPosition + new Vector3(-0.18f, 0.08f, 0.18f);

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.SphereCast(ray, 0.35f, out RaycastHit hit, meleeRange, ~0, QueryTriggerInteraction.Ignore))
        {
            ApplyDamage(hit, meleeDamage);
            CreateBulletHole(hit.point, hit.normal, hit.transform);
        }
    }

    private void ApplyDamage(RaycastHit hit, float baseDamage, bool allowHeadshotCritical = false)
    {
        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
        if (damageable == null)
            return;

        bool critical = allowHeadshotCritical && hit.collider.gameObject.name == "Head";
        float finalDamage = critical ? baseDamage * 3f : baseDamage;
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

    public bool AddReserveAmmo(int rifleRounds, int handgunRounds, int sniperRounds)
    {
        int oldRockets = rifleReserveAmmo;
        int oldGrenades = sniperAmmo;
        rifleReserveAmmo = Mathf.Min(MaximumRocketReserve, rifleReserveAmmo + rifleRounds);
        sniperAmmo = Mathf.Min(MaximumGrenades, sniperAmmo + sniperRounds);
        return rifleReserveAmmo > oldRockets || sniperAmmo > oldGrenades;
    }

    private void TryReload()
    {
        bool reloadable = currentSlot == 0
            || (currentSlot == 1 && (slotSelections[1] == 1 || slotSelections[1] == 2))
            || (currentSlot == 3 && slotSelections[3] == 0);
        if (reloadable && !isReloading && CurrentAmmo < CurrentMagazineSize && CurrentReserve > 0)
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
        AddPart(handgunModel, "Shield Left", new Vector3(-0.28f, 0.05f, 0.2f), new Vector3(0.19f, 0.95f, 0.08f), metal);
        AddPart(handgunModel, "Shield Right", new Vector3(0.28f, 0.05f, 0.2f), new Vector3(0.19f, 0.95f, 0.08f), metal);
        AddPart(handgunModel, "Shield Top", new Vector3(0f, 0.43f, 0.2f), new Vector3(0.75f, 0.19f, 0.08f), metal);
        AddPart(handgunModel, "Shield Bottom", new Vector3(0f, -0.25f, 0.2f), new Vector3(0.75f, 0.57f, 0.08f), metal);
        AddPart(handgunModel, "Window Rim Top", new Vector3(0f, 0.32f, 0.18f), new Vector3(0.39f, 0.035f, 0.04f), dark);
        AddPart(handgunModel, "Window Rim Bottom", new Vector3(0f, 0.05f, 0.18f), new Vector3(0.39f, 0.035f, 0.04f), dark);

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
        string weaponName = SlotWeaponNames[currentSlot][slotSelections[currentSlot]];
        string ammoText = currentWeapon == WeaponType.Melee || currentWeapon == WeaponType.Handgun ? weaponName
            : currentWeapon == WeaponType.Sniper ? $"{weaponName}  {sniperAmmo}"
            : isReloading ? $"{weaponName}  RELOADING..." : $"{weaponName}  {CurrentAmmo} / {CurrentReserve}";
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

        if (isPrimingGrenade)
        {
            float primeRatio = grenadePrimeTime / 1.85f;
            GUIStyle primeStyle = new GUIStyle(help) { fontSize = 15, fontStyle = FontStyle.Bold };
            primeStyle.normal.textColor = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.08f), primeRatio);
            GUI.Label(new Rect(Screen.width * 0.5f - 150f, Screen.height - 120f, 300f, 26f), $"PRIMING  {grenadePrimeTime:0.0}s", primeStyle);
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
