using UnityEditor;
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.IO;
using System.Collections.Generic;

public class WeChatSDKPlatformSwitcher : IActiveBuildTargetChanged
{
    public int callbackOrder => 0;

    public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
    {
        Debug.Log($"========== [WeChatSDK] 检测到平台切换: {previousTarget} → {newTarget} ==========");
        SetWeChatDLLCompatibility(newTarget);
        Debug.Log($"========== [WeChatSDK] 平台配置完成 ==========");
    }

    public void SetWeChatDLLCompatibility(BuildTarget target)
    {
        string projectPath = Application.dataPath.Replace("/Assets", "").Replace("\\Assets", "");
        string packagePath = Path.Combine(projectPath, "Packages", "com.qq.weixin.minigame");

        if (!Directory.Exists(packagePath))
        {
            Debug.LogError($"[WeChatSDK] ❌ SDK 目录不存在: {packagePath}");
            return;
        }

        // ✅ 处理所有 Runtime/Plugins 下的 DLL
        string[] allDlls = Directory.GetFiles(packagePath, "*.dll", SearchOption.AllDirectories);
        List<string> targetDlls = new List<string>();

        foreach (string dll in allDlls)
        {
            if (!dll.Contains("Runtime/Plugins") && !dll.Contains("Runtime\\Plugins"))
                continue;
            if (dll.Contains("editor") || dll.Contains("Editor"))
                continue;
            targetDlls.Add(dll);
        }

        if (targetDlls.Count == 0)
        {
            Debug.LogError("[WeChatSDK] ❌ 未找到任何需要处理的 DLL！");
            return;
        }

        Debug.Log($"[WeChatSDK] 找到 {targetDlls.Count} 个 DLL，目标平台: {target}");

        int processedCount = 0;
        foreach (string fullPath in targetDlls)
        {
            string fileName = Path.GetFileName(fullPath);
            string relativePath = "Packages/com.qq.weixin.minigame" + fullPath.Replace(packagePath, "").Replace("\\", "/");

            Debug.Log($"[WeChatSDK] 正在处理: {relativePath}");

            PluginImporter importer = AssetImporter.GetAtPath(relativePath) as PluginImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[WeChatSDK] ⚠️ 无法获取 PluginImporter，尝试强制导入");
                AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(relativePath) as PluginImporter;

                if (importer == null)
                {
                    Debug.LogError($"[WeChatSDK] ❌ 仍然无法获取 PluginImporter: {relativePath}");
                    continue;
                }
            }

            // ✅ 关键修复：设置所有平台
            importer.SetCompatibleWithAnyPlatform(false);

            if (target == BuildTarget.Android)
            {
                importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
                importer.SetCompatibleWithPlatform(BuildTarget.WebGL, false);
                // 确保其他平台也都禁用
                importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, false);
                importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
                importer.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX, false);
                importer.SetCompatibleWithPlatform(BuildTarget.iOS, false);
                Debug.Log($"[WeChatSDK] ✅ Android: 已禁用 {fileName}");
            }
            else if (target == BuildTarget.WebGL)
            {
                importer.SetCompatibleWithPlatform(BuildTarget.WebGL, true);
                importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
                Debug.Log($"[WeChatSDK] ✅ WebGL: 已启用 {fileName}");
            }

            importer.SaveAndReimport();
            processedCount++;
        }

        // ✅ 关键步骤：清理编译缓存
        string libraryPath = Path.Combine(projectPath, "Library");
        string scriptAssembliesPath = Path.Combine(libraryPath, "ScriptAssemblies");
        if (Directory.Exists(scriptAssembliesPath))
        {
            Debug.Log("[WeChatSDK] 清理编译缓存...");
            foreach (string file in Directory.GetFiles(scriptAssembliesPath, "*.dll"))
            {
                try { File.Delete(file); } catch { }
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"[WeChatSDK] ✅ 配置完成，共处理 {processedCount} 个 DLL");

        // ✅ 强制重新编译
        EditorUtility.RequestScriptReload();
    }

    // ✅ 菜单项：配置 Android
    [MenuItem("Tools/WeChatSDK/配置 Android (禁用微信 SDK)")]
    public static void SetupAndroid()
    {
        Debug.Log("========== [WeChatSDK] 配置 Android 平台 ==========");
        var switcher = new WeChatSDKPlatformSwitcher();
        switcher.SetWeChatDLLCompatibility(BuildTarget.Android);
        Debug.Log("========== [WeChatSDK] Android 配置完成 ==========");
        EditorUtility.DisplayDialog("配置完成", "微信 SDK DLL 已禁用，编译缓存已清理\n\n请重新编译后再打包 Android", "确定");
    }

    // ✅ 菜单项：配置 WebGL/小游戏
    [MenuItem("Tools/WeChatSDK/配置 WebGL (启用微信 SDK)")]
    public static void SetupWebGL()
    {
        Debug.Log("========== [WeChatSDK] 配置 WebGL 平台 ==========");
        var switcher = new WeChatSDKPlatformSwitcher();
        switcher.SetWeChatDLLCompatibility(BuildTarget.WebGL);
        Debug.Log("========== [WeChatSDK] WebGL 配置完成 ==========");
        EditorUtility.DisplayDialog("配置完成", "微信 SDK DLL 已启用，编译缓存已清理\n\n请重新编译后再打包 WebGL", "确定");
    }

    // ✅ 带按钮的编辑器窗口
    [MenuItem("Tools/WeChatSDK/平台切换工具")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow<WeChatSDKSwitchWindow>("微信 SDK 平台切换");
    }
}

public class WeChatSDKSwitchWindow : EditorWindow
{
    private BuildTarget currentTarget;
    private string statusMessage = "就绪";

    private void OnEnable()
    {
        currentTarget = EditorUserBuildSettings.activeBuildTarget;
        UpdateStatusMessage();
    }

    private void UpdateStatusMessage()
    {
        if (currentTarget == BuildTarget.Android)
        {
            statusMessage = "当前: Android (微信 SDK 已禁用)";
        }
        else if (currentTarget == BuildTarget.WebGL)
        {
            statusMessage = "当前: WebGL (微信 SDK 已启用)";
        }
        else
        {
            statusMessage = $"当前: {currentTarget}";
        }
    }

    private void OnGUI()
    {
        GUILayout.Space(10);

        EditorGUILayout.LabelField("微信 SDK 平台切换工具", EditorStyles.boldLabel);
        GUILayout.Space(5);
        EditorGUILayout.LabelField("一键切换平台并自动配置微信 SDK DLL", EditorStyles.helpBox);
        GUILayout.Space(10);

        EditorGUILayout.LabelField("当前平台:", EditorStyles.label);
        EditorGUILayout.LabelField($"  {currentTarget}", EditorStyles.boldLabel);

        GUILayout.Space(5);
        EditorGUILayout.LabelField("状态:", statusMessage, EditorStyles.helpBox);

        GUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Space(10);

        // 按钮1：配置 Android
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("📱 配置 Android 平台 (禁用微信 SDK)", GUILayout.Height(50)))
        {
            WeChatSDKPlatformSwitcher.SetupAndroid();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);

        // 按钮2：配置 WebGL/小游戏
        GUI.backgroundColor = new Color(0.7f, 0.8f, 1f);
        if (GUILayout.Button("🌐 配置 WebGL / 微信小游戏 (启用微信 SDK)", GUILayout.Height(50)))
        {
            WeChatSDKPlatformSwitcher.SetupWebGL();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Space(10);

        if (GUILayout.Button("📋 检查 DLL 状态", GUILayout.Height(30)))
        {
            CheckDLLStatus();
        }

        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "使用说明:\n" +
            "1. 点击 '配置 Android' 禁用微信 SDK\n" +
            "2. 点击 '配置 WebGL' 启用微信 SDK\n" +
            "3. 配置后会自动清理缓存并重新编译",
            MessageType.Info
        );
    }

    private void CheckDLLStatus()
    {
        Debug.Log("========== [WeChatSDK] 检查 DLL 状态 ==========");

        string projectPath = Application.dataPath.Replace("/Assets", "").Replace("\\Assets", "");
        string packagePath = Path.Combine(projectPath, "Packages", "com.qq.weixin.minigame");

        if (!Directory.Exists(packagePath))
        {
            Debug.LogError("[WeChatSDK] ❌ SDK 目录不存在！");
            return;
        }

        string[] allDlls = Directory.GetFiles(packagePath, "*.dll", SearchOption.AllDirectories);
        foreach (string dll in allDlls)
        {
            if (!dll.Contains("Runtime/Plugins"))
                continue;
            if (dll.Contains("editor") || dll.Contains("Editor"))
                continue;

            string relPath = "Packages/com.qq.weixin.minigame" + dll.Replace(packagePath, "").Replace("\\", "/");

            var importer = AssetImporter.GetAtPath(relPath) as PluginImporter;
            if (importer != null)
            {
                bool webgl = importer.GetCompatibleWithPlatform(BuildTarget.WebGL);
                bool android = importer.GetCompatibleWithPlatform(BuildTarget.Android);
                Debug.Log($"[WeChatSDK] {Path.GetFileName(dll)}: WebGL={webgl}, Android={android}");
            }
        }

        EditorUtility.DisplayDialog("检查完成", "详情请查看 Console 日志", "确定");
    }

    private void OnInspectorUpdate()
    {
        if (currentTarget != EditorUserBuildSettings.activeBuildTarget)
        {
            currentTarget = EditorUserBuildSettings.activeBuildTarget;
            UpdateStatusMessage();
            Repaint();
        }
    }
}