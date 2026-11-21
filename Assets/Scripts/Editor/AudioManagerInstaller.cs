using Play.General;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NGJ2026.EditorTools
{
    /// <summary>
    /// 一键把 AudioManager 挂到当前场景，省得手动建空物体 + Add Component。
    /// </summary>
    internal static class AudioManagerInstaller
    {
        [MenuItem("Tools/音频/在当前场景创建 AudioManager", false, 100)]
        private static void CreateInCurrentScene()
        {
            var existing = Object.FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);
            if (existing != null)
            {
                EditorGUIUtility.PingObject(existing);
                Selection.activeObject = existing;
                return;
            }

            var go = new GameObject("[AudioManager]");
            Undo.RegisterCreatedObjectUndo(go, "创建 AudioManager");
            go.AddComponent<AudioManager>();

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);

            Debug.Log("[AudioManager] 已创建。选中它，在 Inspector 的「音频表」里点 + 拖入 AudioClip 即可。", go);
        }

        [MenuItem("Tools/音频/在当前场景创建 AudioManager", true)]
        private static bool CreateInCurrentSceneValidate()
        {
            return !Application.isPlaying;
        }
    }
}
