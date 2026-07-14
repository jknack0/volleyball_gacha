using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace VG.EditorTools
{
    /// <summary>
    /// Automates docs/ai-character-pipeline.md §3.5 end to end.
    ///
    /// 1. AssetPostprocessor: any model under Assets/Art/Characters/ imports as Humanoid with
    ///    cleaned clip names (idle/run/jump/dive) and loop flags — zero clicks, fires on import.
    /// 2. Menu "VG/Build Character Prefabs": for every character folder, extracts the texture
    ///    atlas, creates VG/Toon + VG/Outline materials, builds an AnimatorController
    ///    (idle default), assigns everything, and saves char_<id>.prefab. Idempotent — safe to
    ///    re-run after regenerating a character.
    /// </summary>
    public sealed class CharacterModelPostprocessor : AssetPostprocessor
    {
        private const string CharRoot = "Assets/Art/Characters/";
        private const string AnimRoot = "Assets/Art/Anim/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(CharRoot) && !assetPath.StartsWith(AnimRoot)) return;
            var importer = (ModelImporter)assetImporter;
            // HUMANOID experiment [structural]: the AA_Volleyball mocap bank (Bip01 skeleton) is
            // consumed via Unity Humanoid retargeting onto the Meshy characters (clean Mixamo bone
            // names — unlike Tripo's rig, which is why this was Generic before). If a character
            // avatar mis-maps (backwards head / warped feet), flip CharactersUseHumanoid back.
            importer.animationType = ModelImporterAnimationType.Human;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; // we assign VG/Toon ourselves
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(CharRoot) && !assetPath.StartsWith(AnimRoot)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                // Take names arrive exporter-prefixed ("Armature|ready") — keep the tail.
                int bar = clip.name.LastIndexOf('|');
                if (bar >= 0) clip.name = clip.name[(bar + 1)..];

                // Loop stances/locomotion; one-shot actions stay clamped.
                string n = clip.name.ToLowerInvariant();
                clip.loopTime = n.Contains("idle") || n.Contains("ready") || n.Contains("posture")
                    || n.Contains("shuffle") || n.Contains("sprint") || n.Contains("walk")
                    || n.Contains("run");

                // In-place mocap: keep the character where the game puts her.
                clip.lockRootPositionXZ = true;
                clip.lockRootHeightY = true;
                clip.lockRootRotation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalOrientation = true;
            }
            importer.clipAnimations = clips;
        }
    }

    public static class CharacterPrefabBuilder
    {
        private const string Root = "Assets/Art/Characters";

        [MenuItem("VG/Build Character Prefabs")]
        public static void BuildAll()
        {
            foreach (string dir in Directory.GetDirectories(Root))
            {
                string charId = Path.GetFileName(dir);
                try
                {
                    Build(dir.Replace('\\', '/'), charId);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"VG: {charId} failed: {e.Message}");
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static void Build(string dir, string charId)
        {
            // Source rules [structural], learned the hard way:
            // - unity_model.fbx (tools/blender_unity_prep.py output) is THE import: one armature,
            //   one mesh, clean-named takes, normalized axes. Raw Tripo FBXs are fallbacks only.
            // - Texture NEVER from an animated FBX: Tripo's retarget ships a garbled atlas.
            string preppedFbx = $"{dir}/unity_model.fbx";
            string animFbx = $"{dir}/tripo_anim_model.fbx";
            string riggedFbx = $"{dir}/tripo_rigged_model.fbx";
            string staticFbx = $"{dir}/tripo_model_model.fbx";
            string visualPath = File.Exists(preppedFbx) ? preppedFbx
                : File.Exists(animFbx) ? animFbx
                : File.Exists(riggedFbx) ? riggedFbx : staticFbx;
            string clipsPath = visualPath;
            if (!File.Exists(visualPath))
            {
                Debug.LogWarning($"VG: {charId}: no tripo FBX found, skipping.");
                return;
            }

            // Re-run the postprocessor on both FBXs so importer-setting changes (e.g. the
            // Humanoid→Generic switch) apply without a manual right-click→Reimport.
            AssetDatabase.ImportAsset(visualPath, ImportAssetOptions.ForceUpdate);
            if (clipsPath != visualPath)
                AssetDatabase.ImportAsset(clipsPath, ImportAssetOptions.ForceUpdate);

            // --- texture atlas: NEVER from the animated FBX (garbled) ---
            Texture2D atlas = FindOrExtractTexture(dir, staticFbx, riggedFbx);

            // --- materials (idempotent: reuse if present) ---
            var toon = LoadOrCreateMaterial($"{dir}/{charId}_toon.mat", "VG/Toon", m =>
            {
                if (atlas != null) m.SetTexture("_BaseMap", atlas);
                m.SetColor("_BaseColor", Color.white);
            });
            var ink = LoadOrCreateMaterial($"{dir}/{charId}_ink.mat", "VG/Outline", m =>
            {
                m.SetFloat("_OutlineWidth", 0.02f); // character-scale ink [tunable]
                m.SetFloat("_UseBakedNormals", 1f); // prep script bakes smoothed normals → vertex color
            });

            // --- animator controller: professional mocap bank (Humanoid retarget) ---
            // Clips come straight from the AA_Volleyball bank FBX: Humanoid = muscle curves,
            // so the Generic-era node-curve sanitization is unnecessary (and would strip them).
            // Falls back to the character's own (sanitize-free, Humanoid) clips if no bank.
            const string BankPath = "Assets/Art/Anim/Volleyball/aa_core.fbx";
            string clipSource = File.Exists(BankPath) ? BankPath : clipsPath;
            var clips = AssetDatabase.LoadAllAssetRepresentationsAtPath(clipSource)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToList();
            RuntimeAnimatorController controller = null;
            if (clips.Count > 0)
            {
                string ctrlPath = $"{dir}/{charId}_anim.controller";
                AssetDatabase.DeleteAsset(ctrlPath);
                var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                var ordered = clips.OrderBy(c => c.name == "ready" ? 0 : c.name == "idle" ? 1 : 2).ToList();
                foreach (var clip in ordered)
                    ctrl.AddMotion(clip); // first added = default state
                controller = ctrl;
            }
            // --- prefab: instantiate, wire materials + controller, save ---
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = new[] { toon, ink };

                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                if (controller != null) animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; // in-place clips; the game owns position

                string prefabPath = $"{dir}/{charId}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

                // Runtime copy: GreyBoxMatch spawns characters via Resources.Load("VGCharacters/<id>").
                if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                if (!AssetDatabase.IsValidFolder("Assets/Resources/VGCharacters"))
                    AssetDatabase.CreateFolder("Assets/Resources", "VGCharacters");
                string runtimePath = $"Assets/Resources/VGCharacters/{charId}.prefab";
                AssetDatabase.DeleteAsset(runtimePath);
                AssetDatabase.CopyAsset(prefabPath, runtimePath);

                Debug.Log($"VG: built {prefabPath} (+ Resources copy) (atlas: {(atlas ? atlas.name : "none")}, clips: {clips.Count})");
                EditorGUIUtility.PingObject(prefab);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static Texture2D FindOrExtractTexture(string dir, params string[] fbxPaths)
        {
            // Already-extracted texture in the folder?
            Texture2D existing = AssetDatabase.FindAssets("t:Texture2D", new[] { dir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.EndsWith(".fbx"))
                .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
                .FirstOrDefault(t => t != null);
            if (existing != null) return existing;

            foreach (string fbx in fbxPaths.Where(File.Exists))
            {
                if (AssetImporter.GetAtPath(fbx) is ModelImporter importer)
                {
                    importer.ExtractTextures(dir);
                    AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate);
                }
            }
            AssetDatabase.Refresh();

            return AssetDatabase.FindAssets("t:Texture2D", new[] { dir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
                .FirstOrDefault(t => t != null);
        }

        private static Material LoadOrCreateMaterial(string path, string shaderName, System.Action<Material> configure)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null) throw new System.Exception($"shader {shaderName} not found");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            configure(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
