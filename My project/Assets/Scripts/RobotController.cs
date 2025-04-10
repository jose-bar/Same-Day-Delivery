using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public Transform ceilingCheck; // Place an empty GameObject just above the crouched head
    public float ceilingCheckRadius = 0.1f;
    public LayerMask groundLayer; // Assign this to "Ground" layer in the inspector

    private Vector3 originalBodyPosition;
    private Vector2 originalBodyColliderSize;
    private Vector2 originalBodyColliderOffset;

    [Header("Attachment Points")]
    public Transform rightAttachPoint;
    public Transform leftAttachPoint;
    public Transform topAttachPoint;
    public float attachRange = 1f;
    public LayerMask itemLayer;

    [Header("Attached Packages (Per Side)")]
    private List<GameObject> rightPackages = new List<GameObject>();
    private List<GameObject> leftPackages = new List<GameObject>();
    private List<GameObject> topPackages = new List<GameObject>();

    private bool canToggleAttach = true;

    [Header("Attach Detection Box")]
    public Vector2 sideDetectSize = new Vector2(1.2f, 2.0f);
    public Vector2 topDetectSize = new Vector2(1.0f, 1.0f);

    [Header("Attach Box Offset (from attach point)")]
    public Vector2 sideDetectOffset = new Vector2(0f, -1.0f);
    public Vector2 topDetectOffset = new Vector2(0f, -0.5f);

    void Start()
    {
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

        if (bodyMiddle != null) //store original position for body middle:
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
        horizontalInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
            isGrounded = false;
        }

        HandleCrouch();

        if (canToggleAttach)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                ToggleAttachment(rightPackages, rightAttachPoint);
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                ToggleAttachment(leftPackages, leftAttachPoint);
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                ToggleAttachment(topPackages, topAttachPoint);
            }
        }
    }

    void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(horizontalInput * moveSpeed, rb.linearVelocity.y);
        CheckGrounded();

        if (bodySprite != null)
        {
            bodySprite.rotation = Quaternion.identity;
        }

        //wheel rotation
        if (wheelSprite != null)
        {
            float rotationAmount = -rb.linearVelocity.x * 360f * Time.fixedDeltaTime;
            wheelSprite.Rotate(Vector3.forward, rotationAmount);
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
            // Only uncrouch if nothing is overhead
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
    void ToggleAttachment(List<GameObject> packageList, Transform attachPoint)
    {
        Vector2 size, offset;

        if (attachPoint == topAttachPoint)
        {
            size = topDetectSize;
            offset = topDetectOffset;
        }
        else
        {
            size = sideDetectSize;
            offset = sideDetectOffset;
        }

        Vector2 boxCenter = (Vector2)attachPoint.position + offset;

        Collider2D item = Physics2D.OverlapBox(boxCenter, size, 0f, itemLayer);

        if (item != null && !packageList.Contains(item.gameObject))
        {
            AttachItem(item.gameObject, attachPoint, packageList);
        }
        else if (packageList.Count > 0)
        {
            DetachLastItem(packageList);
        }

        StartCoroutine(AttachCooldown());
    }

    void AttachItem(GameObject item, Transform attachPoint, List<GameObject> packageList)
    {
        item.transform.SetParent(attachPoint);
        item.transform.localPosition = new Vector3(0, packageList.Count * 0.5f, 0);

        Rigidbody2D rb = item.GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = false;

        packageList.Add(item);
        Debug.Log($"Item attached to {attachPoint.name} | Total: {packageList.Count}");
    }

    void DetachLastItem(List<GameObject> packageList)
    {
        GameObject last = packageList.Last();
        packageList.RemoveAt(packageList.Count - 1);

        last.transform.SetParent(null);

        Rigidbody2D rb = last.GetComponent<Rigidbody2D>();
        if (rb != null) rb.simulated = true;

        Debug.Log($"Item detached | Remaining: {packageList.Count}");
    }

    IEnumerator AttachCooldown()
    {
        canToggleAttach = false;
        yield return new WaitForSeconds(0.2f);
        canToggleAttach = true;
    }


    void OnDrawGizmosSelected()
    {
        if (rightAttachPoint != null)
        {
            Gizmos.color = Color.red;
            Vector3 center = rightAttachPoint.position + (Vector3)sideDetectOffset;
            Gizmos.DrawWireCube(center, sideDetectSize);
        }

        if (leftAttachPoint != null)
        {
            Gizmos.color = Color.green;
            Vector3 center = leftAttachPoint.position + (Vector3)sideDetectOffset;
            Gizmos.DrawWireCube(center, sideDetectSize);
        }

        if (topAttachPoint != null)
        {
            Gizmos.color = Color.blue;
            Vector3 center = topAttachPoint.position + (Vector3)topDetectOffset;
            Gizmos.DrawWireCube(center, topDetectSize);
        }
    }

}


