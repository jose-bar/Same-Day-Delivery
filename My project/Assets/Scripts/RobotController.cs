using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotController : MonoBehaviour
{
    // All existing properties and fields remain unchanged...

    [Header("Movement Settings")]
    public float maxMoveSpeed = 5f;
    public float acceleration = 25f;
    public float deceleration = 40f;
    private float currentMoveVelocity = 0f;
    public float jumpForce = 8f;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.6f;
    public float groundedBufferTime = 0.1f;
    private float lastGroundedTime = 0f;

    [Header("Body Settings")]
    public Transform bodySprite;
    public Transform bodyMiddle;
    public Transform wheelSprite;
    private Vector3 originalBodyMiddlePosition;
    public Vector2 bodyColliderSize = new Vector2(0.8f, 1.2f);

    [Header("Crouch Settings")]
    public float crouchAmount = 0.3f;
    public float crouchSpeed = 5f;
    public float crouchColliderReduction = 0.5f;

    [Header("Sway Settings")]
    public float swayAmount = 0.1f;
    public float swaySpeed = 3f;
    public float swayResponsiveness = 5f;
    private float currentSwayAngle = 0f;
    private float swayVelocity = 0f;

    private Rigidbody2D rb;
    private CircleCollider2D wheelCollider;
    private BoxCollider2D bodyCollider;
    private bool isGrounded;
    private float horizontalInput;
    private bool isCrouching = false;
    private bool isAttemptingToStand = false;

    [Header("Crouch Head Clearance Check")]
    public Transform ceilingCheck;
    public float ceilingCheckRadius = 0.1f;
    public LayerMask groundLayer;
    public float sideCheckDistance = 0.2f;

    private Vector3 originalBodyPosition;
    private Vector2 originalBodyColliderSize;
    private Vector2 originalBodyColliderOffset;

    [Header("Attachment")]
    public AttachmentHandler attachmentHandler;

    // Sound effects
    private LoopSoundEffects loopSounds;
    private OneSoundEffects oneSounds;

    [Header("Proxy Collision")]
    public float collisionBounceForce = 0.5f;
    public float collisionPushbackDelay = 0.1f;
    private bool isHandlingProxyCollision = false;
    private Vector2 collisionNormal;

    // Position freezing
    private bool isAttachmentFreezing = false;
    private float freezeTimer = 0f;
    private Vector3 savedPosition;

    // Proxy collision enabling
    private bool proxyCollisionsEnabled = true;
    private float collisionDisableTimer = 0f;

    // Trigger-based collision handling
    private bool isTriggerBlocking = false;
    private Vector2 blockedDirection = Vector2.zero;
    private float triggerBlockResetTime = 0f;

    // Stable position tracking to prevent sinking
    private Vector3 lastStablePosition;
    private bool isTrackingStablePosition = false;
    private float stablePositionTimer = 0f;

    // New attachment system components
    private AttachmentPreview attachmentPreview;
    private DropModeManager dropModeManager;

    // Updated input handling
    private bool lastEKeyState = false;
    private bool lastQKeyState = false;
    private bool useArrowsForAttachment = false;

    void Start()
    {
        if (attachmentHandler == null)
        {
            attachmentHandler = GetComponentInChildren<AttachmentHandler>();
            if (attachmentHandler == null)
                Debug.LogWarning("AttachmentHandler not assigned and not found in children.");
        }

        loopSounds = GetComponent<LoopSoundEffects>();
        oneSounds = GetComponent<OneSoundEffects>();

        rb = GetComponent<Rigidbody2D>();
        wheelCollider = GetComponent<CircleCollider2D>();

        if (wheelCollider == null)
            Debug.LogError("No CircleCollider2D found on the robot!");

        if (bodySprite == null)
        {
            bodySprite = transform.Find("BodySprite");
            if (bodySprite == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.GetComponent<SpriteRenderer>() != null && child.name != "WheelSprite")
                    {
                        bodySprite = child;
                        break;
                    }
                }
            }
        }

        if (bodySprite != null)
        {
            originalBodyPosition = bodySprite.localPosition;
            SetupBodyCollider();
        }
        else
        {
            Debug.LogWarning("Body sprite not found!");
        }

        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (bodyMiddle != null)
        {
            originalBodyMiddlePosition = bodyMiddle.localPosition;
        }

        // Add and initialize new attachment system components
        attachmentPreview = GetComponent<AttachmentPreview>();
        if (attachmentPreview == null)
        {
            attachmentPreview = gameObject.AddComponent<AttachmentPreview>();
        }

        dropModeManager = GetComponent<DropModeManager>();
        if (dropModeManager == null)
        {
            dropModeManager = gameObject.AddComponent<DropModeManager>();
        }
    }

    void SetupBodyCollider()
    {
        bodyCollider = bodySprite.GetComponent<BoxCollider2D>();

        if (bodyCollider == null)
        {
            bodyCollider = bodySprite.gameObject.AddComponent<BoxCollider2D>();
            bodyCollider.size = bodyColliderSize;
            bodyCollider.offset = Vector2.zero;
        }

        originalBodyColliderSize = bodyCollider.size;
        originalBodyColliderOffset = bodyCollider.offset;
    }

    void Update()
    {
        // Track stable position when not moving horizontally
        if (Mathf.Abs(horizontalInput) < 0.1f && isGrounded && !isCrouching)
        {
            if (!isTrackingStablePosition)
            {
                isTrackingStablePosition = true;
                stablePositionTimer = 0.1f; // Short delay before considering position stable
            }
            else
            {
                stablePositionTimer -= Time.deltaTime;
                if (stablePositionTimer <= 0)
                {
                    // Store position as stable reference
                    lastStablePosition = transform.position;
                }
            }
        }
        else
        {
            isTrackingStablePosition = false;
        }

        horizontalInput = 0f;

        // Skip movement input if in attachment or drop mode
        if (!attachmentPreview.IsInPreviewMode() && !dropModeManager.IsInDropMode())
        {
            if (Input.GetKey(KeyCode.A)) horizontalInput = -1f;
            if (Input.GetKey(KeyCode.D)) horizontalInput = 1f;
        }

        // Handle movement sound
        if (Mathf.Abs(currentMoveVelocity) > 0.5f && isGrounded)
        {
            if (loopSounds != null) loopSounds.PlayMoveAudio();
        }
        else
        {
            if (loopSounds != null) loopSounds.StopAudio();
        }

        // Remember ground state
        if (isGrounded)
        {
            lastGroundedTime = Time.time;
        }

        // Allow jumping if we were recently grounded and not in attachment/drop mode
        bool canJump = Time.time - lastGroundedTime < groundedBufferTime &&
                      !attachmentPreview.IsInPreviewMode() &&
                      !dropModeManager.IsInDropMode();

        if (Input.GetKeyDown(KeyCode.Space) && canJump)
        {
            // Rest of jump code remains unchanged...

            // Check if jumping would cause attached items to clip through ceilings
            bool canJumpWithAttachments = true;

            List<GameObject> attachedItems = attachmentHandler.GetAllAttachedItems();
            foreach (GameObject item in attachedItems)
            {
                if (item == null) continue;

                Collider2D itemCollider = item.GetComponent<Collider2D>();
                if (itemCollider == null) continue;

                // Get current bounds
                Bounds itemBounds = itemCollider.bounds;

                // Create expanded bounds to check for jump clearance
                Bounds jumpBounds = itemBounds;
                jumpBounds.Expand(new Vector3(0.02f, 0.15f, 0)); // Expand slightly, more vertically

                // Move up to simulate jump
                jumpBounds.center += new Vector3(0, 0.2f, 0);

                // Check if there's a ceiling or obstacle in the way
                Collider2D[] hits = Physics2D.OverlapBoxAll(
                    jumpBounds.center,
                    jumpBounds.size,
                    0f,
                    groundLayer
                );

                foreach (Collider2D hit in hits)
                {
                    // Skip robot and attachments
                    if (hit.gameObject == gameObject ||
                        hit.transform.IsChildOf(transform) ||
                        hit.CompareTag(AttachmentHandler.ATTACHMENT_COLLIDER_TAG) ||
                        hit.CompareTag("ProxyCollider"))
                    {
                        continue;
                    }

                    // Found an obstacle - can't jump
                    canJumpWithAttachments = false;
                    break;
                }

                if (!canJumpWithAttachments) break;
            }

            // Only jump if we can do so safely
            if (canJumpWithAttachments || attachedItems.Count == 0)
            {
                // Basic jump - physics system will handle collision constraints
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
                rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
                isGrounded = false;

                // Play jump sound
                if (oneSounds != null) oneSounds.PlayJumpAudio();
            }
            else
            {
                // Can't jump - play bump sound to indicate blocked
                if (oneSounds != null && !oneSounds.src2.isPlaying)
                {
                    oneSounds.PlayBumpAudio();
                }
            }
        }

        // Only handle crouch and sway if not in attachment/drop mode
        if (!attachmentPreview.IsInPreviewMode() && !dropModeManager.IsInDropMode())
        {
            HandleCrouch();
            HandleSway();
        }

        // Handle new attachment system
        HandleAttachmentSystem();

        // Handle position freezing for attachment
        if (isAttachmentFreezing)
        {
            freezeTimer -= Time.deltaTime;

            // Force position to stay the same during freeze
            transform.position = savedPosition;

            // Set velocity to zero
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            // Cancel freezing if timer expired
            if (freezeTimer <= 0)
            {
                isAttachmentFreezing = false;
            }
        }

        // Handle collision disable timer
        if (!proxyCollisionsEnabled)
        {
            collisionDisableTimer -= Time.deltaTime;
            if (collisionDisableTimer <= 0)
            {
                proxyCollisionsEnabled = true;
            }
        }

        // Handle trigger blocking timer
        if (isTriggerBlocking)
        {
            triggerBlockResetTime -= Time.deltaTime;
            if (triggerBlockResetTime <= 0)
            {
                isTriggerBlocking = false;
                blockedDirection = Vector2.zero;
            }
        }
    }

    private void HandleAttachmentSystem()
    {
        bool currentEKeyState = Input.GetKey(KeyCode.E);
        bool currentQKeyState = Input.GetKey(KeyCode.Q);

        // E key pressed - toggle attachment preview mode
        if (currentEKeyState && !lastEKeyState)
        {
            if (attachmentPreview.IsInPreviewMode())
            {
                // Confirm attachment if in preview mode
                attachmentPreview.EndPreviewMode(true);
                useArrowsForAttachment = false;
            }
            else if (!dropModeManager.IsInDropMode())
            {
                // Enter preview mode if not in drop mode
                attachmentPreview.StartPreviewMode();
                useArrowsForAttachment = true;
            }
        }

        // Q key pressed - toggle drop mode
        if (currentQKeyState && !lastQKeyState)
        {
            if (dropModeManager.IsInDropMode())
            {
                // Confirm drop if in drop mode
                dropModeManager.ExitDropMode(true);
                useArrowsForAttachment = false;
            }
            else if (!attachmentPreview.IsInPreviewMode() && attachmentHandler.GetAllAttachedItems().Count > 0)
            {
                // Enter drop mode if not in preview mode and have attachments
                dropModeManager.EnterDropMode();
                useArrowsForAttachment = true;
            }
        }

        // Escape key to cancel either mode
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (attachmentPreview.IsInPreviewMode())
            {
                attachmentPreview.EndPreviewMode(false);
                useArrowsForAttachment = false;
            }

            if (dropModeManager.IsInDropMode())
            {
                dropModeManager.ExitDropMode(false);
                useArrowsForAttachment = false;
            }
        }

        // Adjust position in preview mode
        if (attachmentPreview.IsInPreviewMode())
        {
            attachmentPreview.AdjustPreviewPosition();
        }

        // Cycle selection in drop mode
        if (dropModeManager.IsInDropMode())
        {
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                dropModeManager.CycleSelection(true);
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                dropModeManager.CycleSelection(false);
            }
        }

        // For backward compatibility, only use legacy attachment controls if not in new modes
        if (!attachmentPreview.IsInPreviewMode() && !dropModeManager.IsInDropMode() && !useArrowsForAttachment)
        {
            // Legacy attachment keys (delegate to handler)
            if (Input.GetKeyDown(KeyCode.RightArrow))
                attachmentHandler.ToggleAttachment(AttachmentHandler.AttachmentSide.Right);
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
                attachmentHandler.ToggleAttachment(AttachmentHandler.AttachmentSide.Left);
            else if (Input.GetKeyDown(KeyCode.UpArrow))
                attachmentHandler.ToggleAttachment(AttachmentHandler.AttachmentSide.Top);
        }

        // Update key states for next frame
        lastEKeyState = currentEKeyState;
        lastQKeyState = currentQKeyState;
    }

    void HandleSway()
    {
        if (bodySprite == null) return;

        // Target sway based on horizontal input and robot velocity
        float targetSway = -horizontalInput * swayAmount;

        // Apply smooth damping for natural swaying motion
        float smoothTime = isGrounded ? 0.1f : 0.3f; // Less responsive in air
        float newSwayAngle = Mathf.SmoothDamp(currentSwayAngle, targetSway, ref swayVelocity, smoothTime, swaySpeed);

        // Check if the new sway angle would cause clipping and get a safe angle if it would
        float safeAngle = newSwayAngle;
        bool wouldClip = WouldSwayAngleCauseClipping(newSwayAngle, out safeAngle);

        // Use the safe angle
        currentSwayAngle = safeAngle;

        // Apply sway to the body
        if (!isCrouching)
        {
            // Don't reset z rotation - only modify it
            Vector3 currentRot = bodySprite.localRotation.eulerAngles;
            bodySprite.localRotation = Quaternion.Euler(currentRot.x, currentRot.y, currentSwayAngle);

            // Apply the same sway to attachments by rotating the entire transform except wheel
            if (wheelSprite != null)
            {
                // Keep the wheel rotation separate
                Quaternion wheelRot = wheelSprite.localRotation;
                transform.localRotation = Quaternion.Euler(0, 0, currentSwayAngle);
                wheelSprite.localRotation = wheelRot; // Preserve wheel rotation
            }
            else
            {
                transform.localRotation = Quaternion.Euler(0, 0, currentSwayAngle);
            }
        }
        else
        {
            // Less sway when crouching
            float crouchSwayFactor = 0.5f;
            float crouchedSwayAngle = currentSwayAngle * crouchSwayFactor;

            Vector3 currentRot = bodySprite.localRotation.eulerAngles;
            bodySprite.localRotation = Quaternion.Euler(currentRot.x, currentRot.y, crouchedSwayAngle);

            if (wheelSprite != null)
            {
                Quaternion wheelRot = wheelSprite.localRotation;
                transform.localRotation = Quaternion.Euler(0, 0, crouchedSwayAngle);
                wheelSprite.localRotation = wheelRot;
            }
            else
            {
                transform.localRotation = Quaternion.Euler(0, 0, crouchedSwayAngle);
            }
        }
    }

    private bool WouldSwayAngleCauseClipping(float swayAngle, out float maxSafeAngle)
    {
        maxSafeAngle = swayAngle; // Default to requested angle

        List<GameObject> attachedItems = attachmentHandler.GetAllAttachedItems();
        if (attachedItems.Count == 0) return false;

        // Store the current rotation and position
        Quaternion originalRotation = transform.rotation;
        Vector3 originalPosition = transform.position;

        // Try the proposed rotation
        transform.rotation = Quaternion.Euler(0, 0, swayAngle);

        bool wouldClip = false;
        float bestAngle = swayAngle;

        // Check if any attached items would clip in this rotation
        foreach (GameObject item in attachedItems)
        {
            if (item == null) continue;

            Collider2D itemCollider = item.GetComponent<Collider2D>();
            if (itemCollider == null) continue;

            // Get the current bounds after rotation
            Bounds itemBounds = itemCollider.bounds;

            // Add a small expansion to catch near-clips
            Bounds checkBounds = itemBounds;
            checkBounds.Expand(new Vector3(0.02f, 0.02f, 0));

            // Check for overlaps
            Collider2D[] overlaps = Physics2D.OverlapBoxAll(
                checkBounds.center,
                checkBounds.size,
                0f,
                groundLayer
            );

            foreach (Collider2D overlap in overlaps)
            {
                // Skip if it's part of the robot or its attachments
                if (overlap.gameObject == gameObject ||
                    overlap.transform.IsChildOf(transform) ||
                    overlap.CompareTag(AttachmentHandler.ATTACHMENT_COLLIDER_TAG) ||
                    overlap.CompareTag("ProxyCollider"))
                {
                    continue;
                }

                // Found a real overlap
                wouldClip = true;

                // Try to find a safe angle
                if (Mathf.Abs(swayAngle) > 0.1f)
                {
                    // Binary search for the maximum safe angle
                    float minAngle = 0;
                    float maxAngle = swayAngle;
                    float currentTestAngle;

                    // If swayAngle is negative, adjust the search range
                    if (swayAngle < 0)
                    {
                        minAngle = swayAngle;
                        maxAngle = 0;
                    }

                    // Do a few iterations to find a good safe angle
                    for (int i = 0; i < 5; i++)
                    {
                        currentTestAngle = (minAngle + maxAngle) / 2;

                        // Test this angle
                        transform.rotation = Quaternion.Euler(0, 0, currentTestAngle);

                        // Get the updated bounds
                        Bounds testBounds = itemCollider.bounds;
                        testBounds.Expand(new Vector3(0.01f, 0.01f, 0));

                        // Check if this angle causes clipping
                        bool testClips = false;
                        Collider2D[] testOverlaps = Physics2D.OverlapBoxAll(
                            testBounds.center,
                            testBounds.size,
                            0f,
                            groundLayer
                        );

                        foreach (Collider2D testOverlap in testOverlaps)
                        {
                            if (testOverlap.gameObject == gameObject ||
                                testOverlap.transform.IsChildOf(transform) ||
                                testOverlap.CompareTag(AttachmentHandler.ATTACHMENT_COLLIDER_TAG) ||
                                testOverlap.CompareTag("ProxyCollider"))
                            {
                                continue;
                            }

                            testClips = true;
                            break;
                        }

                        // Adjust the search range
                        if (testClips)
                        {
                            if (swayAngle > 0)
                                maxAngle = currentTestAngle;
                            else
                                minAngle = currentTestAngle;
                        }
                        else
                        {
                            if (swayAngle > 0)
                                minAngle = currentTestAngle;
                            else
                                maxAngle = currentTestAngle;

                            // This is a safe angle, remember it
                            bestAngle = currentTestAngle;
                        }
                    }
                }

                break;
            }

            if (wouldClip) break;
        }

        // Restore the original rotation and position
        transform.rotation = originalRotation;
        transform.position = originalPosition; // IMPORTANT: Restore position too

        // Return the best safe angle we found
        maxSafeAngle = bestAngle;

        return wouldClip;
    }

    void FixedUpdate()
    {
        // Skip normal movement updates if freezing
        if (isAttachmentFreezing) return;

        // Calculate target velocity based on input
        float targetVelocity = horizontalInput * maxMoveSpeed;

        // Check if we're trying to move into a blocked direction
        if (isTriggerBlocking)
        {
            // If we're trying to move in the general direction of the block
            float dot = Vector2.Dot(new Vector2(targetVelocity, 0), blockedDirection);
            if (dot > 0)
            {
                // We're pushing against a block - prevent movement in this direction
                targetVelocity = 0;
            }
        }

        // Apply acceleration or deceleration
        if (horizontalInput != 0 && targetVelocity != 0)
        {
            // Accelerate toward target velocity
            currentMoveVelocity = Mathf.MoveTowards(
                currentMoveVelocity,
                targetVelocity,
                acceleration * Time.fixedDeltaTime);
        }
        else
        {
            // Decelerate toward zero when no input or blocked
            currentMoveVelocity = Mathf.MoveTowards(
                currentMoveVelocity,
                0f,
                deceleration * Time.fixedDeltaTime);
        }

        // IMPORTANT: First store the current vertical velocity since PreventClipping might modify it
        float currentVerticalVelocity = rb.linearVelocity.y;

        // Set initial horizontal velocity (vertical will be managed by PreventClipping)
        rb.linearVelocity = new Vector2(currentMoveVelocity, currentVerticalVelocity);

        // Run the clipping prevention check AFTER setting initial velocity
        PreventClipping();

        // Check if we're grounded
        CheckGrounded();

        // Animate wheel rotation
        if (wheelSprite != null)
        {
            float rotationAmount = -rb.linearVelocity.x * 360f * Time.fixedDeltaTime;
            wheelSprite.Rotate(Vector3.forward, rotationAmount);
        }
    }

    private void PreventClipping()
    {
        // Get the attached items
        List<GameObject> attachedItems = attachmentHandler.GetAllAttachedItems();
        if (attachedItems.Count == 0) return;

        // Get current velocity (both horizontal and vertical)
        Vector2 currentVelocity = rb.linearVelocity;
        bool horizontalMovement = Mathf.Abs(currentMoveVelocity) > 0.01f;
        bool verticalMovement = Mathf.Abs(currentVelocity.y) > 0.01f;

        // If we're not moving at all, no need to check
        if (!horizontalMovement && !verticalMovement) return;

        // Create movement direction vector
        Vector2 moveDir = new Vector2(
            horizontalMovement ? Mathf.Sign(currentMoveVelocity) : 0,
            verticalMovement ? Mathf.Sign(currentVelocity.y) : 0
        );

        // Check if moving would cause clipping
        bool wouldClipHorizontal = false;
        bool wouldClipVertical = false;

        // Store the deepest penetration to correct position if needed
        float horizontalPenetration = 0f;
        float verticalPenetration = 0f;

        foreach (GameObject item in attachedItems)
        {
            if (item == null) continue;

            // Get the collider of the item
            Collider2D itemCollider = item.GetComponent<Collider2D>();
            if (itemCollider == null) continue;

            // Get the bounds of the item's collider
            Bounds itemBounds = itemCollider.bounds;

            // IMPORTANT: Use much larger horizontal expansion to prevent any wall clipping
            Vector3 expansion = new Vector3(
                (horizontalMovement ? Mathf.Abs(moveDir.x) * 0.2f : 0) + 0.05f, // Increased horizontal expansion
                (verticalMovement ? Mathf.Abs(moveDir.y) * 0.1f : 0) + 0.03f,   // Slight increase to vertical
                0
            );

            // Create a larger bounds for checking
            Bounds checkBounds = itemBounds;
            checkBounds.Expand(expansion);

            // Calculate movement offset (how far we would move in one frame)
            Vector3 offset = new Vector3(
                horizontalMovement ? moveDir.x * Mathf.Abs(currentMoveVelocity) * Time.fixedDeltaTime : 0,
                verticalMovement ? moveDir.y * Mathf.Abs(currentVelocity.y) * Time.fixedDeltaTime : 0,
                0
            );

            // Don't make the offset too large or we might miss collisions
            offset = Vector3.ClampMagnitude(offset, 0.2f);

            // Move the check bounds in the direction of movement
            checkBounds.center += offset;

            // Check for overlaps with the environment
            Collider2D[] overlaps = Physics2D.OverlapBoxAll(
                checkBounds.center,
                checkBounds.size,
                0f,
                groundLayer
            );

            // Also check for current overlaps to handle existing intersections
            Collider2D[] currentOverlaps = Physics2D.OverlapBoxAll(
                itemBounds.center,
                itemBounds.size * 0.98f, // Slightly smaller to avoid edge cases
                0f,
                groundLayer
            );

            // Combine all overlaps for processing
            List<Collider2D> allOverlaps = new List<Collider2D>(overlaps);
            foreach (Collider2D col in currentOverlaps)
            {
                if (!allOverlaps.Contains(col))
                {
                    allOverlaps.Add(col);
                }
            }

            // Filter out overlaps with the robot and attachments
            foreach (Collider2D overlap in allOverlaps)
            {
                // Skip if it's part of the robot or its attachments
                if (overlap.gameObject == gameObject ||
                    overlap.transform.IsChildOf(transform) ||
                    overlap.CompareTag(AttachmentHandler.ATTACHMENT_COLLIDER_TAG) ||
                    overlap.CompareTag("ProxyCollider"))
                {
                    continue;
                }

                // Get the overlap bounds
                Bounds overlapBounds = overlap.bounds;

                // Calculate penetration depths in both axes
                float xPenetration = 0f;
                float yPenetration = 0f;

                // Check if actually overlapping
                if (itemBounds.Intersects(overlapBounds))
                {
                    // Calculate penetration in both axes
                    float xMin = Mathf.Max(itemBounds.min.x, overlapBounds.min.x);
                    float xMax = Mathf.Min(itemBounds.max.x, overlapBounds.max.x);
                    float yMin = Mathf.Max(itemBounds.min.y, overlapBounds.min.y);
                    float yMax = Mathf.Min(itemBounds.max.y, overlapBounds.max.y);

                    xPenetration = xMax - xMin;
                    yPenetration = yMax - yMin;

                    // Update max penetration
                    horizontalPenetration = Mathf.Max(horizontalPenetration, xPenetration);
                    verticalPenetration = Mathf.Max(verticalPenetration, yPenetration);
                }

                // Found a real overlap - determine if it's horizontal or vertical
                Vector2 overlapDirection = (Vector2)(checkBounds.center - itemBounds.center).normalized;

                // Determine if this is primarily horizontal or vertical movement
                float horizontalAlignment = Mathf.Abs(Vector2.Dot(overlapDirection, Vector2.right));
                float verticalAlignment = Mathf.Abs(Vector2.Dot(overlapDirection, Vector2.up));

                // Decide which direction to block based on alignment and movement
                if (horizontalAlignment > verticalAlignment ||
                    (horizontalAlignment > 0.3f && horizontalMovement))
                {
                    wouldClipHorizontal = true;
                }

                if (verticalAlignment > horizontalAlignment ||
                    (verticalAlignment > 0.3f && verticalMovement))
                {
                    wouldClipVertical = true;
                }

                // If we see significant overlap, be conservative and block both
                if (xPenetration > 0.02f && yPenetration > 0.02f)
                {
                    wouldClipHorizontal = true;
                    wouldClipVertical = true;
                }
            }
        }

        // Prevent horizontal movement if we would clip
        if (wouldClipHorizontal && horizontalMovement)
        {
            // Completely stop horizontal movement
            currentMoveVelocity = 0;
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

            // If there's penetration, push back slightly to prevent visible clipping
            if (horizontalPenetration > 0.01f)
            {
                // IMPORTANT: Store current Y position
                float currentY = transform.position.y;

                // Move slightly away from the wall (in the opposite direction of movement)
                float correctionAmount = Mathf.Min(horizontalPenetration, 0.01f);
                Vector3 correction = new Vector3(-moveDir.x * correctionAmount, 0, 0);
                transform.position += correction;

                // CRITICAL FIX: Restore original Y position to prevent sinking
                transform.position = new Vector3(transform.position.x, currentY, transform.position.z);

                // If we have a last stable position and we're significantly lower than it,
                // restore to closer to the stable height to fix any accumulated sinking
                if (lastStablePosition != Vector3.zero &&
                    transform.position.y < lastStablePosition.y - 0.05f)
                {
                    // Gradually move back toward stable height
                    float correctionY = Mathf.Min(
                        (lastStablePosition.y - transform.position.y) * 0.5f,
                        0.05f
                    );
                    transform.position = new Vector3(
                        transform.position.x,
                        transform.position.y + correctionY,
                        transform.position.z
                    );
                }
            }
        }

        // Prevent vertical movement if we would clip
        if (wouldClipVertical && verticalMovement)
        {
            // Completely stop vertical movement
            if (currentVelocity.y < 0)
            {
                // We're falling and hit something - completely stop and set grounded
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
                isGrounded = true;

                // If we're significantly penetrating, push up slightly
                if (verticalPenetration > 0.01f && moveDir.y < 0)
                {
                    // Apply a minimal correction to avoid visible clipping
                    float correctionAmount = Mathf.Min(verticalPenetration, 0.01f);
                    Vector3 correction = new Vector3(0, correctionAmount, 0);
                    transform.position += correction;
                }
            }
            else if (currentVelocity.y > 0)
            {
                // We're going up and hit something - just stop upward movement
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);

                // If we're penetrating a ceiling, push down slightly
                if (verticalPenetration > 0.01f)
                {
                    // Apply a minimal correction to avoid visible clipping
                    float correctionAmount = Mathf.Min(verticalPenetration, 0.01f);
                    Vector3 correction = new Vector3(0, -correctionAmount, 0);
                    transform.position += correction;
                }
            }

            // Play a sound for feedback on vertical collision
            if (oneSounds != null && !oneSounds.src2.isPlaying)
            {
                oneSounds.PlayBumpAudio();
            }
        }
    }

    void CheckGrounded()
    {
        if (wheelCollider == null) return;

        Vector2 circleBottom = (Vector2)transform.position - new Vector2(0, wheelCollider.radius);

        RaycastHit2D hit = Physics2D.Raycast(circleBottom, Vector2.down, groundCheckDistance);
        RaycastHit2D hitLeft = Physics2D.Raycast(circleBottom - new Vector2(wheelCollider.radius * 0.5f, 0), Vector2.down, groundCheckDistance);
        RaycastHit2D hitRight = Physics2D.Raycast(circleBottom + new Vector2(wheelCollider.radius * 0.5f, 0), Vector2.down, groundCheckDistance);

        // First check if the robot itself is grounded
        bool robotGrounded = (hit.collider != null && hit.collider.gameObject != gameObject && hit.collider.gameObject != bodySprite.gameObject) ||
                         (hitLeft.collider != null && hitLeft.collider.gameObject != gameObject && hitLeft.collider.gameObject != bodySprite.gameObject) ||
                         (hitRight.collider != null && hitRight.collider.gameObject != gameObject && hitRight.collider.gameObject != bodySprite.gameObject);

        // Then check if any attachments are grounded
        bool attachmentsGrounded = AreAttachmentsGrounded();

        // We're grounded if either the robot or any attachments are grounded
        isGrounded = robotGrounded || attachmentsGrounded;

        // If we're grounded by attachments, make sure vertical velocity is zero
        if (attachmentsGrounded && !robotGrounded && rb.linearVelocity.y < 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
        }
    }

    private bool AreAttachmentsGrounded()
    {
        List<GameObject> attachedItems = attachmentHandler.GetAllAttachedItems();
        if (attachedItems.Count == 0) return false;

        foreach (GameObject item in attachedItems)
        {
            if (item == null) continue;

            Collider2D itemCollider = item.GetComponent<Collider2D>();
            if (itemCollider == null) continue;

            // Get the bottom center of the item
            Vector2 bottom = new Vector2(
                itemCollider.bounds.center.x,
                itemCollider.bounds.min.y
            );

            // Cast a short ray down from the bottom of the item
            RaycastHit2D hit = Physics2D.Raycast(
                bottom,
                Vector2.down,
                0.1f,
                groundLayer
            );

            // If we hit something that's not us or our attachments, we're grounded
            if (hit.collider != null &&
                hit.collider.gameObject != gameObject &&
                !hit.collider.transform.IsChildOf(transform) &&
                !hit.collider.CompareTag(AttachmentHandler.ATTACHMENT_COLLIDER_TAG) &&
                !hit.collider.CompareTag("ProxyCollider"))
            {
                return true;
            }

            // Also check edges of the item for better ground detection
            Vector2 bottomLeft = new Vector2(
                itemCollider.bounds.min.x,
                itemCollider.bounds.min.y
            );

            Vector2 bottomRight = new Vector2(
                itemCollider.bounds.max.x,
                itemCollider.bounds.min.y
            );

            RaycastHit2D hitLeft = Physics2D.Raycast(
                bottomLeft,
                Vector2.down,
                0.1f,
                groundLayer
            );

            RaycastHit2D hitRight = Physics2D.Raycast(
                bottomRight,
                Vector2.down,
                0.1f,
                groundLayer
            );

            if ((hitLeft.collider != null &&
                hitLeft.collider.gameObject != gameObject &&
                !hitLeft.collider.transform.IsChildOf(transform) &&
                !hitLeft.collider.CompareTag(AttachmentHandler.ATTACHMENT_COLLIDER_TAG) &&
                !hitLeft.collider.CompareTag("ProxyCollider")) ||
                (hitRight.collider != null &&
                hitRight.collider.gameObject != gameObject &&
                !hitRight.collider.transform.IsChildOf(transform) &&
                !hitRight.collider.CompareTag(AttachmentHandler.ATTACHMENT_COLLIDER_TAG) &&
                !hitRight.collider.CompareTag("ProxyCollider")))
            {
                return true;
            }
        }

        return false;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 contact = collision.GetContact(i).point;
            Vector2 center = transform.position;

            if (contact.y < center.y - wheelCollider.radius * 0.8f)
            {
                isGrounded = true;
                return;
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag(AttachmentHandler.ATTACHMENT_COLLIDER_TAG))
            return; // Ignore collisions with our own attached items

        // Check if this was a significant collision
        if (collision.relativeVelocity.magnitude > 3f)
        {
            // Play bump sound on significant collisions
            if (oneSounds != null)
            {
                oneSounds.PlayBumpAudio();
            }
        }
    }

    // Checks if uncrouching would cause collisions with obstacles
    bool CanStandUp()
    {
        if (ceilingCheck == null)
        {
            Debug.LogWarning("No ceiling check transform assigned!");
            return true;
        }

        // Check for ceiling above
        for (int i = -1; i <= 1; i++)
        {
            Vector2 checkPos = (Vector2)ceilingCheck.position + new Vector2(i * 0.3f, 0);
            Collider2D hitCeiling = Physics2D.OverlapCircle(checkPos, ceilingCheckRadius, groundLayer);
            if (hitCeiling != null)
            {
                return false; // Can't stand up
            }
        }

        // Check if any attached items would collide
        List<GameObject> attachedItems = attachmentHandler.GetAllAttachedItems();
        foreach (GameObject item in attachedItems)
        {
            if (item == null) continue;

            // Calculate how far the item would move up
            Vector3 itemCurrentPos = item.transform.position;
            Vector3 itemNewPos = itemCurrentPos + new Vector3(0, crouchAmount, 0);

            // Check if this position would cause collisions
            bool wasActive = item.activeSelf;
            item.SetActive(false); // Temporarily disable so it doesn't collide with itself

            RaycastHit2D hit = Physics2D.Linecast(itemCurrentPos, itemNewPos, groundLayer);

            item.SetActive(wasActive); // Restore original state

            if (hit.collider != null)
            {
                return false; // Collision detected, can't stand up
            }
        }

        return true; // All checks passed, can stand up
    }

    void HandleCrouch()
    {
        if (bodySprite != null)
        {
            if (Input.GetKey(KeyCode.S))
            {
                // Only play crouch sound when initially crouching
                if (!isCrouching && oneSounds != null)
                {
                    oneSounds.PlayCrouchAudio();
                }

                isCrouching = true;
                isAttemptingToStand = false;

                Vector3 targetPos = new Vector3(originalBodyPosition.x, originalBodyPosition.y - crouchAmount, originalBodyPosition.z);
                bodySprite.localPosition = Vector3.Lerp(bodySprite.localPosition, targetPos, Time.deltaTime * crouchSpeed);

                if (bodyMiddle != null)
                {
                    Vector3 middleTargetPos = new Vector3(originalBodyMiddlePosition.x, originalBodyMiddlePosition.y - crouchAmount, originalBodyMiddlePosition.z);
                    bodyMiddle.localPosition = Vector3.Lerp(bodyMiddle.localPosition, middleTargetPos, Time.deltaTime * crouchSpeed);
                }

                if (bodyCollider != null)
                {
                    Vector2 crouchSize = originalBodyColliderSize;
                    crouchSize.y *= crouchColliderReduction;
                    bodyCollider.size = crouchSize;

                    Vector2 crouchOffset = originalBodyColliderOffset;
                    crouchOffset.y -= (originalBodyColliderSize.y - crouchSize.y) / 2;
                    bodyCollider.offset = crouchOffset;
                }
            }
            else if (isCrouching)
            {
                isAttemptingToStand = true;
                bool canStand = CanStandUp();

                if (canStand)
                {
                    // Only play uncrouch sound when starting to uncrouch
                    if (isAttemptingToStand && Vector3.Distance(bodySprite.localPosition, originalBodyPosition) > 0.05f && oneSounds != null)
                    {
                        oneSounds.PlayUncrouchAudio();
                    }

                    bodySprite.localPosition = Vector3.Lerp(bodySprite.localPosition, originalBodyPosition, Time.deltaTime * crouchSpeed);

                    if (bodyMiddle != null)
                    {
                        bodyMiddle.localPosition = Vector3.Lerp(bodyMiddle.localPosition, originalBodyMiddlePosition, Time.deltaTime * crouchSpeed);
                    }

                    if (bodyCollider != null)
                    {
                        bodyCollider.size = Vector2.Lerp(bodyCollider.size, originalBodyColliderSize, Time.deltaTime * crouchSpeed);
                        bodyCollider.offset = Vector2.Lerp(bodyCollider.offset, originalBodyColliderOffset, Time.deltaTime * crouchSpeed);
                    }

                    if (Vector3.Distance(bodySprite.localPosition, originalBodyPosition) < 0.01f)
                    {
                        isCrouching = false;
                        isAttemptingToStand = false;
                        bodySprite.localPosition = originalBodyPosition;
                        if (bodyMiddle != null) bodyMiddle.localPosition = originalBodyMiddlePosition;

                        if (bodyCollider != null)
                        {
                            bodyCollider.size = originalBodyColliderSize;
                            bodyCollider.offset = originalBodyColliderOffset;
                        }
                    }
                }
                else
                {
                    // We tried to stand but couldn't - keep crouched
                    Vector3 targetPos = new Vector3(originalBodyPosition.x, originalBodyPosition.y - crouchAmount, originalBodyPosition.z);
                    bodySprite.localPosition = Vector3.Lerp(bodySprite.localPosition, targetPos, Time.deltaTime * crouchSpeed);

                    if (bodyMiddle != null)
                    {
                        Vector3 middleTargetPos = new Vector3(originalBodyMiddlePosition.x, originalBodyMiddlePosition.y - crouchAmount, originalBodyMiddlePosition.z);
                        bodyMiddle.localPosition = Vector3.Lerp(bodyMiddle.localPosition, middleTargetPos, Time.deltaTime * crouchSpeed);
                    }
                }
            }
        }
    }

    // Called by CollisionReporter when a proxy trigger collides with something
    public void HandleTriggerCollision(Vector2 collisionDirection, GameObject obstacle)
    {
        // Set blocking direction 
        blockedDirection = collisionDirection.normalized;
        isTriggerBlocking = true;

        // Set a timer to reset the blocking (in case we get stuck)
        triggerBlockResetTime = 0.2f;

        // Maybe play a sound
        if (oneSounds != null)
        {
            oneSounds.PlayBumpAudio();
        }
    }

    // This handles continuous trigger collision
    public void HandleTriggerCollisionStay(Vector2 collisionDirection, GameObject obstacle)
    {
        // Update the blocked direction
        blockedDirection = collisionDirection.normalized;
        isTriggerBlocking = true;

        // Reset the timer while we're still colliding
        triggerBlockResetTime = 0.2f;
    }

    // Legacy proxy collision handling methods - these can now be simplified since we're using triggers
    public void HandleProxyCollision(Collision2D collision, GameObject proxyObject)
    {
        // Skip collision handling if temporarily disabled
        if (!proxyCollisionsEnabled || isHandlingProxyCollision) return;

        // Get the collision normal
        ContactPoint2D contact = collision.GetContact(0);
        collisionNormal = contact.normal;

        // Only apply a small collision response if the relative velocity is high
        if (collision.relativeVelocity.magnitude > 2.0f)
        {
            StartCoroutine(ApplyPushback());
        }
        else
        {
            // For small collisions, just stop movement in that direction
            float dotProduct = Vector2.Dot(new Vector2(currentMoveVelocity, 0), -collisionNormal);
            if (dotProduct > 0)
            {
                // Stop velocity in the direction of the collision
                currentMoveVelocity = 0;
            }
        }

        // Play bump sound if it's a significant collision
        if (collision.relativeVelocity.magnitude > 3f && oneSounds != null)
        {
            oneSounds.PlayBumpAudio();
        }
    }

    public void HandleProxyCollisionStay(Collision2D collision, GameObject proxyObject)
    {
        // This can now be minimal since we're using triggers
        if (!proxyCollisionsEnabled) return;

        // Get the collision normal
        ContactPoint2D contact = collision.GetContact(0);
        collisionNormal = contact.normal;

        // Check if we're trying to move against the collision
        float dotProduct = Vector2.Dot(new Vector2(currentMoveVelocity, 0), -collisionNormal);

        // If we're pushing against the collision, reduce our velocity
        if (dotProduct > 0)
        {
            // Reduce velocity in the direction of the collision
            Vector2 velocityComponent = dotProduct * -collisionNormal;
            currentMoveVelocity -= velocityComponent.x;
        }
    }

    // For legacy support - can be simplified with triggers
    private IEnumerator ApplyPushback()
    {
        isHandlingProxyCollision = true;

        // Apply a small velocity in the direction of the normal
        rb.linearVelocity = collisionNormal * collisionBounceForce;

        // Pause the robot's ability to accelerate for a moment
        float originalAcceleration = acceleration;
        acceleration = 0;

        yield return new WaitForSeconds(collisionPushbackDelay);

        // Restore acceleration
        acceleration = originalAcceleration;
        isHandlingProxyCollision = false;
    }

    // Method to freeze position during attachment
    public void FreezePositionForAttachment(float duration)
    {
        // Save current position
        savedPosition = transform.position;

        // Set flags
        isAttachmentFreezing = true;
        freezeTimer = duration;

        // Completely zero out all velocities
        rb.linearVelocity = Vector2.zero;
        currentMoveVelocity = 0f;

        // Disable proxy collisions
        DisableProxyCollisionsTemporarily(duration);
    }

    // Method to disable proxy collisions temporarily
    public void DisableProxyCollisionsTemporarily(float duration)
    {
        proxyCollisionsEnabled = false;
        collisionDisableTimer = duration;

        // Also reset any existing proxy collision state
        isHandlingProxyCollision = false;
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            // Draw ground check rays
            if (wheelCollider != null)
            {
                Vector2 circleBottom = (Vector2)transform.position - new Vector2(0, wheelCollider.radius);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(circleBottom, circleBottom + Vector2.down * groundCheckDistance);
                Gizmos.DrawLine(circleBottom - new Vector2(wheelCollider.radius * 0.5f, 0),
                                circleBottom - new Vector2(wheelCollider.radius * 0.5f, 0) + Vector2.down * groundCheckDistance);
                Gizmos.DrawLine(circleBottom + new Vector2(wheelCollider.radius * 0.5f, 0),
                                circleBottom + new Vector2(wheelCollider.radius * 0.5f, 0) + Vector2.down * groundCheckDistance);
            }

            // Draw ceiling check
            if (ceilingCheck != null)
            {
                Gizmos.color = CanStandUp() ? Color.green : Color.red;
                for (int i = -1; i <= 1; i++)
                {
                    Vector2 checkPos = (Vector2)ceilingCheck.position + new Vector2(i * 0.3f, 0);
                    Gizmos.DrawWireSphere(checkPos, ceilingCheckRadius);
                }
            }

            // Draw blocked direction if active
            if (isTriggerBlocking)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position, blockedDirection * 1.5f);
            }

            // Draw attached item collision bounds for debugging
            if (attachmentHandler != null)
            {
                List<GameObject> attachedItems = attachmentHandler.GetAllAttachedItems();
                foreach (GameObject item in attachedItems)
                {
                    if (item == null) continue;

                    Collider2D itemCollider = item.GetComponent<Collider2D>();
                    if (itemCollider == null) continue;

                    // Get the bounds of the item's collider
                    Bounds itemBounds = itemCollider.bounds;

                    // Draw the current bounds
                    Gizmos.color = new Color(0, 1, 0, 0.3f);
                    Gizmos.DrawCube(itemBounds.center, itemBounds.size);

                    // Draw the predicted movement bounds (if we're moving)
                    if (Mathf.Abs(currentMoveVelocity) > 0.01f)
                    {
                        Vector2 moveDir = new Vector2(Mathf.Sign(currentMoveVelocity), 0);

                        // Expanded bounds
                        Vector3 expansion = new Vector3(
                            Mathf.Abs(moveDir.x) * 0.1f,
                            0.02f,
                            0
                        );

                        Bounds checkBounds = itemBounds;
                        checkBounds.Expand(expansion);

                        // Move in the direction of movement
                        Vector3 offset = new Vector3(moveDir.x * Mathf.Abs(currentMoveVelocity) * Time.fixedDeltaTime, 0, 0);
                        checkBounds.center += offset;

                        // Draw the prediction bounds
                        Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
                        Gizmos.DrawCube(checkBounds.center, checkBounds.size);
                        Gizmos.DrawWireCube(checkBounds.center, checkBounds.size);
                    }

                    // Draw grounding raycasts
                    Gizmos.color = Color.blue;

                    Vector2 bottom = new Vector2(
                        itemCollider.bounds.center.x,
                        itemCollider.bounds.min.y
                    );

                    Vector2 bottomLeft = new Vector2(
                        itemCollider.bounds.min.x,
                        itemCollider.bounds.min.y
                    );

                    Vector2 bottomRight = new Vector2(
                        itemCollider.bounds.max.x,
                        itemCollider.bounds.min.y
                    );

                    Gizmos.DrawLine(bottom, bottom + Vector2.down * 0.1f);
                    Gizmos.DrawLine(bottomLeft, bottomLeft + Vector2.down * 0.1f);
                    Gizmos.DrawLine(bottomRight, bottomRight + Vector2.down * 0.1f);
                }
            }

            // Draw stable position reference
            if (lastStablePosition != Vector3.zero)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(lastStablePosition, 0.1f);
            }
        }
    }
}