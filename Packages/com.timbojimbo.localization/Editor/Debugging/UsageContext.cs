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
        private static readonly List<string> ContextFieldNames = new();
        private static readonly string[] TargetExtensions = { ".prefab", ".asset" };
        private static bool _isInjecting;

        public static bool IsInjecting => _isInjecting;

        static UsageContextInjector()
        {
            RebuildContextFieldCache();
        }

        public static void RebuildContextFieldCache()
        {
            ContextFieldNames.Clear();

            foreach (FieldInfo field in TypeCache.GetFieldsWithAttribute<InjectUsageContextAttribute>())
            {
                if (field.FieldType == typeof(UsageContext) && !ContextFieldNames.Contains(field.Name))
                    ContextFieldNames.Add(field.Name);
            }
        }

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
                if (InjectObjectTree(prefabRoot, path))
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
                InjectObjectTree(prefabStage.prefabContentsRoot, prefabStage.assetPath);
            }
            finally
            {
                _isInjecting = false;
            }
        }

        public static bool InjectObjectTree(UnityEngine.Object target, string sourcePathOverride = null)
        {
            if (target == null) return false;

            if (target is GameObject gameObject)
            {
                bool changed = Inject(gameObject, sourcePathOverride);
                Component[] components = gameObject.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < components.Length; i++)
                    changed |= Inject(components[i], sourcePathOverride);

                return changed;
            }

            return Inject(target, sourcePathOverride);
        }

        public static bool Inject(UnityEngine.Object target, string sourcePathOverride = null)
        {
            if (target == null || ContextFieldNames.Count == 0) return false;

            var serializedObject = new SerializedObject(target);
            serializedObject.UpdateIfRequiredOrScript();

            bool changed = false;

            for (int i = 0; i < ContextFieldNames.Count; i++)
            {
                SerializedProperty rootContextProperty = serializedObject.FindProperty(ContextFieldNames[i]);
                if (rootContextProperty == null) continue;

                changed |= WriteContext(target, target.GetType().Name, rootContextProperty, sourcePathOverride);
            }

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (iterator.propertyType != SerializedPropertyType.Generic && iterator.propertyType != SerializedPropertyType.ManagedReference)
                    continue;

                for (int i = 0; i < ContextFieldNames.Count; i++)
                {
                    SerializedProperty contextProperty = iterator.FindPropertyRelative(ContextFieldNames[i]);
                    if (contextProperty == null) continue;

                    changed |= WriteContext(target, iterator.propertyPath, contextProperty, sourcePathOverride);
                }
            }

            if (changed)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }

            return changed;
        }

        private static bool WriteContext(UnityEngine.Object owner, string propertyPath, SerializedProperty contextProperty, string sourcePathOverride)
        {
            bool changed = false;
            string sourcePath = string.IsNullOrEmpty(sourcePathOverride) ? AssetDatabase.GetAssetPath(owner) : sourcePathOverride;
            string hierarchyPath = GetHierarchyPath(owner, string.IsNullOrEmpty(sourcePathOverride));

            if (!string.IsNullOrEmpty(hierarchyPath))
            {
                sourcePath = ZString.Format("{0} > {1}", sourcePath, hierarchyPath);
            }

            changed |= SetObjectReference(contextProperty, nameof(UsageContext.Context), owner);
            changed |= SetString(contextProperty, nameof(UsageContext.SourcePath), sourcePath);
            changed |= SetString(contextProperty, nameof(UsageContext.PropertyPath), propertyPath);

            return changed;
        }

        private static bool SetString(SerializedProperty parent, string relativePath, string value)
        {
            SerializedProperty property = parent.FindPropertyRelative(relativePath);
            if (property == null || property.stringValue == value) return false;

            property.stringValue = value;
            return true;
        }

        private static bool SetObjectReference(SerializedProperty parent, string relativePath, UnityEngine.Object value)
        {
            SerializedProperty property = parent.FindPropertyRelative(relativePath);
            if (property == null || property.objectReferenceValue == value) return false;

            property.objectReferenceValue = value;
            return true;
        }

        private static string GetHierarchyPath(UnityEngine.Object owner, bool includeSceneName)
        {
            if (owner is Component component)
                return GetTransformPath(component.transform, includeSceneName);

            if (owner is GameObject gameObject)
                return GetTransformPath(gameObject.transform, includeSceneName);

            return string.Empty;
        }

        private static string GetTransformPath(Transform transform, bool includeSceneName)
        {
            if (transform == null) return string.Empty;

            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            Scene scene = transform.gameObject.scene;
            return includeSceneName && scene.IsValid() && !string.IsNullOrEmpty(scene.name) ? scene.name + "/" + path : path;
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

            UsageContextInjector.RebuildContextFieldCache();

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
            UsageContextInjector.RebuildContextFieldCache();
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

            UsageContextInjector.RebuildContextFieldCache();

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
                UsageContextInjector.InjectAllInPrefabStage(prefabStage);

            for (int i = 0; i < SceneManager.sceneCount; i++)
                UsageContextInjector.InjectAllInScene(SceneManager.GetSceneAt(i));
        }
    }
}
