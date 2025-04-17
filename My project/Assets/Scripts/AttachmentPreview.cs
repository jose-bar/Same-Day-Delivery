using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the attachment preview system for placing packages precisely
/// </summary>
public class AttachmentPreview : MonoBehaviour
{
    [Header("References")]
    public AttachmentHandler attachmentHandler;

    [Header("Preview Settings")]
    public GameObject previewPrefab;
    public float adjustmentSpeed = 0.25f; // Increased for better grid movement
    public float adjustmentCooldownTime = 0.15f;
    public Color validColor = new Color(0, 1, 0, 0.5f);  // Semi-transparent green
    public Color invalidColor = new Color(1, 0, 0, 0.5f); // Semi-transparent red

    [Header("Snap Grid Settings")]
    public bool useSnapGrid = true;
    public float gridSize = 0.25f; // Size of the grid cells
    public float snapThreshold = 0.1f; // Distance at which to snap to attach points

    [Header("Attachment Point Snapping")]
    public float attachPointSnapDistance = 0.3f; // How close to be to snap to attachment points
    public float attachPointSnapWeight = 2.0f;  // How strongly to prioritize attachment points

    private GameObject currentPreview;
    private bool isInPreviewMode = false;
    private Vector2 previewPosition;
    private GameObject targetPackage;

    // Position adjustment controls
    private bool canAdjustPosition = true;

    // Store original position of the package
    private Vector3 originalPackagePosition;
    private Transform originalPackageParent;

    // List of valid snap points (attachment points)
    private List<Vector2> validSnapPoints = new List<Vector2>();

    // Keep track of the attachment side during preview
    private AttachmentHandler.AttachmentSide currentPreviewSide = AttachmentHandler.AttachmentSide.Custom;

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
                Debug.LogError("AttachmentPreview requires an AttachmentHandler component");
                enabled = false;
                return;
            }
        }

        if (previewPrefab == null)
        {
            CreateDefaultPreviewPrefab();
        }
    }

    private void CreateDefaultPreviewPrefab()
    {
        previewPrefab = new GameObject("DefaultPreviewPrefab");
        SpriteRenderer sr = previewPrefab.AddComponent<SpriteRenderer>();
        sr.color = new Color(0, 1, 0, 0.5f);
        previewPrefab.SetActive(false);
    }

    public bool IsInPreviewMode()
    {
        return isInPreviewMode;
    }

    public void StartPreviewMode()
    {
        if (isInPreviewMode || attachmentHandler == null) return;

        // Find closest package
        targetPackage = attachmentHandler.FindClosestUnattachedPackage();
        if (targetPackage == null) return;

        // Remember original position and parent
        originalPackagePosition = targetPackage.transform.position;
        originalPackageParent = targetPackage.transform.parent;

        // Create outline preview
        currentPreview = Instantiate(previewPrefab, transform);
        currentPreview.SetActive(true);
        SpriteRenderer previewRenderer = currentPreview.GetComponent<SpriteRenderer>();

        // Match size and sprite with target package
        SpriteRenderer targetRenderer = targetPackage.GetComponent<SpriteRenderer>();
        if (targetRenderer != null && previewRenderer != null)
        {
            previewRenderer.sprite = targetRenderer.sprite;
            previewRenderer.size = targetRenderer.size;
        }

        // Generate valid attachment points for snapping
        GenerateValidSnapPoints();

        // Find the closest attachment point to place it initially
        Vector2 initialPos = FindClosestAttachmentPoint(targetPackage.transform.position);
        previewPosition = initialPos;
        currentPreview.transform.position = previewPosition;

        // Determine the attachment side
        currentPreviewSide = DetermineAttachmentSide(previewPosition);

        // Check if the initial position is valid
        bool isValid = attachmentHandler.IsValidAttachmentPosition(previewPosition, targetPackage);
        UpdatePreviewVisual(isValid);

        // Make the target package follow the preview
        MakePackageTransparentAndFollow();

        isInPreviewMode = true;
    }

    // Determine which attachment side we're currently on
    private AttachmentHandler.AttachmentSide DetermineAttachmentSide(Vector2 position)
    {
        return attachmentHandler.DetermineSideFromPosition(position);
    }

    void GenerateValidSnapPoints()
    {
        validSnapPoints.Clear();

        // Add specific attachment points as primary snap targets
        if (attachmentHandler.rightAttachPoint != null)
        {
            validSnapPoints.Add(attachmentHandler.rightAttachPoint.position);
        }

        if (attachmentHandler.leftAttachPoint != null)
        {
            validSnapPoints.Add(attachmentHandler.leftAttachPoint.position);
        }

        if (attachmentHandler.topAttachPoint != null)
        {
            validSnapPoints.Add(attachmentHandler.topAttachPoint.position);
        }

        // Get positions of already attached packages to enable "chaining"
        List<GameObject> attachedItems = attachmentHandler.GetAllAttachedItems();
        foreach (GameObject item in attachedItems)
        {
            if (item != null)
            {
                // Add package position as a potential snap point
                validSnapPoints.Add(item.transform.position);

                // Also create grid points around this item
                AddGridPointsAroundPosition(item.transform.position, attachmentHandler.validAttachDistance);
            }
        }

        // Add grid points to fill in the rest of the valid attachment area
        Vector2 robotCenter = transform.position;
        float robotRadius = attachmentHandler.validAttachDistance * 1.5f; // Slightly larger area

        // Add grid points in a circular pattern around the robot
        for (float x = robotCenter.x - robotRadius; x <= robotCenter.x + robotRadius; x += gridSize)
        {
            for (float y = robotCenter.y - robotRadius; y <= robotCenter.y + robotRadius; y += gridSize)
            {
                Vector2 point = new Vector2(x, y);
                float distToRobot = Vector2.Distance(point, robotCenter);

                if (distToRobot <= robotRadius && distToRobot >= robotRadius * 0.1f)
                {
                    validSnapPoints.Add(point);
                }
            }
        }
    }

    // Helper to add grid points around a position
    private void AddGridPointsAroundPosition(Vector2 center, float radius)
    {
        for (float x = center.x - radius; x <= center.x + radius; x += gridSize)
        {
            for (float y = center.y - radius; y <= center.y + radius; y += gridSize)
            {
                Vector2 point = new Vector2(x, y);
                float dist = Vector2.Distance(point, center);

                if (dist <= radius && dist >= radius * 0.3f) // Not too close
                {
                    validSnapPoints.Add(point);
                }
            }
        }
    }

    // Find the closest primary attachment point (head, left, right)
    Vector2 FindClosestAttachmentPoint(Vector2 position)
    {
        // Prioritize main attachment points
        Vector2 closestPoint = Vector2.zero;
        float closestDistance = float.MaxValue;

        // Check attachment points first
        if (attachmentHandler.rightAttachPoint != null)
        {
            Vector2 rightPos = attachmentHandler.rightAttachPoint.position;
            float dist = Vector2.Distance(position, rightPos);

            // Weight attachment points more heavily
            dist /= attachPointSnapWeight;

            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestPoint = rightPos;
            }
        }

        if (attachmentHandler.leftAttachPoint != null)
        {
            Vector2 leftPos = attachmentHandler.leftAttachPoint.position;
            float dist = Vector2.Distance(position, leftPos);

            // Weight attachment points more heavily
            dist /= attachPointSnapWeight;

            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestPoint = leftPos;
            }
        }

        if (attachmentHandler.topAttachPoint != null)
        {
            Vector2 topPos = attachmentHandler.topAttachPoint.position;
            float dist = Vector2.Distance(position, topPos);

            // Weight attachment points more heavily
            dist /= attachPointSnapWeight;

            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestPoint = topPos;
            }
        }

        // If we found a close attachment point, use it
        if (closestDistance < float.MaxValue && closestDistance * attachPointSnapWeight < attachPointSnapDistance)
        {
            return closestPoint;
        }

        // Otherwise, try general snap points
        if (validSnapPoints.Count > 0)
        {
            closestDistance = float.MaxValue;
            Vector2 closest = validSnapPoints[0];

            foreach (Vector2 point in validSnapPoints)
            {
                float dist = Vector2.Distance(position, point);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closest = point;
                }
            }

            return closest;
        }

        // Last resort - use a default attachment position
        return attachmentHandler.GetAttachPosition(AttachmentHandler.AttachmentSide.Right);
    }

    public void EndPreviewMode(bool confirm)
    {
        if (!isInPreviewMode) return;

        if (confirm && attachmentHandler != null && targetPackage != null &&
            attachmentHandler.IsValidAttachmentPosition(previewPosition, targetPackage))
        {
            // Apply the attachment
            attachmentHandler.AttachItemAtPosition(targetPackage, previewPosition);
        }
        else
        {
            // Restore the package to its original state
            RestorePackageToOriginal();
        }

        // Clean up preview
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }

        isInPreviewMode = false;
        targetPackage = null;
        validSnapPoints.Clear();
    }

    void MakePackageTransparentAndFollow()
    {
        if (targetPackage == null) return;

        // Save rigidbody state
        Rigidbody2D rb = targetPackage.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = false;
        }

        // Make semi-transparent
        SpriteRenderer sr = targetPackage.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0.5f);
        }

        // Disable colliders temporarily
        Collider2D[] colliders = targetPackage.GetComponents<Collider2D>();
        foreach (Collider2D col in colliders)
        {
            col.enabled = false;
        }

        // Position at the preview point
        targetPackage.transform.position = previewPosition;
    }

    void RestorePackageToOriginal()
    {
        if (targetPackage == null) return;

        // Restore position and parent
        targetPackage.transform.position = originalPackagePosition;
        targetPackage.transform.SetParent(originalPackageParent);

        // Restore rigidbody
        Rigidbody2D rb = targetPackage.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = true;
        }

        // Restore transparency
        SpriteRenderer sr = targetPackage.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f);
        }

        // Re-enable colliders
        Collider2D[] colliders = targetPackage.GetComponents<Collider2D>();
        foreach (Collider2D col in colliders)
        {
            col.enabled = true;
        }
    }

    public void AdjustPreviewPosition()
    {
        if (!isInPreviewMode || !canAdjustPosition || currentPreview == null || targetPackage == null)
            return;

        Vector2 adjustment = Vector2.zero;

        if (Input.GetKey(KeyCode.LeftArrow)) adjustment.x -= adjustmentSpeed;
        if (Input.GetKey(KeyCode.RightArrow)) adjustment.x += adjustmentSpeed;
        if (Input.GetKey(KeyCode.UpArrow)) adjustment.y += adjustmentSpeed;
        if (Input.GetKey(KeyCode.DownArrow)) adjustment.y -= adjustmentSpeed;

        if (adjustment != Vector2.zero)
        {
            // Move the position based on input
            Vector2 newPosition = previewPosition + adjustment;

            // If using snap grid, find the closest valid point
            if (useSnapGrid)
            {
                // First snap to grid
                newPosition.x = Mathf.Round(newPosition.x / gridSize) * gridSize;
                newPosition.y = Mathf.Round(newPosition.y / gridSize) * gridSize;

                // Check if we're close to an attachment point for snapping
                bool snappedToAttachPoint = false;

                // Check primary attachment points first with high priority
                if (attachmentHandler.rightAttachPoint != null)
                {
                    Vector2 rightPos = attachmentHandler.rightAttachPoint.position;
                    if (Vector2.Distance(newPosition, rightPos) < attachPointSnapDistance)
                    {
                        newPosition = rightPos;
                        snappedToAttachPoint = true;
                    }
                }

                if (!snappedToAttachPoint && attachmentHandler.leftAttachPoint != null)
                {
                    Vector2 leftPos = attachmentHandler.leftAttachPoint.position;
                    if (Vector2.Distance(newPosition, leftPos) < attachPointSnapDistance)
                    {
                        newPosition = leftPos;
                        snappedToAttachPoint = true;
                    }
                }

                if (!snappedToAttachPoint && attachmentHandler.topAttachPoint != null)
                {
                    Vector2 topPos = attachmentHandler.topAttachPoint.position;
                    if (Vector2.Distance(newPosition, topPos) < attachPointSnapDistance)
                    {
                        newPosition = topPos;
                        snappedToAttachPoint = true;
                    }
                }

                // If not snapped to a primary point, check other snap points
                if (!snappedToAttachPoint)
                {
                    // Find if we're close enough to a valid snap point
                    foreach (Vector2 snapPoint in validSnapPoints)
                    {
                        if (Vector2.Distance(newPosition, snapPoint) < snapThreshold)
                        {
                            newPosition = snapPoint;
                            break;
                        }
                    }
                }
            }

            previewPosition = newPosition;
            currentPreview.transform.position = previewPosition;

            // Update the target package position to match
            targetPackage.transform.position = previewPosition;

            // Update the current side based on position
            currentPreviewSide = DetermineAttachmentSide(previewPosition);

            // Check if position is valid
            bool isValid = attachmentHandler.IsValidAttachmentPosition(previewPosition, targetPackage);
            UpdatePreviewVisual(isValid);

            // Apply cooldown
            StartCoroutine(AdjustmentCooldown());
        }
    }

    void UpdatePreviewVisual(bool isValid)
    {
        SpriteRenderer renderer = currentPreview.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = isValid ? validColor : invalidColor;
        }
    }

    IEnumerator AdjustmentCooldown()
    {
        canAdjustPosition = false;
        yield return new WaitForSeconds(adjustmentCooldownTime);
        canAdjustPosition = true;
    }

    void OnDrawGizmos()
    {
        if (isInPreviewMode && currentPreview != null)
        {
            // Draw a line from the robot to the preview
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, previewPosition);

            // Draw snap points if in preview mode
            if (useSnapGrid)
            {
                Gizmos.color = new Color(0f, 0.5f, 1f, 0.2f);
                foreach (Vector2 point in validSnapPoints)
                {
                    Gizmos.DrawSphere(point, 0.05f);
                }
            }

            // Draw attachment points with different color
            if (attachmentHandler != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);

                if (attachmentHandler.rightAttachPoint != null)
                    Gizmos.DrawSphere(attachmentHandler.rightAttachPoint.position, 0.1f);

                if (attachmentHandler.leftAttachPoint != null)
                    Gizmos.DrawSphere(attachmentHandler.leftAttachPoint.position, 0.1f);

                if (attachmentHandler.topAttachPoint != null)
                    Gizmos.DrawSphere(attachmentHandler.topAttachPoint.position, 0.1f);
            }
        }
    }
}