using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SimpleRifle))]
public sealed class ClassAbilityController : MonoBehaviour
{
    private SimpleRifle weapons;
    private float nextModeChangeTime;
    private float statusEndsAt;
    private string status = string.Empty;

    private void Awake() => weapons = GetComponent<SimpleRifle>();

    private void Update()
    {
        if (Time.timeScale <= 0f || weapons.CurrentClass != SimpleRifle.PlayerClass.Engineer
            || Keyboard.current == null || !Keyboard.current.qKey.wasPressedThisFrame
            || Time.time < nextModeChangeTime) return;

        EngineerTurret turret = FindAnyObjectByType<EngineerTurret>();
        if (turret == null)
        {
            status = "DEPLOY A TURRET FIRST";
            statusEndsAt = Time.time + 2.5f;
            return;
        }

        turret.CycleMode();
        nextModeChangeTime = Time.time + 3f;
        status = "TURRET: " + turret.ModeDescription;
        statusEndsAt = Time.time + 2.5f;
    }

    private void OnGUI()
    {
        if (weapons == null || Time.timeScale <= 0f || weapons.CurrentClass != SimpleRifle.PlayerClass.Engineer) return;
        GUI.Label(new Rect(Screen.width - 325f, Screen.height - 72f, 300f, 25f), "[Q] CHANGE TURRET MODE",
            new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold });
        if (!string.IsNullOrEmpty(status) && Time.time < statusEndsAt)
            GUI.Label(new Rect(Screen.width * 0.5f - 220f, 165f, 440f, 28f), status,
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
    }
}
