using UnityEngine;

public sealed class WorldHealthBar : MonoBehaviour
{
    private Camera targetCamera;

    private void Start()
    {
        targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (targetCamera != null)
            transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.transform.position);
    }
}
