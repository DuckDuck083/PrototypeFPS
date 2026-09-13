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
        nextSunTick = Time.time + 1f;
        Light sun = RenderSettings.sun != null ? RenderSettings.sun : FindAnyObjectByType<Light>();
        if (sun == null || !sun.enabled || sun.intensity <= 0.05f) return;
        Vector3 origin = transform.position + Vector3.up * 1.3f;
        if (!Physics.Raycast(origin, -sun.transform.forward, 100f, ~0, QueryTriggerInteraction.Ignore))
            vitals.TakeDamage(4f);
    }
}
