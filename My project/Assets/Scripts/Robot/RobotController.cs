using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class RobotController : MonoBehaviour
{
    #region Component References
    private Rigidbody2D rb;
    private CircleCollider2D wheelCollider;
    private BoxCollider2D bodyCollider;
    private LoopSoundEffects loopSounds;
    private OneSoundEffects oneSounds;
    private AttachmentPreview attachmentPreview;
    private DropModeManager dropModeManager;
    private WeightManager weightManager;
    #endregion

    #region Movement Settings
    [Header("Movement Settings")]
    public float maxMoveSpeed = 5f;
    public float acceleration = 25f;
    public float deceleration = 40f;
    private float currentMoveVelocity = 0f;
    public float jumpForce = 8f;
    #endregion

    #region Death Settings
    [Header("Death Settings")]
    public Sprite brokenHeadSprite; // Assign in inspector
    public GameObject gameOverUI;   // UI Panel with "GAME OVER"
    public float deathDelay = 2f;
    #endregion

    #region Ground Check
    [Header("Ground Check")]
    public float groundCheckDistance = 0.6f;
    public float groundedBufferTime = 0.1f;
    private float lastGroundedTime = 0f;
    private bool isGrounded;
    #endregion

    #region Body Settings
    [Header("Body Settings")]
    public Transform bodySprite;
    public Transform bodyMiddle;
    public Transform wheelSprite;
    public Vector2 bodyColliderSize = new Vector2(0.8f, 1.2f);
    private Vector3 originalBodyPosition;
    private Vector3 originalBodyMiddlePosition;
    private Vector2 originalBodyColliderSize;
    private Vector2 originalBodyColliderOffset;
    #endregion

    #region Crouch Settings
    [Header("Crouch Settings")]
    public float crouchAmount = 0.3f;
    public float crouchSpeed = 5f;
    public float crouchColliderReduction = 0.5f;
    private bool isCrouching = false;
    private bool isAttemptingToStand = false;

    public Transform ceilingCheck;
    public float ceilingCheckRadius = 0.1f;
    public LayerMask groundLayer;
    public float sideCheckDistance = 0.2f;
    #endregion

    #region Sway Settings
    [Header("Sway Settings")]
    public float swayAmount = 0.1f;
    public float swaySpeed = 3f;
    public float swayResponsiveness = 5f;
    public float passiveSwayAmount = 3f; // Strength of weight-based passive sway
    private float currentSwayAngle = 0f;
    private float swayVelocity = 0f;
    private float passiveSwayAngle = 0f;
    private float passiveSwayVelocity = 0f;
    #endregion

    #region Weight Balance Settings
    [Header("Weight Balance Settings")]
    public float weightSwayMultiplier = 1.5f;  // How much weight affects swaying
    public float maxWeightImbalanceEffect = 0.7f; // Maximum effect of weight on movement (0-1)
    #endregion

    #region Attachment Settings
    [Header("Attachment")]
    public AttachmentHandler attachmentHandler;
    #endregion

    #region Collision Handling
    [Header("Collision Handling")]
    public float collisionBounceForce = 0.5f;
    public float collisionPushbackDelay = 0.1f;
    private bool isHandlingProxyCollision = false;
    private Vector2 collisionNormal;
    private bool proxyCollisionsEnabled = true;
    private float collisionDisableTimer = 0f;
    private bool isTriggerBlocking = false;
    private Vector2 blockedDirection = Vector2.zero;
    private float triggerBlockResetTime = 0f;
    #endregion

    #region Position Handling
    // Position freezing for attachment operations
    private bool isAttachmentFreezing = false;
    private float freezeTimer = 0f;
    private Vector3 savedPosition;

    // Stable position tracking to prevent sinking
    private Vector3 lastStablePosition;
    private bool isTrackingStablePosition = false;
    private float stablePositionTimer = 0f;
    #endregion

    #region Input Handling
    private float horizontalInput;
    private bool lastEKeyState = false;
    private bool lastQKeyState = false;
    #endregion

    void Start()
    {
        // Initialize components
        InitializeComponents();

        // Set initial states
        if (bodySprite != null)
        {
            originalBodyPosition = bodySprite.localPosition;
            SetupBodyCollider();
        }

        if (bodyMiddle != null)
        {
            originalBodyMiddlePosition = bodyMiddle.localPosition;
        }

        // Prevent rotation
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        //
        gameOverUI.SetActive(false);
    }

    void InitializeComponents()
    {
        // Get or add required components
        rb = GetComponent<Rigidbody2D>();
        wheelCollider = GetComponent<CircleCollider2D>();
        loopSounds = GetComponent<LoopSoundEffects>();
        oneSounds = GetComponent<OneSoundEffects>();

        // Find body sprite if not set
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

        // Get or add attachment handler
        if (attachmentHandler == null)
        {
            attachmentHandler = GetComponentInChildren<AttachmentHandler>();
            if (attachmentHandler == null)
                Debug.LogWarning("AttachmentHandler not assigned and not found in children.");
        }

        // Get or add attachment preview
        attachmentPreview = GetComponent<AttachmentPreview>();
        if (attachmentPreview == null)
        {
            attachmentPreview = gameObject.AddComponent<AttachmentPreview>();
        }

        // Get or add drop mode manager
        dropModeManager = GetComponent<DropModeManager>();
        if (dropModeManager == null)
        {
            dropModeManager = gameObject.AddComponent<DropModeManager>();
        }

        // Get or add weight manager
        weightManager = GetComponent<WeightManager>();
        if (weightManager == null)
        {
            weightManager = gameObject.AddComponent<WeightManager>();
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

     public void Die()
    {
        // 1. Play death audio and stop other audio
        oneSounds.PlayDeathAudio();
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Noisy")) {
            obj.GetComponent<ObjectSoundEffects>().PauseAudio();
        }
        
        // 2. Detach & enable physics on children
        foreach (Transform child in transform)
        {
            var rb = child.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType      = RigidbodyType2D.Dynamic;
                rb.gravityScale  = 1;
                rb.constraints   = RigidbodyConstraints2D.None;
            }
            child.SetParent(null);
        }

        // 3. Swap head sprite immediately
        var head = transform.Find("HeadSprite");
        if (head != null)
        {
            var sr = head.GetComponent<SpriteRenderer>();
            if (sr != null && brokenHeadSprite != null)
                sr.sprite = brokenHeadSprite;
        }

        // 4. Stop player control
        this.enabled = false;

        // 5. Schedule the UI+pause after a delay
        Invoke(nameof(ShowGameOver), deathDelay);
    }

    private void ShowGameOver()
    {
        if (gameOverUI != null)
            gameOverUI.SetActive(true);

        Time.timeScale = 0f;
        loopSounds.PauseAudio();
        oneSounds.PauseAllAudio();
    }


    void Update()
    {
        // Update weight distribution
        if (weightManager != null)
        {
            weightManager.UpdateWeightDistribution();
        }

        // Track stable position to prevent sinking
        TrackStablePosition();

        // Get horizontal input if not in special modes
        horizontalInput = 0f;
        if (!attachmentPreview.IsInPreviewMode() && !dropModeManager.IsInDropMode())
        {
            if (Input.GetKey(KeyCode.A)) horizontalInput = -1f;
            if (Input.GetKey(KeyCode.D)) horizontalInput = 1f;
        }

        // Handle movement sound
        HandleMovementSound();

        // Track grounded state
        if (isGrounded)
        {
            lastGroundedTime = Time.time;
        }

        // Handle jumping
        HandleJumping();

        // Handle crouch and sway if not in attachment/drop mode
        if (!attachmentPreview.IsInPreviewMode() && !dropModeManager.IsInDropMode())
        {
            HandleCrouch();
            HandleSway();
        }

        // Handle attachment system
        HandleAttachmentSystem();

        // Handle position freezing for attachment
        HandlePositionFreezing();

        // Handle collision timers
        HandleCollisionTimers();
    }

    private void TrackStablePosition()
    {
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
    }

    private void HandleMovementSound()
    {
        if (Mathf.Abs(currentMoveVelocity) > 0.5f && isGrounded)
        {
            if (loopSounds != null) loopSounds.PlayMoveAudio();
        }
        else
        {
            if (loopSounds != null) loopSounds.StopAudio();
        }
    }

    private void HandleJumping()
    {
        // Allow jumping if we were recently grounded and not in attachment/drop mode
        bool canJump = Time.time - lastGroundedTime < groundedBufferTime &&
                      !attachmentPreview.IsInPreviewMode() &&
                      !dropModeManager.IsInDropMode();

        if (Input.GetKeyDown(KeyCode.Space) && canJump)
        {
            // Check if jumping would cause attached items to clip through ceilings
            bool canJumpWithAttachments = CanJumpWithAttachments();

            // Only jump if we can do so safely
            if (canJumpWithAttachments || attachmentHandler.GetAllAttachedItems().Count == 0)
            {
                // Apply weight penalty to jump force if heavily loaded
                float jumpModifier = 1.0f;
                if (weightManager != null)
                {
                    float totalWeight = weightManager.GetTotalWeight();
                    float baseWeight = weightManager.baseRobotWeight;

                    // Reduce jump force based on additional weight
                    jumpModifier = Mathf.Lerp(1.0f, 0.6f, Mathf.Clamp01((totalWeight - baseWeight) / 15f));
                }

                // Execute jump
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
                rb.AddForce(Vector2.up * jumpForce * jumpModifier, ForceMode2D.Impulse);
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
    }

    private bool CanJumpWithAttachments()
    {
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
            jumpBounds.center += new Vector3(0, 0.2f, 0);    // Move up to simulate jump

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
                return false;
            }
        }

        return true;
    }

    void HandleSway()
    {
        if (bodySprite == null) return;

        // Get weight-based passive sway
        float weightSway = 0f;
        if (weightManager != null)
        {
            weightSway = weightManager.GetWeightSwayAngle() * weightSwayMultiplier;
        }

        // Target sway based on horizontal input and robot velocity
        float inputSway = -horizontalInput * swayAmount;

        // Blend input sway with passive weight sway
        float targetSway = inputSway;

        // Only apply passive weight sway when not getting horizontal input
        if (Mathf.Abs(horizontalInput) < 0.1f)
        {
            // Smoothly blend toward weight-based sway
            passiveSwayAngle = Mathf.SmoothDamp(
                passiveSwayAngle,
                weightSway,
                ref passiveSwayVelocity,
                0.5f, // Slower to respond than input sway
                passiveSwayAmount
            );

            targetSway = passiveSwayAngle;
        }
        else
        {
            // Input is active, but still add a bit of weight influence
            targetSway = inputSway + (weightSway * 0.3f);

            // Reset passive sway
            passiveSwayAngle = 0f;
        }

        // Apply smooth damping for natural swaying motion
        float smoothTime = isGrounded ? 0.1f : 0.3f; // Less responsive in air
        float newSwayAngle = Mathf.SmoothDamp(currentSwayAngle, targetSway, ref swayVelocity, smoothTime, swaySpeed);

        // Check if the new sway angle would cause clipping and get a safe angle if it would
        float safeAngle = newSwayAngle;
        bool wouldClip = WouldSwayAngleCauseClipping(newSwayAngle, out safeAngle);

        // Use the safe angle
        currentSwayAngle = safeAngle;

        // Apply sway to the robot body
        ApplySwayRotation(isCrouching ? currentSwayAngle * 0.5f : currentSwayAngle);
    }

    private void ApplySwayRotation(float angle)
    {
        // Apply sway to the body sprite
        if (bodySprite != null)
        {
            Vector3 currentRot = bodySprite.localRotation.eulerAngles;
            bodySprite.localRotation = Quaternion.Euler(currentRot.x, currentRot.y, angle);
        }

        // Apply the same sway to attachments by rotating the entire transform except wheel
        if (wheelSprite != null)
        {
            // Keep the wheel rotation separate
            Quaternion wheelRot = wheelSprite.localRotation;
            transform.localRotation = Quaternion.Euler(0, 0, angle);
            wheelSprite.localRotation = wheelRot; // Preserve wheel rotation
        }
        else
        {
            transform.localRotation = Quaternion.Euler(0, 0, angle);
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
        transform.position = originalPosition;

        // Return the best safe angle we found
        maxSafeAngle = bestAngle;

        return wouldClip;
    }

    private void HandleAttachmentSystem()
    {
        bool currentEKeyState = Input.GetKey(KeyCode.E);
        bool currentQKeyState = Input.GetKey(KeyCode.Q);

        // E key pressed - toggle attachment preview mode
        if (Input.GetKeyDown(KeyCode.E) && !currentQKeyState)
        {
            if (attachmentPreview.IsInPreviewMode())
            {
                // Confirm attachment if in preview mode
                attachmentPreview.EndPreviewMode(true);
            }
            else if (!dropModeManager.IsInDropMode())
            {
                // Enter preview mode if not in drop mode
                attachmentPreview.StartPreviewMode();
            }
        }

        // Q key pressed - toggle drop mode
        if (Input.GetKeyDown(KeyCode.Q) && !currentEKeyState)
        {
            if (dropModeManager.IsInDropMode())
            {
                // Confirm drop if in drop mode
                dropModeManager.ExitDropMode(true);
            }
            else if (!attachmentPreview.IsInPreviewMode() && attachmentHandler.GetAllAttachedItems().Count > 0)
            {
                // Enter drop mode if not in preview mode and have attachments
                dropModeManager.EnterDropMode();
            }
        }

        // X key to cancel either mode
        if (Input.GetKeyDown(KeyCode.X))
        {
            if (attachmentPreview.IsInPreviewMode())
            {
                attachmentPreview.EndPreviewMode(false);
            }

            if (dropModeManager.IsInDropMode())
            {
                dropModeManager.ExitDropMode(false);
            }
        }

        // If in preview mode, let attachment preview handle arrow keys
        if (attachmentPreview.IsInPreviewMode())
        {
            attachmentPreview.AdjustPreviewPosition();
        }
        // If in drop mode, let drop mode manager handle input directly
        else if (dropModeManager.IsInDropMode())
        {
            dropModeManager.HandleDropModeInput();
        }

        // Update key states for next frame
        lastEKeyState = currentEKeyState;
        lastQKeyState = currentQKeyState;
    }

    private void HandlePositionFreezing()
    {
        if (isAttachmentFreezing)
        {
            freezeTimer -= Time.deltaTime;

            // Force position to stay the same during freeze
            transform.position = savedPosition;

            // Set velocity to zero
            if (rb != null) rb.linearVelocity = Vector2.zero;

            // Cancel freezing if timer expired
            if (freezeTimer <= 0) isAttachmentFreezing = false;
        }
    }

    private void HandleCollisionTimers()
    {
        // Handle collision disable timer
        if (!proxyCollisionsEnabled)
        {
            collisionDisableTimer -= Time.deltaTime;
            if (collisionDisableTimer <= 0) proxyCollisionsEnabled = true;
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

    void FixedUpdate()
    {
        // Skip normal movement updates if freezing
        if (isAttachmentFreezing) return;

        // Calculate base target velocity
        float targetVelocity = horizontalInput * maxMoveSpeed;

        // Apply weight imbalance effects to movement
        if (weightManager != null && Mathf.Abs(horizontalInput) > 0.01f)
        {
            // Get speed multiplier based on weight and direction
            float speedMultiplier = weightManager.GetSpeedMultiplier(horizontalInput);

            // Apply to target velocity
            targetVelocity *= speedMultiplier;
        }

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

        // Apply acceleration or deceleration (with weight adjustments)
        if (horizontalInput != 0 && targetVelocity != 0)
        {
            // Get acceleration multiplier based on weight
            float accelMultiplier = 1.0f;
            if (weightManager != null)
            {
                accelMultiplier = weightManager.GetAccelerationMultiplier(horizontalInput);
            }

            // Accelerate toward target velocity with weight adjustment
            currentMoveVelocity = Mathf.MoveTowards(
                currentMoveVelocity,
                targetVelocity,
                acceleration * accelMultiplier * Time.fixedDeltaTime);
        }
        else
        {
            // Decelerate toward zero when no input or blocked
            // Use different deceleration based on weight imbalance
            float decelMultiplier = 1.0f;
            if (weightManager != null)
            {
                // When slowing down, heavier side causes faster deceleration (momentum)
                float imbalance = weightManager.GetWeightImbalance();
                float directionSign = Mathf.Sign(currentMoveVelocity);

                // If moving toward the heavy side, decelerate slower (momentum)
                // If moving away from heavy side, decelerate faster (less momentum)
                if (Mathf.Sign(imbalance) == directionSign)
                {
                    // Moving toward heavy side - harder to stop
                    decelMultiplier = 1.0f - (Mathf.Abs(imbalance) * 0.3f);
                }
                else
                {
                    // Moving away from heavy side - easier to stop
                    decelMultiplier = 1.0f + (Mathf.Abs(imbalance) * 0.3f);
                }
            }

            currentMoveVelocity = Mathf.MoveTowards(
                currentMoveVelocity,
                0f,
                deceleration * decelMultiplier * Time.fixedDeltaTime);
        }

        // Store the current vertical velocity since PreventClipping might modify it
        float currentVerticalVelocity = rb.linearVelocity.y;

        // Set horizontal velocity (vertical will be managed by PreventClipping)
        rb.linearVelocity = new Vector2(currentMoveVelocity, currentVerticalVelocity);

        // Run the clipping prevention check
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

            // Use much larger horizontal expansion to prevent any wall clipping
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
                // Store current Y position
                float currentY = transform.position.y;

                // Move slightly away from the wall (in the opposite direction of movement)
                float correctionAmount = Mathf.Min(horizontalPenetration, 0.01f);
                Vector3 correction = new Vector3(-moveDir.x * correctionAmount, 0, 0);
                transform.position += correction;

                // Restore original Y position to prevent sinking
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

    // For handling proxy collisions (legacy support)
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

    // Helper method to get debug info for UI
    public string GetWeightDebugInfo()
    {
        if (weightManager == null) return "Weight system not active";

        return string.Format(
            "Left: {0:F1}  Right: {1:F1}  Top: {2:F1}\nImbalance: {3:F2}  Total: {4:F1}",
            weightManager.GetLeftSideWeight(),
            weightManager.GetRightSideWeight(),
            weightManager.GetTopSideWeight(),
            weightManager.GetWeightImbalance(),
            weightManager.GetTotalWeight()
        );
    }
}