using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景自举脚本，解决“用编辑器打开工程后点 Play 什么都不显示”的问题。
///
/// 背景：这个工程以前从来没有在团结引擎里真正打开过场景，
/// 结果 Library\LastSceneManagerSetup.txt 里记录的场景路径是空的，
/// 编辑器打开工程时 Hierarchy 是空的 —— 点 Play 跑的是一个空场景，
/// 屏幕什么都不显示，也不会执行任何游戏脚本（日志里一条都没有）。
/// 另外 SampleScene.unity 的文件头还是旧版 Unity 的 %TAG unity3d.com,2011，
/// 而团结引擎自己写的是 yousandi.cn,2023，顺手用这个脚本把它规范化一遍。
///
/// 两件事：
///   1. 菜单「场景/打开并保存 SampleScene」：打开场景并重新保存，
///      把文件规范成团结引擎的原生格式，同时让编辑器记下“上次打开的场景”。
///   2. [InitializeOnLoadMethod]：打开工程时如果一个场景都没打开，
///      自动打开 SampleScene —— 这样以后再也不会出现“打开工程是空场景”。
///
/// 批处理用法（要求工程处于关闭状态，否则项目锁会挡住）：
///   Tuanjie.exe -batchmode -quit -projectPath "工程目录" ^
///       -executeMethod SceneBootstrap.NormalizeScenes -logFile 日志路径
///   成功时日志里会打印 SCENE_OK，失败打印 SCENE_FAILED。
/// </summary>
public static class SceneBootstrap
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    /// <summary>打开并重新保存场景：规范文件格式 + 写下“上次打开的场景”。</summary>
    [MenuItem("场景/打开并保存 SampleScene（规范化）")]
    public static void NormalizeScenes()
    {
        bool ok = false;
        try
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("SCENE_FAILED 找不到 " + ScenePath);
            }
            else
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    Debug.LogError("SCENE_FAILED 保存失败：" + ScenePath);
                }
                else
                {
                    ok = true;
                    Debug.Log("SCENE_OK path=" + ScenePath
                              + "  根对象数=" + scene.rootCount
                              + "  场景数=" + SceneManager.sceneCount);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("SCENE_FAILED 异常：" + e);
        }

        // 批处理模式下必须主动退出（成功和失败都要走到这里），否则命令行会一直挂着等编辑器
        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    /// <summary>
    /// 打开工程 / 脚本重编译之后，如果一个场景都没打开，就自动打开 SampleScene。
    /// 只在图形界面下生效（批处理构建不需要，也不该被它干扰）。
    /// </summary>
    [InitializeOnLoadMethod]
    private static void AutoOpenSceneOnLoad()
    {
        if (Application.isBatchMode) return;

        // 延迟到编辑器完全就绪之后再做，避免在资产导入阶段动场景
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.sceneCount > 0) return;          // 已经有场景打开着，不打扰用户
            if (!File.Exists(ScenePath)) return;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[场景自举] 打开工程时没有打开任何场景，已自动打开 " + scene.path + "，现在可以直接点 Play。");
        };
    }
}
