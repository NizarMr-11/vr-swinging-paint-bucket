using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarmonicEngine.Tests.PlayMode
{
    internal static class PlayModeTestUtility
    {
        /// <summary>
        /// Ensures the editor is in Play Mode. Safe for Edit Mode test runs that host Play Mode tests.
        /// </summary>
        public static IEnumerator EnsurePlayMode()
        {
            if (!Application.isPlaying)
            {
                yield return new EnterPlayMode();
            }

            yield return null;
        }

        public static IEnumerator ExitPlayModeIfNeeded()
        {
            if (Application.isPlaying)
            {
                yield return new ExitPlayMode();
            }
        }
    }
}
