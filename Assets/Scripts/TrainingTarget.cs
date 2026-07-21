using UnityEngine;

public sealed class TrainingTarget : MonoBehaviour, IDamageable
{
    public enum EnemyArchetype { Normal, Handgun, Rifle, Sniper, Knife, Demolition, Tank }
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
    public bool IsAlive => !dead;
    public bool IsHostile => followsPlayer;
    public bool IsWaveEnemy => waveManager != null;
    private WaveManager waveManager;
    private EnemyArchetype archetype;
    private int weaponAmmo;
    private int maximumWeaponAmmo;
    private EngineerTurret aggroTurret;
    private float turretThreat;
    private Vector3 lastProgressPosition;
    private float lastProgressTime;

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

    public void ConfigureWave(WaveManager manager, EnemyArchetype enemyType)
    {
        waveManager = manager;
        archetype = enemyType;
        usesRangedWeapon = enemyType == EnemyArchetype.Handgun || enemyType == EnemyArchetype.Rifle
            || enemyType == EnemyArchetype.Sniper || enemyType == EnemyArchetype.Demolition || enemyType == EnemyArchetype.Tank;
        usesRifle = enemyType == EnemyArchetype.Rifle || enemyType == EnemyArchetype.Tank;
        attackInterval = enemyType == EnemyArchetype.Sniper ? 2.5f
            : enemyType == EnemyArchetype.Demolition ? 1.7f
            : enemyType == EnemyArchetype.Tank ? 0.16f
            : enemyType == EnemyArchetype.Rifle ? 0.32f
            : enemyType == EnemyArchetype.Handgun ? 0.85f
            : enemyType == EnemyArchetype.Knife ? 0.55f
            : 1f;
        maximumWeaponAmmo = enemyType == EnemyArchetype.Handgun ? 12
            : enemyType == EnemyArchetype.Rifle ? 30
            : enemyType == EnemyArchetype.Sniper ? 1
            : enemyType == EnemyArchetype.Demolition ? 6
            : enemyType == EnemyArchetype.Tank ? 100
            : 0;
        weaponAmmo = maximumWeaponAmmo;
    }

    private void Awake()
    {
        player = FindAnyObjectByType<PlayerVitals>();
        controller = GetComponent<CharacterController>();
        renderers = GetComponentsInChildren<Renderer>();
        spawnPosition = transform.position;
        lastProgressPosition = transform.position;
        lastProgressTime = Time.time;
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

        if (aggroTurret == null) turretThreat = 0f;
        Transform attackTarget = aggroTurret != null ? aggroTurret.transform : player.transform;
        Vector3 offset = attackTarget.position - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;

        float desiredRange = archetype == EnemyArchetype.Sniper ? 30f
            : archetype == EnemyArchetype.Demolition ? 18f
            : archetype == EnemyArchetype.Tank ? 16f
            : usesRangedWeapon ? (usesRifle ? 14f : 10f)
            : archetype == EnemyArchetype.Knife ? 1.7f : 1.35f;
        if (distance > desiredRange)
        {
            Vector3 direction = GetSteeringDirection(offset.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 8f * Time.deltaTime);
            controller.Move(direction * moveSpeed * Time.deltaTime + Vector3.down * 2f * Time.deltaTime);
            RecoverIfStuck();
        }
        else if (Time.time >= nextAttackTime)
        {
            if (usesRangedWeapon && weaponAmmo <= 0)
            {
                weaponAmmo = maximumWeaponAmmo;
                nextAttackTime = Time.time + (archetype == EnemyArchetype.Tank ? 3.2f : 2f);
                return;
            }
            if (!usesRangedWeapon || HasLineOfSight(attackTarget))
            {
                if (aggroTurret != null)
                    aggroTurret.TakeDamage(attackDamage);
                else if (archetype == EnemyArchetype.Demolition)
                    player.TakeExplosiveDamage(attackDamage, transform.position);
                else
                    player.TakeDamage(attackDamage, transform.position);
                if (usesRangedWeapon) DrawEnemyTracer(attackTarget.position + Vector3.up);
                if (usesRangedWeapon) weaponAmmo--;
            }
            nextAttackTime = Time.time + attackInterval;
        }
    }

    private Vector3 GetSteeringDirection(Vector3 desired)
    {
        Vector3 origin = transform.position + Vector3.up;
        if (!Physics.SphereCast(origin, 0.38f, desired, out RaycastHit obstacle, 1.4f, ~0, QueryTriggerInteraction.Ignore)
            || obstacle.collider.transform.root == transform)
            return desired;
        Vector3 side = Vector3.Cross(Vector3.up, desired) * (GetInstanceID() % 2 == 0 ? 1f : -1f);
        return (side + desired * 0.25f).normalized;
    }

    private void RecoverIfStuck()
    {
        if (Vector3.Distance(transform.position, lastProgressPosition) > 0.45f)
        {
            lastProgressPosition = transform.position;
            lastProgressTime = Time.time;
            return;
        }
        if (waveManager == null || Time.time < lastProgressTime + 6f) return;
        Vector3 recovery = player.transform.position + Vector3.forward * 17f;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float angle = (GetInstanceID() * 37f + attempt * 41f) * Mathf.Deg2Rad;
            Vector3 candidate = player.transform.position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 17f;
            if (!Physics.CheckCapsule(candidate + Vector3.up * 0.7f, candidate + Vector3.up * 2f, 0.55f, ~0, QueryTriggerInteraction.Ignore))
            {
                recovery = candidate;
                break;
            }
        }
        controller.enabled = false;
        transform.position = recovery;
        controller.enabled = true;
        lastProgressPosition = recovery;
        lastProgressTime = Time.time;
    }

    private bool HasLineOfSight(Transform targetTransform)
    {
        Vector3 start = transform.position + Vector3.up * 1.45f;
        Vector3 end = targetTransform.position + Vector3.up;
        Vector3 direction = end - start;
        RaycastHit[] hits = Physics.RaycastAll(start, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.root == transform) continue;
            return targetTransform == player.transform
                ? hit.collider.GetComponentInParent<PlayerVitals>() != null
                : hit.collider.GetComponentInParent<EngineerTurret>() == aggroTurret;
        }
        return true;
    }

    private void DrawEnemyTracer(Vector3 targetPoint)
    {
        if (archetype == EnemyArchetype.Demolition)
        {
            GameObject blast = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            blast.name = "Enemy Grenade Blast";
            blast.transform.position = targetPoint;
            blast.transform.localScale = Vector3.one * 2.4f;
            Destroy(blast.GetComponent<Collider>());
            blast.GetComponent<Renderer>().material.color = new Color(1f, 0.2f, 0.03f);
            Destroy(blast, 0.12f);
        }
        GameObject tracer = new GameObject("Enemy Bullet Tracer");
        LineRenderer line = tracer.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = archetype == EnemyArchetype.Tank ? 0.035f : 0.018f;
        line.endWidth = 0.004f;
        line.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        line.startColor = new Color(0.35f, 1f, 0.3f, 0.9f);
        line.endColor = new Color(1f, 0.7f, 0.15f, 0.1f);
        line.SetPosition(0, transform.position + Vector3.up * 1.35f + transform.forward * 0.6f);
        line.SetPosition(1, targetPoint);
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
            if (waveManager != null)
                waveManager.NotifyEnemyDefeated(this);
        }
    }

    public void TakeDamageFromTurret(float amount, EngineerTurret turret)
    {
        turretThreat += amount;
        if (turretThreat >= 32f) aggroTurret = turret;
        TakeDamage(amount);
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
        if (waveManager != null)
        {
            Destroy(gameObject);
            return;
        }
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

        Ray aimRay = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        if (Physics.Raycast(aimRay, out RaycastHit aimedHit, 80f, ~0, QueryTriggerInteraction.Ignore)
            && aimedHit.collider.GetComponentInParent<TrainingTarget>() == this)
            DrawInspectionPanel();
    }

    private void DrawInspectionPanel()
    {
        string typeName = archetype == EnemyArchetype.Normal ? "NORMAL MELEE" : archetype.ToString().ToUpper();
        string ammoText = usesRangedWeapon ? $"AMMO  {weaponAmmo} / {maximumWeaponAmmo}" : "AMMO  N/A";
        Rect panel = new Rect(Screen.width * 0.5f + 34f, Screen.height * 0.5f - 62f, 190f, 74f);
        GUI.color = new Color(0.02f, 0.025f, 0.03f, 0.9f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
        title.normal.textColor = new Color(1f, 0.35f, 0.2f);
        GUI.Label(new Rect(panel.x + 9f, panel.y + 5f, 172f, 22f), typeName, title);
        GUIStyle info = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        info.normal.textColor = Color.white;
        GUI.Label(new Rect(panel.x + 9f, panel.y + 28f, 172f, 20f), $"HEALTH  {Mathf.CeilToInt(health)} / {Mathf.CeilToInt(maximumHealth)}", info);
        GUI.Label(new Rect(panel.x + 9f, panel.y + 48f, 172f, 20f), ammoText, info);
    }
}
