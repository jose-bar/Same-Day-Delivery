using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the drop mode for selecting which item to detach
/// </summary>
public class DropModeManager : MonoBehaviour
{
    [Header("References")]
    public AttachmentHandler attachmentHandler; // Make this public and assignable in inspector

    [Header("Drop Mode Settings")]
    public Color highlightColor = new Color(1f, 0.5f, 0f, 0.8f); // Orange highlight
    public float cycleDelay = 0.15f; // Cooldown between selections

    private bool isInDropMode = false;
    private GameObject highlightedItem = null;
    private bool canCycle = true;

    void Awake()
    {
        // Try to find the attachment handler if not assigned
        if (attachmentHandler == null)
        {
            attachmentHandler = GetComponent<AttachmentHandler>();

            // If still null, try to find in parent/children
            if (attachmentHandler == null)
            {
                attachmentHandler = GetComponentInParent<AttachmentHandler>();
            }

            if (attachmentHandler == null)
            {
                attachmentHandler = GetComponentInChildren<AttachmentHandler>();
            }
        }
    }

    void Start()
    {
        // Double-check in Start() to make sure the handler is available
        if (attachmentHandler == null)
        {
            // One more attempt to find it anywhere in the scene
            attachmentHandler = FindObjectOfType<AttachmentHandler>();

            // If still null, log error but don't disable (allow runtime assignment)
            if (attachmentHandler == null)
            {
                Debug.LogError("DropModeManager requires an AttachmentHandler component. Please assign one in the inspector.");
            }
        }
    }

    public bool IsInDropMode()
    {
        return isInDropMode;
    }

    public void EnterDropMode()
    {
        if (isInDropMode) return;
        if (attachmentHandler == null) return; // Safety check

        List<GameObject> attachedItems = attachmentHandler.GetAllAttachedItems();
        if (attachedItems.Count == 0) return;

        isInDropMode = true;

        // Highlight the first attached item
        highlightedItem = attachedItems[0];
        HighlightItem(highlightedItem);
    }

    public void ExitDropMode(bool confirmDrop)
    {
        if (!isInDropMode) return;
        if (attachmentHandler == null) return; // Safety check

        if (confirmDrop && highlightedItem != null)
        {
            // Detach the highlighted item and its dependents
            attachmentHandler.DetachItem(highlightedItem);
        }

        // Remove highlights
        if (highlightedItem != null)
        {
            RemoveHighlight(highlightedItem);
        }

        isInDropMode = false;
        highlightedItem = null;
    }

    public void CycleSelection(bool forward = true)
    {
        if (!isInDropMode || !canCycle || attachmentHandler == null) return;

        List<GameObject> attachedItems = attachmentHandler.GetAllAttachedItems();
        if (attachedItems.Count == 0) return;

        // Remove current highlight
        if (highlightedItem != null)
        {
            RemoveHighlight(highlightedItem);
        }

        // Find current index
        int currentIndex = attachedItems.IndexOf(highlightedItem);
        int nextIndex;

        if (forward)
        {
            nextIndex = (currentIndex + 1) % attachedItems.Count;
        }
        else
        {
            nextIndex = (currentIndex - 1 + attachedItems.Count) % attachedItems.Count;
        }

        // Set new highlighted item
        highlightedItem = attachedItems[nextIndex];
        HighlightItem(highlightedItem);

        // Apply cooldown
        StartCoroutine(CycleCooldown());
    }

    void HighlightItem(GameObject item)
    {
        if (item == null) return;

        // Get or add ItemHighlighter component
        ItemHighlighter highlighter = item.GetComponent<ItemHighlighter>();
        if (highlighter == null)
        {
            highlighter = item.AddComponent<ItemHighlighter>();
        }

        // Start pulsing highlight
        highlighter.StartPulsingHighlight(highlightColor);

        // Also highlight dependents with a different color
        List<GameObject> dependents = FindAllDependents(item);
        Color dependentColor = new Color(highlightColor.r * 0.7f, highlightColor.g * 0.7f, highlightColor.b * 0.7f, highlightColor.a * 0.7f);

        foreach (GameObject dependent in dependents)
        {
            ItemHighlighter depHighlighter = dependent.GetComponent<ItemHighlighter>();
            if (depHighlighter == null)
            {
                depHighlighter = dependent.AddComponent<ItemHighlighter>();
            }

            depHighlighter.StartHighlight(dependentColor);
        }
    }

    void RemoveHighlight(GameObject item)
    {
        if (item == null) return;

        // Remove highlight from item
        ItemHighlighter highlighter = item.GetComponent<ItemHighlighter>();
        if (highlighter != null)
        {
            highlighter.StopHighlight();
        }

        // Also remove from dependents
        List<GameObject> dependents = FindAllDependents(item);
        foreach (GameObject dependent in dependents)
        {
            ItemHighlighter depHighlighter = dependent.GetComponent<ItemHighlighter>();
            if (depHighlighter != null)
            {
                depHighlighter.StopHighlight();
            }
        }
    }

    // Helper method to find all dependents (will need a reference to attachmentHandler)
    private List<GameObject> FindAllDependents(GameObject item)
    {
        if (attachmentHandler == null) return new List<GameObject>();

        // This is just a placeholder - in the real implementation,
        // we'd use the attachmentHandler's dependency tracking
        List<GameObject> dependents = new List<GameObject>();

        // This mimics the process of finding dependencies
        // Ideally would be integrated with the actual dependency system
        foreach (Transform child in transform)
        {
            if (child.gameObject != item && attachmentHandler.IsAttachedToAnyList(child.gameObject))
            {
                // This would be replaced with actual dependency checking
                float distance = Vector2.Distance(child.position, item.transform.position);
                if (distance < 0.75f)
                {
                    dependents.Add(child.gameObject);
                }
            }
        }

        return dependents;
    }

    IEnumerator CycleCooldown()
    {
        canCycle = false;
        yield return new WaitForSeconds(cycleDelay);
        canCycle = true;
    }

    void OnDrawGizmos()
    {
        if (isInDropMode && highlightedItem != null)
        {
            // Draw a highlight around the selected item
            Gizmos.color = highlightColor;
            Renderer renderer = highlightedItem.GetComponent<Renderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size * 1.1f);
            }

            // Draw lines to dependents
            List<GameObject> dependents = FindAllDependents(highlightedItem);
            Gizmos.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0.5f);

            foreach (GameObject dependent in dependents)
            {
                Gizmos.DrawLine(highlightedItem.transform.position, dependent.transform.position);
            }
        }
    }
}