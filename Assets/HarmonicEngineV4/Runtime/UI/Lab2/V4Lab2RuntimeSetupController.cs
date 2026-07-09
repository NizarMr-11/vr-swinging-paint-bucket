using HarmonicEngineV4.Profiles;
using HarmonicEngineV4.Rendering;
using HarmonicEngineV4.Simulation;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HarmonicEngineV4.UI.Lab2
{
    public enum V4Lab2UiState
    {
        Startup,
        Wizard,
        Running
    }

    /// <summary>Lab2 startup choice, setup wizard, and play HUD orchestration.</summary>
    [DefaultExecutionOrder(-250)]
    public sealed class V4Lab2RuntimeSetupController : MonoBehaviour
    {
        [Header("Scene refs")]
        [SerializeField] private V4PipelineRoot pipeline;
        [SerializeField] private V4Bucket bucket;
        [SerializeField] private V4SphericalPendulumController pendulum;
        [SerializeField] private Transform pivotTransform;
        [SerializeField] private V4TorricelliEjectionSettings torricelli;
        [SerializeField] private V4RenderingSettings rendering;
        [SerializeField] private V4LiquidProfile lab2LiquidProfile;
        [SerializeField] private V4LiquidProfile defaultLiquidProfile;

        [Header("Behaviour")]
        [SerializeField] private bool skipStartupPrompt;

        [Header("Panels")]
        [SerializeField] private GameObject startupPanel;
        [SerializeField] private GameObject wizardPanel;
        [SerializeField] private GameObject playHud;

        [Header("Startup")]
        [SerializeField] private Button runSceneButton;
        [SerializeField] private Button setupSceneButton;

        [Header("Wizard - pendulum")]
        [SerializeField] private Slider pivotHeightSlider;
        [SerializeField] private Slider ropeLengthSlider;
        [SerializeField] private Slider swingSpeedSlider;
        [SerializeField] private Dropdown swingDirectionDropdown;
        [SerializeField] private Slider dampingSlider;
        [SerializeField] private Slider twistSlider;
        [SerializeField] private Slider sloshSlider;

        [Header("Wizard - layers")]
        [SerializeField] private Slider[] layerThicknessSliders = new Slider[V4Lab2SessionConfig.LayerCount];
        [SerializeField] private Button[] layerBlackButtons = new Button[V4Lab2SessionConfig.LayerCount];
        [SerializeField] private Button[] layerWhiteButtons = new Button[V4Lab2SessionConfig.LayerCount];
        [SerializeField] private Button[] layerYellowButtons = new Button[V4Lab2SessionConfig.LayerCount];
        [SerializeField] private Button balanceLayersButton;

        [Header("Wizard - holes & sim")]
        [SerializeField] private Slider hole0RadiusSlider;
        [SerializeField] private Slider hole1RadiusSlider;
        [SerializeField] private Slider torricelliHeadSlider;
        [SerializeField] private Dropdown liquidProfileDropdown;
        [SerializeField] private Slider densitySlider;
        [SerializeField] private Slider carryRateSlider;
        [SerializeField] private Slider pbfSlider;
        [SerializeField] private Slider ghostSlider;
        [SerializeField] private Slider gravitySlider;
        [SerializeField] private Slider viscositySlider;
        [SerializeField] private Slider cohesionSlider;
        [SerializeField] private Slider surfaceFrictionSlider;
        [SerializeField] private Slider surfaceRestitutionSlider;
        [SerializeField] private Slider zone0StrengthSlider;
        [SerializeField] private Slider colorDiffusionSlider;
        [SerializeField] private Slider settleEpsilonSlider;
        [SerializeField] private Text validationText;
        [SerializeField] private Button applyAndStartButton;
        [SerializeField] private Button resetDefaultsButton;
        [SerializeField] private Button wizardBackButton;

        [Header("Play HUD")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Text pauseButtonLabel;
        [SerializeField] private Button tuningToggleButton;
        [SerializeField] private V4Lab2StatsHudController statsHud;
        [SerializeField] private V4Lab2RuntimeTuningController tuning;

        private readonly V4Lab2SessionConfig _config = new V4Lab2SessionConfig();
        private V4Lab2UiState _state = V4Lab2UiState.Startup;
        private bool _uiBuilt;

        public V4Lab2UiState State => _state;

        private void Awake()
        {
            V4Lab2EventSystemUtility.EnsureCompatibleInputModule();
            ResolveSceneRefs();
            if (pipeline != null)
            {
                pipeline.deferAutoInitialize = true;
                pipeline.autoRun = false;
            }

            if (pendulum != null)
            {
                pendulum.enableKeyboardReset = false;
            }

            V4Lab2SetupApplicator.CaptureFromScene(BuildSceneRefs(), _config);
            WireEvents();
        }

        private void Start()
        {
            if (skipStartupPrompt)
            {
                BeginRunning(initializeOnly: true);
                return;
            }

            ShowStartup();
        }

        private void Update()
        {
            if (_state != V4Lab2UiState.Running)
            {
                return;
            }

            if (WasTabPressed())
            {
                tuning?.ToggleVisible();
            }

            if (WasSpacePressed())
            {
                TogglePause();
            }
        }

        public void OpenSetupFromPlay()
        {
            if (pipeline != null)
            {
                pipeline.autoRun = false;
            }

            V4Lab2SetupApplicator.CaptureFromScene(BuildSceneRefs(), _config);
            SyncWizardFromConfig();
            tuning?.SetVisible(false);
            statsHud?.SetPaused(true);
            UpdatePauseLabel();
            _state = V4Lab2UiState.Wizard;
            SetPanelActive(startupPanel, false);
            SetPanelActive(wizardPanel, true);
            SetPanelActive(playHud, true);
        }

        public void TogglePause()
        {
            if (pipeline == null || _state != V4Lab2UiState.Running)
            {
                return;
            }

            pipeline.autoRun = !pipeline.autoRun;
            statsHud?.SetPaused(!pipeline.autoRun);
            UpdatePauseLabel();
        }

        private void BeginRunning(bool initializeOnly)
        {
            if (pipeline != null)
            {
                if (!pipeline.Initialized)
                {
                    pipeline.Initialize();
                }

                pipeline.autoRun = true;
            }

            _state = V4Lab2UiState.Running;
            SetPanelActive(startupPanel, false);
            SetPanelActive(wizardPanel, false);
            SetPanelActive(playHud, true);
            statsHud?.SetVisible(true);
            statsHud?.SetPaused(false);
            tuning?.SyncFromScene();
            tuning?.SetVisible(false);
            UpdatePauseLabel();
        }

        private void ShowStartup()
        {
            _state = V4Lab2UiState.Startup;
            SetPanelActive(startupPanel, true);
            SetPanelActive(wizardPanel, false);
            SetPanelActive(playHud, false);
        }

        private void ShowWizard()
        {
            V4Lab2SetupApplicator.CaptureFromScene(BuildSceneRefs(), _config);
            SyncWizardFromConfig();
            _state = V4Lab2UiState.Wizard;
            SetPanelActive(startupPanel, false);
            SetPanelActive(wizardPanel, true);
            SetPanelActive(playHud, false);
            SetValidation(null);
        }

        private void OnRunScene()
        {
            BeginRunning(initializeOnly: true);
        }

        private void OnSetupScene()
        {
            ShowWizard();
        }

        private void OnApplyAndStart()
        {
            ReadConfigFromWizard();
            string error = V4Lab2SetupApplicator.ValidateConfig(_config, BuildSceneRefs());
            if (!string.IsNullOrEmpty(error))
            {
                SetValidation(error);
                return;
            }

            V4Lab2SetupApplicator.ApplyFull(_config, BuildSceneRefs(), reinitializePipeline: true);
            BeginRunning(initializeOnly: false);
        }

        private void OnResetDefaults()
        {
            V4Lab2SetupApplicator.CaptureFromScene(BuildSceneRefs(), _config);
            SyncWizardFromConfig();
            SetValidation(null);
        }

        private void OnWizardBack()
        {
            ShowStartup();
        }

        private void OnBalanceLayers()
        {
            if (bucket == null)
            {
                return;
            }

            _config.ResetLayersToDefaults(bucket.height);
            SyncWizardFromConfig();
        }

        private void OnLiquidProfileChanged(int index)
        {
            _config.liquidProfilePreset = index;
            V4LiquidProfile profile = index == 0 ? lab2LiquidProfile : defaultLiquidProfile;
            if (profile != null)
            {
                _config.liquidTuning = V4Lab2LiquidTuning.FromProfile(profile);
                SyncLiquidTuningSliders();
            }
        }

        private void SetLayerColor(int layerIndex, Color color)
        {
            if (layerIndex < 0 || layerIndex >= _config.layers.Length)
            {
                return;
            }

            V4Lab2LayerConfig layer = _config.layers[layerIndex];
            layer.color = color;
            _config.layers[layerIndex] = layer;
        }

        private V4Lab2SetupApplicator.SceneRefs BuildSceneRefs()
        {
            return new V4Lab2SetupApplicator.SceneRefs
            {
                pipeline = pipeline,
                bucket = bucket,
                pendulum = pendulum,
                pivotTransform = pivotTransform,
                torricelli = torricelli,
                lab2LiquidProfile = lab2LiquidProfile,
                defaultLiquidProfile = defaultLiquidProfile
            };
        }

        private void ResolveSceneRefs()
        {
            if (pipeline == null)
            {
                pipeline = FindFirstObjectByType<V4PipelineRoot>();
            }

            if (bucket == null && pipeline != null)
            {
                bucket = pipeline.bucket;
            }

            if (pendulum == null && bucket != null)
            {
                pendulum = bucket.GetComponent<V4SphericalPendulumController>();
            }

            if (torricelli == null && bucket != null)
            {
                torricelli = bucket.GetComponent<V4TorricelliEjectionSettings>();
            }

            if (rendering == null && pipeline != null)
            {
                rendering = pipeline.GetComponent<V4RenderingSettings>();
            }

            if (pivotTransform == null)
            {
                GameObject pivot = GameObject.Find("Pendulum Pivot");
                if (pivot != null)
                {
                    pivotTransform = pivot.transform;
                }
            }

            if (lab2LiquidProfile == null)
            {
                lab2LiquidProfile = Resources.Load<V4LiquidProfile>("HarmonicEngineV4/V4Liquid_Lab2");
            }

            if (defaultLiquidProfile == null && pipeline != null)
            {
                defaultLiquidProfile = pipeline.globalProfile;
            }
        }

        private void WireEvents()
        {
            BindButton(runSceneButton, OnRunScene);
            BindButton(setupSceneButton, OnSetupScene);
            BindButton(applyAndStartButton, OnApplyAndStart);
            BindButton(resetDefaultsButton, OnResetDefaults);
            BindButton(wizardBackButton, OnWizardBack);
            BindButton(balanceLayersButton, OnBalanceLayers);
            BindButton(pauseButton, TogglePause);
            BindButton(tuningToggleButton, () => tuning?.ToggleVisible());

            if (liquidProfileDropdown != null)
            {
                liquidProfileDropdown.onValueChanged.RemoveListener(OnLiquidProfileChanged);
                liquidProfileDropdown.onValueChanged.AddListener(OnLiquidProfileChanged);
            }

            for (int i = 0; i < V4Lab2SessionConfig.LayerCount; i++)
            {
                int layerIndex = i;
                BindButton(layerBlackButtons != null && i < layerBlackButtons.Length ? layerBlackButtons[i] : null,
                    () => SetLayerColor(layerIndex, Color.black));
                BindButton(layerWhiteButtons != null && i < layerWhiteButtons.Length ? layerWhiteButtons[i] : null,
                    () => SetLayerColor(layerIndex, Color.white));
                BindButton(layerYellowButtons != null && i < layerYellowButtons.Length ? layerYellowButtons[i] : null,
                    () => SetLayerColor(layerIndex, Color.yellow));
            }
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null || action == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void SyncWizardFromConfig()
        {
            SetSlider(pivotHeightSlider, _config.pivotHeightY);
            SetSlider(ropeLengthSlider, _config.ropeLength);
            SetSlider(swingSpeedSlider, _config.swingSpeed);
            if (swingDirectionDropdown != null)
            {
                swingDirectionDropdown.SetValueWithoutNotify((int)_config.swingDirection);
            }

            SetSlider(dampingSlider, _config.dampingCoefficient);
            SetSlider(twistSlider, _config.twistAngleDegrees);
            SetSlider(sloshSlider, _config.sloshFeedbackScale);

            for (int i = 0; i < V4Lab2SessionConfig.LayerCount; i++)
            {
                if (layerThicknessSliders != null && i < layerThicknessSliders.Length && layerThicknessSliders[i] != null)
                {
                    layerThicknessSliders[i].SetValueWithoutNotify(_config.layers[i].thickness);
                }
            }

            SetSlider(hole0RadiusSlider, _config.hole0Radius);
            SetSlider(hole1RadiusSlider, _config.hole1Radius);
            SetSlider(torricelliHeadSlider, _config.torricelliHeadHeight);
            if (liquidProfileDropdown != null)
            {
                liquidProfileDropdown.SetValueWithoutNotify(_config.liquidProfilePreset);
            }

            SetSlider(densitySlider, _config.globalDensity);
            SetSlider(carryRateSlider, _config.carryRate);
            SetSlider(pbfSlider, _config.pbfIterations);
            SetSlider(ghostSlider, _config.boundaryGhostWeight);
            SetSlider(gravitySlider, _config.gravityY);
            SyncLiquidTuningSliders();
        }

        private void SyncLiquidTuningSliders()
        {
            SetSlider(viscositySlider, _config.liquidTuning.viscosity);
            SetSlider(cohesionSlider, _config.liquidTuning.cohesion);
            SetSlider(surfaceFrictionSlider, _config.liquidTuning.surfaceFriction);
            SetSlider(surfaceRestitutionSlider, _config.liquidTuning.surfaceRestitution);
            SetSlider(zone0StrengthSlider, _config.liquidTuning.zone0Strength);
            SetSlider(colorDiffusionSlider, _config.liquidTuning.colorDiffusionRate);
            SetSlider(settleEpsilonSlider, _config.liquidTuning.settleEpsilon);
        }

        private void ReadConfigFromWizard()
        {
            _config.pivotHeightY = ReadSlider(pivotHeightSlider, _config.pivotHeightY);
            _config.ropeLength = ReadSlider(ropeLengthSlider, _config.ropeLength);
            _config.swingSpeed = ReadSlider(swingSpeedSlider, _config.swingSpeed);
            if (swingDirectionDropdown != null)
            {
                _config.swingDirection = (V4Lab2SwingDirection)swingDirectionDropdown.value;
            }

            _config.dampingCoefficient = ReadSlider(dampingSlider, _config.dampingCoefficient);
            _config.twistAngleDegrees = ReadSlider(twistSlider, _config.twistAngleDegrees);
            _config.sloshFeedbackScale = ReadSlider(sloshSlider, _config.sloshFeedbackScale);

            for (int i = 0; i < V4Lab2SessionConfig.LayerCount; i++)
            {
                if (layerThicknessSliders != null && i < layerThicknessSliders.Length && layerThicknessSliders[i] != null)
                {
                    V4Lab2LayerConfig layer = _config.layers[i];
                    layer.thickness = layerThicknessSliders[i].value;
                    _config.layers[i] = layer;
                }
            }

            _config.hole0Radius = ReadSlider(hole0RadiusSlider, _config.hole0Radius);
            _config.hole1Radius = ReadSlider(hole1RadiusSlider, _config.hole1Radius);
            _config.torricelliHeadHeight = ReadSlider(torricelliHeadSlider, _config.torricelliHeadHeight);
            if (liquidProfileDropdown != null)
            {
                _config.liquidProfilePreset = liquidProfileDropdown.value;
            }

            _config.globalDensity = ReadSlider(densitySlider, _config.globalDensity);
            _config.carryRate = ReadSlider(carryRateSlider, _config.carryRate);
            _config.pbfIterations = Mathf.RoundToInt(ReadSlider(pbfSlider, _config.pbfIterations));
            _config.boundaryGhostWeight = ReadSlider(ghostSlider, _config.boundaryGhostWeight);
            _config.gravityY = ReadSlider(gravitySlider, _config.gravityY);

            V4Lab2LiquidTuning liquid = _config.liquidTuning;
            liquid.viscosity = ReadSlider(viscositySlider, liquid.viscosity);
            liquid.cohesion = ReadSlider(cohesionSlider, liquid.cohesion);
            liquid.surfaceFriction = ReadSlider(surfaceFrictionSlider, liquid.surfaceFriction);
            liquid.surfaceRestitution = ReadSlider(surfaceRestitutionSlider, liquid.surfaceRestitution);
            liquid.zone0Strength = ReadSlider(zone0StrengthSlider, liquid.zone0Strength);
            liquid.colorDiffusionRate = ReadSlider(colorDiffusionSlider, liquid.colorDiffusionRate);
            liquid.settleEpsilon = ReadSlider(settleEpsilonSlider, liquid.settleEpsilon);
            _config.liquidTuning = liquid;
        }

        private static void SetSlider(Slider slider, float value)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(value);
            }
        }

        private static float ReadSlider(Slider slider, float fallback)
        {
            return slider != null ? slider.value : fallback;
        }

        private void SetValidation(string message)
        {
            if (validationText == null)
            {
                return;
            }

            bool hasError = !string.IsNullOrEmpty(message);
            validationText.gameObject.SetActive(hasError);
            validationText.text = hasError ? message : string.Empty;
            validationText.color = Color.Lerp(V4Lab2UITheme.TextColor, new Color(1f, 0.45f, 0.4f), hasError ? 1f : 0f);
        }

        private void UpdatePauseLabel()
        {
            if (pauseButtonLabel == null || pipeline == null)
            {
                return;
            }

            pauseButtonLabel.text = pipeline.autoRun ? "Pause" : "Resume";
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }

        private static bool WasTabPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Tab);
#endif
        }

        private static bool WasSpacePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }

        /// <summary>Called by the editor UI builder after hierarchy creation.</summary>
        public void MarkUiBuilt() => _uiBuilt = true;

        public bool UiBuilt => _uiBuilt;
    }
}
