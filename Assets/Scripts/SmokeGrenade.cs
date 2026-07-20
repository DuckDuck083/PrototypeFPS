using System.Collections;
using UnityEngine;

public sealed class SmokeGrenade : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return new WaitForSeconds(1.1f);

        ParticleSystem particles = gameObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 7f;
        main.loop = true;
        main.startLifetime = 4.5f;
        main.startSpeed = 1.2f;
        main.startSize = 3.5f;
        main.startColor = new Color(0.28f, 0.31f, 0.34f, 0.92f);
        main.maxParticles = 90;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 16f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 2.2f;

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

        Destroy(gameObject, 8f);
    }
}
