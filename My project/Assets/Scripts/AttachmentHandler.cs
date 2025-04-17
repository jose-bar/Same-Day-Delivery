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

    // Constant to identify physic colliders we create
    public const string ATTACHMENT_COLLIDER_TAG = "AttachmentCollider";

    private List<GameObject> rightPackages = new List<GameObject>();
    private List<GameObject> leftPackages = new List<GameObject>();
    private List<GameObject> topPackages = new List<GameObject>();
    private Dictionary<GameObject, Collider2D> itemColliders = new Dictionary<GameObject, Collider2D>();
    private Dictionary<GameObject, Rigidbody2D> itemRigidbodies = new Dictionary<GameObject, Rigidbody2D>();

    private bool canToggleAttach = true;
    private bool hasPackageInRange = false;

    // Debug properties
    private Vector2 lastAttachPosition;
    private bool showDebugRays = true;
    private float debugRayDuration = 2f;

    private RobotController robotController;

    public enum AttachmentSide { Right, Left, Top }

    // Sound Effects
    OneSoundEffects oneSounds;

    void Start()
    {
        // Get a reference to the robot controller
        robotController = GetComponent<RobotController>();
        oneSounds = GetComponentInParent<OneSoundEffects>();
    }

    public void ToggleAttachment(AttachmentSide side)
    {
        if (!canToggleAttach) return;
        List<GameObject> packageList = GetPackageList(side);

        // Detach logic if we already have packages attached
        if (packageList.Count >= 1)
        {
            DetachLastItem(packageList);

            // Play detach audio
            oneSounds.PlayDetachAudio();
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

                    // Play attach audio on success
                    oneSounds.PlayAttachAudio();

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
        packageList.RemoveAt(packageList.Count - 1);

        // IMPORTANT: Make sure the item is properly removed from all other attachment lists as well
        // This ensures the IsAttachedToAnyList check will work properly later
        rightPackages.Remove(last);
        leftPackages.Remove(last);
        topPackages.Remove(last);

        // Reset layer to the original Package layer
        last.gameObject.layer = LayerMask.NameToLayer("Package");

        // Reset tag - VERY important for re-attachment
        last.tag = "Untagged";

        // Re-enable the original rigidbody
        if (itemRigidbodies.ContainsKey(last))
        {
            Rigidbody2D rb = itemRigidbodies[last];
            if (rb != null) rb.simulated = true;
            itemRigidbodies.Remove(last);

            // Reset transform parent and apply a small detachment force
            last.transform.SetParent(null);

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
        else
        {
            // If no rigidbody was stored, just detach it
            last.transform.SetParent(null);
        }

        // Reset any colliders we might have modified
        Collider2D[] itemColliders = last.GetComponents<Collider2D>();
        foreach (Collider2D col in itemColliders)
        {
            col.enabled = true;
        }

        // Make sure any proxy is cleaned up immediately
        // This will be handled by ProxyColliderManager in LateUpdate
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