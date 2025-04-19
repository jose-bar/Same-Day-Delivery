using System.Collections;
using UnityEngine;

public class Scale : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The child GameObject (or its SpriteRenderer) that shows pass/fail.")]
    public string screenChildName = "ScreenSprite";

    [Header("Threshold")]
    [Tooltip("Weight value above which we show the Pass sprite.")]
    public float passThreshold = 10f;

    [Header("Sprites")]
    public Sprite passSprite;
    public Sprite failSprite;

    private SpriteRenderer screenRenderer;
    private WeightManager   weightManager;
    private OneSoundEffects oneSounds;

    private void Awake()
    {
        // find the screen child and grab its SpriteRenderer
        var screenGO = transform.Find(screenChildName);
        if (screenGO == null)
            Debug.LogError($"[Scale] Could not find child named '{screenChildName}'");
        else
            screenRenderer = screenGO.GetComponent<SpriteRenderer>();

        // grab components from Player
        var player = GameObject.FindWithTag("Player");
        if (player == null)
            Debug.LogError("[Scale] Player not found in scene!");
        else
        {
            weightManager = player.GetComponent<WeightManager>();
            oneSounds     = player.GetComponent<OneSoundEffects>();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            StartCoroutine(ScaleTimer());
            oneSounds.PlayScaleStepAudio();
            oneSounds.PlayScaleAudio();
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            StopCoroutine(ScaleTimer());
            oneSounds.PlayScaleStepAudio();
            oneSounds.StopAudio1();
        }
    }

    private IEnumerator ScaleTimer()
    {
        yield return new WaitForSeconds(3f);

        float total = weightManager.GetTotalWeight();
        Debug.Log($"[Scale] Total weight = {total}");

        if (screenRenderer != null)
        {
            // swap sprite based on threshold
            screenRenderer.sprite = (total >= passThreshold)
                                   ? passSprite
                                   : failSprite;
        }
    }
}
