using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the attachment preview system for placing packages precisely
/// </summary>
public class AttachmentPreview : MonoBehaviour
{
    [Header("References")]
    public AttachmentHandler attachmentHandler; // Make this public and assignable in inspector

    [Header("Preview Settings")]
    public GameObject previewPrefab;
    public float adjustmentSpeed = 0.1f;
    public float adjustmentCooldownTime = 0.05f;
    public Color validColor = new Color(0, 1, 0, 0.5f);  // Semi-transparent green
    public Color invalidColor = new Color(1, 0, 0, 0.5f); // Semi-transparent red

    private GameObject currentPreview;
    private bool isInPreviewMode = false;
    private Vector2 previewPosition;
    private GameObject targetPackage;

    // Position adjustment controls
    private bool canAdjustPosition = true;

    // Store original position of the package
    private Vector3 originalPackagePosition;
    private Transform originalPackageParent;

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
                Debug.LogError("AttachmentPreview requires an AttachmentHandler component. Please assign one in the inspector.");
            }
        }

        // Create preview prefab if not set
        if (previewPrefab == null)
        {
            CreateDefaultPreviewPrefab();
        }
    }

    // Creates a default preview prefab if none is assigned
    private void CreateDefaultPreviewPrefab()
    {
        previewPrefab = new GameObject("DefaultPreviewPrefab");
        SpriteRenderer sr = previewPrefab.AddComponent<SpriteRenderer>();
        sr.color = new Color(0, 1, 0, 0.5f);
        previewPrefab.SetActive(false); // Hide it until needed
    }

    public bool IsInPreviewMode()
    {
        return isInPreviewMode;
    }

    public void StartPreviewMode()
    {
        if (isInPreviewMode) return;
        if (attachmentHandler == null) return; // Safety check

        // Find closest package
        targetPackage = attachmentHandler.FindClosestUnattachedPackage();
        if (targetPackage == null) return;

        // Remember original position and parent
        originalPackagePosition = targetPackage.transform.position;
        originalPackageParent = targetPackage.transform.parent;

        // Create outline preview
        currentPreview = Instantiate(previewPrefab, transform);
        SpriteRenderer previewRenderer = currentPreview.GetComponent<SpriteRenderer>();

        // Match size and sprite with target package
        SpriteRenderer targetRenderer = targetPackage.GetComponent<SpriteRenderer>();
        if (targetRenderer != null && previewRenderer != null)
        {
            previewRenderer.sprite = targetRenderer.sprite;
            previewRenderer.size = targetRenderer.size;
        }

        // Initial position at a good attachment point
        AttachmentHandler.AttachmentSide side = AttachmentHandler.AttachmentSide.Right;
        previewPosition = attachmentHandler.GetAttachPosition(side);
        currentPreview.transform.position = previewPosition;

        // Check if the initial position is valid
        bool isValid = attachmentHandler.IsValidAttachmentPosition(previewPosition, targetPackage);
        UpdatePreviewVisual(isValid);

        // Temporarily parent the target to the preview with transparency
        MakePackageTransparentAndFollow();

        isInPreviewMode = true;
    }

    public void EndPreviewMode(bool confirm)
    {
        if (!isInPreviewMode) return;
        if (attachmentHandler == null) return; // Safety check

        if (confirm && attachmentHandler.IsValidAttachmentPosition(previewPosition, targetPackage))
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

        // Make it follow the preview position
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
        if (!isInPreviewMode || !canAdjustPosition || currentPreview == null || targetPackage == null || attachmentHandler == null)
            return;

        Vector2 adjustment = Vector2.zero;

        if (Input.GetKey(KeyCode.LeftArrow)) adjustment.x -= adjustmentSpeed;
        if (Input.GetKey(KeyCode.RightArrow)) adjustment.x += adjustmentSpeed;
        if (Input.GetKey(KeyCode.UpArrow)) adjustment.y += adjustmentSpeed;
        if (Input.GetKey(KeyCode.DownArrow)) adjustment.y -= adjustmentSpeed;

        if (adjustment != Vector2.zero)
        {
            previewPosition += adjustment;
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
        // Change preview color based on validity
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
        if (isInPreviewMode && currentPreview != null && attachmentHandler != null)
        {
            // Draw a line from the robot to the preview
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, previewPosition);

            // Draw connection line to attached item if connected
            GameObject connected = attachmentHandler.FindConnectedItem(previewPosition);
            if (connected != null && connected != gameObject)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(previewPosition, connected.transform.position);
            }
        }
    }
}