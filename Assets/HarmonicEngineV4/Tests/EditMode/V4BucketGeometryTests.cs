using HarmonicEngineV4.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarmonicEngineV4.Tests.EditMode
{
    /// <summary>
    /// Layer 1 CPU reference tests for bucket geometry (plan Phase 3 Step 1).
    /// Bucket-local space: origin at floor center, +Y up, rim at y = height.
    /// </summary>
    public sealed class V4BucketGeometryTests
    {
        private const float R = 0.5f;   // inner radius
        private const float H = 1.0f;   // height
        private const float T = 0.05f;  // wall thickness

        // --- IsInside ---

        [Test]
        public void Center_IsInside()
        {
            Assert.IsTrue(V4BucketGeometry.IsInside(new Vector3(0f, 0.5f, 0f), R, H));
        }

        [Test]
        public void ExactlyOnWall_IsInside()
        {
            Assert.IsTrue(V4BucketGeometry.IsInside(new Vector3(R, 0.5f, 0f), R, H));
        }

        [Test]
        public void JustOutsideWall_IsOutside()
        {
            Assert.IsFalse(V4BucketGeometry.IsInside(new Vector3(R + 1e-4f, 0.5f, 0f), R, H));
        }

        [Test]
        public void ExactlyAtRim_IsInside()
        {
            Assert.IsTrue(V4BucketGeometry.IsInside(new Vector3(0f, H, 0f), R, H));
        }

        [Test]
        public void AboveRim_IsOutside()
        {
            Assert.IsFalse(V4BucketGeometry.IsInside(new Vector3(0f, H + 1e-4f, 0f), R, H));
        }

        [Test]
        public void ExactlyOnFloor_IsInside()
        {
            Assert.IsTrue(V4BucketGeometry.IsInside(new Vector3(0.2f, 0f, 0.2f), R, H));
        }

        [Test]
        public void BelowFloor_IsOutside()
        {
            Assert.IsFalse(V4BucketGeometry.IsInside(new Vector3(0f, -1e-4f, 0f), R, H));
        }

        // --- ShouldContainInside ---

        [Test]
        public void ShouldContainInside_LagsBelowFloor_InFootprint()
        {
            Assert.IsTrue(V4BucketGeometry.ShouldContainInside(new Vector3(0.1f, -0.05f, 0f), R, H, T));
        }

        [Test]
        public void ShouldContainInside_DeepBelowFloor_IsFalse()
        {
            Assert.IsFalse(V4BucketGeometry.ShouldContainInside(new Vector3(0f, -0.5f, 0f), R, H, T));
        }

        [Test]
        public void ShouldContainInside_AboveRim_IsFalse()
        {
            Assert.IsFalse(V4BucketGeometry.ShouldContainInside(new Vector3(0f, H + 0.01f, 0f), R, H, T));
        }

        [Test]
        public void ShouldContainInside_OutsideWallFootprint_IsFalse()
        {
            Assert.IsFalse(V4BucketGeometry.ShouldContainInside(new Vector3(R + 0.1f, 0.5f, 0f), R, H, T));
        }

        [Test]
        public void ComputeContainInside_FrameStartInsideFlag_IsTrueEvenWhenOutsideFootprint()
        {
            uint flags = V4ParticleFlags.SetInside(0u, true);
            var local = new Vector3(R + T * 0.9f, 0.5f, 0f);
            var reference = new Vector3(R, 0.5f, 0f);
            Assert.IsTrue(V4BucketGeometry.ComputeContainInside(flags, local, reference, R, H, T));
        }

        [Test]
        public void ComputeContainInside_ReferenceAtWallFace_ContainsWhenFlagFlickeredOutside()
        {
            uint flags = V4ParticleFlags.SetInside(0u, false);
            var local = new Vector3(R + T * 0.9f, 0f, 0f);
            var reference = new Vector3(R, 0f, 0f);
            Assert.IsTrue(V4BucketGeometry.ComputeContainInside(flags, local, reference, R, H, T));
        }

        [Test]
        public void InsideParticlePinnedAtWallFace_ReflectsOutwardRadialVelocity()
        {
            var pos = new Vector3(R, 0.5f, 0f);
            var vel = new Vector3(2f, 0f, 0f);
            V4BucketGeometry.ResolveCollision(ref pos, ref vel, R, H, T, restitution: 0f, friction: 0f, containInside: true);
            Assert.LessOrEqual(vel.x, 1e-5f, "outward radial velocity must be removed at exactly r = R");
        }

        // --- IsInSolidShell ---

        [Test]
        public void PointInWallBand_IsInShell()
        {
            Assert.IsTrue(V4BucketGeometry.IsInSolidShell(new Vector3(R + T * 0.5f, 0.5f, 0f), R, H, T));
        }

        [Test]
        public void PointInFloorSlab_IsInShell()
        {
            Assert.IsTrue(V4BucketGeometry.IsInSolidShell(new Vector3(0.1f, -T * 0.5f, 0f), R, H, T));
        }

        [Test]
        public void PointInCavity_IsNotInShell()
        {
            Assert.IsFalse(V4BucketGeometry.IsInSolidShell(new Vector3(0f, 0.5f, 0f), R, H, T));
        }

        [Test]
        public void PointOutsideOuterWall_IsNotInShell()
        {
            Assert.IsFalse(V4BucketGeometry.IsInSolidShell(new Vector3(R + T + 0.01f, 0.5f, 0f), R, H, T));
        }

        [Test]
        public void PointAboveRim_IsNotInShell()
        {
            Assert.IsFalse(V4BucketGeometry.IsInSolidShell(new Vector3(R + T * 0.5f, H + 0.01f, 0f), R, H, T));
        }

        // --- ResolveCollision ---

        [Test]
        public void InsideParticlePenetratingWall_IsPushedBackToInnerRadius()
        {
            var pos = new Vector3(R + 0.01f, 0.5f, 0f);
            var vel = new Vector3(1f, 0f, 0f);
            V4BucketGeometry.ResolveCollision(ref pos, ref vel, R, H, T, restitution: 0f, friction: 0f, containInside: true);

            float r = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
            Assert.AreEqual(R, r, 1e-5f);
            Assert.LessOrEqual(vel.x, 1e-5f, "outward velocity must be removed");
        }

        [Test]
        public void InsideParticleTunneledPastWallMidpoint_IsStillPulledBackInside()
        {
            // Deeper than half the wall: nearest-face resolution would eject it through
            // the wall - contained particles must always come back to the inner face.
            var pos = new Vector3(R + T * 0.9f, 0.5f, 0f);
            var vel = new Vector3(3f, 0f, 0f);
            V4BucketGeometry.ResolveCollision(ref pos, ref vel, R, H, T, restitution: 0f, friction: 0f, containInside: true);

            float r = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
            Assert.AreEqual(R, r, 1e-5f, "contained particle escaped through the wall");
            Assert.LessOrEqual(vel.x, 1e-5f, "outward velocity must be removed");
        }

        [Test]
        public void InsideParticleSweptBelowFloorSlab_IsClampedBackToFloor()
        {
            // Bucket moving up can sweep the whole floor slab past a particle in one
            // step; contained particles must be recovered, not dropped out the bottom.
            var pos = new Vector3(0.1f, -T * 2f, 0f);
            var vel = new Vector3(0f, -2f, 0f);
            V4BucketGeometry.ResolveCollision(ref pos, ref vel, R, H, T, restitution: 0f, friction: 0f, containInside: true);

            Assert.AreEqual(0f, pos.y, 1e-5f, "contained particle fell through the floor");
            Assert.GreaterOrEqual(vel.y, 0f, "downward velocity must be removed");
        }

        [Test]
        public void OutsideParticlePenetratingWall_IsPushedOutToOuterRadius()
        {
            var pos = new Vector3(R + T - 0.005f, 0.5f, 0f);
            var vel = new Vector3(-1f, 0f, 0f);
            V4BucketGeometry.ResolveCollision(ref pos, ref vel, R, H, T, restitution: 0f, friction: 0f, containInside: false);

            float r = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
            Assert.AreEqual(R + T, r, 1e-5f);
            Assert.GreaterOrEqual(vel.x, -1e-5f, "inward velocity must be removed");
        }

        [Test]
        public void WallTouchingParticleMisclassifiedOutside_ResolvesBackToInnerFace()
        {
            // A particle pressed against the inner wall sits at exactly r = R; float
            // rounding can put it epsilon outside, so classification flags it Outside
            // for one frame. Nearest-face resolution must bring it back to the inner
            // face - ejecting it to the outer face makes the wall nonexistent.
            var pos = new Vector3(R + 1e-5f, 0.5f, 0f);
            var vel = Vector3.zero;
            V4BucketGeometry.ResolveCollision(ref pos, ref vel, R, H, T, restitution: 0f, friction: 0f, containInside: false);

            float r = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
            Assert.AreEqual(R, r, 1e-5f, "wall-touching particle was ejected through the wall");
        }

        [Test]
        public void FloorTouchingParticleMisclassifiedOutside_ResolvesBackUpToFloor()
        {
            // Same float-noise story at the floor: y drifts a hair below 0, one frame
            // of Outside classification must not drop the particle through the slab.
            var pos = new Vector3(0.1f, -1e-5f, 0f);
            var vel = new Vector3(0f, -0.1f, 0f);
            V4BucketGeometry.ResolveCollision(ref pos, ref vel, R, H, T, restitution: 0f, friction: 0f, containInside: false);

            Assert.AreEqual(0f, pos.y, 1e-6f, "floor-touching particle fell through the floor slab");
        }

        [Test]
        public void ParticleBelowFloor_InsideFootprint_IsClampedToFloor()
        {
            var pos = new Vector3(0.1f, -0.01f, 0f);
            var vel = new Vector3(0f, -2f, 0f);
            V4BucketGeometry.ResolveCollision(ref pos, ref vel, R, H, T, restitution: 0f, friction: 0f, containInside: true);

            Assert.AreEqual(0f, pos.y, 1e-5f);
            Assert.GreaterOrEqual(vel.y, 0f, "downward velocity must be removed");
        }

        [Test]
        public void WallBandParticleBelowFloor_IsLiftedToInnerFloor()
        {
            // Lateral slosh pushes a particle into the wall band with y slightly below 0.
            var pos = new Vector3(R + T * 0.3f, -0.016f, 0f);
            var vel = new Vector3(0.5f, -0.2f, 0f);
            V4BucketGeometry.ResolveCollision(ref pos, ref vel, R, H, T, restitution: 0f, friction: 0f, containInside: false);

            Assert.AreEqual(0f, pos.y, 1e-5f, "wall-band particle fell through the floor");
            float r = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
            Assert.AreEqual(R, r, 1e-4f, "particle should resolve to the inner wall");
        }

        [Test]
        public void ParticleAboveRim_IsUntouched_OpenTop([Values(false, true)] bool containInside)
        {
            var pos = new Vector3(R + T * 0.5f, H + 0.1f, 0f);
            Vector3 posBefore = pos;
            var vel = new Vector3(0.3f, -0.1f, 0f);
            Vector3 velBefore = vel;
            V4BucketGeometry.ResolveCollision(ref pos, ref vel, R, H, T, restitution: 0.5f, friction: 0.5f, containInside: containInside);

            Assert.AreEqual(posBefore, pos);
            Assert.AreEqual(velBefore, vel);
        }

        [Test]
        public void Restitution_ReflectsNormalVelocity()
        {
            var pos = new Vector3(R + 0.01f, 0.5f, 0f);
            var vel = new Vector3(2f, 0f, 0f);
            V4BucketGeometry.ResolveCollision(ref pos, ref vel, R, H, T, restitution: 0.5f, friction: 0f, containInside: true);
            Assert.AreEqual(-1f, vel.x, 1e-4f, "restitution 0.5 must reflect half the normal speed");
        }

        [Test]
        public void Friction_DampsTangentialVelocity()
        {
            var pos = new Vector3(R + 0.01f, 0.5f, 0f);
            var vel = new Vector3(1f, -3f, 0f); // x = into wall, y = tangential slide
            V4BucketGeometry.ResolveCollision(ref pos, ref vel, R, H, T, restitution: 0f, friction: 0.25f, containInside: true);
            Assert.AreEqual(-2.25f, vel.y, 1e-4f, "tangential speed must be scaled by (1 - friction)");
        }

        [Test]
        public void SeparatingVelocity_IsNotReflected()
        {
            var vel = new Vector3(0f, 1f, 0f); // moving away from the floor
            Vector3 before = vel;
            V4BucketGeometry.ReflectAgainstNormal(ref vel, Vector3.up, 0.5f, 0.5f);
            Assert.AreEqual(before, vel, "velocity moving away from the surface must be untouched");
        }

        [Test]
        public void ParticleInCavity_IsUntouched([Values(false, true)] bool containInside)
        {
            var pos = new Vector3(0.2f, 0.5f, 0.1f);
            Vector3 posBefore = pos;
            var vel = new Vector3(0.5f, 0.5f, 0.5f);
            Vector3 velBefore = vel;
            V4BucketGeometry.ResolveCollision(ref pos, ref vel, R, H, T, 0.5f, 0.5f, containInside);

            Assert.AreEqual(posBefore, pos);
            Assert.AreEqual(velBefore, vel);
        }
    }
}
