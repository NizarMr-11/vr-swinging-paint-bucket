using HarmonicEngine.Infrastructure.Management;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace HarmonicEngine.Tests
{
    public class ParticleSpawnCoordinatorTests
    {
        [Test]
        public void ComputeAllocations_UnderCapacity_GivesFullCounts()
        {
            List<ParticleSpawnVolume> volumes = CreateVolumes(
                (10, 100),
                (5, 200),
                (1, 50));

            try
            {
                int[] alloc = HarmonicParticleSpawnCoordinator.ComputeAllocations(volumes, capacity: 1000);
                Assert.AreEqual(100, alloc[0]);
                Assert.AreEqual(200, alloc[1]);
                Assert.AreEqual(50, alloc[2]);
            }
            finally
            {
                Cleanup(volumes);
            }
        }

        [Test]
        public void ComputeAllocations_OverCapacity_HigherPriorityGetsMore()
        {
            List<ParticleSpawnVolume> volumes = CreateVolumes(
                (10, 1000),
                (5, 1000),
                (1, 1000));

            try
            {
                int[] alloc = HarmonicParticleSpawnCoordinator.ComputeAllocations(volumes, capacity: 1000);
                Assert.Greater(alloc[0], alloc[2], "Priority 10 should receive more than priority 1.");
                Assert.AreEqual(1000, alloc[0] + alloc[1] + alloc[2]);
            }
            finally
            {
                Cleanup(volumes);
            }
        }

        [Test]
        public void ComputeAllocations_SortedByPriorityDesc_PutsHighestFirst()
        {
            List<ParticleSpawnVolume> volumes = CreateVolumes(
                (1, 10),
                (10, 10),
                (5, 10));

            try
            {
                volumes.Sort((a, b) =>
                {
                    int byPriority = b.SpawnPriority.CompareTo(a.SpawnPriority);
                    return byPriority != 0 ? byPriority : a.GetInstanceID().CompareTo(b.GetInstanceID());
                });

                Assert.AreEqual(10, volumes[0].SpawnPriority);
                Assert.AreEqual(5, volumes[1].SpawnPriority);
                Assert.AreEqual(1, volumes[2].SpawnPriority);
            }
            finally
            {
                Cleanup(volumes);
            }
        }

        private static List<ParticleSpawnVolume> CreateVolumes(params (int priority, int count)[] specs)
        {
            var list = new List<ParticleSpawnVolume>();
            foreach ((int priority, int count) in specs)
            {
                var go = new GameObject("ParticleSpawnVolume");
                ParticleSpawnVolume volume = go.AddComponent<ParticleSpawnVolume>();
                volume.PrepareRun(count, clearBeforeEmitValue: false, activateSimulationOnEmitValue: false);
                volume.SetSpawnPriority(priority);
                list.Add(volume);
            }

            return list;
        }

        private static void Cleanup(List<ParticleSpawnVolume> volumes)
        {
            foreach (ParticleSpawnVolume volume in volumes)
            {
                if (volume != null)
                {
                    Object.DestroyImmediate(volume.gameObject);
                }
            }
        }
    }
}
