using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the drop mode for selecting which item to detach
/// </summary>
public class DropModeManager : MonoBehaviour
{
    [Header("References")]
    public AttachmentHandler attachmentHandler;

    [Header("Drop Mode Settings")]
    public Color primaryHighlightColor = new Color(1f, 0.5f, 0f, 0.8f); // Orange highlight
    public Color dependentHighlightColor = new Color(1f, 0.3f, 0f, 0.6f); // Darker orange
    public float cycleDelay = 0.15f; // Cooldown between selections

    private bool isInDropMode = false;
    private GameObject highlightedItem = null;
    private bool canCycle = true;
    private List<GameObject> currentHighlightedDependents = new List<GameObject>();

    void Awake()
    {
        if (attachmentHandler == null)
        {
            attachmentHandler = GetComponent<AttachmentHandler>();
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
        if (attachmentHandler == null)
        {
            attachmentHandler = FindObjectOfType<AttachmentHandler>();
            if (attachmentHandler == null)
            {
                Debug.LogError("DropModeManager requires an AttachmentHandler component");
                enabled = false;
                return;
            }
        }
    }

    public bool IsInDropMode()
    {
        return isInDropMode;
    }

    public void EnterDropMode()
    {
        if (isInDropMode || attachmentHandler == null) return;

        List<GameObject> attachedItems = attachmentHandler.GetAllAttachedItems();
        if (attachedItems.Count == 0) return;

        isInDropMode = true;

        // Find a good starting item (preferably one with dependents)
        GameObject bestStartItem = FindBestStartingItem(attachedItems);
        highlightedItem = bestStartItem ?? attachedItems[0];

        // Highlight the selected item and its dependents
        HighlightItemWithDependents(highlightedItem);
    }

    private GameObject FindBestStartingItem(List<GameObject> items)
    {
        // Start with an item that has dependents if possible
        foreach (GameObject item in items)
        {
            if (item == null) continue;

            HashSet<GameObject> processed = new HashSet<GameObject>();
            List<GameObject> dependents = FindDependentItems(item, processed);
            if (dependents.Count > 0)
            {
                return item;
            }
        }
        return null; // No item with dependents found
    }

    public void ExitDropMode(bool confirmDrop)
    {
        if (!isInDropMode) return;

        if (confirmDrop && highlightedItem != null && attachmentHandler != null)
        {
            // Detach the highlighted item and its dependents
            attachmentHandler.DetachItem(highlightedItem);

            // Play a sound effect if available
            AudioSource audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.Play();
            }
        }

        // Remove all highlights
        RemoveAllHighlights();

        isInDropMode = false;
        highlightedItem = null;
        currentHighlightedDependents.Clear();
    }

    public void CycleSelection(bool forward = true)
    {
        if (!isInDropMode || !canCycle || attachmentHandler == null) return;

        List<GameObject> attachedItems = attachmentHandler.GetAllAttachedItems();
        if (attachedItems.Count <= 1) return; // Nothing to cycle through

        // Remove current highlights
        RemoveAllHighlights();

        // Find current index
        int currentIndex = attachedItems.IndexOf(highlightedItem);
        if (currentIndex < 0) currentIndex = 0;

        int nextIndex;
        if (forward)
        {
            nextIndex = (currentIndex + 1) % attachedItems.Count;
        }
        else
        {
            nextIndex = (currentIndex - 1 + attachedItems.Count) % attachedItems.Count;
        }

        // Set and highlight new item
        highlightedItem = attachedItems[nextIndex];
        HighlightItemWithDependents(highlightedItem);

        // Apply cooldown
        StartCoroutine(CycleCooldown());
    }

    private void HighlightItemWithDependents(GameObject item)
    {
        if (item == null) return;

        // Clear previous dependent highlights list
        currentHighlightedDependents.Clear();

        // Find dependents first - use a HashSet to track processed items and avoid infinite recursion
        HashSet<GameObject> processed = new HashSet<GameObject>();
        List<GameObject> dependents = FindDependentItems(item, processed);
        currentHighlightedDependents = dependents;

        // Highlight main item with pulsing effect
        HighlightMainItem(item);

        // Highlight all dependents with static color
        foreach (GameObject dependent in dependents)
        {
            if (dependent != null) // Safety check
            {
                HighlightDependentItem(dependent);
            }
        }
    }

    private void HighlightMainItem(GameObject item)
    {
        if (item == null) return;

        ItemHighlighter highlighter = GetOrAddHighlighter(item);
        highlighter.StartPulsingHighlight(primaryHighlightColor);
    }

    private void HighlightDependentItem(GameObject item)
    {
        if (item == null) return;

        ItemHighlighter highlighter = GetOrAddHighlighter(item);
        highlighter.StartHighlight(dependentHighlightColor);
    }

    private ItemHighlighter GetOrAddHighlighter(GameObject item)
    {
        if (item == null) return null;

        ItemHighlighter highlighter = item.GetComponent<ItemHighlighter>();
        if (highlighter == null)
        {
            highlighter = item.AddComponent<ItemHighlighter>();
        }
        return highlighter;
    }

    private void RemoveAllHighlights()
    {
        // Remove highlight from main item
        if (highlightedItem != null)
        {
            ItemHighlighter highlighter = highlightedItem.GetComponent<ItemHighlighter>();
            if (highlighter != null)
            {
                highlighter.StopHighlight();
            }
        }

        // Remove highlight from all dependents
        foreach (GameObject dependent in currentHighlightedDependents)
        {
            if (dependent != null)
            {
                ItemHighlighter highlighter = dependent.GetComponent<ItemHighlighter>();
                if (highlighter != null)
                {
                    highlighter.StopHighlight();
                }
            }
        }
    }

    // Find all items that depend on this one, with protection against infinite recursion
    private List<GameObject> FindDependentItems(GameObject item, HashSet<GameObject> processed)
    {
        if (item == null || attachmentHandler == null)
            return new List<GameObject>();

        // Add this item to the processed set to avoid revisiting it (prevents infinite recursion)
        processed.Add(item);

        List<GameObject> dependents = new List<GameObject>();
        List<GameObject> allAttached = attachmentHandler.GetAllAttachedItems();

        foreach (GameObject attached in allAttached)
        {
            // Skip null, self, or already processed items
            if (attached == null || attached == item || processed.Contains(attached))
                continue;

            // This is a simplified dependency check - in reality, you'd use the 
            // attachmentHandler's dependency tracking system
            float distance = Vector2.Distance(attached.transform.position, item.transform.position);
            if (distance < attachmentHandler.validAttachDistance)
            {
                // This could be a dependent - check if it's closer to this item than to the robot
                float distToRobot = Vector2.Distance(attached.transform.position, transform.position);

                if (distance < distToRobot)
                {
                    dependents.Add(attached);

                    // Mark this item as processed to avoid checking it again
                    processed.Add(attached);

                    // Recursively find items dependent on this dependent
                    List<GameObject> subDependents = FindDependentItems(attached, processed);
                    foreach (GameObject subDependent in subDependents)
                    {
                        if (subDependent != null && !dependents.Contains(subDependent))
                        {
                            dependents.Add(subDependent);
                        }
                    }
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
        if (!isInDropMode || highlightedItem == null) return;

        // Draw a highlight around the selected item
        Gizmos.color = primaryHighlightColor;
        Renderer renderer = highlightedItem.GetComponent<Renderer>();
        if (renderer != null)
        {
            Bounds bounds = renderer.bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size * 1.1f);
        }

        // Draw connecting lines to all dependents
        Gizmos.color = dependentHighlightColor;
        foreach (GameObject dependent in currentHighlightedDependents)
        {
            if (dependent != null)
            {
                DrawDependencyLines(highlightedItem, dependent);
            }
        }
    }

    // Draw lines showing dependency relationships
    private void DrawDependencyLines(GameObject root, GameObject dependent)
    {
        if (root == null || dependent == null) return;

        // Draw a line from the root to this dependent
        Gizmos.DrawLine(root.transform.position, dependent.transform.position);

        // Draw an arrow tip to show direction
        Vector3 direction = (dependent.transform.position - root.transform.position).normalized;
        Vector3 arrowPos = dependent.transform.position - direction * 0.2f;
        Vector3 right = Vector3.Cross(direction, Vector3.forward).normalized * 0.1f;

        Gizmos.DrawLine(arrowPos, dependent.transform.position);
        Gizmos.DrawLine(arrowPos, arrowPos + right - direction * 0.1f);
        Gizmos.DrawLine(arrowPos, arrowPos - right - direction * 0.1f);
    }
}