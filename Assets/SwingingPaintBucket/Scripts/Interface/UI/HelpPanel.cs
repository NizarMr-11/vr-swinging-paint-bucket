using UnityEngine;
using UnityEngine.UI;
using System.Text;
using SwingingPaintBucket.Pendulum;
using SwingingPaintBucket.Bucket;

namespace SwingingPaintBucket.Interface.UI
{
    /// <summary>
    /// لوحة معلومات اللعبة وتعليمات الاستخدام
    /// </summary>
    public class InfoPanel : MonoBehaviour
    {
        [Header("References")]
        public PendulumSimulator Pendulum;
        public BucketController Bucket;

        // أقسام اللوحة
        private GameObject _mainContainer;
        private GameObject _aboutSection;
        private GameObject _controlsSection;
        private GameObject _tipsSection;
        
        // عناوين الأقسام
        private Text _aboutTitle;
        private Text _controlsTitle;
        private Text _tipsTitle;
        
        // نصوص المحتوى
        private Text _aboutText;
        private Text _controlsText;
        private Text _tipsText;
        
        // أزرار التبديل
        private Button _aboutTabBtn;
        private Button _controlsTabBtn;
        private Button _tipsTabBtn;
        
        // حالة العرض الحالية
        private enum TabType { About, Controls, Tips }
        private TabType _currentTab = TabType.About;
        
        // الألوان
        private readonly Color _activeTabColor = new Color(0.15f, 0.35f, 0.65f, 1f);
        private readonly Color _inactiveTabColor = new Color(0.15f, 0.15f, 0.25f, 1f);
        private readonly Color _titleColor = new Color(1f, 0.85f, 0.1f);
        private readonly Color _textColor = new Color(0.85f, 0.87f, 0.9f);
        private readonly Color _highlightColor = new Color(0.3f, 0.75f, 1f);
        private readonly Color _warningColor = new Color(1f, 0.6f, 0.2f);
        private readonly Color _keyColor = new Color(0.3f, 0.85f, 0.5f);
        private readonly Color _separatorColor = new Color(1f, 0.8f, 0f, 0.15f);
        
        // عرض اللوحة
        private bool _isVisible = true;
        
        /// <summary>
        /// بناء واجهة لوحة المعلومات
        /// </summary>
        public void BuildUI(Transform parent)
        {
            // ==================== الحاوية الرئيسية ====================
            _mainContainer = new GameObject("InfoPanel", typeof(RectTransform));
            _mainContainer.transform.SetParent(parent, false);
            
            var mainLayout = _mainContainer.AddComponent<LayoutElement>();
            mainLayout.flexibleHeight = 1f;
            mainLayout.flexibleWidth = 1f;
            
            var mainVL = _mainContainer.AddComponent<VerticalLayoutGroup>();
            mainVL.spacing = 0;
            mainVL.padding = new RectOffset(0, 0, 0, 0);
            mainVL.childForceExpandWidth = true;
            mainVL.childControlHeight = true;
            mainVL.childControlWidth = true;
            
            // ==================== شريط التبويبات ====================
            CreateTabBar();
            
            // ==================== فاصل ====================
            CreateSectionSeparator();
            
            // ==================== محتوى عن اللعبة ====================
            _aboutSection = CreateContentSection("📖 ABOUT THE SIMULATION");
            _aboutText = CreateContentText(_aboutSection.transform);
            
            _aboutText.text = GetAboutText();
            
            // ==================== محتوى التحكم ====================
            _controlsSection = CreateContentSection("🎮 CONTROLS & USAGE");
            _controlsText = CreateContentText(_controlsSection.transform);
            
            _controlsText.text = GetControlsText();
            _controlsSection.SetActive(false);
            
            // ==================== محتوى النصائح ====================
            _tipsSection = CreateContentSection("💡 TIPS & TRICKS");
            _tipsText = CreateContentText(_tipsSection.transform);
            
            _tipsText.text = GetTipsText();
            _tipsSection.SetActive(false);
            
            // ==================== فاصل سفلي ====================
            CreateSectionSeparator();
            
            // ==================== معلومات سريعة ====================
            CreateQuickInfo();
        }
        
        /// <summary>
        /// إنشاء شريط التبويبات (About | Controls | Tips)
        /// </summary>
        private void CreateTabBar()
        {
            var tabBar = new GameObject("TabBar", typeof(RectTransform));
            tabBar.transform.SetParent(_mainContainer.transform, false);
            
            var tabLayout = tabBar.AddComponent<LayoutElement>();
            tabLayout.preferredHeight = 22;
            tabLayout.flexibleWidth = 1f;
            
            var tabHL = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabHL.spacing = 2;
            tabHL.padding = new RectOffset(4, 4, 2, 2);
            tabHL.childForceExpandWidth = true;
            tabHL.childForceExpandHeight = true;
            
            // تبويب "About"
            _aboutTabBtn = CreateTabButton(tabBar.transform, "📖 About", TabType.About);
            _aboutTabBtn.onClick.AddListener(() => SwitchTab(TabType.About));
            
            // تبويب "Controls"
            _controlsTabBtn = CreateTabButton(tabBar.transform, "🎮 Controls", TabType.Controls);
            _controlsTabBtn.onClick.AddListener(() => SwitchTab(TabType.Controls));
            
            // تبويب "Tips"
            _tipsTabBtn = CreateTabButton(tabBar.transform, "💡 Tips", TabType.Tips);
            _tipsTabBtn.onClick.AddListener(() => SwitchTab(TabType.Tips));
            
            // تحديث حالة التبويبات
            UpdateTabVisuals();
        }
        
        /// <summary>
        /// إنشاء زر تبويب
        /// </summary>
        private Button CreateTabButton(Transform parent, string label, TabType tabType)
        {
            var btnGo = new GameObject("Tab_" + tabType, typeof(RectTransform));
            btnGo.transform.SetParent(parent, false);
            
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = _inactiveTabColor;
            
            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            
            var colors = btn.colors;
            colors.normalColor = _inactiveTabColor;
            colors.highlightedColor = new Color(0.25f, 0.25f, 0.4f, 1f);
            colors.pressedColor = new Color(0.1f, 0.1f, 0.2f, 1f);
            colors.selectedColor = _activeTabColor;
            btn.colors = colors;
            
            // نص الزر
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(btnGo.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            
            var text = textGo.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 10;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            
            return btn;
        }
        
        /// <summary>
        /// إنشاء قسم محتوى
        /// </summary>
        private GameObject CreateContentSection(string title)
        {
            var section = new GameObject("Section", typeof(RectTransform));
            section.transform.SetParent(_mainContainer.transform, false);
            
            var sectionLayout = section.AddComponent<LayoutElement>();
            sectionLayout.flexibleHeight = 1f;
            sectionLayout.flexibleWidth = 1f;
            
            var sectionVL = section.AddComponent<VerticalLayoutGroup>();
            sectionVL.spacing = 2;
            sectionVL.padding = new RectOffset(6, 6, 4, 4);
            sectionVL.childForceExpandWidth = true;
            sectionVL.childControlHeight = true;
            
            // عنوان القسم
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(section.transform, false);
            var titleLayout = titleGo.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 16;
            
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = title;
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 10;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = _titleColor;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.raycastTarget = false;
            
            return section;
        }
        
        /// <summary>
        /// إنشاء نص المحتوى مع شريط تمرير
        /// </summary>
        private Text CreateContentText(Transform parent)
        {
            // ScrollView
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform));
            scrollGo.transform.SetParent(parent, false);
            
            var scrollLayout = scrollGo.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1f;
            scrollLayout.flexibleWidth = 1f;
            
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            
            // Viewport
            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRt = viewport.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.sizeDelta = Vector2.zero;
            
            var vpMask = viewport.AddComponent<Mask>();
            vpMask.showMaskGraphic = false;
            
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0.05f, 0.05f, 0.1f, 0.3f);
            
            // Content
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.sizeDelta = new Vector2(0, 200);
            
            var contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            var text = content.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 9;
            text.color = _textColor;
            text.alignment = TextAnchor.UpperLeft;
            text.supportRichText = true;
            text.lineSpacing = 1.3f;
            text.raycastTarget = true;
            
            var shadow = content.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.4f);
            shadow.effectDistance = new Vector2(1, -1);
            
            // إعداد ScrollRect
            scrollRect.content = contentRt;
            scrollRect.viewport = vpRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 15f;
            
            return text;
        }
        
        /// <summary>
        /// إنشاء فاصل بين الأقسام
        /// </summary>
        private void CreateSectionSeparator()
        {
            var sep = new GameObject("Separator", typeof(RectTransform));
            sep.transform.SetParent(_mainContainer.transform, false);
            
            var sepLayout = sep.AddComponent<LayoutElement>();
            sepLayout.preferredHeight = 2;
            sepLayout.flexibleWidth = 1f;
            
            var line = new GameObject("Line", typeof(RectTransform));
            line.transform.SetParent(sep.transform, false);
            var lineRt = line.GetComponent<RectTransform>();
            lineRt.anchorMin = new Vector2(0.05f, 0.5f);
            lineRt.anchorMax = new Vector2(0.95f, 0.5f);
            lineRt.sizeDelta = Vector2.zero;
            
            var lineImg = line.AddComponent<Image>();
            lineImg.color = _separatorColor;
            lineImg.raycastTarget = false;
        }
        
        /// <summary>
        /// إنشاء معلومات سريعة في الأسفل
        /// </summary>
        private void CreateQuickInfo()
        {
            var quickInfo = new GameObject("QuickInfo", typeof(RectTransform));
            quickInfo.transform.SetParent(_mainContainer.transform, false);
            
            var qiLayout = quickInfo.AddComponent<LayoutElement>();
            qiLayout.preferredHeight = 14;
            qiLayout.flexibleWidth = 1f;
            
            var qiText = quickInfo.AddComponent<Text>();
            qiText.text = "💡 <b>Pro tip:</b> Use sliders for real-time physics tweaks!";
            qiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            qiText.fontSize = 8;
            qiText.fontStyle = FontStyle.Italic;
            qiText.color = new Color(0.5f, 0.6f, 0.7f);
            qiText.alignment = TextAnchor.MiddleCenter;
            qiText.raycastTarget = false;
        }
        
        /// <summary>
        /// تبديل التبويب النشط
        /// </summary>
        private void SwitchTab(TabType tab)
        {
            _currentTab = tab;
            
            _aboutSection.SetActive(tab == TabType.About);
            _controlsSection.SetActive(tab == TabType.Controls);
            _tipsSection.SetActive(tab == TabType.Tips);
            
            UpdateTabVisuals();
        }
        
        /// <summary>
        /// تحديث مظهر التبويبات
        /// </summary>
        private void UpdateTabVisuals()
        {
            if (_aboutTabBtn != null)
                _aboutTabBtn.image.color = _currentTab == TabType.About ? _activeTabColor : _inactiveTabColor;
            
            if (_controlsTabBtn != null)
                _controlsTabBtn.image.color = _currentTab == TabType.Controls ? _activeTabColor : _inactiveTabColor;
            
            if (_tipsTabBtn != null)
                _tipsTabBtn.image.color = _currentTab == TabType.Tips ? _activeTabColor : _inactiveTabColor;
        }
        
        /// <summary>
        /// نص عن المحاكاة
        /// </summary>
        private string GetAboutText()
        {
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine("<b><color=#FFD700>🔬 The Swinging Paint Bucket</color></b>");
            sb.AppendLine("A precision physics simulation exploring the");
            sb.AppendLine("fascinating dynamics of a pendulum with a leak!");
            sb.AppendLine();
            
            sb.AppendLine("<b><color=#4FC3F7>🌌 Multi-Scale Physics Engine</color></b>");
            sb.AppendLine("This simulation bridges the gap between");
            sb.AppendLine("<b>macroscopic</b> and <b>microscopic</b> physics:");
            sb.AppendLine();
            sb.AppendLine("  • <b>Classical Mechanics</b>");
            sb.AppendLine("    Pendulum motion, gravity, momentum");
            sb.AppendLine();
            sb.AppendLine("  • <b>Fluid Dynamics</b>");
            sb.AppendLine("    Viscosity, flow rate, nozzle discharge");
            sb.AppendLine();
            sb.AppendLine("  • <b>Atomic-Level Interactions</b>");
            sb.AppendLine("    Molecular cohesion, surface tension,");
            sb.AppendLine("    absorption rates at material boundaries");
            sb.AppendLine();
            
            sb.AppendLine("<b><color=#81C784>🧪 From Atoms to Art</color></b>");
            sb.AppendLine("Every droplet is calculated considering:");
            sb.AppendLine("  • Intermolecular forces (van der Waals)");
            sb.AppendLine("  • Thermal agitation (Brownian motion)");
            sb.AppendLine("  • Capillary action at nozzle edges");
            sb.AppendLine();
            
            sb.AppendLine("<b><color=#FFB74D>📐 The Physics Model</color></b>");
            sb.AppendLine("The pendulum follows the nonlinear equation:");
            sb.AppendLine("  θ'' + (g/L)sin(θ) + γ·θ' = F_ext(t)");
            sb.AppendLine();
            sb.AppendLine("Paint flow rate is governed by:");
            sb.AppendLine("  dV/dt = -C_d · A · √(2gh_eff)");
            sb.AppendLine("Where h_eff considers centrifugal effects!");
            sb.AppendLine();
            
            sb.AppendLine("<b><color=#CE93D8>🎯 Educational Value</color></b>");
            sb.AppendLine("Perfect for exploring:");
            sb.AppendLine("  • Conservation of energy & momentum");
            sb.AppendLine("  • Damped harmonic oscillators");
            sb.AppendLine("  • Non-linear dynamics & chaos theory");
            sb.AppendLine("  • Fluid mechanics principles");
            sb.AppendLine("  • Computational physics methods");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// نص تعليمات التحكم والاستخدام
        /// </summary>
        private string GetControlsText()
        {
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine("<b><color=#FFD700>⌨️ KEYBOARD SHORTCUTS</color></b>");
            sb.AppendLine();
            
            sb.AppendLine("<b><color=#4FC3F7>🧪 Experiment Controls</color></b>");
            sb.AppendLine($"  <color={ColorToHex(_keyColor)}>[R]</color>    Record current experiment");
            sb.AppendLine($"  <color={ColorToHex(_keyColor)}>[C]</color>    Clear all recorded experiments");
            sb.AppendLine();
            
            sb.AppendLine("<b><color=#81C784>🖥️ Interface Controls</color></b>");
            sb.AppendLine($"  <color={ColorToHex(_keyColor)}>[H]</color>    Toggle this panel visibility");
            sb.AppendLine();
            
            sb.AppendLine("<b><color=#FFB74D>📋 EXPERIMENT PANEL BUTTONS</color></b>");
            sb.AppendLine("When experiment panel is visible:");
            sb.AppendLine();
            sb.AppendLine("  • <b>Record Button</b>");
            sb.AppendLine("    Saves current simulation state with");
            sb.AppendLine("    all parameters (angle, velocity, paint, etc.)");
            sb.AppendLine();
            sb.AppendLine("  • <b>Clear Button</b>");
            sb.AppendLine("    Removes all recorded experiments");
            sb.AppendLine();
            sb.AppendLine("  • <b>◄ Previous Button</b>");
            sb.AppendLine("    Navigate to previous experiment");
            sb.AppendLine();
            sb.AppendLine("  • <b>Next Button ►</b>");
            sb.AppendLine("    Navigate to next experiment");
            sb.AppendLine();
            
            sb.AppendLine("<b><color=#CE93D8>📊 QUICK SETTINGS PANEL</color></b>");
            sb.AppendLine("Located on the right side of screen:");
            sb.AppendLine();
            sb.AppendLine("  • <b>Sliders</b> - Adjust physics parameters");
            sb.AppendLine("    in real-time (Mass, Rope, Gravity, Wind, Angle)");
            sb.AppendLine();
            sb.AppendLine("  • <b>Color Picker</b> - Change paint color");
            sb.AppendLine("    instantly with visual swatches");
            sb.AppendLine();
            sb.AppendLine("  • <b>✓ APPLY Button</b>");
            sb.AppendLine("    Confirm and apply all parameter changes");
            sb.AppendLine();
            sb.AppendLine("  • <b>✕ CANCEL Button</b>");
            sb.AppendLine("    Revert parameters to last applied values");
            sb.AppendLine();
            
            sb.AppendLine("<b><color=#FFD700>⚙ MENU BUTTON</color></b>");
            sb.AppendLine("Located at top-left corner:");
            sb.AppendLine();
            sb.AppendLine("  • Opens advanced settings configuration");
            sb.AppendLine("  • Access to save/load simulation presets");
            sb.AppendLine("  • Graphics & performance options");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// نص النصائح والحيل
        /// </summary>
        private string GetTipsText()
        {
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine("<b><color=#FFD700>🌟 PRO TIPS</color></b>");
            sb.AppendLine();
            
            sb.AppendLine("<b><color=#4FC3F7>🎯 Getting Started</color></b>");
            sb.AppendLine("  1. Start with default settings to understand");
            sb.AppendLine("     the basic pendulum behavior");
            sb.AppendLine("  2. Gradually adjust one parameter at a time");
            sb.AppendLine("  3. Use the HUD to monitor real-time changes");
            sb.AppendLine("  4. Record interesting experiments for comparison");
            sb.AppendLine();
            
            sb.AppendLine("<b><color=#81C784>🔬 Experiment Ideas</color></b>");
            sb.AppendLine("  • <b>Chaos Explorer:</b> Set angle > 150°");
            sb.AppendLine("    and observe non-linear behavior");
            sb.AppendLine("  • <b>Viscosity Test:</b> Compare water (0.001)");
            sb.AppendLine("    vs honey (10.0) flow patterns");
            sb.AppendLine("  • <b>Lunar Mode:</b> Set gravity to 1.62 m/s²");
            sb.AppendLine("    to simulate Moon physics");
            sb.AppendLine("  • <b>Wind Tunnel:</b> Add high wind force to");
            sb.AppendLine("    see aerodynamic effects on paint");
            sb.AppendLine("  • <b>Precision Art:</b> Low viscosity + small");
            sb.AppendLine("    nozzle = fine, detailed patterns");
            sb.AppendLine();
            
            sb.AppendLine("<b><color=#FFB74D>⚡ Performance Tips</color></b>");
            sb.AppendLine("  • Reduce particle count if FPS drops");
            sb.AppendLine("  • Lower paint resolution for smoother sim");
            sb.AppendLine("  • Close other applications for CPU headroom");
            sb.AppendLine();
            
            sb.AppendLine("<b><color=#CE93D8>🎨 Artistic Suggestions</color></b>");
            sb.AppendLine("  • Mix colors by overlapping swing patterns");
            sb.AppendLine("  • Create Lissajous-like figures with");
            sb.AppendLine("    specific angle/gravity combinations");
            sb.AppendLine("  • Use high viscosity for 3D paint effects");
            sb.AppendLine("  • Experiment with material absorption rates");
            sb.AppendLine("    for unique paint-surface interactions");
            sb.AppendLine();
            
            sb.AppendLine("<b><color=#FF6B6B>⚠️ Common Pitfalls</color></b>");
            sb.AppendLine("  • Too short rope = unstable simulation");
            sb.AppendLine("  • Too high mass + low gravity = slow swing");
            sb.AppendLine("  • Empty bucket still swings - check volume!");
            sb.AppendLine("  • Extreme wind can blow paint off canvas");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// تحويل اللون إلى كود hex للاستخدام في Rich Text
        /// </summary>
        private string ColorToHex(Color color)
        {
            return $"#{ColorUtility.ToHtmlStringRGB(color)}";
        }
        
        /// <summary>
        /// تحديث عرض اللوحة
        /// </summary>
        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_mainContainer != null)
                _mainContainer.SetActive(visible);
        }
        
        /// <summary>
        /// قلب حالة العرض
        /// </summary>
        public void ToggleVisibility()
        {
            SetVisible(!_isVisible);
        }
        
        /// <summary>
        /// هل اللوحة مرئية؟
        /// </summary>
        public bool IsVisible()
        {
            return _isVisible;
        }
    }
}