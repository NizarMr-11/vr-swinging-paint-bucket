// OtcFieldParityTests.cs
//
// Phase 1a gate: OtcSampleField must be a drop-in source of truth for the six legacy predicates.
// Driven through OtcBoundaryTestKernels.compute (CS_BatchParity), which evaluates the legacy
// predicates and the new field for a batch of points in one dispatch.
//
// Two cases:
//   A) _HoleCount = 0  -> field region bits must EXACTLY match the legacy predicates across a dense
//      grid, and THROUGH_HOLE must never be set. This is the strict parity gate.
//   B) 1 floor + 1 side hole -> field must match an independent CPU reference port of OtcSampleField
//      everywhere, and must still match the legacy predicates at all points OUTSIDE every hole clip
//      volume (holes do not leak beyond their local cut).

using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class OtcFieldParityTests
{
    const string TestKernelAssetPath = "Assets/Tests/Shaders/OtcBoundaryTestKernels.compute";

    const float RADIUS = 2.0f;
    const float HEIGHT = 3.0f;
    const float RESTITUTION = 0.3f;
    const float FRICTION = 0.9f;
    const float CANVAS_PLANE_Y = -1.0f;
    const float CLIP_DEPTH_SCALE = 1.0f;

    // Must match OpenTopCylinderBoundary.hlsl.
    const float FLOOR_TOL = 0.01f;
    const float RADIUS_EPS = 1e-4f;

    // Region bits (must match OtcParticleField.hlsl).
    const uint PARTICIPATES = 1u << 0;
    const uint INSIDE_VOLUME = 1u << 1;
    const uint INSIDE_RIGID_CARRY = 1u << 2;
    const uint CANVAS_CLAIM = 1u << 3;
    const uint BEAM = 1u << 4;
    const uint THROUGH_HOLE = 1u << 5;

    // Legacy bit layout from CS_BatchParity.
    const uint L_PARTICIPATES = 1u << 0;
    const uint L_INSIDE_VOLUME = 1u << 1;
    const uint L_INSIDE_RIGID_CARRY = 1u << 2;
    const uint L_CANVAS_CLAIM = 1u << 3;
    const uint L_BEAM_DESPAWN = 1u << 4;
    const uint L_OUTSIDE_FOOTPRINT = 1u << 5;

    const int MAX_HOLES = 16;

    [StructLayout(LayoutKind.Sequential)]
    struct ParityResult
    {
        public uint legacyBits;
        public uint fieldRegion;
        public float fieldDist;
        public float pad;
    }

    struct Hole
    {
        public Vector3 position;
        public float radius;
        public Vector3 axis;
        public uint surface; // 0 floor, 1 side
    }

    ComputeShader _cs;
    int _kernel;
    int _applyInlineKernel;

    [StructLayout(LayoutKind.Sequential)]
    struct ApplyInlineResult
    {
        public uint canvasClaimInline;
        public uint beamDespawnInline;
        public uint canvasClaimFromSample;
        public uint beamDespawnFromSample;
        public uint throughHoleFromSample;
        public float fieldDist;
        public float pad;
    }

    [SetUp]
    public void Setup()
    {
#if UNITY_EDITOR
        _cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(TestKernelAssetPath);
#else
        _cs = null;
#endif
        Assert.IsNotNull(_cs, "Could not load OtcBoundaryTestKernels — fix the load path.");
        _kernel = _cs.FindKernel("CS_BatchParity");
        _applyInlineKernel = _cs.FindKernel("CS_ApplyInlineParity");
    }

    void SetContainerUniforms(Hole[] holes, int holeCount)
    {
        Matrix4x4 identity = Matrix4x4.identity;
        _cs.SetMatrix("_ContainerLocalToWorld", identity);
        _cs.SetMatrix("_ContainerWorldToLocal", identity);
        _cs.SetInt("_ContainerUsesOrientation", 0);
        _cs.SetFloat("_ContainerHeight", HEIGHT);
        _cs.SetFloat("_ContainerRadius", RADIUS);
        _cs.SetFloat("_ContainerFloorY", 0f);
        _cs.SetFloat("_ContainerRimY", HEIGHT);
        _cs.SetVector("_ContainerCenter", Vector3.zero);
        _cs.SetVector("_ContainerFloorPivot", Vector3.zero);
        _cs.SetFloat("_ContainerRestitution", RESTITUTION);
        _cs.SetFloat("_ContainerFriction", FRICTION);
        _cs.SetFloat("_CanvasPlaneY", CANVAS_PLANE_Y);

        var holesVec = new Vector4[MAX_HOLES];
        var axisVec = new Vector4[MAX_HOLES];
        for (int i = 0; i < MAX_HOLES; i++)
        {
            if (i < holeCount)
            {
                holesVec[i] = new Vector4(holes[i].position.x, holes[i].position.y, holes[i].position.z, holes[i].radius);
                axisVec[i] = new Vector4(holes[i].axis.x, holes[i].axis.y, holes[i].axis.z, holes[i].surface);
            }
            else
            {
                holesVec[i] = Vector4.zero;
                axisVec[i] = Vector4.zero;
            }
        }
        _cs.SetVectorArray("_Holes", holesVec);
        _cs.SetVectorArray("_HoleAxis", axisVec);
        _cs.SetInt("_HoleCount", holeCount);
        _cs.SetFloat("_HoleClipDepthScale", CLIP_DEPTH_SCALE);
    }

    static List<Vector3> BuildGrid()
    {
        var pts = new List<Vector3>();
        for (float x = -3f; x <= 3f + 1e-3f; x += 0.3f)
        for (float z = -3f; z <= 3f + 1e-3f; z += 0.3f)
        for (float y = -3f; y <= 4.5f + 1e-3f; y += 0.3f)
        {
            pts.Add(new Vector3(x, y, z));
        }
        return pts;
    }

    /// <summary>Denser sampling (0.05 step) around each hole's clip volume for opening-shape confidence.</summary>
    static List<Vector3> BuildHoleLocalDenseGrid(Hole[] holes)
    {
        const float step = 0.05f;
        var pts = new List<Vector3>();
        foreach (Hole h in holes)
        {
            float depth = CLIP_DEPTH_SCALE * h.radius;
            float extent = h.radius + depth + 0.25f;
            Vector3 c = h.position;
            for (float dx = -extent; dx <= extent + 1e-3f; dx += step)
            for (float dy = -extent; dy <= extent + 1e-3f; dy += step)
            for (float dz = -extent; dz <= extent + 1e-3f; dz += step)
            {
                pts.Add(c + new Vector3(dx, dy, dz));
            }
        }
        return pts;
    }

    ParityResult[] Dispatch(List<Vector3> points)
    {
        int n = points.Count;
        var posBuf = new ComputeBuffer(n, sizeof(float) * 3);
        var outBuf = new ComputeBuffer(n, Marshal.SizeOf(typeof(ParityResult)));
        try
        {
            posBuf.SetData(points.ToArray());
            _cs.SetBuffer(_kernel, "_ParityPos", posBuf);
            _cs.SetBuffer(_kernel, "_ParityOut", outBuf);
            _cs.SetInt("_ParityCount", n);
            int groups = Mathf.CeilToInt(n / 64f);
            _cs.Dispatch(_kernel, groups, 1, 1);
            var results = new ParityResult[n];
            outBuf.GetData(results);
            return results;
        }
        finally
        {
            posBuf.Release();
            outBuf.Release();
        }
    }

    // ---- Case A: strict parity, no holes --------------------------------------------------------
    [Test]
    public void FieldMatchesLegacyPredicates_NoHoles()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        SetContainerUniforms(null, 0);
        List<Vector3> pts = BuildGrid();
        ParityResult[] res = Dispatch(pts);

        int mismatches = 0;
        var sb = new StringBuilder();
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 p = pts[i];
            uint legacy = res[i].legacyBits;
            uint region = res[i].fieldRegion;

            bool ok = true;
            ok &= ((region & PARTICIPATES) != 0) == ((legacy & L_PARTICIPATES) != 0);
            ok &= ((region & INSIDE_VOLUME) != 0) == ((legacy & L_INSIDE_VOLUME) != 0);
            ok &= ((region & INSIDE_RIGID_CARRY) != 0) == ((legacy & L_INSIDE_RIGID_CARRY) != 0);
            ok &= ((region & CANVAS_CLAIM) != 0) == ((legacy & L_CANVAS_CLAIM) != 0);

            bool despawnReconstructed = ((region & BEAM) != 0) && (p.y <= CANVAS_PLANE_Y);
            ok &= despawnReconstructed == ((legacy & L_BEAM_DESPAWN) != 0);

            ok &= (region & THROUGH_HOLE) == 0;

            // Sanity: legacy "outside footprint" implies field does not classify as participating.
            if ((legacy & L_OUTSIDE_FOOTPRINT) != 0)
            {
                ok &= (region & PARTICIPATES) == 0;
            }

            if (!ok)
            {
                mismatches++;
                if (mismatches <= 20)
                {
                    sb.AppendLine(
                        $"  mismatch at ({p.x:F3},{p.y:F3},{p.z:F3}) legacy=0x{legacy:X2} region=0x{region:X2} dist={res[i].fieldDist:F4}");
                }
            }
        }

        Debug.Log($"[OtcFieldParity:NoHoles] points={pts.Count} mismatches={mismatches}\n{sb}");
        Assert.AreEqual(0, mismatches, $"Field/legacy parity failed at {mismatches} of {pts.Count} points.\n{sb}");
    }

    // ---- Case B: with one floor + one side hole -------------------------------------------------
    [Test]
    public void FieldMatchesCpuReference_AndLegacyOutsideHoles_WithHoles()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        Hole[] holes = BuildTwoHoles();
        SetContainerUniforms(holes, holes.Length);

        List<Vector3> pts = BuildGrid();
        ParityResult[] res = Dispatch(pts);

        int fieldVsCpuMismatches = 0;
        int legacyOutsideHoleMismatches = 0;
        var sb = new StringBuilder();

        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 p = pts[i];
            RefSampleField(p, holes, out float refDist, out uint refRegion);

            // 1) shader field must match the CPU reference exactly (region bits) and closely (dist).
            bool regionOk = res[i].fieldRegion == refRegion;
            bool distOk = Mathf.Abs(res[i].fieldDist - refDist) <= 1e-3f;
            if (!regionOk || !distOk)
            {
                fieldVsCpuMismatches++;
                if (fieldVsCpuMismatches <= 20)
                {
                    sb.AppendLine(
                        $"  [field!=cpu] ({p.x:F3},{p.y:F3},{p.z:F3}) shaderRegion=0x{res[i].fieldRegion:X2} cpuRegion=0x{refRegion:X2} shaderDist={res[i].fieldDist:F4} cpuDist={refDist:F4}");
                }
            }

            // 2) outside every hole clip volume, field must still match the legacy predicates.
            if (!InAnyHoleClip(p, holes))
            {
                uint legacy = res[i].legacyBits;
                uint region = res[i].fieldRegion;
                bool ok = true;
                ok &= ((region & PARTICIPATES) != 0) == ((legacy & L_PARTICIPATES) != 0);
                ok &= ((region & INSIDE_VOLUME) != 0) == ((legacy & L_INSIDE_VOLUME) != 0);
                ok &= ((region & INSIDE_RIGID_CARRY) != 0) == ((legacy & L_INSIDE_RIGID_CARRY) != 0);
                ok &= ((region & CANVAS_CLAIM) != 0) == ((legacy & L_CANVAS_CLAIM) != 0);
                bool despawnReconstructed = ((region & BEAM) != 0) && (p.y <= CANVAS_PLANE_Y);
                ok &= despawnReconstructed == ((legacy & L_BEAM_DESPAWN) != 0);
                ok &= (region & THROUGH_HOLE) == 0;
                if (!ok)
                {
                    legacyOutsideHoleMismatches++;
                    if (legacyOutsideHoleMismatches <= 20)
                    {
                        sb.AppendLine(
                            $"  [legacy-leak] ({p.x:F3},{p.y:F3},{p.z:F3}) legacy=0x{legacy:X2} region=0x{region:X2}");
                    }
                }
            }
        }

        int throughHoleCount = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            if ((res[i].fieldRegion & THROUGH_HOLE) != 0) throughHoleCount++;
        }

        Debug.Log(
            $"[OtcFieldParity:Holes] points={pts.Count} field!=cpu={fieldVsCpuMismatches} " +
            $"legacyLeak={legacyOutsideHoleMismatches} throughHolePoints={throughHoleCount}\n{sb}");

        Assert.AreEqual(0, fieldVsCpuMismatches, $"Shader field disagreed with CPU reference.\n{sb}");
        Assert.AreEqual(0, legacyOutsideHoleMismatches, $"Holes leaked past their clip volume.\n{sb}");
        Assert.Greater(throughHoleCount, 0, "No THROUGH_HOLE points found — holes had no effect (test setup invalid).");
    }

    [Test]
    public void FieldMatchesCpuReference_HoleLocalDenseGrid()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        Hole[] holes = BuildTwoHoles();
        SetContainerUniforms(holes, holes.Length);
        List<Vector3> pts = BuildHoleLocalDenseGrid(holes);
        ParityResult[] res = Dispatch(pts);

        int fieldVsCpuMismatches = 0;
        int legacyOutsideHoleMismatches = 0;
        var sb = new StringBuilder();

        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 p = pts[i];
            RefSampleField(p, holes, out float refDist, out uint refRegion);

            bool regionOk = res[i].fieldRegion == refRegion;
            bool distOk = Mathf.Abs(res[i].fieldDist - refDist) <= 1e-3f;
            if (!regionOk || !distOk)
            {
                fieldVsCpuMismatches++;
                if (fieldVsCpuMismatches <= 20)
                {
                    sb.AppendLine(
                        $"  [field!=cpu] ({p.x:F3},{p.y:F3},{p.z:F3}) shaderRegion=0x{res[i].fieldRegion:X2} cpuRegion=0x{refRegion:X2}");
                }
            }

            if (!InAnyHoleClip(p, holes))
            {
                uint legacy = res[i].legacyBits;
                uint region = res[i].fieldRegion;
                bool ok = true;
                ok &= ((region & PARTICIPATES) != 0) == ((legacy & L_PARTICIPATES) != 0);
                ok &= ((region & INSIDE_VOLUME) != 0) == ((legacy & L_INSIDE_VOLUME) != 0);
                ok &= ((region & INSIDE_RIGID_CARRY) != 0) == ((legacy & L_INSIDE_RIGID_CARRY) != 0);
                ok &= ((region & CANVAS_CLAIM) != 0) == ((legacy & L_CANVAS_CLAIM) != 0);
                bool despawnReconstructed = ((region & BEAM) != 0) && (p.y <= CANVAS_PLANE_Y);
                ok &= despawnReconstructed == ((legacy & L_BEAM_DESPAWN) != 0);
                ok &= (region & THROUGH_HOLE) == 0;
                if (!ok)
                {
                    legacyOutsideHoleMismatches++;
                }
            }
        }

        Debug.Log(
            $"[OtcFieldParity:HoleLocalDense] points={pts.Count} field!=cpu={fieldVsCpuMismatches} " +
            $"legacyLeak={legacyOutsideHoleMismatches}\n{sb}");

        Assert.AreEqual(0, fieldVsCpuMismatches, $"Dense hole-local grid: field disagreed with CPU reference.\n{sb}");
        Assert.AreEqual(0, legacyOutsideHoleMismatches, $"Dense hole-local grid: legacy leak outside clip volumes.\n{sb}");
    }

    /// <summary>
    /// ApplyPositionsKernel uses OtcFieldCanvasClaimAtLocal / OtcFieldBeamDespawnAtWorld (inline
    /// OtcSampleField), not the classification buffer. This test ensures those helpers match a direct
    /// field sample under active holes — the same _Holes/_HoleCount uniforms ApplyPbfUniforms uploads.
    /// </summary>
    [Test]
    public void ApplyInlineFinalPositionPath_MatchesFieldSample_WithHoles()
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            Assert.Ignore("Compute shaders not supported on this machine.");
        }

        Assert.GreaterOrEqual(_applyInlineKernel, 0, "CS_ApplyInlineParity kernel missing from test shader.");

        Hole[] holes = BuildTwoHoles();
        SetContainerUniforms(holes, holes.Length);

        // Dense samples in/near hole openings plus a few exterior/beam points Apply cares about.
        List<Vector3> pts = BuildHoleLocalDenseGrid(holes);
        pts.Add(new Vector3(0.4f, 0.05f, -0.2f));   // floor-hole interior (through-hole)
        pts.Add(new Vector3(3.0f, 0.5f, 0f));         // exterior canvas-claim
        pts.Add(new Vector3(0.2f, -0.5f, 0f));       // beam below floor

        ApplyInlineResult[] res = DispatchApplyInline(pts);

        int mismatches = 0;
        var sb = new StringBuilder();
        for (int i = 0; i < pts.Count; i++)
        {
            bool canvasOk = res[i].canvasClaimInline == res[i].canvasClaimFromSample;
            bool beamOk = res[i].beamDespawnInline == res[i].beamDespawnFromSample;
            if (!canvasOk || !beamOk)
            {
                mismatches++;
                if (mismatches <= 20)
                {
                    Vector3 p = pts[i];
                    sb.AppendLine(
                        $"  ({p.x:F3},{p.y:F3},{p.z:F3}) inlineCanvas={res[i].canvasClaimInline} sampleCanvas={res[i].canvasClaimFromSample} " +
                        $"inlineBeam={res[i].beamDespawnInline} sampleBeam={res[i].beamDespawnFromSample} throughHole={res[i].throughHoleFromSample}");
                }
            }
        }

        int throughHoleInlineHits = 0;
        for (int i = 0; i < res.Length; i++)
        {
            if (res[i].throughHoleFromSample != 0) throughHoleInlineHits++;
        }

        Debug.Log(
            $"[OtcFieldParity:ApplyInline] points={pts.Count} mismatches={mismatches} throughHolePoints={throughHoleInlineHits}\n{sb}");

        Assert.AreEqual(0, mismatches, $"Apply inline path disagreed with OtcSampleField under active holes.\n{sb}");
        Assert.Greater(throughHoleInlineHits, 0, "No through-hole samples in Apply-inline test grid.");
    }

    ApplyInlineResult[] DispatchApplyInline(List<Vector3> points)
    {
        int n = points.Count;
        var posBuf = new ComputeBuffer(n, sizeof(float) * 3);
        var outBuf = new ComputeBuffer(n, Marshal.SizeOf(typeof(ApplyInlineResult)));
        try
        {
            posBuf.SetData(points.ToArray());
            _cs.SetBuffer(_applyInlineKernel, "_ApplyInlinePos", posBuf);
            _cs.SetBuffer(_applyInlineKernel, "_ApplyInlineOut", outBuf);
            _cs.SetInt("_ApplyInlineCount", n);
            int groups = Mathf.CeilToInt(n / 64f);
            _cs.Dispatch(_applyInlineKernel, groups, 1, 1);
            var results = new ApplyInlineResult[n];
            outBuf.GetData(results);
            return results;
        }
        finally
        {
            posBuf.Release();
            outBuf.Release();
        }
    }

    // ---- Hole setup + CPU reference (mirror of OtcParticleField.hlsl) ----------------------------

    static Hole[] BuildTwoHoles()
    {
        // Floor hole near center.
        var floor = new Hole
        {
            position = new Vector3(0.4f, 0f, -0.2f),
            radius = 0.35f,
            axis = new Vector3(0f, 1f, 0f),
            surface = 0u,
        };

        // Side hole authored slightly off-surface, then projected onto the wall (project-at-setup).
        Vector3 authored = new Vector3(RADIUS - 0.12f, 1.4f, 0.25f); // off the wall on purpose
        var xz = new Vector2(authored.x, authored.z);
        Vector2 snapped = xz.normalized * RADIUS;
        Vector3 projected = new Vector3(snapped.x, Mathf.Clamp(authored.y, 0f, HEIGHT), snapped.y);
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

    static float CavitySdf(Vector3 lp)
    {
        float radial = new Vector2(lp.x, lp.z).magnitude - RADIUS;
        float floorHalf = -lp.y;
        return Mathf.Max(radial, floorHalf);
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
        float depth = CLIP_DEPTH_SCALE * h.radius;
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
            if (HoleSdf(lp, holes[i]) < 0f) return true;
        }
        return false;
    }

    static void RefSampleField(Vector3 lp, Hole[] holes, out float dist, out uint region)
    {
        float cavity = CavitySdf(lp);
        float d = cavity;
        for (int i = 0; i < holes.Length; i++)
        {
            d = Mathf.Max(d, -HoleSdf(lp, holes[i]));
        }
        dist = d;

        float r = new Vector2(lp.x, lp.z).magnitude;
        bool insideRadius = r <= RADIUS;
        bool insideRadiusEps = r <= RADIUS + RADIUS_EPS;
        bool aboveFloorBand = lp.y >= -FLOOR_TOL;
        bool belowFloorBand = lp.y < -FLOOR_TOL;
        bool withinHeight = lp.y <= HEIGHT;
        bool aboveFloorStrict = lp.y >= 0f;
        bool outsideFootprint = r > RADIUS;
        bool beamBelowFloor = (r <= RADIUS + RADIUS_EPS) && belowFloorBand;

        uint reg = 0u;
        if (insideRadius && aboveFloorBand) reg |= PARTICIPATES;
        if (aboveFloorBand && withinHeight && insideRadiusEps) reg |= INSIDE_VOLUME;
        if (aboveFloorStrict && withinHeight && insideRadius) reg |= INSIDE_RIGID_CARRY;
        if (outsideFootprint || beamBelowFloor) reg |= CANVAS_CLAIM;
        if (beamBelowFloor) reg |= BEAM;

        bool throughHole = (cavity < 0f) && (d > 0f);
        if (throughHole)
        {
            reg &= ~(PARTICIPATES | INSIDE_VOLUME | INSIDE_RIGID_CARRY);
            reg |= THROUGH_HOLE;
            reg |= CANVAS_CLAIM;
        }

        region = reg;
    }
}
