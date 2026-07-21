using System.Collections.Generic;
using UnityEngine;

public sealed class IncendiaryProjectile : MonoBehaviour
{
    private bool impactIgnition;
    private bool burning;
    private float igniteAt;
    private float expireAt;
    private float nextDamageTime;
    private SimpleRifle owner;

    public void Configure(bool igniteOnImpact, SimpleRifle weaponOwner)
    {
        impactIgnition = igniteOnImpact;
        owner = weaponOwner;
        igniteAt = Time.time + (igniteOnImpact ? 3.5f : 1.2f);
    }

    private void Update()
    {
        if (!burning && Time.time >= igniteAt) Ignite();
        if (!burning) return;
        if (Time.time >= expireAt) { Destroy(gameObject); return; }
        if (Time.time < nextDamageTime) return;
        nextDamageTime = Time.time + 0.5f;
        HashSet<TrainingTarget> damaged = new HashSet<TrainingTarget>();
        float total = 0f;
        foreach (Collider hit in Physics.OverlapSphere(transform.position, 4.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            TrainingTarget target = hit.GetComponentInParent<TrainingTarget>();
            if (target == null || !target.IsHostile || !damaged.Add(target)) continue;
            target.TakeDamage(8f);
            total += 8f;
        }
        if (total > 0f) owner.ReportExplosiveHit(total);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (impactIgnition) Ignite();
    }

    private void Ignite()
    {
        if (burning) return;
        burning = true;
        expireAt = Time.time + 6f;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null) { body.linearVelocity = Vector3.zero; body.isKinematic = true; }
        transform.localScale = Vector3.one * 4.5f;
        Destroy(GetComponent<Collider>());
        Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.color = new Color(1f, 0.2f, 0.02f, 0.32f);
        GetComponent<Renderer>().material = material;
    }
}
