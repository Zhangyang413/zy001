using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 一键生成 Windows 独立运行包：输出的文件夹里有 .exe 与 _Data 等全部文件，
/// 双击 .exe 就能直接玩，不需要再装团结引擎或任何其他软件。
///
/// 两种用法：
///   A. 批处理（推荐，不用点编辑器界面；要求工程没被编辑器打开，否则项目锁会挡住）：
///      Tuanjie.exe -batchmode -nographics -quit -projectPath "工程目录" ^
///          -executeMethod BuildStandalone.BuildWindows64 ^
///          -buildOutput "E:\zy001_build" -logFile "E:\zy001_build\build.log"
///      成功时日志里会打印 BUILD_OK，失败时打印 BUILD_FAILED。
///
///   B. 编辑器里手动点：菜单「构建 / 生成 Windows 独立运行包」，
///      默认输出到工程目录下的 Builds\PickUpAdventure（已在 .gitignore 里）。
/// </summary>
public static class BuildStandalone
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ExeName = "PickUpAdventure";          // 捡物品大冒险
    private const string CompanyName = "ZhangYang";
    private const string ProductName = "捡物品大冒险";

    [MenuItem("构建/生成 Windows 独立运行包")]
    public static void BuildWindows64()
    {
        string outputRoot = GetArgument("-buildOutput");
        if (string.IsNullOrEmpty(outputRoot)) outputRoot = Path.Combine(Directory.GetCurrentDirectory(), "Builds");

        string outDir = Path.Combine(outputRoot, ExeName);
        string exePath = Path.Combine(outDir, ExeName + ".exe");
        bool succeeded = false;

        try
        {
            ApplyPlayerSettings();

            // 工程默认场景列表是空的，这里补上 SampleScene（BuildPlayer 也用这个列表）
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Directory.CreateDirectory(outDir);
            ClearDirectory(outDir);                 // 先清空旧产物，避免残留文件混进包里
            Debug.Log("[独立运行包] 开始构建：" + exePath);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log(string.Format("[独立运行包] 构建结果 {0}，耗时 {1:0.0} 秒，产物 {2:0.0} MB",
                                   summary.result, summary.totalTime.TotalSeconds, summary.totalSize / 1024f / 1024f));

            if (summary.result == BuildResult.Succeeded)
            {
                succeeded = true;
                Debug.Log("BUILD_OK exe=" + exePath);
            }
            else
            {
                Debug.LogError("BUILD_FAILED " + summary.result + "（详见 Editor 里的构建报告）");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("BUILD_FAILED 异常：" + e);
        }

        // 批处理模式下要主动退出，否则命令行会一直挂着等编辑器
        if (Application.isBatchMode) EditorApplication.Exit(succeeded ? 0 : 1);
    }

    /// <summary>发布用的 Player 设置：中文产品名、窗口模式、Mono 后端（自带运行时，体积小、不依赖外部软件）。</summary>
    private static void ApplyPlayerSettings()
    {
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = ProductName;
        PlayerSettings.bundleVersion = "1.0.0";

        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;

        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard_2_0);
    }

    /// <summary>读取命令行参数，例如 -buildOutput "E:\zy001_build"。</summary>
    private static string GetArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        }
        return null;
    }

    /// <summary>清空输出目录里的旧文件（只删自己这一层，不动上级目录）。</summary>
    private static void ClearDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return;

        foreach (string file in Directory.GetFiles(dir))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[独立运行包] 无法删除旧文件 " + file + "：" + e.Message);
            }
        }

        foreach (string sub in Directory.GetDirectories(dir))
        {
            try
            {
                Directory.Delete(sub, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[独立运行包] 无法删除旧目录 " + sub + "：" + e.Message);
            }
        }
    }
}
