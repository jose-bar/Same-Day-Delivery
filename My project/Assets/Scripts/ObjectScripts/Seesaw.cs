using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HingeJoint2D))]
public class SeesawController : MonoBehaviour
{
    [Header("Zones (set these in Inspector)")]
    public Collider2D leftZone;   // trigger covering left side
    public Collider2D rightZone;  // trigger covering right side

    [Header("Tipping Settings")]
    public float threshold   = 5f;
    public float motorSpeed  = 100f;
    public float motorTorque = 1000f;

    private HingeJoint2D hinge;
    private readonly List<Rigidbody2D> leftBodies  = new List<Rigidbody2D>();
    private readonly List<Rigidbody2D> rightBodies = new List<Rigidbody2D>();

    void Awake()
    {
        hinge = GetComponent<HingeJoint2D>();
        hinge.useMotor = true;
        var m = hinge.motor;
        m.maxMotorTorque = motorTorque;
        hinge.motor = m;
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        var rb = col.attachedRigidbody;
        if (rb == null) return;
        if (col == leftZone)  leftBodies.Add(rb);
        if (col == rightZone) rightBodies.Add(rb);
    }

    void OnTriggerExit2D(Collider2D col)
    {
        var rb = col.attachedRigidbody;
        if (rb == null) return;
        leftBodies.Remove(rb);
        rightBodies.Remove(rb);
    }

    void FixedUpdate()
    {
        
        float leftSum  = 0f;
        float rightSum = 0f;

        // SUM LEFT
        foreach (var rb in leftBodies)
        {
            // 1) Package component?
            if (rb.TryGetComponent<Package>(out var pkg))
            {
                leftSum += pkg.weight;
            }
            // 2) WeightManager (player) component?
            else if (rb.TryGetComponent<WeightManager>(out var wm))
            {
                leftSum += wm.GetTotalWeight();
            }
        }

        // SUM RIGHT
        foreach (var rb in rightBodies)
        {
            if (rb.TryGetComponent<Package>(out var pkg))
            {
                rightSum += pkg.weight;
            }
            else if (rb.TryGetComponent<WeightManager>(out var wm))
            {
                rightSum += wm.GetTotalWeight();
            }
        }

        float diff = leftSum - rightSum;

    // If the difference is too small, turn the motor OFF so the hinge is free to swing
    if (Mathf.Abs(diff) < threshold)
    {
        hinge.useMotor = false;
    }
    else
    {
        // Turn the motor back on to tilt toward the heavier side
        hinge.useMotor = true;

        var m = hinge.motor;
        m.motorSpeed = (diff > 0) ?  motorSpeed  // tilt left
                                 : -motorSpeed; // tilt right
        hinge.motor = m;
    }
    }
}
