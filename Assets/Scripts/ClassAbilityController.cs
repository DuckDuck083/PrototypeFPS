using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SimpleRifle))]
public sealed class ClassAbilityController : MonoBehaviour
{
    private SimpleRifle weapons;
    private float abilityReadyAt;
    private float abilityEndsAt;
    private float scanEndsAt;
    private float statusEndsAt;
    private string status = string.Empty;

    private void Awake() => weapons = GetComponent<SimpleRifle>();

    private void OnDisable() => ResetModifiers();

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (Time.time >= abilityEndsAt) ResetModifiers();
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            Activate();
    }

    private void Activate()
    {
        if (Time.time < abilityReadyAt) return;
        switch (weapons.CurrentClass)
        {
            case SimpleRifle.PlayerClass.Tank:
                abilityReadyAt = Time.time + 25f;
                abilityEndsAt = Time.time + 8f;
                weapons.ClassDamageTakenMultiplier = 0.52f;
                weapons.ClassMovementMultiplier = 0.72f;
                weapons.ClassDamageMultiplier = 1.2f;
                status = "HEAVY: HOLD THE LINE";
                break;
            case SimpleRifle.PlayerClass.Engineer:
                EngineerTurret turret = FindAnyObjectByType<EngineerTurret>();
                if (turret == null) { status = "DEPLOY A TURRET FIRST"; return; }
                turret.CycleMode();
                abilityReadyAt = Time.time + 3f;
                status = "TURRET MODE: " + turret.ModeName;
                break;
            case SimpleRifle.PlayerClass.Scout:
                abilityReadyAt = Time.time + 18f;
                abilityEndsAt = Time.time + 7f;
                weapons.ClassMovementMultiplier = 1.25f;
                weapons.ClassReloadMultiplier = 0.55f;
                weapons.ClassDamageMultiplier = 1.12f;
                status = "ASSAULT ADRENALINE";
                break;
            case SimpleRifle.PlayerClass.Sniper:
                abilityReadyAt = Time.time + 20f;
                scanEndsAt = Time.time + 8f;
                status = "RECON SCAN ACTIVE";
                break;
            case SimpleRifle.PlayerClass.Demoman:
                abilityReadyAt = Time.time + 22f;
                weapons.RestockExplosives();
                status = "DEMOLITION RESUPPLIED";
                break;
            case SimpleRifle.PlayerClass.SpecialForce:
                abilityReadyAt = Time.time + 24f;
                foreach (TrainingTarget target in FindObjectsByType<TrainingTarget>())
                    if (target.IsHostile && target.IsAlive && Vector3.Distance(transform.position, target.transform.position) <= 32f)
                        target.Stun(5f);
                status = "SPECIALIST EMP FIRED";
                break;
            default:
                abilityReadyAt = Time.time + 16f;
                abilityEndsAt = Time.time + 6f;
                weapons.ClassReloadMultiplier = 0.7f;
                status = "COMBAT FOCUS";
                break;
        }
        statusEndsAt = Time.time + 2.5f;
    }

    private void ResetModifiers()
    {
        if (weapons == null) return;
        weapons.ClassDamageMultiplier = 1f;
        weapons.ClassDamageTakenMultiplier = 1f;
        weapons.ClassMovementMultiplier = 1f;
        weapons.ClassReloadMultiplier = 1f;
        abilityEndsAt = float.MaxValue;
    }

    private void OnGUI()
    {
        if (weapons == null || Time.timeScale <= 0f) return;
        float remaining = Mathf.Max(0f, abilityReadyAt - Time.time);
        string label = remaining <= 0f ? "CLASS ABILITY [Q] READY" : $"CLASS ABILITY  {remaining:0.0}s";
        GUI.Label(new Rect(Screen.width - 275f, Screen.height - 72f, 250f, 25f), label,
            new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold });
        if (!string.IsNullOrEmpty(status) && Time.time < statusEndsAt)
            GUI.Label(new Rect(Screen.width * 0.5f - 180f, 165f, 360f, 28f), status,
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
        if (Time.time < scanEndsAt && Camera.main != null)
            DrawScanner();
    }

    private static void DrawScanner()
    {
        GUIStyle scan = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11, fontStyle = FontStyle.Bold };
        scan.normal.textColor = new Color(1f, 0.25f, 0.15f);
        foreach (TrainingTarget target in FindObjectsByType<TrainingTarget>())
        {
            if (!target.IsHostile || !target.IsAlive) continue;
            Vector3 screen = Camera.main.WorldToScreenPoint(target.transform.position + Vector3.up * 1.4f);
            if (screen.z > 0f)
                GUI.Label(new Rect(screen.x - 45f, Screen.height - screen.y - 12f, 90f, 24f), "◆ HOSTILE", scan);
        }
    }
}
