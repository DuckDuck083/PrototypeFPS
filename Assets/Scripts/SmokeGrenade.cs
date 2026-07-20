using System.Collections;
using UnityEngine;

public sealed class SmokeGrenade : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return new WaitForSeconds(0.55f);

        ParticleSystem particles = gameObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 10f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5.5f, 7.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(4.5f, 7f);
        main.startColor = new Color(0.18f, 0.2f, 0.22f, 0.98f);
        main.maxParticles = 240;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 34f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 3.8f;

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader != null)
            particleRenderer.material = new Material(shader);

        Renderer grenadeRenderer = GetComponent<Renderer>();
        if (grenadeRenderer != null) grenadeRenderer.enabled = false;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        Destroy(gameObject, 11f);
    }
}
