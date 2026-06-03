using UnityEngine;
using UnityEngine.UI;
using SwingingPaintBucket.Pendulum;
using SwingingPaintBucket.Bucket;
using SwingingPaintBucket.Simulation;
using SwingingPaintBucket.Materials;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace SwingingPaintBucket.Interface
{
    public class SimulationUI : MonoBehaviour
    {
        [Header("Core References")]
        public SimulationManager SimulationManager;
        public GameObject BucketObject;

        // Cached Components
        private global::UnityEngine.Canvas _canvas; 
        private PendulumSimulator _pendulum;
        private BucketController _bucket;
        private GameObject _overlayPanel;
        private bool _uiInitialized = false;

        // UI Controls
        private Slider _massSlider, _ropeLengthSlider, _gravitySlider, _dampingSlider;
        private Slider _initialAngleSlider, _angularVelocitySlider;
        private Slider _paintVolumeSlider, _viscositySlider, _densitySlider, _nozzleRadiusSlider;
        private Slider _dischargeSlider, _paintLossSlider, _absorptionSlider;
        private Dropdown _materialDropdown;
        private Image _paintColorPreview;
        private Color _selectedPaintColor = Color.red;

        // Labels
        private Text _massLabel, _ropeLengthLabel, _gravityLabel, _dampingLabel;
        private Text _initialAngleLabel, _angularVelocityLabel;
        private Text _paintVolumeLabel, _viscosityLabel, _densityLabel, _nozzleRadiusLabel;
        private Text _dischargeLabel, _paintLossLabel, _absorptionLabel;

        // Theme Colors
        private readonly Color _panelColor = new Color(0.12f, 0.12f, 0.15f, 1f);
        private readonly Color _headerColor = new Color(0.15f, 0.15f, 0.2f, 1f);
        private readonly Color _accentColor = new Color(0.20f, 0.59f, 0.96f);
        private readonly Color _textColor = new Color(0.85f, 0.87f, 0.90f);

        // Resources
        private Sprite _whiteSprite;

private void Awake()
{
    // 1. Initialize Resources FIRST
    CreateUIResources();
    
    // 2. Cache Logic References (تم تقديمها هنا لتجهيز البيانات قبل بناء الواجهة)
    if (SimulationManager == null) SimulationManager = FindObjectOfType<SimulationManager>();
    if (BucketObject == null && SimulationManager != null) BucketObject = SimulationManager.BucketObject;

    if (BucketObject != null)
    {
        _pendulum = BucketObject.GetComponent<PendulumSimulator>();
        _bucket = BucketObject.GetComponent<BucketController>();
    }

    // 3. تطبيق القيم المحفوظة من الـ PlayerPrefs مباشرة على الكائنات الفيزيائية
    ApplySavedSettingsToSimulation();

    // 4. Initialize Canvas LAST (الآن ستبنى الواجهة بالقيم المحدثة تماماً)
    BuildInterface();
}
private void ApplySavedSettingsToSimulation()
{
    // تطبيق إعدادات النواس الفيزيائية (Pendulum Physics)
    if (_pendulum != null)
    {
        _pendulum.Mass = PlayerPrefs.GetFloat("Sim_Mass", _pendulum.Mass);
        _pendulum.RopeLength = PlayerPrefs.GetFloat("Sim_RopeLength", _pendulum.RopeLength);
        _pendulum.Gravity = PlayerPrefs.GetFloat("Sim_Gravity", _pendulum.Gravity);
        _pendulum.DampingCoefficient = PlayerPrefs.GetFloat("Sim_Damping", _pendulum.DampingCoefficient);
        _pendulum.InitialAngleDegrees = PlayerPrefs.GetFloat("Sim_InitialAngle", _pendulum.InitialAngleDegrees);
        _pendulum.InitialAngularVelocity = PlayerPrefs.GetFloat("Sim_AngularVelocity", _pendulum.InitialAngularVelocity);
        
        // ملاحظة هامة: إذا كان كلاس PendulumSimulator يحتوي على دالة لتحديث الحبل أو إعادة الحساب برمجياً، 
        // قم بإلغاء التعليق عن السطر التالي واستدعائها هنا:
        // _pendulum.ResetAndApplyParameters();
    }

    // تطبيق إعدادات الطلاء والدلو (Paint Properties & Material)
    if (_bucket != null)
    {
        _bucket.InitialPaintVolume = PlayerPrefs.GetFloat("Sim_PaintVolume", _bucket.InitialPaintVolume);
        _bucket.Viscosity = PlayerPrefs.GetFloat("Sim_Viscosity", _bucket.Viscosity);
        _bucket.Density = PlayerPrefs.GetFloat("Sim_Density", _bucket.Density);
        _bucket.NozzleRadius = PlayerPrefs.GetFloat("Sim_NozzleRadius", _bucket.NozzleRadius);
        _bucket.DischargeCoefficent = PlayerPrefs.GetFloat("Sim_Discharge", _bucket.DischargeCoefficent);
        _bucket.PaintLossRate = PlayerPrefs.GetFloat("Sim_PaintLoss", _bucket.PaintLossRate);
        _bucket.AbsorptionRate = PlayerPrefs.GetFloat("Sim_Absorption", _bucket.AbsorptionRate);
    }
}
        private void Start()
        {
            // Logic updates can happen in Start
            if (_bucket != null) _selectedPaintColor = _bucket.PaintColor;
        }

        // Creates a simple white pixel sprite to use for all UI images
        private void CreateUIResources()
        {
            if (_whiteSprite != null) return; // Already created
            
            Texture2D tex = new Texture2D(2, 2);
            tex.SetPixel(0, 0, Color.white);
            tex.SetPixel(1, 0, Color.white);
            tex.SetPixel(0, 1, Color.white);
            tex.SetPixel(1, 1, Color.white);
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.zero);
        }

        private void BuildInterface()
        {
            if (_canvas == null)
            {
                CreateCanvas();
                CreateMenuButton();
                _uiInitialized = true;
            }
        }

        private void CreateCanvas()
        {
            var canvasGo = new GameObject("SimUI_Canvas");
            canvasGo.transform.SetParent(transform, false);
            
            _canvas = canvasGo.AddComponent<global::UnityEngine.Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }

        private void CreateMenuButton()
        {
            var btnGo = new GameObject("MenuButton", typeof(RectTransform));
            btnGo.transform.SetParent(_canvas.transform, false);
            
            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(50, -50);
            rt.sizeDelta = new Vector2(60, 60);

            var img = btnGo.AddComponent<Image>();
            img.sprite = _whiteSprite;
            img.color = new Color(0.15f, 0.15f, 0.18f, 1f);

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(btnGo.transform, false);
            iconGo.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            iconGo.GetComponent<RectTransform>().anchorMax = Vector2.one;
            iconGo.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            
            var iconTxt = iconGo.AddComponent<Text>();
            iconTxt.text = "\u2699";
            iconTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            iconTxt.fontSize = 32;
            iconTxt.alignment = TextAnchor.MiddleCenter;
            iconTxt.color = Color.white;

            btn.onClick.AddListener(OpenSettings);
        }

        private void OpenSettings()
        {
            if (_canvas == null || _whiteSprite == null) return;
            
            if (_overlayPanel != null) { _overlayPanel.SetActive(true); return; }
            CreateSettingsModal();
        }

        private void CloseSettings()
        {
            if (_overlayPanel != null) _overlayPanel.SetActive(false);
        }

        private void CreateSettingsModal()
        {
            _overlayPanel = new GameObject("Overlay", typeof(RectTransform));
            _overlayPanel.transform.SetParent(_canvas.transform, false);
            var overlayRt = _overlayPanel.GetComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.sizeDelta = Vector2.zero;

            var overlayImg = _overlayPanel.AddComponent<Image>();
            overlayImg.sprite = _whiteSprite;
            overlayImg.color = new Color(0, 0, 0, 0.8f);
            
            var overlayBtn = _overlayPanel.AddComponent<Button>();
            overlayBtn.targetGraphic = overlayImg;
            overlayBtn.onClick.AddListener(CloseSettings);

            var windowGo = new GameObject("Window", typeof(RectTransform));
            windowGo.transform.SetParent(_overlayPanel.transform, false);
            var windowRt = windowGo.GetComponent<RectTransform>();
            windowRt.anchorMin = windowRt.anchorMax = new Vector2(0.5f, 0.5f);
            windowRt.pivot = new Vector2(0.5f, 0.5f);
            windowRt.sizeDelta = new Vector2(700, 750);

            var windowImg = windowGo.AddComponent<Image>();
            windowImg.sprite = _whiteSprite;
            windowImg.color = _panelColor;
            
            var shadow = windowGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.6f);
            shadow.effectDistance = new Vector2(0, -8);

            CreateWindowTitle(windowGo.transform);
            var contentGo = CreateScrollView(windowGo.transform);

            BuildPendulumSection(contentGo.transform);
            BuildBucketSection(contentGo.transform);
            BuildMaterialSection(contentGo.transform);
            BuildColorSection(contentGo.transform);

            CreateFooterButtons(windowGo.transform);
        }

private void CreateWindowTitle(Transform parent)
{
    // 1. Create the Header Background (Image)
    var headerGo = new GameObject("Header", typeof(RectTransform));
    headerGo.transform.SetParent(parent, false);
    var rt = headerGo.GetComponent<RectTransform>();
    rt.anchorMin = new Vector2(0, 1);
    rt.anchorMax = new Vector2(1, 1);
    rt.pivot = new Vector2(0.5f, 1);
    rt.sizeDelta = new Vector2(0, 60);
    rt.anchoredPosition = Vector2.zero;

    var img = headerGo.AddComponent<Image>();
    img.sprite = _whiteSprite;
    img.color = _headerColor;

    // 2. Create a child GameObject for the Text
    var titleTextGo = new GameObject("TitleText", typeof(RectTransform));
    titleTextGo.transform.SetParent(headerGo.transform, false);
    
    // Stretch the text RectTransform to fill the header background
    var textRt = titleTextGo.GetComponent<RectTransform>();
    textRt.anchorMin = Vector2.zero;
    textRt.anchorMax = Vector2.one;
    textRt.sizeDelta = Vector2.zero;

    // 3. Add the Text component to the new child object
    var txt = titleTextGo.AddComponent<Text>();
    txt.text = "SIMULATION SETTINGS";
    txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    txt.fontSize = 22;
    txt.fontStyle = FontStyle.Bold;
    txt.color = _accentColor;
    txt.alignment = TextAnchor.MiddleCenter;
}

private GameObject CreateScrollView(Transform parent)
{
    // 1. التأكد من وجود ScrollRect وتفعيله عمودياً فقط
    ScrollRect scrollRect = parent.GetComponent<ScrollRect>();
    if (scrollRect == null)
    {
        scrollRect = parent.gameObject.AddComponent<ScrollRect>();
    }

    // 2. 👇 حل مشكلة خروج العناصر خارج الإطار 👇
    // إضافة قناع (Mask) لقص وإخفاء العناصر التي تتجاوز حدود الإطار الأب
    if (parent.GetComponent<RectMask2D>() == null)
    {
        parent.gameObject.AddComponent<RectMask2D>();
    }

    // 3. إنشاء كائن الحاوية للمحتوى (Content)
    GameObject contentGo = new GameObject("Content", typeof(RectTransform));
    contentGo.transform.SetParent(parent, false);
    
    RectTransform contentRt = contentGo.GetComponent<RectTransform>();
    
    // ضبط الـ Anchors لتبدأ القائمة من الأعلى وتتمدد لملء العرض
    contentRt.anchorMin = new Vector2(0, 1);
    contentRt.anchorMax = new Vector2(1, 1);
    contentRt.pivot = new Vector2(0.5f, 1);
    contentRt.sizeDelta = new Vector2(0, 0);
    
    // 4. إضافة وإعداد VerticalLayoutGroup
    var vGroup = contentGo.AddComponent<VerticalLayoutGroup>();
    vGroup.padding = new RectOffset(20, 20, 20, 20);
    vGroup.spacing = 15; // مسافة مريحة بين العناصر
    vGroup.childForceExpandWidth = true;
    vGroup.childForceExpandHeight = false;
    vGroup.childControlWidth = true;
    vGroup.childControlHeight = true;

    // 5. إضافة ContentSizeFitter ليتمدد الإطار مع زيادة العناصر
    var fitter = contentGo.AddComponent<ContentSizeFitter>();
    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

    // 6. ربط الـ Content بالـ ScrollRect
    if (scrollRect != null)
    {
        scrollRect.content = contentRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
    }

    return contentGo;
}
private void BuildPendulumSection(Transform parent)
{
    CreateSectionHeader(parent, "PENDULUM PHYSICS");
    
    // فحص الذاكرة أولاً، وإذا كانت فارغة نقرأ من الكائن أو نضع القيمة الافتراضية
    float savedMass = PlayerPrefs.GetFloat("Sim_Mass", _pendulum?.Mass ?? 1f);
    _massSlider = CreateModernSlider(parent, "Mass", 0.1f, 50f, savedMass, "kg", out _massLabel);

    float savedLength = PlayerPrefs.GetFloat("Sim_RopeLength", _pendulum?.RopeLength ?? 5f);
    _ropeLengthSlider = CreateModernSlider(parent, "Rope Length", 0.5f, 20f, savedLength, "m", out _ropeLengthLabel);

    float savedGravity = PlayerPrefs.GetFloat("Sim_Gravity", _pendulum?.Gravity ?? 9.81f);
    _gravitySlider = CreateModernSlider(parent, "Gravity", 0f, 20f, savedGravity, "m/s²", out _gravityLabel);

    float savedDamping = PlayerPrefs.GetFloat("Sim_Damping", _pendulum?.DampingCoefficient ?? 0.05f);
    _dampingSlider = CreateModernSlider(parent, "Damping", 0f, 1f, savedDamping, "", out _dampingLabel);

    float savedAngle = PlayerPrefs.GetFloat("Sim_InitialAngle", _pendulum?.InitialAngleDegrees ?? 45f);
    _initialAngleSlider = CreateModernSlider(parent, "Initial Angle", -180f, 180f, savedAngle, "°", out _initialAngleLabel);

    float savedVelocity = PlayerPrefs.GetFloat("Sim_AngularVelocity", _pendulum?.InitialAngularVelocity ?? 0f);
    _angularVelocitySlider = CreateModernSlider(parent, "Angular Velocity", -10f, 10f, savedVelocity, "rad/s", out _angularVelocityLabel);
    
    CreateSeparator(parent);
}

  private void BuildBucketSection(Transform parent)
{
    CreateSectionHeader(parent, "PAINT PROPERTIES");
    
    float savedVolume = PlayerPrefs.GetFloat("Sim_PaintVolume", _bucket?.InitialPaintVolume ?? 2f);
    _paintVolumeSlider = CreateModernSlider(parent, "Paint Volume", 0.1f, 10f, savedVolume, "L", out _paintVolumeLabel);

    float savedViscosity = PlayerPrefs.GetFloat("Sim_Viscosity", _bucket?.Viscosity ?? 1f);
    _viscositySlider = CreateModernSlider(parent, "Viscosity", 0.1f, 10f, savedViscosity, "", out _viscosityLabel);

    float savedDensity = PlayerPrefs.GetFloat("Sim_Density", _bucket?.Density ?? 1f);
    _densitySlider = CreateModernSlider(parent, "Density", 0.1f, 5f, savedDensity, "g/cm³", out _densityLabel);

    float savedNozzle = PlayerPrefs.GetFloat("Sim_NozzleRadius", _bucket?.NozzleRadius ?? 0.005f);
    _nozzleRadiusSlider = CreateModernSlider(parent, "Nozzle Radius", 0.001f, 0.05f, savedNozzle, "m", out _nozzleRadiusLabel);
    
    CreateSeparator(parent);
}
    private void BuildMaterialSection(Transform parent)
{
    CreateSectionHeader(parent, "MATERIAL & FLOW");
    CreateMaterialDropdown(parent);
    
    // إذا كنت تريد حفظ خيار القائمة المنسدلة وتطبيقه:
    if (_materialDropdown != null)
    {
        _materialDropdown.value = PlayerPrefs.GetInt("Sim_MaterialIndex", _materialDropdown.value);
    }

    float savedDischarge = PlayerPrefs.GetFloat("Sim_Discharge", _bucket?.DischargeCoefficent ?? 0.7f);
    _dischargeSlider = CreateModernSlider(parent, "Discharge Coeff.", 0.1f, 1f, savedDischarge, "", out _dischargeLabel);

    float savedLoss = PlayerPrefs.GetFloat("Sim_PaintLoss", _bucket?.PaintLossRate ?? 0f);
    _paintLossSlider = CreateModernSlider(parent, "Paint Loss Rate", 0f, 0.5f, savedLoss, "", out _paintLossLabel);

    float savedAbsorption = PlayerPrefs.GetFloat("Sim_Absorption", _bucket?.AbsorptionRate ?? 0f);
    _absorptionSlider = CreateModernSlider(parent, "Absorption Rate", 0f, 0.1f, savedAbsorption, "", out _absorptionLabel);
    
    CreateSeparator(parent);
}

        private void BuildColorSection(Transform parent)
        {
            CreateSectionHeader(parent, "APPEARANCE");
            CreateColorPickerRow(parent);
        }

        private void CreateSectionHeader(Transform parent, string title)
        {
            var go = new GameObject("Header_" + title, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 30;

            var txt = go.AddComponent<Text>();
            txt.text = title;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 14;
            txt.fontStyle = FontStyle.Bold;
            txt.color = _accentColor;
            txt.alignment = TextAnchor.MiddleLeft;
        }

        private void CreateSeparator(Transform parent)
        {
            var go = new GameObject("Sep", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 1;
            
            var img = go.AddComponent<Image>();
            img.sprite = _whiteSprite;
            img.color = new Color(1, 1, 1, 0.1f);
        }

    private Slider CreateModernSlider(Transform parent, string label, float min, float max, float current, string unit, out Text valueLabel)
{
    // 1. الحاوية الرئيسية للصف
    var rowGo = new GameObject("Row_" + label, typeof(RectTransform));
    rowGo.transform.SetParent(parent, false);

    var vGroup = rowGo.AddComponent<VerticalLayoutGroup>();
    vGroup.spacing = 6;
    vGroup.padding = new RectOffset(0, 0, 4, 4);
    vGroup.childForceExpandWidth = true;
    vGroup.childForceExpandHeight = false;
    vGroup.childControlWidth = true;
    vGroup.childControlHeight = true;

    // 2. الصف العلوي (العنوان + القيمة الحالية)
    var topRow = new GameObject("TopRow", typeof(RectTransform));
    topRow.transform.SetParent(rowGo.transform, false);
    topRow.AddComponent<LayoutElement>().preferredHeight = 20;
    
    var topHGroup = topRow.AddComponent<HorizontalLayoutGroup>();
    topHGroup.childForceExpandWidth = true;
    topHGroup.childForceExpandHeight = false;
    topHGroup.childControlWidth = true;
    topHGroup.childControlHeight = true;

    // نص اسم الخاصية
    var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
    labelGo.transform.SetParent(topRow.transform, false);
    var labelText = labelGo.GetComponent<Text>();
    labelText.text = label;
    labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); 
    labelText.fontSize = 14;
    labelText.color = Color.white;
    labelText.alignment = TextAnchor.MiddleLeft;

    // نص القيمة الرقمية
    var valueGo = new GameObject("Value", typeof(RectTransform), typeof(Text));
    valueGo.transform.SetParent(topRow.transform, false);
    valueLabel = valueGo.GetComponent<Text>();
    valueLabel.text = current.ToString("0.0") + " " + unit;
    valueLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    valueLabel.fontSize = 14;
    valueLabel.color = new Color(0.7f, 0.8f, 1f); // لون أزرق مائل للبياض خفيف
    valueLabel.alignment = TextAnchor.MiddleRight;

    // 3. حاوية شريط التمرير (Slider Row)
    var sliderRow = new GameObject("SliderRow", typeof(RectTransform));
    sliderRow.transform.SetParent(rowGo.transform, false);
    sliderRow.AddComponent<LayoutElement>().preferredHeight = 24;
    
    var sliderHGroup = sliderRow.AddComponent<HorizontalLayoutGroup>();
    sliderHGroup.childForceExpandWidth = true;
    sliderHGroup.childForceExpandHeight = false;
    sliderHGroup.childControlWidth = true;
    sliderHGroup.childControlHeight = true;

    // 4. كائن الـ Slider الأساسي
    var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
    sliderGo.transform.SetParent(sliderRow.transform, false);
    
    // 👇 --- الحل السحري لإظهار السلايدر --- 👇
    // إجبار الـ Layout Group على إعطاء مساحة للسلايدر ليملأ العرض بارتفاع مناسب
    var sliderLayout = sliderGo.AddComponent<LayoutElement>();
    sliderLayout.flexibleWidth = 1f;       // يملأ كل المساحة الأفقية المتبقية
    sliderLayout.preferredHeight = 20f;     // تحديد ارتفاع السلايدر
    // 👆 --------------------------------- 👆

    var slider = sliderGo.GetComponent<Slider>();
    
    // خلفية الشريط (الرمادي الداكن)
    var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
    bgGo.transform.SetParent(sliderGo.transform, false);
    var bgImage = bgGo.GetComponent<Image>();
    bgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f); 
    var bgRt = bgGo.GetComponent<RectTransform>();
    bgRt.anchorMin = new Vector2(0, 0.3f);
    bgRt.anchorMax = new Vector2(1, 0.7f);
    bgRt.sizeDelta = Vector2.zero;

    // منطقة الملء (Fill Area)
    var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
    fillAreaGo.transform.SetParent(sliderGo.transform, false);
    var fillAreaRt = fillAreaGo.GetComponent<RectTransform>();
    fillAreaRt.anchorMin = new Vector2(0, 0.3f);
    fillAreaRt.anchorMax = new Vector2(1, 0.7f);
    fillAreaRt.sizeDelta = Vector2.zero;

    // جزء الملء المتحرك (الأزرق)
    var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
    fillGo.transform.SetParent(fillAreaGo.transform, false);
    var fillImage = fillGo.GetComponent<Image>();
    fillImage.color = new Color(0.2f, 0.55f, 1f, 1f); 
    var fillRt = fillGo.GetComponent<RectTransform>();
    fillRt.sizeDelta = Vector2.zero;

    // ربط المكونات برمجياً بالـ Slider
    slider.targetGraphic = bgImage;
    slider.fillRect = fillRt;
    slider.minValue = min;
    slider.maxValue = max;
    slider.value = current;

    // تحديث النص تلقائياً عند تحريك السلايدر
    Text localValueLabel = valueLabel; 
    slider.onValueChanged.AddListener((val) => 
    {
        localValueLabel.text = val.ToString("0.0") + " " + unit;
    });

    return slider;
}
        private void CreateMaterialDropdown(Transform parent)
        {
            var rowGo = new GameObject("MaterialRow", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var hGroup = rowGo.AddComponent<HorizontalLayoutGroup>();
            hGroup.childForceExpandWidth = true; hGroup.spacing = 15;
            rowGo.AddComponent<LayoutElement>().preferredHeight = 35;

            var labelTxt = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
            labelTxt.transform.SetParent(rowGo.transform, false);
            labelTxt.text = "Material Type";
            labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelTxt.fontSize = 13;
            labelTxt.color = _textColor;

            var ddGo = new GameObject("Dropdown", typeof(RectTransform));
            ddGo.transform.SetParent(rowGo.transform, false);
            ddGo.AddComponent<LayoutElement>().preferredWidth = 200;
            var ddImg = ddGo.AddComponent<Image>();
            ddImg.sprite = _whiteSprite;
            ddImg.color = new Color(0.18f, 0.18f, 0.2f);
            var dd = ddGo.AddComponent<Dropdown>();

            var templateGo = new GameObject("Template", typeof(RectTransform));
            templateGo.transform.SetParent(ddGo.transform, false);
            templateGo.SetActive(false);
            var templateRt = templateGo.GetComponent<RectTransform>();
            templateRt.anchorMin = new Vector2(0, 0);
            templateRt.anchorMax = new Vector2(1, 0);
            templateRt.pivot = new Vector2(0.5f, 1);
            templateRt.anchoredPosition = new Vector2(0, -2);
            templateRt.sizeDelta = new Vector2(0, 150);

            var templateBg = templateGo.AddComponent<Image>();
            templateBg.sprite = _whiteSprite;
            templateBg.color = new Color(0.15f, 0.15f, 0.18f);

            var scroll = templateGo.AddComponent<ScrollRect>();
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(templateGo.transform, false);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            scroll.content = content;
            scroll.viewport = templateRt;

            var item = new GameObject("Item", typeof(RectTransform));
            item.transform.SetParent(content, false);
            item.AddComponent<LayoutElement>().preferredHeight = 30;
            var itemBg = item.AddComponent<Image>();
            itemBg.sprite = _whiteSprite;
            itemBg.color = new Color(0.2f, 0.2f, 0.25f);
            
            var toggle = item.AddComponent<Toggle>();
            toggle.targetGraphic = itemBg;
            var tCol = toggle.colors;
            tCol.highlightedColor = new Color(0.3f, 0.3f, 0.4f);
            toggle.colors = tCol;
            
            var itemTxt = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
            itemTxt.transform.SetParent(item.transform, false);
            itemTxt.GetComponent<RectTransform>().anchorMin = Vector2.zero; itemTxt.GetComponent<RectTransform>().anchorMax = Vector2.one;
            itemTxt.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            itemTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            itemTxt.color = Color.white;

            dd.template = templateRt;
            dd.captionText = itemTxt; 
            dd.itemText = itemTxt;
            
            dd.ClearOptions();
            var options = new List<string>();
            foreach (BucketMaterialType m in System.Enum.GetValues(typeof(BucketMaterialType))) options.Add(m.ToString());
            dd.AddOptions(options);
            if (_bucket != null) dd.value = (int)_bucket.MaterialType;

            _materialDropdown = dd;
        }

        private void CreateColorPickerRow(Transform parent)
        {
            var rowGo = new GameObject("ColorRow", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            var hGroup = rowGo.AddComponent<HorizontalLayoutGroup>();
            hGroup.childForceExpandWidth = true; hGroup.spacing = 15;
            rowGo.AddComponent<LayoutElement>().preferredHeight = 40;

            var labelTxt = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
            labelTxt.transform.SetParent(rowGo.transform, false);
            labelTxt.text = "Paint Color";
            labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelTxt.fontSize = 13;
            labelTxt.color = _textColor;

            var swatchGo = new GameObject("Swatch", typeof(RectTransform));
            swatchGo.transform.SetParent(rowGo.transform, false);
            swatchGo.AddComponent<LayoutElement>().preferredWidth = 50; swatchGo.AddComponent<LayoutElement>().preferredHeight = 30;
            _paintColorPreview = swatchGo.AddComponent<Image>();
            _paintColorPreview.sprite = _whiteSprite;
            _paintColorPreview.color = _selectedPaintColor;
            
            var btn = swatchGo.AddComponent<Button>();
            btn.onClick.AddListener(CycleColor);
        }

private void CreateFooterButtons(Transform parent)
{
    var footerGo = new GameObject("Footer", typeof(RectTransform));
    footerGo.transform.SetParent(parent, false);
    var rt = footerGo.GetComponent<RectTransform>();
    rt.anchorMin = new Vector2(0, 0); 
    rt.anchorMax = new Vector2(1, 0);
    rt.pivot = new Vector2(0.5f, 0); 
    rt.anchoredPosition = new Vector2(0, 15); 
    rt.sizeDelta = new Vector2(0, 50);

    var hGroup = footerGo.AddComponent<HorizontalLayoutGroup>();
    hGroup.padding = new RectOffset(40, 40, 0, 0); 
    hGroup.spacing = 20; 
    hGroup.childForceExpandWidth = true;
    
    // 👇 --- التعديل المباشر لحل الخطأ وتنسيق الواجهة --- 👇
    
    // 1. استدعاء تابع إنشاء زر الحفظ وتمرير الـ footerGo كأب له ليكون بجانب زر Cancel
    CreateSaveButton(footerGo.transform);

    // 2. إنشاء زر الإلغاء Cancel كما هو في كودك الأصلي
    var cancelBtn = CreateButton("CANCEL", new Color(0.25f, 0.25f, 0.25f));
    cancelBtn.transform.SetParent(footerGo.transform, false);
    cancelBtn.GetComponent<Button>().onClick.AddListener(OnCancel);
}

        private GameObject CreateButton(string text, Color bgColor)
        {
            var go = new GameObject("Btn_" + text, typeof(RectTransform));
            var img = go.AddComponent<Image>();
            img.sprite = _whiteSprite;
            img.color = bgColor;
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.2f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;

            var txtGo = new GameObject("Txt", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            txtGo.GetComponent<RectTransform>().anchorMin = Vector2.zero; txtGo.GetComponent<RectTransform>().anchorMax = Vector2.one;
            txtGo.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            var txt = txtGo.AddComponent<Text>();
            txt.text = text;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 16; txt.fontStyle = FontStyle.Bold; txt.color = Color.white; txt.alignment = TextAnchor.MiddleCenter;

            return go;
        }

        private void CycleColor()
        {
            Color[] palette = { Color.red, Color.blue, Color.green, Color.yellow, Color.cyan, Color.magenta };
            int index = 0;
            for (int i = 0; i < palette.Length; i++) { if (palette[i] == _selectedPaintColor) { index = i; break; } }
            index = (index + 1) % palette.Length;
            _selectedPaintColor = palette[index];
            _paintColorPreview.color = _selectedPaintColor;
        }

private void CreateSaveButton(Transform parent)
{
    GameObject btnGo = new GameObject("SaveButton", typeof(RectTransform), typeof(Image), typeof(Button));
    btnGo.transform.SetParent(parent, false);

    var layoutElement = btnGo.AddComponent<LayoutElement>();
    layoutElement.preferredHeight = 42f;
    layoutElement.flexibleWidth = 1f;

    // تلوين الزر باللون الأزرق المميز للثيم لديك (_accentColor)
    Image btnImage = btnGo.GetComponent<Image>();
    btnImage.color = _accentColor; 

    GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
    textGo.transform.SetParent(btnGo.transform, false);
    
    Text btnText = textGo.GetComponent<Text>();
    btnText.text = "حفظ وإعادة تشغيل المحاكاة";
    btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    btnText.fontSize = 15;
    btnText.color = Color.white;
    btnText.alignment = TextAnchor.MiddleCenter;

    RectTransform textRt = btnText.rectTransform;
    textRt.anchorMin = Vector2.zero;
    textRt.anchorMax = Vector2.one;
    textRt.sizeDelta = Vector2.zero;

    Button button = btnGo.GetComponent<Button>();
    button.onClick.AddListener(() =>
    {
        // 1. حفظ القيم
        ApplyNewSettings();
        
        // 2. إعادة تحميل اللعبة من الصفر
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    });
}
private void ApplyNewSettings()
{
    // حفظ خصائص النواس (Pendulum)
    if (_massSlider != null) PlayerPrefs.SetFloat("Sim_Mass", _massSlider.value);
    if (_ropeLengthSlider != null) PlayerPrefs.SetFloat("Sim_RopeLength", _ropeLengthSlider.value);
    if (_gravitySlider != null) PlayerPrefs.SetFloat("Sim_Gravity", _gravitySlider.value);
    if (_dampingSlider != null) PlayerPrefs.SetFloat("Sim_Damping", _dampingSlider.value);
    if (_initialAngleSlider != null) PlayerPrefs.SetFloat("Sim_InitialAngle", _initialAngleSlider.value);
    if (_angularVelocitySlider != null) PlayerPrefs.SetFloat("Sim_AngularVelocity", _angularVelocitySlider.value);

    // حفظ خصائص الطلاء والسطح (Paint & Fluid)
    if (_paintVolumeSlider != null) PlayerPrefs.SetFloat("Sim_PaintVolume", _paintVolumeSlider.value);
    if (_viscositySlider != null) PlayerPrefs.SetFloat("Sim_Viscosity", _viscositySlider.value);
    if (_densitySlider != null) PlayerPrefs.SetFloat("Sim_Density", _densitySlider.value);
    if (_nozzleRadiusSlider != null) PlayerPrefs.SetFloat("Sim_NozzleRadius", _nozzleRadiusSlider.value);
    if (_dischargeSlider != null) PlayerPrefs.SetFloat("Sim_Discharge", _dischargeSlider.value);
    if (_paintLossSlider != null) PlayerPrefs.SetFloat("Sim_PaintLoss", _paintLossSlider.value);
    if (_absorptionSlider != null) PlayerPrefs.SetFloat("Sim_Absorption", _absorptionSlider.value);

    // حفظ خيار القائمة المنسدلة للمادة إن وجد
    if (_materialDropdown != null) PlayerPrefs.SetInt("Sim_MaterialIndex", _materialDropdown.value);

    // حفظ التعديلات نهائياً
    PlayerPrefs.Save();
    Debug.Log("تم حفظ جميع الإعدادات الجديدة بنجاح!");
}

        private void OnSave()
        {
            ApplySettings();
            SimulationManager?.ResetSimulation();
            _bucket?.SyncPaintVolume();
            SimulationManager?.StartSimulation();
            CloseSettings();
        }

        private void OnCancel() => CloseSettings();

        private void ApplySettings()
        {
            if (_pendulum != null)
            {
                _pendulum.Mass = _massSlider.value;
                _pendulum.RopeLength = _ropeLengthSlider.value;
                _pendulum.Gravity = _gravitySlider.value;
                _pendulum.DampingCoefficient = _dampingSlider.value;
                _pendulum.InitialAngleDegrees = _initialAngleSlider.value;
                _pendulum.InitialAngularVelocity = _angularVelocitySlider.value;
            }
            if (_bucket != null)
            {
                _bucket.InitialPaintVolume = _paintVolumeSlider.value;
                _bucket.Viscosity = _viscositySlider.value;
                _bucket.Density = _densitySlider.value;
                _bucket.NozzleRadius = _nozzleRadiusSlider.value;
                _bucket.MaterialType = (BucketMaterialType)_materialDropdown.value;
                _bucket.DischargeCoefficent = _dischargeSlider.value;
                _bucket.PaintLossRate = _paintLossSlider.value;
                _bucket.AbsorptionRate = _absorptionSlider.value;
                _bucket.PaintColor = _selectedPaintColor;
            }
        }
    }
}