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

    private SpriteRenderer   screenRenderer;
    private WeightManager    weightManager;
    private OneSoundEffects  oneSounds;
    private Coroutine        scaleRoutine;
    private bool             levelPassed = false;

    private void Awake()
    {
        // 1) Grab the little screen SpriteRenderer
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

        // 2) Find the player in the scene and cache its managers
        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[Scale] Player not found in scene! Make sure your Player is tagged \"Player\".");
        }
        else
        {
            weightManager = player.GetComponent<WeightManager>();
            oneSounds     = player.GetComponent<OneSoundEffects>();

            if (weightManager == null)
                Debug.LogError("[Scale] Player has no WeightManager component!");
            if (oneSounds == null)
                Debug.LogError("[Scale] Player has no OneSoundEffects component!");
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        // start our delay
        if (scaleRoutine == null)
            scaleRoutine = StartCoroutine(ScaleTimer());

        // play your sounds (null‑safe)
        oneSounds?.PlayScaleStepAudio();
        oneSounds?.PlayScaleAudio();
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        // stop exactly our coroutine
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
        // 3s weight‐accumulation delay
        yield return new WaitForSeconds(3f);

        // guard null manager
        float total = (weightManager != null)
                    ? weightManager.GetTotalWeight()
                    : 0f;
        Debug.Log($"[Scale] Total weight = {total}");

        // swap the sprite
        if (screenRenderer != null)
        {
            bool pass = total >= passThreshold;
            screenRenderer.sprite = pass ? passSprite : failSprite;
            levelPassed = pass;
        }

        // give the player a moment to see it
        yield return new WaitForSeconds(1.5f);

        // advance if they passed
        if (levelPassed)
            ProgressLevel();

        scaleRoutine = null;
    }

    private void ProgressLevel()
    {
        // default Single mode unloads this scene
        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex + 1,
            LoadSceneMode.Single
        );
    }
}
