using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyBombProjectile : MonoBehaviour
{
    private float damage;
    private Transform owner;
    private float explodeAt;
    private bool exploded;

    public void Configure(float bombDamage, Transform bombOwner)
    {
        damage = bombDamage;
        owner = bombOwner;
        explodeAt = Time.time + 3f;
        Collider bombCollider = GetComponent<Collider>();
        if (bombCollider != null && owner != null)
            foreach (Collider ownerCollider in owner.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(bombCollider, ownerCollider);
    }

    private void Update()
    {
        if (Time.time >= explodeAt) Explode();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (owner != null && collision.transform.root == owner) return;
        Explode();
    }

    private void Explode()
    {
        if (exploded) return;
        exploded = true;
        const float radius = 6f;
        PlayerVitals player = FindAnyObjectByType<PlayerVitals>();
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance <= radius)
            {
                float proximity = 1f - Mathf.Clamp01(distance / radius);
                float blastDamage = damage * Mathf.Lerp(0.55f, 1f, proximity);
                player.TakeExplosiveDamage(blastDamage, transform.position);
            }
        }
        HashSet<EngineerTurret> turrets = new HashSet<EngineerTurret>();
        HashSet<DestructibleObjective> objectives = new HashSet<DestructibleObjective>();
        foreach (Collider hit in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            EngineerTurret turret = hit.GetComponentInParent<EngineerTurret>();
            if (turret != null && turrets.Add(turret)) turret.TakeDamage(damage);
            DestructibleObjective objective = hit.GetComponentInParent<DestructibleObjective>();
            if (objective != null && objectives.Add(objective)) objective.TakeDamage(damage);
        }

        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "Enemy Bomb Explosion";
        flash.transform.position = transform.position;
        flash.transform.localScale = Vector3.one * radius * 0.65f;
        Destroy(flash.GetComponent<Collider>());
        Renderer renderer = flash.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 0.18f, 0.02f) };
        Light light = flash.AddComponent<Light>();
        light.color = new Color(1f, 0.22f, 0.03f);
        light.range = 8f;
        light.intensity = 5f;
        Destroy(flash, 0.12f);
        Destroy(gameObject);
    }
}
