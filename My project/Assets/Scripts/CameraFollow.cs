using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 0.125f;
    public Vector3 offset = new Vector3(0, 1, -10);

    void LateUpdate()
    {
        if (target == null)
        {
            // Try to find the player if target is not assigned
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
                Debug.Log("Camera found player target: " + player.name);
            }
            else
            {
                Debug.LogWarning("Camera cannot find a target. Please assign a target with the Player tag.");
                return;
            }
        }

        // Calculate desired position
        Vector3 desiredPosition = target.position + offset;

        // Only move camera in X and Y, keep Z fixed
        desiredPosition.z = transform.position.z;

        // Smoothly move camera
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }
}