using UnityEngine;
using UnityEngine.UI;
using System;

namespace SwingingPaintBucket.Interface.UI
{
    public class MenuManager : MonoBehaviour
    {
        [SerializeField] private RetroUIConfig _config;
        private GameObject _overlay;
        private GameObject _mainMenu;
        private GameObject _settingsPanel;
        private GameObject _helpPanel;
        
        private bool _isMenuVisible = false;

        public event Action OnContinue;
        public event Action OnSettingsApplied;
        public event Action OnMenuClosed;

        public bool IsMenuVisible => _isMenuVisible;

        private void Awake()
        {
            if (_config != null)
                UIFactory.Initialize(_config);
        }

        public void OpenMenu()
        {
            Debug.Log("Opening menu...");
            
            if (_overlay == null)
            {
                CreateOverlay();
                Debug.Log("Overlay created");
            }

            _overlay.SetActive(true);
            _isMenuVisible = true;
            ShowMainMenu();
        }

        public void CloseMenu()
        {
            if (_overlay != null)
            {
                _overlay.SetActive(false);
                _isMenuVisible = false;
                OnMenuClosed?.Invoke();
                Debug.Log("Menu closed");
            }
        }

        public bool IsMenuOpen()
        {
            return _isMenuVisible;
        }

        private void CreateOverlay()
        {
            _overlay = UIFactory.CreateCanvas("RetroOverlay", transform);
            
            var overlayImg = _overlay.AddComponent<Image>();
            overlayImg.color = new Color(0, 0, 0, 0.92f);
            
            var rt = overlayImg.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            
            Debug.Log("Overlay created - full screen");
        }

        public void ShowMainMenu()
        {
            HideAllPanels();
            
            if (_mainMenu == null)
                CreateMainMenu();
            
            _mainMenu.SetActive(true);
            Debug.Log("Main menu shown");
        }

        private void CreateMainMenu()
        {
            _mainMenu = CreateFullScreenPanel(_overlay.transform, new Color(0.05f, 0.05f, 0.1f));
            
            var container = new GameObject("Container", typeof(RectTransform));
            container.transform.SetParent(_mainMenu.transform, false);
            
            var containerRt = container.GetComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(0.5f, 0.5f);
            containerRt.anchorMax = new Vector2(0.5f, 0.5f);
            containerRt.pivot = new Vector2(0.5f, 0.5f);
            containerRt.sizeDelta = new Vector2(700, 600);

            var title = CreateSimpleText(container.transform, "PAINT SWING", 64, new Color(1f, 0.8f, 0f), FontStyle.Bold);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchoredPosition = new Vector2(0, 240);
            titleRt.sizeDelta = new Vector2(700, 100);

            var btnContainer = new GameObject("Buttons", typeof(RectTransform));
            btnContainer.transform.SetParent(container.transform, false);
            var btnRt = btnContainer.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(0, -20);
            btnRt.sizeDelta = new Vector2(550, 350);

            var vLayout = btnContainer.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 30;
            vLayout.childForceExpandWidth = true;

            var continueBtn = CreateLargeButton(btnContainer.transform, "▶ CONTINUE", new Color(0.2f, 0.7f, 0.2f));
            continueBtn.GetComponent<Button>().onClick.AddListener(() => {
                OnContinue?.Invoke();
                CloseMenu();
            });

            var settingsBtn = CreateLargeButton(btnContainer.transform, "⚙ CHANGE ENVIRONMENT", new Color(0.2f, 0.4f, 0.8f));
            settingsBtn.GetComponent<Button>().onClick.AddListener(ShowSettings);

            var helpBtn = CreateLargeButton(btnContainer.transform, "? HELP", new Color(0.7f, 0.5f, 0.1f));
            helpBtn.GetComponent<Button>().onClick.AddListener(ShowHelp);
            
            CreateCloseButton(_mainMenu.transform);
        }

        private void CreateCloseButton(Transform parent)
        {
            var closeBtn = new GameObject("CloseButton", typeof(RectTransform));
            closeBtn.transform.SetParent(parent, false);
            
            var rt = closeBtn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-40, -40);
            rt.sizeDelta = new Vector2(80, 80);

            var img = closeBtn.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.3f);

            var btn = closeBtn.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.3f, 0.3f, 0.3f);
            colors.highlightedColor = new Color(0.5f, 0.2f, 0.2f);
            colors.pressedColor = new Color(0.2f, 0.1f, 0.1f);
            btn.colors = colors;

            var text = CreateSimpleText(closeBtn.transform, "✕", 40, Color.white, FontStyle.Bold);
            text.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            text.GetComponent<RectTransform>().anchorMax = Vector2.one;
            text.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            btn.onClick.AddListener(CloseMenu);
        }

        public void ShowSettings()
        {
            HideAllPanels();
            
            if (_settingsPanel == null)
                CreateSettingsPanel();
            
            _settingsPanel.SetActive(true);
            Debug.Log("Settings shown");
        }

       // في دالة CreateSettingsPanel، قم بتحديث جزء الـ ScrollRect:

private void CreateSettingsPanel()
{
    Debug.Log("Creating Settings Panel...");
    
    _settingsPanel = CreateFullScreenPanel(_overlay.transform, new Color(0.05f, 0.05f, 0.12f));
    
    var container = new GameObject("Container", typeof(RectTransform));
    container.transform.SetParent(_settingsPanel.transform, false);
    
    var containerRt = container.GetComponent<RectTransform>();
    containerRt.anchorMin = new Vector2(0.5f, 0.5f);
    containerRt.anchorMax = new Vector2(0.5f, 0.5f);
    containerRt.pivot = new Vector2(0.5f, 0.5f);
    containerRt.sizeDelta = new Vector2(1100, 850);
    
    // Title
    var title = CreateSimpleText(container.transform, "ENVIRONMENT SETTINGS", 40, new Color(1f, 0.8f, 0f), FontStyle.Bold);
    var titleRt = title.GetComponent<RectTransform>();
    titleRt.anchoredPosition = new Vector2(0, 380);
    titleRt.sizeDelta = new Vector2(1100, 80);

    // Content area with scroll - مع إظهار الـ Scrollbar
    var content = new GameObject("Content", typeof(RectTransform));
    content.transform.SetParent(container.transform, false);
    var contentRt = content.GetComponent<RectTransform>();
    contentRt.anchorMin = new Vector2(0, 0.1f);
    contentRt.anchorMax = new Vector2(1, 0.85f);
    contentRt.offsetMin = new Vector2(30, 30);
    contentRt.offsetMax = new Vector2(-30, -30);

    var scrollRect = content.AddComponent<ScrollRect>();
    scrollRect.horizontal = false;
    scrollRect.vertical = true;
    scrollRect.movementType = ScrollRect.MovementType.Clamped;

    // Viewport
    var viewport = new GameObject("Viewport", typeof(RectTransform));
    viewport.transform.SetParent(content.transform, false);
    var viewportRt = viewport.GetComponent<RectTransform>();
    viewportRt.anchorMin = Vector2.zero;
    viewportRt.anchorMax = Vector2.one;
    viewportRt.sizeDelta = Vector2.zero;
    viewport.AddComponent<RectMask2D>();

    // Scrollbar - مؤشر السحب (أضف هذا الجزء)
    var scrollbarGo = new GameObject("Scrollbar", typeof(RectTransform));
    scrollbarGo.transform.SetParent(content.transform, false);
    var scrollbarRt = scrollbarGo.GetComponent<RectTransform>();
    scrollbarRt.anchorMin = new Vector2(1, 0);
    scrollbarRt.anchorMax = new Vector2(1, 1);
    scrollbarRt.pivot = new Vector2(1, 0.5f);
    scrollbarRt.sizeDelta = new Vector2(20, 0); // شريط أنحف
    
    var scrollbar = scrollbarGo.AddComponent<Scrollbar>();
    scrollbar.direction = Scrollbar.Direction.TopToBottom;
    
    // خلفية الشريط
    var bg = new GameObject("Background", typeof(RectTransform));
    bg.transform.SetParent(scrollbarGo.transform, false);
    var bgRt = bg.GetComponent<RectTransform>();
    bgRt.anchorMin = Vector2.zero;
    bgRt.anchorMax = Vector2.one;
    bgRt.sizeDelta = Vector2.zero;
    var bgImg = bg.AddComponent<Image>();
    bgImg.color = new Color(0.15f, 0.15f, 0.2f);
    
    // منطقة الـ Handle
    var handleArea = new GameObject("HandleArea", typeof(RectTransform));
    handleArea.transform.SetParent(scrollbarGo.transform, false);
    var handleAreaRt = handleArea.GetComponent<RectTransform>();
    handleAreaRt.anchorMin = Vector2.zero;
    handleAreaRt.anchorMax = Vector2.one;
    handleAreaRt.sizeDelta = Vector2.zero;
    
    // الـ Handle نفسه
    var handle = new GameObject("Handle", typeof(RectTransform));
    handle.transform.SetParent(handleArea.transform, false);
    var handleRt = handle.GetComponent<RectTransform>();
    handleRt.anchorMin = new Vector2(0.1f, 0);
    handleRt.anchorMax = new Vector2(0.9f, 1);
    handleRt.sizeDelta = new Vector2(0, 0);
    var handleImg = handle.AddComponent<Image>();
    handleImg.color = new Color(0.4f, 0.4f, 0.6f);
    
    // ربط الـ Scrollbar
    scrollbar.targetGraphic = handleImg;
    scrollbar.handleRect = handleRt;
    scrollRect.verticalScrollbar = scrollbar;
    scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

    // Scroll Content
    var scrollContent = new GameObject("ScrollContent", typeof(RectTransform));
    scrollContent.transform.SetParent(viewport.transform, false);
    var scrollContentRt = scrollContent.GetComponent<RectTransform>();
    scrollContentRt.anchorMin = new Vector2(0, 1);
    scrollContentRt.anchorMax = new Vector2(1, 1);
    scrollContentRt.pivot = new Vector2(0.5f, 1);
    scrollContentRt.sizeDelta = new Vector2(0, 0);
    // إضافة مساحة للـ Scrollbar على اليمين
    scrollContentRt.offsetMin = new Vector2(0, 0);
    scrollContentRt.offsetMax = new Vector2(-25, 0); // مسافة للـ Scrollbar

    var vLayout = scrollContent.AddComponent<VerticalLayoutGroup>();
    vLayout.spacing = 20;
    vLayout.padding = new RectOffset(20, 20, 20, 20);
    vLayout.childForceExpandWidth = true;
    vLayout.childControlHeight = true;
    vLayout.childForceExpandHeight = false;

    var fitter = scrollContent.AddComponent<ContentSizeFitter>();
    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

    scrollRect.viewport = viewportRt;
    scrollRect.content = scrollContentRt;

    // إضافة SettingsPanel
    var settingsComp = _settingsPanel.AddComponent<SettingsPanel>();
    settingsComp.Initialize(scrollContent.transform, _config);

    // Footer
    var footer = new GameObject("Footer", typeof(RectTransform));
    footer.transform.SetParent(container.transform, false);
    var footerRt = footer.GetComponent<RectTransform>();
    footerRt.anchorMin = new Vector2(0, 0);
    footerRt.anchorMax = new Vector2(1, 0);
    footerRt.pivot = new Vector2(0.5f, 0);
    footerRt.anchoredPosition = new Vector2(0, 30);
    footerRt.sizeDelta = new Vector2(0, 80);

    var hLayout = footer.AddComponent<HorizontalLayoutGroup>();
    hLayout.spacing = 30;
    hLayout.padding = new RectOffset(60, 60, 0, 0);

    var backBtn = CreateLargeButton(footer.transform, "◄ BACK", new Color(0.3f, 0.3f, 0.3f));
    backBtn.GetComponent<Button>().onClick.AddListener(ShowMainMenu);
    backBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 60);

    var applyBtn = CreateLargeButton(footer.transform, "✓ APPLY", new Color(0f, 0.8f, 0.8f));
    applyBtn.GetComponent<Button>().onClick.AddListener(() => {
        if (settingsComp != null)
        {
            settingsComp.ApplySettings();
        }
        OnSettingsApplied?.Invoke();
        ShowMainMenu();
    });
    applyBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(250, 60);
    
    CreateCloseButton(_settingsPanel.transform);
    
    Debug.Log("Settings Panel created successfully with Scrollbar!");
}
public void ShowHelp()
{
    HideAllPanels();
    
    if (_helpPanel == null)
        CreateHelpPanel();
    
    _helpPanel.SetActive(true);
    Debug.Log("Help shown");
}

private void CreateHelpPanel()
{
    _helpPanel = CreateFullScreenPanel(_overlay.transform, new Color(0.05f, 0.05f, 0.12f));
    
    var container = new GameObject("Container", typeof(RectTransform));
    container.transform.SetParent(_helpPanel.transform, false);
    
    var containerRt = container.GetComponent<RectTransform>();
    containerRt.anchorMin = new Vector2(0.5f, 0.5f);
    containerRt.anchorMax = new Vector2(0.5f, 0.5f);
    containerRt.pivot = new Vector2(0.5f, 0.5f);
    containerRt.sizeDelta = new Vector2(900, 700);
    
    // Title
    var title = CreateSimpleText(container.transform, "📖 INFORMATION & HELP", 36, new Color(1f, 0.8f, 0f), FontStyle.Bold);
    var titleRt = title.GetComponent<RectTransform>();
    titleRt.anchoredPosition = new Vector2(0, 300);
    titleRt.sizeDelta = new Vector2(900, 60);

    // ==================== TAB BAR ====================
    var tabBar = new GameObject("TabBar", typeof(RectTransform));
    tabBar.transform.SetParent(container.transform, false);
    var tabBarRt = tabBar.GetComponent<RectTransform>();
    tabBarRt.anchorMin = new Vector2(0.5f, 1);
    tabBarRt.anchorMax = new Vector2(0.5f, 1);
    tabBarRt.pivot = new Vector2(0.5f, 1);
    tabBarRt.anchoredPosition = new Vector2(0, -80);
    tabBarRt.sizeDelta = new Vector2(800, 40);
    
    var tabHL = tabBar.AddComponent<HorizontalLayoutGroup>();
    tabHL.spacing = 4;
    tabHL.childForceExpandWidth = true;
    tabHL.childForceExpandHeight = true;
    tabHL.padding = new RectOffset(10, 10, 0, 0);
    
    // Tab buttons
    Button aboutTab = CreateTabButton(tabBar.transform, "📖 About");
    Button controlsTab = CreateTabButton(tabBar.transform, "🎮 Controls");
    Button tipsTab = CreateTabButton(tabBar.transform, "💡 Tips");
    
    // ==================== CONTENT AREA ====================
    var contentArea = new GameObject("ContentArea", typeof(RectTransform));
    contentArea.transform.SetParent(container.transform, false);
    var contentAreaRt = contentArea.GetComponent<RectTransform>();
    contentAreaRt.anchorMin = new Vector2(0.5f, 0.5f);
    contentAreaRt.anchorMax = new Vector2(0.5f, 0.5f);
    contentAreaRt.pivot = new Vector2(0.5f, 0.5f);
    contentAreaRt.anchoredPosition = new Vector2(0, -50);
    contentAreaRt.sizeDelta = new Vector2(820, 440);
    
    // إنشاء الأقسام الثلاثة
    GameObject aboutSection = CreateHelpSection(contentArea.transform, GetAboutText());
    GameObject controlsSection = CreateHelpSection(contentArea.transform, GetControlsText());
    GameObject tipsSection = CreateHelpSection(contentArea.transform, GetTipsText());
    
    // إخفاء الكل ما عدا About
    controlsSection.SetActive(false);
    tipsSection.SetActive(false);
    
    // ربط أزرار التبويب
    aboutTab.onClick.AddListener(() => {
        aboutSection.SetActive(true);
        controlsSection.SetActive(false);
        tipsSection.SetActive(false);
        UpdateTabColors(aboutTab, controlsTab, tipsTab);
    });
    
    controlsTab.onClick.AddListener(() => {
        aboutSection.SetActive(false);
        controlsSection.SetActive(true);
        tipsSection.SetActive(false);
        UpdateTabColors(controlsTab, aboutTab, tipsTab);
    });
    
    tipsTab.onClick.AddListener(() => {
        aboutSection.SetActive(false);
        controlsSection.SetActive(false);
        tipsSection.SetActive(true);
        UpdateTabColors(tipsTab, aboutTab, controlsTab);
    });
    
    // تعيين الحالة الافتراضية
    UpdateTabColors(aboutTab, controlsTab, tipsTab);
    
    // ==================== BACK BUTTON ====================
    var backBtn = CreateLargeButton(container.transform, "◄ BACK TO MENU", new Color(0.3f, 0.3f, 0.3f));
    backBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -320);
    backBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(350, 55);
    backBtn.GetComponent<Button>().onClick.AddListener(ShowMainMenu);
    
    CreateCloseButton(_helpPanel.transform);
}

/// <summary>
/// إنشاء زر تبويب
/// </summary>
private Button CreateTabButton(Transform parent, string label)
{
    var btnGo = new GameObject("Tab_" + label, typeof(RectTransform));
    btnGo.transform.SetParent(parent, false);
    
    var btnImg = btnGo.AddComponent<Image>();
    btnImg.color = new Color(0.15f, 0.15f, 0.25f, 1f);
    
    var btn = btnGo.AddComponent<Button>();
    btn.targetGraphic = btnImg;
    
    var colors = btn.colors;
    colors.normalColor = new Color(0.15f, 0.15f, 0.25f, 1f);
    colors.highlightedColor = new Color(0.25f, 0.25f, 0.4f, 1f);
    colors.pressedColor = new Color(0.1f, 0.1f, 0.2f, 1f);
    colors.selectedColor = new Color(0.15f, 0.35f, 0.65f, 1f);
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
    text.fontSize = 14;
    text.fontStyle = FontStyle.Bold;
    text.color = Color.white;
    text.alignment = TextAnchor.MiddleCenter;
    text.raycastTarget = false;
    
    return btn;
}

/// <summary>
/// تحديث ألوان التبويبات
/// </summary>
private void UpdateTabColors(Button active, Button inactive1, Button inactive2)
{
    active.image.color = new Color(0.15f, 0.35f, 0.65f, 1f);
    inactive1.image.color = new Color(0.15f, 0.15f, 0.25f, 1f);
    inactive2.image.color = new Color(0.15f, 0.15f, 0.25f, 1f);
}

/// <summary>
/// إنشاء قسم مساعدة مع شريط تمرير
/// </summary>
private GameObject CreateHelpSection(Transform parent, string content)
{
    var section = new GameObject("Section", typeof(RectTransform));
    section.transform.SetParent(parent, false);
    
    var sectionRt = section.GetComponent<RectTransform>();
    sectionRt.anchorMin = Vector2.zero;
    sectionRt.anchorMax = Vector2.one;
    sectionRt.sizeDelta = Vector2.zero;
    
    // ScrollView
    var scrollRect = section.AddComponent<ScrollRect>();
    scrollRect.horizontal = false;
    scrollRect.vertical = true;
    scrollRect.movementType = ScrollRect.MovementType.Clamped;
    scrollRect.scrollSensitivity = 20f;
    
    // Viewport
    var viewport = new GameObject("Viewport", typeof(RectTransform));
    viewport.transform.SetParent(section.transform, false);
    var vpRt = viewport.GetComponent<RectTransform>();
    vpRt.anchorMin = Vector2.zero;
    vpRt.anchorMax = Vector2.one;
    vpRt.sizeDelta = Vector2.zero;
    vpRt.offsetMin = new Vector2(5, 5);
    vpRt.offsetMax = new Vector2(-5, -5);
    
    var vpMask = viewport.AddComponent<Mask>();
    vpMask.showMaskGraphic = false;
    
    var vpImg = viewport.AddComponent<Image>();
    vpImg.color = new Color(0.03f, 0.03f, 0.08f, 0.5f);
    
    // Content
    var scrollContent = new GameObject("Content", typeof(RectTransform));
    scrollContent.transform.SetParent(viewport.transform, false);
    var scRt = scrollContent.GetComponent<RectTransform>();
    scRt.anchorMin = new Vector2(0, 1);
    scRt.anchorMax = new Vector2(1, 1);
    scRt.pivot = new Vector2(0.5f, 1);
    scRt.sizeDelta = new Vector2(0, 800);
    
    var text = scrollContent.AddComponent<Text>();
    text.text = content;
    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    text.fontSize = 13;
    text.color = new Color(0.85f, 0.87f, 0.9f);
    text.alignment = TextAnchor.UpperLeft;
    text.supportRichText = true;
    text.lineSpacing = 1.4f;
    text.raycastTarget = true;
    
    var shadow = scrollContent.AddComponent<Shadow>();
    shadow.effectColor = new Color(0, 0, 0, 0.3f);
    shadow.effectDistance = new Vector2(1, -1);
    
    // Content fitter
    var fitter = scrollContent.AddComponent<ContentSizeFitter>();
    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    
    // ربط ScrollRect
    scrollRect.viewport = vpRt;
    scrollRect.content = scRt;
    
    return section;
}

/// <summary>
/// نص عن المحاكاة
/// </summary>
private string GetAboutText()
{
    return "<b><color=#FFD700>🔬 The Swinging Paint Bucket</color></b>\n\n" +
           "A precision physics simulation exploring the\n" +
           "fascinating dynamics of a pendulum with a leak!\n\n" +
           
           "<b><color=#4FC3F7>🌌 Multi-Scale Physics Engine</color></b>\n" +
           "This simulation bridges the gap between\n" +
           "<b>macroscopic</b> and <b>microscopic</b> physics:\n\n" +
           
           "  • <b>Classical Mechanics</b>\n" +
           "    Pendulum motion, gravity, momentum\n\n" +
           
           "  • <b>Fluid Dynamics</b>\n" +
           "    Viscosity, flow rate, nozzle discharge\n\n" +
           
           "  • <b>Atomic-Level Interactions</b>\n" +
           "    Molecular cohesion, surface tension,\n" +
           "    absorption rates at material boundaries\n\n" +
           
           "<b><color=#81C784>🧪 From Atoms to Art</color></b>\n" +
           "Every droplet is calculated considering:\n" +
           "  • Intermolecular forces (van der Waals)\n" +
           "  • Thermal agitation (Brownian motion)\n" +
           "  • Capillary action at nozzle edges\n\n" +
           
           "<b><color=#FFB74D>📐 The Physics Model</color></b>\n" +
           "The pendulum follows the nonlinear equation:\n" +
           "  θ'' + (g/L)sin(θ) + γ·θ' = F_ext(t)\n\n" +
           
           "Paint flow rate is governed by:\n" +
           "  dV/dt = -C_d · A · √(2gh_eff)\n" +
           "Where h_eff considers centrifugal effects!\n\n" +
           
           "<b><color=#CE93D8>🎯 Educational Value</color></b>\n" +
           "Perfect for exploring:\n" +
           "  • Conservation of energy & momentum\n" +
           "  • Damped harmonic oscillators\n" +
           "  • Non-linear dynamics & chaos theory\n" +
           "  • Fluid mechanics principles\n" +
           "  • Computational physics methods";
}

/// <summary>
/// نص تعليمات التحكم
/// </summary>
private string GetControlsText()
{

            
    return "<b><color=#FFD700>⌨️ KEYBOARD SHORTCUTS</color></b>\n\n" +
           
           "<b><color=#4FC3F7>🧪 Experiment Controls</color></b>\n" +
           "  <color=#4CAF50>[R]</color>    Record current experiment\n" +
           "  <color=#4CAF50>[C]</color>    Clear all recorded experiments\n\n" +
           
           "<b><color=#81C784>🖥️ Interface Controls</color></b>\n" +
           "  <color=#4CAF50>[H]</color>    Toggle panel visibility\n\n" +
           "  <color=#4CAF50>[Enter]</color>    Resumes the simulation\n"+
           "  <color=#4CAF50>[Space]</color>    Pauses the simulation\n"+
           "  <color=#4CAF50>[S]</color>    Saves a PNG picture of the paint draw\n\n"+
           "<b><color=#FFB74D>📋 EXPERIMENT PANEL BUTTONS</color></b>\n" +
           "When experiment panel is visible:\n\n" +
           
           "  • <b>Record Button</b>\n" +
           "    Saves current simulation state with\n" +
           "    all parameters (angle, velocity, paint, etc.)\n\n" +
           
           "  • <b>Clear Button</b>\n" +
           "    Removes all recorded experiments\n\n" +
           
           "  • <b>◄ Previous Button</b>\n" +
           "    Navigate to previous experiment\n\n" +
           
           "  • <b>Next Button ►</b>\n" +
           "    Navigate to next experiment\n\n" +
           
           "<b><color=#CE93D8>📊 QUICK SETTINGS PANEL</color></b>\n" +
           "Located on the right side of screen:\n\n" +
           
           "  • <b>Sliders</b> - Adjust physics parameters\n" +
           "    in real-time (Mass, Rope, Gravity, Wind, Angle)\n\n" +
           
           "  • <b>Color Picker</b> - Change paint color\n" +
           "    instantly with visual swatches\n\n" +
           
           "  • <b>✓ APPLY Button</b>\n" +
           "    Confirm and apply all parameter changes\n\n" +
           
           "  • <b>✕ CANCEL Button</b>\n" +
           "    Revert parameters to last applied values\n\n" +
           
           "<b><color=#FFD700>⚙ MENU BUTTON</color></b>\n" +
           "Located at top-left corner:\n\n" +
           
           "  • Opens advanced settings configuration\n" +
           "  • Access to save/load simulation presets\n" +
           "  • Graphics & performance options";
}

/// <summary>
/// نص النصائح والحيل
/// </summary>
private string GetTipsText()
{
    return "<b><color=#FFD700>🌟 PRO TIPS</color></b>\n\n" +
           
           "<b><color=#4FC3F7>🎯 Getting Started</color></b>\n" +
           "  1. Start with default settings to understand\n" +
           "     the basic pendulum behavior\n" +
           "  2. Gradually adjust one parameter at a time\n" +
           "  3. Use the HUD to monitor real-time changes\n" +
           "  4. Record interesting experiments for comparison\n\n" +
           
           "<b><color=#81C784>🔬 Experiment Ideas</color></b>\n" +
           "  • <b>Chaos Explorer:</b> Set angle > 150°\n" +
           "    and observe non-linear behavior\n" +
           "  • <b>Viscosity Test:</b> Compare water (0.001)\n" +
           "    vs honey (10.0) flow patterns\n" +
           "  • <b>Lunar Mode:</b> Set gravity to 1.62 m/s²\n" +
           "    to simulate Moon physics\n" +
           "  • <b>Wind Tunnel:</b> Add high wind force to\n" +
           "    see aerodynamic effects on paint\n" +
           "  • <b>Precision Art:</b> Low viscosity + small\n" +
           "    nozzle = fine, detailed patterns\n\n" +
           
           "<b><color=#FFB74D>⚡ Performance Tips</color></b>\n" +
           "  • Reduce particle count if FPS drops\n" +
           "  • Lower paint resolution for smoother sim\n" +
           "  • Close other applications for CPU headroom\n\n" +
           
           "<b><color=#CE93D8>🎨 Artistic Suggestions</color></b>\n" +
           "  • Mix colors by overlapping swing patterns\n" +
           "  • Create Lissajous-like figures with\n" +
           "    specific angle/gravity combinations\n" +
           "  • Use high viscosity for 3D paint effects\n" +
           "  • Experiment with material absorption rates\n" +
           "    for unique paint-surface interactions\n\n" +
           
           "<b><color=#FF6B6B>⚠️ Common Pitfalls</color></b>\n" +
           "  • Too short rope = unstable simulation\n" +
           "  • Too high mass + low gravity = slow swing\n" +
           "  • Empty bucket still swings - check volume!\n" +
           "  • Extreme wind can blow paint off canvas";
}
        private void HideAllPanels()
        {
            if (_mainMenu != null) _mainMenu.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            if (_helpPanel != null) _helpPanel.SetActive(false);
        }

        private GameObject CreateFullScreenPanel(Transform parent, Color color)
        {
            var go = new GameObject("FullScreenPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = color;

            return go;
        }

        private GameObject CreateSimpleText(Transform parent, string text, int fontSize, Color color, FontStyle style = FontStyle.Normal)
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
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(2, -2);

            return go;
        }

        private GameObject CreateLargeButton(Transform parent, string text, Color bgColor)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(450, 65);

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = bgColor * 1.2f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;

            var textObj = CreateSimpleText(go.transform, text, 24, Color.white, FontStyle.Bold);
            textObj.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            textObj.GetComponent<RectTransform>().anchorMax = Vector2.one;
            textObj.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

            return go;
        }
    }
}