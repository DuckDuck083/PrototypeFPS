using System;
using UnityEngine;

public sealed class DestructibleObjective : MonoBehaviour, IDamageable
{
    public float Health { get; private set; }
    public float MaximumHealth { get; private set; }
    public bool IsDestroyed => Health <= 0f;
    public Action<DestructibleObjective> Destroyed;

    public void Configure(float health)
    {
        MaximumHealth = health;
        Health = health;
    }

    public void TakeDamage(float amount)
    {
        if (IsDestroyed) return;
        Health = Mathf.Max(0f, Health - amount);
        if (IsDestroyed)
        {
            Destroyed?.Invoke(this);
            foreach (Renderer item in GetComponentsInChildren<Renderer>()) item.material.color = new Color(0.1f, 0.1f, 0.1f);
        }
    }
}
