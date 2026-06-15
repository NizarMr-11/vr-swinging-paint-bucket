using SwingingPaintBucket.Bucket;
using SwingingPaintBucket.Canvas;
using SwingingPaintBucket.Pendulum;
using SwingingPaintBucket.Simulation;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class ExperimentManager : MonoBehaviour
{
    [System.Serializable]
    public class ExperimentData
    {
        public string experimentName;
        public float ropeLength;
        public float nozzleRadius;
        public float viscosity;
        public float spilledPaint;
        public float paintedArea;
    }

    [System.Serializable]
    public class ExperimentListWrapper
    {
        public List<ExperimentData> experiments = new List<ExperimentData>();
    }

    public List<ExperimentData> pastExperiments = new List<ExperimentData>();

    // مسار الملف الذي سيحفظ التجارب على جهازك
    private string saveFilePath;

    [Header("Auto-Linked References")]
    public SimulationManager simManager;
    public PendulumSimulator pendulum;
    public BucketController bucket;
    public CanvasController canvas;

    IEnumerator Start() // لاحظ وجود IEnumerator هنا لعمل توقيت الانتظار
    {
        saveFilePath = Path.Combine(Application.persistentDataPath, "ExperimentsData.json");

        // 1. تحميل التجارب القديمة
        LoadExperiments();

        // 2. ننتظر إطار واحد فقط (جزء من ثانية) ليكتمل استيقاظ باقي السكريبتات
        yield return null;

        // 3. الآن نربط الأكواد بأمان
        simManager = FindAnyObjectByType<SimulationManager>();
        if (simManager != null && simManager.BucketObject != null)
        {
            pendulum = simManager.BucketObject.GetComponent<PendulumSimulator>();
            bucket = simManager.BucketObject.GetComponent<BucketController>();
        }
        else
        {
            bucket = FindAnyObjectByType<BucketController>();
            if (bucket != null) pendulum = bucket.GetComponent<PendulumSimulator>();
        }
        canvas = FindAnyObjectByType<CanvasController>();

        // 4. الآن مسح المنصة بأمان تام (لن يظهر الخطأ الأحمر)
        if (canvas != null) canvas.ClearCanvas();

        Debug.Log("<color=cyan>[System] Ready. Press 'R' to record. Press 'C' to clear history.</color>");
    }

    void Update()
    {
        if (Keyboard.current != null)
        {
            // الضغط على R لتسجيل ومقارنة التجربة
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                RecordCurrentExperiment("Exp_" + (pastExperiments.Count + 1));
            }

            // الضغط على C لمسح تاريخ المقارنات وبدء صفحة جديدة
            if (Keyboard.current.cKey.wasPressedThisFrame)
            {
                pastExperiments.Clear();
                if (File.Exists(saveFilePath)) File.Delete(saveFilePath);
                Debug.Log("<color=red>[System] Comparison history cleared! Starting fresh.</color>");
            }
        }
    }

    public void RecordCurrentExperiment(string name)
    {
        if (bucket == null || pendulum == null) return;

        float spilledPaint = bucket.InitialPaintVolume - bucket.PaintVolume;
        float paintedArea = (canvas != null) ? canvas.CalculateRealPaintedArea() : 0f;

        ExperimentData newExp = new ExperimentData
        {
            experimentName = name,
            ropeLength = pendulum.RopeLength,
            nozzleRadius = bucket.NozzleRadius,
            viscosity = bucket.Viscosity,
            spilledPaint = spilledPaint,
            paintedArea = paintedArea
        };

        pastExperiments.Add(newExp);

        // حفظ في الملف فوراً
        SaveExperiments();

        // طباعة تقرير المقارنة الجديد
        PrintBeautifulComparison();
    }

    void SaveExperiments()
    {
        ExperimentListWrapper wrapper = new ExperimentListWrapper { experiments = pastExperiments };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(saveFilePath, json);
    }

    void LoadExperiments()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            ExperimentListWrapper wrapper = JsonUtility.FromJson<ExperimentListWrapper>(json);
            if (wrapper != null && wrapper.experiments != null)
            {
                pastExperiments = wrapper.experiments;
            }
        }
    }

    void PrintBeautifulComparison()
    {
        Debug.Log("====================================================");
        Debug.Log("           EXPERIMENT COMPARISON REPORT");
        Debug.Log("====================================================");

        foreach (var exp in pastExperiments)
        {
            Debug.Log($"[ {exp.experimentName} ]");
            Debug.Log($"  > Rope: {exp.ropeLength}m  |  Nozzle: {exp.nozzleRadius}m  |  Viscosity: {exp.viscosity}");
            Debug.Log($"  > RESULT: Spilled = {exp.spilledPaint:F4} L  |  Painted Area = {exp.paintedArea:F2} m2");
            Debug.Log("----------------------------------------------------");
        }
        Debug.Log("====================================================");
    }
}




//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.InputSystem; // نظام الإدخال الجديد المتوافق مع المشروع
//using SwingingPaintBucket.Simulation;
//using SwingingPaintBucket.Bucket;
//using SwingingPaintBucket.Pendulum;
//using SwingingPaintBucket.Canvas;

//public class ExperimentManager : MonoBehaviour
//{
//    [System.Serializable]
//    public class ExperimentData
//    {
//        public string experimentName;
//        public float ropeLength;
//        public float nozzleRadius;
//        public float viscosity;
//        public float spilledPaint;
//        public float paintedArea;
//    }

//    public List<ExperimentData> pastExperiments = new List<ExperimentData>();

//    [Header("Auto-Linked References (Read Only)")]
//    public SimulationManager simManager;
//    public PendulumSimulator pendulum;
//    public BucketController bucket;
//    public CanvasController canvas;

//    void Start()
//    {
//        // 1. محاولة إيجاد مدير المحاكاة أولاً (تم التحديث للإصدار الحديث)
//        simManager = FindAnyObjectByType<SimulationManager>();

//        if (simManager != null && simManager.BucketObject != null)
//        {
//            pendulum = simManager.BucketObject.GetComponent<PendulumSimulator>();
//            bucket = simManager.BucketObject.GetComponent<BucketController>();
//        }
//        else
//        {
//            // 2. إذا لم يجده، يبحث في المشهد كله عن الدلو والبندول مباشرة
//            bucket = FindAnyObjectByType<BucketController>();
//            if (bucket != null)
//                pendulum = bucket.GetComponent<PendulumSimulator>();
//        }

//        // 3. البحث الشامل عن المنصة البيضاء
//        canvas = FindAnyObjectByType<CanvasController>();

//        // 4. رسائل التشخيص
//        if (bucket != null) Debug.Log("[ExpManager] ✅ Found Bucket!");
//        else Debug.LogError("[ExpManager] ❌ Bucket NOT FOUND in scene!");

//        if (pendulum != null) Debug.Log("[ExpManager] ✅ Found Pendulum!");
//        else Debug.LogError("[ExpManager] ❌ Pendulum NOT FOUND in scene!");

//        if (canvas != null) Debug.Log("[ExpManager] ✅ Found Canvas!");
//        else Debug.LogError("[ExpManager] ❌ Canvas NOT FOUND in scene!");

//        Debug.Log("[ExperimentManager] System Auto-Linked Successfully!");
//    }


//    void Update()
//    {
//        // للتأكد من أن يونيتي يستقبل ضغطاتك
//        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
//        {
//            Debug.Log("<color=yellow>[ExpManager] You pressed R! Attempting to record...</color>");
//            RecordCurrentExperiment("Real Test " + (pastExperiments.Count + 1));
//        }
//    }

//    public void RecordCurrentExperiment(string name)
//    {
//        if (bucket == null || pendulum == null) return;

//        // 1. حساب كمية الطلاء المتساقط الحقيقية
//        float spilledPaint = bucket.InitialPaintVolume - bucket.PaintVolume;

//        // 2. حساب مساحة التلوين الحقيقية من المنصة
//        float paintedArea = 0f;
//        if (canvas != null)
//        {
//            paintedArea = canvas.CalculateRealPaintedArea();
//        }

//        // 3. حفظ بيانات التجربة
//        ExperimentData newExp = new ExperimentData
//        {
//            experimentName = name,
//            ropeLength = pendulum.RopeLength,
//            nozzleRadius = bucket.NozzleRadius,
//            viscosity = bucket.Viscosity,
//            spilledPaint = spilledPaint,
//            paintedArea = paintedArea
//        };

//        pastExperiments.Add(newExp);

//        // 4. طباعة المقارنة
//        PrintComparison();
//    }

//    void PrintComparison()
//    {
//        Debug.Log("==================================================");
//        Debug.Log("     REAL Experiment Comparison System");
//        Debug.Log("==================================================");

//        foreach (var exp in pastExperiments)
//        {
//            Debug.Log($"[{exp.experimentName}] => Rope: {exp.ropeLength}m | Nozzle: {exp.nozzleRadius}m | Viscosity: {exp.viscosity}");
//            Debug.Log($"   -> Spilled: {exp.spilledPaint:F4} L  |  REAL Painted Area: {exp.paintedArea:F2} m2");
//        }
//        Debug.Log("==================================================");
//    }
//}