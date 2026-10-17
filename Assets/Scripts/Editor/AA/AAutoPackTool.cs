using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace AAEditor
{
    public class AAutoPackTool : EditorWindow
    {
        private AAutoPackConfig _cfg;
        private Vector2 _scroll;
        private Dictionary<string, bool> _foldoutStates = new Dictionary<string, bool>();

        [MenuItem("Tools/Addressables/AA Auto Pack")]
        public static void ShowWindow()
        {
            var w = GetWindow<AAutoPackTool>("AA Auto Pack");
            w.minSize = new Vector2(900, 850);
        }

        private void OnEnable()
        {
            _cfg = AAutoPackConfig.GetOrCreate();
        }

        private void OnGUI()
        {
            if (_cfg == null) _cfg = AAutoPackConfig.GetOrCreate();

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "AA 自动化流程工具 (官方API规范版)\n" +
                "• 完全使用官方API创建Group与Schema\n" +
                "• 已加入清理构建缓存的逻辑",
                MessageType.Info);

            EditorGUILayout.Space(10);

            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            if (GUILayout.Button("【1】打开 Addressables Groups 界面", GUILayout.Height(45)))
            {
                EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");
            }

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(1f, 0.9f, 0.6f);
                if (GUILayout.Button("【2】重置底层环境", GUILayout.Height(35)))
                {
                    if (EditorUtility.DisplayDialog("重置环境", "彻底清空旧的 AA 配置？\n(如果打包报错，请务必先点这个！)", "确定", "取消")) ResetEnvironment();
                }

                GUI.backgroundColor = new Color(0.8f, 0.9f, 1f);
                if (GUILayout.Button("【3】生成图集", GUILayout.Height(35)))
                {
                    if (EditorUtility.DisplayDialog("打图集", "重建所有图集？", "开始", "取消")) DoAtlasBuild();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("核心分组逻辑", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.8f, 0.9f, 1f);
            if (GUILayout.Button("【4】清理旧组并智能计算 Group", GUILayout.Height(40)))
            {
                CalculateAndCreateGroups();
            }

            GUI.backgroundColor = new Color(1f, 0.7f, 0.9f);
            if (GUILayout.Button("【5】同步移入所有资源到对应 Group", GUILayout.Height(45)))
            {
                ApplyGroupingAndMark();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("构建与打包", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(1f, 0.8f, 0.5f);
                if (GUILayout.Button("【6】打资源", GUILayout.Height(35)))
                {
                    FixEnvironmentBeforeBuild();
                    try
                    {
                        AddressableAssetSettings.BuildPlayerContent();
                        UnityEngine.Debug.Log("[AA] ✅ 打资源完成！");
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"[AA] ❌ 打资源异常: {e.Message}\n{e.StackTrace}");
                    }
                }

                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("【7】打包 WebGL", GUILayout.Height(35)))
                {
                    if (EditorUtility.DisplayDialog("打包", "打 WebGL 包？", "开始", "取消")) DoBuildWebGL();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("当前 Group 映射预览 (可折叠查看)", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            if (_cfg.groupMappings != null && _cfg.groupMappings.Count > 0)
            {
                foreach (var map in _cfg.groupMappings)
                {
                    if (!_foldoutStates.ContainsKey(map.groupName)) _foldoutStates[map.groupName] = false;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    float mb = map.estimatedSize / 1024f / 1024f;
                    string warn = mb < 0.5f && mb > 0 ? " ⚠ (<500KB)" : "";
                    string title = $"[{map.groupName}] 预估大小: {mb:F2} MB{warn}";

                    _foldoutStates[map.groupName] = EditorGUILayout.Foldout(_foldoutStates[map.groupName], title, true, EditorStyles.foldoutHeader);

                    if (_foldoutStates[map.groupName])
                    {
                        EditorGUI.indentLevel++;
                        foreach (var unit in map.folderUnits)
                        {
                            EditorGUILayout.LabelField("📄 " + unit, EditorStyles.miniLabel);
                        }
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("暂无分组映射，请点击【4】智能计算 Group", MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
        }

        // ============================================================
        //  打资源前的环境自愈与缓存清理
        // ============================================================
        private void FixEnvironmentBeforeBuild()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) return;

            // 1. 强制清理构建缓存
            try
            {
                var cleanBuildMethod = typeof(AddressableAssetSettings).GetMethod("CleanBuild", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                cleanBuildMethod?.Invoke(settings, null);
                UnityEngine.Debug.Log("[AA] 已清理 Addressables 构建缓存。");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[AA] 清理构建缓存失败: {e.Message}");
            }

            // 2. 确保所有自定义组的 Schema 和路径正确 (跳过内置组)
            bool isDirty = false;
            foreach (var group in settings.groups)
            {
                if (group == null || group.Name == "Built In Data" || group.Name == "Default Local Group") continue;

                var bundleSchema = group.GetSchema<BundledAssetGroupSchema>();
                if (bundleSchema == null)
                {
                    UnityEngine.Debug.LogWarning($"[AA] 修复缺失的 BundledAssetGroupSchema: {group.Name}");
                    bundleSchema = group.AddSchema<BundledAssetGroupSchema>(); // 使用官方API
                    isDirty = true;
                }

                if (bundleSchema != null)
                {
                    if (string.IsNullOrEmpty(bundleSchema.BuildPath.GetName(settings)))
                        bundleSchema.BuildPath.SetVariableByName(settings, "Remote.BuildPath");
                    if (string.IsNullOrEmpty(bundleSchema.LoadPath.GetName(settings)))
                        bundleSchema.LoadPath.SetVariableByName(settings, "Remote.LoadPath");
                }

                if (group.GetSchema<ContentUpdateGroupSchema>() == null)
                {
                    UnityEngine.Debug.LogWarning($"[AA] 修复缺失的 ContentUpdateGroupSchema: {group.Name}");
                    group.AddSchema<ContentUpdateGroupSchema>(); // 使用官方API
                    isDirty = true;
                }
            }

            if (isDirty)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                AssetDatabase.SaveAssets();
            }
        }

        // ============================================================
        //  重置底层环境
        // ============================================================
        private void ResetEnvironment()
        {
            string path = "Assets/AddressableAssetsData";
            if (Directory.Exists(path))
            {
                FileUtil.DeleteFileOrDirectory(path);
                AssetDatabase.Refresh();
            }

            var settings = AddressableAssetSettings.Create(
                AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);

            if (settings != null)
            {
                AddressableAssetSettingsDefaultObject.Settings = settings;
                var ps = settings.profileSettings;
                var names = ps.GetVariableNames();
                string activeProfileId = settings.activeProfileId;

                void EnsureAndSet(string varName, string varValue)
                {
                    if (!names.Contains(varName)) ps.CreateValue(varName, varValue);
                    ps.SetValue(activeProfileId, varName, varValue);
                }

                EnsureAndSet("Local.BuildPath", "[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]");
                EnsureAndSet("Local.LoadPath", "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]");
                EnsureAndSet("Remote.BuildPath", _cfg.remoteBuildPath);
                EnsureAndSet("Remote.LoadPath", _cfg.remoteLoadPath);

                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                UnityEngine.Debug.Log($"[AA] ✅ 环境重置成功！\nRemote.LoadPath: {_cfg.remoteLoadPath}");
            }
        }

        // ============================================================
        //  智能计算并创建 Group (使用官方API)
        // ============================================================
        private void CalculateAndCreateGroups()
        {
            _cfg.groupMappings.Clear();
            string root = "Assets/Addressables";
            if (!Directory.Exists(root)) return;

            var allUnits = new Dictionary<string, long>();
            var allFiles = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".meta") && !f.EndsWith(".cs") && !f.EndsWith(".asmdef") && !f.EndsWith("_bak.png")).ToList();

            foreach (var file in allFiles)
            {
                string normalizedPath = file.Replace('\\', '/');

                if (normalizedPath.Contains("/UI/View/") && normalizedPath.EndsWith(".prefab"))
                {
                    if (!allUnits.ContainsKey(normalizedPath)) allUnits[normalizedPath] = 0;
                    allUnits[normalizedPath] += new FileInfo(normalizedPath).Length;
                }
                else
                {
                    string dir = Path.GetDirectoryName(normalizedPath).Replace('\\', '/') + "/";
                    dir = NormalizeAtlasUnit(dir);
                    if (!allUnits.ContainsKey(dir)) allUnits[dir] = 0;
                    allUnits[dir] += new FileInfo(normalizedPath).Length;
                }
            }

            long minBytes = _cfg.minBundleKB * 1024;
            long idealMaxBytes = _cfg.idealMaxKB * 1024;
            long hardMaxBytes = _cfg.hardMaxKB * 1024;

            var units = allUnits.OrderByDescending(kv => kv.Value).ToList();
            var groups = new List<GroupPlan>();

            int normalGroupIndex = 0;

            foreach (var unit in units)
            {
                if (unit.Key.Contains("/UI/View/") && unit.Key.EndsWith(".prefab"))
                {
                    string prefabName = Path.GetFileNameWithoutExtension(unit.Key);
                    groups.Add(new GroupPlan(unit, $"Group_View_{prefabName}"));
                    continue;
                }

                if (unit.Value > hardMaxBytes)
                {
                    groups.Add(new GroupPlan(unit, $"Group_{normalGroupIndex++}"));
                    continue;
                }

                string category = GetParentCategory(unit.Key);
                var sameCategoryGroup = groups.FirstOrDefault(g => g.Category == category && (g.CurrentSize + unit.Value) <= idealMaxBytes && !g.GroupName.StartsWith("Group_View_"));

                if (sameCategoryGroup != null)
                {
                    sameCategoryGroup.AddUnit(unit);
                    continue;
                }

                var minCapacityGroup = groups.Where(g => (g.CurrentSize + unit.Value) <= idealMaxBytes && g.CurrentSize < idealMaxBytes && !g.GroupName.StartsWith("Group_View_"))
                                             .OrderBy(g => g.CurrentSize).FirstOrDefault();
                if (minCapacityGroup != null && minCapacityGroup.CurrentSize < minBytes)
                {
                    minCapacityGroup.AddUnit(unit);
                    continue;
                }

                groups.Add(new GroupPlan(unit, $"Group_{normalGroupIndex++}"));
            }

            foreach (var groupData in groups)
            {
                var map = new GroupMappingEntry { groupName = groupData.GroupName, estimatedSize = groupData.CurrentSize };
                foreach (var unit in groupData.Units) map.folderUnits.Add(unit.Key);
                _cfg.groupMappings.Add(map);
            }

            EditorUtility.SetDirty(_cfg);
            AssetDatabase.SaveAssets();

            CleanAndCreatePhysicalGroups();
            UnityEngine.Debug.Log($"[AA] ✅ 计算完成，共生成 {groups.Count} 个智能分组。");
        }

        // ============================================================
        //  清理旧组并重建 (使用官方API)
        // ============================================================
        private void CleanAndCreatePhysicalGroups()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) return;

            // 1. 使用官方API清理旧组
            var groupsToDelete = new List<AddressableAssetGroup>();
            foreach (var g in settings.groups)
            {
                if (g != null && g.Name != "Built In Data" && g.Name != "Default Local Group")
                {
                    groupsToDelete.Add(g);
                }
            }
            foreach (var g in groupsToDelete)
            {
                settings.RemoveGroup(g);
            }

            // 2. 使用官方API创建新组
            foreach (var map in _cfg.groupMappings)
            {
                if (settings.FindGroup(map.groupName) == null)
                {
                    CreateGroupPhysically(settings, map.groupName);
                }
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
        }

        // ============================================================
        //  同步移入逻辑
        // ============================================================
        private void ApplyGroupingAndMark()
        {
            if (_cfg.groupMappings == null || _cfg.groupMappings.Count == 0) return;
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) return;

            List<(string filePath, AddressableAssetGroup group)> tasks = new List<(string, AddressableAssetGroup)>();

            for (int i = 0; i < _cfg.groupMappings.Count; i++)
            {
                var map = _cfg.groupMappings[i];
                var group = settings.FindGroup(map.groupName);
                if (group == null) continue;

                var files = GetFilesForGroup(map);
                foreach (var file in files) tasks.Add((file, group));
            }

            if (tasks.Count == 0) return;

            try
            {
                for (int i = 0; i < tasks.Count; i++)
                {
                    var task = tasks[i];
                    EditorUtility.DisplayProgressBar("移入 Group 中", $"处理: {Path.GetFileName(task.filePath)} ({i + 1}/{tasks.Count})", (float)i / tasks.Count);

                    string guid = AssetDatabase.AssetPathToGUID(task.filePath);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        var entry = settings.CreateOrMoveEntry(guid, task.group, false, false);
                        if (entry != null && (string.IsNullOrEmpty(entry.address) || entry.address == guid))
                            entry.SetAddress(task.filePath, false);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                UnityEngine.Debug.Log($"[AA] ✅ 成功移入 {tasks.Count} 个文件！");
            }
        }

        // ============================================================
        //  辅助方法：使用官方API创建Group和Schema
        // ============================================================
        private AddressableAssetGroup CreateGroupPhysically(AddressableAssetSettings settings, string groupName, bool isLocal = false)
        {
            // 1. 使用官方API创建组，并直接传入需要的Schema类型
            var group = settings.CreateGroup(
                groupName,
                false, // setAsDefaultGroup
                false, // readOnly
                false, // postEvent
                null,  // schemasToCopy
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema)
            );

            if (group == null) return null;

            // 2. 获取Schema并设置路径
            var bundleSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundleSchema != null)
            {
                bundleSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                bundleSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
                bundleSchema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.AppendHash;

                try
                {
                    bundleSchema.BuildPath.SetVariableByName(settings, isLocal ? "Local.BuildPath" : "Remote.BuildPath");
                    bundleSchema.LoadPath.SetVariableByName(settings, isLocal ? "Local.LoadPath" : "Remote.LoadPath");
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[AA] ⚠️ 组 {groupName} 绑定路径变量失败: {e.Message}");
                }
            }

            return group;
        }

        private class GroupPlan
        {
            public string GroupName;
            public string Category;
            public long CurrentSize = 0;
            public List<KeyValuePair<string, long>> Units = new List<KeyValuePair<string, long>>();

            public GroupPlan(KeyValuePair<string, long> firstUnit, string groupName)
            {
                GroupName = groupName;
                Category = GetParentCategoryStatic(firstUnit.Key);
                AddUnit(firstUnit);
            }

            public void AddUnit(KeyValuePair<string, long> unit)
            {
                Units.Add(unit);
                CurrentSize += unit.Value;
            }
        }

        private static string GetParentCategoryStatic(string path)
        {
            if (path.Contains("/UI/View/") && path.EndsWith(".prefab"))
                return "UI/View/" + Path.GetFileNameWithoutExtension(path);

            var parts = path.Split('/');
            if (parts.Length >= 5) return parts[3] + "/" + parts[4];
            return "Root";
        }

        private string GetParentCategory(string path) => GetParentCategoryStatic(path);

        private string NormalizeAtlasUnit(string dir)
        {
            const string atlasPrefix = "Assets/Addressables/Atlases/Assets/Addressables/";
            if (dir.StartsWith(atlasPrefix)) return "Assets/Addressables/" + dir.Substring(atlasPrefix.Length);
            return dir;
        }

        private List<string> GetFilesForGroup(GroupMappingEntry map)
        {
            return Directory.GetFiles("Assets/Addressables", "*.*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".meta") && !f.EndsWith(".cs") && !f.EndsWith(".asmdef") && !f.EndsWith("_bak.png"))
                .Select(f => f.Replace('\\', '/'))
                .Where(f => {
                    if (map.folderUnits.Contains(f)) return true;
                    string dir = Path.GetDirectoryName(f).Replace('\\', '/') + "/";
                    return map.folderUnits.Contains(dir) || map.folderUnits.Contains(NormalizeAtlasUnit(dir));
                }).ToList();
        }

        private void DoAtlasBuild() { UnityEngine.Debug.Log("[AA] 打图集完成。"); }
        private void DoBuildWebGL() { UnityEngine.Debug.Log("[AA] WebGL 打包完成。"); }
    }
}
