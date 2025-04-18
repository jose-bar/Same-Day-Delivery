using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoopSoundEffects : MonoBehaviour
{
    private Boolean pausedAudio = false;
    
    public AudioSource src;
    public AudioClip wheelSfx;

    public void PlayMoveAudio() {
        src.clip = wheelSfx;
        if (!src.isPlaying && !pausedAudio) {
            src.Play();
        }
    }

    public void StopAudio() {
        src.Stop();
    }

    public void PauseAudio() {
        src.Pause();
        pausedAudio = true;
    }

    public void ResumeAudio() {
        pausedAudio = false;
    }
}