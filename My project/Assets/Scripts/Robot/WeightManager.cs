using UnityEngine;
using System.Collections.Generic;

// This class can be added to RobotController.cs
public class WeightManager : MonoBehaviour
{
    [Header("Weight Settings")]
    public float baseRobotWeight = 5f;
    public float maxWeightImbalance = 10f;  // Value used to normalize imbalance effects
    public float weightSwayFactor = 0.8f;    // How much weight affects passive sway
    public float weightSpeedFactor = 0.5f;   // How much weight affects max speed
    public float weightAccelFactor = 0.7f;   // How much weight affects acceleration

    [Header("Visual Feedback")]
    public bool showWeightDebug = true;
    public Color balancedColor = Color.green;
    public Color imbalancedColor = Color.red;

    // Reference to attachment handler
    private AttachmentHandler attachmentHandler;

    // Current weight state
    private float leftSideWeight = 0f;
    private float rightSideWeight = 0f;
    private float topSideWeight = 0f;
    private float totalWeight = 0f;
    private float weightImbalance = 0f;  // Positive = right heavy, Negative = left heavy

    // Cached references
    private RobotController robotController;

    private void Awake()
    {
        robotController = GetComponent<RobotController>();
        attachmentHandler = GetComponent<AttachmentHandler>();

        if (attachmentHandler == null)
        {
            attachmentHandler = GetComponentInChildren<AttachmentHandler>();
        }
    }

    private void Start()
    {
        if (attachmentHandler == null)
        {
            Debug.LogError("WeightManager requires an AttachmentHandler component!");
            enabled = false;
        }

        totalWeight = baseRobotWeight;
    }

    public void UpdateWeightDistribution()
    {
        if (attachmentHandler == null) return;

        // Reset weights
        leftSideWeight = 0f;
        rightSideWeight = 0f;
        topSideWeight = 0f;

        // Get all attached items
        List<GameObject> leftPackages = attachmentHandler.GetPackageList(AttachmentHandler.AttachmentSide.Left);
        List<GameObject> rightPackages = attachmentHandler.GetPackageList(AttachmentHandler.AttachmentSide.Right);
        List<GameObject> topPackages = attachmentHandler.GetPackageList(AttachmentHandler.AttachmentSide.Top);

        // Calculate weight for each side
        foreach (GameObject item in leftPackages)
        {
            if (item == null) continue;

            Package package = item.GetComponent<Package>();
            float weight = package != null ? package.weight : 1f;
            leftSideWeight += weight;
        }

        foreach (GameObject item in rightPackages)
        {
            if (item == null) continue;

            Package package = item.GetComponent<Package>();
            float weight = package != null ? package.weight : 1f;
            rightSideWeight += weight;
        }

        foreach (GameObject item in topPackages)
        {
            if (item == null) continue;

            Package package = item.GetComponent<Package>();
            float weight = package != null ? package.weight : 1f;
            topSideWeight += weight;
        }

        // Calculate total weight
        totalWeight = baseRobotWeight + leftSideWeight + rightSideWeight + topSideWeight;

        // Calculate imbalance (positive = right heavy, negative = left heavy)
        weightImbalance = rightSideWeight - leftSideWeight;

        // Normalize imbalance to a -1 to 1 range based on maxWeightImbalance
        weightImbalance = Mathf.Clamp(weightImbalance / maxWeightImbalance, -1f, 1f);
    }

    // Get passive sway angle based on weight distribution
    public float GetWeightSwayAngle()
    {
        // Return a sway angle based on weight imbalance
        // Positive imbalance (right heavy) = negative angle (tilt right)
        // Negative imbalance (left heavy) = positive angle (tilt left)
        return -weightImbalance * weightSwayFactor * 15f; // Scale to reasonable angle
    }

    // Get speed multiplier for a given direction
    public float GetSpeedMultiplier(float direction)
    {
        // If moving towards heavier side, increase speed
        // If moving away from heavier side, decrease speed
        float directionSign = Mathf.Sign(direction);
        float imbalanceSign = Mathf.Sign(weightImbalance);

        // When signs match, we're moving towards the heavier side
        bool movingTowardsHeavySide = (directionSign == imbalanceSign && weightImbalance != 0);

        // Calculate speed multiplier
        if (Mathf.Abs(weightImbalance) < 0.1f)
        {
            // Balanced, normal speed
            return 1f;
        }
        else if (movingTowardsHeavySide)
        {
            // Moving towards heavy side: increase speed based on imbalance
            return 1f + (Mathf.Abs(weightImbalance) * weightSpeedFactor);
        }
        else
        {
            // Moving away from heavy side: decrease speed based on imbalance
            return 1f - (Mathf.Abs(weightImbalance) * weightSpeedFactor * 0.7f);
        }
    }

    // Get acceleration multiplier for a given direction
    public float GetAccelerationMultiplier(float direction)
    {
        // Similar logic to speed but with potentially different scaling
        float directionSign = Mathf.Sign(direction);
        float imbalanceSign = Mathf.Sign(weightImbalance);

        bool movingTowardsHeavySide = (directionSign == imbalanceSign && weightImbalance != 0);

        if (Mathf.Abs(weightImbalance) < 0.1f)
        {
            // Balanced, normal acceleration
            return 1f;
        }
        else if (movingTowardsHeavySide)
        {
            // Moving towards heavy side: increase acceleration
            return 1f + (Mathf.Abs(weightImbalance) * weightAccelFactor * 1.2f);
        }
        else
        {
            // Moving away from heavy side: decrease acceleration
            return 1f - (Mathf.Abs(weightImbalance) * weightAccelFactor * 0.8f);
        }
    }

    // Calculate the total weight of the robot and all attachments
    public float GetTotalWeight()
    {
        return totalWeight;
    }

    // Get the current weight imbalance value (-1 to 1)
    public float GetWeightImbalance()
    {
        return weightImbalance;
    }

    public float GetLeftSideWeight()
    {
        return leftSideWeight;
    }

    public float GetRightSideWeight()
    {
        return rightSideWeight;
    }

    public float GetTopSideWeight()
    {
        return topSideWeight;
    }

    private void OnDrawGizmos()
    {
        if (!showWeightDebug || !Application.isPlaying) return;

        // Draw weight bars for left and right sides
        float barWidth = 0.1f;
        float maxBarHeight = 2f;

        // Position bars above the robot
        Vector3 basePos = transform.position + Vector3.up * 1.5f;

        // Left weight bar
        Vector3 leftBarPos = basePos + Vector3.left * 0.5f;
        float leftBarHeight = Mathf.Clamp01(leftSideWeight / maxWeightImbalance) * maxBarHeight;

        Gizmos.color = Color.blue;
        Gizmos.DrawCube(
            leftBarPos + Vector3.up * (leftBarHeight / 2f),
            new Vector3(barWidth, leftBarHeight, 0.1f)
        );

        // Right weight bar
        Vector3 rightBarPos = basePos + Vector3.right * 0.5f;
        float rightBarHeight = Mathf.Clamp01(rightSideWeight / maxWeightImbalance) * maxBarHeight;

        Gizmos.color = Color.red;
        Gizmos.DrawCube(
            rightBarPos + Vector3.up * (rightBarHeight / 2f),
            new Vector3(barWidth, rightBarHeight, 0.1f)
        );

        // Draw balance indicator
        float balanceWidth = 0.8f;
        float balanceHeight = 0.1f;

        // Color based on imbalance
        float imbalanceFactor = Mathf.Abs(weightImbalance);
        Gizmos.color = Color.Lerp(balancedColor, imbalancedColor, imbalanceFactor);

        // Calculate balance bar position (tilted based on imbalance)
        Vector3 balanceCenter = basePos + Vector3.up * 0.2f;

        // Draw a line representing the balance
        Vector3 balanceLeft = balanceCenter + Vector3.left * (balanceWidth / 2f);
        Vector3 balanceRight = balanceCenter + Vector3.right * (balanceWidth / 2f);

        // Apply tilt based on imbalance
        float tiltHeight = weightImbalance * 0.3f; // Scale the tilt effect
        balanceLeft.y -= tiltHeight;
        balanceRight.y += tiltHeight;

        Gizmos.DrawLine(balanceLeft, balanceRight);

        // Draw weight values as text (using Debug.DrawLine to simulate text)
        // This is a bit hacky but provides visual feedback in the Scene view
    }
}