using UnityEngine;
using UnityEngine.UI;

namespace SwingingPaintBucket.Interface.UI
{
    [CreateAssetMenu(fileName = "RetroUIConfig", menuName = "UI/Retro Config")]
    public class RetroUIConfig : ScriptableObject
    {
        [Header("Retro Colors - Neon Arcade")]
        public Color PrimaryColor = new Color(1f, 0.2f, 0.7f);     // Neon Pink
        public Color SecondaryColor = new Color(0f, 1f, 0.8f);     // Cyan
        public Color AccentColor = new Color(1f, 0.8f, 0f);        // Yellow
        public Color BackgroundColor = new Color(0.05f, 0.05f, 0.1f); // Dark Navy
        public Color PanelColor = new Color(0.08f, 0.08f, 0.15f);
        public Color TextColor = new Color(0.9f, 0.9f, 1f);
        public Color DangerColor = new Color(1f, 0.2f, 0.2f);
        public Color SuccessColor = new Color(0.2f, 1f, 0.2f);

        [Header("Retro Font Settings")]
        public int TitleFontSize = 32;
        public int HeaderFontSize = 20;
        public int BodyFontSize = 16;
        public int SmallFontSize = 12;

        [Header("Retro Effects")]
        public float GlowIntensity = 0.5f;
        public int BorderThickness = 3;
        public float ScanlineOpacity = 0.1f;

        [Header("Retro Sounds")]
        public AudioClip ButtonClickSound;
        public AudioClip MenuOpenSound;
        public AudioClip MenuCloseSound;
    }
}