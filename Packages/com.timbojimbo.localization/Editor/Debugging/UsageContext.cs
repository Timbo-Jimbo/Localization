using System;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TimboJimbo.Localization.Debugging;
using Cysharp.Text;

namespace TimboJimboEditor.Localization.Debugging
{
    internal static class UsageContextInjector
    {
        private static readonly string[] TargetExtensions = { ".prefab", ".asset" };
        private static bool _isInjecting;

        public static bool IsInjecting => _isInjecting;

        public static void Inject(string path)
        {
            if (!CanInjectPath(path)) return;

            if (_isInjecting) return;

            _isInjecting = true;
            try
            {
                InjectInternal(path);
            }
            finally
            {
                _isInjecting = false;
            }
        }

        private static void InjectInternal(string path)
        {
            if (IsPrefabPath(path))
            {
                InjectPrefabPath(path);
                return;
            }

            bool changed = false;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                changed |= InjectObjectTree(asset);

            if (changed)
                SaveAssetPath(path);
        }

        private static void SaveAssetPath(string path)
        {
            UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
            if (mainAsset != null)
                EditorUtility.SetDirty(mainAsset);

            AssetDatabase.SaveAssetIfDirty(AssetDatabase.GUIDFromAssetPath(path));
        }

        private static void InjectPrefabPath(string path)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (InjectObjectTree(prefabRoot))
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            }
            finally
            {
                if (prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool CanInjectPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            for (int i = 0; i < TargetExtensions.Length; i++)
            {
                if (path.EndsWith(TargetExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsPrefabPath(string path)
        {
            return path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        public static void InjectAllInScene(Scene scene)
        {
            if (_isInjecting || !scene.IsValid() || !scene.isLoaded) return;

            _isInjecting = true;
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                    InjectObjectTree(roots[i]);
            }
            finally
            {
                _isInjecting = false;
            }
        }

        public static void InjectAllInPrefabStage(PrefabStage prefabStage)
        {
            if (_isInjecting || prefabStage == null || prefabStage.prefabContentsRoot == null) return;

            _isInjecting = true;
            try
            {
                InjectObjectTree(prefabStage.prefabContentsRoot);
            }
            finally
            {
                _isInjecting = false;
            }
        }

        public static bool InjectObjectTree(UnityEngine.Object target)
        {
            if (target == null) return false;

            if (target is GameObject gameObject)
            {
                bool changed = Inject(gameObject);
                Component[] components = gameObject.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < components.Length; i++)
                    changed |= Inject(components[i]);

                return changed;
            }

            return Inject(target);
        }

        public static bool Inject(UnityEngine.Object target)
        {
            if (target == null) return false;

            var serializedObject = new SerializedObject(target);
            serializedObject.UpdateIfRequiredOrScript();

            bool changed = false;
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (!IsUsageContextProperty(iterator))
                    continue;

                changed |= WriteContext(target, iterator);
            }

            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }

            return changed;
        }

        private static bool IsUsageContextProperty(SerializedProperty property)
        {
            return property.propertyType == SerializedPropertyType.Generic && property.type == nameof(UsageContext);
        }

        private static bool WriteContext(UnityEngine.Object owner, SerializedProperty contextProperty)
        {
            SerializedProperty property = contextProperty.FindPropertyRelative(nameof(UsageContext.Context));
            if (property == null || property.objectReferenceValue == owner) return false;

            property.objectReferenceValue = owner;
            return true;
        }
    }

    public sealed class UsageContexdtInjectorSaveProcessor : AssetModificationProcessor
    {
        private static readonly HashSet<string> PendingInjectPaths = new();
        private static bool _flushQueued;

        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (UsageContextInjector.IsInjecting)
                return paths;

            for (int i = 0; i < paths.Length; i++)
                QueueInject(paths[i]);

            return paths;
        }

        private static void OnWillCreateAsset(string path)
        {
            QueueInject(NormalizeCreatedPath(path));
        }

        private static AssetMoveResult OnWillMoveAsset(string sourcePath, string destinationPath)
        {
            QueueInject(destinationPath);
            return AssetMoveResult.DidNotMove;
        }

        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            PendingInjectPaths.Remove(assetPath);
            return AssetDeleteResult.DidNotDelete;
        }

        private static void QueueInject(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            PendingInjectPaths.Add(path);
            if (_flushQueued) return;

            _flushQueued = true;
            EditorApplication.delayCall += FlushQueuedInjects;
        }

        private static void FlushQueuedInjects()
        {
            _flushQueued = false;
            if (PendingInjectPaths.Count == 0) return;

            string[] paths = new string[PendingInjectPaths.Count];
            PendingInjectPaths.CopyTo(paths);
            PendingInjectPaths.Clear();

            for (int i = 0; i < paths.Length; i++)
                UsageContextInjector.Inject(paths[i]);
        }

        private static string NormalizeCreatedPath(string path)
        {
            return path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ? path[..^5] : path;
        }
    }

    [InitializeOnLoad]
    public static class UsageContextSceneSaveHook
    {
        private static bool _hierarchyInjectQueued;

        static UsageContextSceneSaveHook()
        {
            EditorSceneManager.sceneSaving += OnSceneSaving;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private static void OnSceneSaving(Scene scene, string path)
        {
            UsageContextInjector.InjectAllInScene(scene);
        }

        private static void OnHierarchyChanged()
        {
            if (_hierarchyInjectQueued || UsageContextInjector.IsInjecting || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            _hierarchyInjectQueued = true;
            EditorApplication.delayCall += FlushHierarchyInject;
        }

        private static void FlushHierarchyInject()
        {
            _hierarchyInjectQueued = false;

            if (UsageContextInjector.IsInjecting || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
                UsageContextInjector.InjectAllInPrefabStage(prefabStage);

            for (int i = 0; i < SceneManager.sceneCount; i++)
                UsageContextInjector.InjectAllInScene(SceneManager.GetSceneAt(i));
        }
    }
}
