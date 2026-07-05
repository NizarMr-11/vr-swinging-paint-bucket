using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using SwingingPaintBucket.Pendulum;
using SwingingPaintBucket.Bucket;
using SwingingPaintBucket.Simulation;

namespace SwingingPaintBucket.Interface.UI
{
    public class QuickSettings : MonoBehaviour
    {
        [Header("References (set by SimulationUI)")]
        public PendulumSimulator Pendulum;
        public BucketController Bucket;
        public EnvironmentController Environment;
        public SimulationManager SimulationManager;

        [Header("Events")]
        public UnityEvent OnApply = new UnityEvent();
        public UnityEvent OnCancel = new UnityEvent();

        // Design constants for slim, modern look
        private const float ROW_HEIGHT = 14f;
        private const float LABEL_WIDTH = 35f;
        private const float SLIDER_WIDTH = 180f;
        private const float SLIDER_HEIGHT = 10f;
        private const float VALUE_WIDTH = 42f;
        private const float SPACING = 4f;
        private const int LABEL_FONT_SIZE = 10;
        private const int VALUE_FONT_SIZE = 9;

        private Slider _massSlider, _ropeSlider, _gravitySlider, _windSlider, _angleSlider;
        private Text _massValue, _ropeValue, _gravityValue, _windValue, _angleValue;
        private Color _paintColor = Color.red;
        private Image _colorPreview;
        private int _colorIndex;
        private readonly Color[] _palette = {
            new Color(1f, 0.2f, 0.2f),
            new Color(0.2f, 0.6f, 1f),
            new Color(0.2f, 1f, 0.4f),
            new Color(1f, 0.8f, 0f),
            new Color(0f, 1f, 0.8f),
            new Color(1f, 0.2f, 0.8f)
        };

        public void BuildUI(Transform parent)
        {
            // Title - compact
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(parent, false);
            var titleLayout = titleGo.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 16;
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "⚙ CONTROLS";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 11;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(1f, 0.8f, 0f);
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.raycastTarget = false;

            _massSlider    = CreateSlimSliderRow(parent, "Mass",   0.1f,  50f,  1f,    "kg",     out _massValue);
            _ropeSlider    = CreateSlimSliderRow(parent, "Rope",   0.5f,  20f,  5f,    "m",      out _ropeValue);
            _gravitySlider = CreateSlimSliderRow(parent, "Gravity",0f,    20f,  9.81f, "m/s²",   out _gravityValue);
            _windSlider    = CreateSlimSliderRow(parent, "Wind",   0f,    20f,  0f,    "",       out _windValue);
            _angleSlider   = CreateSlimSliderRow(parent, "Angle", -180f, 180f,  45f,   "°",      out _angleValue);

            CreateColorPickerRow(parent);
            CreateButtonRow(parent);

            SyncSlidersToSaved();
        }

        private Slider CreateSlimSliderRow(Transform parent, string label, float min, float max,
            float defaultVal, string unit, out Text valueText)
        {
            // Container
            var container = new GameObject("SliderRow_" + label, typeof(RectTransform));
            container.transform.SetParent(parent, false);
            var layoutEl = container.AddComponent<LayoutElement>();
            layoutEl.preferredHeight = ROW_HEIGHT;
            layoutEl.flexibleWidth = 1f;

            var hLayout = container.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = SPACING;
            hLayout.childForceExpandWidth = false;
            hLayout.childControlHeight = true;
            hLayout.childControlWidth = false;
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.padding = new RectOffset(0, 0, 0, 0);

            // Label - compact
            var lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(container.transform, false);
            var lblLayout = lblGo.AddComponent<LayoutElement>();
            lblLayout.preferredWidth = LABEL_WIDTH;
            lblLayout.preferredHeight = ROW_HEIGHT;
            var lblText = lblGo.AddComponent<Text>();
            lblText.text = label;
            lblText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lblText.fontSize = LABEL_FONT_SIZE;
            lblText.fontStyle = FontStyle.Bold;
            lblText.color = new Color(0.65f, 0.7f, 0.75f);
            lblText.alignment = TextAnchor.MiddleLeft;
            lblText.raycastTarget = false;

            // Slider container
            var barGo = new GameObject("SlimBar", typeof(RectTransform));
            barGo.transform.SetParent(container.transform, false);
            var barLayout = barGo.AddComponent<LayoutElement>();
            barLayout.preferredWidth = SLIDER_WIDTH;
            barLayout.preferredHeight = SLIDER_HEIGHT;

            var barRt = barGo.GetComponent<RectTransform>();
            barRt.sizeDelta = new Vector2(SLIDER_WIDTH, SLIDER_HEIGHT);

            var slider = barGo.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultVal;

            // Background track - thinner
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(barGo.transform, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.1f);
            bgRt.anchorMax = new Vector2(1, 0.9f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.12f, 0.18f, 0.8f);

            // Fill area - thinner
            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(barGo.transform, false);
            var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0, 0.1f);
            fillAreaRt.anchorMax = new Vector2(1, 0.9f);
            fillAreaRt.offsetMin = Vector2.zero;
            fillAreaRt.offsetMax = Vector2.zero;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.25f, 0.75f, 0.95f, 0.9f);

            // Handle - completely hidden (1x1 transparent)
            var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaGo.transform.SetParent(barGo.transform, false);
            var handleAreaRt = handleAreaGo.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.sizeDelta = Vector2.zero;

            var handleGo = new GameObject("Handle", typeof(RectTransform));
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0, 0.5f);
            handleRt.anchorMax = new Vector2(0, 0.5f);
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = Vector2.one; // 1x1 pixel
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = Color.clear; // شفاف تماماً

            slider.targetGraphic = fillImg;
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;

            // Value display - compact
            var valGo = new GameObject("Value", typeof(RectTransform));
            valGo.transform.SetParent(container.transform, false);
            var valLayout = valGo.AddComponent<LayoutElement>();
            valLayout.preferredWidth = VALUE_WIDTH;
            valLayout.preferredHeight = ROW_HEIGHT;
            valueText = valGo.AddComponent<Text>();
            valueText.text = defaultVal.ToString("F1") + " " + unit;
            valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueText.fontSize = VALUE_FONT_SIZE;
            valueText.fontStyle = FontStyle.Bold;
            valueText.color = new Color(0.25f, 0.75f, 0.95f);
            valueText.alignment = TextAnchor.MiddleRight;
            valueText.raycastTarget = false;

            // Fix: Store in local variable before lambda
            Text capturedText = valueText;
            string capturedUnit = unit;
            slider.onValueChanged.AddListener(val => capturedText.text = val.ToString("F1") + " " + capturedUnit);

            return slider;
        }

        private void CreateColorPickerRow(Transform parent)
        {
            var row = new GameObject("ColorRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            row.AddComponent<LayoutElement>().preferredHeight = ROW_HEIGHT + 4;
            var hLayout = row.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 3;
            hLayout.padding = new RectOffset(0, 0, 0, 0);
            hLayout.childForceExpandWidth = false;
            hLayout.childAlignment = TextAnchor.MiddleCenter;

            var lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(row.transform, false);
            var lblLayout = lblGo.AddComponent<LayoutElement>();
            lblLayout.preferredWidth = LABEL_WIDTH;
            lblLayout.preferredHeight = ROW_HEIGHT;
            var lblText = lblGo.AddComponent<Text>();
            lblText.text = "Color";
            lblText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lblText.fontSize = LABEL_FONT_SIZE;
            lblText.fontStyle = FontStyle.Bold;
            lblText.color = new Color(0.65f, 0.7f, 0.75f);
            lblText.alignment = TextAnchor.MiddleLeft;

            // Add spacer
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(row.transform, false);
            spacer.AddComponent<LayoutElement>().preferredWidth = SLIDER_WIDTH + SPACING;

            _colorIndex = PlayerPrefs.GetInt("Hud_ColorIndex", 0);
            _paintColor = _palette[_colorIndex];

            Image previewRef = null;

            for (int i = 0; i < _palette.Length; i++)
            {
                var c = _palette[i];
                var swatch = new GameObject("Swatch", typeof(RectTransform));
                swatch.transform.SetParent(row.transform, false);
                var swLayout = swatch.AddComponent<LayoutElement>();
                swLayout.preferredWidth = 14;
                swLayout.preferredHeight = 14;
                var swImg = swatch.AddComponent<Image>();
                swImg.color = c;
                var btn = swatch.AddComponent<Button>();
                int idx = i;
                btn.onClick.AddListener(() => {
                    _paintColor = _palette[idx];
                    _colorIndex = idx;
                    if (previewRef != null) previewRef.color = _palette[idx];
                });
            }

            var preview = new GameObject("Preview", typeof(RectTransform));
            preview.transform.SetParent(row.transform, false);
            var prevLayout = preview.AddComponent<LayoutElement>();
            prevLayout.preferredWidth = 16;
            prevLayout.preferredHeight = 16;
            _colorPreview = preview.AddComponent<Image>();
            _colorPreview.color = _paintColor;
            previewRef = _colorPreview;
            var outline = preview.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(1, 1);
        }

        private void CreateButtonRow(Transform parent)
        {
            var row = new GameObject("BtnRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            row.AddComponent<LayoutElement>().preferredHeight = 16;
            var hLayout = row.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 6;
            hLayout.childForceExpandWidth = true;
            hLayout.childControlHeight = true;
            hLayout.padding = new RectOffset(0, 0, 0, 0);

            // Apply button - compact
            var applyGo = new GameObject("ApplyBtn", typeof(RectTransform));
            applyGo.transform.SetParent(row.transform, false);
            var applyImg = applyGo.AddComponent<Image>();
            applyImg.color = new Color(0.1f, 0.65f, 0.25f);
            var applyBtn = applyGo.AddComponent<Button>();
            var applyColors = applyBtn.colors;
            applyColors.normalColor = new Color(0.1f, 0.65f, 0.25f);
            applyColors.highlightedColor = new Color(0.15f, 0.75f, 0.35f);
            applyColors.pressedColor = new Color(0.05f, 0.45f, 0.15f);
            applyBtn.colors = applyColors;
            var applyTextGo = new GameObject("Text", typeof(RectTransform));
            applyTextGo.transform.SetParent(applyGo.transform, false);
            var rt = applyTextGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            var applyText = applyTextGo.AddComponent<Text>();
            applyText.text = "✓ APPLY";
            applyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            applyText.fontSize = 10;
            applyText.fontStyle = FontStyle.Bold;
            applyText.color = Color.white;
            applyText.alignment = TextAnchor.MiddleCenter;
            applyBtn.onClick.AddListener(() => {
                ApplyParameters();
                OnApply.Invoke();
            });

            // Cancel button - compact
            var cancelGo = new GameObject("CancelBtn", typeof(RectTransform));
            cancelGo.transform.SetParent(row.transform, false);
            var cancelImg = cancelGo.AddComponent<Image>();
            cancelImg.color = new Color(0.5f, 0.2f, 0.2f);
            var cancelBtn = cancelGo.AddComponent<Button>();
            var cancelColors = cancelBtn.colors;
            cancelColors.normalColor = new Color(0.5f, 0.2f, 0.2f);
            cancelColors.highlightedColor = new Color(0.55f, 0.25f, 0.25f);
            cancelColors.pressedColor = new Color(0.35f, 0.1f, 0.1f);
            cancelBtn.colors = cancelColors;
            var cancelTextGo = new GameObject("Text", typeof(RectTransform));
            cancelTextGo.transform.SetParent(cancelGo.transform, false);
            var rt2 = cancelTextGo.GetComponent<RectTransform>();
            rt2.anchorMin = Vector2.zero; rt2.anchorMax = Vector2.one; rt2.sizeDelta = Vector2.zero;
            var cancelText = cancelTextGo.AddComponent<Text>();
            cancelText.text = "✕ CANCEL";
            cancelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cancelText.fontSize = 10;
            cancelText.fontStyle = FontStyle.Bold;
            cancelText.color = Color.white;
            cancelText.alignment = TextAnchor.MiddleCenter;
            cancelBtn.onClick.AddListener(() => {
                RevertSliders();
                OnCancel.Invoke();
            });
        }

        private void ApplyParameters()
        {
            if (Pendulum != null)
            {
                Pendulum.Mass = _massSlider.value;
                Pendulum.RopeLength = _ropeSlider.value;
                Pendulum.Gravity = _gravitySlider.value;
                Pendulum.InitialAngleDegrees = _angleSlider.value;
                PlayerPrefs.SetFloat("Sim_Mass", Pendulum.Mass);
                PlayerPrefs.SetFloat("Sim_RopeLength", Pendulum.RopeLength);
                PlayerPrefs.SetFloat("Sim_Gravity", Pendulum.Gravity);
                PlayerPrefs.SetFloat("Sim_InitialAngle", Pendulum.InitialAngleDegrees);
            }
            if (Environment != null)
            {
                float w = _windSlider.value;
                Environment.WindForce = new Vector3(w, 0f, 0f);
                PlayerPrefs.SetFloat("Sim_WindForce", w);
            }
            if (Bucket != null)
            {
                Bucket.PaintColors = CreateSolidGradient(_paintColor);
                PlayerPrefs.SetInt("Hud_ColorIndex", _colorIndex);
            }
            PlayerPrefs.Save();
        }

        public void RevertSliders()
        {
            if (_massSlider)    _massSlider.value    = PlayerPrefs.GetFloat("Sim_Mass", Pendulum?.Mass ?? 1f);
            if (_ropeSlider)    _ropeSlider.value    = PlayerPrefs.GetFloat("Sim_RopeLength", Pendulum?.RopeLength ?? 5f);
            if (_gravitySlider) _gravitySlider.value = PlayerPrefs.GetFloat("Sim_Gravity", Pendulum?.Gravity ?? 9.81f);
            if (_windSlider)    _windSlider.value    = PlayerPrefs.GetFloat("Sim_WindForce", Environment != null ? Environment.WindForce.magnitude : 0f);
            if (_angleSlider)   _angleSlider.value   = PlayerPrefs.GetFloat("Sim_InitialAngle", Pendulum?.InitialAngleDegrees ?? 45f);
        }

        public void SyncSlidersToSaved()
        {
            if (_massSlider)    _massSlider.value    = PlayerPrefs.GetFloat("Sim_Mass", Pendulum?.Mass ?? 1f);
            if (_ropeSlider)    _ropeSlider.value    = PlayerPrefs.GetFloat("Sim_RopeLength", Pendulum?.RopeLength ?? 5f);
            if (_gravitySlider) _gravitySlider.value = PlayerPrefs.GetFloat("Sim_Gravity", Pendulum?.Gravity ?? 9.81f);
            if (_windSlider)    _windSlider.value    = PlayerPrefs.GetFloat("Sim_WindForce", Environment != null ? Environment.WindForce.magnitude : 0f);
            if (_angleSlider)   _angleSlider.value   = PlayerPrefs.GetFloat("Sim_InitialAngle", Pendulum?.InitialAngleDegrees ?? 45f);
        }

        private Gradient CreateSolidGradient(Color color)
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return g;
        }
    }
}