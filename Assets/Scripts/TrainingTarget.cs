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
    private Transform healthBarFill;
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
        healthBarFill = transform.Find("Health Bar/Fill");
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
        UpdateHealthBar();
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

    private void UpdateHealthBar()
    {
        if (healthBarFill == null)
            return;

        float ratio = Mathf.Clamp01(health / maximumHealth);
        healthBarFill.localScale = new Vector3(0.82f * ratio, 0.08f, 0.08f);
        healthBarFill.localPosition = new Vector3(-0.41f * (1f - ratio), 0f, -0.06f);
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
        UpdateHealthBar();
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
        UpdateHealthBar();
        foreach (Renderer targetRenderer in renderers)
            targetRenderer.enabled = true;
    }
}
