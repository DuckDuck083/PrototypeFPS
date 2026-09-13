using UnityEngine;

[RequireComponent(typeof(SimpleRifle), typeof(PlayerVitals))]
public sealed class NewClassController : MonoBehaviour
{
    private SimpleRifle weapons;
    private PlayerVitals vitals;
    private float nextSunTick;
    private void Awake() { weapons = GetComponent<SimpleRifle>(); vitals = GetComponent<PlayerVitals>(); }
    private void Update()
    {
        if (weapons.CurrentClass != SimpleRifle.PlayerClass.Vampire || Time.time < nextSunTick) return;
        nextSunTick = Time.time + 0.5f;
        Light sun = RenderSettings.sun != null ? RenderSettings.sun : FindAnyObjectByType<Light>();
        if (sun == null || !sun.enabled || sun.intensity <= 0.05f) return;
        Vector3 origin = transform.position + Vector3.up * 1.3f;
        RaycastHit[] hits = Physics.RaycastAll(origin, -sun.transform.forward, 100f, ~0, QueryTriggerInteraction.Ignore);
        bool blocked = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.root == transform.root) continue;
            blocked = true;
            break;
        }
        if (!blocked) vitals.TakeDamage(6f);
    }
}
