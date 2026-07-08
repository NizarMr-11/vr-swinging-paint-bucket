using UnityEngine;
using UnityEngine.UI;
using SwingingPaintBucket.Pendulum;
using SwingingPaintBucket.Bucket;
using SwingingPaintBucket.Simulation;
using SwingingPaintBucket.Interface.UI;

namespace SwingingPaintBucket.Interface
{
    public class SimulationUI : MonoBehaviour
    {
        [Header("Core References")]
        public SimulationManager SimulationManager;
        public GameObject BucketObject;

        [Header("Retro UI")]
        public RetroUIConfig RetroConfig;

        [Header("Menu Controls")]
        public bool ShowMenuButton = true;
        public bool EnableMenuSystem = true;

        [Header("HUD Settings")]
        public bool ShowHUD = true;

        private PendulumSimulator _pendulum;
        private BucketController _bucket;
        private EnvironmentController _environment;
        private MenuManager _menuManager;
        private GameObject _canvas;
        private GameObject _menuButton;
        private GameObject _hudPanel;

        // الأجزاء المقسمة
        private QuickSettings _quickSettings;
        private LogPanel _logPanel;

        private void Awake()
        {
            if (SimulationManager == null)
                SimulationManager = FindObjectOfType<SimulationManager>();

            if (BucketObject == null && SimulationManager != null)
                BucketObject = SimulationManager.BucketObject;

            if (BucketObject != null)
            {
                _pendulum = BucketObject.GetComponent<PendulumSimulator>();
                _bucket = BucketObject.GetComponent<BucketController>();
                _environment = _pendulum?.Environment;
            }

            ApplySavedSettings();
            BuildUI();
            Debug.Log("SimulationUI Awake complete!");
        }

        private void ApplySavedSettings()
        {
            if (_pendulum != null)
            {
                _pendulum.Mass = PlayerPrefs.GetFloat("Sim_Mass", _pendulum.Mass);
                _pendulum.RopeLength = PlayerPrefs.GetFloat("Sim_RopeLength", _pendulum.RopeLength);
                _pendulum.Gravity = PlayerPrefs.GetFloat("Sim_Gravity", _pendulum.Gravity);
                _pendulum.DampingCoefficient = PlayerPrefs.GetFloat("Sim_Damping", _pendulum.DampingCoefficient);
                _pendulum.InitialAngleDegrees = PlayerPrefs.GetFloat("Sim_InitialAngle", _pendulum.InitialAngleDegrees);
                _pendulum.InitialAngularVelocity = PlayerPrefs.GetFloat("Sim_AngularVelocity", _pendulum.InitialAngularVelocity);
            }
            if (_bucket != null)
            {
                _bucket.InitialPaintVolume = PlayerPrefs.GetFloat("Sim_PaintVolume", _bucket.InitialPaintVolume);
                _bucket.Viscosity = PlayerPrefs.GetFloat("Sim_Viscosity", _bucket.Viscosity);
                _bucket.Density = PlayerPrefs.GetFloat("Sim_Density", _bucket.Density);
                _bucket.NozzleRadius = PlayerPrefs.GetFloat("Sim_NozzleRadius", _bucket.NozzleRadius);
                _bucket.DischargeCoefficent = PlayerPrefs.GetFloat("Sim_Discharge", _bucket.DischargeCoefficent);
                _bucket.PaintLossRate = PlayerPrefs.GetFloat("Sim_PaintLoss", _bucket.PaintLossRate);
                _bucket.AbsorptionRate = PlayerPrefs.GetFloat("Sim_Absorption", _bucket.AbsorptionRate);
            }
            if (_environment != null)
            {
                float windMag = PlayerPrefs.GetFloat("Sim_WindForce", 0f);
                _environment.WindForce = new Vector3(windMag, 0f, 0f);
                _environment.Temperature = PlayerPrefs.GetFloat("Sim_Temperature", _environment.Temperature);
                _environment.Humidity = PlayerPrefs.GetFloat("Sim_Humidity", _environment.Humidity);
            }
        }

        private void BuildUI()
        {
            if (!EnableMenuSystem)
            {
                Debug.Log("Menu system is disabled");
                return;
            }

            if (RetroConfig == null)
            {
                Debug.LogWarning("Retro Config missing! Creating default...");
                RetroConfig = ScriptableObject.CreateInstance<RetroUIConfig>();
            }

            _canvas = UIFactory.CreateCanvas("RetroUI", transform);

            if (ShowMenuButton)
            {
                CreateMenuButton();
            }

            // إنشاء حاوية HUD
            CreateHUDPanel();

            // بناء واجهة الإعدادات السريعة
            var settingsGo = new GameObject("QuickSettings", typeof(RectTransform));
            settingsGo.transform.SetParent(_hudPanel.transform, false);
            _quickSettings = settingsGo.AddComponent<QuickSettings>();
            _quickSettings.Pendulum = _pendulum;
            _quickSettings.Bucket = _bucket;
            _quickSettings.Environment = _environment;
            _quickSettings.SimulationManager = SimulationManager;
            _quickSettings.BuildUI(_hudPanel.transform);

            // بناء لوحة المعلومات
            var logGo = new GameObject("LogPanel", typeof(RectTransform));
            logGo.transform.SetParent(_hudPanel.transform, false);
            _logPanel = logGo.AddComponent<LogPanel>();
            _logPanel.Pendulum = _pendulum;
            _logPanel.Bucket = _bucket;
            _logPanel.BuildUI(_hudPanel.transform);

            // ربط الأحداث
            _quickSettings.OnApply.AddListener(OnQuickApply);
            _quickSettings.OnCancel.AddListener(OnQuickCancel);

            // إعداد القائمة
            _menuManager = _canvas.AddComponent<MenuManager>();
            var configField = typeof(MenuManager).GetField("_config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (configField != null)
                configField.SetValue(_menuManager, RetroConfig);

            _menuManager.OnContinue += OnContinue;
            _menuManager.OnSettingsApplied += OnSettingsApplied;
            _menuManager.OnMenuClosed += OnMenuClosed;

            _hudPanel.SetActive(ShowHUD);
            Debug.Log("UI Build complete!");
        }

        private void CreateHUDPanel()
        {
            _hudPanel = new GameObject("HUDPanel", typeof(RectTransform));
            _hudPanel.transform.SetParent(_canvas.transform, false);

            var rt = _hudPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-20, 0);
            rt.sizeDelta = new Vector2(420, 0);

            var bg = _hudPanel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.12f, 0.92f);
            bg.raycastTarget = false;

            var outline = _hudPanel.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.8f, 0f, 0.4f);
            outline.effectDistance = new Vector2(3, 3);

            var vLayout = _hudPanel.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(12, 12, 0, 0);
            vLayout.spacing = 0;
            vLayout.childForceExpandWidth = true;
            vLayout.childControlHeight = true;
        }

        private void CreateMenuButton()
        {
            _menuButton = new GameObject("MenuButton", typeof(RectTransform));
            _menuButton.transform.SetParent(_canvas.transform, false);

            var rt = _menuButton.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(80, -50);
            rt.sizeDelta = new Vector2(120, 45);

            var img = _menuButton.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.3f, 0.9f);

            var outline = _menuButton.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.8f, 0f, 0.5f);
            outline.effectDistance = new Vector2(2, 2);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(_menuButton.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            var text = textGo.AddComponent<Text>();
            text.text = "⚙ MENU";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            var btn = _menuButton.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = new Color(0.2f, 0.2f, 0.3f, 0.9f);
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.5f, 0.9f);
            colors.pressedColor = new Color(0.1f, 0.1f, 0.2f, 0.9f);
            btn.colors = colors;

            btn.onClick.AddListener(() => _menuManager?.OpenMenu());
        }

        private void OnQuickApply()
        {
            Debug.Log("Quick Apply");
            SimulationManager?.ResetSimulation();
            _bucket?.SyncPaintVolume();
            SimulationManager?.StartSimulation();
            RefreshHUD();
        }

        private void OnQuickCancel()
        {
            Debug.Log("Quick Cancel");
            // القيم تعود تلقائياً داخل QuickSettings
            RefreshHUD();
        }

        public void RefreshHUD()
        {
            _logPanel?.UpdateInfo();
        }

        private void OnContinue()
        {
            SimulationManager?.StartSimulation();
            RefreshHUD();
        }

        private void OnSettingsApplied()
        {
            var settingsPanel = _canvas.GetComponentInChildren<SettingsPanel>();
            if (settingsPanel != null)
                settingsPanel.ApplySettings();

            _environment = _pendulum?.Environment;
            _quickSettings?.SyncSlidersToSaved();

            SimulationManager?.ResetSimulation();
            _bucket?.SyncPaintVolume();
            SimulationManager?.StartSimulation();
            RefreshHUD();
        }

        private void OnMenuClosed()
        {
            SimulationManager?.StartSimulation();
            RefreshHUD();
        }

        public void SetHUDVisible(bool visible)
        {
            ShowHUD = visible;
            if (_hudPanel != null)
            {
                _hudPanel.SetActive(visible);
                if (visible) RefreshHUD();
            }
        }

        public bool IsHUDVisible()
        {
            return ShowHUD && _hudPanel != null && _hudPanel.activeSelf;
        }

        private void OnDestroy()
        {
            if (_menuManager != null)
            {
                _menuManager.OnContinue -= OnContinue;
                _menuManager.OnSettingsApplied -= OnSettingsApplied;
                _menuManager.OnMenuClosed -= OnMenuClosed;
            }
            if (_quickSettings != null)
            {
                _quickSettings.OnApply.RemoveListener(OnQuickApply);
                _quickSettings.OnCancel.RemoveListener(OnQuickCancel);
            }
        }
    }
}