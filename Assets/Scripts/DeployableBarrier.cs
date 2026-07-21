using UnityEngine;

public sealed class DeployableBarrier : MonoBehaviour, IDamageable
{
    private const float MaximumHealth = 400f;
    private float health = MaximumHealth;

    public void TakeDamage(float amount)
    {
        health -= amount;
        if (health <= 0f) Destroy(gameObject);
    }
}
