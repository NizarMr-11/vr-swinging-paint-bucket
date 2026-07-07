using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using SwingingPaintBucket.Simulation;
using SwingingPaintBucket.Bucket;
using SwingingPaintBucket.Pendulum;
using SwingingPaintBucket.Canvas;

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

        public float initialAngle;
        public float currentAngularVelocity;
        public float currentAngularAcceleration;
        public float gravity;

        public float experimentDuration;
        public int paintTrajectories;
    }

    [System.Serializable]
    public class ExperimentListWrapper
    {
        public List<ExperimentData> experiments = new List<ExperimentData>();
    }

    public List<ExperimentData> pastExperiments = new List<ExperimentData>();
    private string saveFilePath;
    private float experimentStartTime;
    private bool isTrackingTime = false;
    private bool wasSpilling = false;
    private int trajectoryCount = 0;

    [Header("System References")]
    public SimulationManager simManager;
    public PendulumSimulator pendulum;
    public BucketController bucket;
    public CanvasController canvas;

    IEnumerator Start()
    {
        saveFilePath = Path.Combine(UnityEngine.Application.persistentDataPath, "ExperimentsData.json");
        LoadExperiments();
        yield return null;

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

        if (simManager != null) UnityEngine.Debug.Log("✅ SimulationManager Found!");
        else UnityEngine.Debug.LogError("❌ SimulationManager NOT Found!");

        if (bucket != null) UnityEngine.Debug.Log("✅ Bucket Found!");
        else UnityEngine.Debug.LogError("❌ Bucket NOT Found!");
        if (pendulum != null) UnityEngine.Debug.Log("✅ Pendulum Found!");
        else UnityEngine.Debug.LogError("❌ Pendulum NOT Found!");

        if (canvas != null) canvas.ClearCanvas();

        UnityEngine.Debug.Log("<color=cyan>[System] Ready. Press 'R' to record. Press 'C' to clear.</color>");
    }

    void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                RecordCurrentExperiment("Exp_" + (pastExperiments.Count + 1));
            }

            if (Keyboard.current.cKey.wasPressedThisFrame)
            {
                pastExperiments.Clear();
                if (File.Exists(saveFilePath)) File.Delete(saveFilePath);
                trajectoryCount = 0; // تصفير العداد عند المسح
                UnityEngine.Debug.Log("<color=red>[System] History cleared!</color>");
            }
        }

        if (simManager != null && bucket != null)
        {
            if (bucket.VolumeThisFrame > 0f && !isTrackingTime)
            {
                experimentStartTime = Time.time;
                isTrackingTime = true;
                trajectoryCount = 0; // تصفير المسارات لبداية تجربة نظيفة
                UnityEngine.Debug.Log("<color=green>[Timer] Experiment timer started.</color>");
            }

            if (isTrackingTime)
            {
                if (bucket.VolumeThisFrame > 0f)
                {
                    if (!wasSpilling)
                    {
                        trajectoryCount++; 
                        wasSpilling = true;
                    }
                }
                else
                {
                    wasSpilling = false; 
                }
            }
        }
        else
        {
            if (simManager == null && Time.frameCount % 300 == 0)
            {
                UnityEngine.Debug.LogWarning("Warning: SimulationManager not linked. Time tracking disabled.");
            }
        }
    }

    public void RecordCurrentExperiment(string name)
    {
        if (bucket == null || pendulum == null) return;

        
        float spilledPaint = bucket.InitialPaintVolume - bucket.PaintVolume;
        float paintedArea = (canvas != null) ? canvas.CalculateRealPaintedArea() : 0f;

        
        float initialAngle = pendulum.InitialAngleDegrees;
        float omega = pendulum.Omega;

        float alpha = -(pendulum.Gravity / pendulum.RopeLength) * Mathf.Sin(pendulum.Theta)
                      - (pendulum.DampingCoefficient * omega);

        ExperimentData newExp = new ExperimentData
        {
            experimentName = name,
            ropeLength = pendulum.RopeLength,
            nozzleRadius = bucket.NozzleRadius,
            viscosity = bucket.Viscosity,
            spilledPaint = spilledPaint,
            paintedArea = paintedArea,

            initialAngle = initialAngle,
            currentAngularVelocity = omega,
            currentAngularAcceleration = alpha,
            gravity = pendulum.Gravity,

            experimentDuration = isTrackingTime ? (Time.time - experimentStartTime) : 0f,
            paintTrajectories = trajectoryCount
        };

        pastExperiments.Add(newExp);
        SaveExperiments();
        PrintBeautifulComparison();

        UnityEngine.Debug.Log($"<color=green>[{newExp.experimentName}] Recorded!</color>");
    }

    void PrintBeautifulComparison()
    {
        UnityEngine.Debug.Log("===========================================================================================================");
        UnityEngine.Debug.Log("                                    EXPERIMENT COMPARISON REPORT");
        UnityEngine.Debug.Log("===========================================================================================================");

        foreach (var exp in pastExperiments)
        {
            UnityEngine.Debug.Log($"[ {exp.experimentName} ]");
            UnityEngine.Debug.Log($"  > Factors  : Gravity = {exp.gravity} m/s² | Rope = {exp.ropeLength}m | Nozzle = {exp.nozzleRadius}m | Viscosity = {exp.viscosity}");
            UnityEngine.Debug.Log($"  > Dynamics : Init Angle = {exp.initialAngle}° | ω (Velocity) = {exp.currentAngularVelocity:F3} rad/s | α (Accel) = {exp.currentAngularAcceleration:F3} rad/s²");
            UnityEngine.Debug.Log($"  > Results  : Duration = {exp.experimentDuration:F2} sec | Trajectories = {exp.paintTrajectories} | Spilled = {exp.spilledPaint:F4} L | Area = {exp.paintedArea:F2} m2");
            UnityEngine.Debug.Log("-----------------------------------------------------------------------------------------------------------");
        }
        UnityEngine.Debug.Log("===========================================================================================================");
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
                pastExperiments = wrapper.experiments;
        }
    }
}