using UnityEngine;

public sealed class TrainingTarget : MonoBehaviour, IDamageable
{
    [SerializeField] private bool followsPlayer;
    [SerializeField, Min(1f)] private float maximumHealth = 60f;
    [SerializeField, Min(0f)] private float moveSpeed = 2.3f;
    [SerializeField, Min(0f)] private float attackDamage = 5f;
    [SerializeField, Min(0.1f)] private float attackInterval = 1f;
    [SerializeField, Min(0f)] private float respawnDelay = 2.5f;

    private PlayerVitals player;
    private CharacterController controller;
    private Renderer[] renderers;
    private Vector3 spawnPosition;
    private float health;
    private float nextAttackTime;
    private float respawnTime;
    private bool dead;

    public void Configure(bool shouldFollowPlayer, float healthAmount = 100f, float speed = 2.3f, float damage = 5f)
    {
        followsPlayer = shouldFollowPlayer;
        maximumHealth = healthAmount;
        moveSpeed = speed;
        attackDamage = damage;
        health = maximumHealth;
    }

    private void Awake()
    {
        player = FindAnyObjectByType<PlayerVitals>();
        controller = GetComponent<CharacterController>();
        renderers = GetComponentsInChildren<Renderer>();
        spawnPosition = transform.position;
        health = maximumHealth;
    }

    private void Update()
    {
        if (dead)
        {
            if (Time.time >= respawnTime)
                Respawn();
            return;
        }

        if (!followsPlayer || player == null)
            return;

        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;

        if (distance > 1.35f)
        {
            Vector3 direction = offset.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 8f * Time.deltaTime);
            controller.Move(direction * moveSpeed * Time.deltaTime + Vector3.down * 2f * Time.deltaTime);
        }
        else if (Time.time >= nextAttackTime)
        {
            player.TakeDamage(attackDamage);
            nextAttackTime = Time.time + attackInterval;
        }
    }

    public void TakeDamage(float amount)
    {
        if (dead)
            return;

        health -= amount;
        if (health <= 0f)
        {
            dead = true;
            respawnTime = Time.time + respawnDelay;
            controller.enabled = false;
            RemoveBulletHoles();
            foreach (Renderer targetRenderer in renderers)
                targetRenderer.enabled = false;
        }
    }

    private void RemoveBulletHoles()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != transform && child.name == "Bullet Hole")
                Destroy(child.gameObject);
        }
    }

    private void Respawn()
    {
        transform.position = spawnPosition;
        health = maximumHealth;
        dead = false;
        controller.enabled = true;
        foreach (Renderer targetRenderer in renderers)
            targetRenderer.enabled = true;
    }

    public void ResetToSpawn()
    {
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        transform.position = spawnPosition;
        health = maximumHealth;
        dead = false;
        respawnTime = 0f;
        controller.enabled = wasEnabled || followsPlayer;
        foreach (Renderer targetRenderer in renderers)
            targetRenderer.enabled = true;
    }

    private void OnGUI()
    {
        if (!followsPlayer || dead || player == null || Camera.main == null)
            return;

        Vector3 screenPoint = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.5f);
        if (screenPoint.z <= 0f)
            return;

        float distance = Vector3.Distance(Camera.main.transform.position, transform.position);
        if (distance > 45f)
            return;

        Vector3 visibilityStart = Camera.main.transform.position + Camera.main.transform.forward * 0.5f;
        if (Physics.Linecast(visibilityStart, transform.position + Vector3.up * 1.8f, out RaycastHit visibilityHit)
            && visibilityHit.transform.root != transform)
            return;

        float width = Mathf.Lerp(90f, 45f, distance / 45f);
        Rect background = new Rect(screenPoint.x - width * 0.5f, Screen.height - screenPoint.y, width, 8f);
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(background, Texture2D.whiteTexture);
        GUI.color = new Color(0.15f, 0.9f, 0.22f);
        GUI.DrawTexture(new Rect(background.x + 1f, background.y + 1f, (background.width - 2f) * Mathf.Clamp01(health / maximumHealth), background.height - 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
