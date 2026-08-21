#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// Addressables 远程地址管理工具
/// </summary>
public class AAUrlTool : EditorWindow
{
    private string status = "";
    private string currentRemotePath = "";

    private AddressableAssetSettings settings;
    private List<string> profileIds = new List<string>();
    private List<string> profileNames = new List<string>();
    private string selectedProfileId = "";
    private int selectedEnvIndex = 0;

    private readonly string[] envNames = { "Dev", "Test", "Prod" };
    private readonly string[] envColors = { "#4CAF88", "#FF9800", "#F44336" };

    private const string BUCKET_ID = "ecd936fa-2414-4570-bcf4-1ae5caa7e824";
    private const string BASE_URL = "https://a.unity.cn/client_api/v1/buckets/";

    // 使用你项目中实际存在的变量名（带点号）
    private const string REMOTE_LOAD_PATH_KEY = "Remote.LoadPath";

    // 是否正在等待打开界面
    private bool pendingOpenProfile = false;

    [MenuItem("Tools/资源工具/2.更改AA资源地址", false)]
    public static void ShowWindow()
    {
        var window = GetWindow<AAUrlTool>("AAUrlTool");
        window.minSize = new Vector2(600, 280);
        window.Show();
    }

    private void OnEnable()
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            status = "❌ 未找到 Addressables 设置";
            return;
        }

        profileIds.Clear();
        profileNames.Clear();

        var profileSettings = settings.profileSettings;
        var profileEntries = profileSettings.GetType().GetField("m_Profiles",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (profileEntries != null)
        {
            var profiles = profileEntries.GetValue(profileSettings) as System.Collections.IList;
            if (profiles != null)
            {
                foreach (var p in profiles)
                {
                    var idField = p.GetType().GetField("m_Id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var nameField = p.GetType().GetField("m_ProfileName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (idField != null && nameField != null)
                    {
                        string id = idField.GetValue(p)?.ToString();
                        string name = nameField.GetValue(p)?.ToString();
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name))
                        {
                            profileIds.Add(id);
                            profileNames.Add(name);
                        }
                    }
                }
            }
        }

        if (profileIds.Count > 0)
        {
            selectedProfileId = profileIds[0];
            GetCurrentRemotePath();
            status = "✅ 加载成功";
            Z_Logger.Log($"📋 [AAUrlTool] 加载完成，共 {profileIds.Count} 个 Profile");
        }
        else
        {
            status = "⚠️ 没有找到任何 Profile";
        }
    }

    private void GetCurrentRemotePath()
    {
        try
        {
            string value = settings.profileSettings.GetValueByName(selectedProfileId, REMOTE_LOAD_PATH_KEY);

            Z_Logger.Log($"📖 [AAUrlTool] 读取 {REMOTE_LOAD_PATH_KEY}: {(string.IsNullOrEmpty(value) ? "null" : value)}");

            if (!string.IsNullOrEmpty(value))
            {
                currentRemotePath = value;
                if (value.Contains("/Dev/") || value.Contains("/dev/"))
                    selectedEnvIndex = 0;
                else if (value.Contains("/Test/") || value.Contains("/test/"))
                    selectedEnvIndex = 1;
                else if (value.Contains("/Prod/") || value.Contains("/prod/") || value.Contains("/release/"))
                    selectedEnvIndex = 2;
                else
                    selectedEnvIndex = 0;
            }
            else
            {
                currentRemotePath = $"{BASE_URL}{BUCKET_ID}/release_by_badge/Dev/content/";
                selectedEnvIndex = 0;
            }
        }
        catch (Exception e)
        {
            currentRemotePath = $"{BASE_URL}{BUCKET_ID}/release_by_badge/Dev/content/";
            selectedEnvIndex = 0;
            Z_Logger.LogError($"❌ [AAUrlTool] 读取异常: {e.Message}");
        }
    }

    private void OnGUI()
    {
        if (settings == null)
        {
            EditorGUILayout.HelpBox("请先在 Addressables Groups 窗口中初始化 Addressables", MessageType.Error);
            if (GUILayout.Button("重新加载", GUILayout.Height(30)))
            {
                LoadSettings();
            }
            return;
        }

        DrawHeader();
        DrawSeparator();
        DrawProfileSelector();
        DrawSeparator();
        DrawEnvironmentSelector();
        DrawSeparator();
        DrawCurrentUrl();
        DrawSeparator();
        DrawStatus();
        DrawSeparator();
        DrawActionButtons();

        // 如果标记了待打开Profile界面，在下一帧打开
        if (pendingOpenProfile)
        {
            pendingOpenProfile = false;
            OpenProfilesWindow();
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("☁️ Addressables 远程地址管理", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("🔄 刷新", GUILayout.Width(60)))
        {
            LoadSettings();
        }
        EditorGUILayout.EndHorizontal();

        string profileName = profileNames.Count > 0 ? profileNames[0] : "未加载";
        EditorGUILayout.LabelField($"当前配置文件: {profileName}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"变量名: {REMOTE_LOAD_PATH_KEY}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Bucket ID: {BUCKET_ID}", EditorStyles.miniLabel);
    }

    private void DrawSeparator()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        rect.height = 1;
        EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
        EditorGUILayout.Space(4);
    }

    private void DrawProfileSelector()
    {
        EditorGUILayout.LabelField("📋 Profile 选择", EditorStyles.boldLabel);

        if (profileIds.Count == 0)
        {
            EditorGUILayout.HelpBox("没有找到任何 Profile", MessageType.Warning);
            return;
        }

        int currentIndex = profileIds.IndexOf(selectedProfileId);
        if (currentIndex < 0) currentIndex = 0;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("当前 Profile:", GUILayout.Width(100));
        int newIndex = EditorGUILayout.Popup(currentIndex, profileNames.ToArray(), GUILayout.Width(200));
        if (newIndex != currentIndex && newIndex >= 0 && newIndex < profileIds.Count)
        {
            selectedProfileId = profileIds[newIndex];
            GetCurrentRemotePath();
        }
        EditorGUILayout.EndHorizontal();

        string currentProfileName = currentIndex >= 0 && currentIndex < profileNames.Count ? profileNames[currentIndex] : "未知";
        EditorGUILayout.LabelField($"当前使用的 Profile: {currentProfileName}", EditorStyles.miniLabel);
    }

    private void DrawEnvironmentSelector()
    {
        EditorGUILayout.LabelField("🌐 选择环境", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < envNames.Length; i++)
        {
            bool isSelected = (selectedEnvIndex == i);
            GUI.color = isSelected ? GetColor(envColors[i]) : Color.white;
            if (GUILayout.Button(envNames[i], GUILayout.Height(35)))
            {
                selectedEnvIndex = i;
                string newUrl = $"{BASE_URL}{BUCKET_ID}/release_by_badge/{envNames[i]}/content/";
                currentRemotePath = newUrl;
                status = $"📋 已选择 {envNames[i]} 环境，点击「应用更改」生效";
            }
            GUI.color = Color.white;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Label($"当前选中: {envNames[selectedEnvIndex]}", EditorStyles.boldLabel);
    }

    private Color GetColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        return color;
    }

    private void DrawCurrentUrl()
    {
        EditorGUILayout.LabelField("📝 Remote.LoadPath", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.color = new Color(0.8f, 0.9f, 1f);
        EditorGUILayout.TextField(currentRemotePath, GUILayout.ExpandWidth(true));
        GUI.color = Color.white;

        if (GUILayout.Button("📋", GUILayout.Width(30)))
        {
            GUIUtility.systemCopyBuffer = currentRemotePath;
            status = "✅ 已复制到剪贴板";
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawStatus()
    {
        if (!string.IsNullOrEmpty(status))
        {
            GUI.color = status.StartsWith("✅") ? Color.green :
                        status.StartsWith("⚠️") ? Color.yellow :
                        status.StartsWith("❌") ? Color.red : Color.white;
            EditorGUILayout.LabelField(status, EditorStyles.boldLabel);
            GUI.color = Color.white;
        }
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.LabelField("🔧 操作", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button($"✅ 应用更改 ({envNames[selectedEnvIndex]})", GUILayout.Height(32)))
        {
            ApplyRemoteUrl();
        }
        if (GUILayout.Button("📂 Profiles", GUILayout.Height(32)))
        {
            OpenProfilesWindow();
        }
        if (GUILayout.Button("📦 Groups", GUILayout.Height(32)))
        {
            OpenGroupsWindow();
        }
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 打开 Addressables Profiles 窗口
    /// </summary>
    private void OpenProfilesWindow()
    {
        try
        {
            bool success = EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Profiles");

            if (success)
            {
                status = "✅ 已打开 Profiles 窗口";
                Z_Logger.Log("📂 [AAUrlTool] 已打开 Addressables Profiles 窗口");
            }
            else
            {
                var profilesWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AddressableAssets.GUI.AddressableAssetProfileWindow");
                if (profilesWindowType != null)
                {
                    var window = EditorWindow.GetWindow(profilesWindowType);
                    if (window != null)
                    {
                        window.Show();
                        status = "✅ 已打开 Profiles 窗口";
                        Z_Logger.Log("📂 [AAUrlTool] 已打开 Addressables Profiles 窗口");
                        return;
                    }
                }

                status = "⚠️ 无法打开，请手动打开: Window → Asset Management → Addressables → Profiles";
                Z_Logger.LogWarning("⚠️ [AAUrlTool] 无法打开 Profiles 窗口");
            }
        }
        catch (Exception e)
        {
            status = $"⚠️ 打开失败: {e.Message}";
            Z_Logger.LogError($"⚠️ [AAUrlTool] 打开Profile界面失败: {e.Message}");
        }
    }

    /// <summary>
    /// 打开 Addressables Groups 窗口
    /// </summary>
    private void OpenGroupsWindow()
    {
        try
        {
            bool success = EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");

            if (success)
            {
                status = "✅ 已打开 Groups 窗口";
                Z_Logger.Log("📂 [AAUrlTool] 已打开 Addressables Groups 窗口");
            }
            else
            {
                var groupsWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.AddressableAssets.GUI.AddressableAssetsWindow");
                if (groupsWindowType != null)
                {
                    var window = EditorWindow.GetWindow(groupsWindowType);
                    if (window != null)
                    {
                        window.Show();
                        status = "✅ 已打开 Groups 窗口";
                        Z_Logger.Log("📂 [AAUrlTool] 已打开 Addressables Groups 窗口");
                        return;
                    }
                }

                status = "⚠️ 无法打开，请手动打开: Window → Asset Management → Addressables → Groups";
                Z_Logger.LogWarning("⚠️ [AAUrlTool] 无法打开 Groups 窗口");
            }
        }
        catch (Exception e)
        {
            status = $"⚠️ 打开失败: {e.Message}";
            Z_Logger.LogError($"⚠️ [AAUrlTool] 打开Groups界面失败: {e.Message}");
        }
    }

    private void ApplyRemoteUrl()
    {
        try
        {
            string newUrl = $"{BASE_URL}{BUCKET_ID}/release_by_badge/{envNames[selectedEnvIndex]}/content/";
            currentRemotePath = newUrl;

            Z_Logger.Log($"🔄 [AAUrlTool] 开始切换: {envNames[selectedEnvIndex]}");
            Z_Logger.Log($"📝 [AAUrlTool] 目标地址: {newUrl}");
            Z_Logger.Log($"📝 [AAUrlTool] 变量名: {REMOTE_LOAD_PATH_KEY}");

            // 直接读取当前值
            string currentValue = settings.profileSettings.GetValueByName(selectedProfileId, REMOTE_LOAD_PATH_KEY);
            Z_Logger.Log($"📖 [AAUrlTool] 当前值: {(string.IsNullOrEmpty(currentValue) ? "null" : currentValue)}");

            // 直接用 SetValue 设置
            settings.profileSettings.SetValue(selectedProfileId, REMOTE_LOAD_PATH_KEY, newUrl);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            // 验证
            string verifyValue = settings.profileSettings.GetValueByName(selectedProfileId, REMOTE_LOAD_PATH_KEY);
            Z_Logger.Log($"🔍 [AAUrlTool] 验证读取: {(string.IsNullOrEmpty(verifyValue) ? "null" : verifyValue)}");

            if (verifyValue == newUrl)
            {
                currentRemotePath = verifyValue;
                status = $"✅ 成功切换到 {envNames[selectedEnvIndex]} 环境";
                Z_Logger.Log($"✅ [AAUrlTool] 成功切换到 {envNames[selectedEnvIndex]} 环境");

                // 标记需要打开 Profiles 界面
                pendingOpenProfile = true;
                Z_Logger.Log("📂 [AAUrlTool] 正在打开 Profiles 窗口...");
            }
            else
            {
                status = $"⚠️ 切换可能失败，当前值: {verifyValue}";
                Z_Logger.LogWarning($"⚠️ [AAUrlTool] 切换可能失败，当前值: {verifyValue}");
            }

            Z_Logger.Log($"✅ [AAUrlTool] 切换环境到: {envNames[selectedEnvIndex]}\n地址: {newUrl}");
        }
        catch (Exception e)
        {
            status = $"❌ 切换失败: {e.Message}";
            Z_Logger.LogError($"❌ [AAUrlTool] 切换失败: {e.Message}");
        }
    }
}
#endif
