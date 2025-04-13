using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float maxMoveSpeed = 5f;
    public float acceleration = 25f;     // How quickly the robot accelerates
    public float deceleration = 40f;     // How quickly the robot decelerates when no input
    private float currentMoveVelocity = 0f; // Current horizontal velocity
    public float jumpForce = 8f;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.6f;
    public float groundedBufferTime = 0.1f; // Time to remember being grounded (helps with jump timing)
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
    public float swayAmount = 0.1f;      // Amount of sway
    public float swaySpeed = 3f;         // Speed of sway
    public float swayResponsiveness = 5f; // How responsive the sway is to input change
    private float currentSwayAngle = 0f;  // Current sway angle
    private float swayVelocity = 0f;      // Current sway velocity for smooth damping
    private float lastSafeSwayAngle = 0f; // Last sway angle that didn't cause clipping

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
    public float sideCheckDistance = 0.2f; // Distance to check for side obstacles when uncrouching

    private Vector3 originalBodyPosition;
    private Vector2 originalBodyColliderSize;
    private Vector2 originalBodyColliderOffset;

    [Header("Attachment")]
    public AttachmentHandler attachmentHandler;

    [Header("Movement Validation")]
    public LayerMask obstacleLayer; // Same layer mask as in AttachmentHandler
    public float collisionBuffer = 0.05f; // Buffer to prevent getting stuck on edges

    [Header("Physics")]
    public bool allowBottomCollisions = true; // Whether items can hit obstacles from below

    // Sound effects
    private LoopSoundEffects loopSounds;
    private OneSoundEffects oneSounds;

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
        horizontalInput = 0f;

        if (Input.GetKey(KeyCode.A)) horizontalInput = -1f;
        if (Input.GetKey(KeyCode.D)) horizontalInput = 1f;

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

        // Allow jumping if we were recently grounded
        bool canJump = Time.time - lastGroundedTime < groundedBufferTime;
        if (Input.GetKeyDown(KeyCode.Space) && canJump)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            isGrounded = false;

            // Play jump sound
            if (oneSounds != null) oneSounds.PlayJumpAudio();
        }

        HandleCrouch();
        HandleSway();

        // Attachment keys (delegate to handler)
        if (Input.GetKeyDown(KeyCode.RightArrow))
            attachmentHandler.ToggleAttachment(AttachmentHandler.AttachmentSide.Right);
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
            attachmentHandler.ToggleAttachment(AttachmentHandler.AttachmentSide.Left);
        else if (Input.GetKeyDown(KeyCode.UpArrow))
            attachmentHandler.ToggleAttachment(AttachmentHandler.AttachmentSide.Top);
    }

    void HandleSway()
    {
        if (bodySprite == null) return;

        // Target sway based on horizontal input and robot velocity
        float targetSway = -horizontalInput * swayAmount;

        // Apply smooth damping for natural swaying motion
        float smoothTime = isGrounded ? 0.1f : 0.3f; // Less responsive in air
        float previousSwayAngle = currentSwayAngle;
        currentSwayAngle = Mathf.SmoothDamp(currentSwayAngle, targetSway, ref swayVelocity, smoothTime, swaySpeed);

        // Check if the new sway angle would cause any attached items to clip through obstacles
        if (WouldSwayAngleCauseClipping(currentSwayAngle))
        {
            // If it would cause clipping, revert to the previous angle
            currentSwayAngle = previousSwayAngle;
            swayVelocity = 0f; // Reset velocity to prevent oscillation

            // Try to find a safe angle between the current and target
            float testAngle = currentSwayAngle;
            float step = (targetSway - currentSwayAngle) / 10f; // Test 10 steps

            // Only try to find safe angle if we're moving toward the target
            if (Mathf.Abs(step) > 0.001f)
            {
                for (int i = 0; i < 10; i++)
                {
                    testAngle += step;
                    if (!WouldSwayAngleCauseClipping(testAngle))
                    {
                        // Found a safe angle
                        currentSwayAngle = testAngle;
                        lastSafeSwayAngle = testAngle;
                        break;
                    }
                }
            }
        }
        else
        {
            // If the angle is safe, remember it
            lastSafeSwayAngle = currentSwayAngle;
        }

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

    // Check if a given sway angle would cause any attached items to clip through obstacles
    bool WouldSwayAngleCauseClipping(float swayAngle)
    {
        if (attachmentHandler == null) return false;

        // Get all attached items
        List<GameObject> allAttachedItems = GetAllAttachedItems();
        if (allAttachedItems.Count == 0) return false;

        // Store current rotation
        Quaternion originalRotation = transform.rotation;

        // Temporarily rotate to test angle
        transform.rotation = Quaternion.Euler(0, 0, swayAngle);

        bool wouldClip = false;

        // Check each attached item for potential collisions
        foreach (GameObject item in allAttachedItems)
        {
            if (item == null) continue;

            Collider2D itemCollider = item.GetComponent<Collider2D>();
            if (itemCollider == null) continue;

            // Store current state
            bool wasEnabled = itemCollider.enabled;
            itemCollider.enabled = true;

            // Get bounds at this rotation
            Bounds itemBounds = itemCollider.bounds;

            // Check for overlaps with obstacles
            Collider2D[] overlaps = Physics2D.OverlapBoxAll(
                itemBounds.center,
                itemBounds.size * 0.95f, // Slightly smaller to avoid edge cases
                swayAngle,
                obstacleLayer);

            // Filter out self-collisions
            foreach (Collider2D overlap in overlaps)
            {
                if (overlap.gameObject != gameObject &&
                    !overlap.transform.IsChildOf(transform) &&
                    !transform.IsChildOf(overlap.transform))
                {
                    wouldClip = true;
                    break;
                }
            }

            // Restore collider state
            itemCollider.enabled = wasEnabled;

            if (wouldClip) break;
        }

        // Restore original rotation
        transform.rotation = originalRotation;

        return wouldClip;
    }

    void FixedUpdate()
    {
        // Handling acceleration-based movement
        float targetVelocity = horizontalInput * maxMoveSpeed;

        // Apply acceleration or deceleration based on input
        if (horizontalInput != 0)
        {
            // Accelerate toward target velocity
            currentMoveVelocity = Mathf.MoveTowards(
                currentMoveVelocity,
                targetVelocity,
                acceleration * Time.fixedDeltaTime);
        }
        else
        {
            // Decelerate toward zero when no input
            currentMoveVelocity = Mathf.MoveTowards(
                currentMoveVelocity,
                0f,
                deceleration * Time.fixedDeltaTime);
        }

        // Create the intended velocity vector
        Vector2 intendedVelocity = new Vector2(currentMoveVelocity, rb.linearVelocity.y);

        // Check if the movement would cause collisions
        if (CanMove(intendedVelocity))
        {
            rb.linearVelocity = intendedVelocity;
        }
        else
        {
            // If movement not allowed, stop horizontal movement but keep vertical
            currentMoveVelocity = 0f; // Reset current velocity
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

            // Play bump sound if we tried to move but couldn't
            if (Mathf.Abs(horizontalInput) > 0.1f && oneSounds != null)
            {
                oneSounds.PlayBumpAudio();
            }
        }

        CheckGrounded();
        CheckForAttachmentBottomCollisions();

        // We handle body rotation in the HandleSway method now
        if (wheelSprite != null)
        {
            float rotationAmount = -rb.linearVelocity.x * 360f * Time.fixedDeltaTime;
            wheelSprite.Rotate(Vector3.forward, rotationAmount);
        }
    }

    // Check if movement is allowed (won't cause clipping through obstacles)
    bool CanMove(Vector2 velocity)
    {
        // If no horizontal movement, always allow
        if (Mathf.Approximately(velocity.x, 0f))
            return true;

        // Get movement direction (1 for right, -1 for left)
        float direction = Mathf.Sign(velocity.x);
        float moveDistance = Mathf.Abs(velocity.x * Time.fixedDeltaTime);

        // Create a starting position slightly above the ground to avoid ground collisions
        Vector2 castOrigin = (Vector2)transform.position + new Vector2(0, collisionBuffer);

        // Check robot wheel collider first (this is most important for base movement)
        if (wheelCollider != null)
        {
            // Only check for horizontal collisions, not vertical
            RaycastHit2D wheelHit = Physics2D.CircleCast(
                castOrigin,
                wheelCollider.radius * 0.95f, // Slightly smaller than actual to avoid edge cases
                new Vector2(direction, 0),
                moveDistance,
                obstacleLayer);

            if (wheelHit.collider != null)
            {
                // Only block if we're not trying to move onto a platform
                Vector2 hitNormal = wheelHit.normal;
                float angle = Vector2.Angle(hitNormal, Vector2.up);
                if (angle > 30f) // Not a flat surface we can roll on
                {
                    // Debug visualization
                    Debug.DrawLine(castOrigin, wheelHit.point, Color.red, 0.1f);
                    return false;
                }
            }
        }

        // Check body collider
        if (bodyCollider != null && !isCrouching)
        {
            Vector2 bodyCenter = (Vector2)bodySprite.position + bodyCollider.offset;

            RaycastHit2D bodyHit = Physics2D.BoxCast(
                bodyCenter,
                bodyCollider.size * 0.95f, // Slightly smaller than actual
                bodySprite.rotation.eulerAngles.z, // Include rotation for accurate collision
                new Vector2(direction, 0),
                moveDistance,
                obstacleLayer);

            if (bodyHit.collider != null)
            {
                // Only block if we're not trying to move onto a platform
                Vector2 hitNormal = bodyHit.normal;
                float angle = Vector2.Angle(hitNormal, Vector2.up);
                if (angle > 30f) // Not a flat surface we can roll on
                {
                    // Debug visualization
                    Debug.DrawLine(bodyCenter, bodyHit.point, Color.red, 0.1f);
                    return false;
                }
            }
        }

        // Check all attachment proxy colliders
        if (attachmentHandler != null)
        {
            List<Collider2D> proxyColliders = attachmentHandler.GetAllProxyColliders();

            foreach (Collider2D proxy in proxyColliders)
            {
                if (proxy == null) continue;

                if (proxy is BoxCollider2D)
                {
                    BoxCollider2D boxProxy = proxy as BoxCollider2D;
                    Vector2 proxyCenter = (Vector2)proxy.transform.position + boxProxy.offset;

                    RaycastHit2D hit = Physics2D.BoxCast(
                        proxyCenter,
                        boxProxy.size,
                        proxy.transform.rotation.eulerAngles.z, // Include rotation for accurate collision
                        new Vector2(direction, 0),
                        moveDistance,
                        obstacleLayer);

                    if (hit.collider != null)
                    {
                        // Only block if we're not trying to move onto a platform
                        Vector2 hitNormal = hit.normal;
                        float angle = Vector2.Angle(hitNormal, Vector2.up);
                        if (angle > 30f) // Not a flat surface we can roll on
                        {
                            // Debug visualization
                            Debug.DrawLine(proxyCenter, hit.point, Color.red, 0.1f);
                            return false;
                        }
                    }
                }
                else if (proxy is CircleCollider2D)
                {
                    CircleCollider2D circleProxy = proxy as CircleCollider2D;
                    Vector2 proxyCenter = (Vector2)proxy.transform.position + circleProxy.offset;

                    RaycastHit2D hit = Physics2D.CircleCast(
                        proxyCenter,
                        circleProxy.radius,
                        new Vector2(direction, 0),
                        moveDistance,
                        obstacleLayer);

                    if (hit.collider != null)
                    {
                        // Only block if we're not trying to move onto a platform
                        Vector2 hitNormal = hit.normal;
                        float angle = Vector2.Angle(hitNormal, Vector2.up);
                        if (angle > 30f) // Not a flat surface we can roll on
                        {
                            // Debug visualization
                            Debug.DrawLine(proxyCenter, hit.point, Color.red, 0.1f);
                            return false;
                        }
                    }
                }
            }
        }

        // If no collisions detected, movement is allowed
        return true;
    }

    // Check for items hitting obstacles from below
    void CheckForAttachmentBottomCollisions()
    {
        if (!allowBottomCollisions || rb.linearVelocity.y >= 0) return; // Only check when falling

        // Get all attachment proxy colliders
        if (attachmentHandler == null) return;

        List<Collider2D> proxyColliders = attachmentHandler.GetAllProxyColliders();
        if (proxyColliders.Count == 0) return;

        foreach (Collider2D proxy in proxyColliders)
        {
            if (proxy == null) continue;

            // Create bounds for the collider
            Bounds proxyBounds = proxy.bounds;

            // Check below the collider
            Vector2 rayOrigin = new Vector2(proxyBounds.center.x, proxyBounds.min.y);
            float rayLength = Mathf.Abs(rb.linearVelocity.y * Time.fixedDeltaTime) + 0.05f; // A bit more than next frame's movement

            // Draw debug ray
            Debug.DrawRay(rayOrigin, Vector2.down * rayLength, Color.yellow, 0.1f);

            // Cast ray down
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, rayLength, obstacleLayer);
            if (hit.collider != null)
            {
                // We hit something below, stop downward velocity
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);

                // Apply a small upward force to prevent getting stuck
                rb.AddForce(Vector2.up * 1f, ForceMode2D.Impulse);

                // Debug visualization
                Debug.DrawLine(rayOrigin, hit.point, Color.red, 0.5f);
                return; // Only need to hit once
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

        isGrounded = (hit.collider != null && hit.collider.gameObject != gameObject && hit.collider.gameObject != bodySprite.gameObject) ||
                     (hitLeft.collider != null && hitLeft.collider.gameObject != gameObject && hitLeft.collider.gameObject != bodySprite.gameObject) ||
                     (hitRight.collider != null && hitRight.collider.gameObject != gameObject && hitRight.collider.gameObject != bodySprite.gameObject);
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

    // Improved ceiling check with multiple raycasts that also checks for side obstacles and attached items
    bool CanStandUp()
    {
        if (ceilingCheck == null)
        {
            Debug.LogWarning("No ceiling check transform assigned!");
            return true; // Default to allowing stand if not set up properly
        }

        // Get the current and uncrouch collider sizes
        Vector2 crouchedSize = bodyCollider.size;
        Vector2 fullSize = originalBodyColliderSize;

        // Calculate difference in height and vertical position
        float heightDifference = fullSize.y - crouchedSize.y;
        float centerOffset = (heightDifference / 2);

        // Use a wider check area to catch any potential ceiling obstacles
        float checkWidth = bodyCollider.size.x * 0.8f;
        int numChecks = 5; // Use multiple points to check

        // 1. Check for ceiling obstacles above
        for (int i = 0; i < numChecks; i++)
        {
            // Calculate position across the width of the collider
            float xOffset = -checkWidth / 2 + (i * checkWidth / (numChecks - 1));
            Vector2 checkPos = (Vector2)ceilingCheck.position + new Vector2(xOffset, 0);

            // Visual debug
            Debug.DrawRay(checkPos, Vector2.up * ceilingCheckRadius, Color.yellow, 0.1f);

            // Check for ceiling
            Collider2D hitCeiling = Physics2D.OverlapCircle(checkPos, ceilingCheckRadius, groundLayer);
            if (hitCeiling != null)
            {
                return false; // Can't stand up, something is above
            }
        }

        // 2. Check for obstacles to the sides that would intersect with the uncrouch animation
        // Get the current body center position
        Vector2 bodyCenter = (Vector2)bodySprite.position + bodyCollider.offset;

        // Top half of the body (this is the part that extends when uncrouching)
        Vector2 topCenterPos = bodyCenter + new Vector2(0, centerOffset);

        // Check left side
        RaycastHit2D leftHit = Physics2D.Raycast(
            topCenterPos,
            Vector2.left,
            fullSize.x / 2 + sideCheckDistance,
            obstacleLayer);

        if (leftHit.collider != null)
        {
            // Debug visualization
            Debug.DrawLine(topCenterPos, leftHit.point, Color.red, 0.1f);
            return false; // Can't stand up, something is to the left
        }

        // Check right side
        RaycastHit2D rightHit = Physics2D.Raycast(
            topCenterPos,
            Vector2.right,
            fullSize.x / 2 + sideCheckDistance,
            obstacleLayer);

        if (rightHit.collider != null)
        {
            // Debug visualization
            Debug.DrawLine(topCenterPos, rightHit.point, Color.red, 0.1f);
            return false; // Can't stand up, something is to the right
        }

        // 3. Check if any attached items would clip through obstacles when uncrouching
        if (attachmentHandler != null)
        {
            // Calculate how much the robot will move up when uncrouching
            float uncrouchMovement = crouchAmount;

            // Get all attached items
            List<GameObject> allAttachedItems = GetAllAttachedItems();

            foreach (GameObject item in allAttachedItems)
            {
                if (item == null) continue;

                // Get the collider of the item
                Collider2D itemCollider = item.GetComponent<Collider2D>();
                if (itemCollider == null) continue;

                // Calculate the new position of the item after uncrouching
                Vector3 currentPos = item.transform.position;
                Vector3 newPos = currentPos + new Vector3(0, uncrouchMovement, 0);

                // Temporarily disable the collider for the check
                bool wasEnabled = itemCollider.enabled;
                itemCollider.enabled = false;

                // Check if the item would collide with any obstacles at the new position
                Bounds itemBounds = itemCollider.bounds;

                // Adjust bounds to the new position
                Vector3 boundsCenter = itemBounds.center + new Vector3(0, uncrouchMovement, 0);
                Bounds newBounds = new Bounds(boundsCenter, itemBounds.size);

                // Visual debugging
                DrawDebugBounds(newBounds, Color.magenta, 0.1f);

                // Check for overlaps with obstacles
                Collider2D[] overlaps = Physics2D.OverlapBoxAll(
                    newBounds.center,
                    newBounds.size,
                    0,
                    obstacleLayer);

                // Restore the collider state
                itemCollider.enabled = wasEnabled;

                // Filter out self-collisions
                bool wouldCollide = false;
                foreach (Collider2D overlap in overlaps)
                {
                    if (overlap.gameObject != gameObject &&
                        !overlap.transform.IsChildOf(transform) &&
                        !transform.IsChildOf(overlap.transform))
                    {
                        // Debug visualization
                        Debug.DrawLine(boundsCenter, overlap.transform.position, Color.red, 0.1f);
                        wouldCollide = true;
                        break;
                    }
                }

                if (wouldCollide)
                {
                    return false; // Can't stand up, an item would clip through an obstacle
                }
            }
        }

        // 4. Final check: Make sure the full body box wouldn't intersect with any obstacles
        // This is a more comprehensive check to catch edge cases
        Vector2 fullBodyCenter = bodyCenter + new Vector2(0, centerOffset);
        Collider2D[] bodyOverlaps = Physics2D.OverlapBoxAll(fullBodyCenter, fullSize, 0, obstacleLayer);

        foreach (Collider2D overlap in bodyOverlaps)
        {
            // Skip self-collisions
            if (overlap.gameObject != gameObject &&
                !overlap.transform.IsChildOf(transform) &&
                !transform.IsChildOf(overlap.transform))
            {
                // Debug visualization
                Debug.DrawLine(fullBodyCenter, overlap.transform.position, Color.red, 0.1f);
                return false; // Can't stand up, would overlap with an obstacle
            }
        }

        return true; // All checks passed, can stand up
    }

    // Helper method to get all attached items
    List<GameObject> GetAllAttachedItems()
    {
        List<GameObject> allItems = new List<GameObject>();

        if (attachmentHandler == null) return allItems;

        // Use reflection to access private fields of AttachmentHandler
        System.Type type = attachmentHandler.GetType();

        System.Reflection.FieldInfo rightPackagesField = type.GetField("rightPackages",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        System.Reflection.FieldInfo leftPackagesField = type.GetField("leftPackages",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        System.Reflection.FieldInfo topPackagesField = type.GetField("topPackages",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (rightPackagesField != null && leftPackagesField != null && topPackagesField != null)
        {
            List<GameObject> rightPackages = rightPackagesField.GetValue(attachmentHandler) as List<GameObject>;
            List<GameObject> leftPackages = leftPackagesField.GetValue(attachmentHandler) as List<GameObject>;
            List<GameObject> topPackages = topPackagesField.GetValue(attachmentHandler) as List<GameObject>;

            if (rightPackages != null) allItems.AddRange(rightPackages);
            if (leftPackages != null) allItems.AddRange(leftPackages);
            if (topPackages != null) allItems.AddRange(topPackages);
        }

        return allItems;
    }

    // Helper method to draw debug bounds
    void DrawDebugBounds(Bounds bounds, Color color, float duration = 0)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        // Draw the wireframe of the bounds
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), color, duration);
        Debug.DrawLine(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), color, duration);
        Debug.DrawLine(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), color, duration);
        Debug.DrawLine(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), color, duration);
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

    // Add visual debugging
    void OnDrawGizmos()
    {
        if (Application.isPlaying && ceilingCheck != null)
        {
            // Visualize ceiling check area
            float checkWidth = bodyCollider != null ? bodyCollider.size.x * 0.8f : 0.8f;
            int numChecks = 5;

            Gizmos.color = CanStandUp() ? Color.green : Color.red;
            for (int i = 0; i < numChecks; i++)
            {
                float xOffset = -checkWidth / 2 + (i * checkWidth / (numChecks - 1));
                Vector2 checkPos = (Vector2)ceilingCheck.position + new Vector2(xOffset, 0);
                Gizmos.DrawWireSphere(checkPos, ceilingCheckRadius);
            }

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

            // Draw the side checks for uncrouching
            if (bodyCollider != null && isCrouching)
            {
                Vector2 bodyCenter = (Vector2)bodySprite.position + bodyCollider.offset;
                Vector2 crouchedSize = bodyCollider.size;
                Vector2 fullSize = originalBodyColliderSize;
                float heightDifference = fullSize.y - crouchedSize.y;
                float centerOffset = (heightDifference / 2);

                // Top center position
                Vector2 topCenterPos = bodyCenter + new Vector2(0, centerOffset);

                // Left and right side checks
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(topCenterPos, topCenterPos + Vector2.left * (fullSize.x / 2 + sideCheckDistance));
                Gizmos.DrawLine(topCenterPos, topCenterPos + Vector2.right * (fullSize.x / 2 + sideCheckDistance));

                // Full body box for uncrouching
                Vector2 fullBodyCenter = bodyCenter + new Vector2(0, centerOffset);
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange with transparency
                Gizmos.DrawCube(fullBodyCenter, fullSize);

                // Visualize where attached items would be after uncrouching
                Gizmos.color = new Color(1f, 0f, 1f, 0.5f); // Purple with transparency
                List<GameObject> attachedItems = GetAllAttachedItems();
                float uncrouchMovement = crouchAmount;

                foreach (GameObject item in attachedItems)
                {
                    if (item == null) continue;

                    Collider2D col = item.GetComponent<Collider2D>();
                    if (col == null) continue;

                    Bounds itemBounds = col.bounds;
                    Vector3 newCenter = itemBounds.center + new Vector3(0, uncrouchMovement, 0);
                    Gizmos.DrawWireCube(newCenter, itemBounds.size);
                }
            }
        }
    }
}