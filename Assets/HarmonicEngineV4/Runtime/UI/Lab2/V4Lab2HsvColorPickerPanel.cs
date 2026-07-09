using System;
using UnityEngine;
using UnityEngine.UI;

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Compact runtime HSV color picker popup for layer paint colors.</summary>
    public sealed class V4Lab2HsvColorPickerPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Slider hueSlider;
        [SerializeField] private Slider saturationSlider;
        [SerializeField] private Slider valueSlider;
        [SerializeField] private Image previewImage;
        [SerializeField] private Button pickButton;
        [SerializeField] private Button closeButton;

        private Action<Color> _onColorChanged;
        private bool _suppressEvents;
        private Transform _popupOriginalParent;
        private Vector2 _popupOriginalAnchoredPosition;
        private Vector2 _popupOriginalAnchorMin;
        private Vector2 _popupOriginalAnchorMax;
        private Vector2 _popupOriginalPivot;
        private Vector2 _popupOriginalSizeDelta;
        private Canvas _popupOverlayCanvas;

        public static V4Lab2HsvColorPickerPanel Attach(Transform row, Image preview, Action<Color> onColorChanged)
        {
            var existing = row.GetComponentInChildren<V4Lab2HsvColorPickerPanel>(true);
            if (existing != null)
            {
                existing.Bind(preview, onColorChanged);
                return existing;
            }

            var host = new GameObject("CustomColorPicker", typeof(RectTransform));
            host.transform.SetParent(row, false);
            var panel = host.AddComponent<V4Lab2HsvColorPickerPanel>();
            panel.BuildUi(row, preview, onColorChanged);
            return panel;
        }

        public void Bind(Image preview, Action<Color> onColorChanged)
        {
            previewImage = preview;
            _onColorChanged = onColorChanged;
        }

        public void SetColor(Color color, bool notify = false)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            _suppressEvents = true;
            if (hueSlider != null)
            {
                hueSlider.SetValueWithoutNotify(h);
            }

            if (saturationSlider != null)
            {
                saturationSlider.SetValueWithoutNotify(s);
            }

            if (valueSlider != null)
            {
                valueSlider.SetValueWithoutNotify(v);
            }

            _suppressEvents = false;
            UpdatePreview(notify);
        }

        public void SetOpen(bool open)
        {
            if (panelRoot == null)
            {
                return;
            }

            if (open)
            {
                PromotePopupToOverlay();
            }
            else
            {
                RestorePopupParent();
            }

            panelRoot.SetActive(open);
        }

        private void PromotePopupToOverlay()
        {
            if (panelRoot == null)
            {
                return;
            }

            Transform popupTransform = panelRoot.transform;
            if (_popupOriginalParent == null)
            {
                _popupOriginalParent = popupTransform.parent;
                RectTransform popupRect = popupTransform as RectTransform;
                if (popupRect != null)
                {
                    _popupOriginalAnchoredPosition = popupRect.anchoredPosition;
                    _popupOriginalAnchorMin = popupRect.anchorMin;
                    _popupOriginalAnchorMax = popupRect.anchorMax;
                    _popupOriginalPivot = popupRect.pivot;
                    _popupOriginalSizeDelta = popupRect.sizeDelta;
                }
            }

            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                popupTransform.SetParent(rootCanvas.transform, true);
                popupTransform.SetAsLastSibling();
            }

            EnsurePopupOverlayCanvas();
        }

        private void RestorePopupParent()
        {
            if (panelRoot == null || _popupOriginalParent == null)
            {
                return;
            }

            Transform popupTransform = panelRoot.transform;
            popupTransform.SetParent(_popupOriginalParent, false);
            RectTransform popupRect = popupTransform as RectTransform;
            if (popupRect != null)
            {
                popupRect.anchorMin = _popupOriginalAnchorMin;
                popupRect.anchorMax = _popupOriginalAnchorMax;
                popupRect.pivot = _popupOriginalPivot;
                popupRect.anchoredPosition = _popupOriginalAnchoredPosition;
                popupRect.sizeDelta = _popupOriginalSizeDelta;
            }
        }

        private void EnsurePopupOverlayCanvas()
        {
            if (panelRoot == null)
            {
                return;
            }

            if (_popupOverlayCanvas == null)
            {
                _popupOverlayCanvas = panelRoot.GetComponent<Canvas>();
                if (_popupOverlayCanvas == null)
                {
                    _popupOverlayCanvas = panelRoot.AddComponent<Canvas>();
                }
            }

            _popupOverlayCanvas.overrideSorting = true;
            _popupOverlayCanvas.sortingOrder = 320;

            if (panelRoot.GetComponent<GraphicRaycaster>() == null)
            {
                panelRoot.AddComponent<GraphicRaycaster>();
            }
        }

        private void BuildUi(Transform row, Image preview, Action<Color> onColorChanged)
        {
            previewImage = preview;
            _onColorChanged = onColorChanged;

            pickButton = CreateButton(row, "PickColorButton", "Pick", new Vector2(154f, 0f), new Vector2(64f, 32f));

            panelRoot = new GameObject("ColorPickerPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelRoot.transform.SetParent(row, false);
            RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(40f, -44f);
            panelRect.sizeDelta = new Vector2(300f, 148f);
            panelRoot.GetComponent<Image>().color = V4Lab2UITheme.CardColor;
            V4Lab2UITheme.CreateChipBorder(panelRoot.transform);

            float y = -12f;
            hueSlider = CreatePopupSlider(panelRoot.transform, "Hue", 0f, 1f, 0f, ref y);
            saturationSlider = CreatePopupSlider(panelRoot.transform, "Saturation", 0f, 1f, 1f, ref y);
            valueSlider = CreatePopupSlider(panelRoot.transform, "Value", 0f, 1f, 1f, ref y);
            closeButton = CreateButton(panelRoot.transform, "CloseColorPickerButton", "Done", new Vector2(0f, y - 8f), new Vector2(120f, 28f));

            pickButton.onClick.AddListener(() => SetOpen(true));
            closeButton.onClick.AddListener(() => SetOpen(false));
            hueSlider.onValueChanged.AddListener(_ => UpdatePreview(notify: true));
            saturationSlider.onValueChanged.AddListener(_ => UpdatePreview(notify: true));
            valueSlider.onValueChanged.AddListener(_ => UpdatePreview(notify: true));
            panelRoot.SetActive(false);
        }

        private void UpdatePreview(bool notify)
        {
            if (hueSlider == null || saturationSlider == null || valueSlider == null)
            {
                return;
            }

            Color color = Color.HSVToRGB(hueSlider.value, saturationSlider.value, valueSlider.value);
            if (previewImage != null)
            {
                previewImage.color = color;
            }

            if (notify && !_suppressEvents)
            {
                _onColorChanged?.Invoke(color);
            }
        }

        private static Slider CreatePopupSlider(Transform parent, string label, float min, float max, float value, ref float y)
        {
            y -= 36f;
            CreatePopupLabel(parent, label, new Vector2(-110f, y + 6f));
            return CreatePopupSliderControl(parent, label + "Slider", min, max, value, new Vector2(20f, y));
        }

        private static void CreatePopupLabel(Transform parent, string text, Vector2 pos)
        {
            var go = new GameObject("Label_" + text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(120f, 20f);
            Text label = go.GetComponent<Text>();
            label.text = text;
            V4Lab2UITheme.ApplyBodyText(label);
        }

        private static Slider CreatePopupSliderControl(Transform parent, string name, float min, float max, float value, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(180f, 18f);

            var background = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background.transform.SetParent(go.transform, false);
            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = V4Lab2UITheme.SliderTrack;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(4f, 0f);
            fillAreaRect.offsetMax = new Vector2(-4f, 0f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = V4Lab2UITheme.SliderFill;

            var handleSlideArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleSlideArea.transform.SetParent(go.transform, false);
            RectTransform handleAreaRect = handleSlideArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(8f, 0f);
            handleAreaRect.offsetMax = new Vector2(-8f, 0f);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(handleSlideArea.transform, false);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(14f, 14f);
            handle.GetComponent<Image>().color = V4Lab2UITheme.AccentColor;

            Slider slider = go.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            V4Lab2UITheme.ApplySlider(slider);
            return slider;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            Button button = go.GetComponent<Button>();
            V4Lab2UITheme.ApplyButton(button, false);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            Text text = textGo.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            V4Lab2UITheme.ApplyBodyText(text);
            return button;
        }
    }
}
