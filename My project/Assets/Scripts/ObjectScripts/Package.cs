using UnityEngine;

public class Package : MonoBehaviour
{
    [Header("Package Settings")]
    [Tooltip("The weight of the package affects robot movement")]
    [Range(0.1f, 10f)]
    public float weight = 1f;
    public bool canBeAttached = true;

    [Header("Visual Indicators")]
    public bool showWeightText = true;
    public Color lightWeightColor = new Color(0.5f, 1f, 0.5f, 1f); // Light green
    public Color mediumWeightColor = new Color(1f, 1f, 0.5f, 1f);  // Yellow
    public Color heavyWeightColor = new Color(1f, 0.5f, 0.5f, 1f);  // Light red

    private SpriteRenderer spriteRenderer;
    private Collider2D packageCollider;
    private Rigidbody2D rb;
    private TextMesh weightText;
    private SpriteRenderer originalRenderer;
    private Color originalColor;

    void Awake()
    {
        // Make sure we have all the necessary components
        EnsureComponents();

        // Store original renderer color
        if (spriteRenderer != null)
        {
            originalRenderer = spriteRenderer;
            originalColor = spriteRenderer.color;
        }
    }

    void Start()
    {
        // Create weight visualization
        if (showWeightText)
        {
            CreateWeightDisplay();
        }

        // Tint the sprite based on weight
        UpdateVisualBasedOnWeight();
    }

    void EnsureComponents()
    {
        // Get sprite renderer
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Ensure we have a collider
        packageCollider = GetComponent<Collider2D>();
        if (packageCollider == null)
        {
            // Add a box collider matching the sprite if no collider exists
            if (spriteRenderer != null)
            {
                BoxCollider2D boxCollider = gameObject.AddComponent<BoxCollider2D>();
                boxCollider.size = spriteRenderer.bounds.size;
                packageCollider = boxCollider;
            }
            else
            {
                // Fallback if no sprite renderer
                packageCollider = gameObject.AddComponent<BoxCollider2D>();
            }
        }

        // Ensure we have a rigidbody for physics
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    void CreateWeightDisplay()
    {
        // Create a child object for the weight text
        GameObject textObj = new GameObject("WeightDisplay");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = Vector3.zero;

        // Add TextMesh component
        weightText = textObj.AddComponent<TextMesh>();
        weightText.text = weight.ToString("F1");
        weightText.fontSize = 24;
        weightText.characterSize = 0.05f;
        weightText.anchor = TextAnchor.MiddleCenter;
        weightText.alignment = TextAlignment.Center;
        weightText.color = Color.black;

        // Position the text in the center of the sprite
        if (spriteRenderer != null)
        {
            textObj.transform.localPosition = new Vector3(0, 0, -0.1f);
        }
    }

    void UpdateVisualBasedOnWeight()
    {
        if (spriteRenderer == null) return;

        // Update text if it exists
        if (weightText != null)
        {
            weightText.text = weight.ToString("F1");
        }

        // Determine color based on weight
        Color weightColor;
        if (weight < 2f)
        {
            weightColor = lightWeightColor;
        }
        else if (weight < 5f)
        {
            weightColor = mediumWeightColor;
        }
        else
        {
            weightColor = heavyWeightColor;
        }

        // Tint the sprite based on weight
        spriteRenderer.color = weightColor;
    }

    // This can be called by the AttachmentHandler when this package is attached
    public void OnAttached(Transform newParent)
    {
        if (rb != null)
        {
            rb.simulated = false;
        }

        // Any additional behaviors when attached
    }

    // This can be called by the AttachmentHandler when this package is detached
    public void OnDetached()
    {
        if (rb != null)
        {
            rb.simulated = true;
            // Add a small force to "push" the package away slightly
            rb.AddForce(new Vector2(Random.Range(-1f, 1f), 0.5f), ForceMode2D.Impulse);
        }

        // Any additional behaviors when detached

        // Restore original color if we still have reference
        if (spriteRenderer != null && originalRenderer == spriteRenderer)
        {
            spriteRenderer.color = originalColor;
        }
    }

    // Allow updating the weight dynamically
    public void SetWeight(float newWeight)
    {
        weight = Mathf.Clamp(newWeight, 0.1f, 10f);
        UpdateVisualBasedOnWeight();
    }

    // For editor usage - update visuals when values change
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            UpdateVisualBasedOnWeight();
        }
    }
}