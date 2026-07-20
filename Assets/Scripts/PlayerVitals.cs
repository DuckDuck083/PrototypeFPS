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
    private float damageFlash;
    private float invulnerableUntil;
    private float respawnAt;
    private bool isDead;
    private Vector3 spawnPosition;

    private void Awake()
    {
        Health = maximumHealth;
        Stamina = maximumStamina;
        spawnPosition = transform.position;
    }

    private void Update()
    {
        if (isDead)
        {
            if (Time.time >= respawnAt)
                RespawnPlayer();
            return;
        }

        if (Time.time >= lastStaminaUseTime + recoveryDelay)
            Stamina = Mathf.MoveTowards(Stamina, maximumStamina, staminaRecoveryPerSecond * Time.deltaTime);

        damageFlash = Mathf.MoveTowards(damageFlash, 0f, 1.8f * Time.deltaTime);
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
        if (isDead || Time.time < invulnerableUntil)
            return;

        SimpleRifle weapons = GetComponent<SimpleRifle>();
        if (weapons != null && weapons.IsShieldBlocking)
            amount *= 0.2f;

        Health = Mathf.Max(0f, Health - amount);
        damageFlash = Mathf.Clamp01(damageFlash + amount / 35f);
        if (Health <= 0f)
            BeginRespawn();
    }

    public bool Heal(float amount)
    {
        if (Health >= maximumHealth)
            return false;

        Health = Mathf.Min(maximumHealth, Health + amount);
        return true;
    }

    private void BeginRespawn()
    {
        isDead = true;
        respawnAt = Time.time + 5f;
        GetComponent<FirstPersonController>().enabled = false;
        GetComponent<SimpleRifle>().enabled = false;
    }

    private void RespawnPlayer()
    {
        CharacterController controller = GetComponent<CharacterController>();
        controller.enabled = false;
        transform.position = spawnPosition;
        controller.enabled = true;
        Health = maximumHealth;
        Stamina = maximumStamina;
        isDead = false;
        invulnerableUntil = Time.time + 3f;

        TrainingTarget[] targets = FindObjectsByType<TrainingTarget>();
        foreach (TrainingTarget target in targets)
            target.ResetToSpawn();

        GetComponent<FirstPersonController>().enabled = true;
        SimpleRifle weapons = GetComponent<SimpleRifle>();
        weapons.RestoreSpawnAmmo();
        weapons.enabled = true;
    }

    private void OnGUI()
    {
        if (isDead)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUIStyle respawnStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 34, fontStyle = FontStyle.Bold };
            respawnStyle.normal.textColor = new Color(0.85f, 0.9f, 1f);
            float remaining = Mathf.Max(0f, respawnAt - Time.time);
            GUI.Label(new Rect(Screen.width * 0.5f - 250f, Screen.height * 0.5f - 50f, 500f, 100f), $"RESPAWNING IN {Mathf.CeilToInt(remaining)}", respawnStyle);
            return;
        }

        if (damageFlash > 0f)
        {
            GUI.color = new Color(0.75f, 0f, 0f, damageFlash * 0.22f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        GUI.color = new Color(0f, 0f, 0f, 0.68f);
        GUI.DrawTexture(new Rect(15f, Screen.height - 92f, 245f, 72f), Texture2D.whiteTexture);
        GUI.color = Color.white;
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
