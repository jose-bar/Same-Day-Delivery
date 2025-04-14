using UnityEngine;

/// <summary>
/// Reports collisions from proxy colliders to the robot controller.
/// This version uses triggers instead of physics collisions.
/// </summary>
public class CollisionReporter : MonoBehaviour
{
    public RobotController robotController;

    // Use triggers instead of collisions
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Ignore collisions with the robot itself or other proxies
        if (collision.CompareTag(AttachmentHandler.ATTACHMENT_COLLIDER_TAG) ||
            collision.CompareTag("ProxyCollider") ||
            collision.transform.IsChildOf(transform.parent))
        {
            return;
        }

        // Report collision to the robot controller
        if (robotController != null)
        {
            Vector2 direction = (transform.position - collision.transform.position).normalized;
            robotController.HandleTriggerCollision(direction, collision.gameObject);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // Ignore collisions with the robot itself or other proxies
        if (collision.CompareTag(AttachmentHandler.ATTACHMENT_COLLIDER_TAG) ||
            collision.CompareTag("ProxyCollider") ||
            collision.transform.IsChildOf(transform.parent))
        {
            return;
        }

        // Report collision to the robot controller
        if (robotController != null)
        {
            Vector2 direction = (transform.position - collision.transform.position).normalized;
            robotController.HandleTriggerCollisionStay(direction, collision.gameObject);
        }
    }
}