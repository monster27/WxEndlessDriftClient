// ==================== NestBaitDataJsonEditor.cs ====================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class NestBaitDataJsonEditor : EditorWindow
{
    private List<NestBaitData> dataList = new List<NestBaitData>();
    private NestBaitConstants constants = new NestBaitConstants();
    private int selectedIndex = -1;
    private Vector2 scrollPosition = Vector2.zero;

    private int editId = 2501;
    private string editName = "";
    private string editDescription = "";
    private int editApplicableScene = 101;

    private float col1 = 50;
    private float col2 = 150;
    private float col3 = 100;
    private float col4 = 350;

    private const string RELATIVE_PATH = "Resources/JsonData/Game/BagItem/nestBaits.json";

    private string FullPath => Path.Combine(Application.dataPath, RELATIVE_PATH);

    [MenuItem("Tools/游戏内容/2.物品内部数据/2501_窝料")]
    public static void ShowWindow()
    {
        NestBaitDataJsonEditor window = GetWindow<NestBaitDataJsonEditor>("窝料数据编辑器");
        window.minSize = new Vector2(800, 700);
        window.Show();
    }

    private void OnEnable() => LoadData();

    private void OnGUI()
    {
        DrawToolbar();
        DrawConstantsPanel();
        DrawDataTable();
        DrawEditPanel();
        DrawBottomButtons();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60))) LoadData();
        if (GUILayout.Button("新增", EditorStyles.toolbarButton, GUILayout.Width(60))) AddNewItem();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"共 {dataList.Count} 条数据", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    private void DrawConstantsPanel()
    {
        EditorGUILayout.LabelField("窝料常量配置", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("默认窝料ID:", GUILayout.Width(100));
        constants.defaultBaitItemId = EditorGUILayout.IntField(constants.defaultBaitItemId);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("连续模式加时(秒):", GUILayout.Width(120));
        constants.continuousModeAddTime = EditorGUILayout.FloatField(constants.continuousModeAddTime);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("连续模式最大时长(秒):", GUILayout.Width(140));
        constants.continuousModeMaxTime = EditorGUILayout.FloatField(constants.continuousModeMaxTime);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    private void DrawDataTable()
    {
        EditorGUILayout.LabelField("窝料列表", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal("box");

        EditorGUILayout.LabelField("ID", GUILayout.Width(col1));
        EditorGUILayout.LabelField("名称", GUILayout.Width(col2));
        EditorGUILayout.LabelField("适用场景", GUILayout.Width(col3));
        EditorGUILayout.LabelField("描述", GUILayout.Width(col4));

        EditorGUILayout.LabelField("操作", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < dataList.Count; i++)
        {
            DrawDataRow(i);
        }

        if (dataList.Count == 0)
        {
            EditorGUILayout.LabelField("暂无数据，点击\"新增\"添加", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndScrollView();
        GUILayout.Space(10);
    }

    private void DrawDataRow(int index)
    {
        NestBaitData item = dataList[index];

        if (selectedIndex == index)
            GUI.backgroundColor = Color.cyan;
        else if (index % 2 == 0)
            GUI.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        else
            GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        EditorGUILayout.BeginHorizontal("box");

        EditorGUILayout.LabelField(item.id.ToString(), GUILayout.Width(col1));
        EditorGUILayout.LabelField(item.name, GUILayout.Width(col2));
        EditorGUILayout.LabelField(item.applicableScene.ToString(), GUILayout.Width(col3));
        EditorGUILayout.LabelField(item.description, GUILayout.Width(col4));

        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("编辑", GUILayout.Width(50))) selectedIndex = index;

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("删除", GUILayout.Width(50)) && EditorUtility.DisplayDialog("确认删除", $"确定要删除窝料 [{item.id}] {item.name} 吗？", "删除", "取消"))
        {
            dataList.RemoveAt(index);
            if (selectedIndex >= dataList.Count) selectedIndex = -1;
            SaveData();
            LoadData();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    private void DrawEditPanel()
    {
        EditorGUILayout.LabelField("编辑区域", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (selectedIndex >= 0 && selectedIndex < dataList.Count)
        {
            NestBaitData item = dataList[selectedIndex];
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
            EditorGUILayout.LabelField("适用场景:", GUILayout.Width(60));
            item.applicableScene = EditorGUILayout.IntField(item.applicableScene);
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
        editName = EditorGUILayout.TextField(editName, GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("适用场景:", GUILayout.Width(60));
        editApplicableScene = EditorGUILayout.IntField(editApplicableScene, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("描述:", GUILayout.Width(30));
        editDescription = EditorGUILayout.TextField(editDescription, GUILayout.Width(350));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("新增", GUILayout.Width(100))) AddQuickItem();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("提示：修改常量或列表后记得点击\"保存修改\"或使用快速新增区域添加数据", MessageType.Info);
    }

    private void LoadData()
    {
        if (File.Exists(FullPath))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<NestBaitListWrapper>(File.ReadAllText(FullPath));
                dataList = wrapper?.nestBaits != null ? new List<NestBaitData>(wrapper.nestBaits) : new List<NestBaitData>();
                constants = wrapper?.constants ?? new NestBaitConstants();
                if (dataList.Count > 0) Debug.Log($"加载成功，共{dataList.Count}条数据");
            }
            catch (System.Exception e) { Debug.LogError($"加载失败: {e.Message}"); dataList = new List<NestBaitData>(); }
        }
        else
        {
            Debug.LogWarning($"文件不存在: {FullPath}，创建空列表");
            dataList = new List<NestBaitData>();
            constants = new NestBaitConstants();
        }
        Repaint();
    }

    private void SaveData()
    {
        string directory = Path.Combine(Application.dataPath, "Resources/JsonData/Game");
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(FullPath, JsonUtility.ToJson(new NestBaitListWrapper { constants = constants, nestBaits = dataList.ToArray() }, true));
        AssetDatabase.Refresh();
        Debug.Log("保存成功");
    }

    private void AddNewItem()
    {
        int newId = 2501;
        if (dataList.Count > 0)
        {
            int maxId = 0;
            foreach (var item in dataList) if (item.id > maxId) maxId = item.id;
            newId = maxId + 1;
        }
        dataList.Add(new NestBaitData { id = newId, name = "新窝料", description = "描述", applicableScene = 101 });
        selectedIndex = dataList.Count - 1;
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
        dataList.Add(new NestBaitData { id = editId, name = editName, description = editDescription, applicableScene = editApplicableScene });
        for (int i = 0; i < dataList.Count - 1; i++)
        {
            for (int j = i + 1; j < dataList.Count; j++)
            {
                if (dataList[i].id > dataList[j].id)
                {
                    NestBaitData temp = dataList[i];
                    dataList[i] = dataList[j];
                    dataList[j] = temp;
                }
            }
        }
        SaveData();
        LoadData();
        int nextId = 2501;
        if (dataList.Count > 0)
        {
            int maxId = 0;
            foreach (var item in dataList) if (item.id > maxId) maxId = item.id;
            nextId = maxId + 1;
        }
        editId = nextId;
        editName = "";
        editDescription = "";
        editApplicableScene = 101;
        EditorUtility.DisplayDialog("成功", "新增成功", "确定");
    }

    private bool IsIdDuplicate(int id, int excludeIndex)
    {
        for (int i = 0; i < dataList.Count; i++)
        {
            if (i != excludeIndex && dataList[i].id == id) return true;
        }
        return false;
    }

    private void HandleColumnResize()
    {
        Event current = Event.current;
        if (current.type == EventType.MouseUp && current.button == 0)
        {
            if (GUI.tooltip == "col1") col1 = Mathf.Max(50, col1 + current.delta.x);
            else if (GUI.tooltip == "col2") col2 = Mathf.Max(100, col2 + current.delta.x);
            else if (GUI.tooltip == "col3") col3 = Mathf.Max(80, col3 + current.delta.x);
            else if (GUI.tooltip == "col4") col4 = Mathf.Max(200, col4 + current.delta.x);
        }
    }

    private void DrawResizableColumn(string label, ref float width, string tooltip)
    {
        EditorGUILayout.LabelField(label, GUILayout.Width(width));
        if (Event.current.type == EventType.MouseDown &&
            Event.current.mousePosition.x > GUILayoutUtility.GetLastRect().xMax - 2 &&
            Event.current.mousePosition.x < GUILayoutUtility.GetLastRect().xMax + 2)
        {
            GUI.tooltip = tooltip;
        }
    }
}
#endif