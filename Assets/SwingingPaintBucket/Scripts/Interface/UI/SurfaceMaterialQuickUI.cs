using SwingingPaintBucket.Canvas;
using SwingingPaintBucket.Materials;
using UnityEngine;
using UnityEngine.UI;

namespace SwingingPaintBucket.Interface.UI
{
    public class SurfaceMaterialQuickUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasOverlayBinder canvasOverlayBinder;

        [Header("Layout")]
        [SerializeField] private Vector2 panelPosition = new Vector2(20f, -20f);
        [SerializeField] private Vector2 buttonSize = new Vector2(120f, 36f);
        [SerializeField] private float spacing = 8f;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.15f, 0.15f, 0.18f, 0.9f);
        [SerializeField] private Color selectedColor = new Color(0.25f, 0.55f, 1f, 0.95f);
        [SerializeField] private Color textColor = Color.white;

        private Button[] buttons;
        private CanvasSurfaceType currentSurface = CanvasSurfaceType.Fabric;

        private void Awake()
        {
            if (canvasOverlayBinder == null)
                canvasOverlayBinder = FindAnyObjectByType<CanvasOverlayBinder>();

            BuildUI();
            SelectSurface(CanvasSurfaceType.Fabric);
        }

        private void BuildUI()
        {
            GameObject panel = new GameObject("SurfaceMaterialQuickPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = panelPosition;
            panelRect.sizeDelta = new Vector2(560f, 56f);

            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.35f);

            HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CanvasSurfaceType[] surfaces =
            {
                CanvasSurfaceType.Fabric,
                CanvasSurfaceType.Wood,
                CanvasSurfaceType.Metal,
                CanvasSurfaceType.Paper
            };

            buttons = new Button[surfaces.Length];

            for (int i = 0; i < surfaces.Length; i++)
            {
                CanvasSurfaceType surface = surfaces[i];
                buttons[i] = CreateButton(panel.transform, surface.ToString(), () => SelectSurface(surface));
            }
        }

        private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = buttonSize.x;
            layoutElement.preferredHeight = buttonSize.y;

            Image image = buttonObject.GetComponent<Image>();
            image.color = normalColor;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = textColor;
            text.fontSize = 16;
            text.fontStyle = FontStyle.Bold;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;

            return button;
        }

        private void SelectSurface(CanvasSurfaceType surface)
        {
            currentSurface = surface;

            if (canvasOverlayBinder == null)
            {
                Debug.LogWarning("[SurfaceMaterialQuickUI] CanvasOverlayBinder reference is missing.");
                canvasOverlayBinder = FindAnyObjectByType<CanvasOverlayBinder>();
            }

            if (canvasOverlayBinder != null)
            {
                canvasOverlayBinder.SetSurfaceType(surface);
                Debug.Log("[SurfaceMaterialQuickUI] Surface changed to " + surface);
            }

            UpdateButtonVisuals();
        }

        private void UpdateButtonVisuals()
        {
            if (buttons == null)
                return;

            for (int i = 0; i < buttons.Length; i++)
            {
                Image image = buttons[i].GetComponent<Image>();
                Text text = buttons[i].GetComponentInChildren<Text>();

                bool selected = text != null && text.text == currentSurface.ToString();
                image.color = selected ? selectedColor : normalColor;
            }
        }
    }
}