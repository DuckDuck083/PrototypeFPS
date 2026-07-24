using UnityEngine;

public sealed class TrainingTarget : MonoBehaviour, IDamageable
{
    public enum EnemyArchetype { Normal, Handgun, Rifle, Sniper, Knife, Demolition, Tank, Engineer, Medic, Pyro, Scout, Officer }
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
    private float steeringSign;
    private float recoveryAngleOffset;
    private float stunnedUntil;
    private float poisonUntil;
    private float nextPoisonTick;
    private float poisonDamage;
    private bool guardsPost;
    private Vector3 guardPost;
    private bool destroyAfterDefeat;
    private DestructibleObjective attackObjective;
    private float verticalVelocity;
    private float nextAbilityTime;
    private float officerBuffUntil;
    private EnemyTurret engineerTurret;
    public float HealthRatio => maximumHealth <= 0f ? 0f : health / maximumHealth;

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
        destroyAfterDefeat = true;
        archetype = enemyType;
        usesRangedWeapon = enemyType == EnemyArchetype.Handgun || enemyType == EnemyArchetype.Rifle
            || enemyType == EnemyArchetype.Sniper || enemyType == EnemyArchetype.Demolition || enemyType == EnemyArchetype.Tank
            || enemyType == EnemyArchetype.Engineer || enemyType == EnemyArchetype.Medic || enemyType == EnemyArchetype.Pyro || enemyType == EnemyArchetype.Officer || enemyType == EnemyArchetype.Scout;
        usesRifle = enemyType == EnemyArchetype.Rifle || enemyType == EnemyArchetype.Tank || enemyType == EnemyArchetype.Officer;
        attackInterval = enemyType == EnemyArchetype.Sniper ? 2.5f
            : enemyType == EnemyArchetype.Demolition ? 1.7f
            : enemyType == EnemyArchetype.Tank ? 0.16f
            : enemyType == EnemyArchetype.Rifle ? 0.32f
            : enemyType == EnemyArchetype.Handgun ? 0.85f
            : enemyType == EnemyArchetype.Knife ? 0.55f
            : enemyType == EnemyArchetype.Pyro ? 0.16f
            : enemyType == EnemyArchetype.Scout ? 0.42f
            : enemyType == EnemyArchetype.Officer ? 0.7f
            : 1f;
        maximumWeaponAmmo = enemyType == EnemyArchetype.Handgun ? 12
            : enemyType == EnemyArchetype.Rifle ? 30
            : enemyType == EnemyArchetype.Sniper ? 1
            : enemyType == EnemyArchetype.Demolition ? 6
            : enemyType == EnemyArchetype.Tank ? 100
            : enemyType == EnemyArchetype.Engineer || enemyType == EnemyArchetype.Medic || enemyType == EnemyArchetype.Officer ? 20
            : enemyType == EnemyArchetype.Pyro ? 80
            : enemyType == EnemyArchetype.Scout ? 2
            : 0;
        weaponAmmo = maximumWeaponAmmo;
    }

    public void ConfigureGuardPost(Vector3 position)
    {
        guardsPost = true;
        guardPost = position;
    }

    public void ConfigureAttackObjective(DestructibleObjective objective)
    {
        attackObjective = objective;
    }

    private void Awake()
    {
        player = FindAnyObjectByType<PlayerVitals>();
        controller = GetComponent<CharacterController>();
        renderers = GetComponentsInChildren<Renderer>();
        spawnPosition = transform.position;
        lastProgressPosition = transform.position;
        lastProgressTime = Time.time;
        steeringSign = Random.value < 0.5f ? -1f : 1f;
        recoveryAngleOffset = Random.Range(0f, 360f);
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

        if (Time.time < poisonUntil && Time.time >= nextPoisonTick)
        {
            nextPoisonTick = Time.time + 1f;
            TakeDamage(poisonDamage);
        }
        if (Time.time < stunnedUntil) return;

        UpdateSupportAbility();

        if (!followsPlayer || player == null)
            return;

        if (aggroTurret == null) turretThreat = 0f;
        Transform attackTarget = aggroTurret != null ? aggroTurret.transform
            : attackObjective != null && !attackObjective.IsDestroyed ? attackObjective.transform
            : player.transform;
        Vector3 offset = attackTarget.position - transform.position;
        offset.y = 0f;
        float distance = offset.magnitude;

        float desiredRange = archetype == EnemyArchetype.Sniper ? 30f
            : archetype == EnemyArchetype.Demolition ? 18f
            : archetype == EnemyArchetype.Tank ? 16f
            : archetype == EnemyArchetype.Pyro ? 6f
            : archetype == EnemyArchetype.Scout ? 6f
            : archetype == EnemyArchetype.Officer ? 13f
            : usesRangedWeapon ? (usesRifle ? 14f : 10f)
            : archetype == EnemyArchetype.Knife ? 1.7f : 1.35f;
        if (distance > desiredRange)
        {
            if (guardsPost)
            {
                Vector3 returnOffset = guardPost - transform.position;
                returnOffset.y = 0f;
                if (returnOffset.magnitude > 1f)
                {
                    Vector3 returnDirection = GetSteeringDirection(returnOffset.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(returnDirection), 8f * Time.deltaTime);
                    MoveCharacter(returnDirection);
                }
                else if (offset.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(offset.normalized), 5f * Time.deltaTime);
                return;
            }
            Vector3 direction = GetSteeringDirection(offset.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 8f * Time.deltaTime);
            MoveCharacter(direction);
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
                if (archetype == EnemyArchetype.Demolition)
                    LaunchBomb(attackTarget);
                else if (aggroTurret != null)
                    aggroTurret.TakeDamage(attackDamage);
                else if (attackObjective != null && !attackObjective.IsDestroyed)
                    attackObjective.TakeDamage(attackDamage);
                else
                    player.TakeDamage(attackDamage * (Time.time < officerBuffUntil ? 1.25f : 1f), transform.position);
                if (usesRangedWeapon && archetype != EnemyArchetype.Demolition) DrawEnemyTracer(attackTarget.position + Vector3.up);
                if (usesRangedWeapon) weaponAmmo--;
            }
            nextAttackTime = Time.time + attackInterval;
        }
    }

    private void LaunchBomb(Transform target)
    {
        GameObject bomb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bomb.name = "Enemy Demolition Bomb";
        bomb.transform.position = transform.position + Vector3.up * 1.35f + transform.forward * 0.6f;
        bomb.transform.localScale = Vector3.one * 0.34f;
        bomb.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 0.16f, 0.02f) };
        Rigidbody body = bomb.AddComponent<Rigidbody>();
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        Vector3 targetPoint = target.position + Vector3.up * 0.8f;
        Vector3 flat = targetPoint - bomb.transform.position;
        float distance = new Vector2(flat.x, flat.z).magnitude;
        flat.y = 0f;
        body.linearVelocity = flat.normalized * Mathf.Clamp(distance * 0.8f, 8f, 15f) + Vector3.up * Mathf.Clamp(4.5f + distance * 0.09f, 5f, 8f);
        bomb.AddComponent<EnemyBombProjectile>().Configure(attackDamage, transform);
    }

    private void MoveCharacter(Vector3 direction)
    {
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        Vector3 lowOrigin = transform.position + Vector3.up * 0.45f;
        Vector3 highOrigin = transform.position + Vector3.up * 1.45f;
        bool lowBlocked = HasBlockingObstacle(lowOrigin, direction, 1.05f);
        bool highBlocked = HasBlockingObstacle(highOrigin, direction, 1.05f);
        if (controller.isGrounded && lowBlocked && !highBlocked)
            verticalVelocity = 6.2f;

        verticalVelocity += Physics.gravity.y * Time.deltaTime;
        float buffedSpeed = moveSpeed * (Time.time < officerBuffUntil ? 1.2f : 1f);
        controller.Move((direction * buffedSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    private void UpdateSupportAbility()
    {
        if (Time.time < nextAbilityTime) return;
        if (archetype == EnemyArchetype.Medic)
        {
            nextAbilityTime = Time.time + 1.2f;
            TrainingTarget best = null;
            float lowest = 1f;
            foreach (TrainingTarget ally in FindObjectsByType<TrainingTarget>())
                if (ally != this && ally.IsHostile && ally.IsAlive && Vector3.Distance(transform.position, ally.transform.position) < 12f && ally.HealthRatio < lowest)
                { best = ally; lowest = ally.HealthRatio; }
            best?.Heal(18f);
        }
        else if (archetype == EnemyArchetype.Officer)
        {
            nextAbilityTime = Time.time + 0.6f;
            foreach (TrainingTarget ally in FindObjectsByType<TrainingTarget>())
                if (ally.IsHostile && ally.IsAlive && Vector3.Distance(transform.position, ally.transform.position) < 14f)
                    ally.officerBuffUntil = Time.time + 1f;
        }
        else if (archetype == EnemyArchetype.Engineer && engineerTurret == null)
        {
            nextAbilityTime = Time.time + 14f;
            GameObject turret = new GameObject("Enemy Engineer Turret");
            turret.transform.position = transform.position + transform.right * 1.5f;
            engineerTurret = turret.AddComponent<EnemyTurret>();
            engineerTurret.Configure(transform);
        }
    }

    public void Heal(float amount)
    {
        if (!dead) health = Mathf.Min(maximumHealth, health + Mathf.Max(0f, amount));
    }

    private bool HasBlockingObstacle(Vector3 origin, Vector3 direction, float distance)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.root != transform)
                return true;
        }

        return false;
    }

    private Vector3 GetSteeringDirection(Vector3 desired)
    {
        Vector3 origin = transform.position + Vector3.up;
        if (!Physics.SphereCast(origin, 0.38f, desired, out RaycastHit obstacle, 1.4f, ~0, QueryTriggerInteraction.Ignore)
            || obstacle.collider.transform.root == transform)
            return desired;
        Vector3 side = Vector3.Cross(Vector3.up, desired) * steeringSign;
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
        if (Time.time < lastProgressTime + 6f) return;
        Vector3 recovery = player.transform.position + Vector3.forward * 17f;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float angle = (recoveryAngleOffset + attempt * 41f) * Mathf.Deg2Rad;
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
                : aggroTurret != null
                    ? hit.collider.GetComponentInParent<EngineerTurret>() == aggroTurret
                    : hit.collider.GetComponentInParent<DestructibleObjective>() == attackObjective;
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
        line.startWidth = archetype == EnemyArchetype.Pyro ? 0.16f : archetype == EnemyArchetype.Tank ? 0.035f : 0.018f;
        line.endWidth = archetype == EnemyArchetype.Pyro ? 0.05f : 0.004f;
        line.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        line.startColor = archetype == EnemyArchetype.Pyro ? new Color(1f, 0.15f, 0.01f, 0.95f) : new Color(0.35f, 1f, 0.3f, 0.9f);
        line.endColor = archetype == EnemyArchetype.Pyro ? new Color(1f, 0.75f, 0.05f, 0.15f) : new Color(1f, 0.7f, 0.15f, 0.1f);
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
            FindAnyObjectByType<GameModeManager>()?.RecordEnemyKill();
            if (followsPlayer) EconomyManager.Instance?.RewardEnemy(archetype);
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

    public void Stun(float duration)
    {
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + duration);
    }

    public void ApplyPoison(float duration, float damagePerSecond)
    {
        poisonUntil = Mathf.Max(poisonUntil, Time.time + duration);
        poisonDamage = Mathf.Max(poisonDamage, damagePerSecond);
        nextPoisonTick = Mathf.Min(nextPoisonTick, Time.time + 0.25f);
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
        if (waveManager != null || destroyAfterDefeat)
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
        if (distance > 75f)
            return;

        Vector3 visibilityStart = Camera.main.transform.position + Camera.main.transform.forward * 0.5f;
        if (Physics.Linecast(visibilityStart, transform.position + Vector3.up * 1.8f, out RaycastHit visibilityHit)
            && visibilityHit.transform.root != transform)
            return;

        float width = Mathf.Lerp(110f, 52f, distance / 75f);
        Rect background = new Rect(screenPoint.x - width * 0.5f, Screen.height - screenPoint.y, width, 8f);
        GUIStyle enemyLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = distance < 35f ? 12 : 10, fontStyle = FontStyle.Bold };
        enemyLabel.normal.textColor = new Color(1f, 0.72f, 0.25f);
        GUI.Label(new Rect(background.x - 20f, background.y - 20f, background.width + 40f, 18f), archetype == EnemyArchetype.Normal ? "MELEE" : archetype.ToString().ToUpper(), enemyLabel);
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(background, Texture2D.whiteTexture);
        GUI.color = new Color(0.95f, 0.18f, 0.08f);
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
