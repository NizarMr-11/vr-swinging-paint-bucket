// OpenTopCylinderBoundaryTests.cs
//
// PlayMode tests for OpenTopCylinderBoundary.hlsl, driven through
// OtcBoundaryTestKernels.compute. Place under Assets/Tests/PlayMode/.
//
// These tests assume an axis-aligned container (_ContainerUsesOrientation = 0)
// with center at world origin, floor at y=0, for simplicity. Adjust
// SetupContainer() if your default test fixture differs.
//
// TODO before running:
//   - Fix the ComputeShader asset path in Setup().
//   - Confirm uniform names against your actual _Container* declarations
//     (these match what was shown in OpenTopCylinderBoundary.hlsl).
//   - If OtcParticipatesInPbf / OtcIsOutsideFootprint / OtcIsSpilledOutside
//     have different signatures than assumed in the test kernel file, this
//     will fail to compile — fix the kernel file, not this one.

using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class OpenTopCylinderBoundaryTests
{
    ComputeShader _cs;
    ComputeBuffer _posBuf, _velBuf, _prevPosBuf, _flagBuf;

    const float RADIUS = 2.0f;
    const float HEIGHT = 3.0f;
    const float RESTITUTION = 0.3f;
    const float FRICTION = 0.9f;
    const float EPS = 1e-4f;
    const float CANVAS_PLANE_Y = -1.0f;

    const string TestKernelAssetPath = "Assets/Tests/Shaders/OtcBoundaryTestKernels.compute";

    [SetUp]
    public void Setup()
    {
#if UNITY_EDITOR
        _cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(TestKernelAssetPath);
#else
        _cs = null;
#endif
        Assert.IsNotNull(_cs, "Could not load OtcBoundaryTestKernels — fix the load path.");

        _posBuf = new ComputeBuffer(1, sizeof(float) * 3);
        _velBuf = new ComputeBuffer(1, sizeof(float) * 3);
        _prevPosBuf = new ComputeBuffer(1, sizeof(float) * 3);
        _flagBuf = new ComputeBuffer(1, sizeof(uint));

        SetContainerUniforms();
    }

    [TearDown]
    public void Teardown()
    {
        _posBuf?.Release();
        _velBuf?.Release();
        _prevPosBuf?.Release();
        _flagBuf?.Release();
    }

    void SetContainerUniforms()
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
    }

    void RunClamp(Vector3 pos, Vector3 vel, out Vector3 outPos, out Vector3 outVel)
    {
        int k = _cs.FindKernel("CS_ClampContainer");
        _posBuf.SetData(new[] { pos });
        _velBuf.SetData(new[] { vel });
        _cs.SetBuffer(k, "_TestPos", _posBuf);
        _cs.SetBuffer(k, "_TestVel", _velBuf);
        _cs.Dispatch(k, 1, 1, 1);
        var p = new Vector3[1]; var v = new Vector3[1];
        _posBuf.GetData(p); _velBuf.GetData(v);
        outPos = p[0]; outVel = v[0];
    }

    void RunClampWithPrev(Vector3 prevPos, Vector3 pos, Vector3 vel, out Vector3 outPos, out Vector3 outVel)
    {
        int k = _cs.FindKernel("CS_ClampContainerWithPrev");
        _prevPosBuf.SetData(new[] { prevPos });
        _posBuf.SetData(new[] { pos });
        _velBuf.SetData(new[] { vel });
        _cs.SetBuffer(k, "_TestPrevPos", _prevPosBuf);
        _cs.SetBuffer(k, "_TestPos", _posBuf);
        _cs.SetBuffer(k, "_TestVel", _velBuf);
        _cs.Dispatch(k, 1, 1, 1);
        var p = new Vector3[1]; var v = new Vector3[1];
        _posBuf.GetData(p); _velBuf.GetData(v);
        outPos = p[0]; outVel = v[0];
    }

    bool RunBoolKernel(string kernelName, Vector3 pos)
    {
        int k = _cs.FindKernel(kernelName);
        _posBuf.SetData(new[] { pos });
        _cs.SetBuffer(k, "_TestPos", _posBuf);
        _cs.SetBuffer(k, "_TestFlag", _flagBuf);
        _cs.Dispatch(k, 1, 1, 1);
        var f = new uint[1];
        _flagBuf.GetData(f);
        return f[0] != 0;
    }

    // --- Rule 4: Floor is solid under the footprint ---
    [Test]
    public void Floor_ClampsToZero_WhenInsideRadius()
    {
        RunClamp(new Vector3(0.5f, -1.0f, 0.0f), new Vector3(0, -5, 0), out var p, out var v);
        Assert.AreEqual(0f, p.y, EPS, "Particle inside radius below floor must clamp to y=0.");
        Assert.GreaterOrEqual(v.y, 0f, "Vertical velocity must reflect (bounce) after floor clamp.");
    }

    // --- Rule 5: Under the bucket outside radius is fully free ---
    [Test]
    public void UnderBucket_OutsideRadius_IsNotClampedByFloor()
    {
        RunClamp(new Vector3(RADIUS + 1.0f, -5.0f, 0.0f), new Vector3(0, -5, 0), out var p, out var v);
        Assert.AreEqual(-5.0f, p.y, EPS, "Particle outside radius under the bucket must NOT be floor-clamped.");
        Assert.AreEqual(-5.0f, v.y, EPS, "Velocity must be untouched when fully free under the bucket.");
    }

    // --- Rule 3: Wall is solid from inside ---
    [Test]
    public void Wall_ClampsFromInside_BelowRim()
    {
        RunClamp(new Vector3(RADIUS + 0.5f, 1.0f, 0.0f), new Vector3(5, 0, 0), out var p, out var v);
        float r = new Vector2(p.x, p.z).magnitude;
        Assert.AreEqual(RADIUS, r, EPS, "Particle pushed radially outward from inside must clamp to r=radius.");
    }

    // --- Rule 3: Wall is solid from outside too (bidirectional) ---
    // NOTE: this drives the LEGACY unconditional OtcClampToContainer(pos, vel) directly with a
    // far-exterior inward-moving particle. That input never reaches this function on its real
    // production call site (SolvePositionsKernel only receives particles already classified
    // interior this frame). This test therefore documents legacy/direct-call semantics, NOT the
    // bounded Predict/Apply path's behavior — do not "reconcile" it by widening the bounded
    // wall-capture margin (OTC_WALL_CAPTURE_MARGIN), which would reintroduce unbounded capture.
    [Test]
    public void Wall_ClampsFromOutside_BelowRim()
    {
        // Particle starts outside the radius, below the rim, moving inward.
        RunClamp(new Vector3(RADIUS + 0.5f, 1.0f, 0.0f), new Vector3(-5, 0, 0), out var p, out var v);
        float r = new Vector2(p.x, p.z).magnitude;
        Assert.AreEqual(RADIUS, r, EPS,
            "Particle outside the radius moving inward, below the rim, must be clamped to r=radius (no re-entry through the wall).");
    }

    // --- Rule 1/2: Above rim + outside radius is free; re-entry through the top works ---
    [Test]
    public void AboveRim_OutsideRadius_IsFree()
    {
        var before = new Vector3(RADIUS + 1.0f, HEIGHT + 1.0f, 0.0f);
        RunClamp(before, new Vector3(-5, -1, 0), out var p, out var v);
        Assert.AreEqual(before + new Vector3(0, 0, 0), p, "no-op check placeholder");
        // Real assertion: position/velocity should be unmodified by the clamp
        // (open top, no wall/floor interaction above the rim outside the radius).
        Assert.AreEqual(-5f, v.x, EPS);
        Assert.AreEqual(-1f, v.y, EPS);
    }

    [Test]
    public void AboveRim_OutsideRadius_IsOutsideFootprint()
    {
        bool outside = RunBoolKernel("CS_IsOutsideFootprint", new Vector3(RADIUS + 1.0f, HEIGHT + 1.0f, 0f));
        Assert.IsTrue(outside);
    }

    [Test]
    public void AboveRim_InsideRadius_IsNotOutsideFootprint()
    {
        // Rule 2: re-entry — inside the radius above the rim should NOT be classified outside.
        bool outside = RunBoolKernel("CS_IsOutsideFootprint", new Vector3(0.5f, HEIGHT + 1.0f, 0f));
        Assert.IsFalse(outside, "A particle falling back through the open top (inside radius) must re-enter, not stay classified outside.");
    }

    // --- The bug from the diagnostic report: rigid-carry classification gap ---
    [Test]
    public void RigidCarryClassification_RejectsSubFloorParticle()
    {
        // A particle that has tunneled below the floor but is still within
        // radius/height must NOT be classified "inside" for rigid carry,
        // per the stated invariant: "a particle can't be under the rim
        // unless it's inside the bucket" (which implies y >= 0 too).
        bool inside = RunBoolKernel("CS_IsInsideForRigidCarry", new Vector3(0.5f, -0.5f, 0f));
        Assert.IsFalse(inside,
            "KNOWN GAP (see diagnostic report): OtcIsInsideForRigidCarry currently only checks " +
            "localY <= height, not localY >= 0. This test should FAIL until that check is added, " +
            "confirming the tunneling root cause before/after the fix.");
    }

    [Test]
    public void ParticipatesInPbf_RejectsSubFloorParticle()
    {
        // DECIDED (Zone-A beam fix): a sub-floor, in-radius particle is the "beam" — radially inside
        // the column but never in the 3D cup. It must NOT participate in the PBF density/pressure
        // solve (otherwise it couples into the cup fluid and free-falls with pressure-driven motion,
        // claimed by no collision handler). Participation now also requires localY >= -floor_tol.
        bool participates = RunBoolKernel("CS_ParticipatesInPbf", new Vector3(0.5f, -0.5f, 0f));
        Assert.IsFalse(participates,
            "Sub-floor in-radius particle (the Zone-A beam) must be excluded from PBF participation " +
            "(localY < -OTC_FLOOR_GATE_TOLERANCE).");
    }

    [Test]
    public void ParticipatesInPbf_AcceptsSloshAboveRim()
    {
        // Guardrail for the beam fix: the floor bound must NOT introduce an upper bound. Slosh above
        // the rim (localY > height, r <= radius) must keep participating so it re-enters via open top.
        bool participates = RunBoolKernel("CS_ParticipatesInPbf", new Vector3(0.5f, HEIGHT + 1.0f, 0f));
        Assert.IsTrue(participates,
            "Slosh above the rim (inside radius) must still participate in PBF — no upper vertical bound.");
    }

    [Test]
    public void ParticipatesInPbf_AcceptsInteriorAndFloorRestingFluid()
    {
        // Interior fluid and floor-resting fluid (within the tolerance band) must keep participating.
        Assert.IsTrue(RunBoolKernel("CS_ParticipatesInPbf", new Vector3(0.5f, 1.0f, 0f)),
            "Interior fluid (0 < localY < height, r <= radius) must participate in PBF.");
        Assert.IsTrue(RunBoolKernel("CS_ParticipatesInPbf", new Vector3(0.5f, -0.005f, 0f)),
            "Floor-resting fluid within the FP tolerance band (localY >= -floor_tol) must participate.");
    }

    [Test]
    public void CanvasCollisionApplies_BeamBelowFloor_IsClaimed()
    {
        // Option A: a sub-floor, in-radius beam particle must be claimed by canvas collision.
        bool applies = RunBoolKernel("CS_CanvasCollisionApplies", new Vector3(0.5f, -0.5f, 0f));
        Assert.IsTrue(applies,
            "Beam particle (radially inside, below floor band) must be claimed by canvas collision.");
    }

    [Test]
    public void CanvasCollisionApplies_ExteriorParticle_IsClaimed()
    {
        // Existing behavior preserved: radially outside the footprint is still claimed.
        bool applies = RunBoolKernel("CS_CanvasCollisionApplies", new Vector3(RADIUS + 0.5f, -0.5f, 0f));
        Assert.IsTrue(applies, "Exterior (r > radius) particle must be claimed by canvas collision.");
    }

    [Test]
    public void CanvasCollisionApplies_InteriorFluid_IsNotClaimed()
    {
        // Genuine in-cup fluid must NOT be canvas-claimed.
        bool applies = RunBoolKernel("CS_CanvasCollisionApplies", new Vector3(0.5f, 1.0f, 0f));
        Assert.IsFalse(applies, "Interior in-cup fluid (0 <= localY <= height, r <= radius) must not be canvas-claimed.");
    }

    [Test]
    public void CanvasCollisionApplies_SloshAboveRimInsideRadius_IsNotClaimed()
    {
        // Critical Option-A guardrail: slosh above the rim (inside radius) must NOT be canvas-claimed —
        // it stays aloft and re-enters PBF via the open top. This is the behavior the full-volume
        // variant would have broken; Option A must preserve it.
        bool applies = RunBoolKernel("CS_CanvasCollisionApplies", new Vector3(0.5f, HEIGHT + 1.0f, 0f));
        Assert.IsFalse(applies,
            "Slosh above the rim (localY > height, r <= radius) must NOT be canvas-claimed under Option A.");
    }

    // --- Zone-A beam despawn predicate (OtcBeamDespawnApplies) ---
    [Test]
    public void BeamDespawn_BeamBelowFloorAtCanvasPlane_IsDespawned()
    {
        // A sub-floor in-radius beam particle that has reached/passed the canvas plane must be dropped.
        bool despawn = RunBoolKernel("CS_BeamDespawnApplies", new Vector3(0.5f, CANVAS_PLANE_Y - 0.5f, 0f));
        Assert.IsTrue(despawn,
            "Beam particle (radially inside, below floor band) at/below the canvas plane must be despawned.");
    }

    [Test]
    public void BeamDespawn_BeamBelowFloorAboveCanvasPlane_IsNotDespawned()
    {
        // Still-falling beam particle above the canvas plane must be kept (it hasn't landed yet).
        bool despawn = RunBoolKernel("CS_BeamDespawnApplies", new Vector3(0.5f, CANVAS_PLANE_Y + 0.5f, 0f));
        Assert.IsFalse(despawn,
            "Beam particle above the canvas plane has not landed yet and must NOT be despawned.");
    }

    [Test]
    public void BeamDespawn_ExteriorParticleAtCanvasPlane_IsNotDespawned()
    {
        // Beam-only: exterior/rim-spill particles keep their existing lifecycle even at the plane.
        bool despawn = RunBoolKernel("CS_BeamDespawnApplies", new Vector3(RADIUS + 0.5f, CANVAS_PLANE_Y - 0.5f, 0f));
        Assert.IsFalse(despawn,
            "Exterior (r > radius) particle must NOT be beam-despawned — its lifecycle is untouched.");
    }

    [Test]
    public void BeamDespawn_InteriorFluid_IsNotDespawned()
    {
        // Genuine in-cup fluid must never be beam-despawned regardless of depth query.
        bool despawn = RunBoolKernel("CS_BeamDespawnApplies", new Vector3(0.5f, 1.0f, 0f));
        Assert.IsFalse(despawn, "Interior in-cup fluid must not be beam-despawned.");
    }

    [Test]
    public void BoundedWallCapture_ClampsInteriorCrossingOutward()
    {
        Vector3 prevPos = new Vector3(RADIUS - 0.01f, 0.3f, 0.0f);
        Vector3 pos = new Vector3(RADIUS + 0.60f, 0.3f, 0.0f);
        RunClampWithPrev(prevPos, pos, Vector3.zero, out var outPos, out _);
        float r = new Vector2(outPos.x, outPos.z).magnitude;
        Assert.AreEqual(RADIUS, r, EPS,
            "Interior->exterior crossing must still clamp to prevent leaks.");
    }

    [Test]
    public void BoundedWallCapture_KeepsWallSlidingClampedAcrossFrames()
    {
        Vector3 prevPos = new Vector3(RADIUS, 0.3f, 0.0f);
        Vector3 pos = new Vector3(RADIUS + 0.02f, 0.3f, 0.0f);
        for (int frame = 0; frame < 5; frame++)
        {
            RunClampWithPrev(prevPos, pos, new Vector3(0.0f, -0.2f, 0.0f), out var outPos, out _);
            float r = new Vector2(outPos.x, outPos.z).magnitude;
            Assert.AreEqual(RADIUS, r, EPS, $"Frame {frame}: wall-adjacent particle should remain clamped.");
            prevPos = outPos;
            pos = new Vector3(RADIUS + 0.02f, 0.3f, 0.0f);
        }
    }

    [Test]
    public void BoundedWallCapture_DoesNotClampFarExteriorVerticalFall()
    {
        float farR = RADIUS + 1.45f;
        Vector3 prevPos = new Vector3(farR, 0.3f, 0.0f);
        Vector3 pos = new Vector3(farR, 0.3f, 0.0f);
        RunClampWithPrev(prevPos, pos, new Vector3(0.0f, -1.0f, 0.0f), out var outPos, out _);
        float r = new Vector2(outPos.x, outPos.z).magnitude;
        Assert.AreEqual(farR, r, EPS,
            "Far exterior particle falling through [0,height] must remain free (no wall capture teleport).");
    }

    [Test]
    public void BoundedWallCapture_DoesNotClampModeratelyExteriorParticle()
    {
        float exteriorR = RADIUS + 0.30f;
        Vector3 prevPos = new Vector3(exteriorR, 0.3f, 0.0f);
        Vector3 pos = new Vector3(exteriorR, 0.2f, 0.0f);
        RunClampWithPrev(prevPos, pos, new Vector3(0.0f, -1.0f, 0.0f), out var outPos, out _);
        float r = new Vector2(outPos.x, outPos.z).magnitude;
        Assert.AreEqual(exteriorR, r, EPS,
            "Moderately exterior particle must not be pulled onto the wall.");
    }
}
