using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttachmentHandler : MonoBehaviour
{
    [Header("Attachment Settings")]
    public float attachRange = 0.5f;
    public LayerMask itemLayer;
    public LayerMask obstacleLayer;
    public Vector2 detectionPadding = new Vector2(0.5f, 0.5f);
    public Vector2 detectionOffset = Vector2.zero;
    public float validAttachDistance = 0.75f;

    // Constant to identify physic colliders we create
    public const string ATTACHMENT_COLLIDER_TAG = "AttachmentCollider";

    private List<GameObject> rightPackages = new List<GameObject>();
    private List<GameObject> leftPackages = new List<GameObject>();
    private List<GameObject> topPackages = new List<GameObject>();
    private Dictionary<GameObject, Collider2D> itemColliders = new Dictionary<GameObject, Collider2D>();
    private Dictionary<GameObject, Rigidbody2D> itemRigidbodies = new Dictionary<GameObject, Rigidbody2D>();

    // Dictionary to track which item an attachment is connected to
    private Dictionary<GameObject, GameObject> attachmentConnections = new Dictionary<GameObject, GameObject>();

    private bool canToggleAttach = true;
    private bool hasPackageInRange = false;

    // Debug properties
    private Vector2 lastAttachPosition;
    private bool showDebugRays = true;
    private float debugRayDuration = 2f;

    private RobotController robotController;

    // State tracking for attachment preview mode
    private bool isInPreviewMode = false;
    private GameObject targetPackage = null;

    public enum AttachmentSide { Right, Left, Top, Custom }

    void Start()
    {
        // Get a reference to the robot controller
        robotController = GetComponent<RobotController>();
    }

    // Legacy method for quick side attachment (for backward compatibility)
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

                // Check if attaching would cause a collision - temporarily move it to test
                if (!WouldCollideAtPosition(item, attachPos, side))
                {
                    AttachItem(item, attachPos, packageList, side);
                    attachedSuccessfully = true;

                    // Store the connection - connected to robot directly in this case
                    attachmentConnections[item] = gameObject;

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
    public bool IsAttachedToAnyList(GameObject item)
    {
        return rightPackages.Contains(item) || leftPackages.Contains(item) || topPackages.Contains(item);
    }

    // New method to attach an item at a specific position
    public void AttachItemAtPosition(GameObject item, Vector2 attachPosition)
    {
        if (!canToggleAttach) return;

        // Don't attach if already attached
        if (IsAttachedToAnyList(item))
        {
            Debug.LogWarning("Item is already attached");
            return;
        }

        // Find what this item will be connected to
        GameObject connectedTo = FindConnectedItem(attachPosition);
        if (connectedTo == null && Vector2.Distance(attachPosition, transform.position) > validAttachDistance)
        {
            Debug.LogWarning("Item must be connected to robot or another attached item");

            // Show failure feedback
            AttachmentVisualFeedback feedback = GetComponent<AttachmentVisualFeedback>();
            if (feedback != null)
            {
                feedback.ShowFailureFeedback(item);
            }
            return;
        }

        // Check if this position would cause collisions
        if (WouldCollideAtPosition(item, attachPosition, AttachmentSide.Custom))
        {
            Debug.LogWarning("Item would collide with obstacles at this position");

            // Show failure feedback
            AttachmentVisualFeedback feedback = GetComponent<AttachmentVisualFeedback>();
            if (feedback != null)
            {
                feedback.ShowFailureFeedback(item);
            }
            return;
        }

        // Determine which side this attachment belongs to (based on position relative to robot)
        AttachmentSide side = DetermineSideFromPosition(attachPosition);
        List<GameObject> packageList = GetPackageList(side);

        // Perform the attachment
        AttachItem(item, attachPosition, packageList, side);

        // Track the connection
        if (connectedTo != null)
        {
            attachmentConnections[item] = connectedTo;
        }
        else
        {
            // Connected directly to robot
            attachmentConnections[item] = gameObject;
        }

        // Show success feedback
        AttachmentVisualFeedback feedback2 = GetComponent<AttachmentVisualFeedback>();
        if (feedback2 != null)
        {
            feedback2.ShowSuccessFeedback(item);
        }

        StartCoroutine(AttachCooldown());
    }

    // Find an item that this attachment position connects to
    public GameObject FindConnectedItem(Vector2 position)
    {
        // Check all attached items to find the closest one that this position connects to
        List<GameObject> allItems = GetAllAttachedItems();

        GameObject closestItem = null;
        float closestDistance = validAttachDistance; // Maximum connection distance

        foreach (GameObject item in allItems)
        {
            float distance = Vector2.Distance(position, item.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestItem = item;
            }
        }

        // If no items are close enough, check if we're close to the robot
        if (closestItem == null)
        {
            float robotDistance = Vector2.Distance(position, transform.position);
            if (robotDistance < validAttachDistance)
            {
                return gameObject; // Return the robot itself
            }
        }

        return closestItem;
    }

    // Determine which side an attachment belongs to based on position
    private AttachmentSide DetermineSideFromPosition(Vector2 position)
    {
        // Determine side based on position relative to robot
        Vector2 relativePos = position - (Vector2)transform.position;

        // Simple determination based on angle
        float angle = Mathf.Atan2(relativePos.y, relativePos.x) * Mathf.Rad2Deg;

        if (angle > 45 && angle < 135)
            return AttachmentSide.Top;
        else if (angle > -45 && angle < 45)
            return AttachmentSide.Right;
        else
            return AttachmentSide.Left;
    }

    // Check if attaching an item would cause it to collide with obstacles
    public bool WouldCollideAtPosition(GameObject item, Vector2 position, AttachmentSide side)
    {
        // Most basic check - does a box at the attach position overlap with anything?
        SpriteRenderer sr = item.GetComponent<SpriteRenderer>();
        Vector2 size = sr != null ? (Vector2)sr.bounds.size : new Vector2(0.5f, 0.5f);

        // Add a small buffer to avoid edge cases
        size += new Vector2(0.05f, 0.05f);

        // Simple overlap check
        Collider2D[] hits = Physics2D.OverlapBoxAll(position, size, 0f, obstacleLayer);

        // Filter hits to remove robot and attachments
        for (int i = 0; i < hits.Length; i++)
        {
            // Skip if the hit is:
            // 1. The robot itself
            // 2. The item we're trying to attach
            // 3. Any child of the robot (including currently attached items)
            // 4. Any attachment collider
            if (hits[i].gameObject == gameObject ||
                hits[i].gameObject == item ||
                hits[i].transform.IsChildOf(transform) ||
                hits[i].CompareTag(ATTACHMENT_COLLIDER_TAG) ||
                hits[i].CompareTag("ProxyCollider"))
            {
                continue;
            }

            // If we reach here, we found a real collision
            Debug.Log($"Cannot attach - would collide with {hits[i].gameObject.name}");
            return true;
        }

        return false;
    }

    // New method to detach a specific item and its dependents
    public void DetachItem(GameObject item)
    {
        if (!IsAttachedToAnyList(item)) return;

        // COMPLETELY FREEZE THE ROBOT FOR A MOMENT
        if (robotController != null)
        {
            // Use our new freezing method - freeze for 0.3 seconds
            robotController.FreezePositionForAttachment(0.3f);
        }

        // Find all items that depend on this one
        List<GameObject> dependents = FindDependentItems(item);

        // First detach all dependents
        foreach (GameObject dependent in dependents)
        {
            List<GameObject> packageList = FindContainingList(dependent);
            if (packageList != null)
            {
                DetachSingleItem(dependent, packageList);
            }
        }

        // Then detach the item itself
        List<GameObject> itemList = FindContainingList(item);
        if (itemList != null)
        {
            DetachSingleItem(item, itemList);
        }

        StartCoroutine(AttachCooldown());
    }

    // Find all items that depend on a given item
    private List<GameObject> FindDependentItems(GameObject item)
    {
        List<GameObject> dependents = new List<GameObject>();

        // Find all items that are connected to this item
        foreach (var pair in attachmentConnections)
        {
            if (pair.Value == item)
            {
                dependents.Add(pair.Key);

                // Recursively find items that depend on this dependent
                List<GameObject> subDependents = FindDependentItems(pair.Key);
                dependents.AddRange(subDependents);
            }
        }

        return dependents;
    }

    // Find which list contains a given item
    private List<GameObject> FindContainingList(GameObject item)
    {
        if (rightPackages.Contains(item)) return rightPackages;
        if (leftPackages.Contains(item)) return leftPackages;
        if (topPackages.Contains(item)) return topPackages;
        return null;
    }

    // Detach a single item without affecting dependencies
    private void DetachSingleItem(GameObject item, List<GameObject> packageList)
    {
        // Remove from the package list
        packageList.Remove(item);

        // Remove from connection tracking
        attachmentConnections.Remove(item);

        // Reset layer to the original Package layer
        item.gameObject.layer = LayerMask.NameToLayer("Package");

        // Reset tag - VERY important for re-attachment
        item.tag = "Untagged";

        // Re-enable the original rigidbody
        if (itemRigidbodies.ContainsKey(item))
        {
            Rigidbody2D rb = itemRigidbodies[item];
            if (rb != null) rb.simulated = true;
            itemRigidbodies.Remove(item);

            // Reset transform parent and apply a small detachment force
            item.transform.SetParent(null);

            Package package = item.GetComponent<Package>();
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
        else
        {
            // If no rigidbody was stored, just detach it
            item.transform.SetParent(null);
        }

        // Reset any colliders we might have modified
        Collider2D[] itemColliders = item.GetComponents<Collider2D>();
        foreach (Collider2D col in itemColliders)
        {
            col.enabled = true;
        }
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

    public List<GameObject> GetPackageList(AttachmentSide side)
    {
        return side switch
        {
            AttachmentSide.Right => rightPackages,
            AttachmentSide.Left => leftPackages,
            AttachmentSide.Top => topPackages,
            _ => rightPackages // Default to right for custom positions too
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

    public Vector2 GetAttachPosition(AttachmentSide side)
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

        // COMPLETELY FREEZE THE ROBOT FOR A MOMENT
        if (robotController != null)
        {
            // Use our new freezing method - freeze for 0.3 seconds
            robotController.FreezePositionForAttachment(0.3f);
        }

        // Store reference to the item's rigidbody for later restoration
        Rigidbody2D rb = item.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // Ensure the rigidbody is completely stopped
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
            itemRigidbodies[item] = rb;
        }

        // Parent the item to the robot and position it correctly
        item.transform.SetParent(transform);
        item.transform.rotation = transform.rotation;
        item.transform.position = attachCenter;

        // Handle the item's collider
        Collider2D[] itemColliders = item.GetComponents<Collider2D>();
        foreach (Collider2D col in itemColliders)
        {
            col.enabled = true;
            col.gameObject.layer = gameObject.layer;
            col.gameObject.tag = ATTACHMENT_COLLIDER_TAG;
        }

        // Track the attached item
        packageList.Add(item);

        // Notify the Package component if it exists
        Package packageComponent = item.GetComponent<Package>();
        if (packageComponent != null)
        {
            packageComponent.OnAttached(transform);
        }

        // Play success feedback if we have the component
        AttachmentVisualFeedback feedback = GetComponent<AttachmentVisualFeedback>();
        if (feedback != null)
        {
            feedback.ShowSuccessFeedback(item);
        }
    }

    void DetachLastItem(List<GameObject> packageList)
    {
        if (packageList.Count == 0) return;

        GameObject last = packageList[packageList.Count - 1];

        // Instead of the direct removal, use our new detach method to handle dependencies
        DetachItem(last);
    }

    // Get all attached items
    public List<GameObject> GetAllAttachedItems()
    {
        List<GameObject> allItems = new List<GameObject>();
        allItems.AddRange(rightPackages);
        allItems.AddRange(leftPackages);
        allItems.AddRange(topPackages);
        return allItems;
    }

    // Find the closest unattached package
    public GameObject FindClosestUnattachedPackage()
    {
        Bounds detectionBounds = GetDetectionBounds();
        Collider2D[] hits = Physics2D.OverlapBoxAll(detectionBounds.center, detectionBounds.size, 0f, itemLayer);

        GameObject closest = null;
        float closestDistance = float.MaxValue;

        foreach (Collider2D col in hits)
        {
            GameObject item = col.gameObject;

            // Skip if already attached
            if (IsAttachedToAnyList(item)) continue;

            float distance = Vector2.Distance(transform.position, item.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = item;
            }
        }

        return closest;
    }

    // Check if a position is valid for attachment
    public bool IsValidAttachmentPosition(Vector2 position, GameObject item)
    {
        // Check if connected to robot or another package
        bool isConnected = IsConnectedToRobotOrPackage(position);

        // Check for collisions
        bool wouldCollide = WouldCollideAtPosition(item, position, AttachmentSide.Custom);

        return isConnected && !wouldCollide;
    }

    // Check if a position is close enough to the robot or an attached package
    private bool IsConnectedToRobotOrPackage(Vector2 position)
    {
        // Check proximity to robot
        float robotDistance = Vector2.Distance(position, transform.position);
        if (robotDistance < validAttachDistance) return true;

        // Check proximity to attached packages
        List<GameObject> attachedItems = GetAllAttachedItems();
        foreach (GameObject item in attachedItems)
        {
            float distance = Vector2.Distance(position, item.transform.position);
            if (distance < validAttachDistance) return true;
        }

        return false;
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

        // Draw connection lines between items
        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            foreach (var pair in attachmentConnections)
            {
                if (pair.Key != null && pair.Value != null)
                {
                    Vector3 start = pair.Key.transform.position;
                    Vector3 end = pair.Value.transform.position;
                    Gizmos.DrawLine(start, end);
                }
            }
        }
    }
}