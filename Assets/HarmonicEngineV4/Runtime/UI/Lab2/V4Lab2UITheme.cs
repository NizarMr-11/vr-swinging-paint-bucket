using UnityEngine;
using UnityEngine.UI;

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Shared dark-lab styling for Lab2 runtime UGUI.</summary>
    public static class V4Lab2UITheme
    {
        public static readonly Color PanelColor = new Color(0.102f, 0.102f, 0.18f, 0.88f);
        public static readonly Color AccentColor = new Color(0.29f, 0.56f, 0.82f, 1f);
        public static readonly Color TextColor = new Color(0.93f, 0.94f, 0.96f, 1f);
        public static readonly Color MutedTextColor = new Color(0.65f, 0.7f, 0.78f, 1f);
        public static readonly Color ButtonPrimary = new Color(0.24f, 0.48f, 0.74f, 1f);
        public static readonly Color ButtonSecondary = new Color(0.18f, 0.22f, 0.32f, 0.95f);
        public static readonly Color SliderFill = new Color(0.35f, 0.62f, 0.9f, 1f);
        public static readonly Color SliderTrack = new Color(0.12f, 0.14f, 0.2f, 1f);
        public static readonly Color PausedBadge = new Color(0.9f, 0.35f, 0.25f, 0.92f);

        public const int PanelPadding = 16;
        public const int ControlSpacing = 12;
        public const int SectionSpacing = 24;
        public const int TitleFontSize = 28;
        public const int SectionFontSize = 18;
        public const int BodyFontSize = 14;
        public const int StatsFontSize = 13;

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
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
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
        }
    }
}
