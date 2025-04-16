using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple component to store original color and handle highlighting for drop mode
/// </summary>
public class ItemHighlighter : MonoBehaviour
{
    public Color originalColor;
    public float pulseSpeed = 2f;
    public float pulseMin = 0.7f;
    public float pulseMax = 1.0f;

    private SpriteRenderer spriteRenderer;
    private bool isPulsing = false;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    public void StartHighlight(Color highlightColor)
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            originalColor = spriteRenderer.color;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = highlightColor;
        }
    }

    public void StartPulsingHighlight(Color highlightColor)
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            originalColor = spriteRenderer.color;
        }

        isPulsing = true;
        StartCoroutine(PulseColor(highlightColor));
    }

    public void StopHighlight()
    {
        isPulsing = false;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
    }

    IEnumerator PulseColor(Color targetColor)
    {
        while (isPulsing)
        {
            // Calculate pulse factor (0 to 1)
            float pulse = pulseMin + Mathf.PingPong(Time.time * pulseSpeed, pulseMax - pulseMin);

            // Apply pulsing to alpha
            Color currentColor = targetColor;
            currentColor.a = pulse;

            spriteRenderer.color = currentColor;

            yield return null;
        }
    }
}