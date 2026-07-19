using UnityEngine;

public sealed class PlayerVitals : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1f)] private float maximumHealth = 100f;
    [SerializeField, Min(1f)] private float maximumStamina = 100f;
    [SerializeField, Min(0f)] private float staminaDrainPerSecond = 24f;
    [SerializeField, Min(0f)] private float staminaRecoveryPerSecond = 18f;
    [SerializeField, Min(0f)] private float recoveryDelay = 0.8f;

    public float Health { get; private set; }
    public float Stamina { get; private set; }
    public bool CanSprint => Stamina > 0.1f;

    private float lastStaminaUseTime;
    private Vector3 spawnPosition;

    private void Awake()
    {
        Health = maximumHealth;
        Stamina = maximumStamina;
        spawnPosition = transform.position;
    }

    private void Update()
    {
        if (Time.time >= lastStaminaUseTime + recoveryDelay)
            Stamina = Mathf.MoveTowards(Stamina, maximumStamina, staminaRecoveryPerSecond * Time.deltaTime);
    }

    public bool UseSprintStamina()
    {
        if (!CanSprint)
            return false;

        Stamina = Mathf.Max(0f, Stamina - staminaDrainPerSecond * Time.deltaTime);
        lastStaminaUseTime = Time.time;
        return true;
    }

    public void TakeDamage(float amount)
    {
        Health = Mathf.Max(0f, Health - amount);
        if (Health <= 0f)
            RespawnPlayer();
    }

    private void RespawnPlayer()
    {
        CharacterController controller = GetComponent<CharacterController>();
        controller.enabled = false;
        transform.position = spawnPosition;
        controller.enabled = true;
        Health = maximumHealth;
        Stamina = maximumStamina;
    }

    private void OnGUI()
    {
        DrawBar(new Rect(25f, Screen.height - 75f, 220f, 20f), Health / maximumHealth, new Color(0.8f, 0.12f, 0.12f), $"HEALTH  {Mathf.CeilToInt(Health)}");
        DrawBar(new Rect(25f, Screen.height - 45f, 220f, 16f), Stamina / maximumStamina, new Color(0.15f, 0.7f, 0.25f), $"STAMINA  {Mathf.CeilToInt(Stamina)}");
    }

    private static void DrawBar(Rect rect, float fill, Color color, string label)
    {
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * Mathf.Clamp01(fill), rect.height - 4f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(rect, label, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
        GUI.color = oldColor;
    }
}
