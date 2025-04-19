using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectSoundEffects : MonoBehaviour
{
    private Boolean pausedAudio = false;
    
    public AudioSource src;
    public AudioClip sfx;

    public void PlayAudio() {
        src.clip = sfx;
        if (!src.isPlaying && !pausedAudio) {
            src.Play();
        }
    }

    public void PauseAudio() {
        src.Pause();
        pausedAudio = true;
    }

    public void ResumeAudio() {
        pausedAudio = false;
    }
}