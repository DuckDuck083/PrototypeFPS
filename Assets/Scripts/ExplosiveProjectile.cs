using System.Collections.Generic;
using UnityEngine;

public sealed class ExplosiveProjectile : MonoBehaviour
{
    private float damage;
    private float radius;
    private float fuse;
    private bool explodeOnImpact;
    private bool proximityMine;
    private bool enemyImpactOnly;
    private bool sticky;
    private float armDelay;
    private SimpleRifle owner;
    private bool exploded;

    public void Configure(float explosionDamage, float explosionRadius, float fuseTime, bool impactExplosion, SimpleRifle weaponOwner, bool useProximityTrigger = false, bool impactEnemiesOnly = false, bool stickOnCollision = false)
    {
        damage = explosionDamage;
        radius = explosionRadius;
        fuse = fuseTime;
        explodeOnImpact = impactExplosion;
        owner = weaponOwner;
        proximityMine = useProximityTrigger;
        enemyImpactOnly = impactEnemiesOnly;
        sticky = stickOnCollision;
        armDelay = useProximityTrigger ? 0.75f : 0f;
    }

    private void Update()
    {
        fuse -= Time.deltaTime;
        armDelay -= Time.deltaTime;
        if (proximityMine && armDelay <= 0f)
        {
            Collider[] nearby = Physics.OverlapSphere(transform.position, 2.8f, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider candidate in nearby)
            {
                if (candidate.GetComponentInParent<TrainingTarget>() != null)
                {
                    Explode();
                    return;
                }
            }
        }
        if (fuse <= 0f)
            Explode();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (explodeOnImpact || (enemyImpactOnly && collision.collider.GetComponentInParent<TrainingTarget>() != null))
            Explode();
        else if (sticky)
        {
            Rigidbody body = GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            transform.SetParent(collision.transform, true);
        }
    }

    public void Detonate() => Explode();

    private void Explode()
    {
        if (exploded)
            return;

        exploded = true;
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore);
        HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        float totalDamage = 0f;

        foreach (Collider hit in hits)
        {
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable == null || !damagedTargets.Add(damageable))
                continue;

            float distance = Vector3.Distance(transform.position, hit.ClosestPoint(transform.position));
            float dealtDamage = damage * Mathf.Clamp01(1f - distance / radius);
            if (dealtDamage > 1f)
            {
                damageable.TakeDamage(dealtDamage);
                totalDamage += dealtDamage;
            }
        }

        if (totalDamage > 0f)
            owner.ReportExplosiveHit(totalDamage);

        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "Explosion";
        flash.transform.position = transform.position;
        flash.transform.localScale = Vector3.one * radius * 1.4f;
        Destroy(flash.GetComponent<Collider>());
        Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.color = new Color(1f, 0.25f, 0.03f);
        flash.GetComponent<Renderer>().material = material;
        Destroy(flash, 0.12f);
        Destroy(gameObject);
    }
}
