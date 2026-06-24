#if UNITY_EDITOR
using HarmonicEngine.Domain.Models;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HarmonicEngine.Editor
{
    public static class HarmonicFluidProfileAssetBootstrap
    {
        private const string ProfilesFolder = "Assets/Profiles";

        [MenuItem("HarmonicEngine/Profiles/Create Starter Profiles")]
        public static void CreateStarterProfiles()
        {
            Directory.CreateDirectory(ProfilesFolder);
            CreateProfile("Water", 0.1f, 0.2f, 0.4f, 0.3f, 0.1f, 0.6f);
            CreateProfile("Paint", 0.5f, 0.4f, 0.1f, 0.6f, 0.5f, 0.7f);
            CreateProfile("Honey", 0.9f, 0.6f, 0.0f, 0.8f, 0.8f, 0.9f);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateProfile(
            string name,
            float thickness,
            float surfaceTension,
            float bounciness,
            float incompressibility,
            float adhesion,
            float visualGloss)
        {
            string path = $"{ProfilesFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<HarmonicFluidProfile>(path);
            if (existing != null)
            {
                existing.Thickness = thickness;
                existing.SurfaceTension = surfaceTension;
                existing.Bounciness = bounciness;
                existing.Incompressibility = incompressibility;
                existing.Adhesion = adhesion;
                existing.VisualGloss = visualGloss;
                existing.RebuildDerived();
                EditorUtility.SetDirty(existing);
                return;
            }

            var profile = ScriptableObject.CreateInstance<HarmonicFluidProfile>();
            profile.Thickness = thickness;
            profile.SurfaceTension = surfaceTension;
            profile.Bounciness = bounciness;
            profile.Incompressibility = incompressibility;
            profile.Adhesion = adhesion;
            profile.VisualGloss = visualGloss;
            profile.RebuildDerived();
            AssetDatabase.CreateAsset(profile, path);
        }
    }
}
#endif
