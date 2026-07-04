using System;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonicEngine.Diagnostics;
using HarmonicEngine.Domain.Models;
using HarmonicEngine.Infrastructure.Management;
using HarmonicEngine.Testing;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Hole-opening regression: surface-clipped holes must not form interior columns/tunnels, and fluid
/// must be able to exit through authored openings.
/// </summary>
[Category("Regression")]
public class HoleOpeningTests
{
    const float DeltaTime = 1f / 60f;
    const float Radius = 2.0f;
    const float Height = 3.0f;
    const float CanvasPlaneY = -1.0f;
    const float ClipDepthScale = 1.0f;
    const float FloorTol = 0.01f;
    const float RadiusEps = 1e-4f;

    const uint PARTICIPATES = 1u << 0;
    const uint THROUGH_HOLE = 1u << 5;

    [Test]
    public void SetContainerHoles_ProjectsAndPacksParityReferenceHoles()
    {
        OtcContainerHole[] authored = BuildAuthoredTwoHolesLab();
        Assert.IsTrue(OtcHoleSetupUtility.TryProject(authored[0], Radius, Height, out var floor));
        Assert.IsTrue(OtcHoleSetupUtility.TryProject(authored[1], Radius, Height, out var side));

        Assert.AreEqual(OtcHoleSurface.Floor, floor.surface);
        Assert.AreEqual(0f, floor.localPosition.y, 1e-5f);

        float expectedSideRadius = new Vector2(side.localPosition.x, side.localPosition.z).magnitude;
        Assert.AreEqual(Radius, expectedSideRadius, 1e-3f);

        var holesVec = new Vector4[OtcHoleSetupUtility.MaxHoles];
        var axisVec = new Vector4[OtcHoleSetupUtility.MaxHoles];
        int count = OtcHoleSetupUtility.PackProjectedHoles(authored, Radius, Height, holesVec, axisVec);
        Assert.AreEqual(2, count);

        Hole[] refHoles = BuildParityReferenceHoles();
        Assert.Less(Vector3.Distance(refHoles[0].position, new Vector3(holesVec[0].x, holesVec[0].y, holesVec[0].z)), 1e-4f);
        Assert.AreEqual(refHoles[0].radius, holesVec[0].w, 1e-4f);
        Assert.Less(Vector3.Distance(refHoles[1].position, new Vector3(holesVec[1].x, holesVec[1].y, holesVec[1].z)), 1e-4f);
        Assert.AreEqual(refHoles[1].radius, holesVec[1].w, 1e-4f);
    }

    [Test]
    public void InteriorCup_OutsideHoleClips_HasNoThroughHoleOrLegacyLeak()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        Hole[] holes = BuildParityReferenceHoles();
        var pts = BuildInteriorGridExcludingHoleClips(holes);
        BatchParityResult[] res = DispatchBatchParity(pts, holes);

        int throughHoleInsideCup = 0;
        int legacyLeak = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            if ((res[i].fieldRegion & THROUGH_HOLE) != 0)
            {
                throughHoleInsideCup++;
            }

            if (!InAnyHoleClip(pts[i], holes))
            {
                uint legacy = res[i].legacyBits;
                uint region = res[i].fieldRegion;
                bool participatesOk = ((region & PARTICIPATES) != 0) == ((legacy & (1u << 0)) != 0);
                if (!participatesOk)
                {
                    legacyLeak++;
                }
            }
        }

        Debug.Log($"[HoleOpening:NoTunnel] interiorPoints={pts.Count} throughHole={throughHoleInsideCup} legacyLeak={legacyLeak}");
        Assert.AreEqual(0, throughHoleInsideCup, "Interior cup (outside clip volumes) must not show THROUGH_HOLE.");
        Assert.AreEqual(0, legacyLeak, "Hole CSG leaked past clip volumes into the interior column.");
    }

    [Test]
    public void Particle_ExitsThroughFloorHoleOpening()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        HarmonicPipelineController pipeline = CreatePipeline();
        try
        {
            pipeline.SetCanvasPlaneY(CanvasPlaneY);
            pipeline.SetCanvasCullingEnabled(true);
            pipeline.SetGravity(new Vector3(0f, -25f, 0f));
            SetupContainerWithFloorHole(pipeline, out Matrix4x4 localToWorld);
            Assert.AreEqual(1, pipeline.OtcHoleCount, "Floor-hole uniforms must be packed before sim.");

            Vector3 start = new Vector3(0.4f, 0.08f, -0.2f);
            SpawnParticles(pipeline, new[] { MakeParticle(start, new Vector3(0f, -0.5f, 0f)) });

            float minY = start.y;
            bool fellThroughFloor = false;

            for (int frame = 0; frame < 90; frame++)
            {
                StepStationaryFrame(pipeline, localToWorld);
                ReadParticle(pipeline, 0, out Vector3 pos, out _);
                minY = Mathf.Min(minY, pos.y);
                float localY = LocalHeight(pos, localToWorld);
                if (localY < -FloorTol)
                {
                    fellThroughFloor = true;
                    break;
                }
            }

            Debug.Log($"[HoleOpening:FloorExit] startY={start.y:F4} minY={minY:F4} fellThrough={fellThroughFloor}");
            Assert.IsTrue(fellThroughFloor,
                $"Particle over the floor hole should exit through the opening (local Y below -{FloorTol}), not remain clamped inside. minY={minY:F4}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(pipeline.gameObject);
        }
    }

    static OtcContainerHole[] BuildAuthoredTwoHolesLab() => new[]
    {
        new OtcContainerHole
        {
            localPosition = new Vector3(0.4f, 0f, -0.2f),
            radius = 0.35f,
            surface = OtcHoleSurface.Floor,
        },
        new OtcContainerHole
        {
            localPosition = new Vector3(Radius - 0.12f, 1.4f, 0.25f),
            radius = 0.3f,
            surface = OtcHoleSurface.Side,
        },
    };

    static Hole[] BuildParityReferenceHoles()
    {
        var floor = new Hole
        {
            position = new Vector3(0.4f, 0f, -0.2f),
            radius = 0.35f,
            axis = Vector3.up,
            surface = 0u,
        };
        Vector3 authored = new Vector3(Radius - 0.12f, 1.4f, 0.25f);
        Vector2 snapped = new Vector2(authored.x, authored.z).normalized * Radius;
        Vector3 projected = new Vector3(snapped.x, Mathf.Clamp(authored.y, 0f, Height), snapped.y);
        Vector3 axis = new Vector3(snapped.normalized.x, 0f, snapped.normalized.y);
        var side = new Hole
        {
            position = projected,
            radius = 0.3f,
            axis = axis,
            surface = 1u,
        };
        return new[] { floor, side };
    }

    static System.Collections.Generic.List<Vector3> BuildInteriorGridExcludingHoleClips(Hole[] holes)
    {
        var pts = new System.Collections.Generic.List<Vector3>();
        for (float x = -Radius + 0.2f; x <= Radius - 0.2f; x += 0.25f)
        for (float z = -Radius + 0.2f; z <= Radius - 0.2f; z += 0.25f)
        for (float y = 0.1f; y <= Height - 0.1f; y += 0.25f)
        {
            Vector3 p = new Vector3(x, y, z);
            if (InAnyHoleClip(p, holes))
            {
                continue;
            }
            pts.Add(p);
        }
        return pts;
    }

    struct Hole
    {
        public Vector3 position;
        public float radius;
        public Vector3 axis;
        public uint surface;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BatchParityResult
    {
        public uint legacyBits;
        public uint fieldRegion;
        public float fieldDist;
        public float pad;
    }

    static BatchParityResult[] DispatchBatchParity(System.Collections.Generic.List<Vector3> points, Hole[] holes)
    {
#if UNITY_EDITOR
        var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Tests/Shaders/OtcBoundaryTestKernels.compute");
#else
        ComputeShader cs = null;
#endif
        Assert.IsNotNull(cs);
        int kernel = cs.FindKernel("CS_BatchParity");

        var holesVec = new Vector4[16];
        var axisVec = new Vector4[16];
        for (int i = 0; i < 16; i++)
        {
            if (i < holes.Length)
            {
                holesVec[i] = new Vector4(holes[i].position.x, holes[i].position.y, holes[i].position.z, holes[i].radius);
                axisVec[i] = new Vector4(holes[i].axis.x, holes[i].axis.y, holes[i].axis.z, holes[i].surface);
            }
        }

        Matrix4x4 identity = Matrix4x4.identity;
        cs.SetMatrix("_ContainerLocalToWorld", identity);
        cs.SetMatrix("_ContainerWorldToLocal", identity);
        cs.SetInt("_ContainerUsesOrientation", 0);
        cs.SetFloat("_ContainerHeight", Height);
        cs.SetFloat("_ContainerRadius", Radius);
        cs.SetFloat("_ContainerFloorY", 0f);
        cs.SetFloat("_ContainerRimY", Height);
        cs.SetVector("_ContainerCenter", Vector3.zero);
        cs.SetVector("_ContainerFloorPivot", Vector3.zero);
        cs.SetFloat("_ContainerRestitution", 0.3f);
        cs.SetFloat("_ContainerFriction", 0.9f);
        cs.SetFloat("_CanvasPlaneY", CanvasPlaneY);
        cs.SetVectorArray("_Holes", holesVec);
        cs.SetVectorArray("_HoleAxis", axisVec);
        cs.SetInt("_HoleCount", holes.Length);
        cs.SetFloat("_HoleClipDepthScale", ClipDepthScale);

        int n = points.Count;
        var posBuf = new ComputeBuffer(n, sizeof(float) * 3);
        var outBuf = new ComputeBuffer(n, Marshal.SizeOf(typeof(BatchParityResult)));
        try
        {
            posBuf.SetData(points.ToArray());
            cs.SetBuffer(kernel, "_ParityPos", posBuf);
            cs.SetBuffer(kernel, "_ParityOut", outBuf);
            cs.SetInt("_ParityCount", n);
            cs.Dispatch(kernel, Mathf.CeilToInt(n / 64f), 1, 1);
            var results = new BatchParityResult[n];
            outBuf.GetData(results);
            return results;
        }
        finally
        {
            posBuf.Release();
            outBuf.Release();
        }
    }

    static float HoleSdfFloor(Vector3 lp, Vector3 pos, float radius, float depth)
    {
        float radial = new Vector2(lp.x - pos.x, lp.z - pos.z).magnitude - radius;
        float slab = Mathf.Max(lp.y - depth, -lp.y);
        return Mathf.Max(radial, slab);
    }

    static float HoleSdfSide(Vector3 lp, Vector3 pos, Vector3 axis, float radius, float depth)
    {
        Vector3 tangent = new Vector3(-axis.z, 0f, axis.x);
        Vector3 v = lp - pos;
        Vector2 perp = new Vector2(Vector3.Dot(v, tangent), v.y);
        float radial = perp.magnitude - radius;
        float vIn = -Vector3.Dot(v, axis);
        float slab = Mathf.Max(-vIn, vIn - depth);
        return Mathf.Max(radial, slab);
    }

    static float HoleSdf(Vector3 lp, Hole h)
    {
        float depth = ClipDepthScale * h.radius;
        if (h.surface == 0u)
        {
            return HoleSdfFloor(lp, h.position, h.radius, depth);
        }
        return HoleSdfSide(lp, h.position, h.axis, h.radius, depth);
    }

    static bool InAnyHoleClip(Vector3 lp, Hole[] holes)
    {
        for (int i = 0; i < holes.Length; i++)
        {
            if (HoleSdf(lp, holes[i]) < 0f)
            {
                return true;
            }
        }
        return false;
    }

    static void SetupContainerWithFloorHole(HarmonicPipelineController pipeline, out Matrix4x4 localToWorld)
    {
        pipeline.SetUsePbf(true);
        pipeline.SetContainerFluidEnabled(true);
        pipeline.SetContainerFluidOriented(Vector3.zero, Quaternion.identity, Radius, Height, 0.1f, 0.85f, 400f);
        pipeline.SetContainerHoles(new[]
        {
            new OtcContainerHole
            {
                localPosition = new Vector3(0.4f, 0f, -0.2f),
                radius = 0.35f,
                surface = OtcHoleSurface.Floor,
            },
        }, Radius, Height);
        localToWorld = Matrix4x4.identity;
    }

    static void StepStationaryFrame(HarmonicPipelineController pipeline, Matrix4x4 localToWorld)
    {
        pipeline.SetContainerFluidOriented(Vector3.zero, Quaternion.identity, Radius, Height, 0.1f, 0.85f, 400f);
        pipeline.SetContainerHoles(new[]
        {
            new OtcContainerHole
            {
                localPosition = new Vector3(0.4f, 0f, -0.2f),
                radius = 0.35f,
                surface = OtcHoleSurface.Floor,
            },
        }, Radius, Height);
        pipeline.ApplyContainerRigidRotation(localToWorld, localToWorld, DeltaTime);
        SyncGpu();
        pipeline.ExecutePipelineFrame(DeltaTime);
        SyncGpu();
    }

    static float LocalHeight(Vector3 worldPos, Matrix4x4 localToWorld) =>
        localToWorld.inverse.MultiplyPoint3x4(worldPos).y;

    static FluidParticle MakeParticle(Vector3 position, Vector3 velocity) => new FluidParticle
    {
        Position = new float3(position.x, position.y, position.z),
        Velocity = new float3(velocity.x, velocity.y, velocity.z),
        Density = 1000f,
        Pressure = 0f,
        PackedColorRGBA = 0xFFFFFFFFu,
    };

    static void SpawnParticles(HarmonicPipelineController pipeline, FluidParticle[] particles)
    {
        int appended = pipeline.AppendParticles(particles, particles.Length);
        Assert.AreEqual(particles.Length, appended);
    }

    static void ReadParticle(HarmonicPipelineController pipeline, int index, out Vector3 pos, out Vector3 vel)
    {
        Assert.IsTrue(pipeline.TryGetInternalParticleSoa(out ParticleSoaBuffers soa, out uint count) && index < count);
        FluidParticle[] particles = GpuParticleReadbackUtility.ReadParticles(soa, (int)count);
        pos = new Vector3(particles[index].Position.x, particles[index].Position.y, particles[index].Position.z);
        vel = new Vector3(particles[index].Velocity.x, particles[index].Velocity.y, particles[index].Velocity.z);
    }

    static HarmonicPipelineController CreatePipeline()
    {
        var settings = Resources.Load<HarmonicPipelineTestSettings>("HarmonicPipelineTestSettings");
        Assert.IsNotNull(settings);

#if UNITY_EDITOR
        var carryShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/ContainerRigidCarry.compute");
        var otcFieldShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/AdvancedHarmonicEngine_V3/Infrastructure/ComputeShaders/OtcParticleField.compute");
#else
        ComputeShader carryShader = null;
        ComputeShader otcFieldShader = null;
#endif
        Assert.IsNotNull(carryShader);
        Assert.IsNotNull(otcFieldShader);

        var go = new GameObject("HoleOpeningTestPipeline");
        var pipeline = go.AddComponent<HarmonicPipelineController>();

        typeof(HarmonicPipelineController).GetField("containerRigidCarryShader", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(pipeline, carryShader);
        typeof(HarmonicPipelineController).GetField("otcParticleFieldShader", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(pipeline, otcFieldShader);
        typeof(HarmonicPipelineController).GetField("perfDiagnosticsMuted", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(pipeline, true);

        pipeline.ConfigureAndInitialize(
            settings.argumentUtilityShader,
            settings.spatialHashGridShader,
            settings.wcsphDensityShader,
            settings.dataCompactionShader,
            capacity: 8192,
            externalIngestion: true,
            autoRun: false,
            fallingShader: settings.fallingFluidWorldShader,
            eulerianShader: settings.eulerianDragGridShader,
            integrateShader: settings.wcsphIntegrationShader,
            pbfShader: settings.pbfSolverShader,
            radixShader: settings.radixSortShader);

        return pipeline;
    }

    static void SyncGpu()
    {
        GraphicsFence fence = Graphics.CreateGraphicsFence(
            GraphicsFenceType.AsyncQueueSynchronisation,
            SynchronisationStageFlags.ComputeProcessing);
        Graphics.WaitOnAsyncGraphicsFence(fence);
    }
}
