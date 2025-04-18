using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Scale : MonoBehaviour
{
    private GameObject player;
    private OneSoundEffects oneSounds;
    private WeightManager weightManager;
    
    void Start() {
        player = GameObject.FindWithTag("Player");
        weightManager = player.GetComponent<WeightManager>();
        oneSounds = player.GetComponent<OneSoundEffects>();
    }

    IEnumerator ScaleTimer() {
        yield return new WaitForSeconds(3f);

        // Return total weight
        Debug.Log(weightManager.GetTotalWeight());
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Starts timer on contact with scale
        if (collision.gameObject.name == "Robot") {
            StartCoroutine("ScaleTimer");
            oneSounds.PlayScaleStepAudio();
            oneSounds.PlayScaleAudio();
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        // Ends timer when leaving scale
        if (collision.gameObject.name == "Robot") {
            StopCoroutine("ScaleTimer");
            oneSounds.PlayScaleStepAudio();
            oneSounds.StopAudio1();
        }
    }
}