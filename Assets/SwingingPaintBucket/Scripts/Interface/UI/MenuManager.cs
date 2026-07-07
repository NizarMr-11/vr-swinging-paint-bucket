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
            containerRt.sizeDelta = new Vector2(900, 750);
            
            var title = CreateSimpleText(container.transform, "CONTROLS HELP", 40, new Color(1f, 0.8f, 0f), FontStyle.Bold);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchoredPosition = new Vector2(0, 330);
            titleRt.sizeDelta = new Vector2(900, 80);

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(container.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 0.1f);
            contentRt.anchorMax = new Vector2(1, 0.85f);
            contentRt.offsetMin = new Vector2(40, 40);
            contentRt.offsetMax = new Vector2(-40, -40);

            var scrollRect = content.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(content.transform, false);
            var viewportRt = viewport.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.sizeDelta = Vector2.zero;
            viewport.AddComponent<RectMask2D>();

            var textObj = CreateSimpleText(viewport.transform, GetHelpText(), 22, Color.white, FontStyle.Normal);
            textObj.GetComponent<RectTransform>().anchorMin = new Vector2(0, 1);
            textObj.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
            textObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1);
            textObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 800);
            textObj.GetComponent<Text>().alignment = TextAnchor.UpperLeft;

            scrollRect.viewport = viewportRt;
            scrollRect.content = textObj.GetComponent<RectTransform>();

            var backBtn = CreateLargeButton(container.transform, "◄ BACK TO MENU", new Color(0.3f, 0.3f, 0.3f));
            backBtn.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -340);
            backBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 60);
            backBtn.GetComponent<Button>().onClick.AddListener(ShowMainMenu);
            
            CreateCloseButton(_helpPanel.transform);
        }

        private string GetHelpText()
        {
            return "🎮 RETRO CONTROLS GUIDE\n\n" +
                   "═══════════════════════════════════════\n\n" +
                   "🔹 MENU BUTTON\n" +
                   "  • Click the MENU button in the top-left\n" +
                   "  • Opens the main menu\n\n" +
                   "🔹 CONTINUE\n" +
                   "  • Returns to the simulation\n" +
                   "  • Closes all menu panels\n\n" +
                   "🔹 CHANGE ENVIRONMENT\n" +
                   "  • Adjust physics parameters:\n" +
                   "    - Mass, Rope Length, Gravity\n" +
                   "    - Damping, Initial Angle\n" +
                   "  • Modify paint properties:\n" +
                   "    - Volume, Viscosity, Density\n" +
                   "  • Change material settings:\n" +
                   "    - Material Type\n" +
                   "    - Discharge Coefficient\n" +
                   "    - Paint Loss Rate\n\n" +
                   "🔹 HELP\n" +
                   "  • Shows this controls guide\n\n" +
                   "🔹 SIMULATION CONTROLS\n" +
                   "  • Watch the pendulum swing\n" +
                   "  • Paint drips from the bucket\n" +
                   "  • Paint trails appear on the ground\n" +
                   "  • Colors can be changed in settings\n\n" +
                   "═══════════════════════════════════════\n" +
                   "PRESS BACK TO RETURN";
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