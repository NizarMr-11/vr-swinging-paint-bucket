using UnityEngine;
using UnityEngine.UI;

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Shared dark-lab styling for Lab2 runtime UGUI.</summary>
    public static class V4Lab2UITheme
    {
        public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.14f, 0.94f);
        public static readonly Color CardColor = new Color(0.11f, 0.13f, 0.2f, 0.98f);
        public static readonly Color CardBorderColor = new Color(0.35f, 0.58f, 0.86f, 0.55f);
        public static readonly Color AccentColor = new Color(0.42f, 0.72f, 1f, 1f);
        public static readonly Color TextColor = new Color(0.95f, 0.96f, 0.98f, 1f);
        public static readonly Color MutedTextColor = new Color(0.62f, 0.68f, 0.76f, 1f);
        public static readonly Color ButtonPrimary = new Color(0.22f, 0.5f, 0.82f, 1f);
        public static readonly Color ButtonSecondary = new Color(0.16f, 0.2f, 0.3f, 1f);
        public static readonly Color SliderFill = new Color(0.35f, 0.68f, 0.98f, 1f);
        public static readonly Color SliderTrack = new Color(0.1f, 0.12f, 0.18f, 1f);
        public static readonly Color ChipBorder = new Color(0.92f, 0.94f, 0.98f, 0.85f);
        public static readonly Color PausedBadge = new Color(0.92f, 0.34f, 0.24f, 0.95f);
        public static readonly Color ValueLabelColor = new Color(0.75f, 0.82f, 0.92f, 1f);

        public const int PanelPadding = 16;
        public const int ControlSpacing = 12;
        public const int SectionSpacing = 24;
        public const int TitleFontSize = 30;
        public const int SectionFontSize = 17;
        public const int BodyFontSize = 14;
        public const int StatsFontSize = 13;
        public const float ChipSize = 40f;

        public static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static void ApplyPanel(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.color = PanelColor;
            image.raycastTarget = true;
        }

        public static void ApplyCard(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.color = CardColor;
            image.raycastTarget = true;
            ApplyChipBorder(image.transform, CardBorderColor, 2f);
        }

        public static void ApplySectionHeader(Text text)
        {
            if (text == null)
            {
                return;
            }

            text.font = DefaultFont;
            text.fontSize = SectionFontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = AccentColor;
            text.alignment = TextAnchor.MiddleLeft;
        }

        public static void ApplyBodyText(Text text, bool muted = false)
        {
            if (text == null)
            {
                return;
            }

            text.font = DefaultFont;
            text.fontSize = BodyFontSize;
            text.color = muted ? MutedTextColor : TextColor;
            text.alignment = TextAnchor.MiddleLeft;
        }

        public static void ApplyTabButton(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = active ? ButtonPrimary : ButtonSecondary;
            }

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.font = DefaultFont;
                label.fontSize = BodyFontSize;
                label.color = active ? TextColor : MutedTextColor;
                label.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        public static Text CreateBodyLabel(Transform parent, string name, string text, Vector2 pos, Vector2 size, bool muted = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            Text label = go.GetComponent<Text>();
            label.text = text;
            ApplyBodyText(label, muted);
            return label;
        }

        public static void ApplyButton(Button button, bool primary)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = primary ? ButtonPrimary : ButtonSecondary;
            }

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.font = DefaultFont;
                label.fontSize = BodyFontSize;
                label.color = TextColor;
                label.alignment = TextAnchor.MiddleCenter;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
        }

        /// <summary>Ensure a generated Toggle has a visible checkbox and checkmark.</summary>
        public static void EnsureToggleGraphics(Toggle toggle, float rowWidth = 640f)
        {
            if (toggle == null)
            {
                return;
            }

            RectTransform rowRect = toggle.GetComponent<RectTransform>();
            if (rowRect != null && rowWidth > 0f)
            {
                rowRect.sizeDelta = new Vector2(rowWidth, 32f);
            }

            Transform background = toggle.transform.Find("Background");
            Image backgroundImage;
            if (background == null)
            {
                var bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                bgGo.transform.SetParent(toggle.transform, false);
                RectTransform bgRect = bgGo.GetComponent<RectTransform>();
                bgRect.anchorMin = new Vector2(0f, 0.5f);
                bgRect.anchorMax = new Vector2(0f, 0.5f);
                bgRect.pivot = new Vector2(0f, 0.5f);
                bgRect.anchoredPosition = new Vector2(-rowWidth * 0.5f + 8f, 0f);
                bgRect.sizeDelta = new Vector2(22f, 22f);
                backgroundImage = bgGo.GetComponent<Image>();
            }
            else
            {
                backgroundImage = background.GetComponent<Image>();
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = SliderTrack;
                ApplyChipBorder(backgroundImage.transform, AccentColor, 1.5f);
            }

            Transform checkmark = toggle.transform.Find("Background/Checkmark");
            if (checkmark == null)
            {
                checkmark = toggle.transform.Find("Checkmark");
            }

            Image checkmarkImage;
            if (checkmark == null)
            {
                var checkGo = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                checkGo.transform.SetParent(backgroundImage != null ? backgroundImage.transform : toggle.transform, false);
                RectTransform checkRect = checkGo.GetComponent<RectTransform>();
                checkRect.anchorMin = Vector2.zero;
                checkRect.anchorMax = Vector2.one;
                checkRect.offsetMin = new Vector2(4f, 4f);
                checkRect.offsetMax = new Vector2(-4f, -4f);
                checkmarkImage = checkGo.GetComponent<Image>();
            }
            else
            {
                checkmarkImage = checkmark.GetComponent<Image>();
            }

            if (checkmarkImage != null)
            {
                checkmarkImage.color = AccentColor;
                checkmarkImage.raycastTarget = false;
            }

            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkmarkImage;
            toggle.transition = Selectable.Transition.ColorTint;

            ColorBlock colors = toggle.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
            colors.colorMultiplier = 1f;
            toggle.colors = colors;

            Text label = toggle.GetComponentInChildren<Text>();
            if (label != null)
            {
                RectTransform labelRect = label.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 0.5f);
                labelRect.anchorMax = new Vector2(1f, 0.5f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.anchoredPosition = new Vector2(-rowWidth * 0.5f + 36f, 0f);
                labelRect.sizeDelta = new Vector2(rowWidth - 44f, 24f);
                ApplyBodyText(label);
            }
        }

        public static void ApplySlider(Slider slider)
        {
            if (slider == null)
            {
                return;
            }

            Transform fill = slider.fillRect;
            if (fill != null)
            {
                Image fillImage = fill.GetComponent<Image>();
                if (fillImage != null)
                {
                    fillImage.color = SliderFill;
                }
            }

            Transform background = slider.transform.Find("Background");
            if (background != null)
            {
                Image bgImage = background.GetComponent<Image>();
                if (bgImage != null)
                {
                    bgImage.color = SliderTrack;
                }
            }

            if (slider.handleRect != null)
            {
                Image handle = slider.handleRect.GetComponent<Image>();
                if (handle != null)
                {
                    handle.color = AccentColor;
                }
            }
        }

        public static Image CreateChipFill(Transform parent)
        {
            Transform existing = parent.Find("ChipFill");
            if (existing != null)
            {
                return existing.GetComponent<Image>();
            }

            var fillGo = new GameObject("ChipFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(parent, false);
            RectTransform rect = fillGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
            Image image = fillGo.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        public static Image CreateChipBorder(Transform parent)
        {
            return ApplyChipBorder(parent, ChipBorder, 2f);
        }

        public static Image ApplyChipBorder(Transform parent, Color borderColor, float thickness)
        {
            Transform existing = parent.Find("ChipBorder");
            Image borderImage;
            if (existing != null)
            {
                borderImage = existing.GetComponent<Image>();
            }
            else
            {
                var borderGo = new GameObject("ChipBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                borderGo.transform.SetParent(parent, false);
                borderGo.transform.SetAsFirstSibling();
                RectTransform rect = borderGo.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                borderImage = borderGo.GetComponent<Image>();
            }

            borderImage.color = borderColor;
            borderImage.raycastTarget = false;
            borderImage.enabled = true;
            return borderImage;
        }

        public static Text CreateValueLabel(Transform sliderTransform)
        {
            Transform existing = sliderTransform.Find("ValueLabel");
            if (existing != null)
            {
                Text existingText = existing.GetComponent<Text>();
                AlignValueLabelToSlider(sliderTransform as RectTransform, existing as RectTransform);
                return existingText;
            }

            var go = new GameObject("ValueLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(sliderTransform.parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(72f, 22f);
            AlignValueLabelToSlider(sliderTransform as RectTransform, rect);

            Text text = go.GetComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = 13;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = ValueLabelColor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        public static void AlignValueLabelToSlider(RectTransform sliderRect, RectTransform labelRect)
        {
            if (sliderRect == null || labelRect == null)
            {
                return;
            }

            bool topAnchored = Mathf.Approximately(sliderRect.anchorMin.y, 1f)
                && Mathf.Approximately(sliderRect.anchorMax.y, 1f);

            if (topAnchored)
            {
                labelRect.anchorMin = new Vector2(0.5f, 1f);
                labelRect.anchorMax = new Vector2(0.5f, 1f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                float y = sliderRect.anchoredPosition.y - sliderRect.sizeDelta.y * 0.5f;
                labelRect.anchoredPosition = new Vector2(
                    sliderRect.anchoredPosition.x + sliderRect.sizeDelta.x * 0.5f + 14f,
                    y);
            }
            else
            {
                labelRect.anchorMin = new Vector2(0.5f, 0.5f);
                labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.anchoredPosition = sliderRect.anchoredPosition
                    + new Vector2(sliderRect.sizeDelta.x * 0.5f + 14f, 0f);
            }
        }

        public static V4Lab2ColorChip UpgradeButtonToColorChip(Button button, Color color)
        {
            if (button == null)
            {
                return null;
            }

            RectTransform rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(ChipSize, ChipSize);

            Image bg = button.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = new Color(0f, 0f, 0f, 0.15f);
            }

            foreach (Text label in button.GetComponentsInChildren<Text>())
            {
                label.gameObject.SetActive(false);
            }

            V4Lab2ColorChip chip = button.GetComponent<V4Lab2ColorChip>();
            if (chip == null)
            {
                chip = button.gameObject.AddComponent<V4Lab2ColorChip>();
            }

            chip.Configure(color);
            return chip;
        }
    }
}
