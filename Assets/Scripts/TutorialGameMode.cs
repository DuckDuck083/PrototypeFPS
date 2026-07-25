using UnityEngine;
using UnityEngine.InputSystem;

public sealed class TutorialGameMode : GameModeBase
{
    private static readonly TrainingTarget.EnemyArchetype[] Enemies =
    {
        TrainingTarget.EnemyArchetype.Normal, TrainingTarget.EnemyArchetype.Handgun, TrainingTarget.EnemyArchetype.Rifle,
        TrainingTarget.EnemyArchetype.Sniper, TrainingTarget.EnemyArchetype.Knife, TrainingTarget.EnemyArchetype.Demolition,
        TrainingTarget.EnemyArchetype.Tank, TrainingTarget.EnemyArchetype.Engineer, TrainingTarget.EnemyArchetype.Medic,
        TrainingTarget.EnemyArchetype.Pyro, TrainingTarget.EnemyArchetype.Scout, TrainingTarget.EnemyArchetype.Officer
    };
    private static readonly string[] Briefings =
    {
        "BASIC INFANTRY - closes distance and attacks in melee.",
        "HANDGUN - mobile ranged enemy. Use cover and aim down sights.",
        "RIFLEMAN - sustained automatic fire. Eliminate them quickly.",
        "SNIPER - powerful at long range. Break line of sight.",
        "KNIFE FIGHTER - extremely fast at close range. Keep moving.",
        "DEMOLITION - launches heavily damaging bombs with a large blast radius. Run from the impact marker.",
        "TANK - slow, extremely armored, and armed with a stronger minigun.",
        "ENGINEER - constructs tough hostile turrets. Destroy the builder and turret.",
        "MEDIC - heals injured enemies nearby. Prioritize medics first.",
        "PYRO - uses a powerful short-range flame stream. Stay outside flame range.",
        "SCOUT - extremely fast and carries a deadly close-range scattergun.",
        "OFFICER - weak rifle, but buffs nearby enemy speed and damage."
    };
    private static readonly string[] EquipmentNames = { "ASSAULT RIFLE", "HANDGUN", "BATON", "FRAG GRENADE", "MEDPACK", "AMMO PACK" };
    private static readonly string[] EquipmentInstructions =
    {
        "Slot 1 is your automatic primary. Fire several rounds with LEFT MOUSE, then press R to reload.",
        "Slot 2 is your accurate backup weapon. Press 2 and fire it with LEFT MOUSE.",
        "Slot 3 is your melee weapon. Press 3 and swing the baton with LEFT MOUSE.",
        "Slot 4 holds your grenade. Press 4, then hold and release LEFT MOUSE to throw it.",
        "Walk into the glowing green MEDPACK. It restores lost health.",
        "Walk into the glowing orange AMMO PACK. It restores ammunition for your weapons."
    };

    public override GameModeManager.Mode Type => GameModeManager.Mode.Tutorial;
    public override string Objective => controlStage ? "COMPLETE BASIC SOLDIER CONTROLS"
        : equipmentStage < EquipmentNames.Length ? $"EQUIPMENT {equipmentStage + 1}/{EquipmentNames.Length} - {EquipmentNames[equipmentStage]}"
        : enemyIndex < Enemies.Length ? $"ENEMY TRAINING {enemyIndex + 1}/{Enemies.Length} - {Enemies[enemyIndex].ToString().ToUpper()}"
        : "TRAINING COMPLETE";

    private bool controlStage;
    private bool moved;
    private bool jumped;
    private bool sprinted;
    private bool crouched;
    private bool fired;
    private bool switched;
    private bool looked;
    private bool aimed;
    private bool equipmentFired;
    private bool equipmentSelected;
    private int equipmentStage;
    private int enemyIndex;
    private float nextStageAt;
    private SimpleRifle weapons;
    private PlayerVitals vitals;
    private GameObject tutorialPickup;
    private Renderer tutorialPickupRenderer;

    public override void Begin(GameModeManager manager)
    {
        base.Begin(manager);
        Spawner.ClearEnemies();
        weapons = FindAnyObjectByType<SimpleRifle>();
        vitals = FindAnyObjectByType<PlayerVitals>();
        if (weapons != null)
        {
            weapons.SetPlayerClass(SimpleRifle.PlayerClass.Soldier);
            weapons.SetLoadoutSlot(0, 0);
            weapons.SetLoadoutSlot(1, 1);
            weapons.SetLoadoutSlot(2, 0);
            weapons.SetLoadoutSlot(3, 1);
            weapons.EquipLoadoutSlot(0);
        }
        controlStage = true;
        equipmentStage = -1;
        enemyIndex = 0;
    }

    public override void EndMode()
    {
        if (tutorialPickup != null) Destroy(tutorialPickup);
        base.EndMode();
    }

    private void Update()
    {
        if (controlStage)
        {
            UpdateControls();
            return;
        }
        if (equipmentStage < EquipmentNames.Length)
        {
            UpdateEquipmentTraining();
            return;
        }

        if (enemyIndex >= Enemies.Length)
        {
            Manager.Finish(true, "basic training complete");
            return;
        }
        if (LivingEnemies() == 0 && Time.time >= nextStageAt)
        {
            Spawn(Enemies[enemyIndex], new Vector3(0f, 0f, 27f), 0.7f, 0.35f);
            nextStageAt = float.PositiveInfinity;
        }
        else if (LivingEnemies() == 0 && float.IsPositiveInfinity(nextStageAt))
        {
            enemyIndex++;
            nextStageAt = Time.time + 1.4f;
        }
    }

    private void UpdateControls()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard != null)
        {
            moved |= keyboard.wKey.isPressed || keyboard.aKey.isPressed || keyboard.sKey.isPressed || keyboard.dKey.isPressed;
            jumped |= keyboard.spaceKey.wasPressedThisFrame;
            sprinted |= keyboard.leftShiftKey.isPressed;
            crouched |= keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed;
            switched |= keyboard.digit1Key.wasPressedThisFrame || keyboard.digit2Key.wasPressedThisFrame
                || keyboard.digit3Key.wasPressedThisFrame || keyboard.digit4Key.wasPressedThisFrame;
        }
        if (mouse != null)
        {
            fired |= mouse.leftButton.wasPressedThisFrame;
            aimed |= mouse.rightButton.wasPressedThisFrame;
            looked |= mouse.delta.ReadValue().sqrMagnitude > 2f;
        }
        if (!moved || !jumped || !sprinted || !crouched || !fired || !switched || !looked || !aimed) return;
        controlStage = false;
        equipmentStage = 0;
        equipmentFired = false;
        equipmentSelected = true;
        weapons?.EquipLoadoutSlot(0);
        nextStageAt = Time.time + 0.6f;
    }

    private void UpdateEquipmentTraining()
    {
        if (Time.time < nextStageAt) return;
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (equipmentStage == 0)
        {
            equipmentFired |= mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (equipmentFired && keyboard != null && keyboard.rKey.wasPressedThisFrame) AdvanceEquipment();
        }
        else if (equipmentStage < 4)
        {
            int slot = equipmentStage;
            bool selected = keyboard != null && (slot == 1 ? keyboard.digit2Key.wasPressedThisFrame
                : slot == 2 ? keyboard.digit3Key.wasPressedThisFrame : keyboard.digit4Key.wasPressedThisFrame);
            if (selected)
            {
                equipmentSelected = true;
                weapons?.EquipLoadoutSlot(slot);
            }
            equipmentFired |= mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (equipmentSelected && equipmentFired) AdvanceEquipment();
        }
        else if (tutorialPickupRenderer != null && !tutorialPickupRenderer.enabled)
            AdvanceEquipment();
    }

    private void AdvanceEquipment()
    {
        equipmentStage++;
        equipmentFired = false;
        equipmentSelected = false;
        nextStageAt = Time.time + 0.65f;
        if (tutorialPickup != null) Destroy(tutorialPickup);
        tutorialPickup = null;
        tutorialPickupRenderer = null;

        if (equipmentStage == 4)
        {
            if (vitals != null && vitals.Health > 35f) vitals.TakeDamage(30f);
            CreateTutorialPickup(ArenaPickup.PickupType.Health);
        }
        else if (equipmentStage == 5)
            CreateTutorialPickup(ArenaPickup.PickupType.Ammo);
        else if (equipmentStage >= EquipmentNames.Length)
        {
            weapons?.EquipLoadoutSlot(0);
            nextStageAt = Time.time + 1.2f;
        }
    }

    private void CreateTutorialPickup(ArenaPickup.PickupType type)
    {
        tutorialPickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tutorialPickup.name = type == ArenaPickup.PickupType.Health ? "Tutorial Medpack" : "Tutorial Ammo Pack";
        Transform player = vitals != null ? vitals.transform : weapons != null ? weapons.transform : transform;
        Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
        tutorialPickup.transform.position = player.position + forward * 3f + Vector3.up * 0.7f;
        tutorialPickup.transform.localScale = type == ArenaPickup.PickupType.Health
            ? new Vector3(0.9f, 0.55f, 0.9f) : new Vector3(0.7f, 0.7f, 0.7f);
        tutorialPickupRenderer = tutorialPickup.GetComponent<Renderer>();
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = type == ArenaPickup.PickupType.Health ? new Color(0.08f, 1f, 0.22f) : new Color(1f, 0.55f, 0.03f);
        material.SetFloat("_Emission", 1f);
        tutorialPickupRenderer.material = material;
        tutorialPickup.GetComponent<Collider>().isTrigger = true;
        Rigidbody body = tutorialPickup.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        tutorialPickup.AddComponent<ArenaPickup>().Configure(type);
    }

    private void OnGUI()
    {
        bool equipment = !controlStage && equipmentStage < EquipmentNames.Length;
        Rect panel = new Rect(18f, 175f, 430f, controlStage ? 285f : equipment ? 160f : 125f);
        GUI.color = new Color(0.015f, 0.035f, 0.055f, 0.94f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        title.normal.textColor = new Color(0.25f, 0.82f, 1f);
        string heading = controlStage ? "SOLDIER BASIC TRAINING" : equipment ? EquipmentNames[equipmentStage] : "THREAT BRIEFING";
        GUI.Label(new Rect(panel.x + 16f, panel.y + 12f, panel.width - 32f, 28f), heading, title);
        GUIStyle text = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
        text.normal.textColor = new Color(0.84f, 0.91f, 0.96f);
        if (controlStage)
        {
            string Check(bool done) => done ? "[X]" : "[ ]";
            GUI.Label(new Rect(panel.x + 16f, panel.y + 48f, panel.width - 32f, 225f),
                $"{Check(moved)} Move - WASD\n{Check(looked)} Look - MOVE MOUSE\n{Check(sprinted)} Sprint - LEFT SHIFT\n{Check(jumped)} Jump - SPACE\n{Check(crouched)} Crouch - CTRL or C\n{Check(fired)} Fire - LEFT MOUSE\n{Check(aimed)} Aim - RIGHT MOUSE\n{Check(switched)} Switch equipment - 1, 2, 3, 4\n\nR reloads | P pauses | ESC confirms quitting", text);
        }
        else if (equipment)
            GUI.Label(new Rect(panel.x + 16f, panel.y + 48f, panel.width - 32f, 100f), EquipmentInstructions[equipmentStage], text);
        else if (enemyIndex < Briefings.Length)
            GUI.Label(new Rect(panel.x + 16f, panel.y + 48f, panel.width - 32f, 65f), Briefings[enemyIndex], text);
    }
}
