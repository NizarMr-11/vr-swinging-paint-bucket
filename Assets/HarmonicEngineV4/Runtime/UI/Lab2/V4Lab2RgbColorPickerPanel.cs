using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Compact runtime RGB color picker popup for layer paint colors.</summary>
    public sealed class V4Lab2RgbColorPickerPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Slider redSlider;
        [SerializeField] private Slider greenSlider;
        [SerializeField] private Slider blueSlider;
        [SerializeField] private Image rowPreviewImage;
        [SerializeField] private Image popupPreviewImage;
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
        private readonly List<RaycastResult> _raycastScratch = new List<RaycastResult>();

        public static V4Lab2RgbColorPickerPanel Attach(Transform row, Image preview, Action<Color> onColorChanged)
        {
            var existing = row.GetComponentInChildren<V4Lab2RgbColorPickerPanel>(true);
            if (existing != null)
            {
                existing.Bind(preview, onColorChanged);
                existing.EnsurePopupPreview();
                return existing;
            }

            var host = new GameObject("CustomColorPicker", typeof(RectTransform));
            host.transform.SetParent(row, false);
            var panel = host.AddComponent<V4Lab2RgbColorPickerPanel>();
            panel.BuildUi(row, preview, onColorChanged);
            return panel;
        }

        public void Bind(Image preview, Action<Color> onColorChanged)
        {
            rowPreviewImage = V4Lab2UITheme.EnsureColorPreview(preview, Color.white);
            _onColorChanged = onColorChanged;
        }

        public void SetColor(Color color, bool notify = false)
        {
            _suppressEvents = true;
            if (redSlider != null)
            {
                redSlider.SetValueWithoutNotify(color.r);
            }

            if (greenSlider != null)
            {
                greenSlider.SetValueWithoutNotify(color.g);
            }

            if (blueSlider != null)
            {
                blueSlider.SetValueWithoutNotify(color.b);
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
                EnsurePopupPreview();
                PromotePopupToOverlay();
                UpdatePreview(notify: false);
            }
            else
            {
                RestorePopupParent();
            }

            panelRoot.SetActive(open);
        }

        private void LateUpdate()
        {
            if (panelRoot == null || !panelRoot.activeSelf)
            {
                return;
            }

            if (WasPointerPressedThisFrame() && !IsPointerOverPickerUi())
            {
                SetOpen(false);
            }
        }

        private bool IsPointerOverPickerUi()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            var pointerData = new PointerEventData(eventSystem)
            {
                position = GetPointerScreenPosition()
            };

            _raycastScratch.Clear();
            eventSystem.RaycastAll(pointerData, _raycastScratch);

            foreach (RaycastResult result in _raycastScratch)
            {
                if (result.gameObject == null)
                {
                    continue;
                }

                Transform hit = result.gameObject.transform;
                if (panelRoot != null && (hit == panelRoot.transform || hit.IsChildOf(panelRoot.transform)))
                {
                    return true;
                }

                if (pickButton != null && (hit == pickButton.transform || hit.IsChildOf(pickButton.transform)))
                {
                    return true;
                }
            }

            return false;
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

            Image panelImage = panelRoot.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.raycastTarget = true;
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

            GraphicRaycaster raycaster = panelRoot.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = panelRoot.AddComponent<GraphicRaycaster>();
            }

            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        }

        private void BuildUi(Transform row, Image preview, Action<Color> onColorChanged)
        {
            rowPreviewImage = V4Lab2UITheme.EnsureColorPreview(preview, Color.white);
            _onColorChanged = onColorChanged;

            pickButton = CreateButton(row, "PickColorButton", "Pick", new Vector2(154f, 0f), new Vector2(64f, 32f));

            panelRoot = new GameObject("ColorPickerPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelRoot.transform.SetParent(row, false);
            RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(40f, -44f);
            panelRect.sizeDelta = new Vector2(300f, 188f);
            Image panelImage = panelRoot.GetComponent<Image>();
            panelImage.color = V4Lab2UITheme.CardColor;
            panelImage.raycastTarget = true;
            V4Lab2UITheme.CreateChipBorder(panelRoot.transform);

            popupPreviewImage = CreatePopupPreview(panelRoot.transform);
            float y = -56f;
            redSlider = CreatePopupSlider(panelRoot.transform, "Red", 0f, 1f, 1f, ref y);
            greenSlider = CreatePopupSlider(panelRoot.transform, "Green", 0f, 1f, 1f, ref y);
            blueSlider = CreatePopupSlider(panelRoot.transform, "Blue", 0f, 1f, 1f, ref y);
            closeButton = CreateButton(panelRoot.transform, "CloseColorPickerButton", "Done", new Vector2(0f, y - 8f), new Vector2(120f, 28f));

            pickButton.onClick.AddListener(() => SetOpen(true));
            closeButton.onClick.AddListener(() => SetOpen(false));
            redSlider.onValueChanged.AddListener(_ => UpdatePreview(notify: true));
            greenSlider.onValueChanged.AddListener(_ => UpdatePreview(notify: true));
            blueSlider.onValueChanged.AddListener(_ => UpdatePreview(notify: true));
            panelRoot.SetActive(false);
        }

        private void UpdatePreview(bool notify)
        {
            if (redSlider == null || greenSlider == null || blueSlider == null)
            {
                return;
            }

            Color color = new Color(redSlider.value, greenSlider.value, blueSlider.value, 1f);
            if (popupPreviewImage != null)
            {
                popupPreviewImage.color = color;
            }

            if (rowPreviewImage != null)
            {
                rowPreviewImage.color = color;
            }

            if (notify && !_suppressEvents)
            {
                _onColorChanged?.Invoke(color);
            }
        }

        private static bool WasPointerPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            return Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        private static Vector2 GetPointerScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                return Touchscreen.current.primaryTouch.position.ReadValue();
            }

            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
#endif
            return Input.mousePosition;
        }

        private void EnsurePopupPreview()
        {
            if (panelRoot == null)
            {
                return;
            }

            Color current = popupPreviewImage != null ? popupPreviewImage.color : Color.white;
            Transform previewRoot = panelRoot.transform.Find("PopupColorPreview");
            if (previewRoot != null)
            {
                Image root = previewRoot.GetComponent<Image>();
                popupPreviewImage = V4Lab2UITheme.EnsureColorPreview(root, current);
            }
            else if (popupPreviewImage == null)
            {
                popupPreviewImage = CreatePopupPreview(panelRoot.transform);
            }

            if (panelRoot.transform is RectTransform panelRect)
            {
                Vector2 size = panelRect.sizeDelta;
                if (size.y < 188f)
                {
                    panelRect.sizeDelta = new Vector2(size.x, 188f);
                }
            }

            UpdatePreview(notify: false);
        }

        private static Image CreatePopupPreview(Transform parent)
        {
            var go = new GameObject("PopupColorPreview",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -10f);
            rect.sizeDelta = new Vector2(268f, 40f);
            Image root = go.GetComponent<Image>();
            root.raycastTarget = false;
            return V4Lab2UITheme.EnsureColorPreview(root, Color.white);
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
            label.raycastTarget = false;
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
            text.raycastTarget = false;
            V4Lab2UITheme.ApplyBodyText(text);
            return button;
        }
    }
}
