using UnityEngine;

public sealed class TrainingTarget : MonoBehaviour, IDamageable
{
    [SerializeField] private bool followsPlayer;
    [SerializeField, Min(1f)] private float maximumHealth = 60f;
    [SerializeField, Min(0f)] private float moveSpeed = 2.3f;
    [SerializeField, Min(0f)] private float attackDamage = 5f;
    [SerializeField, Min(0.1f)] private float attackInterval = 1f;
    [SerializeField, Min(0f)] private float respawnDelay = 2.5f;
    [SerializeField] private bool usesRangedWeapon;
    [SerializeField] private bool usesRifle;

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

    public void ConfigureRanged(bool rifle)
    {
        usesRangedWeapon = true;
        usesRifle = rifle;
        attackInterval = rifle ? 0.32f : 0.85f;
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

        float desiredRange = usesRangedWeapon ? (usesRifle ? 14f : 10f) : 1.35f;
        if (distance > desiredRange)
        {
            Vector3 direction = offset.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 8f * Time.deltaTime);
            controller.Move(direction * moveSpeed * Time.deltaTime + Vector3.down * 2f * Time.deltaTime);
        }
        else if (Time.time >= nextAttackTime)
        {
            if (!usesRangedWeapon || HasLineOfSight())
            {
                player.TakeDamage(attackDamage);
                if (usesRangedWeapon) DrawEnemyTracer();
            }
            nextAttackTime = Time.time + attackInterval;
        }
    }

    private bool HasLineOfSight()
    {
        Vector3 start = transform.position + Vector3.up * 1.45f;
        Vector3 end = player.transform.position + Vector3.up * 1.2f;
        Vector3 direction = end - start;
        RaycastHit[] hits = Physics.RaycastAll(start, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.root == transform) continue;
            return hit.collider.GetComponentInParent<PlayerVitals>() != null;
        }
        return true;
    }

    private void DrawEnemyTracer()
    {
        GameObject tracer = new GameObject("Enemy Bullet Tracer");
        LineRenderer line = tracer.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = 0.018f;
        line.endWidth = 0.004f;
        line.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        line.startColor = new Color(0.35f, 1f, 0.3f, 0.9f);
        line.endColor = new Color(1f, 0.7f, 0.15f, 0.1f);
        line.SetPosition(0, transform.position + Vector3.up * 1.35f + transform.forward * 0.6f);
        line.SetPosition(1, player.transform.position + Vector3.up * 1.15f);
        Destroy(tracer, 0.08f);
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
