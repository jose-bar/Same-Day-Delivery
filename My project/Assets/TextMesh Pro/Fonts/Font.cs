using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class TMPTextOutline : MonoBehaviour {
    void Start() {
        var tmp = GetComponent<TMP_Text>();
        tmp.fontMaterial.SetColor("_OutlineColor", Color.black);
        tmp.fontMaterial.SetFloat("_OutlineWidth", 0.2f);
    }
}
