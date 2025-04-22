using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    private WeightManager  weightManager;
    private OneSoundEffects oneSounds;
    private Coroutine       scaleRoutine;
    private bool            levelPassed = false;

    private void Awake()
    {
        // grab the screen's SpriteRenderer
        var screenGO = transform.Find(screenChildName);
        if (screenGO == null)
        {
            Debug.LogError($"[Scale] Could not find child named '{screenChildName}'");
        }
        else
        {
            screenRenderer = screenGO.GetComponent<SpriteRenderer>();
            if (screenRenderer == null)
                Debug.LogError($"[Scale] '{screenChildName}' has no SpriteRenderer!");
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        // grab the player's components on first contact
        if (weightManager == null)
            weightManager = collision.gameObject.GetComponent<WeightManager>();
        if (oneSounds == null)
            oneSounds     = collision.gameObject.GetComponent<OneSoundEffects>();

        // start the weigh‑in coroutine
        if (scaleRoutine == null)
            scaleRoutine = StartCoroutine(ScaleTimer());

        oneSounds?.PlayScaleStepAudio();
        oneSounds?.PlayScaleAudio();
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        // cancel the weigh‑in if they step off early
        if (scaleRoutine != null)
        {
            StopCoroutine(scaleRoutine);
            scaleRoutine = null;
        }

        oneSounds?.PlayScaleStepAudio();
        oneSounds?.StopAudio1();
    }

    private IEnumerator ScaleTimer()
    {
        // wait 3 seconds while the player stands on the scale
        yield return new WaitForSeconds(3f);

        float total = weightManager != null
                    ? weightManager.GetTotalWeight()
                    : 0f;
        Debug.Log($"[Scale] Total weight = {total}");

        // swap to pass/fail sprite
        if (screenRenderer != null)
        {
            bool pass = total >= passThreshold;
            screenRenderer.sprite = pass ? passSprite : failSprite;
            levelPassed = pass;
        }

        // show the result for 1.5 seconds
        yield return new WaitForSeconds(0.5f);

        // if they passed, go back to Level‑Select
        if (levelPassed)
            ProgressLevel();

        scaleRoutine = null;
    }

    private void ProgressLevel()
    {
        // Load your Level-Select scene by name
        SceneManager.LoadScene("SelectLevelMenu", LoadSceneMode.Single);
    }
}
