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

    [Header("Specific Attachment Points")]
    public Transform rightAttachPoint; // Reference to a child object positioned at right attachment point
    public Transform leftAttachPoint;  // Reference to a child object positioned at left attachment point
    public Transform topAttachPoint;   // Reference to a child object positioned at top attachment point
    public float maxAttachmentsPerSide = 3; // Maximum attachments allowed per side
    public float minDistanceBetweenAttachments = 0.3f; // Minimum spacing between attached items

    // Constant to identify physic colliders we create
    public const string ATTACHMENT_COLLIDER_TAG = "AttachmentCollider";

    // Lists for each attachment point
    private List<GameObject> rightPackages = new List<GameObject>();
    private List<GameObject> leftPackages = new List<GameObject>();
    private List<GameObject> topPackages = new List<GameObject>();

    public Dictionary<GameObject, Collider2D> itemColliders = new Dictionary<GameObject, Collider2D>();
    public Dictionary<GameObject, Rigidbody2D> itemRigidbodies = new Dictionary<GameObject, Rigidbody2D>();

    // Dictionary to track which item an attachment is connected to
    public Dictionary<GameObject, GameObject> attachmentConnections = new Dictionary<GameObject, GameObject>();

    // Dictionary to track which side an item belongs to
    public Dictionary<GameObject, AttachmentSide> itemToSideMap = new Dictionary<GameObject, AttachmentSide>();

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

    // Sound Effects
    OneSoundEffects oneSounds;

    void Start()
    {
        // Get a reference to the robot controller
        robotController = GetComponent<RobotController>();
        oneSounds = GetComponentInParent<OneSoundEffects>();

        // Create attachment points if they don't exist
        CreateAttachmentPointsIfNeeded();
    }

    private void CreateAttachmentPointsIfNeeded()
    {
        // Create right attachment point if needed
        if (rightAttachPoint == null)
        {
            GameObject rightPoint = new GameObject("RightAttachPoint");
            rightPoint.transform.parent = transform;

            // Position it to the right of the robot
            SpriteRenderer robotRenderer = GetComponent<SpriteRenderer>();
            if (robotRenderer != null)
            {
                rightPoint.transform.localPosition = new Vector3(robotRenderer.bounds.extents.x + 0.2f, 0, 0);
            }
            else
            {
                rightPoint.transform.localPosition = new Vector3(0.7f, 0, 0);
            }

            rightAttachPoint = rightPoint.transform;
        }

        // Create left attachment point if needed
        if (leftAttachPoint == null)
        {
            GameObject leftPoint = new GameObject("LeftAttachPoint");
            leftPoint.transform.parent = transform;

            // Position it to the left of the robot
            SpriteRenderer robotRenderer = GetComponent<SpriteRenderer>();
            if (robotRenderer != null)
            {
                leftPoint.transform.localPosition = new Vector3(-(robotRenderer.bounds.extents.x + 0.2f), 0, 0);
            }
            else
            {
                leftPoint.transform.localPosition = new Vector3(-0.7f, 0, 0);
            }

            leftAttachPoint = leftPoint.transform;
        }

        // Create top attachment point if needed
        if (topAttachPoint == null)
        {
            GameObject topPoint = new GameObject("TopAttachPoint");
            topPoint.transform.parent = transform;

            // Position it on top of the robot
            SpriteRenderer robotRenderer = GetComponent<SpriteRenderer>();
            if (robotRenderer != null)
            {
                topPoint.transform.localPosition = new Vector3(0, robotRenderer.bounds.extents.y + 0.2f, 0);
            }
            else
            {
                topPoint.transform.localPosition = new Vector3(0, 0.7f, 0);
            }

            topAttachPoint = topPoint.transform;
        }
    }

    // Legacy method for quick side attachment (for backward compatibility)
    public void ToggleAttachment(AttachmentSide side)
    {
        if (!canToggleAttach) return;
        List<GameObject> packageList = GetPackageList(side);

        // Detach logic if we already have packages attached
        if (packageList.Count >= 1)
        {
            // Only detach the last item on this side, not all
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

            if (!IsAttachedToAnyList(item))
            {
                // Get the specific attachment point for this side
                Vector2 attachPos = GetAttachPositionForSide(side);
                lastAttachPosition = attachPos; // Store for debugging

                // Check if attaching would cause a collision - temporarily move it to test
                if (!WouldCollideAtPosition(item, attachPos, side))
                {
                    AttachItem(item, attachPos, packageList, side);
                    attachedSuccessfully = true;

                    // Store the connection and side information
                    attachmentConnections[item] = gameObject;
                    itemToSideMap[item] = side;

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

    // Get the specific attachment position for a side
    private Vector2 GetAttachPositionForSide(AttachmentSide side)
    {
        Transform attachPoint = null;

        switch (side)
        {
            case AttachmentSide.Right:
                attachPoint = rightAttachPoint;
                break;
            case AttachmentSide.Left:
                attachPoint = leftAttachPoint;
                break;
            case AttachmentSide.Top:
                attachPoint = topAttachPoint;
                break;
            default:
                return GetAttachPosition(side); // Use legacy method for Custom
        }

        if (attachPoint != null)
        {
            return attachPoint.position;
        }
        else
        {
            // Fallback to legacy method if attachment points aren't set
            return GetAttachPosition(side);
        }
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

        // First determine which side we're attaching to
        AttachmentSide side = DetermineSideFromPosition(attachPosition);

        // Check if we've reached the maximum attachments for this side
        List<GameObject> packageList = GetPackageList(side);
        if (packageList.Count >= maxAttachmentsPerSide)
        {
            Debug.LogWarning($"Maximum attachments ({maxAttachmentsPerSide}) reached for {side} side");

            // Show failure feedback
            AttachmentVisualFeedback feedback = GetComponent<AttachmentVisualFeedback>();
            if (feedback != null)
            {
                feedback.ShowFailureFeedback(item);
            }
            return;
        }

        // Find what this item will be connected to, prioritizing the same side
        GameObject connectedTo = FindConnectedItemOnSide(attachPosition, side);

        // If nothing on the same side, try the general find method
        if (connectedTo == null)
        {
            connectedTo = FindConnectedItem(attachPosition);
        }

        // Check if this position is valid for connection
        if (connectedTo == null)
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

        // Check if this position would cause collisions or excessive overlap
        if (WouldCollideAtPosition(item, attachPosition, side) ||
            WouldCauseExcessiveOverlap(item, attachPosition, side))
        {
            Debug.LogWarning("Item would collide with obstacles or cause excessive overlap at this position");

            // Show failure feedback
            AttachmentVisualFeedback feedback = GetComponent<AttachmentVisualFeedback>();
            if (feedback != null)
            {
                feedback.ShowFailureFeedback(item);
            }
            return;
        }

        // Perform the attachment
        AttachItem(item, attachPosition, packageList, side);

        // Play attach audio on success
        if (oneSounds != null)
        {
            oneSounds.PlayAttachAudio();
        }

        // Track the connection and side
        if (connectedTo != null)
        {
            attachmentConnections[item] = connectedTo;
        }
        else
        {
            // Connected directly to robot
            attachmentConnections[item] = gameObject;
        }

        // Store which side this item belongs to
        itemToSideMap[item] = side;

        // Show success feedback
        AttachmentVisualFeedback feedback2 = GetComponent<AttachmentVisualFeedback>();
        if (feedback2 != null)
        {
            feedback2.ShowSuccessFeedback(item);
        }

        StartCoroutine(AttachCooldown());
    }


    public GameObject FindConnectedItemOnSide(Vector2 position, AttachmentSide side)
    {
        // Get all items on this specific side
        List<GameObject> sideItems = GetPackageList(side);

        GameObject closestItem = null;
        float closestDistance = validAttachDistance;

        // Find the closest item on this side
        foreach (GameObject item in sideItems)
        {
            float distance = Vector2.Distance(position, item.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestItem = item;
            }
        }

        // If no items on this side are close enough, check the appropriate attachment point
        if (closestItem == null)
        {
            // Get the attachment point for this side
            Transform attachPoint = null;
            switch (side)
            {
                case AttachmentSide.Right:
                    attachPoint = rightAttachPoint;
                    break;
                case AttachmentSide.Left:
                    attachPoint = leftAttachPoint;
                    break;
                case AttachmentSide.Top:
                    attachPoint = topAttachPoint;
                    break;
            }

            if (attachPoint != null)
            {
                float distance = Vector2.Distance(position, attachPoint.position);
                if (distance < validAttachDistance)
                {
                    // Return the robot itself when connecting to an attachment point
                    return gameObject;
                }
            }
        }

        return closestItem;
    }


    // Check if attaching would cause excessive overlap with robot or other packages
    private bool WouldCauseExcessiveOverlap(GameObject item, Vector2 position, AttachmentSide side)
    {
        if (item == null) return false;

        // Get the item's bounds based on sprite or collider
        Bounds itemBounds = GetItemBounds(item);
        if (itemBounds.size == Vector3.zero) return false;

        // Temporarily place the item at the position to check for overlaps
        Vector3 originalPosition = item.transform.position;
        item.transform.position = position;

        // Update the bounds to the new position
        Bounds testBounds = new Bounds(
            position,
            itemBounds.size
        );

        bool excessiveOverlap = false;

        // 1. Check overlap with the robot
        SpriteRenderer robotSprite = GetComponent<SpriteRenderer>();
        if (robotSprite != null)
        {
            Bounds robotBounds = robotSprite.bounds;

            // Calculate how much they overlap (percentage of the item's bounds)
            Bounds intersection = new Bounds();
            bool overlaps = CalculateBoundsIntersection(testBounds, robotBounds, out intersection);

            if (overlaps)
            {
                float overlapVolume = intersection.size.x * intersection.size.y * intersection.size.z;
                float itemVolume = testBounds.size.x * testBounds.size.y * testBounds.size.z;

                // If more than 30% of the item overlaps with the robot, consider it excessive
                if (overlapVolume / itemVolume > 0.3f)
                {
                    excessiveOverlap = true;
                }
            }
        }

        // 2. Check overlap with other attached items
        List<GameObject> allAttached = GetAllAttachedItems();
        foreach (GameObject attached in allAttached)
        {
            if (attached == item) continue;

            Bounds attachedBounds = GetItemBounds(attached);

            // Calculate how much they overlap
            Bounds intersection = new Bounds();
            bool overlaps = CalculateBoundsIntersection(testBounds, attachedBounds, out intersection);

            if (overlaps)
            {
                float overlapVolume = intersection.size.x * intersection.size.y * intersection.size.z;
                float itemVolume = testBounds.size.x * testBounds.size.y * testBounds.size.z;

                // If more than 40% of the item overlaps with another item, consider it excessive
                if (overlapVolume / itemVolume > 0.4f)
                {
                    excessiveOverlap = true;
                    break;
                }
            }
        }

        // Restore original position
        item.transform.position = originalPosition;

        return excessiveOverlap;
    }

    // Helper method to calculate intersection between two bounds
    private bool CalculateBoundsIntersection(Bounds a, Bounds b, out Bounds intersection)
    {
        intersection = new Bounds();

        if (!a.Intersects(b))
        {
            return false;
        }

        // Calculate the intersection bounds
        float xMin = Mathf.Max(a.min.x, b.min.x);
        float yMin = Mathf.Max(a.min.y, b.min.y);
        float zMin = Mathf.Max(a.min.z, b.min.z);
        float xMax = Mathf.Min(a.max.x, b.max.x);
        float yMax = Mathf.Min(a.max.y, b.max.y);
        float zMax = Mathf.Min(a.max.z, b.max.z);

        Vector3 min = new Vector3(xMin, yMin, zMin);
        Vector3 max = new Vector3(xMax, yMax, zMax);

        intersection = new Bounds();
        intersection.SetMinMax(min, max);

        return true;
    }

    // Helper method to get item bounds
    private Bounds GetItemBounds(GameObject item)
    {
        // Try to get bounds from sprite renderer
        SpriteRenderer sr = item.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            return sr.bounds;
        }

        // Try to get bounds from collider
        Collider2D col = item.GetComponent<Collider2D>();
        if (col != null)
        {
            return col.bounds;
        }

        return new Bounds(item.transform.position, Vector3.zero);
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
            // Check closeness to specific attachment points
            float rightDist = rightAttachPoint != null ?
                Vector2.Distance(position, rightAttachPoint.position) : float.MaxValue;

            float leftDist = leftAttachPoint != null ?
                Vector2.Distance(position, leftAttachPoint.position) : float.MaxValue;

            float topDist = topAttachPoint != null ?
                Vector2.Distance(position, topAttachPoint.position) : float.MaxValue;

            if (rightDist < validAttachDistance || leftDist < validAttachDistance || topDist < validAttachDistance)
            {
                return gameObject; // Return the robot itself
            }

            // Legacy check
            float robotDistance = Vector2.Distance(position, transform.position);
            if (robotDistance < validAttachDistance)
            {
                return gameObject; // Return the robot itself
            }
        }

        return closestItem;
    }

    // Determine which side an attachment belongs to based on position
    public AttachmentSide DetermineSideFromPosition(Vector2 position)
    {
        // First, check distance to each attachment point
        float rightDist = rightAttachPoint != null ?
            Vector2.Distance(position, rightAttachPoint.position) : float.MaxValue;

        float leftDist = leftAttachPoint != null ?
            Vector2.Distance(position, leftAttachPoint.position) : float.MaxValue;

        float topDist = topAttachPoint != null ?
            Vector2.Distance(position, topAttachPoint.position) : float.MaxValue;

        // Find the closest attachment point
        if (rightDist < leftDist && rightDist < topDist && rightDist < validAttachDistance)
        {
            return AttachmentSide.Right;
        }
        else if (leftDist < rightDist && leftDist < topDist && leftDist < validAttachDistance)
        {
            return AttachmentSide.Left;
        }
        else if (topDist < rightDist && topDist < leftDist && topDist < validAttachDistance)
        {
            return AttachmentSide.Top;
        }

        // Legacy method as a fallback
        Vector2 relativePos = position - (Vector2)transform.position;
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

    // New method to detach a specific item and its dependents from a specific side
    public void DetachItem(GameObject item)
    {
        if (!IsAttachedToAnyList(item)) return;

        // COMPLETELY FREEZE THE ROBOT FOR A MOMENT
        if (robotController != null)
        {
            // Use our new freezing method - freeze for 0.3 seconds
            robotController.FreezePositionForAttachment(0.3f);
        }

        // Get the side this item belongs to
        AttachmentSide side = AttachmentSide.Custom;
        if (itemToSideMap.TryGetValue(item, out side))
        {
            // Find all items on this side that depend on this one
            List<GameObject> dependents = FindDependentItemsOnSide(item, side);

            // First detach all dependents
            foreach (GameObject dependent in dependents)
            {
                DetachSingleItem(dependent);
            }

            // Then detach the item itself
            DetachSingleItem(item);
        }
        else
        {
            // Fallback to old method if side isn't mapped
            List<GameObject> dependents = FindDependentItems(item);

            // First detach all dependents
            foreach (GameObject dependent in dependents)
            {
                List<GameObject> packageList = FindContainingList(dependent);
                if (packageList != null)
                {
                    DetachSingleItem(dependent);
                }
            }

            // Then detach the item itself
            List<GameObject> itemList = FindContainingList(item);
            if (itemList != null)
            {
                DetachSingleItem(item);
            }
        }

        StartCoroutine(AttachCooldown());
    }

    // Find all items that depend on a given item on the same side
    private List<GameObject> FindDependentItemsOnSide(GameObject item, AttachmentSide side)
    {
        List<GameObject> dependents = new List<GameObject>();

        // Get all items on this side
        List<GameObject> sideItems = GetPackageList(side);

        // Check which items in this side are dependent on the target item
        foreach (GameObject sideItem in sideItems)
        {
            if (sideItem == item) continue; // Skip the item itself

            // See if this item is connected to our target item
            GameObject connectedTo;
            if (attachmentConnections.TryGetValue(sideItem, out connectedTo) && connectedTo == item)
            {
                dependents.Add(sideItem);

                // Recursively find items dependent on this dependent
                List<GameObject> subDependents = FindDependentItemsOnSide(sideItem, side);
                dependents.AddRange(subDependents);
            }
        }

        return dependents;
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

    // Detach a single item without affecting dependencies directly
    private void DetachSingleItem(GameObject item)
    {
        // Find which package list contains this item
        List<GameObject> packageList = FindContainingList(item);
        if (packageList == null) return;

        // Remove from the package list
        packageList.Remove(item);

        // Remove from connection tracking
        attachmentConnections.Remove(item);

        // Remove from side mapping
        itemToSideMap.Remove(item);

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

        // Play detach audio
        oneSounds.PlayDetachAudio();
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
        switch (side)
        {
            case AttachmentSide.Right:
                return rightAttachPoint != null ? (Vector2)rightAttachPoint.position :
                    (Vector2)transform.position + GetLegacyAttachOffset(side);

            case AttachmentSide.Left:
                return leftAttachPoint != null ? (Vector2)leftAttachPoint.position :
                    (Vector2)transform.position + GetLegacyAttachOffset(side);

            case AttachmentSide.Top:
                return topAttachPoint != null ? (Vector2)topAttachPoint.position :
                    (Vector2)transform.position + GetLegacyAttachOffset(side);

            default:
                return (Vector2)transform.position + GetLegacyAttachOffset(side);
        }
    }

    // Legacy method to get offset from robot center
    private Vector2 GetLegacyAttachOffset(AttachmentSide side)
    {
        Vector2 basePos = Vector2.zero;
        Vector2 halfSize = GetVisualBoundsSize() / 2f;

        return side switch
        {
            AttachmentSide.Right => new Vector2(halfSize.x + attachRange, 0),
            AttachmentSide.Left => new Vector2(-(halfSize.x + attachRange), 0),
            AttachmentSide.Top => new Vector2(0, halfSize.y + attachRange),
            _ => Vector2.zero
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
        if (WouldCollideAtPosition(item, attachCenter, side) ||
            WouldCauseExcessiveOverlap(item, attachCenter, side))
        {
            Debug.LogWarning("Collision or excessive overlap detected during final attachment check!");
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

        // Track which side this item belongs to
        itemToSideMap[item] = side;

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

        // Use our new detach method which only affects the specific item and its dependents
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
        // Determine which side we're trying to attach to
        AttachmentSide side = DetermineSideFromPosition(position);

        // Check if connected to an item on the same side or the appropriate attachment point
        GameObject connectedTo = FindConnectedItemOnSide(position, side);
        bool isConnected = connectedTo != null;

        // Check for collisions and excessive overlap
        bool wouldCollide = WouldCollideAtPosition(item, position, side);
        bool wouldOverlap = WouldCauseExcessiveOverlap(item, position, side);

        return isConnected && !wouldCollide && !wouldOverlap;
    }

    // Check if a position is close enough to the robot or an attached package
    private bool IsConnectedToRobotOrPackage(Vector2 position)
    {
        // Check proximity to specific attachment points
        if (rightAttachPoint != null &&
            Vector2.Distance(position, rightAttachPoint.position) < validAttachDistance)
            return true;

        if (leftAttachPoint != null &&
            Vector2.Distance(position, leftAttachPoint.position) < validAttachDistance)
            return true;

        if (topAttachPoint != null &&
            Vector2.Distance(position, topAttachPoint.position) < validAttachDistance)
            return true;

        // Legacy checks

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
        // Draw attachment points
        float pointSize = 0.15f;

        if (rightAttachPoint != null)
        {
            Gizmos.color = rightPackages.Count > 0 ? Color.red : Color.green;
            Gizmos.DrawSphere(rightAttachPoint.position, pointSize);
        }

        if (leftAttachPoint != null)
        {
            Gizmos.color = leftPackages.Count > 0 ? Color.red : Color.green;
            Gizmos.DrawSphere(leftAttachPoint.position, pointSize);
        }

        if (topAttachPoint != null)
        {
            Gizmos.color = topPackages.Count > 0 ? Color.red : Color.green;
            Gizmos.DrawSphere(topAttachPoint.position, pointSize);
        }

        // Draw detection bounds
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