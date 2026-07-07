using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace SwingingPaintBucket.Interface.UI
{
    public static class UIFactory
    {
        private static Sprite _pixelSprite;
        private static RetroUIConfig _config;

        public static void Initialize(RetroUIConfig config)
        {
            _config = config;
            CreatePixelSprite();
        }

        // إضافة هذه الطريقة للوصول إلى الصورة من خارج الكلاس
        public static Sprite GetPixelSprite()
        {
            if (_pixelSprite == null)
                CreatePixelSprite();
            return _pixelSprite;
        }

        private static void CreatePixelSprite()
        {
            if (_pixelSprite != null) return;
            
            Texture2D tex = new Texture2D(4, 4);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            _pixelSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.zero);
            _pixelSprite.name = "PixelSprite"; // إضافة اسم للصورة
        }

        public static GameObject CreateCanvas(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            
            var canvas = go.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                #if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                #endif
            }

            return go;
        }

        public static GameObject CreatePanel(Transform parent, Vector2 size, Color? color = null)
        {
            var go = new GameObject("Panel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.sprite = GetPixelSprite();
            img.color = color ?? _config.PanelColor;

            CreateRetroBorder(go.transform);

            return go;
        }

        private static void CreateRetroBorder(Transform parent)
        {
            CreateBorderLine(parent, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0));
            CreateBorderLine(parent, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0));
            CreateBorderLine(parent, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0));
            CreateBorderLine(parent, new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 0));
        }

        private static void CreateBorderLine(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition)
        {
            var go = new GameObject("Border", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(_config.BorderThickness, _config.BorderThickness);

            var img = go.AddComponent<Image>();
            img.sprite = GetPixelSprite();
            img.color = _config.AccentColor;
        }

        public static GameObject CreateRetroText(Transform parent, string text, int fontSize, Color? color = null, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 60);

            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color ?? _config.TextColor;
            txt.alignment = TextAnchor.MiddleCenter;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(2, -2);

            return go;
        }

        public static GameObject CreateRetroButton(Transform parent, string text, Color? bgColor = null, Color? textColor = null)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(300, 55);

            var img = go.AddComponent<Image>();
            img.sprite = GetPixelSprite();
            img.color = bgColor ?? _config.PrimaryColor;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = _config.AccentColor;
            outline.effectDistance = new Vector2(2, 2);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = bgColor ?? _config.PrimaryColor;
            colors.highlightedColor = (bgColor ?? _config.PrimaryColor) * 1.3f;
            colors.pressedColor = (bgColor ?? _config.PrimaryColor) * 0.7f;
            btn.colors = colors;

            var textObj = CreateRetroText(go.transform, text, _config.BodyFontSize, textColor ?? Color.white, FontStyle.Bold);
            textObj.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            textObj.GetComponent<RectTransform>().anchorMax = Vector2.one;
            textObj.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            return go;
        }

        public static GameObject CreateRetroSlider(Transform parent, string label, float min, float max, float current, string unit, out Slider slider, out Text valueLabel)
        {
            var container = new GameObject("SliderContainer", typeof(RectTransform));
            container.transform.SetParent(parent, false);
            
            var layout = container.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5;
            layout.childForceExpandWidth = true;

            var labelRow = new GameObject("LabelRow", typeof(RectTransform));
            labelRow.transform.SetParent(container.transform, false);
            
            var hLayout = labelRow.AddComponent<HorizontalLayoutGroup>();
            hLayout.childForceExpandWidth = true;

            var labelText = CreateRetroText(labelRow.transform, label, _config.SmallFontSize, _config.TextColor);
            labelText.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 25);

            valueLabel = CreateRetroText(labelRow.transform, current.ToString("0.0") + " " + unit, _config.SmallFontSize, _config.AccentColor, FontStyle.Bold)
                .GetComponent<Text>();
            valueLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 25);
            valueLabel.alignment = TextAnchor.MiddleRight;

            var sliderGo = new GameObject("Slider", typeof(RectTransform));
            sliderGo.transform.SetParent(container.transform, false);
            
            var sliderRt = sliderGo.GetComponent<RectTransform>();
            sliderRt.sizeDelta = new Vector2(0, 25);

            slider = sliderGo.AddComponent<Slider>();
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
            bgImg.sprite = GetPixelSprite();
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
            fillImg.sprite = GetPixelSprite();
            fillImg.color = _config.AccentColor;

            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(sliderGo.transform, false);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0, 0);
            handleRt.anchorMax = new Vector2(0, 1);
            handleRt.sizeDelta = new Vector2(25, 0);
            var handleImg = handle.AddComponent<Image>();
            handleImg.sprite = GetPixelSprite();
            handleImg.color = _config.SecondaryColor;

            slider.targetGraphic = handleImg;
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;

            return container;
        }
    }
}