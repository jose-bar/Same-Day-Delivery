using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages UI elements that display weight balance information
/// </summary>
public class WeightUIManager : MonoBehaviour
{
    [Header("References")]
    public RobotController robotController;
    public WeightManager weightManager;

    [Header("UI Elements")]
    public GameObject weightPanelObject;
    public RectTransform leftWeightBar;
    public RectTransform rightWeightBar;
    public RectTransform balanceIndicator;
    public Text weightInfoText;

    [Header("Settings")]
    public float maxBarHeight = 100f;
    public float maxWeightForFullBar = 10f;
    public Color balancedColor = Color.green;
    public Color imbalancedColor = Color.red;
    public float balanceIndicatorRange = 50f;
    public bool showUI = true;

    // Smoothing variables
    private float leftBarTarget = 0f;
    private float rightBarTarget = 0f;
    private float balanceTarget = 0f;
    private float smoothingSpeed = 3f;

    private void Start()
    {
        // Find references if not set
        if (robotController == null)
        {
            robotController = FindObjectOfType<RobotController>();
        }

        if (weightManager == null && robotController != null)
        {
            weightManager = robotController.GetComponent<WeightManager>();
        }

        // Make sure we have UI elements
        if (weightPanelObject == null || leftWeightBar == null ||
            rightWeightBar == null || balanceIndicator == null)
        {
            Debug.LogWarning("Weight UI elements not assigned. Creating UI dynamically.");
            CreateUIElements();
        }

        // Set initial state
        if (weightPanelObject != null)
        {
            weightPanelObject.SetActive(showUI);
        }
    }

    private void CreateUIElements()
    {
        // This method would create a simple UI for weight display
        // In a real project, you'd probably use a prefab instead

        // Create a canvas if none exists
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("WeightUI_Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Create weight panel
        GameObject panel = new GameObject("WeightPanel");
        panel.transform.SetParent(canvas.transform, false);
        weightPanelObject = panel;

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(10, -10);
        panelRect.sizeDelta = new Vector2(200, 150);

        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0, 0, 0, 0.5f);

        // Create left weight bar
        GameObject leftBar = CreateBar(panel.transform, "LeftWeightBar", new Vector2(30, 20), Color.blue);
        leftWeightBar = leftBar.GetComponent<RectTransform>();

        // Create right weight bar
        GameObject rightBar = CreateBar(panel.transform, "RightWeightBar", new Vector2(130, 20), Color.red);
        rightWeightBar = rightBar.GetComponent<RectTransform>();

        // Create balance indicator
        GameObject indicator = new GameObject("BalanceIndicator");
        indicator.transform.SetParent(panel.transform, false);

        balanceIndicator = indicator.AddComponent<RectTransform>();
        balanceIndicator.anchorMin = new Vector2(0.5f, 0);
        balanceIndicator.anchorMax = new Vector2(0.5f, 0);
        balanceIndicator.pivot = new Vector2(0.5f, 0);
        balanceIndicator.anchoredPosition = new Vector2(0, 20);
        balanceIndicator.sizeDelta = new Vector2(160, 10);

        Image indImg = indicator.AddComponent<Image>();
        indImg.color = balancedColor;

        // Create text display
        GameObject textObj = new GameObject("WeightInfoText");
        textObj.transform.SetParent(panel.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0);
        textRect.anchorMax = new Vector2(1, 0);
        textRect.pivot = new Vector2(0.5f, 0);
        textRect.anchoredPosition = new Vector2(0, 5);
        textRect.sizeDelta = new Vector2(0, 20);

        weightInfoText = textObj.AddComponent<Text>();
        weightInfoText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        weightInfoText.fontSize = 14;
        weightInfoText.alignment = TextAnchor.LowerCenter;
        weightInfoText.color = Color.white;
        weightInfoText.text = "Weight: 0.0";
    }

    private GameObject CreateBar(Transform parent, string name, Vector2 position, Color color)
    {
        GameObject bar = new GameObject(name);
        bar.transform.SetParent(parent, false);

        RectTransform rect = bar.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 0);
        rect.pivot = new Vector2(0.5f, 0);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(40, 0); // Starting with zero height

        Image img = bar.AddComponent<Image>();
        img.color = color;

        return bar;
    }

    private void Update()
    {
        if (weightManager == null || !showUI) return;

        // Update UI with current weight values
        UpdateWeightBars();
        UpdateBalanceIndicator();
        UpdateWeightText();
    }

    private void UpdateWeightBars()
    {
        if (leftWeightBar == null || rightWeightBar == null) return;

        // Get current weights
        float leftWeight = weightManager.GetLeftSideWeight();
        float rightWeight = weightManager.GetRightSideWeight();

        // Calculate bar heights as percentage of max (with smoothing targets)
        leftBarTarget = Mathf.Clamp01(leftWeight / maxWeightForFullBar) * maxBarHeight;
        rightBarTarget = Mathf.Clamp01(rightWeight / maxWeightForFullBar) * maxBarHeight;

        // Smoothly update bar heights
        Vector2 leftSize = leftWeightBar.sizeDelta;
        leftSize.y = Mathf.Lerp(leftSize.y, leftBarTarget, Time.deltaTime * smoothingSpeed);
        leftWeightBar.sizeDelta = leftSize;

        Vector2 rightSize = rightWeightBar.sizeDelta;
        rightSize.y = Mathf.Lerp(rightSize.y, rightBarTarget, Time.deltaTime * smoothingSpeed);
        rightWeightBar.sizeDelta = rightSize;
    }

    private void UpdateBalanceIndicator()
    {
        if (balanceIndicator == null) return;

        // Get imbalance value (-1 to 1)
        float imbalance = weightManager.GetWeightImbalance();

        // Calculate indicator position based on imbalance
        balanceTarget = imbalance * balanceIndicatorRange;

        // Smoothly update position
        Vector2 pos = balanceIndicator.anchoredPosition;
        pos.x = Mathf.Lerp(pos.x, balanceTarget, Time.deltaTime * smoothingSpeed);
        balanceIndicator.anchoredPosition = pos;

        // Update color based on imbalance (green when balanced, red when imbalanced)
        Image img = balanceIndicator.GetComponent<Image>();
        if (img != null)
        {
            float imbalanceFactor = Mathf.Abs(imbalance);
            img.color = Color.Lerp(balancedColor, imbalancedColor, imbalanceFactor);
        }
    }

    private void UpdateWeightText()
    {
        if (weightInfoText == null) return;

        // Format weight information
        float leftWeight = weightManager.GetLeftSideWeight();
        float rightWeight = weightManager.GetRightSideWeight();
        float topWeight = weightManager.GetTopSideWeight();
        float totalWeight = weightManager.GetTotalWeight();

        weightInfoText.text = string.Format(
            "L: {0:F1}  R: {1:F1}  T: {2:F1}  Total: {3:F1}",
            leftWeight, rightWeight, topWeight, totalWeight);
    }

    // Method to toggle UI visibility
    public void ToggleUI()
    {
        showUI = !showUI;
        if (weightPanelObject != null)
        {
            weightPanelObject.SetActive(showUI);
        }
    }
}