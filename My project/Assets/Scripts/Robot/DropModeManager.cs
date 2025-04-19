using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the drop mode for selecting which item to detach using directional controls
/// </summary>
public class DropModeManager : MonoBehaviour
{
    [Header("References")]
    public AttachmentHandler attachmentHandler;

    [Header("Drop Mode Settings")]
    public float cycleDelay = 0.15f; // Cooldown between selections

    [Header("Side-Specific Settings")]
    public Color rightSideColor = new Color(1f, 0.5f, 0f, 0.8f); // Orange for right
    public Color leftSideColor = new Color(0f, 0.7f, 1f, 0.8f);  // Blue for left
    public Color topSideColor = new Color(0.5f, 1f, 0f, 0.8f);   // Green for top

    [Header("Debug")]
    public bool showDebugLogs = false;

    private bool isInDropMode = false;
    private GameObject highlightedItem = null;
    private bool canCycle = true;
    private List<GameObject> currentHighlightedDependents = new List<GameObject>();
    private Dictionary<AttachmentHandler.AttachmentSide, int> currentSideIndices = new Dictionary<AttachmentHandler.AttachmentSide, int>();

    // Track the current active side
    private AttachmentHandler.AttachmentSide currentActiveSide = AttachmentHandler.AttachmentSide.Right;

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

        // Initialize the side indices
        currentSideIndices[AttachmentHandler.AttachmentSide.Right] = 0;
        currentSideIndices[AttachmentHandler.AttachmentSide.Left] = 0;
        currentSideIndices[AttachmentHandler.AttachmentSide.Top] = 0;
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

        // Get counts for each side
        List<GameObject> rightItems = attachmentHandler.GetPackageList(AttachmentHandler.AttachmentSide.Right);
        List<GameObject> leftItems = attachmentHandler.GetPackageList(AttachmentHandler.AttachmentSide.Left);
        List<GameObject> topItems = attachmentHandler.GetPackageList(AttachmentHandler.AttachmentSide.Top);

        // Check if we have any attached items at all
        int totalItems = rightItems.Count + leftItems.Count + topItems.Count;
        if (totalItems == 0) return;

        if (showDebugLogs)
        {
            Debug.Log($"Drop Mode: Right items: {rightItems.Count}, Left items: {leftItems.Count}, Top items: {topItems.Count}");
        }

        // Reset all indices
        currentSideIndices[AttachmentHandler.AttachmentSide.Right] = 0;
        currentSideIndices[AttachmentHandler.AttachmentSide.Left] = 0;
        currentSideIndices[AttachmentHandler.AttachmentSide.Top] = 0;

        // Now we're entering drop mode
        isInDropMode = true;

        // Find first side with items to start with
        if (rightItems.Count > 0)
        {
            currentActiveSide = AttachmentHandler.AttachmentSide.Right;
            highlightedItem = rightItems[0];
        }
        else if (leftItems.Count > 0)
        {
            currentActiveSide = AttachmentHandler.AttachmentSide.Left;
            highlightedItem = leftItems[0];
        }
        else if (topItems.Count > 0)
        {
            currentActiveSide = AttachmentHandler.AttachmentSide.Top;
            highlightedItem = topItems[0];
        }

        if (showDebugLogs)
        {
            Debug.Log($"Drop Mode: Starting with side {currentActiveSide} and item {highlightedItem.name}");
        }

        // Highlight the selected item and its dependents
        HighlightItemWithDependents(highlightedItem);
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
        // This method is now only used for down arrow (cycle back)
        // or when it's called directly from code without specifying a side

        if (!isInDropMode || !canCycle || attachmentHandler == null) return;

        // Use the active side and cycle within it
        CycleWithinSide(currentActiveSide, forward);
    }

    // Handle direction-specific input in the RobotController
    public void HandleDropModeInput()
    {
        if (!isInDropMode || !canCycle) return;

        // Left Arrow = cycle left attachments
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            SelectSide(AttachmentHandler.AttachmentSide.Left);
        }
        // Right Arrow = cycle right attachments
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            SelectSide(AttachmentHandler.AttachmentSide.Right);
        }
        // Up Arrow = cycle top attachments
        else if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            SelectSide(AttachmentHandler.AttachmentSide.Top);
        }
        // Down Arrow = cycle selected side backward
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            CycleWithinSide(currentActiveSide, false);
        }
    }

    // Select and highlight a specific side
    private void SelectSide(AttachmentHandler.AttachmentSide side)
    {
        List<GameObject> sideItems = attachmentHandler.GetPackageList(side);

        if (sideItems.Count == 0)
        {
            // No items on this side, play feedback
            AudioSource audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.Play();
            }

            if (showDebugLogs)
            {
                Debug.Log($"No items on {side} side");
            }

            return;
        }

        // If we're already on this side, cycle to the next item
        if (side == currentActiveSide)
        {
            CycleWithinSide(side, true);
            return;
        }

        // Switch to this side
        currentActiveSide = side;

        // Get the current index for this side
        int index = currentSideIndices[side];

        // Make sure the index is valid
        if (index >= sideItems.Count)
        {
            index = 0;
            currentSideIndices[side] = 0;
        }

        // Clear old highlights
        RemoveAllHighlights();

        // Highlight the item
        highlightedItem = sideItems[index];
        HighlightItemWithDependents(highlightedItem);

        if (showDebugLogs)
        {
            Debug.Log($"Selected {side} side, item {index}: {highlightedItem.name}");
        }

        // Apply cooldown
        StartCoroutine(CycleCooldown());
    }

    // Cycle within a specific side
    private void CycleWithinSide(AttachmentHandler.AttachmentSide side, bool forward)
    {
        List<GameObject> sideItems = attachmentHandler.GetPackageList(side);

        if (sideItems.Count <= 1)
        {
            // Nothing to cycle through
            return;
        }

        // Get the current index
        int currentIndex = currentSideIndices[side];

        // Calculate next index
        int nextIndex;
        if (forward)
        {
            nextIndex = (currentIndex + 1) % sideItems.Count;
        }
        else
        {
            nextIndex = (currentIndex - 1 + sideItems.Count) % sideItems.Count;
        }

        // Update the index
        currentSideIndices[side] = nextIndex;

        // Clear old highlights
        RemoveAllHighlights();

        // Highlight the new item
        highlightedItem = sideItems[nextIndex];
        HighlightItemWithDependents(highlightedItem);

        if (showDebugLogs)
        {
            Debug.Log($"Cycled within {side} side from {currentIndex} to {nextIndex}: {highlightedItem.name}");
        }

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

        // Get highlight color based on the side
        Color highlightColor = GetSideColor(currentActiveSide);

        // Highlight main item with pulsing effect
        HighlightMainItem(item, highlightColor);

        // Highlight all dependents with static color
        foreach (GameObject dependent in dependents)
        {
            if (dependent != null) // Safety check
            {
                HighlightDependentItem(dependent, highlightColor * 0.8f); // Slightly darker
            }
        }
    }

    // Get color based on attachment side
    private Color GetSideColor(AttachmentHandler.AttachmentSide side)
    {
        switch (side)
        {
            case AttachmentHandler.AttachmentSide.Right:
                return rightSideColor;
            case AttachmentHandler.AttachmentSide.Left:
                return leftSideColor;
            case AttachmentHandler.AttachmentSide.Top:
                return topSideColor;
            default:
                return Color.white;
        }
    }

    private void HighlightMainItem(GameObject item, Color highlightColor)
    {
        if (item == null) return;

        ItemHighlighter highlighter = GetOrAddHighlighter(item);
        highlighter.StartPulsingHighlight(highlightColor);
    }

    private void HighlightDependentItem(GameObject item, Color highlightColor)
    {
        if (item == null) return;

        ItemHighlighter highlighter = GetOrAddHighlighter(item);
        highlighter.StartHighlight(highlightColor);
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

        // Get the side this item belongs to
        AttachmentHandler.AttachmentSide itemSide;
        if (attachmentHandler.itemToSideMap.TryGetValue(item, out itemSide))
        {
            // Get all items on this side
            List<GameObject> sideItems = attachmentHandler.GetPackageList(itemSide);

            // Find items on this side that depend on our target item
            foreach (GameObject sideItem in sideItems)
            {
                // Skip null, self, or already processed items
                if (sideItem == null || sideItem == item || processed.Contains(sideItem))
                    continue;

                // Check the connection
                GameObject connectedTo;
                if (attachmentHandler.attachmentConnections.TryGetValue(sideItem, out connectedTo) &&
                    connectedTo == item)
                {
                    dependents.Add(sideItem);

                    // Mark as processed to avoid cycles
                    processed.Add(sideItem);

                    // Recursively find sub-dependents
                    List<GameObject> subDependents = FindDependentItems(sideItem, processed);
                    dependents.AddRange(subDependents);
                }
            }
        }
        else
        {
            // Fallback to a more general approach
            List<GameObject> allAttached = attachmentHandler.GetAllAttachedItems();

            foreach (GameObject attached in allAttached)
            {
                // Skip null, self, or already processed items
                if (attached == null || attached == item || processed.Contains(attached))
                    continue;

                // This is a simplified dependency check
                GameObject connectedTo;
                if (attachmentHandler.attachmentConnections.TryGetValue(attached, out connectedTo) &&
                    connectedTo == item)
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

        // Get side color
        Color sideColor = GetSideColor(currentActiveSide);

        // Draw a highlight around the selected item
        Gizmos.color = sideColor;
        Renderer renderer = highlightedItem.GetComponent<Renderer>();
        if (renderer != null)
        {
            Bounds bounds = renderer.bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size * 1.1f);
        }

        // Draw connecting lines to all dependents
        Gizmos.color = sideColor * 0.8f;
        foreach (GameObject dependent in currentHighlightedDependents)
        {
            if (dependent != null)
            {
                DrawDependencyLines(highlightedItem, dependent);
            }
        }

        // Draw a visual indication of each attachment point
        if (attachmentHandler != null)
        {
            // Right attachment point
            if (attachmentHandler.rightAttachPoint != null)
            {
                Gizmos.color = currentActiveSide == AttachmentHandler.AttachmentSide.Right ?
                    rightSideColor : rightSideColor * 0.5f;
                Gizmos.DrawSphere(attachmentHandler.rightAttachPoint.position, 0.15f);
            }

            // Left attachment point
            if (attachmentHandler.leftAttachPoint != null)
            {
                Gizmos.color = currentActiveSide == AttachmentHandler.AttachmentSide.Left ?
                    leftSideColor : leftSideColor * 0.5f;
                Gizmos.DrawSphere(attachmentHandler.leftAttachPoint.position, 0.15f);
            }

            // Top attachment point
            if (attachmentHandler.topAttachPoint != null)
            {
                Gizmos.color = currentActiveSide == AttachmentHandler.AttachmentSide.Top ?
                    topSideColor : topSideColor * 0.5f;
                Gizmos.DrawSphere(attachmentHandler.topAttachPoint.position, 0.15f);
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