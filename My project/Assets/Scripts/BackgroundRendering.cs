using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    public Transform cameraTransform;
    public float parallaxFactor = 0.5f;
    private Vector3 lastCameraPosition;

    void Start()
    {
        lastCameraPosition = cameraTransform.position;
    }

    void LateUpdate()
    {
        Vector3 delta = cameraTransform.position - lastCameraPosition;
        transform.position += new Vector3(delta.x * parallaxFactor, delta.y * parallaxFactor, 0);
        lastCameraPosition = cameraTransform.position;
    }
}
