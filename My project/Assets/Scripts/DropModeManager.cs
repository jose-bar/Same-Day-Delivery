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

    [Header("Side-Specific Settings")]
    public Color rightSideColor = new Color(1f, 0.5f, 0f, 0.8f); // Orange for right
    public Color leftSideColor = new Color(0f, 0.7f, 1f, 0.8f);  // Blue for left
    public Color topSideColor = new Color(0.5f, 1f, 0f, 0.8f);   // Green for top

    private bool isInDropMode = false;
    private GameObject highlightedItem = null;
    private bool canCycle = true;
    private List<GameObject> currentHighlightedDependents = new List<GameObject>();

    // Track the current side we're viewing
    private AttachmentHandler.AttachmentSide currentSide = AttachmentHandler.AttachmentSide.Right;

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

        // Start with right side by default
        currentSide = AttachmentHandler.AttachmentSide.Right;

        // Find items on the current side
        List<GameObject> sideItems = attachmentHandler.GetPackageList(currentSide);

        // If no items on this side, try another side
        if (sideItems.Count == 0)
        {
            currentSide = AttachmentHandler.AttachmentSide.Left;
            sideItems = attachmentHandler.GetPackageList(currentSide);

            if (sideItems.Count == 0)
            {
                currentSide = AttachmentHandler.AttachmentSide.Top;
                sideItems = attachmentHandler.GetPackageList(currentSide);
            }
        }

        // If we found items on any side, select the first one
        if (sideItems.Count > 0)
        {
            highlightedItem = FindBestStartingItem(sideItems);
            if (highlightedItem == null)
            {
                highlightedItem = sideItems[0]; // Fallback to first item
            }

            // Highlight the selected item and its dependents
            HighlightItemWithDependents(highlightedItem);
        }
        else
        {
            // No items found on any side (this shouldn't happen given earlier check)
            isInDropMode = false;
        }
    }

    private GameObject FindBestStartingItem(List<GameObject> items)
    {
        // First try to find an item directly connected to the robot
        foreach (GameObject item in items)
        {
            if (item == null) continue;

            GameObject connectedTo;
            if (attachmentHandler.attachmentConnections.TryGetValue(item, out connectedTo) &&
                connectedTo == gameObject)
            {
                return item;
            }
        }

        // If none found, try an item with dependents
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

        return null; // No best item found
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

        // Get items on the current side
        List<GameObject> sideItems = attachmentHandler.GetPackageList(currentSide);

        if (sideItems.Count <= 0)
        {
            // If no items on current side, try to switch sides
            if (forward)
            {
                // Try next side (right -> left -> top -> right)
                if (currentSide == AttachmentHandler.AttachmentSide.Right)
                    currentSide = AttachmentHandler.AttachmentSide.Left;
                else if (currentSide == AttachmentHandler.AttachmentSide.Left)
                    currentSide = AttachmentHandler.AttachmentSide.Top;
                else
                    currentSide = AttachmentHandler.AttachmentSide.Right;
            }
            else
            {
                // Try previous side (right -> top -> left -> right)
                if (currentSide == AttachmentHandler.AttachmentSide.Right)
                    currentSide = AttachmentHandler.AttachmentSide.Top;
                else if (currentSide == AttachmentHandler.AttachmentSide.Top)
                    currentSide = AttachmentHandler.AttachmentSide.Left;
                else
                    currentSide = AttachmentHandler.AttachmentSide.Right;
            }

            // Get items on the new side
            sideItems = attachmentHandler.GetPackageList(currentSide);

            // If we found items, select the first one
            if (sideItems.Count > 0)
            {
                // Remove current highlights
                RemoveAllHighlights();

                highlightedItem = sideItems[0];
                HighlightItemWithDependents(highlightedItem);

                // Apply cooldown
                StartCoroutine(CycleCooldown());
                return;
            }
            else
            {
                // If still no items, just return
                return;
            }
        }

        // If we have multiple items on this side, cycle through them
        if (sideItems.Count > 1)
        {
            // Remove current highlights
            RemoveAllHighlights();

            // Find current index
            int currentIndex = sideItems.IndexOf(highlightedItem);
            if (currentIndex < 0) currentIndex = 0;

            int nextIndex;
            if (forward)
            {
                nextIndex = (currentIndex + 1) % sideItems.Count;
            }
            else
            {
                nextIndex = (currentIndex - 1 + sideItems.Count) % sideItems.Count;
            }

            // Set and highlight new item
            highlightedItem = sideItems[nextIndex];
            HighlightItemWithDependents(highlightedItem);
        }
        else if (sideItems.Count == 1 && highlightedItem != sideItems[0])
        {
            // If we have exactly one item and it's not selected, select it
            RemoveAllHighlights();
            highlightedItem = sideItems[0];
            HighlightItemWithDependents(highlightedItem);
        }
        else
        {
            // If we can't cycle within this side, try to switch sides
            AttachmentHandler.AttachmentSide newSide;
            if (forward)
            {
                // Try next side (right -> left -> top -> right)
                newSide = currentSide == AttachmentHandler.AttachmentSide.Right ?
                    AttachmentHandler.AttachmentSide.Left :
                    (currentSide == AttachmentHandler.AttachmentSide.Left ?
                        AttachmentHandler.AttachmentSide.Top : AttachmentHandler.AttachmentSide.Right);
            }
            else
            {
                // Try previous side (right -> top -> left -> right)
                newSide = currentSide == AttachmentHandler.AttachmentSide.Right ?
                    AttachmentHandler.AttachmentSide.Top :
                    (currentSide == AttachmentHandler.AttachmentSide.Top ?
                        AttachmentHandler.AttachmentSide.Left : AttachmentHandler.AttachmentSide.Right);
            }

            // Get items on the new side
            List<GameObject> newSideItems = attachmentHandler.GetPackageList(newSide);

            // If we found items on the new side, switch to it
            if (newSideItems.Count > 0)
            {
                // Remove current highlights
                RemoveAllHighlights();

                // Update current side
                currentSide = newSide;

                // Select first item on the new side
                highlightedItem = newSideItems[0];
                HighlightItemWithDependents(highlightedItem);
            }
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
        Color highlightColor = GetSideColor(currentSide);

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
                return primaryHighlightColor;
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
        Color sideColor = GetSideColor(currentSide);

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

        // Draw a text label showing the current side
        if (attachmentHandler != null)
        {
            // Draw label at the appropriate attachment point
            Vector3 labelPos = Vector3.zero;

            switch (currentSide)
            {
                case AttachmentHandler.AttachmentSide.Right:
                    labelPos = attachmentHandler.rightAttachPoint != null ?
                        attachmentHandler.rightAttachPoint.position :
                        (Vector2)transform.position + Vector2.right * 0.7f;
                    break;
                case AttachmentHandler.AttachmentSide.Left:
                    labelPos = attachmentHandler.leftAttachPoint != null ?
                        attachmentHandler.leftAttachPoint.position :
                        (Vector2)transform.position + Vector2.left * 0.7f;
                    break;
                case AttachmentHandler.AttachmentSide.Top:
                    labelPos = attachmentHandler.topAttachPoint != null ?
                        attachmentHandler.topAttachPoint.position :
                        (Vector2)transform.position + Vector2.up * 0.7f;
                    break;
            }

            // Draw a circle at the point
            Gizmos.color = sideColor;
            Gizmos.DrawSphere(labelPos, 0.15f);
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