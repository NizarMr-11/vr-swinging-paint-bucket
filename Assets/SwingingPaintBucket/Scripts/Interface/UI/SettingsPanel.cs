using System;
using SwingingPaintBucket.Bucket;
using SwingingPaintBucket.Canvas;
using SwingingPaintBucket.Materials;
using SwingingPaintBucket.Pendulum;
using SwingingPaintBucket.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace SwingingPaintBucket.Interface.UI
{
    public class SettingsPanel : MonoBehaviour
    {
        private RetroUIConfig _config;
        private Transform _parentTransform;

        private SimulationManager _simManager;
        private PendulumSimulator _pendulum;
        private BucketController _bucket;
        private EnvironmentController _environment;
        private CanvasController _canvas;

        private Slider _bucketWeightSlider;
        private Slider _bucketRadiusSlider;
        private Slider _ropeLengthSlider;
        private Slider _gravitySlider;
        private Slider _airResistanceSlider;
        private Slider _frictionSlider;
        private Slider _initialAngleSlider;
        private Slider _initialPhiSlider;
        private Slider _angularVelocitySlider;
        private Slider _swingCountSlider;

        private Slider _paintVolumeSlider;
        private Slider _viscositySlider;
        private Slider _densitySlider;
        private Slider _nozzleRadiusSlider;
        private Slider _dischargeSlider;
        private Slider _paintLossSlider;
        private Slider _absorptionSlider;

        private Slider _temperatureSlider;
        private Slider _humiditySlider;

        private Slider _canvasWidthSlider;
        private Slider _canvasHeightSlider;
        private Slider _canvasTiltSlider;
        private Slider _canvasImpactSlider;

        private Text _ropeTypeText;
        private Text _directionText;
        private Text _bucketMaterialText;
        private Text _canvasSurfaceText;
        private Text _paintColorText;

        private int _ropeTypeIndex;
        private int _directionIndex;
        private int _bucketMaterialIndex;
        private int _canvasSurfaceIndex;

        private Image _paintColorPreview;
        private Color _selectedPaintColor = Color.red;

        private Color SelectedTabColor => new Color(0.3f, 0.6f, 1f);
        private Color UnselectedTabColor => new Color(0.15f, 0.15f, 0.2f);
        private Color TextColor => _config != null ? _config.TextColor : Color.white;
        private Color AccentColor => _config != null ? _config.AccentColor : Color.yellow;
        private Color SecondaryColor => _config != null ? _config.SecondaryColor : Color.cyan;
        private Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public void Initialize(Transform parent, RetroUIConfig config)
        {
            _config = config;
            _parentTransform = parent;
            CacheReferences();
            BuildSettingsUI(parent);
            LoadSavedValues();
            RefreshSelectorTexts();
            Debug.Log("[SettingsPanel] Initialization complete.");
        }

        private void BuildSettingsUI(Transform parent)
        {
            var vLayout = parent.GetComponent<VerticalLayoutGroup>();
            if (vLayout == null)
                vLayout = parent.gameObject.AddComponent<VerticalLayoutGroup>();

            vLayout.spacing = 15;
            vLayout.padding = new RectOffset(20, 20, 10, 10);
            vLayout.childForceExpandWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandHeight = false;

            BuildBucketSection(parent);
            BuildRopePendulumSection(parent);
            BuildPaintSection(parent);
            BuildEnvironmentSection(parent);
            BuildCanvasSection(parent);
            BuildAppearanceSection(parent);
            BuildActionSection(parent);
        }

        private void BuildBucketSection(Transform parent)
        {
            CreateSectionHeader(parent, "BUCKET PHYSICAL PROPERTIES");
            _bucketWeightSlider = CreateHorizontalSlider(parent, "Bucket Weight", 0.1f, 50f, _bucket != null ? _bucket.BucketWeightKg : 1f, "kg");
            _bucketRadiusSlider = CreateHorizontalSlider(parent, "Bucket Radius", 0.03f, 0.5f, _bucket != null ? _bucket.BucketRadius : 0.15f, "m");
            CreateCycleSelector(parent, "Bucket Material", () => CycleBucketMaterial(), out _bucketMaterialText);
            CreateSeparator(parent);
        }

        private void BuildRopePendulumSection(Transform parent)
        {
            CreateSectionHeader(parent, "ROPE AND PENDULUM PHYSICS");
            _ropeLengthSlider = CreateHorizontalSlider(parent, "Rope Length", 0.5f, 20f, _pendulum != null ? _pendulum.RopeLength : 5f, "m");
            CreateCycleSelector(parent, "Rope Type", () => CycleRopeType(), out _ropeTypeText);
            _gravitySlider = CreateHorizontalSlider(parent, "Gravity", 0f, 20f, _pendulum != null ? _pendulum.Gravity : 9.81f, "m/s²");
            _airResistanceSlider = CreateHorizontalSlider(parent, "Air Resistance", 0f, 1f, _pendulum != null ? _pendulum.AirResistanceCoefficient : 0.05f, "");
            _frictionSlider = CreateHorizontalSlider(parent, "Friction", 0f, 1f, _pendulum != null ? _pendulum.FrictionCoefficient : 0.02f, "");
            _initialAngleSlider = CreateHorizontalSlider(parent, "Initial Angle", -180f, 180f, _pendulum != null ? _pendulum.InitialAngleDegrees : 45f, "°");
            _initialPhiSlider = CreateHorizontalSlider(parent, "Launch Direction Angle", -180f, 180f, _pendulum != null ? _pendulum.InitialPhiDegrees : 0f, "°");
            CreateCycleSelector(parent, "Direction of Movement", () => CycleDirection(), out _directionText);
            _angularVelocitySlider = CreateHorizontalSlider(parent, "Initial Angular Speed", 0f, 10f, _pendulum != null ? _pendulum.InitialAngularVelocity : 0f, "rad/s");
            _swingCountSlider = CreateHorizontalSlider(parent, "Number of Swings", 0f, 50f, _pendulum != null ? _pendulum.TargetSwingCount : 0f, "");
            _swingCountSlider.wholeNumbers = true;
            CreateSeparator(parent);
        }

        private void BuildPaintSection(Transform parent)
        {
            CreateSectionHeader(parent, "PAINT AND FLOW PROPERTIES");
            _paintVolumeSlider = CreateHorizontalSlider(parent, "Paint Volume", 0.1f, 10f, _bucket != null ? _bucket.InitialPaintVolume : 2f, "L");
            _viscositySlider = CreateHorizontalSlider(parent, "Viscosity", 0.1f, 10f, _bucket != null ? _bucket.Viscosity : 1f, "");
            _densitySlider = CreateHorizontalSlider(parent, "Density", 0.1f, 5f, _bucket != null ? _bucket.Density : 1f, "g/cm³");
            _nozzleRadiusSlider = CreateHorizontalSlider(parent, "Nozzle Radius", 0.001f, 0.05f, _bucket != null ? _bucket.NozzleRadius : 0.005f, "m");
            _dischargeSlider = CreateHorizontalSlider(parent, "Discharge Coeff.", 0.1f, 1f, _bucket != null ? _bucket.DischargeCoefficent : 0.7f, "");
            _paintLossSlider = CreateHorizontalSlider(parent, "Bucket Paint Loss", 0f, 0.5f, _bucket != null ? _bucket.PaintLossRate : 0.02f, "L/s");
            _absorptionSlider = CreateHorizontalSlider(parent, "Bucket Absorption", 0f, 0.1f, _bucket != null ? _bucket.AbsorptionRate : 0f, "L/s");
            CreateSeparator(parent);
        }

        private void BuildEnvironmentSection(Transform parent)
        {
            CreateSectionHeader(parent, "ENVIRONMENT");
            _temperatureSlider = CreateHorizontalSlider(parent, "Temperature", 0f, 50f, _environment != null ? _environment.Temperature : 20f, "°C");
            _humiditySlider = CreateHorizontalSlider(parent, "Humidity", 0f, 100f, _environment != null ? _environment.Humidity : 50f, "%");
            CreateSeparator(parent);
        }

        private void BuildCanvasSection(Transform parent)
        {
            CreateSectionHeader(parent, "CANVAS PROPERTIES");
            CreateCycleSelector(parent, "Canvas Surface", () => CycleCanvasSurface(), out _canvasSurfaceText);
            _canvasWidthSlider = CreateHorizontalSlider(parent, "Canvas Width", 0.5f, 10f, _canvas != null ? _canvas.CanvasWidthMeters : 3f, "m");
            _canvasHeightSlider = CreateHorizontalSlider(parent, "Canvas Height", 0.5f, 10f, _canvas != null ? _canvas.CanvasHeightMeters : 3f, "m");
            _canvasTiltSlider = CreateHorizontalSlider(parent, "Canvas Tilt", -60f, 60f, _canvas != null ? _canvas.CanvasTiltDegrees : 0f, "°");
            _canvasImpactSlider = CreateHorizontalSlider(parent, "Particle Impact Size", 0.1f, 3f, _canvas != null ? _canvas.ParticleImpactSize : 1f, "");
            CreateSeparator(parent);
        }

        private void BuildAppearanceSection(Transform parent)
        {
            CreateSectionHeader(parent, "APPEARANCE");

            var row = CreateRow(parent, "ColorRow", 60);
            CreateSmallText(row.transform, "Paint Color", TextColor, TextAnchor.MiddleLeft, 18, new Vector2(220, 35));

            var swatch = new GameObject("PaintColorSwatch", typeof(RectTransform));
            swatch.transform.SetParent(row.transform, false);
            var swatchLayout = swatch.AddComponent<LayoutElement>();
            swatchLayout.preferredWidth = 90;
            swatchLayout.preferredHeight = 42;
            _paintColorPreview = swatch.AddComponent<Image>();
            _paintColorPreview.color = _selectedPaintColor;
            var swatchButton = swatch.AddComponent<Button>();
            swatchButton.onClick.AddListener(CycleColor);

            _paintColorText = CreateSmallText(row.transform, "Red", AccentColor, TextAnchor.MiddleLeft, 18, new Vector2(140, 35)).GetComponent<Text>();
            CreateSeparator(parent);
        }

        private void BuildActionSection(Transform parent)
        {
            CreateSectionHeader(parent, "ACTIONS");

            var row = CreateRow(parent, "ActionRow", 60);
            CreateButton(row.transform, "Apply + Restart", ApplySettings);
            CreateButton(row.transform, "Reset", () => _simManager?.ResetSimulation());
            CreateButton(row.transform, "Start", () => _simManager?.StartSimulation());
            CreateButton(row.transform, "Pause", () => _simManager?.PauseSimulation());
        }

        public void ApplySettings()
        {
            CacheReferences();

            SaveValues();

            // Reset first to clear particles/canvas/history, then apply the new parameter values.
            _simManager?.ResetSimulation();

            if (_bucket != null)
            {
                _bucket.BucketWeightKg = _bucketWeightSlider.value;
                _bucket.BucketRadius = _bucketRadiusSlider.value;
                _bucket.MaterialType = (BucketMaterialType)_bucketMaterialIndex;
                _bucket.InitialPaintVolume = _paintVolumeSlider.value;
                _bucket.Viscosity = _viscositySlider.value;
                _bucket.Density = _densitySlider.value;
                _bucket.NozzleRadius = _nozzleRadiusSlider.value;
                _bucket.DischargeCoefficent = _dischargeSlider.value;
                _bucket.PaintLossRate = _paintLossSlider.value;
                _bucket.AbsorptionRate = _absorptionSlider.value;
                _bucket.PaintColors = CreateSolidGradient(_selectedPaintColor);
                _bucket.SyncPaintVolume();
            }

            if (_pendulum != null)
            {
                _pendulum.RopeLength = _ropeLengthSlider.value;
                _pendulum.RopeMaterial = (RopeType)_ropeTypeIndex;
                _pendulum.Gravity = _gravitySlider.value;
                _pendulum.AirResistanceCoefficient = _airResistanceSlider.value;
                _pendulum.FrictionCoefficient = _frictionSlider.value;
                _pendulum.InitialAngleDegrees = _initialAngleSlider.value;
                _pendulum.InitialPhiDegrees = _initialPhiSlider.value;
                _pendulum.DirectionOfMovement = _directionIndex == 0 ? MovementDirection.Clockwise : MovementDirection.CounterClockwise;
                _pendulum.InitialAngularVelocity = Mathf.Abs(_angularVelocitySlider.value);
                _pendulum.TargetSwingCount = Mathf.RoundToInt(_swingCountSlider.value);
                _pendulum.ApplyRopeTypePreset();
                _pendulum.ResetSimulation();
            }

            if (_environment != null)
            {
                _environment.Temperature = _temperatureSlider.value;
                _environment.Humidity = _humiditySlider.value;
            }

            if (_canvas != null)
            {
                _canvas.SurfaceType = (CanvasSurfaceType)_canvasSurfaceIndex;
                _canvas.CanvasWidthMeters = _canvasWidthSlider.value;
                _canvas.CanvasHeightMeters = _canvasHeightSlider.value;
                _canvas.CanvasTiltDegrees = _canvasTiltSlider.value;
                _canvas.ParticleImpactSize = _canvasImpactSlider.value;
                _canvas.ApplyCanvasSettings();
            }

            PlayerPrefs.Save();
            _simManager?.StartSimulation();
            Debug.Log("[SettingsPanel] Settings applied and simulation started.");
        }

        private void LoadSavedValues()
        {
            if (_bucketWeightSlider != null) _bucketWeightSlider.value = PlayerPrefs.GetFloat("Sim_BucketWeight", _bucket != null ? _bucket.BucketWeightKg : 1f);
            if (_bucketRadiusSlider != null) _bucketRadiusSlider.value = PlayerPrefs.GetFloat("Sim_BucketRadius", _bucket != null ? _bucket.BucketRadius : 0.15f);
            if (_ropeLengthSlider != null) _ropeLengthSlider.value = PlayerPrefs.GetFloat("Sim_RopeLength", _pendulum != null ? _pendulum.RopeLength : 5f);
            if (_gravitySlider != null) _gravitySlider.value = PlayerPrefs.GetFloat("Sim_Gravity", _pendulum != null ? _pendulum.Gravity : 9.81f);
            if (_airResistanceSlider != null) _airResistanceSlider.value = PlayerPrefs.GetFloat("Sim_AirResistance", _pendulum != null ? _pendulum.AirResistanceCoefficient : 0.05f);
            if (_frictionSlider != null) _frictionSlider.value = PlayerPrefs.GetFloat("Sim_Friction", _pendulum != null ? _pendulum.FrictionCoefficient : 0.02f);
            if (_initialAngleSlider != null) _initialAngleSlider.value = PlayerPrefs.GetFloat("Sim_InitialAngle", _pendulum != null ? _pendulum.InitialAngleDegrees : 45f);
            if (_initialPhiSlider != null) _initialPhiSlider.value = PlayerPrefs.GetFloat("Sim_InitialPhi", _pendulum != null ? _pendulum.InitialPhiDegrees : 0f);
            if (_angularVelocitySlider != null) _angularVelocitySlider.value = PlayerPrefs.GetFloat("Sim_AngularVelocity", _pendulum != null ? _pendulum.InitialAngularVelocity : 0f);
            if (_swingCountSlider != null) _swingCountSlider.value = PlayerPrefs.GetInt("Sim_TargetSwings", _pendulum != null ? _pendulum.TargetSwingCount : 0);

            if (_paintVolumeSlider != null) _paintVolumeSlider.value = PlayerPrefs.GetFloat("Sim_PaintVolume", _bucket != null ? _bucket.InitialPaintVolume : 2f);
            if (_viscositySlider != null) _viscositySlider.value = PlayerPrefs.GetFloat("Sim_Viscosity", _bucket != null ? _bucket.Viscosity : 1f);
            if (_densitySlider != null) _densitySlider.value = PlayerPrefs.GetFloat("Sim_Density", _bucket != null ? _bucket.Density : 1f);
            if (_nozzleRadiusSlider != null) _nozzleRadiusSlider.value = PlayerPrefs.GetFloat("Sim_NozzleRadius", _bucket != null ? _bucket.NozzleRadius : 0.005f);
            if (_dischargeSlider != null) _dischargeSlider.value = PlayerPrefs.GetFloat("Sim_Discharge", _bucket != null ? _bucket.DischargeCoefficent : 0.7f);
            if (_paintLossSlider != null) _paintLossSlider.value = PlayerPrefs.GetFloat("Sim_PaintLoss", _bucket != null ? _bucket.PaintLossRate : 0.02f);
            if (_absorptionSlider != null) _absorptionSlider.value = PlayerPrefs.GetFloat("Sim_Absorption", _bucket != null ? _bucket.AbsorptionRate : 0f);

            if (_temperatureSlider != null) _temperatureSlider.value = PlayerPrefs.GetFloat("Sim_Temperature", _environment != null ? _environment.Temperature : 20f);
            if (_humiditySlider != null) _humiditySlider.value = PlayerPrefs.GetFloat("Sim_Humidity", _environment != null ? _environment.Humidity : 50f);

            if (_canvasWidthSlider != null) _canvasWidthSlider.value = PlayerPrefs.GetFloat("Sim_CanvasWidth", _canvas != null ? _canvas.CanvasWidthMeters : 3f);
            if (_canvasHeightSlider != null) _canvasHeightSlider.value = PlayerPrefs.GetFloat("Sim_CanvasHeight", _canvas != null ? _canvas.CanvasHeightMeters : 3f);
            if (_canvasTiltSlider != null) _canvasTiltSlider.value = PlayerPrefs.GetFloat("Sim_CanvasTilt", _canvas != null ? _canvas.CanvasTiltDegrees : 0f);
            if (_canvasImpactSlider != null) _canvasImpactSlider.value = PlayerPrefs.GetFloat("Sim_CanvasImpact", _canvas != null ? _canvas.ParticleImpactSize : 1f);

            _ropeTypeIndex = PlayerPrefs.GetInt("Sim_RopeType", _pendulum != null ? (int)_pendulum.RopeMaterial : 0);
            _directionIndex = PlayerPrefs.GetInt("Sim_Direction", _pendulum != null && _pendulum.DirectionOfMovement == MovementDirection.CounterClockwise ? 1 : 0);
            _bucketMaterialIndex = PlayerPrefs.GetInt("Sim_BucketMaterial", _bucket != null ? (int)_bucket.MaterialType : 0);
            _canvasSurfaceIndex = PlayerPrefs.GetInt("Sim_CanvasSurface", _canvas != null ? (int)_canvas.SurfaceType : 0);
        }

        private void SaveValues()
        {
            PlayerPrefs.SetFloat("Sim_BucketWeight", _bucketWeightSlider.value);
            PlayerPrefs.SetFloat("Sim_BucketRadius", _bucketRadiusSlider.value);
            PlayerPrefs.SetFloat("Sim_RopeLength", _ropeLengthSlider.value);
            PlayerPrefs.SetFloat("Sim_Gravity", _gravitySlider.value);
            PlayerPrefs.SetFloat("Sim_AirResistance", _airResistanceSlider.value);
            PlayerPrefs.SetFloat("Sim_Friction", _frictionSlider.value);
            PlayerPrefs.SetFloat("Sim_InitialAngle", _initialAngleSlider.value);
            PlayerPrefs.SetFloat("Sim_InitialPhi", _initialPhiSlider.value);
            PlayerPrefs.SetFloat("Sim_AngularVelocity", _angularVelocitySlider.value);
            PlayerPrefs.SetInt("Sim_TargetSwings", Mathf.RoundToInt(_swingCountSlider.value));

            PlayerPrefs.SetFloat("Sim_PaintVolume", _paintVolumeSlider.value);
            PlayerPrefs.SetFloat("Sim_Viscosity", _viscositySlider.value);
            PlayerPrefs.SetFloat("Sim_Density", _densitySlider.value);
            PlayerPrefs.SetFloat("Sim_NozzleRadius", _nozzleRadiusSlider.value);
            PlayerPrefs.SetFloat("Sim_Discharge", _dischargeSlider.value);
            PlayerPrefs.SetFloat("Sim_PaintLoss", _paintLossSlider.value);
            PlayerPrefs.SetFloat("Sim_Absorption", _absorptionSlider.value);

            PlayerPrefs.SetFloat("Sim_Temperature", _temperatureSlider.value);
            PlayerPrefs.SetFloat("Sim_Humidity", _humiditySlider.value);

            PlayerPrefs.SetFloat("Sim_CanvasWidth", _canvasWidthSlider.value);
            PlayerPrefs.SetFloat("Sim_CanvasHeight", _canvasHeightSlider.value);
            PlayerPrefs.SetFloat("Sim_CanvasTilt", _canvasTiltSlider.value);
            PlayerPrefs.SetFloat("Sim_CanvasImpact", _canvasImpactSlider.value);

            PlayerPrefs.SetInt("Sim_RopeType", _ropeTypeIndex);
            PlayerPrefs.SetInt("Sim_Direction", _directionIndex);
            PlayerPrefs.SetInt("Sim_BucketMaterial", _bucketMaterialIndex);
            PlayerPrefs.SetInt("Sim_CanvasSurface", _canvasSurfaceIndex);
        }

        private void CacheReferences()
        {
            _simManager = FindAnyObjectByType<SimulationManager>();

            if (_simManager != null && _simManager.BucketObject != null)
            {
                _pendulum = _simManager.BucketObject.GetComponent<PendulumSimulator>();
                _bucket = _simManager.BucketObject.GetComponent<BucketController>();
            }
            else
            {
                _bucket = FindAnyObjectByType<BucketController>();
                if (_bucket != null)
                    _pendulum = _bucket.GetComponent<PendulumSimulator>();
            }

            _environment = FindAnyObjectByType<EnvironmentController>();
            _canvas = FindAnyObjectByType<CanvasController>();
        }

        private void CycleRopeType()
        {
            _ropeTypeIndex = (_ropeTypeIndex + 1) % Enum.GetValues(typeof(RopeType)).Length;
            RefreshSelectorTexts();
        }

        private void CycleDirection()
        {
            _directionIndex = (_directionIndex + 1) % 2;
            RefreshSelectorTexts();
        }

        private void CycleBucketMaterial()
        {
            _bucketMaterialIndex = (_bucketMaterialIndex + 1) % Enum.GetValues(typeof(BucketMaterialType)).Length;
            RefreshSelectorTexts();
        }

        private void CycleCanvasSurface()
        {
            _canvasSurfaceIndex = (_canvasSurfaceIndex + 1) % Enum.GetValues(typeof(CanvasSurfaceType)).Length;
            RefreshSelectorTexts();
        }

        private void RefreshSelectorTexts()
        {
            if (_ropeTypeText != null)
            {
                RopeType type = (RopeType)_ropeTypeIndex;
                _ropeTypeText.text = type + " — " + RopePhysicsPreset.GetDescription(type);
            }

            if (_directionText != null)
                _directionText.text = _directionIndex == 0 ? "Clockwise" : "CounterClockwise";

            if (_bucketMaterialText != null)
                _bucketMaterialText.text = ((BucketMaterialType)_bucketMaterialIndex).ToString();

            if (_canvasSurfaceText != null)
                _canvasSurfaceText.text = ((CanvasSurfaceType)_canvasSurfaceIndex).ToString();
        }

        private void CycleColor()
        {
            string[] names = { "Red", "Blue", "Green", "Yellow", "Cyan", "Magenta" };
            Color[] colors =
            {
                new Color(1f, 0.2f, 0.2f),
                new Color(0.2f, 0.6f, 1f),
                new Color(0.2f, 1f, 0.4f),
                new Color(1f, 0.8f, 0f),
                new Color(0f, 1f, 0.8f),
                new Color(1f, 0.2f, 0.8f)
            };

            int index = 0;
            for (int i = 0; i < colors.Length; i++)
            {
                if (ApproximatelySameColor(colors[i], _selectedPaintColor))
                {
                    index = i;
                    break;
                }
            }

            index = (index + 1) % colors.Length;
            _selectedPaintColor = colors[index];

            if (_paintColorPreview != null)
                _paintColorPreview.color = _selectedPaintColor;
            if (_paintColorText != null)
                _paintColorText.text = names[index];
        }

        private Gradient CreateSolidGradient(Color color)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return gradient;
        }

        private Slider CreateHorizontalSlider(Transform parent, string label, float min, float max, float current, string unit)
        {
            var container = CreateRow(parent, "SliderContainer_" + label, 50);

            CreateSmallText(container.transform, label, TextColor, TextAnchor.MiddleLeft, 18, new Vector2(230, 35));

            var sliderArea = new GameObject("SliderArea", typeof(RectTransform));
            sliderArea.transform.SetParent(container.transform, false);
            var sliderAreaLayout = sliderArea.AddComponent<LayoutElement>();
            sliderAreaLayout.flexibleWidth = 1f;
            sliderAreaLayout.preferredHeight = 40;
            var sliderAreaLayoutGroup = sliderArea.AddComponent<HorizontalLayoutGroup>();
            sliderAreaLayoutGroup.spacing = 10;
            sliderAreaLayoutGroup.childForceExpandWidth = true;
            sliderAreaLayoutGroup.childControlHeight = true;

            var sliderGo = new GameObject("Slider", typeof(RectTransform));
            sliderGo.transform.SetParent(sliderArea.transform, false);
            var sliderLayout = sliderGo.AddComponent<LayoutElement>();
            sliderLayout.flexibleWidth = 1f;
            sliderLayout.preferredHeight = 25;

            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = current;

            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(sliderGo.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.3f);
            bgRt.anchorMax = new Vector2(1, 0.7f);
            bgRt.sizeDelta = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.3f);

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0, 0.3f);
            fillAreaRt.anchorMax = new Vector2(1, 0.7f);
            fillAreaRt.sizeDelta = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.sizeDelta = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = AccentColor;

            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(sliderGo.transform, false);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0, 0);
            handleRt.anchorMax = new Vector2(0, 1);
            handleRt.sizeDelta = new Vector2(25, 0);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = SecondaryColor;

            slider.targetGraphic = handleImg;
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;

            var valueText = CreateSmallText(sliderArea.transform, FormatValue(current, max, unit), AccentColor, TextAnchor.MiddleRight, 18, new Vector2(130, 35)).GetComponent<Text>();
            slider.onValueChanged.AddListener(val => valueText.text = FormatValue(val, max, unit));

            return slider;
        }

        private GameObject CreateRow(Transform parent, string name, float height)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var layoutElement = row.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;
            layoutElement.flexibleWidth = 1f;

            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 15;
            rowLayout.padding = new RectOffset(0, 0, 5, 5);
            rowLayout.childForceExpandWidth = false;
            rowLayout.childControlHeight = true;

            return row;
        }

        private void CreateCycleSelector(Transform parent, string label, Action onClicked, out Text valueText)
        {
            var row = CreateRow(parent, "Selector_" + label, 55);
            CreateSmallText(row.transform, label, TextColor, TextAnchor.MiddleLeft, 18, new Vector2(230, 35));

            var button = CreateButton(row.transform, "", onClicked);
            var buttonLayout = button.GetComponent<LayoutElement>();
            buttonLayout.flexibleWidth = 1f;
            buttonLayout.preferredWidth = 420f;

            valueText = button.GetComponentInChildren<Text>();
            valueText.alignment = TextAnchor.MiddleCenter;
        }

        private Button CreateButton(Transform parent, string text, Action onClicked)
        {
            var buttonGo = new GameObject("Button_" + text, typeof(RectTransform));
            buttonGo.transform.SetParent(parent, false);

            var layout = buttonGo.AddComponent<LayoutElement>();
            layout.preferredHeight = 45;
            layout.preferredWidth = 170;

            var image = buttonGo.AddComponent<Image>();
            image.color = UnselectedTabColor;

            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClicked?.Invoke());

            CreateSmallText(buttonGo.transform, text, Color.white, TextAnchor.MiddleCenter, 16, Vector2.zero, true);
            return button;
        }

        private GameObject CreateSmallText(Transform parent, string text, Color color, TextAnchor alignment, int fontSize, Vector2 size, bool stretch = false)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();

            if (stretch)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            else
            {
                rt.sizeDelta = size;
            }

            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.font = DefaultFont;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = alignment;
            txt.raycastTarget = false;
            return go;
        }

        private void CreateSectionHeader(Transform parent, string title)
        {
            var go = new GameObject("Header_" + title, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 45;
            layout.flexibleWidth = 1f;

            var txt = go.AddComponent<Text>();
            txt.text = title;
            txt.font = DefaultFont;
            txt.fontSize = 24;
            txt.fontStyle = FontStyle.Bold;
            txt.color = AccentColor;
            txt.alignment = TextAnchor.MiddleLeft;
        }

        private void CreateSeparator(Transform parent)
        {
            var go = new GameObject("Separator", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 2;
            layout.flexibleWidth = 1f;

            var img = go.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.08f);
        }

        private string FormatValue(float value, float max, string unit)
        {
            string format = max <= 0.1f ? "0.000" : max <= 3f ? "0.00" : "0.0";
            return value.ToString(format) + (string.IsNullOrEmpty(unit) ? string.Empty : " " + unit);
        }

        private bool ApproximatelySameColor(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
        }
    }
}
