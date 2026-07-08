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
        
        // ألوان Rich Text (Hex strings)
        private const string COLOR_KEY = "#4CAF50";
        private const string COLOR_ACCENT = "#FFD700";
        private const string COLOR_HIGHLIGHT = "#4FC3F7";
        private const string COLOR_SUCCESS = "#81C784";
        private const string COLOR_WARNING = "#FFB74D";
        private const string COLOR_PURPLE = "#CE93D8";
        private const string COLOR_DANGER = "#FF6B6B";
        
        // الألوان
        private readonly Color _activeTabColor = new Color(0.15f, 0.35f, 0.65f, 1f);
        private readonly Color _inactiveTabColor = new Color(0.15f, 0.15f, 0.25f, 1f);
        private readonly Color _titleColor = new Color(1f, 0.85f, 0.1f);
        private readonly Color _textColor = new Color(0.85f, 0.87f, 0.9f);
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
            
            // جعل اللوحة تأخذ المساحة الكاملة
            var mainRt = _mainContainer.GetComponent<RectTransform>();
            mainRt.anchorMin = Vector2.zero;
            mainRt.anchorMax = Vector2.one;
            mainRt.sizeDelta = Vector2.zero;
            
            var mainLayout = _mainContainer.AddComponent<LayoutElement>();
            mainLayout.flexibleHeight = 1f;
            mainLayout.flexibleWidth = 1f;
            
            var mainVL = _mainContainer.AddComponent<VerticalLayoutGroup>();
            mainVL.spacing = 2;
            mainVL.padding = new RectOffset(8, 8, 4, 4);
            mainVL.childForceExpandWidth = true;
            mainVL.childControlHeight = true;
            mainVL.childForceExpandHeight = false;
            
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
            
            // تفعيل التبويب الأول
            SwitchTab(TabType.About);
            
            Debug.Log("InfoPanel built successfully!");
        }
        
        /// <summary>
        /// إنشاء شريط التبويبات
        /// </summary>
        private void CreateTabBar()
        {
            var tabBar = new GameObject("TabBar", typeof(RectTransform));
            tabBar.transform.SetParent(_mainContainer.transform, false);
            
            var tabLayout = tabBar.AddComponent<LayoutElement>();
            tabLayout.preferredHeight = 30;
            tabLayout.flexibleWidth = 1f;
            
            var tabHL = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabHL.spacing = 4;
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
            text.fontSize = 12;
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
            sectionLayout.minHeight = 200f; // ارتفاع أدنى
            
            var sectionVL = section.AddComponent<VerticalLayoutGroup>();
            sectionVL.spacing = 2;
            sectionVL.padding = new RectOffset(4, 4, 2, 2);
            sectionVL.childForceExpandWidth = true;
            sectionVL.childControlHeight = true;
            sectionVL.childForceExpandHeight = true;
            
            // عنوان القسم
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(section.transform, false);
            var titleLayout = titleGo.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 22;
            titleLayout.flexibleWidth = 1f;
            
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = title;
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 12;
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
            scrollLayout.minHeight = 150f;
            
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
            vpImg.raycastTarget = false;
            
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
            text.fontSize = 10;
            text.color = _textColor;
            text.alignment = TextAnchor.UpperLeft;
            text.supportRichText = true;
            text.lineSpacing = 1.2f;
            text.raycastTarget = true;
            
            // جعل النص يظهر بشكل واضح
            var shadow = content.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(1, -1);
            
            // إعداد ScrollRect
            scrollRect.content = contentRt;
            scrollRect.viewport = vpRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;
            
            // إضافة Scrollbar
            CreateScrollbar(scrollGo.transform);
            
            return text;
        }
        
        /// <summary>
        /// إنشاء شريط تمرير
        /// </summary>
        private void CreateScrollbar(Transform parent)
        {
            var scrollbarGo = new GameObject("Scrollbar", typeof(RectTransform));
            scrollbarGo.transform.SetParent(parent, false);
            
            var sbRt = scrollbarGo.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1, 0);
            sbRt.anchorMax = new Vector2(1, 1);
            sbRt.pivot = new Vector2(1, 0.5f);
            sbRt.sizeDelta = new Vector2(12, 0);
            
            var scrollbar = scrollbarGo.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            
            // خلفية الـ Scrollbar
            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(scrollbarGo.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.15f, 0.8f);
            
            // Handle
            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(scrollbarGo.transform, false);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0.1f, 0);
            handleRt.anchorMax = new Vector2(0.9f, 0.3f);
            handleRt.sizeDelta = Vector2.zero;
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = new Color(0.3f, 0.5f, 0.7f, 0.9f);
            
            scrollbar.targetGraphic = handleImg;
            scrollbar.handleRect = handleRt;
            
            // ربط الـ Scrollbar بالـ ScrollRect
            var scrollRect = parent.GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.verticalScrollbar = scrollbar;
                scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }
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
            qiLayout.preferredHeight = 18;
            qiLayout.flexibleWidth = 1f;
            
            var qiText = quickInfo.AddComponent<Text>();
            qiText.text = "💡 <b>Pro tip:</b> Use sliders for real-time physics tweaks!";
            qiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            qiText.fontSize = 9;
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
            
            // إظهار/إخفاء الأقسام
            if (_aboutSection != null) _aboutSection.SetActive(tab == TabType.About);
            if (_controlsSection != null) _controlsSection.SetActive(tab == TabType.Controls);
            if (_tipsSection != null) _tipsSection.SetActive(tab == TabType.Tips);
            
            UpdateTabVisuals();
            
            Debug.Log($"Switched to tab: {tab}");
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
            
            sb.AppendLine($"<b><color={COLOR_ACCENT}>🔬 The Swinging Paint Bucket</color></b>");
            sb.AppendLine("A precision physics simulation exploring the");
            sb.AppendLine("fascinating dynamics of a pendulum with a leak!");
            sb.AppendLine();
            
            sb.AppendLine($"<b><color={COLOR_HIGHLIGHT}>🌌 Multi-Scale Physics Engine</color></b>");
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
            
            sb.AppendLine($"<b><color={COLOR_SUCCESS}>🧪 From Atoms to Art</color></b>");
            sb.AppendLine("Every droplet is calculated considering:");
            sb.AppendLine("  • Intermolecular forces (van der Waals)");
            sb.AppendLine("  • Thermal agitation (Brownian motion)");
            sb.AppendLine("  • Capillary action at nozzle edges");
            sb.AppendLine();
            
            sb.AppendLine($"<b><color={COLOR_WARNING}>📐 The Physics Model</color></b>");
            sb.AppendLine("The pendulum follows the nonlinear equation:");
            sb.AppendLine("  θ'' + (g/L)sin(θ) + γ·θ' = F_ext(t)");
            sb.AppendLine();
            sb.AppendLine("Paint flow rate is governed by:");
            sb.AppendLine("  dV/dt = -C_d · A · √(2gh_eff)");
            sb.AppendLine("Where h_eff considers centrifugal effects!");
            sb.AppendLine();
            
            sb.AppendLine($"<b><color={COLOR_PURPLE}>🎯 Educational Value</color></b>");
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
            
            sb.AppendLine($"<b><color={COLOR_ACCENT}>⌨️ KEYBOARD SHORTCUTS</color></b>");
            sb.AppendLine();
            
            sb.AppendLine($"<b><color={COLOR_HIGHLIGHT}>🧪 Experiment Controls</color></b>");
            sb.AppendLine($"  <color={COLOR_KEY}>[R]</color>    Record current experiment");
            sb.AppendLine($"  <color={COLOR_KEY}>[C]</color>    Clear all recorded experiments");
            sb.AppendLine();
            
            sb.AppendLine($"<b><color={COLOR_SUCCESS}>🖥️ Interface Controls</color></b>");
            sb.AppendLine($"  <color={COLOR_KEY}>[H]</color>    Toggle this panel visibility");
            sb.AppendLine();
            sb.AppendLine($"  <color={COLOR_KEY}>[Enter]</color>    Resumes the simulation");
            sb.AppendLine($"  <color={COLOR_KEY}>[Space]</color>    Pauses the simulation");
            sb.AppendLine($"  <color={COLOR_KEY}>[S]</color>    Saves a PNG picture of the paint draw");
            sb.AppendLine();
            
            sb.AppendLine($"<b><color={COLOR_WARNING}>📋 EXPERIMENT PANEL BUTTONS</color></b>");
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
            
            sb.AppendLine($"<b><color={COLOR_PURPLE}>📊 QUICK SETTINGS PANEL</color></b>");
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
            
            sb.AppendLine($"<b><color={COLOR_ACCENT}>⚙ MENU BUTTON</color></b>");
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
            
            sb.AppendLine($"<b><color={COLOR_ACCENT}>🌟 PRO TIPS</color></b>");
            sb.AppendLine();
            
            sb.AppendLine($"<b><color={COLOR_HIGHLIGHT}>🎯 Getting Started</color></b>");
            sb.AppendLine("  1. Start with default settings to understand");
            sb.AppendLine("     the basic pendulum behavior");
            sb.AppendLine("  2. Gradually adjust one parameter at a time");
            sb.AppendLine("  3. Use the HUD to monitor real-time changes");
            sb.AppendLine("  4. Record interesting experiments for comparison");
            sb.AppendLine();
            
            sb.AppendLine($"<b><color={COLOR_SUCCESS}>🔬 Experiment Ideas</color></b>");
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
            
            sb.AppendLine($"<b><color={COLOR_WARNING}>⚡ Performance Tips</color></b>");
            sb.AppendLine("  • Reduce particle count if FPS drops");
            sb.AppendLine("  • Lower paint resolution for smoother sim");
            sb.AppendLine("  • Close other applications for CPU headroom");
            sb.AppendLine();
            
            sb.AppendLine($"<b><color={COLOR_PURPLE}>🎨 Artistic Suggestions</color></b>");
            sb.AppendLine("  • Mix colors by overlapping swing patterns");
            sb.AppendLine("  • Create Lissajous-like figures with");
            sb.AppendLine("    specific angle/gravity combinations");
            sb.AppendLine("  • Use high viscosity for 3D paint effects");
            sb.AppendLine("  • Experiment with material absorption rates");
            sb.AppendLine("    for unique paint-surface interactions");
            sb.AppendLine();
            
            sb.AppendLine($"<b><color={COLOR_DANGER}>⚠️ Common Pitfalls</color></b>");
            sb.AppendLine("  • Too short rope = unstable simulation");
            sb.AppendLine("  • Too high mass + low gravity = slow swing");
            sb.AppendLine("  • Empty bucket still swings - check volume!");
            sb.AppendLine("  • Extreme wind can blow paint off canvas");
            
            return sb.ToString();
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