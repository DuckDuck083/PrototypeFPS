using System.Collections.Generic;
using UnityEngine;

public sealed class BloodBomb : MonoBehaviour
{
    private SimpleRifle owner;
    private PlayerVitals vitals;
    private float detonateAt;
    public void Configure(SimpleRifle source, PlayerVitals player) { owner = source; vitals = player; detonateAt = Time.time + 1.4f; }
    private void Update() { if (Time.time >= detonateAt) Detonate(); }
    private void Detonate()
    {
        float total = 0f;
        var struck = new HashSet<TrainingTarget>();
        foreach (Collider hit in Physics.OverlapSphere(transform.position, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            TrainingTarget target = hit.GetComponentInParent<TrainingTarget>();
            if (target == null || !struck.Add(target)) continue;
            target.MarkPlayerDamage(owner);
            target.TakeDamage(55f);
            total += 55f;
        }
        vitals?.Heal(total * 0.2f);
        Destroy(gameObject);
    }
}
