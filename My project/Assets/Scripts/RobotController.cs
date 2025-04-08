using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 8f;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.5f;

    [Header("Body Settings")]
    public Transform bodySprite; // Reference to the body sprite
    public Vector2 bodyColliderSize = new Vector2(0.8f, 1.2f); // Default body collider size

    [Header("Crouch Settings")]
    public float crouchAmount = 0.8f;
    public float crouchSpeed = 5f;
    public float crouchColliderReduction = 0.6f; // How much to reduce collider height when crouching

    private Rigidbody2D rb;
    private CircleCollider2D wheelCollider;
    private BoxCollider2D bodyCollider;
    private bool isGrounded;
    private float horizontalInput;

    // Body sprite references
    private bool isCrouching = false;
    private Vector3 originalBodyPosition;
    private Vector2 originalBodyColliderSize;
    private Vector2 originalBodyColliderOffset;

    [Header("Item Interaction")]
    public Transform attachmentPoint; // Assign a point to the right of the robot in the inspector
    public float attachRange = 1f;
    public LayerMask itemLayer;
    private GameObject attachedItem = null;
    private bool canToggleAttach = true;

    void Start()
    {
        // Get rigidbody
        rb = GetComponent<Rigidbody2D>();

        // Get circle collider (for the wheel)
        wheelCollider = GetComponent<CircleCollider2D>();
        if (wheelCollider == null)
        {
            Debug.LogError("No CircleCollider2D found on the robot!");
        }

        // Find body sprite if not assigned
        if (bodySprite == null)
        {
            bodySprite = transform.Find("BodySprite");
            if (bodySprite == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.GetComponent<SpriteRenderer>() != null &&
                        child.name != "WheelSprite")
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
            Debug.Log("Found body sprite: " + bodySprite.name);

            // Set up the body collider
            SetupBodyCollider();
        }
        else
        {
            Debug.LogWarning("Body sprite not found!");
        }

        // Lock rotation of the main object to keep body upright
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        Debug.Log("Robot initialized at Y: " + transform.position.y);
    }

    void SetupBodyCollider()
    {
        // Check if body already has a collider
        bodyCollider = bodySprite.GetComponent<BoxCollider2D>();

        if (bodyCollider == null)
        {
            // Add a box collider to the body
            bodyCollider = bodySprite.gameObject.AddComponent<BoxCollider2D>();
            bodyCollider.size = bodyColliderSize;

            // Position the collider in the center of the body
            bodyCollider.offset = Vector2.zero;

            Debug.Log("Added BoxCollider2D to body sprite");
        }

        // Store original values
        originalBodyColliderSize = bodyCollider.size;
        originalBodyColliderOffset = bodyCollider.offset;
    }

    void Update()
    {
        // Get input
        horizontalInput = Input.GetAxisRaw("Horizontal");

        // Jump when space is pressed and grounded
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0); // Reset any downward velocity
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            Debug.Log("Jump!");
            isGrounded = false; // Immediately set to false to prevent double jumps
        }

        // Handle crouching
        HandleCrouch();

        if (Input.GetKeyDown(KeyCode.E) && canToggleAttach)
        {
            if (attachedItem == null)
            {
                TryAttachItem();
            }
            else
            {
                DetachItem();
            }

            StartCoroutine(AttachCooldown()); // Prevents multiple toggles from one press
        }
    }

    void FixedUpdate()
    {
        // Simple movement
        rb.velocity = new Vector2(horizontalInput * moveSpeed, rb.velocity.y);

        // Check if grounded using raycasts
        CheckGrounded();

        // Keep body upright if transform rotation is unlocked
        if (bodySprite != null)
        {
            bodySprite.rotation = Quaternion.identity;
        }
    }

    void CheckGrounded()
    {
        if (wheelCollider == null) return;

        // Get the world position of the bottom of the circle
        Vector2 circleBottom = (Vector2)transform.position - new Vector2(0, wheelCollider.radius);

        // Cast a ray downward to check for ground
        RaycastHit2D hit = Physics2D.Raycast(
            circleBottom,
            Vector2.down,
            groundCheckDistance
        );

        RaycastHit2D hitLeft = Physics2D.Raycast(
            circleBottom - new Vector2(wheelCollider.radius * 0.5f, 0),
            Vector2.down,
            groundCheckDistance
        );

        RaycastHit2D hitRight = Physics2D.Raycast(
            circleBottom + new Vector2(wheelCollider.radius * 0.5f, 0),
            Vector2.down,
            groundCheckDistance
        );

        // Check if any of the rays hit something that isn't the player
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

            // If contact below center of circle, is ground
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
                // Crouch down
                isCrouching = true;

                // Move body sprite down
                Vector3 targetPos = new Vector3(originalBodyPosition.x, originalBodyPosition.y - crouchAmount, originalBodyPosition.z);
                bodySprite.localPosition = Vector3.Lerp(bodySprite.localPosition, targetPos, Time.deltaTime * crouchSpeed);

                // Adjust body collider if it exists
                if (bodyCollider != null)
                {
                    // Reduce height of collider
                    Vector2 crouchSize = originalBodyColliderSize;
                    crouchSize.y *= crouchColliderReduction;
                    bodyCollider.size = crouchSize;

                    // Adjust offset to keep the top of the collider in the same place
                    Vector2 crouchOffset = originalBodyColliderOffset;
                    crouchOffset.y -= (originalBodyColliderSize.y - crouchSize.y) / 2;
                    bodyCollider.offset = crouchOffset;
                }
            }
            else if (isCrouching)
            {
                // Return to normal position
                bodySprite.localPosition = Vector3.Lerp(bodySprite.localPosition, originalBodyPosition, Time.deltaTime * crouchSpeed);

                // Restore collider if it exists
                if (bodyCollider != null)
                {
                    bodyCollider.size = Vector2.Lerp(bodyCollider.size, originalBodyColliderSize, Time.deltaTime * crouchSpeed);
                    bodyCollider.offset = Vector2.Lerp(bodyCollider.offset, originalBodyColliderOffset, Time.deltaTime * crouchSpeed);
                }

                // Check if we're close enough to original position
                if (Vector3.Distance(bodySprite.localPosition, originalBodyPosition) < 0.01f)
                {
                    isCrouching = false;
                    bodySprite.localPosition = originalBodyPosition;

                    if (bodyCollider != null)
                    {
                        bodyCollider.size = originalBodyColliderSize;
                        bodyCollider.offset = originalBodyColliderOffset;
                    }
                }
            }
        }
    }

        void TryAttachItem()
    {
        Collider2D item = Physics2D.OverlapCircle(attachmentPoint.position, attachRange, itemLayer);
        if (item != null && attachedItem == null)
        {
            attachedItem = item.gameObject;
            attachedItem.transform.SetParent(attachmentPoint);
            attachedItem.transform.localPosition = Vector3.zero;

            Rigidbody2D rb = attachedItem.GetComponent<Rigidbody2D>();
            if (rb != null) rb.simulated = false;

            Debug.Log("Item attached!");
        }
    }

    void DetachItem()
    {
        if (attachedItem != null)
        {
            attachedItem.transform.SetParent(null);

            Rigidbody2D rb = attachedItem.GetComponent<Rigidbody2D>();
            if (rb != null) rb.simulated = true;

            attachedItem = null;
            Debug.Log("Item detached!");
        }
    }

    IEnumerator AttachCooldown()
    {
        canToggleAttach = false;
        yield return new WaitForSeconds(0.2f); // Small buffer so holding E doesn't spam
        canToggleAttach = true;
    }

}


