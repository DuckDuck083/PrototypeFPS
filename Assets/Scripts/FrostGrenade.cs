using UnityEngine;

public sealed class FrostGrenade : MonoBehaviour
{
    private float detonateAt;
    private void Awake() => detonateAt = Time.time + 1.4f;
    private void Update() { if (Time.time >= detonateAt) Detonate(); }
    private void Detonate()
    {
        foreach (Collider hit in Physics.OverlapSphere(transform.position, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            TrainingTarget target = hit.GetComponentInParent<TrainingTarget>();
            if (target != null) target.ApplyFrost(3);
        }
        Destroy(gameObject);
    }
}
