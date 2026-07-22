using System.Collections.Generic;
using UnityEngine;

public sealed class ConvoyGameMode : GameModeBase
{
    public override GameModeManager.Mode Type => GameModeManager.Mode.Convoy;
    public override string Objective => "CONVOY  •  Escort the briefcase to extraction and keep it intact";
    private DestructibleObjective briefcase;
    private readonly List<Transform> bodyguards = new List<Transform>();
    private Transform player;
    private float nextAttackWave;
    private float nextCombatTick;
    private readonly Vector3 extraction = new Vector3(0f, 0.65f, 72f);

    public override void Begin(GameModeManager manager)
    {
        base.Begin(manager);
        player = FindAnyObjectByType<PlayerVitals>()?.transform;
        briefcase = CreateBriefcase();
        briefcase.Destroyed += _ => Manager.Finish(false, "the briefcase was destroyed");
        for (int i = 0; i < 4; i++) bodyguards.Add(CreateBodyguard(i));
        SpawnAttackWave();
    }

    private void Update()
    {
        if (briefcase == null || briefcase.IsDestroyed) return;
        if (player != null && Vector3.Distance(player.position, briefcase.transform.position) < 18f)
            briefcase.transform.position = Vector3.MoveTowards(briefcase.transform.position, extraction, 2.2f * Time.deltaTime);
        if (Vector3.Distance(briefcase.transform.position, extraction) < 1f)
        {
            Manager.Finish(true, "briefcase reached extraction");
            return;
        }

        for (int i = 0; i < bodyguards.Count; i++)
        {
            Vector3 offset = new Vector3((i % 2 == 0 ? -1f : 1f) * (2.2f + i), 0.4f, -2f - (i / 2) * 2f);
            bodyguards[i].position = Vector3.Lerp(bodyguards[i].position, briefcase.transform.position + offset, 4f * Time.deltaTime);
        }

        if (Time.time >= nextAttackWave) SpawnAttackWave();
        if (Time.time >= nextCombatTick)
        {
            nextCombatTick = Time.time + 0.75f;
            BodyguardsAttack();
            foreach (TrainingTarget enemy in FindObjectsByType<TrainingTarget>(FindObjectsSortMode.None))
                if (enemy.IsHostile && enemy.IsAlive && Vector3.Distance(enemy.transform.position, briefcase.transform.position) < 10f)
                    briefcase.TakeDamage(8f);
        }
    }

    private void SpawnAttackWave()
    {
        nextAttackWave = Time.time + 18f;
        Vector3 center = briefcase == null ? Vector3.zero : briefcase.transform.position;
        for (int i = 0; i < 7; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Spawn(i % 4 == 0 ? TrainingTarget.EnemyArchetype.Rifle : TrainingTarget.EnemyArchetype.Normal,
                center + new Vector3(side * Random.Range(20f, 35f), 0f, Random.Range(12f, 28f)));
        }
    }

    private void BodyguardsAttack()
    {
        foreach (Transform guard in bodyguards)
        foreach (TrainingTarget enemy in FindObjectsByType<TrainingTarget>(FindObjectsSortMode.None))
            if (enemy.IsHostile && enemy.IsAlive && Vector3.Distance(guard.position, enemy.transform.position) < 24f)
            {
                enemy.TakeDamage(9f);
                break;
            }
    }

    private static DestructibleObjective CreateBriefcase()
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = "Top Secret Briefcase";
        item.transform.position = new Vector3(0f, 0.65f, -70f);
        item.transform.localScale = new Vector3(1.4f, 0.8f, 0.45f);
        item.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.12f, 0.12f, 0.1f) };
        DestructibleObjective objective = item.AddComponent<DestructibleObjective>();
        objective.Configure(600f);
        return objective;
    }

    private static Transform CreateBodyguard(int index)
    {
        GameObject guard = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        guard.name = $"Bodyguard {index + 1}";
        guard.transform.position = new Vector3(-5f + index * 3f, 1f, -74f);
        guard.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.08f, 0.35f, 0.8f) };
        Destroy(guard.GetComponent<Collider>());
        return guard.transform;
    }

    public override void EndMode()
    {
        if (briefcase != null) Destroy(briefcase.gameObject);
        foreach (Transform guard in bodyguards) if (guard != null) Destroy(guard.gameObject);
        bodyguards.Clear();
        base.EndMode();
    }
}
