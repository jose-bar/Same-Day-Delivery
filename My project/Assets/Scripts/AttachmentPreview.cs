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

        // Generate valid attachment points
        GenerateValidSnapPoints();

        // Place at closest valid snap point to the player
        Vector2 initialPos = FindClosestSnapPoint(targetPackage.transform.position);
        previewPosition = initialPos;
        currentPreview.transform.position = previewPosition;

        // Check if the initial position is valid
        bool isValid = attachmentHandler.IsValidAttachmentPosition(previewPosition, targetPackage);
        UpdatePreviewVisual(isValid);

        // Make the target package follow the preview
        MakePackageTransparentAndFollow();

        isInPreviewMode = true;
    }

    void GenerateValidSnapPoints()
    {
        validSnapPoints.Clear();

        // Add points around the robot
        Vector2 robotCenter = transform.position;
        float robotRadius = attachmentHandler.validAttachDistance;

        // Add the standard attach positions
        validSnapPoints.Add(attachmentHandler.GetAttachPosition(AttachmentHandler.AttachmentSide.Right));
        validSnapPoints.Add(attachmentHandler.GetAttachPosition(AttachmentHandler.AttachmentSide.Left));
        validSnapPoints.Add(attachmentHandler.GetAttachPosition(AttachmentHandler.AttachmentSide.Top));

        // Add grid points around the robot
        for (float x = robotCenter.x - robotRadius; x <= robotCenter.x + robotRadius; x += gridSize)
        {
            for (float y = robotCenter.y - robotRadius; y <= robotCenter.y + robotRadius; y += gridSize)
            {
                Vector2 point = new Vector2(x, y);
                float distToRobot = Vector2.Distance(point, robotCenter);

                if (distToRobot <= robotRadius && distToRobot >= robotRadius * 0.5f)
                {
                    validSnapPoints.Add(point);
                }
            }
        }

        // Add points around each attached item
        List<GameObject> attachedItems = attachmentHandler.GetAllAttachedItems();
        foreach (GameObject item in attachedItems)
        {
            if (item == null) continue;

            Vector2 itemCenter = item.transform.position;

            // Add grid points around this item
            for (float x = itemCenter.x - robotRadius; x <= itemCenter.x + robotRadius; x += gridSize)
            {
                for (float y = itemCenter.y - robotRadius; y <= itemCenter.y + robotRadius; y += gridSize)
                {
                    Vector2 point = new Vector2(x, y);
                    float distToItem = Vector2.Distance(point, itemCenter);

                    if (distToItem <= robotRadius && distToItem >= 0.2f) // Close but not overlapping
                    {
                        validSnapPoints.Add(point);
                    }
                }
            }
        }
    }

    Vector2 FindClosestSnapPoint(Vector2 position)
    {
        if (validSnapPoints.Count == 0)
        {
            return attachmentHandler.GetAttachPosition(AttachmentHandler.AttachmentSide.Right);
        }

        Vector2 closest = validSnapPoints[0];
        float closestDist = Vector2.Distance(position, closest);

        foreach (Vector2 point in validSnapPoints)
        {
            float dist = Vector2.Distance(position, point);
            if (dist < closestDist)
            {
                closest = point;
                closestDist = dist;
            }
        }

        return closest;
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

            previewPosition = newPosition;
            currentPreview.transform.position = previewPosition;

            // Update the target package position to match
            targetPackage.transform.position = previewPosition;

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
        }
    }
}