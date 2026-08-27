#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

public class SceneTeleporterTool : Editor
{
    private const string SCENE_CACHE_FILE = "LastScenePath.cache";

    private static string GetCacheFilePath()
    {
        string tempPath = Path.Combine(Application.dataPath, "..", "Temp");
        if (!Directory.Exists(tempPath))
        {
            Directory.CreateDirectory(tempPath);
        }
        return Path.Combine(tempPath, SCENE_CACHE_FILE);
    }

    private static void SaveScenePathToCache(string scenePath)
    {
        try
        {
            string filePath = GetCacheFilePath();
            File.WriteAllText(filePath, scenePath);
            Z_Logger.Log($"🔵 [SaveScenePath] 已保存场景路径: {scenePath}");
        }
        catch (System.Exception ex)
        {
            Z_Logger.LogError($"🔴 [SaveScenePath] 保存失败: {ex.Message}");
        }
    }

    private static string LoadScenePathFromCache()
    {
        try
        {
            string filePath = GetCacheFilePath();
            if (File.Exists(filePath))
            {
                string scenePath = File.ReadAllText(filePath);
                if (!string.IsNullOrEmpty(scenePath) && File.Exists(scenePath))
                {
                    Z_Logger.Log($"🔵 [LoadScenePath] 读取成功: {scenePath}");
                    return scenePath;
                }
                else
                {
                    Z_Logger.Log("🔵 [LoadScenePath] 缓存文件内容无效，删除缓存");
                    File.Delete(filePath);
                }
            }
        }
        catch (System.Exception ex)
        {
            Z_Logger.LogError($"🔴 [LoadScenePath] 读取失败: {ex.Message}");
        }
        return "";
    }

    private static void ClearScenePathCache()
    {
        try
        {
            string filePath = GetCacheFilePath();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Z_Logger.Log("🔵 [ClearScenePath] 缓存已清除");
            }
        }
        catch (System.Exception ex)
        {
            Z_Logger.LogError($"🔴 [ClearScenePath] 清除失败: {ex.Message}");
        }
    }

    [MenuItem("Tools/场景相关/切换到第一个场景", priority = 1)]
    public static void RunScene0()
    {
        Z_Logger.Log("🔵 [RunScene0] 开始执行...");

        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "Build Settings中没有场景！请先添加场景到Build Settings。", "确定");
            return;
        }

        bool confirm = EditorUtility.DisplayDialog(
            "切换场景",
            "即将切换到第一个场景，请确保当前场景数据已保存！\n\n当前场景的修改建议先保存。",
            "确认切换",
            "取消"
        );

        if (!confirm)
        {
            Z_Logger.Log("🔵 [RunScene0] 用户取消切换");
            return;
        }

        if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Z_Logger.Log("🔵 [RunScene0] 当前场景已保存");
        }
        else
        {
            Z_Logger.LogWarning("🔵 [RunScene0] 用户取消了保存，继续切换");
        }

        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenes[0].path);

        Z_Logger.Log($"✅ 已切换到场景: {Path.GetFileNameWithoutExtension(scenes[0].path)}");
    }

    private static string GetGameScenePath()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        foreach (var scene in scenes)
        {
            string sceneName = Path.GetFileNameWithoutExtension(scene.path);
            if (sceneName.Equals("GameScene", System.StringComparison.OrdinalIgnoreCase))
            {
                Z_Logger.Log($"🔵 [GetGameScenePath] 在Build Settings中找到GameScene: {scene.path}");
                return scene.path;
            }
        }

        string[] guids = AssetDatabase.FindAssets("GameScene t:Scene");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            Z_Logger.Log($"🔵 [GetGameScenePath] 在Assets中找到GameScene: {path}");
            return path;
        }

        Z_Logger.LogWarning("🔴 [GetGameScenePath] 未找到GameScene！");
        return "";
    }

    private static void CheckPlayModeEnd()
    {
        if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode == false)
        {
            EditorApplication.update -= CheckPlayModeEnd;

            string gameScenePath = LoadScenePathFromCache();

            Z_Logger.Log($"🔵 [CheckPlayModeEnd] 从缓存读取到场景路径: '{gameScenePath}'");

            if (!string.IsNullOrEmpty(gameScenePath) && File.Exists(gameScenePath))
            {
                Z_Logger.Log($"📌 正在切换到GameScene: {Path.GetFileNameWithoutExtension(gameScenePath)}");
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(gameScenePath);
                Z_Logger.Log("✅ 已切换到GameScene！");
                ClearScenePathCache();
            }
            else
            {
                Z_Logger.Log("ℹ️ 没有可切换的GameScene，当前停留在目标场景。");
            }
        }
    }

    [MenuItem("Tools/场景相关/切换到GameScene")]
    public static void SwitchToGameScene()
    {
        string gameScenePath = GetGameScenePath();

        if (!string.IsNullOrEmpty(gameScenePath) && File.Exists(gameScenePath))
        {
            bool confirm = EditorUtility.DisplayDialog(
                "切换场景",
                "即将切换到 GameScene，请确保当前场景数据已保存！\n\n当前场景的修改建议先保存。",
                "确认切换",
                "取消"
            );

            if (!confirm)
            {
                Z_Logger.Log("🔵 [SwitchToGameScene] 用户取消切换");
                return;
            }

            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Z_Logger.Log("🔵 [SwitchToGameScene] 当前场景已保存");
            }
            else
            {
                Z_Logger.LogWarning("🔵 [SwitchToGameScene] 用户取消了保存，继续切换");
            }

            Z_Logger.Log($"📌 手动切换到GameScene: {Path.GetFileNameWithoutExtension(gameScenePath)}");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(gameScenePath);
            Z_Logger.Log($"✅ 已切换到GameScene: {Path.GetFileNameWithoutExtension(gameScenePath)}");
        }
        else
        {
            Z_Logger.Log("ℹ️ 未找到GameScene。");
            EditorUtility.DisplayDialog("提示",
                "未找到GameScene！\n\n请确保场景文件名为 'GameScene' 或已添加到Build Settings中。",
                "确定");
        }
    }

    [MenuItem("Tools/场景相关/运行游戏场景")]
    public static void RunGameScene()
    {
        string gameScenePath = GetGameScenePath();

        if (!string.IsNullOrEmpty(gameScenePath) && File.Exists(gameScenePath))
        {
            SaveScenePathToCache(gameScenePath);
            Z_Logger.Log($"📌 保存GameScene路径，准备进入Play Mode");
            EditorApplication.isPlaying = true;
        }
        else
        {
            Z_Logger.Log("ℹ️ 未找到GameScene。");
            EditorUtility.DisplayDialog("提示",
                "未找到GameScene！\n\n请确保场景文件名为 'GameScene' 或已添加到Build Settings中。",
                "确定");
        }
    }

    [MenuItem("Tools/场景相关/退出游戏")]
    public static void ExitGame()
    {
        EditorApplication.isPlaying = false;
        Z_Logger.Log("🔵 [ExitGame] 退出运行模式");
    }
}
#endif
