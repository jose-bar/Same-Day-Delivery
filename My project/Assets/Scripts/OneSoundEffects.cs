using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OneSoundEffects : MonoBehaviour
{
    public AudioSource src1, src2, src3, src4, src5;
    public AudioClip jumpSfx, bumpSfx, crouchSfx, steamSfx, stepSfx, failSfx,
            scaleSfx, attachSfx, detachSfx, deathSfx;
    // Checks for when audio was playing then paused
    public Boolean paused1, paused2, paused3, paused4, paused5;

    public void PlayJumpAudio() {
        src1.clip = jumpSfx;
        src1.Play();
    }

    public void PlayBumpAudio() {
        src2.clip = bumpSfx;
        src2.Play();
    }

    public void PlayCrouchAudio() {
        src3.clip = crouchSfx;
        src3.Play();
    }

    public void PlayUncrouchAudio() {
        src3.clip = steamSfx;
        if (!src3.isPlaying) {
            src3.Play();
        }
    }

    public void PlayScaleStepAudio() {
        src2.clip = stepSfx;
        src2.Play();
    }

    public void PlayFailAudio() {
        src4.clip = failSfx;
        PlayScaleStepAudio();
        src4.Play();
    }

    public void PlayScaleAudio() {
        src1.clip = scaleSfx;
        src1.Play();
    }

    public void PlayAttachAudio() {
        src5.clip = attachSfx;
        src5.Play();
    }

    public void PlayDetachAudio() {
        src5.clip = detachSfx;
        src5.Play();
    }

    public void PlayDeathAudio() {
        src1.clip = deathSfx;
        src1.Play();
    }

    public void StopAudio1() {
        src1.Stop();
    }

    public void StopAudio2() {
        src2.Stop();
    }

    public void StopAudio3() {
        src3.Stop();
    }

    public void PauseAllAudio() {
        if (src1.isPlaying) {
            paused1 = true;
        }
        if (src2.isPlaying) {
            paused2 = true;
        }
        if (src3.isPlaying) {
            paused3 = true;
        }
        if (src4.isPlaying) {
            paused4 = true;
        }
        if (src5.isPlaying) {
            paused5 = true;
        }
        src1.Pause();
        src2.Pause();
        src3.Pause();
        src4.Pause();
        src5.Pause();
    }

    public void ResumeAllAudio() {
        if (paused1) {
            src1.Play();
        } 
        if (paused2) {
            src2.Play();
        } 
        if (paused3) {
            src3.Play();
        } 
        if (paused4) {
            src4.Play();
        } 
        if (paused5) {
            src5.Play();
        } 

        paused1 = false;
        paused2 = false;
        paused3 = false;
        paused4 = false;
        paused5 = false;
    }
}