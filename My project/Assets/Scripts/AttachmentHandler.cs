using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttachmentHandler : MonoBehaviour
{
    [Header("Attachment Settings")]
    public float attachRange = 0.5f;
    public LayerMask itemLayer;
    public LayerMask obstacleLayer; // Add this to specify what layers are considered obstacles
    public Vector2 detectionPadding = new Vector2(0.5f, 0.5f);
    public Vector2 detectionOffset = Vector2.zero;

    private List<GameObject> rightPackages = new List<GameObject>();
    private List<GameObject> leftPackages = new List<GameObject>();
    private List<GameObject> topPackages = new List<GameObject>();
    private Dictionary<GameObject, Collider2D> itemColliders = new Dictionary<GameObject, Collider2D>(); // Store original colliders
    private Dictionary<GameObject, GameObject> proxyColliders = new Dictionary<GameObject, GameObject>(); // Proxy colliders for attached items

    private bool canToggleAttach = true;
    private bool hasPackageInRange = false;

    // Debug properties
    private Vector2 lastAttachPosition;
    private bool showDebugRays = true;
    private float debugRayDuration = 2f;

    public enum AttachmentSide { Right, Left, Top }

    public void ToggleAttachment(AttachmentSide side)
    {
        if (!canToggleAttach) return;

        List<GameObject> packageList = GetPackageList(side);

        // Detach logic if we already have packages attached
        if (packageList.Count >= 1)
        {
            DetachLastItem(packageList);
            return;
        }

        // Attach logic
        Bounds detectionBounds = GetDetectionBounds();
        Collider2D[] hits = Physics2D.OverlapBoxAll(detectionBounds.center, detectionBounds.size, 0f, itemLayer);

        hasPackageInRange = hits.Length > 0;

        // Track both the item we're trying to attach and whether attachment succeeded
        GameObject targetItem = null;
        bool attachedSuccessfully = false;

        foreach (Collider2D col in hits)
        {
            GameObject item = col.gameObject;
            targetItem = item;  // Save reference even if we can't attach it

            if (!packageList.Contains(item) && !IsAttachedToAnyList(item))
            {
                Vector2 attachPos = GetAttachPosition(side);
                lastAttachPosition = attachPos; // Store for debugging

                // Check if attaching would cause a collision
                if (!WouldCollideAtPosition(item, attachPos, side))
                {
                    AttachItem(item, attachPos, packageList, side);
                    attachedSuccessfully = true;

                    // Show success feedback if we have the component
                    AttachmentVisualFeedback feedback = GetComponent<AttachmentVisualFeedback>();
                    if (feedback != null)
                    {
                        feedback.ShowSuccessFeedback(item);
                    }
                    break;
                }
                else
                {
                    Debug.Log("Cannot attach item - would clip through obstacle");

                    // Show failure feedback if we have the component
                    AttachmentVisualFeedback feedback = GetComponent<AttachmentVisualFeedback>();
                    if (feedback != null && targetItem != null)
                    {
                        feedback.ShowFailureFeedback(targetItem);
                    }
                    break;
                }
            }
        }

        StartCoroutine(AttachCooldown());
    }

    // Check if item is already attached to any side
    private bool IsAttachedToAnyList(GameObject item)
    {
        return rightPackages.Contains(item) || leftPackages.Contains(item) || topPackages.Contains(item);
    }

    // Check if attaching an item would cause it to collide with obstacles
    private bool WouldCollideAtPosition(GameObject item, Vector2 position, AttachmentSide side)
    {
        // Store original position and parent
        Vector3 originalPos = item.transform.position;
        Transform originalParent = item.transform.parent;

        // Get the item's collider(s)
        Collider2D[] itemColliders = item.GetComponents<Collider2D>();
        if (itemColliders.Length == 0)
        {
            Debug.LogWarning("Item has no collider, can't check for collisions");
            return false;
        }

        // Store original collider states
        bool[] originalStates = new bool[itemColliders.Length];
        for (int i = 0; i < itemColliders.Length; i++)
        {
            originalStates[i] = itemColliders[i].enabled;
            itemColliders[i].enabled = true;
        }

        // Temporarily move to test position
        item.transform.SetParent(transform);
        item.transform.position = position;
        item.transform.rotation = transform.rotation;

        // Create a temporary collider to use for overlap check
        GameObject tempObject = new GameObject("TempCollisionCheck");
        tempObject.transform.position = position;
        tempObject.transform.rotation = transform.rotation;

        bool collisionDetected = false;
        List<Collider2D> allOverlappingColliders = new List<Collider2D>();

        // For each collider on the item, create a matching one on our temp object
        for (int i = 0; i < itemColliders.Length; i++)
        {
            Collider2D tempCollider = null;

            if (itemColliders[i] is BoxCollider2D)
            {
                BoxCollider2D original = (BoxCollider2D)itemColliders[i];
                BoxCollider2D copy = tempObject.AddComponent<BoxCollider2D>();
                copy.size = original.size;
                copy.offset = original.offset;
                tempCollider = copy;
            }
            else if (itemColliders[i] is CircleCollider2D)
            {
                CircleCollider2D original = (CircleCollider2D)itemColliders[i];
                CircleCollider2D copy = tempObject.AddComponent<CircleCollider2D>();
                copy.radius = original.radius;
                copy.offset = original.offset;
                tempCollider = copy;
            }
            else if (itemColliders[i] is PolygonCollider2D)
            {
                PolygonCollider2D original = (PolygonCollider2D)itemColliders[i];
                PolygonCollider2D copy = tempObject.AddComponent<PolygonCollider2D>();
                copy.pathCount = original.pathCount;
                for (int p = 0; p < original.pathCount; p++)
                {
                    copy.SetPath(p, original.GetPath(p));
                }
                copy.offset = original.offset;
                tempCollider = copy;
            }

            if (tempCollider != null)
            {
                // Check for overlap with obstacles
                ContactFilter2D filter = new ContactFilter2D();
                filter.SetLayerMask(obstacleLayer);
                filter.useTriggers = false;

                List<Collider2D> results = new List<Collider2D>();
                int count = Physics2D.OverlapCollider(tempCollider, filter, results);

                // Store all results for more detailed analysis
                allOverlappingColliders.AddRange(results);

                // Remove any colliders from our main robot to prevent self-collision
                for (int r = results.Count - 1; r >= 0; r--)
                {
                    if (results[r].gameObject == gameObject ||
                        results[r].transform.IsChildOf(transform) ||
                        transform.IsChildOf(results[r].transform))
                    {
                        results.RemoveAt(r);
                    }
                }

                // Now check if this is a valid collision based on the attachment side and obstacle position
                if (results.Count > 0)
                {
                    // For each potential collision, determine if it's actually blocking the attachment
                    bool trueCollision = false;

                    foreach (Collider2D obstacle in results)
                    {
                        // Get the obstacle bounds
                        Bounds obstacleBounds = obstacle.bounds;
                        Bounds itemBounds = tempCollider.bounds;

                        // Draw debug rays showing the collision points
                        if (showDebugRays)
                        {
                            Debug.DrawLine(position, obstacle.transform.position, Color.red, debugRayDuration);

                            // Draw the bounds of both objects
                            DrawDebugBounds(itemBounds, Color.yellow);
                            DrawDebugBounds(obstacleBounds, Color.cyan);
                        }

                        // Different collision logic based on attachment side
                        switch (side)
                        {
                            case AttachmentSide.Top:
                                // For top attachment, only consider obstacles above the robot
                                if (obstacleBounds.center.y > transform.position.y)
                                {
                                    trueCollision = true;
                                }
                                break;

                            case AttachmentSide.Right:
                                // For right attachment, only consider obstacles to the right or at similar height
                                if (obstacleBounds.center.x > transform.position.x)
                                {
                                    // If the obstacle is above us, make sure it's actually blocking
                                    if (obstacleBounds.min.y > itemBounds.max.y)
                                    {
                                        // Obstacle is above, not blocking
                                        continue;
                                    }
                                    // If the obstacle is below us, make sure it's actually blocking
                                    if (obstacleBounds.max.y < itemBounds.min.y)
                                    {
                                        // Obstacle is below, not blocking
                                        continue;
                                    }
                                    trueCollision = true;
                                }
                                break;

                            case AttachmentSide.Left:
                                // For left attachment, only consider obstacles to the left or at similar height
                                if (obstacleBounds.center.x < transform.position.x)
                                {
                                    // If the obstacle is above us, make sure it's actually blocking
                                    if (obstacleBounds.min.y > itemBounds.max.y)
                                    {
                                        // Obstacle is above, not blocking
                                        continue;
                                    }
                                    // If the obstacle is below us, make sure it's actually blocking
                                    if (obstacleBounds.max.y < itemBounds.min.y)
                                    {
                                        // Obstacle is below, not blocking
                                        continue;
                                    }
                                    trueCollision = true;
                                }
                                break;
                        }

                        if (trueCollision)
                        {
                            collisionDetected = true;
                            break;
                        }
                    }

                    if (collisionDetected)
                        break; // No need to check more colliders
                }
            }
        }

        // Destroy the temporary object
        Destroy(tempObject);

        // Restore original collider states
        for (int i = 0; i < itemColliders.Length; i++)
        {
            itemColliders[i].enabled = originalStates[i];
        }

        // Restore original position and parent
        item.transform.SetParent(originalParent);
        item.transform.position = originalPos;
        item.transform.rotation = Quaternion.identity;

        return collisionDetected;
    }

    private void DrawDebugBounds(Bounds bounds, Color color)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        // Draw the wireframe of the bounds
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), color, debugRayDuration);
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), color, debugRayDuration);
        Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), color, debugRayDuration);
        Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), color, debugRayDuration);
    }

    List<GameObject> GetPackageList(AttachmentSide side)
    {
        return side switch
        {
            AttachmentSide.Right => rightPackages,
            AttachmentSide.Left => leftPackages,
            AttachmentSide.Top => topPackages,
            _ => rightPackages
        };
    }

    Bounds GetDetectionBounds()
    {
        Vector3 basePos = transform.position + (Vector3)detectionOffset;
        Vector2 size = GetVisualBoundsSize() + detectionPadding * 2f;
        return new Bounds(basePos, new Vector3(size.x, size.y, 1f));
    }

    Vector2 GetVisualBoundsSize()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        return sr != null ? sr.bounds.size : Vector2.one;
    }

    Vector2 GetAttachPosition(AttachmentSide side)
    {
        Vector2 basePos = transform.position;
        Vector2 halfSize = GetVisualBoundsSize() / 2f;

        return side switch
        {
            AttachmentSide.Right => basePos + new Vector2(halfSize.x + attachRange, 0),
            AttachmentSide.Left => basePos - new Vector2(halfSize.x + attachRange, 0),
            AttachmentSide.Top => basePos + new Vector2(0, halfSize.y + attachRange),
            _ => basePos
        };
    }

    void AttachItem(GameObject item, Vector2 attachCenter, List<GameObject> packageList, AttachmentSide side)
    {
        // Check if it's already in another list - safety check
        if (IsAttachedToAnyList(item))
        {
            Debug.LogWarning("Attempting to attach an already attached item!");
            return;
        }

        // Check once more for potential collision before finalizing attachment
        if (WouldCollideAtPosition(item, attachCenter, side))
        {
            Debug.LogWarning("Collision detected during final attachment check!");
            return;
        }

        item.transform.SetParent(transform);
        item.transform.rotation = transform.rotation;
        item.transform.position = attachCenter;

        // Disable the item's rigidbody
        Rigidbody2D rb = item.GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        // Store the item's original collider
        Collider2D itemCollider = item.GetComponent<Collider2D>();
        if (itemCollider != null)
        {
            itemColliders[item] = itemCollider;

            // Create a proxy collider that will be used for movement validation
            GameObject proxy = new GameObject($"Proxy_{item.name}");
            proxy.transform.SetParent(transform);
            proxy.transform.position = item.transform.position;
            proxy.transform.rotation = item.transform.rotation;

            // Scale factor to make proxy colliders slightly smaller than the original
            // This gives a small buffer for movement while still preventing obvious clipping
            float proxyScaleFactor = 0.9f;

            // Copy the collider to the proxy, but make it slightly smaller
            Collider2D proxyCollider = null;
            if (itemCollider is BoxCollider2D)
            {
                BoxCollider2D original = itemCollider as BoxCollider2D;
                BoxCollider2D copy = proxy.AddComponent<BoxCollider2D>();
                copy.size = original.size * proxyScaleFactor;
                copy.offset = original.offset;
                proxyCollider = copy;
            }
            else if (itemCollider is CircleCollider2D)
            {
                CircleCollider2D original = itemCollider as CircleCollider2D;
                CircleCollider2D copy = proxy.AddComponent<CircleCollider2D>();
                copy.radius = original.radius * proxyScaleFactor;
                copy.offset = original.offset;
                proxyCollider = copy;
            }
            else if (itemCollider is PolygonCollider2D)
            {
                PolygonCollider2D original = itemCollider as PolygonCollider2D;
                PolygonCollider2D copy = proxy.AddComponent<PolygonCollider2D>();

                // Scale each path point toward the center to make it smaller
                copy.pathCount = original.pathCount;
                for (int i = 0; i < original.pathCount; i++)
                {
                    Vector2[] path = original.GetPath(i);
                    Vector2[] scaledPath = new Vector2[path.Length];

                    // Calculate center of the path
                    Vector2 center = Vector2.zero;
                    foreach (Vector2 point in path)
                    {
                        center += point;
                    }
                    center /= path.Length;

                    // Scale each point toward the center
                    for (int j = 0; j < path.Length; j++)
                    {
                        scaledPath[j] = center + (path[j] - center) * proxyScaleFactor;
                    }

                    copy.SetPath(i, scaledPath);
                }

                copy.offset = original.offset;
                proxyCollider = copy;
            }

            if (proxyCollider != null)
            {
                proxyCollider.isTrigger = true;
                proxyColliders[item] = proxy;

                // Add this component to sync the proxy position with the item
                ProxyPositionSync syncScript = proxy.AddComponent<ProxyPositionSync>();
                syncScript.targetTransform = item.transform;
            }

            // Disable the original collider to prevent double collision
            itemCollider.enabled = false;
        }

        // Notify the Package component if it exists
        Package packageComponent = item.GetComponent<Package>();
        if (packageComponent != null)
        {
            packageComponent.OnAttached(transform);
        }

        packageList.Add(item);
    }

    void DetachLastItem(List<GameObject> packageList)
    {
        if (packageList.Count == 0) return;

        GameObject last = packageList[packageList.Count - 1];
        packageList.RemoveAt(packageList.Count - 1);

        // Re-enable the original collider
        if (itemColliders.ContainsKey(last))
        {
            Collider2D col = itemColliders[last];
            if (col != null) col.enabled = true;
            itemColliders.Remove(last);
        }

        // Destroy the proxy collider
        if (proxyColliders.ContainsKey(last))
        {
            GameObject proxy = proxyColliders[last];
            if (proxy != null) Destroy(proxy);
            proxyColliders.Remove(last);
        }

        // Reset transform parent and re-enable physics
        last.transform.SetParent(null);
        Rigidbody2D rb = last.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = true;

            // Add a small force to "push" the package away slightly
            Package package = last.GetComponent<Package>();
            if (package != null)
            {
                package.OnDetached();
            }
            else
            {
                // Default detachment behavior if no Package component
                rb.AddForce(new Vector2(Random.Range(-1f, 1f), 0.5f), ForceMode2D.Impulse);
            }
        }
    }

    // Get all proxy colliders to be used for movement validation
    public List<Collider2D> GetAllProxyColliders()
    {
        List<Collider2D> colliders = new List<Collider2D>();
        foreach (GameObject proxy in proxyColliders.Values)
        {
            if (proxy != null)
            {
                Collider2D col = proxy.GetComponent<Collider2D>();
                if (col != null) colliders.Add(col);
            }
        }
        return colliders;
    }

    IEnumerator AttachCooldown()
    {
        canToggleAttach = false;
        yield return new WaitForSeconds(0.2f);
        canToggleAttach = true;
    }

    void OnDrawGizmosSelected()
    {
        Bounds bounds = GetDetectionBounds();
        float markerSize = 0.3f;

        Gizmos.color = hasPackageInRange ? new Color(0f, 1f, 0f, 0.2f) : new Color(1f, 0.5f, 0.5f, 0.2f);
        Gizmos.DrawCube(bounds.center, bounds.size);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(GetAttachPosition(AttachmentSide.Right), Vector3.one * markerSize);
        Gizmos.DrawWireCube(GetAttachPosition(AttachmentSide.Left), Vector3.one * markerSize);
        Gizmos.DrawWireCube(GetAttachPosition(AttachmentSide.Top), Vector3.one * markerSize);

        // Draw the last attempted attach position
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastAttachPosition, markerSize * 0.8f);
        }
    }
}