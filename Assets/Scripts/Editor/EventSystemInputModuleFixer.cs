using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace NGJ2026.EditorTools
{
    /// <summary>
    /// 项目已切到 Input System（Active Input Handling = Input System Package），
    /// 但场景里若还留着旧的 StandaloneInputModule，EventSystem 每帧都会抛
    /// InvalidOperationException: You are trying to read Input using the UnityEngine.Input class...
    /// 这个工具负责把旧模块换成 InputSystemUIInputModule。
    ///
    /// 打开工程 / 打开场景时会自动修一次（只改动真的需要改的场景）；
    /// 也可以手动执行菜单 Tools/修复/替换 EventSystem 旧输入模块，一次修完所有场景。
    /// </summary>
    [InitializeOnLoad]
    internal static class EventSystemInputModuleFixer
    {
        static EventSystemInputModuleFixer()
        {
            EditorApplication.delayCall += () =>
            {
                var changed = FixOpenScenes(save: false) > 0;
                if (changed)
                {
                    Debug.Log("[修复] EventSystem 的旧输入模块已换成 InputSystemUIInputModule，记得保存场景（Ctrl+S）。");
                }
            };

            // 新打开的场景也顺手处理
            EditorSceneManager.sceneOpened += (scene, mode) => FixScene(scene, save: false);
        }

        [MenuItem("Tools/修复/替换 EventSystem 旧输入模块", false, 10)]
        private static void FixAllScenes()
        {
            var fixedCount = FixOpenScenes(save: true);

            foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || IsSceneLoaded(path)) continue;

                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                if (FixScene(scene, save: true)) fixedCount++;
                EditorSceneManager.CloseScene(scene, true);
            }

            Debug.Log(fixedCount > 0
                ? $"[修复] 共修复 {fixedCount} 个场景。"
                : "[修复] 没有发现需要替换的 StandaloneInputModule。");
        }

        private static bool IsSceneLoaded(string path)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).path == path) return true;
            }

            return false;
        }

        private static int FixOpenScenes(bool save)
        {
            var count = 0;

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                if (FixScene(SceneManager.GetSceneAt(i), save)) count++;
            }

            return count;
        }

        private static bool FixScene(Scene scene, bool save)
        {
            if (!scene.IsValid() || !scene.isLoaded) return false;

            var changed = false;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var legacy in root.GetComponentsInChildren<StandaloneInputModule>(true))
                {
                    var go = legacy.gameObject;

                    var replacement = go.AddComponent<InputSystemUIInputModule>();
                    replacement.AssignDefaultActions();
                    Object.DestroyImmediate(legacy, true);

                    changed = true;
                    Debug.Log($"[修复] {scene.name}/{go.name}：StandaloneInputModule → InputSystemUIInputModule", go);
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (save) EditorSceneManager.SaveScene(scene);
            }

            return changed;
        }
    }
}
