// ==================== WeatherDataEditor.cs ====================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class WeatherDataEditor : EditorWindow
{
    private List<WeatherData> weathers = new List<WeatherData>();
    private Vector2 scrollPosition;
    private int selectedIndex = -1;
    private int editId = 301;
    private string editName = "";
    private string editDescription = "";
    private int editPercentage = 6;   // 自动计算，不可编辑
    private int editWeight = 60;

    // ✅ 新增：自动计算概率的开关
    private bool autoCalculatePercentage = true;

    [MenuItem("Tools/基础框架/301_天气")]
    public static void ShowWindow()
    {
        WeatherDataEditor window = GetWindow<WeatherDataEditor>("天气数据编辑器");
        window.minSize = new Vector2(500, 600);
        window.Show();
    }

    private void OnEnable() => LoadData();

    private void OnGUI()
    {
        DrawToolbar();
        DrawDataList();
        DrawEditPanel();
        DrawBottomButtons();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            LoadData();
            // ✅ 刷新时自动计算概率
            if (autoCalculatePercentage)
            {
                AutoCalculatePercentages();
            }
        }
        if (GUILayout.Button("新增", EditorStyles.toolbarButton, GUILayout.Width(60))) AddNewItem();
        if (GUILayout.Button("自动计算概率", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            AutoCalculatePercentages();
            EditorUtility.DisplayDialog("完成", "所有天气概率已根据权重重新计算", "确定");
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"共 {weathers.Count} 条数据", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);

        // ✅ 显示总权重信息
        int totalWeight = 0;
        foreach (var w in weathers) totalWeight += w.weight;
        EditorGUILayout.LabelField($"总权重: {totalWeight}  |  自动计算: {(autoCalculatePercentage ? "✅ 开启" : "❌ 关闭")}", EditorStyles.miniLabel);
        GUILayout.Space(5);
    }

    private void DrawDataList()
    {
        EditorGUILayout.LabelField("天气列表", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(250));

        // ✅ 计算总权重用于显示概率
        int totalWeight = 0;
        foreach (var w in weathers) totalWeight += w.weight;

        for (int i = 0; i < weathers.Count; i++)
        {
            DrawListItem(i, totalWeight);
        }
        if (weathers.Count == 0) EditorGUILayout.LabelField("暂无数据，点击\"新增\"添加", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    private void DrawListItem(int index, int totalWeight)
    {
        WeatherData item = weathers[index];
        EditorGUILayout.BeginHorizontal();

        if (selectedIndex == index) GUI.backgroundColor = Color.cyan;
        EditorGUILayout.LabelField($"[{item.id}]", GUILayout.Width(50));
        EditorGUILayout.LabelField(item.name, GUILayout.Width(80));

        // ✅ 显示实际概率（基于权重计算）
        float actualProbability = totalWeight > 0 ? (float)item.weight / totalWeight * 100f : 0f;
        EditorGUILayout.LabelField($"概率:{actualProbability:F2}%", GUILayout.Width(80));
        EditorGUILayout.LabelField($"权重:{item.weight}", GUILayout.Width(70));
        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("编辑", GUILayout.Width(50))) selectedIndex = index;

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("删除", GUILayout.Width(50)) && EditorUtility.DisplayDialog("确认删除", $"确定要删除天气 [{item.id}] {item.name} 吗？", "删除", "取消"))
        {
            weathers.RemoveAt(index);
            if (selectedIndex >= weathers.Count) selectedIndex = -1;
            SaveData();
            LoadData();
            if (autoCalculatePercentage) AutoCalculatePercentages();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        if (index < weathers.Count - 1) EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }

    private void DrawEditPanel()
    {
        EditorGUILayout.LabelField("编辑区域", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (selectedIndex >= 0 && selectedIndex < weathers.Count)
        {
            WeatherData item = weathers[selectedIndex];

            // ✅ 计算总权重
            int totalWeight = 0;
            foreach (var w in weathers) totalWeight += w.weight;
            float actualProbability = totalWeight > 0 ? (float)item.weight / totalWeight * 100f : 0f;

            EditorGUILayout.LabelField($"正在编辑: [{item.id}] {item.name}");
            EditorGUILayout.LabelField($"实际概率: {actualProbability:F2}%  (基于权重计算)", EditorStyles.miniLabel);
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ID:", GUILayout.Width(40));
            int newId = EditorGUILayout.IntField(item.id);
            if (newId != item.id && !IsIdDuplicate(newId, selectedIndex)) item.id = newId;
            else if (newId != item.id) EditorUtility.DisplayDialog("错误", $"ID {newId} 已存在", "确定");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("名称:", GUILayout.Width(40));
            item.name = EditorGUILayout.TextField(item.name);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("描述:", GUILayout.Width(40));
            item.description = EditorGUILayout.TextField(item.description);
            EditorGUILayout.EndHorizontal();

            // ✅ 概率字段设置为只读（不可编辑）
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("大概为(概率%):", GUILayout.Width(80));
            EditorGUI.BeginDisabledGroup(true);  // ✅ 禁用编辑
            float displayPercentage = totalWeight > 0 ? (float)item.weight / totalWeight * 100f : 0f;
            EditorGUILayout.FloatField(displayPercentage, GUILayout.Width(80));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.LabelField($"  (自动计算，基于权重 {item.weight}/{totalWeight})", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // ✅ 权重字段可编辑
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("权重:", GUILayout.Width(40));
            int newWeight = EditorGUILayout.IntField(item.weight);
            if (newWeight != item.weight && newWeight > 0)
            {
                item.weight = newWeight;
                // ✅ 权重改变后自动重新计算所有概率
                if (autoCalculatePercentage)
                {
                    AutoCalculatePercentages();
                }
            }
            if (newWeight <= 0)
            {
                EditorGUILayout.LabelField("  ⚠️ 权重必须大于0", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            // ✅ 显示权重排序建议
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("排序:", GUILayout.Width(40));
            var sorted = new List<WeatherData>(weathers);
            sorted.Sort((a, b) => b.weight.CompareTo(a.weight));
            int rank = sorted.FindIndex(w => w.id == item.id) + 1;
            EditorGUILayout.LabelField($"权重排名: #{rank}/{weathers.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("保存修改", GUILayout.Width(100)))
            {
                SaveData();
                LoadData();
                if (autoCalculatePercentage) AutoCalculatePercentages();
                EditorUtility.DisplayDialog("成功", "数据已保存", "确定");
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.LabelField("请从左侧列表选择要编辑的项", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    private void DrawBottomButtons()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("快速新增", EditorStyles.boldLabel);

        // ✅ 显示当前总权重
        int totalWeight = 0;
        foreach (var w in weathers) totalWeight += w.weight;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ID:", GUILayout.Width(30));
        editId = EditorGUILayout.IntField(editId, GUILayout.Width(60));
        EditorGUILayout.LabelField("名称:", GUILayout.Width(30));
        editName = EditorGUILayout.TextField(editName, GUILayout.Width(100));
        EditorGUILayout.LabelField("权重:", GUILayout.Width(30));
        editWeight = EditorGUILayout.IntField(editWeight, GUILayout.Width(60));
        EditorGUILayout.LabelField("", GUILayout.Width(20));
        // ✅ 显示新增后的概率预览
        if (editWeight > 0 && totalWeight > 0)
        {
            float previewProb = (float)editWeight / (totalWeight + editWeight) * 100f;
            EditorGUILayout.LabelField($"新增后概率约: {previewProb:F1}%", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("描述:", GUILayout.Width(30));
        editDescription = EditorGUILayout.TextField(editDescription, GUILayout.Width(300));
        EditorGUILayout.EndHorizontal();

        // ✅ 新增时自动设置概率的选项
        EditorGUILayout.BeginHorizontal();
        autoCalculatePercentage = EditorGUILayout.Toggle("自动计算所有概率", autoCalculatePercentage);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("新增", GUILayout.Width(100))) AddQuickItem();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("提示：概率由权重自动计算，修改权重后会自动更新所有概率", MessageType.Info);
    }

    // ==================== 核心方法 ====================

    /// <summary>
    /// ✅ 自动计算所有天气的概率（基于权重）
    /// </summary>
    private void AutoCalculatePercentages()
    {
        if (weathers == null || weathers.Count == 0) return;

        int totalWeight = 0;
        foreach (var w in weathers)
        {
            totalWeight += w.weight;
        }

        if (totalWeight <= 0)
        {
            Z_Logger.LogWarning("[WeatherDataEditor] 总权重为0，无法计算概率");
            return;
        }

        foreach (var weather in weathers)
        {
            // 概率 = (该天气权重 / 总权重) * 100
            float calculatedPercentage = (float)weather.weight / totalWeight * 100f;
            weather.percentage = Mathf.RoundToInt(calculatedPercentage);  // 四舍五入取整
        }

        // 自动保存
        SaveData();
        LoadData();

        Z_Logger.Log($"[WeatherDataEditor] 已自动计算 {weathers.Count} 个天气的概率，总权重={totalWeight}");

        // 验证概率总和
        int sum = 0;
        foreach (var w in weathers) sum += w.percentage;
        if (sum != 100)
        {
            Z_Logger.LogWarning($"[WeatherDataEditor] 概率总和为 {sum}%，可能因四舍五入导致不精确");
        }
    }

    private void LoadData()
    {
        string fullPath = Path.Combine(Application.dataPath, "Addressables/JsonData/BaseFramework/weathers.json");
        if (File.Exists(fullPath))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<WeatherListWrapper>(File.ReadAllText(fullPath));
                weathers = wrapper?.weathers ?? new List<WeatherData>();
                if (weathers.Count > 0) Z_Logger.Log($"加载成功，共{weathers.Count}条数据");
            }
            catch (System.Exception e) { Z_Logger.LogError($"加载失败: {e.Message}"); weathers = new List<WeatherData>(); }
        }
        else
        {
            Z_Logger.LogWarning($"文件不存在: {fullPath}，创建空列表");
            weathers = new List<WeatherData>();
        }
        Repaint();
    }

    private void SaveData()
    {
        string fullPath = Path.Combine(Application.dataPath, "Addressables/JsonData/BaseFramework/weathers.json");
        string directory = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, JsonUtility.ToJson(new WeatherListWrapper { weathers = weathers }, true));
        AssetDatabase.Refresh();
        Z_Logger.Log("保存成功");
    }

    private void AddNewItem()
    {
        int newId = 301;
        if (weathers.Count > 0)
        {
            int maxId = 0;
            foreach (var item in weathers) if (item.id > maxId) maxId = item.id;
            newId = maxId + 1;
        }
        weathers.Add(new WeatherData { id = newId, name = "新天气", description = "描述", percentage = 6, weight = 60 });
        selectedIndex = weathers.Count - 1;
        SaveData();
        LoadData();
        if (autoCalculatePercentage) AutoCalculatePercentages();
    }

    private void AddQuickItem()
    {
        if (string.IsNullOrEmpty(editName))
        {
            EditorUtility.DisplayDialog("错误", "名称不能为空", "确定");
            return;
        }
        if (IsIdDuplicate(editId, -1))
        {
            EditorUtility.DisplayDialog("错误", $"ID {editId} 已存在", "确定");
            return;
        }
        if (editWeight <= 0)
        {
            EditorUtility.DisplayDialog("错误", "权重必须大于0", "确定");
            return;
        }

        weathers.Add(new WeatherData
        {
            id = editId,
            name = editName,
            description = editDescription,
            percentage = 0,  // 将由自动计算填充
            weight = editWeight
        });

        // 排序
        weathers.Sort((a, b) => a.id.CompareTo(b.id));

        SaveData();
        LoadData();

        // ✅ 新增后自动计算概率
        if (autoCalculatePercentage)
        {
            AutoCalculatePercentages();
        }

        // 重置输入
        int nextId = 301;
        if (weathers.Count > 0)
        {
            int maxId = 0;
            foreach (var item in weathers) if (item.id > maxId) maxId = item.id;
            nextId = maxId + 1;
        }
        editId = nextId;
        editName = "";
        editDescription = "";
        editWeight = 60;

        EditorUtility.DisplayDialog("成功", "新增成功", "确定");
    }

    private bool IsIdDuplicate(int id, int excludeIndex)
    {
        for (int i = 0; i < weathers.Count; i++)
        {
            if (i != excludeIndex && weathers[i].id == id) return true;
        }
        return false;
    }

    [System.Serializable]
    public class WeatherData
    {
        public int id;
        public string name;
        public string description;
        public int percentage;   // ✅ 由权重自动计算，不可手动编辑
        public int weight;
    }

    [System.Serializable]
    public class WeatherListWrapper
    {
        public List<WeatherData> weathers;
    }
}
#endif
