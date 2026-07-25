using UnityEngine;

public sealed class EnemyTurret : MonoBehaviour, IDamageable
{
    private float health = 220f;
    private float nextShotTime;
    private Transform owner;
    private Transform head;

    public void Configure(Transform turretOwner)
    {
        owner = turretOwner;
        GameObject basePart = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        basePart.transform.SetParent(transform, false);
        basePart.transform.localPosition = Vector3.up * 0.45f;
        basePart.transform.localScale = new Vector3(0.65f, 0.45f, 0.65f);
        basePart.GetComponent<Renderer>().material.color = new Color(0.48f, 0.12f, 0.08f);
        head = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        head.SetParent(transform, false);
        head.localPosition = Vector3.up * 1.15f;
        head.localScale = new Vector3(0.55f, 0.3f, 0.75f);
        Destroy(head.GetComponent<Collider>());
    }

    private void Update()
    {
        if (owner == null) { Destroy(gameObject); return; }
        PlayerVitals player = FindAnyObjectByType<PlayerVitals>();
        if (player == null || player.IsDead) return;
        Vector3 direction = player.transform.position + Vector3.up - head.position;
        if (direction.magnitude > 38f) return;
        head.rotation = Quaternion.LookRotation(direction);
        if (Time.time < nextShotTime) return;
        nextShotTime = Time.time + 0.26f;
        if (Physics.Raycast(head.position, direction.normalized, out RaycastHit hit, 38f, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<PlayerVitals>() != null)
            player.TakeDamage(10f, transform.position);
    }

    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0f) Destroy(gameObject);
    }
}
