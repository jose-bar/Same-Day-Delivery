using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimplifiedGroundController : MonoBehaviour
{
    void Start()
    {
        // Make sure we have a collider
        Collider2D groundCollider = GetComponent<Collider2D>();
        if (groundCollider == null)
        {
            gameObject.AddComponent<BoxCollider2D>();
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Static;
        }
    }
}