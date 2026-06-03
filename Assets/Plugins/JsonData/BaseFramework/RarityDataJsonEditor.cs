#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class RarityDataJsonEditor : EditorWindow
{
    private List<RarityData> rarities = new List<RarityData>();
    private Vector2 scrollPosition;
    private int selectedIndex = -1;
    private int editId = 201;
    private string editName = "";
    private string editColor = "";
    private string editColorCode = "";
    private int editWeight = 100;

    [MenuItem("Tools/基础框架/201_稀有度")]
    public static void ShowWindow()
    {
        RarityDataJsonEditor window = GetWindow<RarityDataJsonEditor>("稀有度数据编辑器");
        window.minSize = new Vector2(500, 600);
        window.Show();
    }

    private void OnEnable()
    {
        LoadData();
    }

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
        EditorGUILayout.LabelField($"共 {rarities.Count} 条数据", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    private void DrawDataList()
    {
        EditorGUILayout.LabelField("稀有度列表", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(250));

        for (int i = 0; i < rarities.Count; i++) DrawListItem(i);
        if (rarities.Count == 0) EditorGUILayout.LabelField("暂无数据，点击\"新增\"添加", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    private void DrawListItem(int index)
    {
        RarityData item = rarities[index];
        EditorGUILayout.BeginHorizontal();

        if (selectedIndex == index) GUI.backgroundColor = Color.cyan;

        // 显示颜色块
        GUI.backgroundColor = GetColorFromCode(item.colorCode);
        EditorGUILayout.LabelField("  ", GUILayout.Width(20), GUILayout.Height(18));
        GUI.backgroundColor = Color.white;

        EditorGUILayout.LabelField($"[{item.id}]", GUILayout.Width(50));
        EditorGUILayout.LabelField(item.name, GUILayout.Width(80));
        EditorGUILayout.LabelField(item.color, GUILayout.Width(60));
        EditorGUILayout.LabelField($"权重:{item.weight}", GUILayout.Width(80));
        EditorGUILayout.LabelField($"经验:{item.exp}", GUILayout.Width(80));
        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("编辑", GUILayout.Width(50))) selectedIndex = index;

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("删除", GUILayout.Width(50)) && EditorUtility.DisplayDialog("确认删除", $"确定要删除稀有度 [{item.id}] {item.name} 吗？", "删除", "取消"))
        {
            rarities.RemoveAt(index);
            if (selectedIndex >= rarities.Count) selectedIndex = -1;
            SaveData();
            LoadData();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        if (index < rarities.Count - 1) EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }

    private void DrawEditPanel()
    {
        EditorGUILayout.LabelField("编辑区域", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (selectedIndex >= 0 && selectedIndex < rarities.Count)
        {
            RarityData item = rarities[selectedIndex];
            EditorGUILayout.LabelField($"正在编辑: [{item.id}] {item.name}");
            GUILayout.Space(5);

            // ID输入
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ID:", GUILayout.Width(40));
            int newId = EditorGUILayout.IntField(item.id);
            if (newId != item.id && !IsIdDuplicate(newId, selectedIndex)) item.id = newId;
            else if (newId != item.id) EditorUtility.DisplayDialog("错误", $"ID {newId} 已存在", "确定");
            EditorGUILayout.EndHorizontal();

            // 名称输入
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("名称:", GUILayout.Width(40));
            item.name = EditorGUILayout.TextField(item.name);
            EditorGUILayout.EndHorizontal();

            // 颜色名称输入
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("颜色名:", GUILayout.Width(40));
            item.color = EditorGUILayout.TextField(item.color);
            EditorGUILayout.EndHorizontal();

            // 颜色代码输入
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("颜色码:", GUILayout.Width(40));
            item.colorCode = EditorGUILayout.TextField(item.colorCode);

            // 颜色预览
            GUI.backgroundColor = GetColorFromCode(item.colorCode);
            EditorGUILayout.LabelField(" 预览  ", GUILayout.Width(50), GUILayout.Height(18));
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // 权重输入
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("权重:", GUILayout.Width(40));
            item.weight = EditorGUILayout.IntField(item.weight);
            EditorGUILayout.EndHorizontal();

            // 经验输入
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("经验:", GUILayout.Width(40));
            item.exp = EditorGUILayout.IntField(item.exp);
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
        editName = EditorGUILayout.TextField(editName, GUILayout.Width(80));
        EditorGUILayout.LabelField("颜色名:", GUILayout.Width(40));
        editColor = EditorGUILayout.TextField(editColor, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("颜色码:", GUILayout.Width(40));
        editColorCode = EditorGUILayout.TextField(editColorCode, GUILayout.Width(80));

        // 颜色预览
        GUI.backgroundColor = GetColorFromCode(editColorCode);
        EditorGUILayout.LabelField("  预览  ", GUILayout.Width(50), GUILayout.Height(18));
        GUI.backgroundColor = Color.white;

        EditorGUILayout.LabelField("权重:", GUILayout.Width(30));
        editWeight = EditorGUILayout.IntField(editWeight, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("新增", GUILayout.Width(100)))
        {
            AddQuickItem();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);
        EditorGUILayout.HelpBox("提示：修改后记得点击\"保存修改\"或使用快速新增区域添加数据", MessageType.Info);
    }

    private void LoadData()
    {
        string fullPath = Path.Combine(Application.dataPath, "Resources/JsonData/BaseFramework/rarities.json");
        if (File.Exists(fullPath))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<RarityListWrapper>(File.ReadAllText(fullPath));
                rarities = wrapper?.rarities ?? new List<RarityData>();
                if (rarities.Count > 0) Debug.Log($"加载成功，共{rarities.Count}条数据");
            }
            catch (System.Exception e) { Debug.LogError($"加载失败: {e.Message}"); rarities = new List<RarityData>(); }
        }
        else
        {
            Debug.LogWarning($"文件不存在: {fullPath}，创建空列表");
            rarities = new List<RarityData>();
        }
        Repaint();
    }

    private void SaveData()
    {
        string directory = Path.Combine(Application.dataPath, "Resources/JsonData/Base");
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        File.WriteAllText(Path.Combine(Application.dataPath, "Resources/JsonData/BaseFramework/rarities.json"), JsonUtility.ToJson(new RarityListWrapper { rarities = rarities }, true));
        AssetDatabase.Refresh();
        Debug.Log("保存成功");
    }

    private void AddNewItem()
    {
        int newId = 201;
        if (rarities.Count > 0)
        {
            int maxId = 0;
            foreach (var rarity in rarities)
            {
                if (rarity.id > maxId) maxId = rarity.id;
            }
            newId = maxId + 1;
        }

        rarities.Add(new RarityData { id = newId, name = "新稀有度", color = "新颜色", colorCode = "#FFFFFF", weight = 100 });
        selectedIndex = rarities.Count - 1;
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

        if (string.IsNullOrEmpty(editColor))
        {
            EditorUtility.DisplayDialog("错误", "颜色名称不能为空", "确定");
            return;
        }

        if (string.IsNullOrEmpty(editColorCode))
        {
            EditorUtility.DisplayDialog("错误", "颜色代码不能为空", "确定");
            return;
        }

        if (IsIdDuplicate(editId, -1))
        {
            EditorUtility.DisplayDialog("错误", $"ID {editId} 已存在", "确定");
            return;
        }

        rarities.Add(new RarityData { id = editId, name = editName, color = editColor, colorCode = editColorCode, weight = editWeight });

        // 按ID排序
        for (int i = 0; i < rarities.Count - 1; i++)
        {
            for (int j = i + 1; j < rarities.Count; j++)
            {
                if (rarities[i].id > rarities[j].id)
                {
                    RarityData temp = rarities[i];
                    rarities[i] = rarities[j];
                    rarities[j] = temp;
                }
            }
        }

        SaveData();
        LoadData();

        // 计算下一个可用ID
        int nextId = 201;
        if (rarities.Count > 0)
        {
            int maxId = 0;
            foreach (var rarity in rarities)
            {
                if (rarity.id > maxId) maxId = rarity.id;
            }
            nextId = maxId + 1;
        }
        editId = nextId;
        editName = "";
        editColor = "";
        editColorCode = "";
        editWeight = 100;

        EditorUtility.DisplayDialog("成功", "新增成功", "确定");
    }

    private bool IsIdDuplicate(int id, int excludeIndex)
    {
        for (int i = 0; i < rarities.Count; i++)
        {
            if (i != excludeIndex && rarities[i].id == id)
            {
                return true;
            }
        }
        return false;
    }

    private Color GetColorFromCode(string colorCode)
    {
        if (string.IsNullOrEmpty(colorCode)) return Color.white;

        if (ColorUtility.TryParseHtmlString(colorCode, out Color color))
        {
            return color;
        }
        return Color.white;
    }

    [System.Serializable]
    public class RarityData
    {
        public int id;
        public string name;
        public string color;
        public string colorCode;
        public int weight;
        public int exp;
    }

    [System.Serializable]
    public class RarityListWrapper
    {
        public List<RarityData> rarities;
    }
}
#endif