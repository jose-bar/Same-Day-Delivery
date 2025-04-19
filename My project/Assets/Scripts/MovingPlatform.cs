using UnityEngine;
using System.Collections.Generic;

public class SimpleMovingPlatform : MonoBehaviour
{
    [Header("Movement Settings")]
    public bool horizontal = true;
    public float moveSpeed = 2f;
    public float patrolDistance = 3f;

    [Header("Passenger Detection")]
    [Tooltip("How far up to check for passengers")]
    public float passengerDetectionHeight = 0.1f;
    [Tooltip("Which layers should be carried by the platform")]
    public LayerMask passengerLayers;

    // Movement variables
    private Vector3 startPosition;
    private Vector3 endPosition;
    private Vector3 moveDirection = Vector3.right;
    private bool movingToEnd = true;

    // Keep track of the platform's previous position
    private Vector3 previousPosition;

    // Debug
    public bool showDebugGizmos = true;

    void Start()
    {
        // Save the starting position
        startPosition = transform.position;

        // Calculate the end position based on direction
        if (horizontal)
        {
            endPosition = startPosition + Vector3.right * patrolDistance;
            moveDirection = Vector3.right;
        }
        else
        {
            endPosition = startPosition + Vector3.up * patrolDistance;
            moveDirection = Vector3.up;
        }

        // Initialize previous position
        previousPosition = transform.position;
    }

    void Update()
    {
        // Move the platform
        MovePlatform();

        // Update the previous position for the next frame
        previousPosition = transform.position;
    }

    void MovePlatform()
    {
        // Determine current target
        Vector3 target = movingToEnd ? endPosition : startPosition;

        // Move toward target
        transform.position = Vector3.MoveTowards(
            transform.position,
            target,
            moveSpeed * Time.deltaTime
        );

        // Check if we've reached the target
        if (Vector3.Distance(transform.position, target) < 0.05f)
        {
            // Switch direction
            movingToEnd = !movingToEnd;
        }
    }

    void FixedUpdate()
    {
        // Calculate the movement this frame
        Vector3 deltaPosition = transform.position - previousPosition;

        // Skip if no movement
        if (deltaPosition.magnitude < 0.001f) return;

        // Find all passengers on the platform
        Collider2D[] hitColliders = Physics2D.OverlapBoxAll(
            transform.position + Vector3.up * passengerDetectionHeight * 0.5f,
            new Vector2(GetPlatformWidth(), passengerDetectionHeight),
            0f,
            passengerLayers
        );

        // Move all passengers with the platform
        foreach (var hitCollider in hitColliders)
        {
            // Skip triggers and the platform itself
            if (hitCollider.isTrigger || hitCollider.transform == transform) continue;

            // Get the rigidbody if available
            Rigidbody2D rb = hitCollider.attachedRigidbody;

            if (rb != null && !rb.isKinematic)
            {
                // Move via rigidbody
                rb.MovePosition(rb.position + (Vector2)deltaPosition);
            }
            else
            {
                // Move the transform directly
                hitCollider.transform.position += deltaPosition;
            }
        }
    }

    // Helper method to get the platform width based on its collider
    private float GetPlatformWidth()
    {
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            return collider.bounds.size.x;
        }

        // Fallback to a default size or try to get size from sprite
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        if (sprite != null)
        {
            return sprite.bounds.size.x;
        }

        // Final fallback
        return 1f;
    }

    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // Show platform path
        Gizmos.color = Color.yellow;

        Vector3 start = Application.isPlaying ? startPosition : transform.position;
        Vector3 end;

        if (horizontal)
        {
            end = start + Vector3.right * patrolDistance;
        }
        else
        {
            end = start + Vector3.up * patrolDistance;
        }

        Gizmos.DrawLine(start, end);

        // Show passenger detection area
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Vector3 center = transform.position + Vector3.up * passengerDetectionHeight * 0.5f;
        Vector3 size = new Vector3(GetPlatformWidth(), passengerDetectionHeight, 0.1f);
        Gizmos.DrawCube(center, size);
    }
}