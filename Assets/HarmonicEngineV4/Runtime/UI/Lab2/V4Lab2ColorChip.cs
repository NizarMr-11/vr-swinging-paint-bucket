using UnityEngine;
using UnityEngine.UI;

namespace HarmonicEngineV4.UI.Lab2
{
    /// <summary>Single paint-color chip with visible fill and selection ring.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class V4Lab2ColorChip : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private Image border;
        [SerializeField] private Color chipColor = Color.white;

        public Color ChipColor => chipColor;

        public void Configure(Color color)
        {
            chipColor = color;
            EnsureVisuals();
            if (fill != null)
            {
                fill.color = color;
            }
        }

        public void SetSelected(bool selected)
        {
            EnsureVisuals();
            if (border != null)
            {
                border.enabled = selected;
                border.color = selected ? V4Lab2UITheme.AccentColor : V4Lab2UITheme.ChipBorder;
            }

            if (fill != null)
            {
                fill.rectTransform.localScale = selected ? Vector3.one * 1.06f : Vector3.one;
            }
        }

        private void EnsureVisuals()
        {
            if (fill == null)
            {
                fill = V4Lab2UITheme.CreateChipFill(transform);
            }

            if (border == null)
            {
                border = V4Lab2UITheme.CreateChipBorder(transform);
            }
        }
    }
}
