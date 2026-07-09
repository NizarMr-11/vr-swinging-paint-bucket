using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Upgrades generated UGUI widgets at runtime for clearer layout and visible colors.</summary>
    [DefaultExecutionOrder(-900)]
    public sealed class V4Lab2UiPolish : MonoBehaviour
    {
        [SerializeField] private V4Lab2RuntimeSetupController setupController;

        private readonly List<V4Lab2LayerColorPicker> _layerPickers = new List<V4Lab2LayerColorPicker>();

        public IReadOnlyList<V4Lab2LayerColorPicker> LayerPickers => _layerPickers;

        private void Awake()
        {
            if (setupController == null)
            {
                setupController = GetComponent<V4Lab2RuntimeSetupController>();
            }

            Apply();
        }

        public void Apply()
        {
            _layerPickers.Clear();
            PolishImages(GetComponentsInChildren<Image>(true));
            PolishTexts(GetComponentsInChildren<Text>(true));
            PolishButtons(GetComponentsInChildren<Button>(true));
            PolishSliders(GetComponentsInChildren<Slider>(true));
            PolishToggles(GetComponentsInChildren<Toggle>(true));
            UpgradeLegacyColorButtons(GetComponentsInChildren<Button>(true));
            BuildLayerColorPickers();
            setupController?.BindLayerColorPickers(_layerPickers);
        }

        private static void PolishImages(Image[] images)
        {
            foreach (Image image in images)
            {
                if (image == null)
                {
                    continue;
                }

                string name = image.gameObject.name;
                if (name is "Card" or "WizardCard" or "StatsPanel" or "RuntimeTuningPanel")
                {
                    V4Lab2UITheme.ApplyCard(image);
                }
                else if (name.EndsWith("Panel") && image.transform.childCount > 0)
                {
                    V4Lab2UITheme.ApplyPanel(image);
                }
            }
        }

        private static void PolishTexts(Text[] texts)
        {
            foreach (Text text in texts)
            {
                if (text == null)
                {
                    continue;
                }

                string name = text.gameObject.name;
                if (name == "TitleText")
                {
                    text.fontSize = V4Lab2UITheme.TitleFontSize;
                    text.color = V4Lab2UITheme.TextColor;
                    text.fontStyle = FontStyle.Bold;
                }
                else if (name.StartsWith("Header_"))
                {
                    V4Lab2UITheme.ApplySectionHeader(text);
                }
                else if (name.StartsWith("Label_") || name == "StatsText")
                {
                    V4Lab2UITheme.ApplyBodyText(text);
                }
                else if (name == "SubtitleText")
                {
                    V4Lab2UITheme.ApplyBodyText(text, muted: true);
                }
            }
        }

        private static void PolishButtons(Button[] buttons)
        {
            foreach (Button button in buttons)
            {
                if (button == null)
                {
                    continue;
                }

                string name = button.gameObject.name;
                if (name.Contains("BlackButton") || name.Contains("WhiteButton") || name.Contains("YellowButton"))
                {
                    continue;
                }

                bool primary = name.Contains("RunScene")
                    || name.Contains("ApplyAndStart")
                    || name.Contains("Pause")
                    || name.Contains("CloseTuning")
                    || name.StartsWith("Tab_");
                V4Lab2UITheme.ApplyButton(button, primary);
            }
        }

        private static void PolishSliders(Slider[] sliders)
        {
            foreach (Slider slider in sliders)
            {
                if (slider == null)
                {
                    continue;
                }

                V4Lab2UITheme.ApplySlider(slider);
                V4Lab2SliderValueLabel binder = slider.GetComponent<V4Lab2SliderValueLabel>();
                if (binder == null)
                {
                    binder = slider.gameObject.AddComponent<V4Lab2SliderValueLabel>();
                    Text label = V4Lab2UITheme.CreateValueLabel(slider.transform);
                    bool integers = slider.wholeNumbers;
                    string format = slider.maxValue > 1000f ? "0" : "0.###";
                    binder.Bind(slider, label, format, integers);
                }
                else
                {
                    binder.RefreshLayout();
                }
            }
        }

        private static void PolishToggles(Toggle[] toggles)
        {
            foreach (Toggle toggle in toggles)
            {
                if (toggle == null)
                {
                    continue;
                }

                V4Lab2UITheme.EnsureToggleGraphics(toggle);
            }
        }

        private static void UpgradeLegacyColorButtons(Button[] buttons)
        {
            foreach (Button button in buttons)
            {
                if (button == null)
                {
                    continue;
                }

                string name = button.gameObject.name;
                Color? color = null;
                if (name.Contains("BlackButton"))
                {
                    color = Color.black;
                }
                else if (name.Contains("WhiteButton"))
                {
                    color = Color.white;
                }
                else if (name.Contains("YellowButton"))
                {
                    color = Color.yellow;
                }

                if (color.HasValue)
                {
                    V4Lab2UITheme.UpgradeButtonToColorChip(button, color.Value);
                }
            }
        }

        private void BuildLayerColorPickers()
        {
            var chipsByLayer = new Dictionary<int, V4Lab2ColorChip[]>();
            foreach (V4Lab2ColorChip chip in GetComponentsInChildren<V4Lab2ColorChip>(true))
            {
                if (chip == null)
                {
                    continue;
                }

                Match match = Regex.Match(chip.gameObject.name, @"L(\d+)(Black|White|Yellow)");
                if (!match.Success)
                {
                    continue;
                }

                int layer = int.Parse(match.Groups[1].Value) - 1;
                int preset = match.Groups[2].Value switch
                {
                    "Black" => 0,
                    "White" => 1,
                    _ => 2
                };

                if (!chipsByLayer.TryGetValue(layer, out V4Lab2ColorChip[] array))
                {
                    array = new V4Lab2ColorChip[3];
                    chipsByLayer[layer] = array;
                }

                array[preset] = chip;
            }

            foreach (KeyValuePair<int, V4Lab2ColorChip[]> pair in chipsByLayer)
            {
                if (pair.Value[0] == null || pair.Value[1] == null || pair.Value[2] == null)
                {
                    continue;
                }

                Transform row = EnsureColorRow(pair.Key, pair.Value[0].transform);
                for (int i = 0; i < pair.Value.Length; i++)
                {
                    if (pair.Value[i] != null)
                    {
                        pair.Value[i].transform.SetParent(row, false);
                    }
                }

                LayoutColorChipsInRow(row, pair.Value);
                Image preview = FindOrCreatePreview(row, pair.Key);
                var picker = row.GetComponent<V4Lab2LayerColorPicker>();
                if (picker == null)
                {
                    picker = row.gameObject.AddComponent<V4Lab2LayerColorPicker>();
                }

                picker.Configure(pair.Key, preview, pair.Value[0], pair.Value[1], pair.Value[2]);
                _layerPickers.Add(picker);
            }

            _layerPickers.Sort((a, b) => a.LayerIndex.CompareTo(b.LayerIndex));
        }

        private static Transform EnsureColorRow(int layerIndex, Transform firstChip)
        {
            string rowName = $"Layer{layerIndex + 1}ColorRow";
            Transform parent = firstChip.parent;
            if (parent != null && parent.name == rowName)
            {
                return parent;
            }

            var rowGo = new GameObject(rowName, typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            RectTransform rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = firstChip.GetComponent<RectTransform>().anchoredPosition;
            rowRect.sizeDelta = new Vector2(580f, 44f);

            CreateTextStatic(rowGo.transform, $"Layer{layerIndex + 1}ColorLabel", $"Layer {layerIndex + 1} color",
                new Vector2(-250f, 0f), new Vector2(150f, 24f));
            return rowGo.transform;
        }

        private static void LayoutColorChipsInRow(Transform row, V4Lab2ColorChip[] chips)
        {
            float[] x = { 10f, 58f, 106f };
            for (int i = 0; i < chips.Length && i < x.Length; i++)
            {
                if (chips[i] == null)
                {
                    continue;
                }

                RectTransform rect = chips[i].GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(x[i], 0f);
                rect.sizeDelta = new Vector2(V4Lab2UITheme.ChipSize, V4Lab2UITheme.ChipSize);
            }
        }

        private static void CreateTextStatic(Transform parent, string name, string text, Vector2 pos, Vector2 size)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return;
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            Text label = go.GetComponent<Text>();
            label.text = text;
            V4Lab2UITheme.ApplyBodyText(label);
        }

        private static Image FindOrCreatePreview(Transform row, int layerIndex)
        {
            Transform existing = row.Find($"Layer{layerIndex + 1}Preview");
            if (existing != null)
            {
                Image root = existing.GetComponent<Image>();
                return V4Lab2UITheme.EnsureColorPreview(root, Color.white);
            }

            var previewGo = new GameObject($"Layer{layerIndex + 1}Preview",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            previewGo.transform.SetParent(row, false);
            RectTransform rect = previewGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-70f, 0f);
            rect.sizeDelta = new Vector2(56f, 36f);
            Image rootImage = previewGo.GetComponent<Image>();
            rootImage.raycastTarget = false;
            return V4Lab2UITheme.EnsureColorPreview(rootImage, Color.white);
        }
    }
}
