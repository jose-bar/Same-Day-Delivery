using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 8f;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.6f;

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

    private Vector3 originalBodyPosition;
    private Vector2 originalBodyColliderSize;
    private Vector2 originalBodyColliderOffset;

    [Header("Attachment")]
    public AttachmentHandler attachmentHandler;

    [Header("Movement Validation")]
    public LayerMask obstacleLayer; // Same layer mask as in AttachmentHandler
    public float collisionBuffer = 0.05f; // Buffer to prevent getting stuck on edges

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
        if (Mathf.Abs(horizontalInput) > 0.1f && isGrounded)
        {
            if (loopSounds != null) loopSounds.PlayMoveAudio();
        }
        else
        {
            if (loopSounds != null) loopSounds.StopAudio();
        }

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
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
        currentSwayAngle = Mathf.SmoothDamp(currentSwayAngle, targetSway, ref swayVelocity, smoothTime, swaySpeed);

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
            Vector3 currentRot = bodySprite.localRotation.eulerAngles;
            bodySprite.localRotation = Quaternion.Euler(currentRot.x, currentRot.y, currentSwayAngle * crouchSwayFactor);

            if (wheelSprite != null)
            {
                Quaternion wheelRot = wheelSprite.localRotation;
                transform.localRotation = Quaternion.Euler(0, 0, currentSwayAngle * crouchSwayFactor);
                wheelSprite.localRotation = wheelRot;
            }
            else
            {
                transform.localRotation = Quaternion.Euler(0, 0, currentSwayAngle * crouchSwayFactor);
            }
        }
    }

    void FixedUpdate()
    {
        // Validate movement before applying it
        Vector2 intendedVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);

        // Only apply horizontal movement if it doesn't cause collisions with attachments
        if (CanMove(intendedVelocity))
        {
            rb.linearVelocity = intendedVelocity;
        }
        else
        {
            // Only allow vertical movement if horizontal would cause a collision
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

            // Play bump sound if we tried to move but couldn't
            if (Mathf.Abs(horizontalInput) > 0.1f && oneSounds != null)
            {
                oneSounds.PlayBumpAudio();
            }
        }

        CheckGrounded();

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
            RaycastHit2D wheelHit = Physics2D.CircleCast(
                castOrigin,
                wheelCollider.radius * 0.95f, // Slightly smaller than actual to avoid edge cases
                new Vector2(direction, 0),
                moveDistance,
                obstacleLayer);

            if (wheelHit.collider != null)
            {
                // Debug visualization
                Debug.DrawLine(castOrigin, wheelHit.point, Color.red, 0.1f);
                return false;
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
                // Debug visualization
                Debug.DrawLine(bodyCenter, bodyHit.point, Color.red, 0.1f);
                return false;
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
                        // Debug visualization
                        Debug.DrawLine(proxyCenter, hit.point, Color.red, 0.1f);
                        return false;
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
                        // Debug visualization
                        Debug.DrawLine(proxyCenter, hit.point, Color.red, 0.1f);
                        return false;
                    }
                }
            }
        }

        // If no collisions detected, movement is allowed
        return true;
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

    // Improved ceiling check with multiple raycasts
    bool CanStandUp()
    {
        if (ceilingCheck == null)
        {
            Debug.LogWarning("No ceiling check transform assigned!");
            return true; // Default to allowing stand if not set up properly
        }

        // Use a wider check area to catch any potential ceiling obstacles
        float checkWidth = bodyCollider.size.x * 0.8f;
        int numChecks = 5; // Use multiple points to check

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
        }
    }
}