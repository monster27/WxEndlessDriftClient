// ==================== WeatherDataEditor.cs ====================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class WeatherDataEditor : EditorWindow
{
    private List<WeatherData> weathers = new List<WeatherData>();
    private Vector2 scrollPosition;
    private int selectedIndex = -1;
    private int editId = 301;
    private string editName = "";
    private string editDescription = "";
    private int editPercentage = 6;
    private int editWeight = 60;

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
        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60))) LoadData();
        if (GUILayout.Button("新增", EditorStyles.toolbarButton, GUILayout.Width(60))) AddNewItem();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"共 {weathers.Count} 条数据", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    private void DrawDataList()
    {
        EditorGUILayout.LabelField("天气列表", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(250));

        for (int i = 0; i < weathers.Count; i++) DrawListItem(i);
        if (weathers.Count == 0) EditorGUILayout.LabelField("暂无数据，点击\"新增\"添加", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    private void DrawListItem(int index)
    {
        WeatherData item = weathers[index];
        EditorGUILayout.BeginHorizontal();

        if (selectedIndex == index) GUI.backgroundColor = Color.cyan;
        EditorGUILayout.LabelField($"[{item.id}]", GUILayout.Width(50));
        EditorGUILayout.LabelField(item.name, GUILayout.Width(80));
        EditorGUILayout.LabelField($"概率:{item.percentage}%", GUILayout.Width(70));
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
            EditorGUILayout.LabelField($"正在编辑: [{item.id}] {item.name}");
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

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("概率(%):", GUILayout.Width(50));
            item.percentage = EditorGUILayout.IntField(item.percentage);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("权重:", GUILayout.Width(40));
            item.weight = EditorGUILayout.IntField(item.weight);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("保存修改", GUILayout.Width(100)))
            {
                SaveData();
                LoadData();
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

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ID:", GUILayout.Width(30));
        editId = EditorGUILayout.IntField(editId, GUILayout.Width(60));
        EditorGUILayout.LabelField("名称:", GUILayout.Width(30));
        editName = EditorGUILayout.TextField(editName, GUILayout.Width(100));
        EditorGUILayout.LabelField("概率(%):", GUILayout.Width(50));
        editPercentage = EditorGUILayout.IntField(editPercentage, GUILayout.Width(60));
        EditorGUILayout.LabelField("权重:", GUILayout.Width(30));
        editWeight = EditorGUILayout.IntField(editWeight, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("描述:", GUILayout.Width(30));
        editDescription = EditorGUILayout.TextField(editDescription, GUILayout.Width(300));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("新增", GUILayout.Width(100))) AddQuickItem();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("提示：修改后记得点击\"保存修改\"或使用快速新增区域添加数据", MessageType.Info);
    }

    private void LoadData()
    {
        string fullPath = Path.Combine(Application.dataPath, "Resources/JsonData/BaseFramework/weathers.json");
        if (File.Exists(fullPath))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<WeatherListWrapper>(File.ReadAllText(fullPath));
                weathers = wrapper?.weathers ?? new List<WeatherData>();
                if (weathers.Count > 0) Debug.Log($"加载成功，共{weathers.Count}条数据");
            }
            catch (System.Exception e) { Debug.LogError($"加载失败: {e.Message}"); weathers = new List<WeatherData>(); }
        }
        else
        {
            Debug.LogWarning($"文件不存在: {fullPath}，创建空列表");
            weathers = new List<WeatherData>();
        }
        Repaint();
    }

    private void SaveData()
    {
        string directory = Path.Combine(Application.dataPath, "Resources/JsonData/Base");
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(Application.dataPath, "Resources/JsonData/BaseFramework/weathers.json"), JsonUtility.ToJson(new WeatherListWrapper { weathers = weathers }, true));
        AssetDatabase.Refresh();
        Debug.Log("保存成功");
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
        weathers.Add(new WeatherData { id = editId, name = editName, description = editDescription, percentage = editPercentage, weight = editWeight });
        for (int i = 0; i < weathers.Count - 1; i++)
        {
            for (int j = i + 1; j < weathers.Count; j++)
            {
                if (weathers[i].id > weathers[j].id)
                {
                    WeatherData temp = weathers[i];
                    weathers[i] = weathers[j];
                    weathers[j] = temp;
                }
            }
        }
        SaveData();
        LoadData();
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
        editPercentage = 6;
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
        public int percentage;
        public int weight;
    }

    [System.Serializable]
    public class WeatherListWrapper
    {
        public List<WeatherData> weathers;
    }
}
#endif