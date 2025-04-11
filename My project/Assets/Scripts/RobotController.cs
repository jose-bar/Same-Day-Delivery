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

    private Rigidbody2D rb;
    private CircleCollider2D wheelCollider;
    private BoxCollider2D bodyCollider;
    private bool isGrounded;
    private float horizontalInput;
    private bool isCrouching = false;

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

    void Start()
    {
        if (attachmentHandler == null)
        {
            attachmentHandler = GetComponentInChildren<AttachmentHandler>();
            if (attachmentHandler == null)
                Debug.LogWarning("AttachmentHandler not assigned and not found in children.");
        }

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

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            isGrounded = false;
        }

        HandleCrouch();

        // Attachment keys (delegate to handler)
        if (Input.GetKeyDown(KeyCode.RightArrow))
            attachmentHandler.ToggleAttachment(AttachmentHandler.AttachmentSide.Right);
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
            attachmentHandler.ToggleAttachment(AttachmentHandler.AttachmentSide.Left);
        else if (Input.GetKeyDown(KeyCode.UpArrow))
            attachmentHandler.ToggleAttachment(AttachmentHandler.AttachmentSide.Top);
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
        }

        CheckGrounded();

        if (bodySprite != null)
        {
            bodySprite.rotation = Quaternion.identity;
        }

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

        // Offset to ignore collisions with ground below
        float verticalOffset = 0.05f;

        // Check body colliders first
        if (bodyCollider != null)
        {
            Vector2 bodyCenter = (Vector2)bodySprite.position + bodyCollider.offset;

            // Ignore collisions slightly below the center to avoid detecting ground
            RaycastHit2D hit = Physics2D.BoxCast(
                bodyCenter,
                new Vector2(bodyCollider.size.x, bodyCollider.size.y - verticalOffset),
                0f,
                new Vector2(direction, 0),
                Mathf.Abs(velocity.x * Time.fixedDeltaTime),
                obstacleLayer);

            // Only count this as a collision if the hit point is not below us
            if (hit.collider != null && hit.point.y >= transform.position.y - wheelCollider.radius)
            {
                return false;
            }
        }

        // Check wheel collider (but only the upper half to avoid ground)
        if (wheelCollider != null)
        {
            // Create a slightly smaller circle cast that doesn't touch the ground
            float adjustedRadius = wheelCollider.radius * 0.8f; // Make it slightly smaller
            Vector2 adjustedCenter = (Vector2)transform.position + new Vector2(0, wheelCollider.radius * 0.2f); // Move up slightly

            RaycastHit2D hit = Physics2D.CircleCast(
                adjustedCenter,
                adjustedRadius,
                new Vector2(direction, 0),
                Mathf.Abs(velocity.x * Time.fixedDeltaTime),
                obstacleLayer);

            // Only count as collision if hit point is not below us
            if (hit.collider != null && hit.point.y >= transform.position.y - wheelCollider.radius)
            {
                return false;
            }
        }

        // Now check all attachment proxy colliders
        if (attachmentHandler != null)
        {
            List<Collider2D> proxyColliders = attachmentHandler.GetAllProxyColliders();

            foreach (Collider2D proxy in proxyColliders)
            {
                if (proxy is BoxCollider2D)
                {
                    BoxCollider2D boxProxy = proxy as BoxCollider2D;
                    Vector2 proxyCenter = (Vector2)proxy.transform.position + boxProxy.offset;

                    RaycastHit2D hit = Physics2D.BoxCast(
                        proxyCenter,
                        boxProxy.size,
                        0f,
                        new Vector2(direction, 0),
                        Mathf.Abs(velocity.x * Time.fixedDeltaTime),
                        obstacleLayer);

                    // Only count as collision if hit point is not below the wheel
                    if (hit.collider != null && hit.point.y >= transform.position.y - wheelCollider.radius)
                    {
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
                        Mathf.Abs(velocity.x * Time.fixedDeltaTime),
                        obstacleLayer);

                    // Only count as collision if hit point is not below the wheel
                    if (hit.collider != null && hit.point.y >= transform.position.y - wheelCollider.radius)
                    {
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

    void HandleCrouch()
    {
        if (bodySprite != null)
        {
            if (Input.GetKey(KeyCode.S))
            {
                isCrouching = true;
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
                bool canStand = !Physics2D.OverlapCircle(ceilingCheck.position, ceilingCheckRadius, groundLayer);

                if (canStand)
                {
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
                        bodySprite.localPosition = originalBodyPosition;
                        if (bodyMiddle != null) bodyMiddle.localPosition = originalBodyMiddlePosition;

                        if (bodyCollider != null)
                        {
                            bodyCollider.size = originalBodyColliderSize;
                            bodyCollider.offset = originalBodyColliderOffset;
                        }
                    }
                }
            }
        }
    }
}