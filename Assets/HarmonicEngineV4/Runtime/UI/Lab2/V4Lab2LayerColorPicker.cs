using System;
using UnityEngine;
using UnityEngine.UI;

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Layer color row: preview stripe, preset chips, and custom RGB picker.</summary>
    public sealed class V4Lab2LayerColorPicker : MonoBehaviour
    {
        private static readonly Color[] Presets = { Color.black, Color.white, Color.yellow };

        [SerializeField] private int layerIndex;
        [SerializeField] private Image preview;
        [SerializeField] private V4Lab2ColorChip[] chips = new V4Lab2ColorChip[3];

        private V4Lab2RgbColorPickerPanel _customPicker;

        public int LayerIndex => layerIndex;
        public Color SelectedColor { get; private set; } = Color.white;

        public event Action<int, Color> ColorChanged;

        public void Configure(int index, Image previewImage, V4Lab2ColorChip black, V4Lab2ColorChip white, V4Lab2ColorChip yellow)
        {
            layerIndex = index;
            preview = previewImage;
            chips = new[] { black, white, yellow };
            WireChips();
            for (int i = 0; i < chips.Length; i++)
            {
                if (chips[i] != null)
                {
                    chips[i].Configure(Presets[i]);
                }
            }

            EnsureCustomPicker();
        }

        private void Awake()
        {
            WireChips();
            EnsureCustomPicker();
        }

        public void SetSelectedColor(Color color, bool notify = false)
        {
            SelectedColor = color;
            if (preview != null)
            {
                preview.color = color;
            }

            int best = FindClosestPresetIndex(color);
            float presetMatch = ColorDistance(color, Presets[best]);
            bool matchesPreset = presetMatch < 0.02f;
            for (int i = 0; i < chips.Length; i++)
            {
                if (chips[i] != null)
                {
                    chips[i].SetSelected(matchesPreset && i == best);
                }
            }

            _customPicker?.SetColor(color, notify: false);

            if (notify)
            {
                ColorChanged?.Invoke(layerIndex, color);
            }
        }

        private void EnsureCustomPicker()
        {
            if (_customPicker != null || preview == null)
            {
                return;
            }

            _customPicker = V4Lab2RgbColorPickerPanel.Attach(transform, preview, color =>
            {
                SetSelectedColor(color, notify: true);
            });
            _customPicker.SetColor(SelectedColor);
        }

        private void WireChips()
        {
            if (chips == null)
            {
                return;
            }

            for (int i = 0; i < chips.Length; i++)
            {
                if (chips[i] == null)
                {
                    continue;
                }

                int presetIndex = i;
                Button button = chips[presetIndex].GetComponent<Button>();
                if (button == null)
                {
                    continue;
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectPreset(presetIndex));
            }
        }

        private void SelectPreset(int presetIndex)
        {
            if (presetIndex < 0 || presetIndex >= Presets.Length)
            {
                return;
            }

            _customPicker?.SetOpen(false);
            SetSelectedColor(Presets[presetIndex], notify: true);
        }

        private static int FindClosestPresetIndex(Color color)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < Presets.Length; i++)
            {
                float dist = ColorDistance(color, Presets[i]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }

            return best;
        }

        private static float ColorDistance(Color a, Color b)
        {
            Vector3 da = new Vector3(a.r, a.g, a.b);
            Vector3 db = new Vector3(b.r, b.g, b.b);
            return (da - db).sqrMagnitude;
        }
    }
}

