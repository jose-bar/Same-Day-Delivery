using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoopSoundEffects : MonoBehaviour
{
    public AudioSource src;
    public AudioClip wheelSfx;

    public void PlayMoveAudio() {
        src.clip = wheelSfx;
        if (!src.isPlaying) {
            src.Play();
        }
    }

    public void StopAudio() {
        src.Stop();
    }
}