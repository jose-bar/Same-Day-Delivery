using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spring object that bounces the player with force based on their weight
/// Works with existing art assets organized in a specific hierarchy
/// </summary>
public class Spring : MonoBehaviour
{
    [Header("Spring Properties")]
    [Tooltip("Base force applied when bouncing the player")]
    public float baseSpringForce = 15f;

    [Tooltip("How much the spring compresses visually")]
    public float maxCompression = 0.5f;

    [Tooltip("How quickly the spring compresses")]
    public float compressionSpeed = 10f;

    [Tooltip("How quickly the spring extends after compression")]
    public float extensionSpeed = 20f;

    [Tooltip("Minimum force multiplier for very heavy loads")]
    [Range(0.1f, 1f)]
    public float minForceMultiplier = 0.5f;

    [Tooltip("Maximum force multiplier for very light loads")]
    [Range(1f, 2f)]
    public float maxForceMultiplier = 1.5f;

    [Tooltip("Cooldown time between bounces")]
    public float cooldownTime = 0.5f;

    [Header("Art References")]
    [Tooltip("The top platform of the spring")]
    public Transform topPlatform;

    [Tooltip("The middle coil part of the spring")]
    public Transform springCoil;

    [Tooltip("The bottom base of the spring")]
    public Transform springBase;

    [Header("Audio")]
    public AudioClip springSound;
    public float minPitch = 0.8f;
    public float maxPitch = 1.2f;

    // Private variables
    private Vector3 originalTopPosition;
    private Vector3 originalCoilPosition;
    private Vector3 originalCoilScale;
    private bool isCompressed = false;
    private bool isCoolingDown = false;
    private float targetCompression = 0f;
    private AudioSource audioSource;

    // Cached player reference
    private RobotController playerRobot;
    private WeightManager playerWeightManager;

    void Start()
    {
        // Validate components
        if (topPlatform == null || springCoil == null)
        {
            Debug.LogError("Spring missing required components! Please assign topPlatform and springCoil in inspector.");
            enabled = false;
            return;
        }

        // Store original positions and scales
        originalTopPosition = topPlatform.localPosition;
        originalCoilPosition = springCoil.localPosition;
        originalCoilScale = springCoil.localScale;

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && springSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.volume = 0.7f;
        }

        // Add collider if none exists
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider2D>();

            // Calculate collider size based on top platform
            if (topPlatform != null)
            {
                SpriteRenderer platformRenderer = topPlatform.GetComponent<SpriteRenderer>();
                if (platformRenderer != null)
                {
                    collider.size = platformRenderer.bounds.size;

                    // Position collider at top platform
                    collider.offset = topPlatform.localPosition;
                }
                else
                {
                    // Default size if no renderer found
                    collider.size = new Vector2(1f, 0.2f);
                    collider.offset = topPlatform.localPosition;
                }
            }
        }
    }

    void Update()
    {
        // Handle spring animation
        if (isCompressed)
        {
            AnimateSpring(targetCompression, compressionSpeed);
        }
        else if (Vector3.Distance(topPlatform.localPosition, originalTopPosition) > 0.01f)
        {
            // Spring is extending back
            AnimateSpring(0f, extensionSpeed);
        }
    }

    private void AnimateSpring(float compressionAmount, float speed)
    {
        // Calculate how much the spring should be compressed (0 = not compressed, 1 = fully compressed)
        float topDistance = Vector3.Distance(topPlatform.localPosition, originalTopPosition);
        float maxDistance = originalTopPosition.y - originalCoilPosition.y; // Maximum possible compression distance
        float currentCompression = topDistance / (maxDistance * maxCompression);

        float targetCompression = Mathf.Clamp01(compressionAmount);

        // Smoothly adjust current compression towards target
        float newCompression = Mathf.Lerp(currentCompression, targetCompression, Time.deltaTime * speed);

        // Apply compression to the top platform position
        if (topPlatform != null)
        {
            // Calculate compressed position
            Vector3 compressedPosition = originalTopPosition;
            compressedPosition.y -= maxCompression * maxDistance * newCompression;

            // Apply new position
            topPlatform.localPosition = compressedPosition;
        }

        // Scale the spring coil to look compressed
        if (springCoil != null)
        {
            // Scale the coil on Y axis
            Vector3 newScale = originalCoilScale;
            newScale.y = originalCoilScale.y * (1f - (maxCompression * newCompression));
            springCoil.localScale = newScale;

            // Adjust position to keep the coil connected to the base
            Vector3 newCoilPosition = originalCoilPosition;
            newCoilPosition.y = originalCoilPosition.y - ((originalCoilScale.y - newScale.y) / 2f);
            springCoil.localPosition = newCoilPosition;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if it's the player
        RobotController robot = collision.gameObject.GetComponent<RobotController>();
        if (robot == null)
        {
            // Try to find the robot controller in the parent
            robot = collision.gameObject.GetComponentInParent<RobotController>();
        }

        if (robot != null && !isCoolingDown)
        {
            // Make sure the collision is from above
            bool hitFromAbove = false;
            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector2 normal = collision.GetContact(i).normal;
                if (Vector2.Dot(normal, Vector2.down) > 0.5f)
                {
                    hitFromAbove = true;
                    break;
                }
            }

            if (hitFromAbove)
            {
                // Store player references
                playerRobot = robot;
                playerWeightManager = robot.GetComponent<WeightManager>();

                // Start spring compression
                StartCoroutine(CompressAndBounce());
            }
        }
    }

    private IEnumerator CompressAndBounce()
    {
        isCoolingDown = true;
        isCompressed = true;

        // Calculate compression based on weight
        float weightFactor = 0.5f; // Default weight factor if no weight manager

        if (playerWeightManager != null)
        {
            float totalWeight = playerWeightManager.GetTotalWeight();
            float baseWeight = playerWeightManager.baseRobotWeight;

            // Calculate how much heavier than base the player is
            float relativeWeight = totalWeight / baseWeight;

            // More weight = more compression (between 0.2 and 1.0)
            weightFactor = Mathf.Clamp(relativeWeight - 0.5f, 0.2f, 1.0f);
        }

        // Set target compression (more weight = more compression)
        targetCompression = weightFactor;

        // Wait for compression to happen visually
        float compressionDuration = 0.1f + weightFactor * 0.15f; // Heavier = slight delay
        yield return new WaitForSeconds(compressionDuration);

        // Calculate bounce force (inverse relationship with weight)
        float forceMultiplier = Mathf.Lerp(maxForceMultiplier, minForceMultiplier, weightFactor);
        float bounceForce = baseSpringForce * forceMultiplier;

        // Apply the bounce force
        if (playerRobot != null && playerRobot.GetComponent<Rigidbody2D>() != null)
        {
            Rigidbody2D playerRb = playerRobot.GetComponent<Rigidbody2D>();
            playerRb.linearVelocity = new Vector2(playerRb.linearVelocity.x, 0); // Zero out vertical velocity
            playerRb.AddForce(Vector2.up * bounceForce, ForceMode2D.Impulse);

            // Play sound with pitch variation based on force
            if (audioSource != null && springSound != null)
            {
                // Heavier = lower pitch
                audioSource.pitch = Mathf.Lerp(maxPitch, minPitch, weightFactor);
                audioSource.clip = springSound;
                audioSource.Play();
            }
        }

        // End compression
        isCompressed = false;

        // Wait for cooldown
        yield return new WaitForSeconds(cooldownTime);

        isCoolingDown = false;
    }
}