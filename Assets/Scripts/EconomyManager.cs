using UnityEngine;

public sealed class EconomyManager : MonoBehaviour
{
    private const string MoneyKey = "PrototypeFPS.Money";
    public static EconomyManager Instance { get; private set; }
    public int Money { get; private set; }
    public bool DevModeUnlocked => PlayerPrefs.GetInt("PrototypeFPS.DevMode", 0) == 1;
    public string LastNotification { get; private set; }
    private float notificationUntil;

    public static readonly string[] PerkNames = { "SPRINTER", "ARMOR PLATING", "HIGH CALIBER", "FIELD MEDIC", "MARATHON", "SCAVENGER", "BOUNTY CHIP", "SECOND WIND" };
    public static readonly string[] PerkDescriptions =
    {
        "+15% movement speed", "15% less incoming damage", "+12% weapon damage", "Regenerate 1 health per second",
        "Stamina drains 25% slower", "+25% ammo from pickups", "+20% enemy bounty credits", "+20 maximum health"
    };
    public static readonly int[] PerkPrices = { 650, 800, 900, 750, 700, 725, 1000, 850 };
    public static readonly int[] ModePrices = { 0, 0, 900, 1100, 1600, 1300 };
    public static readonly int[] ClassPrices = { 0, 700, 750, 800, 850, 1100, 1200 };
    public static readonly string[] LootNames = { "FIELD MEDKIT", "AMMO SATCHEL", "TRAUMA PLATE", "ADRENALINE" };
    public static readonly string[] LootDescriptions = { "Start Hardcore with +25 health", "Start with 35% more reserve ammo", "Start with +20 max health", "Start with full stamina and brief regeneration" };
    public static readonly int[] LootPrices = { 300, 450, 600, 500 };
    private bool matchActive;
    private int pendingCredits;

    private readonly string[] questNames = { "ELITE HUNTER", "MATCH WINNER", "TANK BUSTER", "WAR HERO" };
    private readonly string[] questDescriptions = { "Defeat 20 armed or elite enemies", "Win 3 matches", "Destroy 5 tank enemies", "Complete 6 campaign missions" };
    private readonly int[] questGoals = { 20, 3, 5, 6 };
    private readonly int[] questRewards = { 500, 750, 600, 1200 };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Money = PlayerPrefs.GetInt(MoneyKey, 250);
    }

    private void Start() => ApplyPerks();

    public bool IsModeUnlocked(int mode) => mode <= 1 || PlayerPrefs.GetInt($"PrototypeFPS.Unlock.Mode.{mode}", 0) == 1;
    public bool IsClassUnlocked(int playerClass) => playerClass == 0 || PlayerPrefs.GetInt($"PrototypeFPS.Unlock.Class.{playerClass}", 0) == 1;
    public bool IsWeaponUnlocked(int slot, int weapon) => IsStockWeapon(slot, weapon) || PlayerPrefs.GetInt($"PrototypeFPS.Unlock.Weapon.{slot}.{weapon}", 0) == 1;
    public bool IsPerkUnlocked(int perk) => PlayerPrefs.GetInt($"PrototypeFPS.Unlock.Perk.{perk}", 0) == 1;
    public bool IsLootUnlocked(int loot) => PlayerPrefs.GetInt($"PrototypeFPS.Unlock.Loot.{loot}", 0) == 1;
    public int WeaponPrice(int slot, int weapon) => 250 + slot * 60 + weapon * 55;

    private static bool IsStockWeapon(int slot, int weapon)
        => (slot == 0 && weapon == 0) || (slot == 1 && weapon == 1) || (slot == 2 && weapon == 0) || (slot == 3 && weapon == 1);

    public bool BuyMode(int mode) => Buy($"PrototypeFPS.Unlock.Mode.{mode}", ModePrices[mode], $"MODE UNLOCKED: {(GameModeManager.Mode)mode}", () => IsModeUnlocked(mode));
    public bool BuyClass(int playerClass) => Buy($"PrototypeFPS.Unlock.Class.{playerClass}", ClassPrices[playerClass], $"CLASS UNLOCKED: {(SimpleRifle.PlayerClass)playerClass}", () => IsClassUnlocked(playerClass));
    public bool BuyWeapon(int slot, int weapon, string name) => Buy($"PrototypeFPS.Unlock.Weapon.{slot}.{weapon}", WeaponPrice(slot, weapon), $"WEAPON UNLOCKED: {name}", () => IsWeaponUnlocked(slot, weapon));
    public bool BuyPerk(int perk) => Buy($"PrototypeFPS.Unlock.Perk.{perk}", PerkPrices[perk], $"PERK ACQUIRED: {PerkNames[perk]}", () => IsPerkUnlocked(perk), ApplyPerks);
    public bool BuyLoot(int loot) => Buy($"PrototypeFPS.Unlock.Loot.{loot}", LootPrices[loot], $"HARDCORE LOOT ACQUIRED: {LootNames[loot]}", () => IsLootUnlocked(loot));

    public void BeginMatch() { matchActive = true; pendingCredits = 0; }
    public void SettleMatch(float multiplier)
    {
        int payout = Mathf.RoundToInt(pendingCredits * Mathf.Clamp01(multiplier));
        matchActive = false;
        pendingCredits = 0;
        if (payout > 0) AddMoney(payout, $"+{payout} MATCH PAYOUT");
    }

    public string RedeemPromoCode(string code)
    {
        string normalized = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized != "mustardmango") return "INVALID CODE";
        if (DevModeUnlocked) return "DEV MODE ALREADY ACTIVE";
        PlayerPrefs.SetInt("PrototypeFPS.DevMode", 1);
        PlayerPrefs.Save();
        Notify("DEVELOPER ACCESS GRANTED");
        return "ACCESS GRANTED — ADMIN PANEL UNLOCKED";
    }

    public void AdminGrantMoney(int amount) => AddMoney(Mathf.Max(0, amount), $"ADMIN: +{amount:N0} CREDITS");

    public void AdminUnlockAll()
    {
        for (int i = 0; i < ModePrices.Length; i++) PlayerPrefs.SetInt($"PrototypeFPS.Unlock.Mode.{i}", 1);
        for (int i = 0; i < ClassPrices.Length; i++) PlayerPrefs.SetInt($"PrototypeFPS.Unlock.Class.{i}", 1);
        int[] weaponCounts = { 13, 11, 9, 9 };
        for (int slot = 0; slot < weaponCounts.Length; slot++)
        for (int weapon = 0; weapon < weaponCounts[slot]; weapon++)
            PlayerPrefs.SetInt($"PrototypeFPS.Unlock.Weapon.{slot}.{weapon}", 1);
        for (int i = 0; i < PerkPrices.Length; i++) PlayerPrefs.SetInt($"PrototypeFPS.Unlock.Perk.{i}", 1);
        for (int i = 0; i < LootPrices.Length; i++) PlayerPrefs.SetInt($"PrototypeFPS.Unlock.Loot.{i}", 1);
        PlayerPrefs.Save();
        ApplyPerks();
        Notify("ADMIN: ALL CONTENT UNLOCKED");
    }

    public void ResetAllProgress()
    {
        matchActive = false;
        pendingCredits = 0;
        PlayerPrefs.DeleteKey(MoneyKey);
        PlayerPrefs.DeleteKey("PrototypeFPS.DevMode");
        PlayerPrefs.DeleteKey("PrototypeFPS.CurrentWave");
        for (int i = 0; i < ModePrices.Length; i++) PlayerPrefs.DeleteKey($"PrototypeFPS.Unlock.Mode.{i}");
        for (int i = 0; i < ClassPrices.Length; i++) PlayerPrefs.DeleteKey($"PrototypeFPS.Unlock.Class.{i}");
        int[] weaponCounts = { 13, 11, 9, 9 };
        for (int slot = 0; slot < weaponCounts.Length; slot++)
        for (int weapon = 0; weapon < weaponCounts[slot]; weapon++)
            PlayerPrefs.DeleteKey($"PrototypeFPS.Unlock.Weapon.{slot}.{weapon}");
        for (int i = 0; i < PerkPrices.Length; i++) PlayerPrefs.DeleteKey($"PrototypeFPS.Unlock.Perk.{i}");
        for (int i = 0; i < LootPrices.Length; i++) PlayerPrefs.DeleteKey($"PrototypeFPS.Unlock.Loot.{i}");
        for (int i = 0; i < questNames.Length; i++)
        {
            PlayerPrefs.DeleteKey($"PrototypeFPS.Quest.{i}.Progress");
            PlayerPrefs.DeleteKey($"PrototypeFPS.Quest.{i}.Claimed");
        }
        Money = 250;
        PlayerPrefs.SetInt(MoneyKey, Money);
        PlayerPrefs.Save();
        ApplyPerks();
        Notify("ALL PROGRESS RESET");
    }

    private bool Buy(string key, int price, string success, System.Func<bool> owned, System.Action afterPurchase = null)
    {
        if (owned()) return false;
        if (Money < price) { Notify("NOT ENOUGH CREDITS"); return false; }
        Money -= price;
        PlayerPrefs.SetInt(MoneyKey, Money);
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        afterPurchase?.Invoke();
        Notify(success);
        return true;
    }

    public void RewardEnemy(TrainingTarget.EnemyArchetype type)
    {
        int reward = type == TrainingTarget.EnemyArchetype.Tank ? 45
            : type == TrainingTarget.EnemyArchetype.Sniper || type == TrainingTarget.EnemyArchetype.Demolition ? 25
            : type == TrainingTarget.EnemyArchetype.Rifle ? 15
            : type == TrainingTarget.EnemyArchetype.Handgun || type == TrainingTarget.EnemyArchetype.Knife ? 8 : 3;
        if (IsPerkUnlocked(6)) reward = Mathf.RoundToInt(reward * 1.2f);
        AwardMatchCredits(reward, $"+{reward} ENEMY BOUNTY");
        if (type != TrainingTarget.EnemyArchetype.Normal) AddQuestProgress(0, 1);
        if (type == TrainingTarget.EnemyArchetype.Tank) AddQuestProgress(2, 1);
    }

    public void RewardVictory(GameModeManager.Mode mode)
    {
        int reward = 300 + (int)mode * 100;
        AwardMatchCredits(reward, $"+{reward} MATCH VICTORY");
        AddQuestProgress(1, 1);
    }

    public void NotifyMissionCompleted()
    {
        AwardMatchCredits(100, "+100 MISSION COMPLETE");
        AddQuestProgress(3, 1);
    }

    public int QuestCount => questNames.Length;
    public string QuestName(int index) => questNames[index];
    public string QuestDescription(int index) => questDescriptions[index];
    public int QuestGoal(int index) => questGoals[index];
    public int QuestReward(int index) => questRewards[index];
    public int QuestProgress(int index) => Mathf.Min(questGoals[index], PlayerPrefs.GetInt($"PrototypeFPS.Quest.{index}.Progress", 0));
    public bool IsQuestClaimed(int index) => PlayerPrefs.GetInt($"PrototypeFPS.Quest.{index}.Claimed", 0) == 1;

    public void ClaimQuest(int index)
    {
        if (IsQuestClaimed(index) || QuestProgress(index) < QuestGoal(index)) return;
        PlayerPrefs.SetInt($"PrototypeFPS.Quest.{index}.Claimed", 1);
        AddMoney(questRewards[index], $"+{questRewards[index]} QUEST REWARD");
    }

    private void AddQuestProgress(int index, int amount)
    {
        if (IsQuestClaimed(index)) return;
        string key = $"PrototypeFPS.Quest.{index}.Progress";
        PlayerPrefs.SetInt(key, Mathf.Min(questGoals[index], PlayerPrefs.GetInt(key, 0) + amount));
        PlayerPrefs.Save();
    }

    private void AddMoney(int amount, string message)
    {
        Money += amount;
        PlayerPrefs.SetInt(MoneyKey, Money);
        PlayerPrefs.Save();
        Notify(message);
    }

    private void AwardMatchCredits(int amount, string message)
    {
        if (matchActive) { pendingCredits += amount; Notify(message); }
        else AddMoney(amount, message);
    }

    private void Notify(string message)
    {
        LastNotification = message;
        notificationUntil = Time.unscaledTime + 2.5f;
    }

    public void ApplyPerks()
    {
        FirstPersonController movement = FindAnyObjectByType<FirstPersonController>();
        if (movement != null) movement.PerkSpeedMultiplier = IsPerkUnlocked(0) ? 1.15f : 1f;
        PlayerVitals vitals = FindAnyObjectByType<PlayerVitals>();
        if (vitals != null)
        {
            vitals.PerkDamageReduction = IsPerkUnlocked(1) ? 0.15f : 0f;
            vitals.PerkRegeneration = IsPerkUnlocked(3) ? 1f : 0f;
            vitals.PerkStaminaMultiplier = IsPerkUnlocked(4) ? 0.75f : 1f;
            vitals.PerkBonusHealth = IsPerkUnlocked(7) ? 20f : 0f;
        }
        SimpleRifle rifle = FindAnyObjectByType<SimpleRifle>();
        if (rifle != null) { rifle.PerkDamageMultiplier = IsPerkUnlocked(2) ? 1.12f : 1f; rifle.PerkAmmoPickupMultiplier = IsPerkUnlocked(5) ? 1.25f : 1f; }
    }

    private void OnGUI()
    {
        GUI.color = new Color(0.02f, 0.03f, 0.045f, 0.88f);
        GUI.DrawTexture(new Rect(Screen.width - 190f, Screen.height - 62f, 170f, 42f), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.78f, 0.18f);
        GUIStyle moneyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
        moneyStyle.normal.textColor = GUI.color;
        GUI.Label(new Rect(Screen.width - 185f, Screen.height - 58f, 160f, 34f), $"◆ {Money:N0} CREDITS", moneyStyle);
        if (Time.unscaledTime < notificationUntil)
        {
            GUIStyle notice = new GUIStyle(moneyStyle) { fontSize = 16 };
            GUI.Label(new Rect(Screen.width * 0.5f - 240f, Screen.height - 100f, 480f, 36f), LastNotification, notice);
        }
        GUI.color = Color.white;
    }
}
