using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spring object that bounces the player and packages with force based on weight
/// Works with existing art assets organized in a specific hierarchy
/// </summary>
public class Spring : MonoBehaviour
{
    [Header("Spring Properties")]
    [Tooltip("Base force applied when bouncing objects")]
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

    [Header("Package-Specific Settings")]
    [Tooltip("Force multiplier for packages compared to the robot")]
    [Range(0.5f, 2f)]
    public float packageForceMultiplier = 1.2f;

    [Tooltip("Maximum number of packages that can bounce at once")]
    public int maxPackagesBounced = 3;

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

    // Legacy sound
    private AudioSource audioSource;

    // Sound effects
    private ObjectSoundEffects springSounds;

    // Cached player reference
    private RobotController playerRobot;
    private WeightManager playerWeightManager;

    // Tracked objects currently on spring
    private List<Rigidbody2D> objectsOnSpring = new List<Rigidbody2D>();
    private List<float> objectWeights = new List<float>();
    private float totalWeightOnSpring = 0f;

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

        // [legacy] Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && springSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.volume = 0.7f;
        }

        // New audio setup
        springSounds = GetComponent<ObjectSoundEffects>();

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
        // Skip if cooling down
        if (isCoolingDown) return;

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

        if (!hitFromAbove) return;

        // Get the rigidbody from the colliding object
        Rigidbody2D rb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            // Try to find the rigidbody in the parent
            rb = collision.gameObject.GetComponentInParent<Rigidbody2D>();
        }

        if (rb != null)
        {
            // Check if it's the player or a package
            bool isPlayer = false;
            float objectWeight = 1f; // Default weight

            // Check for player
            RobotController robot = rb.GetComponent<RobotController>();
            if (robot != null)
            {
                isPlayer = true;
                playerRobot = robot;
                playerWeightManager = robot.GetComponent<WeightManager>();

                // Get player weight if available
                if (playerWeightManager != null)
                {
                    objectWeight = playerWeightManager.GetTotalWeight();
                }
            }
            else
            {
                // Check if it's a package
                Package package = rb.GetComponent<Package>();
                if (package != null)
                {
                    objectWeight = package.weight;
                }
            }

            // Track this object for bouncing
            if (!objectsOnSpring.Contains(rb))
            {
                objectsOnSpring.Add(rb);
                objectWeights.Add(objectWeight);
                totalWeightOnSpring += objectWeight;

                // If too many objects are on the spring, limit it
                if (objectsOnSpring.Count > maxPackagesBounced)
                {
                    // Remove the oldest object (except the player, if present)
                    int indexToRemove = 0;
                    if (isPlayer && objectsOnSpring.Count > 1)
                    {
                        // Find the first non-player object
                        for (int i = 0; i < objectsOnSpring.Count; i++)
                        {
                            if (objectsOnSpring[i].GetComponent<RobotController>() == null)
                            {
                                indexToRemove = i;
                                break;
                            }
                        }
                    }

                    // Remove the object
                    totalWeightOnSpring -= objectWeights[indexToRemove];
                    objectWeights.RemoveAt(indexToRemove);
                    objectsOnSpring.RemoveAt(indexToRemove);
                }
            }

            // Start spring compression if not already compressed
            if (!isCompressed)
            {
                StartCoroutine(CompressAndBounce());

                // Play sound
            //    / springSounds.PlayAudio();
            }
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        // When an object leaves the spring, remove it from tracking
        Rigidbody2D rb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = collision.gameObject.GetComponentInParent<Rigidbody2D>();
        }

        if (rb != null && objectsOnSpring.Contains(rb))
        {
            int index = objectsOnSpring.IndexOf(rb);
            if (index >= 0 && index < objectWeights.Count)
            {
                totalWeightOnSpring -= objectWeights[index];
                objectWeights.RemoveAt(index);
            }
            objectsOnSpring.Remove(rb);
        }
    }

    private IEnumerator CompressAndBounce()
    {
        isCoolingDown = true;
        isCompressed = true;

        // Calculate compression based on total weight
        float baseWeight = 5f; // Default base weight if no robot
        if (playerWeightManager != null)
        {
            baseWeight = playerWeightManager.baseRobotWeight;
        }

        // Calculate weight factor (0.2 - 1.0)
        float relativeWeight = Mathf.Clamp01(totalWeightOnSpring / (baseWeight * 2f));
        float weightFactor = Mathf.Clamp(relativeWeight, 0.2f, 1.0f);

        // Set target compression (more weight = more compression)
        targetCompression = weightFactor;

        // Wait for compression to happen visually
        float compressionDuration = 0.1f + weightFactor * 0.15f; // Heavier = slight delay
        yield return new WaitForSeconds(compressionDuration);

        // Calculate bounce force (inverse relationship with weight)
        float forceMultiplier = Mathf.Lerp(maxForceMultiplier, minForceMultiplier, weightFactor);

        // Bounce all objects on the spring
        foreach (Rigidbody2D rb in objectsOnSpring)
        {
            if (rb != null)
            {
                // Check if it's the player or a package
                bool isPackage = rb.GetComponent<Package>() != null;

                // Apply force multiplier for packages if needed
                float objectMultiplier = isPackage ? packageForceMultiplier : 1f;
                float finalForce = baseSpringForce * forceMultiplier * objectMultiplier;

                // Reset vertical velocity and apply bounce force
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
                rb.AddForce(Vector2.up * finalForce, ForceMode2D.Impulse);
            }
        }

        // Play sound with pitch variation based on force
        if (audioSource != null && springSound != null)
        {
            // Heavier = lower pitch
            audioSource.pitch = Mathf.Lerp(maxPitch, minPitch, weightFactor);
            audioSource.clip = springSound;
            audioSource.Play();
        }

        // Clear the objects list
        objectsOnSpring.Clear();
        objectWeights.Clear();
        totalWeightOnSpring = 0f;

        // End compression
        isCompressed = false;

        // Wait for cooldown
        yield return new WaitForSeconds(cooldownTime);

        isCoolingDown = false;
    }

    // Helper method to manually trigger the spring
    public void TriggerSpring(float forceMultiplier = 1f)
    {
        if (!isCoolingDown)
        {
            StartCoroutine(ManuallyTriggerSpring(forceMultiplier));
        }
    }

    // Manually trigger the spring with a specified compression and force
    private IEnumerator ManuallyTriggerSpring(float forceMultiplier)
    {
        isCoolingDown = true;
        isCompressed = true;

        // Set compression amount
        targetCompression = Mathf.Clamp01(forceMultiplier);

        // Wait for compression to happen visually
        yield return new WaitForSeconds(0.15f);

        // Bounce all objects on the spring
        foreach (Rigidbody2D rb in objectsOnSpring)
        {
            if (rb != null)
            {
                // Reset vertical velocity and apply bounce force
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
                rb.AddForce(Vector2.up * baseSpringForce * forceMultiplier, ForceMode2D.Impulse);
            }
        }

        // Play sound
        if (audioSource != null && springSound != null)
        {
            audioSource.pitch = Mathf.Lerp(minPitch, maxPitch, forceMultiplier);
            audioSource.clip = springSound;
            audioSource.Play();
        }

        // Clear the objects list
        objectsOnSpring.Clear();
        objectWeights.Clear();
        totalWeightOnSpring = 0f;

        // End compression
        isCompressed = false;

        // Wait for cooldown
        yield return new WaitForSeconds(cooldownTime);

        isCoolingDown = false;
    }
}