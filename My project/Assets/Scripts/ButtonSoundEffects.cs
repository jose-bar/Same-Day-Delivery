using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(AudioSource))]
public class ButtonSound : MonoBehaviour,
                           IPointerEnterHandler,
                           IPointerClickHandler
{

    //Hover Sound Effect: Menu Interface Selection Download from https://tunetank.com
    public AudioClip hoverClip;
    public AudioClip clickClip;

    AudioSource src;

    void Awake()
    {

        if (hoverClip == null)
            hoverClip = Resources.Load<AudioClip>("hover");
        if (clickClip == null)
            clickClip = Resources.Load<AudioClip>("click");

    
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
    }

    // Called when the mouse (or finger) goes over the button
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverClip != null)
            src.PlayOneShot(hoverClip);
    }

    // Called when the button is clicked
    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickClip != null)
            src.PlayOneShot(clickClip);
    }
}
