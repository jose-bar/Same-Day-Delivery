using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OneSoundEffects : MonoBehaviour
{
    public AudioSource src1, src2, src3, src4, src5;
    public AudioClip jumpSfx, bumpSfx, crouchSfx, steamSfx, stepSfx, failSfx,
            scaleSfx, attachSfx, detachSfx;

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

    public void StopAudio1() {
        src1.Stop();
    }

    public void StopAudio2() {
        src2.Stop();
    }

    public void StopAudio3() {
        src3.Stop();
    }
}