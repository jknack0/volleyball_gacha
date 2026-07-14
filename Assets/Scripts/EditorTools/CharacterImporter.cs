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
        private const string Root = "Assets/Art/Characters/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; // we assign VG/Toon ourselves
        }

        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                string n = clip.name.ToLowerInvariant();
                foreach (var key in new[] { "idle", "run", "walk", "jump", "dive" })
                {
                    if (!n.Contains(key)) continue;
                    clip.name = key;
                    clip.loopTime = key == "idle" || key == "run" || key == "walk";
                    break;
                }
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
            // Source split [structural]: Tripo's animate_retarget FBX ships a GARBLED texture —
            // visuals come from the rigged (or static) FBX, ONLY the clips come from the animated
            // one (Humanoid retargeting makes clips portable across avatars of the same skeleton).
            string animFbx = $"{dir}/tripo_anim_model.fbx";
            string riggedFbx = $"{dir}/tripo_rigged_model.fbx";
            string staticFbx = $"{dir}/tripo_model_model.fbx";
            string visualPath = File.Exists(riggedFbx) ? riggedFbx : staticFbx;
            string clipsPath = File.Exists(animFbx) ? animFbx : visualPath;
            if (!File.Exists(visualPath))
            {
                Debug.LogWarning($"VG: {charId}: no tripo FBX found, skipping.");
                return;
            }

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
            });

            // --- animator controller: idle default + every other clip as a state ---
            var clips = AssetDatabase.LoadAllAssetRepresentationsAtPath(clipsPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToList();
            RuntimeAnimatorController controller = null;
            if (clips.Count > 0)
            {
                string ctrlPath = $"{dir}/{charId}_anim.controller";
                AssetDatabase.DeleteAsset(ctrlPath);
                var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                var ordered = clips.OrderBy(c => c.name == "idle" ? 0 : 1).ToList();
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
