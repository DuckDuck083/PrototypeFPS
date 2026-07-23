using System.Collections;
using UnityEngine;

public sealed class ArenaPickup : MonoBehaviour
{
    public enum PickupType { Health, Ammo }

    private PickupType pickupType;
    private Renderer pickupRenderer;
    private Collider pickupCollider;
    private Vector3 startPosition;

    public void Configure(PickupType type)
    {
        pickupType = type;
        name = type == PickupType.Health ? "Med Pack" : "Ammo Pack";
    }

    private void Awake()
    {
        pickupRenderer = GetComponent<Renderer>();
        pickupCollider = GetComponent<Collider>();
        startPosition = transform.position;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, 70f * Time.deltaTime, Space.World);
        transform.position = startPosition + Vector3.up * (Mathf.Sin(Time.time * 2.5f) * 0.12f);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerVitals vitals = other.GetComponent<PlayerVitals>();
        if (vitals == null)
            return;

        bool collected;
        if (pickupType == PickupType.Health)
        {
            bool hardcore = FindAnyObjectByType<GameModeManager>()?.IsHardcore == true;
            collected = vitals.Heal(hardcore ? 10f : 30f);
        }
        else
        {
            SimpleRifle weapons = other.GetComponent<SimpleRifle>();
            collected = weapons != null && weapons.AddReserveAmmo(2, 0, 2);
        }

        if (collected)
            StartCoroutine(Respawn());
    }

    private IEnumerator Respawn()
    {
        pickupRenderer.enabled = false;
        pickupCollider.enabled = false;
        yield return new WaitForSeconds(15f);
        pickupRenderer.enabled = true;
        pickupCollider.enabled = true;
    }
}
