using System.Collections.Generic;
using UnityEngine;

namespace HarmonicEngine.Infrastructure.Management
{
    /// <summary>
    /// Spawns all <see cref="ParticleSpawnVolume"/> instances in priority order,
    /// splitting pipeline capacity proportionally when total requests exceed <see cref="PipelineExecutionController.MaxCapacity"/>.
    /// </summary>
    public static class HarmonicParticleSpawnCoordinator
    {
        public static int SpawnAll(
            PipelineExecutionController pipeline,
            bool clearFirst = true,
            bool activateSimulation = true,
            IReadOnlyList<ParticleSpawnVolume> volumes = null)
        {
            if (pipeline == null)
            {
                return 0;
            }

            ParticleSpawnVolume[] source = CollectVolumes(volumes);
            if (source.Length == 0)
            {
                return 0;
            }

            var sorted = new List<ParticleSpawnVolume>(source);
            sorted.Sort(ComparePriority);

            if (clearFirst)
            {
                pipeline.ClearAllParticles();
            }

            int remainingCapacity = pipeline.MaxCapacity - (clearFirst ? 0 : (int)pipeline.GetActiveParticleCount());
            remainingCapacity = Mathf.Max(0, remainingCapacity);

            int[] allocations = ComputeAllocations(sorted, remainingCapacity);
            int totalAppended = 0;

            for (int i = 0; i < sorted.Count; i++)
            {
                int count = allocations[i];
                if (count <= 0)
                {
                    continue;
                }

                totalAppended += sorted[i].Emit(count, clearFirst: false, activateSimulation: false);
            }

            if (activateSimulation && totalAppended > 0)
            {
                pipeline.SetSimulationActive(true);
            }

            return totalAppended;
        }

        public static int[] ComputeAllocations(IReadOnlyList<ParticleSpawnVolume> sortedByPriorityDesc, int capacity)
        {
            int count = sortedByPriorityDesc.Count;
            var allocations = new int[count];
            if (count == 0 || capacity <= 0)
            {
                return allocations;
            }

            int totalRequested = 0;
            for (int i = 0; i < count; i++)
            {
                totalRequested += Mathf.Max(0, sortedByPriorityDesc[i].ParticleCount);
            }

            if (totalRequested <= capacity)
            {
                for (int i = 0; i < count; i++)
                {
                    allocations[i] = sortedByPriorityDesc[i].ParticleCount;
                }

                return allocations;
            }

            int sumPriority = 0;
            for (int i = 0; i < count; i++)
            {
                sumPriority += Mathf.Max(1, sortedByPriorityDesc[i].SpawnPriority);
            }

            int assigned = 0;
            for (int i = 0; i < count; i++)
            {
                int requested = sortedByPriorityDesc[i].ParticleCount;
                if (requested <= 0)
                {
                    allocations[i] = 0;
                    continue;
                }

                int weight = Mathf.Max(1, sortedByPriorityDesc[i].SpawnPriority);
                int share = Mathf.Max(1, (int)((long)capacity * weight / sumPriority));
                share = Mathf.Min(share, requested);
                allocations[i] = share;
                assigned += share;
            }

            // Trim excess from lowest-priority volumes first.
            int index = count - 1;
            while (assigned > capacity && index >= 0)
            {
                if (allocations[index] > 1)
                {
                    allocations[index]--;
                    assigned--;
                }
                else
                {
                    index--;
                }
            }

            // Distribute any remainder to highest-priority volumes first.
            index = 0;
            while (assigned < capacity && index < count)
            {
                int requested = sortedByPriorityDesc[index].ParticleCount;
                if (allocations[index] < requested)
                {
                    allocations[index]++;
                    assigned++;
                }
                else
                {
                    index++;
                }
            }

            return allocations;
        }

        private static ParticleSpawnVolume[] CollectVolumes(IReadOnlyList<ParticleSpawnVolume> volumes)
        {
            if (volumes != null && volumes.Count > 0)
            {
                var enabled = new List<ParticleSpawnVolume>();
                for (int i = 0; i < volumes.Count; i++)
                {
                    ParticleSpawnVolume volume = volumes[i];
                    if (volume != null && volume.isActiveAndEnabled)
                    {
                        enabled.Add(volume);
                    }
                }

                return enabled.ToArray();
            }

            return Object.FindObjectsByType<ParticleSpawnVolume>(FindObjectsSortMode.None);
        }

        private static int ComparePriority(ParticleSpawnVolume a, ParticleSpawnVolume b)
        {
            int byPriority = b.SpawnPriority.CompareTo(a.SpawnPriority);
            return byPriority != 0 ? byPriority : a.GetInstanceID().CompareTo(b.GetInstanceID());
        }
    }
}
