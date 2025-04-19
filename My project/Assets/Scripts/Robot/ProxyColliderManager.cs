using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages proxy colliders for attached items.
/// This component should be added to the robot alongside AttachmentHandler.
/// </summary>
public class ProxyColliderManager : MonoBehaviour
{
    [Header("References")]
    public AttachmentHandler attachmentHandler;

    [Header("Proxy Settings")]
    public LayerMask environmentLayer;
    public string proxyColliderTag = "ProxyCollider";

    private List<GameObject> pendingProxyItems = new List<GameObject>();
    private List<float> pendingProxyTimers = new List<float>();
    private float proxyCreationDelay = 0.3f; // Match this with the freeze duration

    // Dictionary of original items to their proxy colliders
    private Dictionary<GameObject, GameObject> itemToProxyMap = new Dictionary<GameObject, GameObject>();

    void Start()
    {
        if (attachmentHandler == null)
        {
            attachmentHandler = GetComponent<AttachmentHandler>();
            if (attachmentHandler == null)
            {
                Debug.LogError("ProxyColliderManager requires an AttachmentHandler component!");
                enabled = false;
                return;
            }
        }

        // Clear any proxies that might have been left over
        CleanupAllProxies();
    }

    void OnDisable()
    {
        // Clean up all proxies when disabled
        CleanupAllProxies();
    }

    void CleanupAllProxies()
    {
        foreach (var pair in itemToProxyMap)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value);
            }
        }
        itemToProxyMap.Clear();
    }

    // MODIFY YOUR LATEUPDATE METHOD
    void LateUpdate()
    {
        // First handle any pending proxy creations
        for (int i = pendingProxyTimers.Count - 1; i >= 0; i--)
        {
            pendingProxyTimers[i] -= Time.deltaTime;

            if (pendingProxyTimers[i] <= 0)
            {
                // Time to create this proxy
                CreateProxyForItem(pendingProxyItems[i]);

                // Remove from pending lists
                pendingProxyItems.RemoveAt(i);
                pendingProxyTimers.RemoveAt(i);
            }
        }

        // Get all currently attached items
        List<GameObject> attachedItems = attachmentHandler.GetAllAttachedItems();

        // Check for new items (not already in map or pending)
        foreach (GameObject item in attachedItems)
        {
            if (item == null) continue;

            if (!itemToProxyMap.ContainsKey(item) && !pendingProxyItems.Contains(item))
            {
                // Instead of creating immediately, add to pending list
                pendingProxyItems.Add(item);
                pendingProxyTimers.Add(proxyCreationDelay);
            }
        }

        // The rest of your existing code for removing proxies
        List<GameObject> itemsToRemove = new List<GameObject>();
        foreach (var pair in itemToProxyMap)
        {
            if (pair.Key == null || !attachedItems.Contains(pair.Key))
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
                itemsToRemove.Add(pair.Key);
            }
        }

        // Clean up the dictionary
        foreach (GameObject key in itemsToRemove)
        {
            itemToProxyMap.Remove(key);
        }
    }

    private void CreateProxyForItem(GameObject item)
    {
        if (item == null) return;

        // Create a new GameObject for the proxy
        GameObject proxy = new GameObject($"Proxy_{item.name}");
        proxy.tag = proxyColliderTag;

        // Parent it to the robot
        proxy.transform.parent = transform;
        proxy.transform.position = item.transform.position;
        proxy.transform.rotation = item.transform.rotation;

        // We don't need a rigidbody for triggers!

        // Copy all colliders from the item to the proxy
        Collider2D[] itemColliders = item.GetComponents<Collider2D>();
        bool addedAtLeastOneCollider = false;

        foreach (Collider2D col in itemColliders)
        {
            // Copy the collider properties
            Collider2D proxyCol = null;

            if (col is BoxCollider2D)
            {
                BoxCollider2D boxCol = col as BoxCollider2D;
                BoxCollider2D proxyBoxCol = proxy.AddComponent<BoxCollider2D>();
                proxyBoxCol.size = boxCol.size;
                proxyBoxCol.offset = boxCol.offset;
                proxyCol = proxyBoxCol;
                addedAtLeastOneCollider = true;
            }
            else if (col is CircleCollider2D)
            {
                CircleCollider2D circleCol = col as CircleCollider2D;
                CircleCollider2D proxyCircleCol = proxy.AddComponent<CircleCollider2D>();
                proxyCircleCol.radius = circleCol.radius;
                proxyCircleCol.offset = circleCol.offset;
                proxyCol = proxyCircleCol;
                addedAtLeastOneCollider = true;
            }
            else if (col is PolygonCollider2D)
            {
                PolygonCollider2D polyCol = col as PolygonCollider2D;
                PolygonCollider2D proxyPolyCol = proxy.AddComponent<PolygonCollider2D>();
                proxyPolyCol.points = polyCol.points;
                proxyCol = proxyPolyCol;
                addedAtLeastOneCollider = true;
            }

            if (proxyCol != null)
            {
                // CRITICAL CHANGE: Make it a trigger!
                proxyCol.isTrigger = true;

                // Use the obstacle layer to ensure we detect collisions with obstacles
                proxyCol.gameObject.layer = LayerMask.NameToLayer("Default");
            }
        }

        // If we didn't add any colliders, add a default box collider based on sprite size
        if (!addedAtLeastOneCollider)
        {
            SpriteRenderer sr = item.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                BoxCollider2D defaultCollider = proxy.AddComponent<BoxCollider2D>();
                defaultCollider.size = sr.bounds.size;
                defaultCollider.offset = Vector2.zero;
                defaultCollider.isTrigger = true;
            }
        }

        // Add the ProxyPositionSync script
        ProxyPositionSync posSync = proxy.AddComponent<ProxyPositionSync>();
        posSync.targetTransform = item.transform;

        // Add a CollisionReporter
        CollisionReporter reporter = proxy.AddComponent<CollisionReporter>();
        reporter.robotController = GetComponent<RobotController>();

        // Store the proxy in the map
        itemToProxyMap[item] = proxy;
    }
}