using UnityEngine;

public sealed class EngineerTurret : MonoBehaviour, IDamageable
{
    private const float MaximumHealth = 160f;
    private const float Range = 32f;
    private const float FireInterval = 0.22f;
    private const float Damage = 9f;
    private float health = MaximumHealth;
    private float nextShotTime;
    private Transform head;
    private Material tracerMaterial;

    public void Configure(Transform turretHead)
    {
        head = turretHead;
        tracerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
    }

    private void Update()
    {
        TrainingTarget target = FindTarget();
        if (target == null) return;
        Vector3 targetPoint = target.transform.position + Vector3.up * 1.25f;
        Vector3 direction = targetPoint - head.position;
        head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(direction), 12f * Time.deltaTime);
        if (Time.time < nextShotTime) return;
        nextShotTime = Time.time + FireInterval;
        target.TakeDamage(Damage);
        DrawTracer(targetPoint);
    }

    private TrainingTarget FindTarget()
    {
        TrainingTarget best = null;
        float bestDistance = Range;
        foreach (TrainingTarget candidate in FindObjectsByType<TrainingTarget>())
        {
            if (!candidate.IsAlive || !candidate.IsHostile) continue;
            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance >= bestDistance) continue;
            Vector3 targetPoint = candidate.transform.position + Vector3.up * 1.25f;
            Vector3 direction = targetPoint - head.position;
            if (Physics.Raycast(head.position, direction.normalized, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<TrainingTarget>() != candidate) continue;
            best = candidate;
            bestDistance = distance;
        }
        return best;
    }

    private void DrawTracer(Vector3 end)
    {
        GameObject tracer = new GameObject("Turret Tracer");
        LineRenderer line = tracer.AddComponent<LineRenderer>();
        line.material = tracerMaterial;
        line.positionCount = 2;
        line.startWidth = 0.018f;
        line.endWidth = 0.003f;
        line.startColor = new Color(0.3f, 0.9f, 1f, 0.9f);
        line.endColor = new Color(1f, 0.7f, 0.15f, 0.1f);
        line.SetPosition(0, head.position + head.forward * 0.55f);
        line.SetPosition(1, end);
        Destroy(tracer, 0.07f);
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0f) Destroy(gameObject);
    }

    private void OnGUI()
    {
        if (Camera.main == null) return;
        Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.8f);
        if (screen.z <= 0f || screen.z > 35f) return;
        Rect bar = new Rect(screen.x - 40f, Screen.height - screen.y, 80f, 7f);
        GUI.color = Color.black;
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = new Color(0.15f, 0.75f, 1f);
        GUI.DrawTexture(new Rect(bar.x + 1f, bar.y + 1f, 78f * Mathf.Clamp01(health / MaximumHealth), 5f), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
