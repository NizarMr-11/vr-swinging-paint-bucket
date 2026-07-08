using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class ExperimentUI : MonoBehaviour
{
    [Header("=== UI References ===")]
    [SerializeField] private GameObject experimentPanel;
    [SerializeField] private TextMeshProUGUI experimentTitleText;
    [SerializeField] private TextMeshProUGUI experimentDetailsText;
    [SerializeField] private TextMeshProUGUI experimentCountText;
    [SerializeField] private TextMeshProUGUI currentExperimentIndexText;

    [Header("=== Navigation Buttons ===")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button recordButton;
    [SerializeField] private Button clearButton;

    [Header("=== System References ===")]
    [SerializeField] private ExperimentManager experimentManager;

    private int currentIndex = 0;
    private bool isPanelVisible = true;

    void Start()
    {
        // التحقق من وجود جميع المراجع
        ValidateReferences();

        // إعداد الأزرار
        SetupButtons();

        if (experimentManager == null)
        {
            experimentManager = FindAnyObjectByType<ExperimentManager>();
            if (experimentManager == null)
            {
                Debug.LogError(" ExperimentManager not found in scene!");
                return;
            }
        }

        UpdateUI();

        Debug.Log(" ExperimentUI initialized. Press 'H' to toggle panel.");
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            TogglePanelVisibility();
        }
    }

    private void ValidateReferences()
    {
        if (experimentPanel == null)
            Debug.LogWarning(" experimentPanel not assigned in Inspector!");
        if (experimentTitleText == null)
            Debug.LogWarning(" experimentTitleText not assigned in Inspector!");
        if (experimentDetailsText == null)
            Debug.LogWarning(" experimentDetailsText not assigned in Inspector!");
        if (experimentCountText == null)
            Debug.LogWarning(" experimentCountText not assigned in Inspector!");
        if (currentExperimentIndexText == null)
            Debug.LogWarning(" currentExperimentIndexText not assigned in Inspector!");
    }

    private void SetupButtons()
    {
        if (recordButton != null)
            recordButton.onClick.AddListener(OnRecordButtonPressed);
        else
            Debug.LogWarning(" recordButton not assigned!");

        if (clearButton != null)
            clearButton.onClick.AddListener(OnClearButtonPressed);
        else
            Debug.LogWarning(" clearButton not assigned!");

        if (previousButton != null)
            previousButton.onClick.AddListener(ShowPreviousExperiment);
        else
            Debug.LogWarning(" previousButton not assigned!");

        if (nextButton != null)
            nextButton.onClick.AddListener(ShowNextExperiment);
        else
            Debug.LogWarning(" nextButton not assigned!");
    }

    private void TogglePanelVisibility()
    {
        if (experimentPanel != null)
        {
            isPanelVisible = !isPanelVisible;
            experimentPanel.SetActive(isPanelVisible);
            Debug.Log($" Panel visibility: {(isPanelVisible ? "ON" : "OFF")}");
        }
    }

    private void OnRecordButtonPressed()
    {
        if (experimentManager == null)
        {
            Debug.LogError("ExperimentManager is null!");
            return;
        }

        string name = "Exp_" + (experimentManager.pastExperiments.Count + 1);
        experimentManager.RecordCurrentExperiment(name);
        UpdateUI();
        Debug.Log($"Recorded: {name}");
    }

    private void OnClearButtonPressed()
    {
        if (experimentManager == null)
        {
            Debug.LogError("ExperimentManager is null!");
            return;
        }

        experimentManager.pastExperiments.Clear();
        currentIndex = 0;
        UpdateUI();
        Debug.Log("All experiments cleared!");
    }

    private void ShowPreviousExperiment()
    {
        if (experimentManager == null || experimentManager.pastExperiments.Count == 0)
            return;

        currentIndex = (currentIndex - 1 + experimentManager.pastExperiments.Count) % experimentManager.pastExperiments.Count;
        UpdateUI();
        Debug.Log($"Showing experiment {currentIndex + 1}/{experimentManager.pastExperiments.Count}");
    }

    private void ShowNextExperiment()
    {
        if (experimentManager == null || experimentManager.pastExperiments.Count == 0)
            return;

        currentIndex = (currentIndex + 1) % experimentManager.pastExperiments.Count;
        UpdateUI();
        Debug.Log($"Showing experiment {currentIndex + 1}/{experimentManager.pastExperiments.Count}");
    }

    private void UpdateUI()
    {
        if (experimentManager == null)
        {
            SetUIState(false);
            return;
        }

        int totalExperiments = experimentManager.pastExperiments.Count;

        if (experimentCountText != null)
            experimentCountText.text = $"Total Experiments: {totalExperiments}";

        if (totalExperiments == 0)
        {
            SetUIState(false);
            return;
        }

        if (currentIndex >= totalExperiments)
            currentIndex = totalExperiments - 1;

        SetUIState(true);

        var exp = experimentManager.pastExperiments[currentIndex];

        if (experimentTitleText != null)
            experimentTitleText.text = $" {exp.experimentName}";

        if (currentExperimentIndexText != null)
            currentExperimentIndexText.text = $"({currentIndex + 1}/{totalExperiments})";

        if (experimentDetailsText != null)
        {
            string details = $"<b><color=#FFD700> Parameters </b><br>" +
                            $"  • Rope Length    : <color=#4FC3F7>{exp.ropeLength:F3} m</color><br>" +
                            $"  • Nozzle Radius  : <color=#4FC3F7>{exp.nozzleRadius:F3} m</color><br>" +
                            $"  • Viscosity      : <color=#4FC3F7>{exp.viscosity:F3}</color><br>" +
                            $"  • Gravity        : <color=#4FC3F7>{exp.gravity:F2} m/s²</color><br><br>" +

                            $"<b><color=#FFD700> Dynamics </b><br>" +
                            $"  • Initial Angle  : <color=#FFB74D>{exp.initialAngle:F2}°</color><br>" +
                            $"  • Velocity (ω)   : <color=#FFB74D>{exp.currentAngularVelocity:F3} rad/s</color><br>" +
                            $"  • Acceleration (α): <color=#FFB74D>{exp.currentAngularAcceleration:F3} rad/s²</color><br><br>" +

                            $"<b><color=#FFD700> Results </b><br>" +
                            $"  • Spilled Paint  : <color=#FF6B6B>{exp.spilledPaint:F4} L</color><br>" +
                            $"  • Painted Area   : <color=#81C784>{exp.paintedArea:F2} m²</color>";

            experimentDetailsText.text = details;
        }
    }

    private void SetUIState(bool hasExperiments)
    {
        // إظهار أو إخفاء أزرار التنقل حسب وجود تجارب
        if (previousButton != null)
            previousButton.interactable = hasExperiments && experimentManager.pastExperiments.Count > 1;

        if (nextButton != null)
            nextButton.interactable = hasExperiments && experimentManager.pastExperiments.Count > 1;

        // إذا لم توجد تجارب، عرض رسالة
        if (experimentDetailsText != null && !hasExperiments)
        {
            experimentDetailsText.text = "<b><color=#FFD700> No experiments recorded yet.</color></b>\n\n" +
                                        "Press <color=#4CAF50>'R'</color> to record or click the <color=#4CAF50>Record</color> button.\n\n" +
                                        "Press <color=#FF6B6B>'C'</color> to clear all experiments.\n\n" +
                                        "Press <color=#2196F3>'H'</color> to toggle panel.";
        }

        if (experimentTitleText != null && !hasExperiments)
            experimentTitleText.text = " No Data";

        if (currentExperimentIndexText != null && !hasExperiments)
            currentExperimentIndexText.text = "(0/0)";
    }
}