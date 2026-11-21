// 这个文件原本是一个空的 SceneManager : MonoSingleton<SceneManager> 壳子。
//
// 它已经被移除，原因：类名 SceneManager 位于 namespace Play 下，会在 Play 及其子命名空间里
// 遮蔽 UnityEngine.SceneManagement.SceneManager，导致 SceneLoader 里
// SceneManager.LoadSceneAsync / GetSceneByName / SetActiveScene 全部报
// "does not contain a definition for ..." 编译错误。
//
// 场景管理器的实际实现见：Assets/Scripts/Play/General/SceneLoader.cs
// 用法：SceneLoader.Instance.LoadScene("关卡名", 目标位置, 是否渐变);
