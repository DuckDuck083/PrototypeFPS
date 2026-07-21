using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SimpleRifle : MonoBehaviour
{
    public enum PlayerClass { Soldier, Tank, Engineer, Sniper, Demoman }
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
    [SerializeField, Min(1)] private int sniperMagazineSize = 1;
    [SerializeField, Min(0)] private int sniperReserveAmmo = 24;

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
        new[] { "ASSAULT RIFLE", "ROCKET LAUNCHER", "SHOTGUN", "MINIGUN", "GRENADE LAUNCHER", "STICKYBOMB LAUNCHER", "SNIPER RIFLE" },
        new[] { "RIOT SHIELD", "HANDGUN", "REVOLVER", "MEDPACK", "SHOTGUN", "TURRET BUILDER", "STICKYBOMB LAUNCHER" },
        new[] { "BATON", "FISTS", "KNIFE", "SCYTHE", "AXE", "WRENCH" },
        new[] { "SNIPER RIFLE", "FRAG GRENADE", "SMOKE GRENADE", "PROXIMITY MINE", "VAMP PISTOL" }
    };
    private int rifleAmmo;
    private int handgunAmmo;
    private int sniperAmmo;
    private float nextShotTime;
    private float normalFieldOfView;
    private float sniperCharge;
    private const float MaximumSniperChargeTime = 2.6f;
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
    private float nextDashTime;
    private float dashAnimationUntil;
    private float nextTurretTime;
    private EngineerTurret activeTurret;
    public PlayerClass CurrentClass { get; private set; } = PlayerClass.Soldier;
    private readonly System.Collections.Generic.List<ExplosiveProjectile> activeStickies = new System.Collections.Generic.List<ExplosiveProjectile>();
    private float recoilPitch;
    private float recoilYaw;
    private static Texture2D scopeMaskTexture;
    private const float FalloffStart = 9f;
    private const float FalloffEnd = 58f;
    private const float MinimumFalloffMultiplier = 0.35f;

    private bool IsAiming => currentWeapon != WeaponType.Melee && !isReloading && (IsSniperRifleEquipped ? sniperScopeToggled : aimAction.IsPressed());
    private bool IsSniperRifleEquipped => (currentSlot == 3 && slotSelections[3] == 0) || (currentSlot == 0 && slotSelections[0] == 6);
    public bool IsShieldBlocking => currentSlot == 1 && slotSelections[1] == 0
        && attackAction.IsPressed()
        && !isReloading;
    public float MovementMultiplier => (CurrentClass == PlayerClass.Tank ? 0.68f : 1f)
        * (currentSlot == 0 && slotSelections[0] == 3 && attackAction.IsPressed() ? 0.62f : 1f);
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
        CreateScopeMask();
        SetPlayerClass(CurrentClass);
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
        if (currentSlot == 2 && slotSelections[2] == 3 && aimAction.WasPressedThisFrame() && Time.time >= nextDashTime)
            ScytheDash();

        if (((currentSlot == 0 && slotSelections[0] == 5) || (currentSlot == 1 && slotSelections[1] == 6)) && aimAction.WasPressedThisFrame())
            DetonateStickies();

        if (IsSniperRifleEquipped && !isReloading && aimAction.WasPressedThisFrame())
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
        if (slotIndex < 0 || slotIndex >= 4 || weaponIndex < 0 || weaponIndex >= SlotWeaponNames[slotIndex].Length)
            return;
        slotSelections[slotIndex] = weaponIndex;
        if (slotIndex == 0)
        {
            int[] magazines = { 30, 4, 8, 100, 6, 8, 1 };
            int[] reserves = { 90, 12, 32, 200, 24, 24, 24 };
            rifleMagazineSize = magazines[weaponIndex];
            rifleAmmo = rifleMagazineSize;
            rifleReserveAmmo = reserves[weaponIndex];
        }
        else if (slotIndex == 1)
        {
            int[] magazines = { 0, 12, 6, 3, 8, 0, 8 };
            int[] reserves = { 0, 48, 30, 0, 32, 0, 24 };
            handgunMagazineSize = Mathf.Max(1, magazines[weaponIndex]);
            handgunAmmo = magazines[weaponIndex];
            handgunReserveAmmo = reserves[weaponIndex];
        }
        else if (slotIndex == 3)
        {
            int[] ammunition = { 1, 4, 3, 2, 10 };
            sniperMagazineSize = ammunition[weaponIndex];
            sniperAmmo = ammunition[weaponIndex];
            sniperReserveAmmo = weaponIndex == 0 ? 24 : 0;
        }
        FindAnyObjectByType<WaveManager>()?.SaveProgress();
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
        return slotIndex >= 0 && slotIndex < 4 && optionIndex >= 0 && optionIndex < SlotWeaponNames[slotIndex].Length
            ? SlotWeaponNames[slotIndex][optionIndex]
            : "EMPTY";
    }

    public int GetLoadoutOptionCount(int slotIndex) => slotIndex >= 0 && slotIndex < 4 ? SlotWeaponNames[slotIndex].Length : 0;

    private static readonly int[][][] ClassLoadouts =
    {
        new[] { new[] { 1, 0 }, new[] { 1, 4 }, new[] { 0, 3 }, new[] { 1 } },
        new[] { new[] { 3 }, new[] { 0 }, new[] { 1, 4, 3 }, new[] { 2, 3 } },
        new[] { new[] { 2 }, new[] { 5 }, new[] { 5, 3 }, new[] { 3 } },
        new[] { new[] { 6 }, new[] { 2 }, new[] { 2, 3 }, new[] { 4 } },
        new[] { new[] { 4 }, new[] { 6 }, new[] { 0, 3 }, new[] { 3 } }
    };

    public int GetClassOptionCount(int slotIndex) => ClassLoadouts[(int)CurrentClass][slotIndex].Length;
    public int GetClassOptionIndex(int slotIndex, int classOptionIndex) => ClassLoadouts[(int)CurrentClass][slotIndex][classOptionIndex];

    public void SetPlayerClass(PlayerClass playerClass)
    {
        if (CurrentClass == PlayerClass.Engineer && playerClass != PlayerClass.Engineer && activeTurret != null)
            Destroy(activeTurret.gameObject);
        CurrentClass = playerClass;
        GetComponent<PlayerVitals>().ApplyClassStats(CurrentClass);
        for (int slot = 0; slot < 4; slot++)
        {
            int[] allowed = ClassLoadouts[(int)CurrentClass][slot];
            if (System.Array.IndexOf(allowed, slotSelections[slot]) < 0)
                SetLoadoutSlot(slot, allowed[0]);
        }
        SelectSlot(currentSlot);
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
        AddVariantDetails(model, slotIndex, option, dark, metal);
    }

    private static void AddVariantDetails(Transform model, int slot, int option, Material dark, Material metal)
    {
        Material accent = CreateMaterial(slot == 3 ? new Color(0.18f, 0.42f, 0.28f) : new Color(0.55f, 0.16f, 0.08f));
        if (slot == 0 && option == 0)
        {
            AddPart(model, "Stock", new Vector3(0f, -0.01f, -0.18f), new Vector3(0.2f, 0.18f, 0.32f), dark);
            AddPart(model, "Magazine", new Vector3(0f, -0.18f, 0.28f), new Vector3(0.13f, 0.3f, 0.18f), accent, -12f);
            AddPart(model, "Carry Sight", new Vector3(0f, 0.14f, 0.32f), new Vector3(0.07f, 0.1f, 0.28f), dark);
        }
        else if (slot == 0 && option == 1)
        {
            AddPart(model, "Rear Vent", new Vector3(0f, 0f, -0.32f), new Vector3(0.34f, 0.34f, 0.18f), dark);
            AddPart(model, "Warhead Cage", new Vector3(0f, 0f, 1.02f), new Vector3(0.38f, 0.38f, 0.12f), accent);
            AddPart(model, "Top Rail", new Vector3(0f, 0.18f, 0.3f), new Vector3(0.08f, 0.06f, 0.72f), dark);
        }
        else if (slot == 0 && option == 2)
        {
            AddPart(model, "Pump", new Vector3(0f, -0.03f, 0.65f), new Vector3(0.22f, 0.18f, 0.3f), dark);
            AddPart(model, "Shell Tube", new Vector3(0f, -0.11f, 0.55f), new Vector3(0.08f, 0.08f, 0.58f), accent);
            AddPart(model, "Wood Stock", new Vector3(0f, -0.04f, -0.13f), new Vector3(0.19f, 0.2f, 0.38f), accent, -7f);
        }
        else if (slot == 0 && option == 3)
        {
            AddPart(model, "Barrel Cluster", new Vector3(0f, 0.02f, 0.9f), new Vector3(0.29f, 0.29f, 0.55f), dark);
            AddPart(model, "Ammo Drum", new Vector3(0f, -0.22f, 0.25f), new Vector3(0.38f, 0.38f, 0.22f), accent, 90f);
            AddPart(model, "Rear Motor", new Vector3(0f, 0f, -0.18f), new Vector3(0.3f, 0.3f, 0.28f), metal);
        }
        else if (slot == 0 && option == 4)
        {
            AddPart(model, "Grenade Drum", new Vector3(0f, -0.18f, 0.25f), new Vector3(0.34f, 0.34f, 0.3f), accent, 90f);
            AddPart(model, "Wide Muzzle", new Vector3(0f, 0f, 0.88f), new Vector3(0.3f, 0.3f, 0.22f), dark);
            AddPart(model, "Launcher Stock", new Vector3(0f, -0.02f, -0.2f), new Vector3(0.23f, 0.2f, 0.4f), dark);
        }
        else if (slot == 0 && option == 5)
        {
            AddPart(model, "Sticky Canister", new Vector3(0f, -0.16f, 0.23f), new Vector3(0.4f, 0.28f, 0.38f), accent);
            AddPart(model, "Detonator Aerial", new Vector3(0.18f, 0.18f, 0.2f), new Vector3(0.035f, 0.3f, 0.035f), metal, -15f);
            AddPart(model, "Forked Muzzle", new Vector3(0f, 0f, 0.86f), new Vector3(0.35f, 0.12f, 0.22f), dark);
        }
        else if (slot == 0)
        {
            AddPart(model, "Long Barrel", new Vector3(0f, 0f, 0.82f), new Vector3(0.11f, 0.11f, 0.95f), dark);
            AddPart(model, "Scope", new Vector3(0f, 0.16f, 0.28f), new Vector3(0.14f, 0.14f, 0.42f), metal);
            AddPart(model, "Sniper Stock", new Vector3(0f, -0.02f, -0.22f), new Vector3(0.22f, 0.22f, 0.5f), accent);
        }
        else if (slot == 1 && option == 0)
        {
            AddPart(model, "Viewport", new Vector3(0f, 0.23f, 0.15f), new Vector3(0.38f, 0.17f, 0.025f), dark);
            AddPart(model, "Center Brace", new Vector3(0f, -0.12f, 0.14f), new Vector3(0.08f, 0.5f, 0.04f), accent);
        }
        else if (slot == 1 && option == 1)
            AddPart(model, "Slide", new Vector3(0f, 0.07f, 0.27f), new Vector3(0.18f, 0.09f, 0.48f), metal);
        else if (slot == 1 && option == 2)
        {
            AddPart(model, "Cylinder", new Vector3(0f, -0.01f, 0.22f), new Vector3(0.25f, 0.25f, 0.2f), metal, 90f);
            AddPart(model, "Hammer", new Vector3(0f, 0.12f, -0.08f), new Vector3(0.08f, 0.12f, 0.08f), dark, -20f);
        }
        else if (slot == 1 && option == 3)
        {
            AddPart(model, "Medical Cross", new Vector3(0f, 0.01f, 0.05f), new Vector3(0.08f, 0.26f, 0.04f), accent);
            AddPart(model, "Medical Cross Bar", new Vector3(0f, 0.01f, 0.03f), new Vector3(0.25f, 0.08f, 0.04f), accent);
        }
        else if (slot == 1 && option == 4)
        {
            AddPart(model, "Pump", new Vector3(0f, -0.03f, 0.56f), new Vector3(0.22f, 0.18f, 0.28f), dark);
            AddPart(model, "Shell Tube", new Vector3(0f, -0.1f, 0.45f), new Vector3(0.08f, 0.08f, 0.52f), accent);
        }
        else if (slot == 1)
        {
            AddPart(model, "Builder Screen", new Vector3(0f, 0.08f, 0.2f), new Vector3(0.3f, 0.2f, 0.08f), accent);
            AddPart(model, "Antenna", new Vector3(0.15f, 0.24f, 0.18f), new Vector3(0.035f, 0.28f, 0.035f), metal, -12f);
        }
        else if (slot == 2 && option == 0) AddPart(model, "Guard", new Vector3(0f, -0.02f, 0.05f), new Vector3(0.32f, 0.06f, 0.08f), accent);
        else if (slot == 2 && option == 1) AddPart(model, "Knuckle Guard", new Vector3(0f, 0.04f, 0.12f), new Vector3(0.32f, 0.18f, 0.12f), metal);
        else if (slot == 2 && option == 2) AddPart(model, "Knife Guard", new Vector3(0f, -0.03f, 0.08f), new Vector3(0.28f, 0.05f, 0.08f), accent);
        else if (slot == 2 && option == 3) AddPart(model, "Scythe Blade", new Vector3(0.17f, 0.5f, 0.2f), new Vector3(0.42f, 0.06f, 0.12f), metal, 18f);
        else if (slot == 2 && option == 4)
        {
            AddPart(model, "Axe Head", new Vector3(0.12f, 0.45f, 0.18f), new Vector3(0.42f, 0.28f, 0.1f), metal, 15f);
            AddPart(model, "Axe Edge", new Vector3(0.3f, 0.46f, 0.18f), new Vector3(0.12f, 0.36f, 0.07f), accent, 15f);
        }
        else if (slot == 2)
        {
            AddPart(model, "Wrench Jaw A", new Vector3(-0.12f, 0.43f, 0.18f), new Vector3(0.1f, 0.3f, 0.1f), metal, -20f);
            AddPart(model, "Wrench Jaw B", new Vector3(0.12f, 0.43f, 0.18f), new Vector3(0.1f, 0.3f, 0.1f), metal, 20f);
        }
        else if (slot == 3 && option == 0)
        {
            AddPart(model, "Stock", new Vector3(0f, -0.02f, -0.35f), new Vector3(0.2f, 0.2f, 0.45f), dark);
            AddPart(model, "Bipod", new Vector3(0f, -0.14f, 0.72f), new Vector3(0.32f, 0.06f, 0.08f), accent);
            AddPart(model, "Muzzle Brake", new Vector3(0f, 0f, 1.02f), new Vector3(0.25f, 0.2f, 0.16f), dark);
        }
        else if (option != 4)
        {
            AddPart(model, option == 3 ? "Mine Sensor" : "Safety Lever", new Vector3(0f, 0.22f, 0.25f), new Vector3(0.12f, 0.08f, 0.24f), option == 2 ? accent : dark, -12f);
            AddPart(model, "Safety Pin", new Vector3(0.16f, 0.14f, 0.25f), new Vector3(0.05f, 0.2f, 0.05f), metal, 30f);
        }
        else
        {
            AddPart(model, "Vamp Pistol Slide", new Vector3(0f, 0.04f, 0.35f), new Vector3(0.18f, 0.13f, 0.62f), dark);
            AddPart(model, "Healing Vial", new Vector3(0.13f, 0.02f, 0.2f), new Vector3(0.08f, 0.22f, 0.08f), accent);
            AddPart(model, "Pistol Grip", new Vector3(0f, -0.2f, 0.06f), new Vector3(0.15f, 0.34f, 0.16f), metal, 12f);
        }
    }

    private void HandleCurrentWeapon()
    {
        int option = slotSelections[currentSlot];
        if (currentSlot == 0)
        {
            if (option == 0 && attackAction.IsPressed()) FireHitscan(24f, 0.12f, 1, 0.002f, false);
            else if (option == 1 && attackAction.WasPressedThisFrame()) LaunchRocket();
            else if (option == 2 && attackAction.WasPressedThisFrame()) FireHitscan(12f, 0.62f, 12, IsAiming ? 0.075f : 0.12f, false);
            else if (option == 3 && attackAction.IsPressed()) FireHitscan(10f, 0.065f, 1, 0.018f, false);
            else if (option == 4 && attackAction.WasPressedThisFrame()) LaunchBouncingGrenade();
            else if (option == 5 && attackAction.WasPressedThisFrame()) LaunchStickyBomb();
            else if (option == 6) UpdateSniperCharge();
        }
        else if (currentSlot == 1)
        {
            if (option == 1 && attackAction.WasPressedThisFrame()) FireHitscan(22f, 0.24f, 1, 0.008f, false);
            else if (option == 2 && attackAction.WasPressedThisFrame()) FireHitscan(48f, 0.48f, 1, 0.004f, true, 0.18f, true);
            else if (option == 3 && attackAction.WasPressedThisFrame())
            {
                if (handgunAmmo > 0 && GetComponent<PlayerVitals>().Heal(35f))
                {
                    handgunAmmo--;
                    nextShotTime = Time.time + 3f;
                }
            }
            else if (option == 4 && attackAction.WasPressedThisFrame()) FireHitscan(12f, 0.62f, 12, IsAiming ? 0.075f : 0.12f, false);
            else if (option == 5 && attackAction.WasPressedThisFrame()) PlaceTurret();
            else if (option == 6 && attackAction.WasPressedThisFrame()) LaunchStickyBomb();
        }
        else if (currentSlot == 2 && attackAction.IsPressed())
        {
            float[] damages = { 45f, 38f, 60f, 85f, 78f, 52f };
            float[] delays = { 0.65f, 0.32f, 0.48f, 0.9f, 0.78f, 0.55f };
            SwingMelee(damages[option], delays[option], option == 3 ? 3.2f : 2.4f, option == 2 ? 0.22f : 0f);
        }
        else if (currentSlot == 3)
        {
            if (option == 0) UpdateSniperCharge();
            else if (option == 1) UpdateGrenadePriming();
            else if (option == 2 && attackAction.WasPressedThisFrame()) ThrowSmokeGrenade();
            else if (option == 3 && attackAction.WasPressedThisFrame()) PlaceMine();
            else if (option == 4 && attackAction.WasPressedThisFrame()) FireHitscan(16f, 0.3f, 1, IsAiming ? 0.003f : 0.012f, false, 0f, false, 12f);
        }

    }

    private void FireHitscan(float damage, float delay, int pellets, float spread, bool headshotCriticals, float miniCritChance = 0f, bool miniCrits = false, float healingOnHit = 0f)
    {
        if (CurrentAmmo <= 0) { TryReload(); return; }
        SetCurrentAmmo(CurrentAmmo - 1);
        nextShotTime = Time.time + delay;
        damage *= Random.Range(0.94f, 1.07f);
        bool registeredHit = false;
        for (int i = 0; i < pellets; i++)
        {
            Vector3 direction = playerCamera.transform.forward
                + playerCamera.transform.right * Random.Range(-spread, spread)
                + playerCamera.transform.up * Random.Range(-spread, spread);
            Vector3 tracerEnd = playerCamera.transform.position + direction.normalized * range;
            if (!Physics.Raycast(playerCamera.transform.position, direction.normalized, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
            {
                CreateTracer(tracerEnd);
                continue;
            }
            tracerEnd = hit.point;
            CreateTracer(tracerEnd);
            CreateBulletHole(hit.point, hit.normal, hit.transform);
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target == null) continue;
            bool critical = headshotCriticals && hit.collider.gameObject.name == "Head";
            bool miniCritical = !critical && miniCrits && Random.value < miniCritChance;
            float falloff = critical || miniCritical ? 1f : GetDamageFalloff(hit.distance);
            float dealt = (critical ? damage * 3f : miniCritical ? damage * 1.35f : damage) * falloff;
            target.TakeDamage(dealt);
            lastDamageAmount = dealt;
            lastHitWasCritical = critical || miniCritical;
            registeredHit = true;
            if (healingOnHit > 0f) GetComponent<PlayerVitals>().Heal(healingOnHit);
        }
        if (registeredHit) hitMarkerUntil = Time.time + 0.35f;
        gunshotAudio.pitch = Random.Range(0.92f, 1.08f);
        gunshotAudio.PlayOneShot(gunshotClip, 0.65f);
        CreateMuzzleFlash();
        ApplyRecoil(pellets > 1 ? 5.2f : 1.8f, pellets > 1 ? 1.5f : 0.55f);
    }

    private static float GetDamageFalloff(float distance)
    {
        return Mathf.Lerp(1f, MinimumFalloffMultiplier, Mathf.InverseLerp(FalloffStart, FalloffEnd, distance));
    }

    private void ApplyRecoil(float pitch, float yaw)
    {
        float scale = IsAiming ? 0.55f : 1f;
        recoilPitch = Mathf.Min(9f, recoilPitch + pitch * scale);
        recoilYaw += Random.Range(-yaw, yaw) * scale;
    }

    private void ScytheDash()
    {
        CharacterController controller = GetComponent<CharacterController>();
        controller.Move(transform.forward * 5.5f);
        nextDashTime = Time.time + 2.2f;
        dashAnimationUntil = Time.time + 0.32f;
    }

    private void ThrowSmokeGrenade()
    {
        if (sniperAmmo <= 0) return;
        sniperAmmo--;
        nextShotTime = Time.time + 1f;
        GameObject smoke = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        smoke.name = "Smoke Grenade";
        smoke.transform.position = playerCamera.transform.position + playerCamera.transform.forward * 0.8f;
        smoke.transform.localScale = Vector3.one * 0.25f;
        Rigidbody body = smoke.AddComponent<Rigidbody>();
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = playerCamera.transform.forward * 12f + Vector3.up * 3.2f;
        smoke.AddComponent<SmokeGrenade>();
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

    private void PlaceTurret()
    {
        if (activeTurret != null || Time.time < nextTurretTime) return;
        nextTurretTime = Time.time + 10f;
        GameObject turret = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        turret.name = "Engineer Turret";
        turret.transform.position = transform.position + transform.forward * 2f + Vector3.up * 0.38f;
        turret.transform.localScale = new Vector3(0.62f, 0.38f, 0.62f);
        turret.GetComponent<Renderer>().material = CreateMaterial(new Color(0.1f, 0.28f, 0.34f));
        Transform head = new GameObject("Tracking Head").transform;
        head.SetParent(turret.transform, false);
        head.localPosition = new Vector3(0f, 1.25f, 0f);
        AddPart(head, "Gun Body", Vector3.zero, new Vector3(0.55f, 0.3f, 0.65f), CreateMaterial(new Color(0.12f, 0.14f, 0.16f)));
        AddPart(head, "Barrel", new Vector3(0f, 0f, 0.62f), new Vector3(0.12f, 0.12f, 0.72f), CreateMaterial(new Color(0.04f, 0.05f, 0.06f)));
        activeTurret = turret.AddComponent<EngineerTurret>();
        activeTurret.Configure(head, transform);
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

        if (Time.time < dashAnimationUntil)
        {
            float phase = 1f - (dashAnimationUntil - Time.time) / 0.32f;
            currentModel.localPosition = currentRestPosition + new Vector3(-0.3f, 0.15f, 0.25f) * Mathf.Sin(phase * Mathf.PI);
            currentModel.localRotation = Quaternion.Euler(-25f, 25f, Mathf.Lerp(-95f, 55f, phase));
            return;
        }

        recoilPitch = Mathf.MoveTowards(recoilPitch, 0f, 18f * Time.deltaTime);
        recoilYaw = Mathf.MoveTowards(recoilYaw, 0f, 12f * Time.deltaTime);
        Vector3 shieldRaisedPosition = new Vector3(0f, -0.08f, 0.38f);
        Vector3 adsPosition = new Vector3(0f, -0.205f, currentRestPosition.z - 0.06f);
        Vector3 targetPosition = IsShieldBlocking ? shieldRaisedPosition : IsAiming ? adsPosition : currentRestPosition;
        currentModel.localPosition = Vector3.Lerp(currentModel.localPosition, targetPosition, 14f * Time.deltaTime);
        currentModel.localRotation = Quaternion.Slerp(currentModel.localRotation, Quaternion.Euler(-recoilPitch, recoilYaw, 0f), 18f * Time.deltaTime);
        bool sniperScoped = IsSniperRifleEquipped && IsAiming;
        float targetFov = sniperScoped ? 25f : IsAiming ? normalFieldOfView * 0.82f : normalFieldOfView;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, 12f * Time.deltaTime);
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

    private void LaunchBouncingGrenade()
    {
        if (rifleAmmo <= 0) { TryReload(); return; }
        rifleAmmo--;
        nextShotTime = Time.time + 0.72f;
        GameObject grenade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        grenade.name = "Launcher Grenade";
        grenade.transform.position = playerCamera.transform.position + playerCamera.transform.forward * 0.9f;
        grenade.transform.localScale = Vector3.one * 0.24f;
        Rigidbody body = grenade.AddComponent<Rigidbody>();
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = playerCamera.transform.forward * 18f + Vector3.up * 1.8f;
        body.mass = 0.65f;
        Collider grenadeCollider = grenade.GetComponent<Collider>();
        PhysicsMaterial bounce = new PhysicsMaterial("Grenade Bounce") { bounciness = 0.68f, dynamicFriction = 0.25f, staticFriction = 0.25f, bounceCombine = PhysicsMaterialCombine.Maximum };
        grenadeCollider.material = bounce;
        ExplosiveProjectile explosive = grenade.AddComponent<ExplosiveProjectile>();
        explosive.Configure(95f, 4.5f, 3.2f, false, this, false, true);
        CreateMuzzleFlash();
        ApplyRecoil(4.5f, 1f);
    }

    private void LaunchStickyBomb()
    {
        if (CurrentAmmo <= 0) { TryReload(); return; }
        SetCurrentAmmo(CurrentAmmo - 1);
        nextShotTime = Time.time + 0.55f;
        GameObject sticky = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sticky.name = "Stickybomb";
        sticky.transform.position = playerCamera.transform.position + playerCamera.transform.forward * 0.9f;
        sticky.transform.localScale = new Vector3(0.32f, 0.16f, 0.32f);
        Rigidbody body = sticky.AddComponent<Rigidbody>();
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = playerCamera.transform.forward * 15f + Vector3.up * 1.5f;
        ExplosiveProjectile explosive = sticky.AddComponent<ExplosiveProjectile>();
        explosive.Configure(105f, 4.8f, 45f, false, this, false, false, true);
        activeStickies.RemoveAll(item => item == null);
        activeStickies.Add(explosive);
        if (activeStickies.Count > 8) activeStickies[0].Detonate();
        CreateMuzzleFlash();
        ApplyRecoil(3.5f, 0.8f);
    }

    private void DetonateStickies()
    {
        activeStickies.RemoveAll(item => item == null);
        ExplosiveProjectile[] bombs = activeStickies.ToArray();
        activeStickies.Clear();
        foreach (ExplosiveProjectile bomb in bombs)
            if (bomb != null) bomb.Detonate();
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
        bool sniperShot = IsSniperRifleEquipped;
        float damage = sniperShot ? Mathf.Lerp(70f, 160f, sniperChargeRatio) : currentWeapon == WeaponType.Rifle ? 25f : currentWeapon == WeaponType.Handgun ? 18f : 70f;
        damage *= Random.Range(0.94f, 1.07f);
        if (aimedShot)
        {
            baseDelay *= 0.78f;
            damage *= 1.2f;
        }

        SetCurrentAmmo(CurrentAmmo - 1);
        nextShotTime = Time.time + baseDelay;
        currentModel.localPosition += Vector3.back * 0.055f;
        gunshotAudio.pitch = sniperShot ? Random.Range(0.72f, 0.78f) : currentWeapon == WeaponType.Rifle ? Random.Range(0.96f, 1.04f) : Random.Range(1.15f, 1.22f);
        gunshotAudio.PlayOneShot(gunshotClip, sniperShot ? 1f : currentWeapon == WeaponType.Rifle ? 0.7f : 0.55f);
        ApplyRecoil(sniperShot ? 7f : currentWeapon == WeaponType.Handgun ? 3.8f : 2f, 0.8f);

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        Vector3 tracerEnd = ray.GetPoint(range);
        if (Physics.Raycast(ray, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
        {
            tracerEnd = hit.point;
            if (hit.rigidbody != null)
                hit.rigidbody.AddForceAtPosition(ray.direction * hitForce, hit.point, ForceMode.Impulse);

            ApplyDamage(hit, damage, sniperShot, 0f, sniperShot);
            CreateBulletHole(hit.point, hit.normal, hit.transform);
        }

        CreateTracer(tracerEnd);
        CreateMuzzleFlash();
        if (CurrentAmmo == 0)
            TryReload();
    }

    private void SwingMelee(float meleeDamage = 45f, float meleeDelay = 0.65f, float meleeRange = 2.4f, float randomCritChance = 0f)
    {
        nextShotTime = Time.time + meleeDelay;
        currentModel.localRotation = Quaternion.Euler(0f, 0f, -55f);
        currentModel.localPosition = currentRestPosition + new Vector3(-0.18f, 0.08f, 0.18f);

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.SphereCast(ray, 0.35f, out RaycastHit hit, meleeRange, ~0, QueryTriggerInteraction.Ignore))
        {
            EngineerTurret turret = hit.collider.GetComponentInParent<EngineerTurret>();
            if (currentSlot == 2 && slotSelections[2] == 5 && turret != null)
            {
                turret.Repair(35f);
                return;
            }
            ApplyDamage(hit, meleeDamage, false, randomCritChance);
            CreateBulletHole(hit.point, hit.normal, hit.transform);
        }
    }

    private void ApplyDamage(RaycastHit hit, float baseDamage, bool allowHeadshotCritical = false, float randomCritChance = 0f, bool ignoresFalloff = false)
    {
        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
        if (damageable == null)
            return;

        bool critical = (allowHeadshotCritical && hit.collider.gameObject.name == "Head") || Random.value < randomCritChance;
        float falloff = critical || ignoresFalloff ? 1f : GetDamageFalloff(hit.distance);
        float finalDamage = (critical ? baseDamage * 3f : baseDamage) * falloff;
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
        int oldPrimary = rifleReserveAmmo;
        int oldSecondary = handgunReserveAmmo;
        int oldSpecial = sniperAmmo + sniperReserveAmmo;
        int[] primaryAdds = { 30, 2, 8, 100, 6, 8, 5 };
        int[] primaryCaps = { 180, 12, 64, 400, 48, 48, 24 };
        rifleReserveAmmo = Mathf.Min(primaryCaps[slotSelections[0]], rifleReserveAmmo + primaryAdds[slotSelections[0]]);

        if (slotSelections[1] == 1) handgunReserveAmmo = Mathf.Min(96, handgunReserveAmmo + 12);
        else if (slotSelections[1] == 2) handgunReserveAmmo = Mathf.Min(60, handgunReserveAmmo + 6);
        else if (slotSelections[1] == 4) handgunReserveAmmo = Mathf.Min(64, handgunReserveAmmo + 8);
        else if (slotSelections[1] == 6) handgunReserveAmmo = Mathf.Min(48, handgunReserveAmmo + 8);

        if (slotSelections[3] == 0) sniperReserveAmmo = Mathf.Min(24, sniperReserveAmmo + 5);
        else if (slotSelections[3] == 4)
            sniperAmmo = Mathf.Min(20, sniperAmmo + 10);
        else
        {
            int[] specialistAdds = { 0, 2, 2, 1, 0 };
            sniperAmmo = Mathf.Min(MaximumGrenades, sniperAmmo + specialistAdds[slotSelections[3]]);
        }

        return rifleReserveAmmo > oldPrimary || handgunReserveAmmo > oldSecondary || sniperAmmo + sniperReserveAmmo > oldSpecial;
    }

    public void RestoreSpawnAmmo()
    {
        int[] primaryReserves = { 90, 12, 32, 200, 24, 24, 24 };
        int[] secondaryReserves = { 0, 48, 30, 0, 32, 0, 24 };
        int[] specialistAmmo = { 1, 4, 3, 2, 10 };
        rifleAmmo = rifleMagazineSize;
        rifleReserveAmmo = primaryReserves[slotSelections[0]];
        handgunAmmo = slotSelections[1] == 0 ? 0 : handgunMagazineSize;
        handgunReserveAmmo = secondaryReserves[slotSelections[1]];
        sniperAmmo = specialistAmmo[slotSelections[3]];
        sniperReserveAmmo = slotSelections[3] == 0 ? 24 : 0;
        isReloading = false;
        reloadProgress = 0f;
    }

    private void TryReload()
    {
        bool reloadable = currentSlot == 0
            || (currentSlot == 1 && (slotSelections[1] == 1 || slotSelections[1] == 2 || slotSelections[1] == 4 || slotSelections[1] == 6))
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
        GameObject flare = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flare.name = "Muzzle Flare";
        flare.transform.SetParent(flash.transform, false);
        flare.transform.localScale = new Vector3(0.12f, 0.12f, 0.32f);
        flare.GetComponent<Renderer>().material = tracerMaterial;
        Destroy(flare.GetComponent<Collider>());
        Destroy(flash, 0.04f);
    }

    private void CreateBulletHole(Vector3 point, Vector3 normal, Transform hitTransform)
    {
        GameObject hole = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hole.name = "Bullet Hole";
        hole.transform.position = point + normal * 0.006f;
        hole.transform.rotation = Quaternion.LookRotation(normal);
        float impactSize = Random.Range(0.045f, 0.075f);
        hole.transform.localScale = new Vector3(impactSize, impactSize, 0.004f);
        hole.transform.Rotate(0f, 0f, Random.Range(0f, 360f), Space.Self);
        hole.GetComponent<Renderer>().material = bulletHoleMaterial;
        Destroy(hole.GetComponent<Collider>());
        if (hitTransform != null) hole.transform.SetParent(hitTransform, true);
        Destroy(hole, 20f);
    }

    private void OnGUI()
    {
        bool scoped = IsSniperRifleEquipped && IsAiming;
        if (scoped)
        {
            CreateScopeMask();
            DrawSniperScope();
        }
        GUIStyle centered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
        centered.normal.textColor = Color.white;
        if (!scoped) GUI.Label(new Rect(Screen.width * 0.5f - 15f, Screen.height * 0.5f - 15f, 30f, 30f), "+", centered);
        string weaponName = SlotWeaponNames[currentSlot][slotSelections[currentSlot]];
        bool hidesAmmo = currentSlot == 2 || (currentSlot == 1 && (slotSelections[1] == 0 || slotSelections[1] == 3 || slotSelections[1] == 5));
        string ammoText = hidesAmmo ? weaponName
            : currentWeapon == WeaponType.Sniper && slotSelections[3] != 0 ? $"{weaponName}  {sniperAmmo}"
            : isReloading ? $"{weaponName}  RELOADING..." : $"{weaponName}  {CurrentAmmo} / {CurrentReserve}";
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(Screen.width - 300f, Screen.height - 92f, 275f, 65f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(Screen.width - 290f, Screen.height - 84f, 255f, 40f), ammoText, centered);
        GUIStyle help = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 13 };
        help.normal.textColor = new Color(0.75f, 0.82f, 0.88f);
        GUI.Label(new Rect(Screen.width * 0.5f - 390f, 12f, 780f, 28f), $"{CurrentClass.ToString().ToUpper()}   [1] {GetLoadoutSlotName(0)}   [2] {GetLoadoutSlotName(1)}   [3] {GetLoadoutSlotName(2)}   [4] {GetLoadoutSlotName(3)}", help);

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

        if (currentSlot == 2 && slotSelections[2] == 3)
        {
            float ready = Mathf.Clamp01(1f - (nextDashTime - Time.time) / 2.2f);
            Rect dashBar = new Rect(Screen.width * 0.5f - 105f, Screen.height - 145f, 210f, 13f);
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(dashBar, Texture2D.whiteTexture);
            GUI.color = ready >= 1f ? new Color(0.35f, 1f, 0.35f) : new Color(0.35f, 0.75f, 1f);
            GUI.DrawTexture(new Rect(dashBar.x + 2f, dashBar.y + 2f, (dashBar.width - 4f) * ready, dashBar.height - 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(dashBar.x, dashBar.y - 23f, dashBar.width, 22f), ready >= 1f ? "DASH READY [RMB]" : "DASH RECHARGING", help);
        }

        if (currentSlot == 1 && slotSelections[1] == 5)
        {
            float ready = activeTurret != null ? 0f : Mathf.Clamp01(1f - (nextTurretTime - Time.time) / 10f);
            Rect turretBar = new Rect(Screen.width * 0.5f - 105f, Screen.height - 145f, 210f, 13f);
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(turretBar, Texture2D.whiteTexture);
            GUI.color = ready >= 1f ? new Color(0.2f, 1f, 0.65f) : new Color(0.15f, 0.6f, 0.9f);
            GUI.DrawTexture(new Rect(turretBar.x + 2f, turretBar.y + 2f, (turretBar.width - 4f) * ready, turretBar.height - 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            string turretState = activeTurret != null ? "TURRET ACTIVE" : ready >= 1f ? "TURRET READY [LMB]" : "TURRET COOLDOWN";
            GUI.Label(new Rect(turretBar.x, turretBar.y - 23f, turretBar.width, 22f), turretState, help);
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
        string label = lastHitWasCritical ? $"CRITICAL HIT!!!  -{Mathf.RoundToInt(lastDamageAmount)}" : $"-{Mathf.RoundToInt(lastDamageAmount)}";
        GUI.Label(new Rect(Screen.width * 0.5f - 100f, Screen.height * 0.5f + 24f, 200f, 28f), label, damage);
    }

    private static void DrawSniperScope()
    {
        float lensSize = Mathf.Min(Screen.width, Screen.height);
        float lensLeft = (Screen.width - lensSize) * 0.5f;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(lensLeft, 0f, lensSize, lensSize), scopeMaskTexture, ScaleMode.StretchToFill, true);
        GUI.color = Color.black;
        if (lensLeft > 0f)
        {
            GUI.DrawTexture(new Rect(0f, 0f, lensLeft, Screen.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(lensLeft + lensSize, 0f, lensLeft, Screen.height), Texture2D.whiteTexture);
        }
        float radius = Mathf.Min(Screen.width, Screen.height) * 0.46f;
        GUI.color = new Color(0f, 0f, 0f, 0.9f);
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - 1f, Screen.height * 0.5f - radius, 2f, radius * 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - radius, Screen.height * 0.5f - 1f, radius * 2f, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private static void CreateScopeMask()
    {
        if (scopeMaskTexture != null) return;
        const int size = 512;
        scopeMaskTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Round Scope Mask" };
        Color[] pixels = new Color[size * size];
        Vector2 center = Vector2.one * (size * 0.5f);
        float radius = size * 0.46f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float edge = Mathf.InverseLerp(radius - 3f, radius + 3f, Vector2.Distance(new Vector2(x, y), center));
            pixels[y * size + x] = new Color(0f, 0f, 0f, edge);
        }
        scopeMaskTexture.SetPixels(pixels);
        scopeMaskTexture.Apply(false, true);
    }
}
