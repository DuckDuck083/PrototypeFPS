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
        "BASIC INFANTRY — closes distance and attacks in melee.",
        "HANDGUN — mobile ranged enemy. Use cover and aim down sights.",
        "RIFLEMAN — sustained automatic fire. Eliminate them quickly.",
        "SNIPER — lethal at long range. Break line of sight.",
        "KNIFE FIGHTER — extremely fast at close range. Keep moving.",
        "DEMOLITION — launches visible arcing bombs. Move away before impact.",
        "TANK — slow, heavily armored, and armed with a minigun.",
        "ENGINEER — constructs hostile turrets. Destroy the builder and turret.",
        "MEDIC — heals injured enemies nearby. Prioritize medics first.",
        "PYRO — uses a short-range flame stream. Stay outside flame range.",
        "SCOUT — very fast and carries a close-range scattergun.",
        "OFFICER — weak rifle, but buffs nearby enemy speed and damage."
    };

    public override GameModeManager.Mode Type => GameModeManager.Mode.Tutorial;
    public override string Objective => controlStage ? "COMPLETE BASIC SOLDIER CONTROLS"
        : enemyIndex < Enemies.Length ? $"ENEMY TRAINING {enemyIndex + 1}/{Enemies.Length} — {Enemies[enemyIndex].ToString().ToUpper()}"
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
    private int enemyIndex;
    private float nextStageAt;

    public override void Begin(GameModeManager manager)
    {
        base.Begin(manager);
        Spawner.ClearEnemies();
        SimpleRifle rifle = FindAnyObjectByType<SimpleRifle>();
        if (rifle != null)
        {
            rifle.SetPlayerClass(SimpleRifle.PlayerClass.Soldier);
            rifle.SetLoadoutSlot(0, 0);
            rifle.SetLoadoutSlot(1, 1);
            rifle.SetLoadoutSlot(2, 0);
            rifle.SetLoadoutSlot(3, 1);
            rifle.EquipLoadoutSlot(0);
        }
        controlStage = true;
        enemyIndex = 0;
    }

    private void Update()
    {
        if (controlStage)
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
            if (moved && jumped && sprinted && crouched && fired && switched && looked && aimed)
            {
                controlStage = false;
                nextStageAt = Time.time + 1.2f;
            }
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

    private void OnGUI()
    {
        Rect panel = new Rect(18f, 175f, 430f, controlStage ? 285f : 125f);
        GUI.color = new Color(0.015f, 0.035f, 0.055f, 0.94f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        title.normal.textColor = new Color(0.25f, 0.82f, 1f);
        GUI.Label(new Rect(panel.x + 16f, panel.y + 12f, panel.width - 32f, 28f), controlStage ? "SOLDIER BASIC TRAINING" : "THREAT BRIEFING", title);
        GUIStyle text = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
        text.normal.textColor = new Color(0.84f, 0.91f, 0.96f);
        if (controlStage)
        {
            string Check(bool done) => done ? "✓" : "○";
            GUI.Label(new Rect(panel.x + 16f, panel.y + 48f, panel.width - 32f, 225f),
                $"{Check(moved)} Move — WASD\n{Check(looked)} Look — MOVE MOUSE\n{Check(sprinted)} Sprint — LEFT SHIFT\n{Check(jumped)} Jump — SPACE\n{Check(crouched)} Crouch — CTRL or C\n{Check(fired)} Fire — LEFT MOUSE\n{Check(aimed)} Aim — RIGHT MOUSE\n{Check(switched)} Switch equipment — 1, 2, 3, 4\n\nR reloads • P pauses • ESC confirms quitting", text);
        }
        else if (enemyIndex < Briefings.Length)
            GUI.Label(new Rect(panel.x + 16f, panel.y + 48f, panel.width - 32f, 65f), Briefings[enemyIndex], text);
    }
}
