using System.Collections;
using UnityEngine;

public sealed class FlashbangGrenade : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return new WaitForSeconds(1.25f);
        foreach (Collider hit in Physics.OverlapSphere(transform.position, 12f, ~0, QueryTriggerInteraction.Ignore))
        {
            TrainingTarget target = hit.GetComponentInParent<TrainingTarget>();
            if (target != null && target.IsHostile) target.Stun(4.5f);
        }
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "Flashbang Burst";
        flash.transform.position = transform.position;
        flash.transform.localScale = Vector3.one * 5f;
        Destroy(flash.GetComponent<Collider>());
        Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.color = Color.white;
        flash.GetComponent<Renderer>().material = material;
        Destroy(flash, 0.12f);
        Destroy(gameObject);
    }
}
