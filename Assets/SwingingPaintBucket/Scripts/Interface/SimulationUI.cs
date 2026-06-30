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
        private MenuManager _menuManager;
        private GameObject _canvas;
        private GameObject _menuButton;
        private GameObject _hudPanel;
        private Text _hudText;
        private float _hudUpdateTimer = 0f;
        private const float HUD_UPDATE_INTERVAL = 0.2f;

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

            // دائماً إنشاء الـ HUD
            CreateHUD();

            _menuManager = _canvas.AddComponent<MenuManager>();
            
            var configField = typeof(MenuManager).GetField("_config", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (configField != null)
                configField.SetValue(_menuManager, RetroConfig);

            _menuManager.OnContinue += OnContinue;
            _menuManager.OnSettingsApplied += OnSettingsApplied;
            _menuManager.OnMenuClosed += OnMenuClosed;
            
            Debug.Log("UI Build complete!");
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

            btn.onClick.AddListener(() => {
                if (_menuManager != null)
                    _menuManager.OpenMenu();
            });
        }

        private void CreateHUD()
        {
            Debug.Log("Creating HUD...");
            
            // إنشاء اللوحة الرئيسية
            _hudPanel = new GameObject("HUDPanel", typeof(RectTransform));
            _hudPanel.transform.SetParent(_canvas.transform, false);
            
            var rt = _hudPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-20, 0);
            rt.sizeDelta = new Vector2(420, 0);

            // خلفية داكنة
            var bg = _hudPanel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.12f, 0.92f);
            bg.raycastTarget = false;

            // إطار ذهبي
            var outline = _hudPanel.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.8f, 0f, 0.4f);
            outline.effectDistance = new Vector2(3, 3);

            // Vertical Layout
            var vLayout = _hudPanel.AddComponent<VerticalLayoutGroup>();
            vLayout.padding = new RectOffset(15, 15, 15, 15);
            vLayout.spacing = 8;
            vLayout.childForceExpandWidth = true;
            vLayout.childControlHeight = true;

            // العنوان
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(_hudPanel.transform, false);
            
            var titleLayout = titleGo.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 35;
            
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "📊 SIMULATION INFO";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 18;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(1f, 0.8f, 0f);
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.raycastTarget = false;

            // خط فاصل
            var sepGo = new GameObject("Separator", typeof(RectTransform));
            sepGo.transform.SetParent(_hudPanel.transform, false);
            
            var sepLayout = sepGo.AddComponent<LayoutElement>();
            sepLayout.preferredHeight = 2;
            sepLayout.flexibleWidth = 1f;
            
            var sepImg = sepGo.AddComponent<Image>();
            sepImg.color = new Color(1f, 0.8f, 0f, 0.2f);
            sepImg.raycastTarget = false;

            // النص الرئيسي - مع تأكيد أنه سيظهر
            var textGo = new GameObject("HUDText", typeof(RectTransform));
            textGo.transform.SetParent(_hudPanel.transform, false);
            
            var textLayout = textGo.AddComponent<LayoutElement>();
            textLayout.flexibleHeight = 1f;
            textLayout.flexibleWidth = 1f;
            textLayout.preferredHeight = 400;
            
            _hudText = textGo.AddComponent<Text>();
            _hudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _hudText.fontSize = 14;
            _hudText.color = new Color(0.85f, 0.87f, 0.90f);
            _hudText.alignment = TextAnchor.UpperLeft;
            _hudText.supportRichText = true;
            _hudText.lineSpacing = 1.4f;
            _hudText.raycastTarget = false;

            // ظل للنص
            var shadow = textGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(1, -1);

            // تحديث النص فوراً
            UpdateHUDText();
            
            // إظهار الـ HUD إذا كان مفعلاً
            _hudPanel.SetActive(ShowHUD);
            
            Debug.Log("HUD created and updated!");
        }

        private void Update()
        {
            if (_hudText != null && _hudPanel != null && _hudPanel.activeSelf)
            {
                _hudUpdateTimer += Time.deltaTime;
                if (_hudUpdateTimer >= HUD_UPDATE_INTERVAL)
                {
                    _hudUpdateTimer = 0f;
                    UpdateHUDText();
                }
            }
        }

        private void UpdateHUDText()
        {
            if (_hudText == null) 
            {
                Debug.LogError("HUD Text is null!");
                return;
            }

            string info = "=== SIMULATION DATA ===\n\n";

            // PENDULUM
            if (_pendulum != null)
            {
                info += "⚡ PENDULUM\n";
                info += $"  Mass     : {_pendulum.Mass:F2} kg\n";
                info += $"  Length   : {_pendulum.RopeLength:F2} m\n";
                
                float angleDeg = _pendulum.Theta * Mathf.Rad2Deg;
                info += $"  Angle    : {angleDeg:F1}°\n";
                info += $"  Ang.Vel  : {_pendulum.Omega:F2} rad/s\n";
                
                Vector3 momentum = _pendulum.Momentum;
                info += $"  Momentum : {momentum.magnitude:F2} kg·m/s\n";
                info += "\n";
            }
            else
            {
                info += "⚡ PENDULUM: NOT FOUND\n\n";
            }

            // PAINT
            if (_bucket != null)
            {
                info += "🎨 PAINT\n";
                
                float currentVolume = _bucket.PaintVolume;
                info += $"  Volume   : {currentVolume:F3} L\n";
                
                float percentage = _bucket.InitialPaintVolume > 0 ? 
                    (currentVolume / _bucket.InitialPaintVolume) * 100f : 0f;
                info += $"  Remaining: {percentage:F1}%\n";
                
                info += $"  Status   : {(_bucket.HasPaint ? "● ACTIVE" : "○ EMPTY")}\n";
                info += $"  Viscosity: {_bucket.Viscosity:F2}\n";
                info += $"  Density  : {_bucket.Density:F2} g/cm³\n";
                
                float nozzleMm = _bucket.NozzleRadius * 1000f;
                info += $"  Nozzle   : {nozzleMm:F1} mm\n";
                info += $"  Flow     : {_bucket.VolumeThisFrame:F6} L/s\n";
                info += "\n";
            }
            else
            {
                info += "🎨 PAINT: NOT FOUND\n\n";
            }

            // MATERIAL
            if (_bucket != null)
            {
                info += "🔧 MATERIAL\n";
                info += $"  Type      : {_bucket.MaterialType}\n";
                info += $"  Discharge : {_bucket.DischargeCoefficent:F2}\n";
                info += $"  Loss      : {_bucket.PaintLossRate:F3}\n";
                info += $"  Absorption: {_bucket.AbsorptionRate:F3}\n";
            }
            else
            {
                info += "🔧 MATERIAL: NOT FOUND";
            }

            _hudText.text = info;
        }

        public void SetHUDVisible(bool visible)
        {
            ShowHUD = visible;
            if (_hudPanel != null)
            {
                _hudPanel.SetActive(visible);
                Debug.Log($"HUD visibility set to: {visible}");
                
                if (visible)
                    UpdateHUDText();
            }
        }

        public void RefreshHUD()
        {
            Debug.Log("Refreshing HUD...");
            UpdateHUDText();
        }

        public bool IsHUDVisible()
        {
            return ShowHUD && _hudPanel != null && _hudPanel.activeSelf;
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

            SimulationManager?.ResetSimulation();
            if (_bucket != null) _bucket.SyncPaintVolume();
            SimulationManager?.StartSimulation();
            RefreshHUD();
        }

        private void OnMenuClosed()
        {
            SimulationManager?.StartSimulation();
            RefreshHUD();
        }

        private void OnDestroy()
        {
            if (_menuManager != null)
            {
                _menuManager.OnContinue -= OnContinue;
                _menuManager.OnSettingsApplied -= OnSettingsApplied;
                _menuManager.OnMenuClosed -= OnMenuClosed;
            }
        }
    }
}