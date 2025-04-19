using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spring : MonoBehaviour
{
    [Header("Spring Settings")]
    public float baseBouncePower = 15f;
    public float weightInfluence = 0.5f; // 0 = no influence, 1 = full influence
    public float compressionAmount = 0.3f; // How much the spring visually compresses
    public float compressionSpeed = 0.1f; // How quickly the spring compresses
    public float expansionSpeed = 0.2f; // How quickly the spring expands back
    public float cooldownTime = 0.1f; // Delay before spring can be used again

    [Header("Visual Components")]
    public Transform springTop; // The top part of the spring that will move
    public Transform springMiddle; // Optional middle part that will stretch
    public SpriteRenderer springRenderer; // For color feedback
    public Color compressedColor = Color.red;
    public Color defaultColor = Color.white;

    // Audio
    public AudioClip springSound;
    private AudioSource audioSource;

    // Internal state
    private bool isCompressed = false;
    private Vector3 originalTopPosition;
    private Vector3 compressedPosition;
    private Vector3 originalMiddleScale;
    private Color originalColor;
    private BoxCollider2D springTrigger;

    private void Start()
    {
        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && springSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Find components if not assigned
        if (springTop == null)
        {
            springTop = transform.Find("SpringTop");
        }

        if (springMiddle == null)
        {
            springMiddle = transform.Find("SpringMiddle");
        }

        if (springRenderer == null && springTop != null)
        {
            springRenderer = springTop.GetComponent<SpriteRenderer>();
        }

        // Find or create trigger collider
        springTrigger = GetComponent<BoxCollider2D>();
        if (springTrigger == null && springTop != null)
        {
            springTrigger = springTop.GetComponent<BoxCollider2D>();
            if (springTrigger == null)
            {
                springTrigger = springTop.gameObject.AddComponent<BoxCollider2D>();
                springTrigger.isTrigger = true;
                springTrigger.size = new Vector2(0.8f, 0.2f);
                springTrigger.offset = Vector2.zero;
            }
        }

        // Store original positions, scales and colors
        if (springTop != null)
        {
            originalTopPosition = springTop.localPosition;
            compressedPosition = originalTopPosition - new Vector3(0, compressionAmount, 0);
        }

        if (springMiddle != null)
        {
            originalMiddleScale = springMiddle.localScale;
        }

        if (springRenderer != null)
        {
            originalColor = springRenderer.color;
        }
        else
        {
            originalColor = defaultColor;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Skip if already compressed
        if (isCompressed) return;

        // Check if the robot or an attached package hit the spring from above
        RobotController robot = other.GetComponentInParent<RobotController>();
        if (robot != null)
        {
            // Only trigger if coming from above with downward velocity
            Rigidbody2D robotRb = robot.GetComponent<Rigidbody2D>();
            if (robotRb != null && robotRb.linearVelocity.y < 0)
            {
                // Start spring effect
                StartCoroutine(SpringEffect(robot));
            }
        }
        // Also check for any attached items
        else if (other.CompareTag(AttachmentHandler.ATTACHMENT_COLLIDER_TAG))
        {
            // Find the robot this attachment belongs to
            RobotController robotParent = other.transform.root.GetComponent<RobotController>();
            if (robotParent != null)
            {
                Rigidbody2D robotRb = robotParent.GetComponent<Rigidbody2D>();
                if (robotRb != null && robotRb.linearVelocity.y < 0)
                {
                    // Start spring effect
                    StartCoroutine(SpringEffect(robotParent));
                }
            }
        }
    }

    private IEnumerator SpringEffect(RobotController robot)
    {
        isCompressed = true;

        // Compress spring visually
        if (springTop != null)
        {
            // Animate compression
            float time = 0;

            while (time < compressionSpeed)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / compressionSpeed);

                // Move top down
                springTop.localPosition = Vector3.Lerp(originalTopPosition, compressedPosition, t);

                // Scale middle (if exists)
                if (springMiddle != null)
                {
                    Vector3 newScale = originalMiddleScale;
                    newScale.y = originalMiddleScale.y * (1f - t * (compressionAmount / originalTopPosition.y));
                    springMiddle.localScale = newScale;
                }

                // Change color during compression
                if (springRenderer != null)
                {
                    springRenderer.color = Color.Lerp(originalColor, compressedColor, t);
                }

                yield return null;
            }

            // Ensure we're fully compressed
            springTop.localPosition = compressedPosition;
            if (springRenderer != null)
            {
                springRenderer.color = compressedColor;
            }
        }

        // Get weight influence
        float weightModifier = 1f;
        WeightManager weightManager = robot.GetComponent<WeightManager>();
        if (weightManager != null)
        {
            float totalWeight = weightManager.GetTotalWeight();
            float baseWeight = weightManager.baseRobotWeight;

            // Calculate weight factor (heavier = less bounce)
            float extraWeight = totalWeight - baseWeight;

            // Apply weight influence - clamp to ensure it doesn't go too low
            weightModifier = Mathf.Max(0.4f, 1f - (extraWeight / 15f) * weightInfluence);

            if (springRenderer != null)
            {
                // Visual feedback - more red for heavier weights
                springRenderer.color = Color.Lerp(compressedColor, Color.yellow, weightModifier);
            }
        }

        // Calculate final bounce force
        float finalBounceForce = baseBouncePower * weightModifier;

        // Apply bounce force to robot
        Rigidbody2D robotRb = robot.GetComponent<Rigidbody2D>();
        if (robotRb != null)
        {
            // Zero out any downward velocity first
            robotRb.linearVelocity = new Vector2(robotRb.linearVelocity.x, 0);

            // Apply the bounce force
            robotRb.AddForce(Vector2.up * finalBounceForce, ForceMode2D.Impulse);

            // Apply a small horizontal boost in the direction of any weight imbalance
            if (weightManager != null)
            {
                float imbalance = weightManager.GetWeightImbalance();
                if (Mathf.Abs(imbalance) > 0.2f)
                {
                    // Apply a side force based on weight imbalance
                    float sideBounce = imbalance * 3f;
                    robotRb.AddForce(Vector2.right * sideBounce, ForceMode2D.Impulse);
                }
            }
        }

        // Play sound
        if (audioSource != null && springSound != null)
        {
            audioSource.PlayOneShot(springSound);
        }

        // Small delay at full compression
        yield return new WaitForSeconds(0.05f);

        // Spring back with visual effect
        if (springTop != null)
        {
            // Animate expansion
            float time = 0;
            while (time < expansionSpeed)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / expansionSpeed);

                // Move top back up
                springTop.localPosition = Vector3.Lerp(compressedPosition, originalTopPosition, t);

                // Scale middle (if exists)
                if (springMiddle != null)
                {
                    Vector3 newScale = originalMiddleScale;
                    newScale.y = originalMiddleScale.y * (1f - (1f - t) * (compressionAmount / originalTopPosition.y));
                    springMiddle.localScale = newScale;
                }

                // Return color to normal
                if (springRenderer != null)
                {
                    springRenderer.color = Color.Lerp(compressedColor, originalColor, t);
                }

                yield return null;
            }

            // Ensure we're fully expanded
            springTop.localPosition = originalTopPosition;
            if (springMiddle != null)
            {
                springMiddle.localScale = originalMiddleScale;
            }
            if (springRenderer != null)
            {
                springRenderer.color = originalColor;
            }
        }

        // Cooldown period before allowing another bounce
        yield return new WaitForSeconds(cooldownTime);
        isCompressed = false;
    }
}