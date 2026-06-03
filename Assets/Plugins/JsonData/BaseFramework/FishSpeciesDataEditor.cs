// ==================== FishSpeciesDataEditor.cs ====================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class FishSpeciesDataEditor : BaseDataEditor<FishSpeciesData>
{
    private int selectedIndex = -1;
    private int editId = 601;
    private string editName = "";
    private string editDescription = "";
    private string editMovementType = "free";
    private string editPositionType = "water";

    private const string RELATIVE_PATH = "Resources/JsonData/BaseFramework/fishSpecies.json";

    // 表头宽度
    private float col1 = 60;   // ID
    private float col2 = 100;  // 名称
    private float col3 = 100;  // 移动类型
    private float col4 = 100;  // 位置类型

    public FishSpeciesDataEditor() : base(RELATIVE_PATH) { }

    [MenuItem("Tools/基础框架/601_鱼类品种")]
    public static void ShowWindow()
    {
        FishSpeciesDataEditor window = GetWindow<FishSpeciesDataEditor>("鱼类品种数据编辑器");
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
        HandleColumnResize();
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

    private void DrawDataList()
    {
        EditorGUILayout.LabelField("鱼类品种列表", EditorStyles.boldLabel);

        // ==================== 表头 ====================
        EditorGUILayout.BeginHorizontal("box");

        DrawResizableColumn("ID", ref col1, "col1");
        DrawResizableColumn("名称", ref col2, "col2");
        DrawResizableColumn("移动类型", ref col3, "col3");
        DrawResizableColumn("位置类型", ref col4, "col4");

        EditorGUILayout.LabelField("操作", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        // ==================== 数据行 ====================
        EditorGUILayout.BeginVertical("box");
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(250));

        for (int i = 0; i < dataList.Count; i++) DrawListItem(i);
        if (dataList.Count == 0) EditorGUILayout.LabelField("暂无数据，点击\"新增\"添加", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    private void DrawListItem(int index)
    {
        FishSpeciesData item = dataList[index];
        EditorGUILayout.BeginHorizontal();

        if (selectedIndex == index) GUI.backgroundColor = Color.cyan;
        EditorGUILayout.LabelField($"[{item.id}]", GUILayout.Width(col1));
        EditorGUILayout.LabelField(item.name, GUILayout.Width(col2));
        EditorGUILayout.LabelField(item.movementType, GUILayout.Width(col3));
        EditorGUILayout.LabelField(item.positionType, GUILayout.Width(col4));
        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("编辑", GUILayout.Width(50))) selectedIndex = index;

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("删除", GUILayout.Width(50)) && EditorUtility.DisplayDialog("确认删除", $"确定要删除鱼类品种 [{item.id}] {item.name} 吗？", "删除", "取消"))
        {
            dataList.RemoveAt(index);
            if (selectedIndex >= dataList.Count) selectedIndex = -1;
            SaveData();
            LoadData();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        if (index < dataList.Count - 1) EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }

    private void DrawEditPanel()
    {
        EditorGUILayout.LabelField("编辑区域", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (selectedIndex >= 0 && selectedIndex < dataList.Count)
        {
            FishSpeciesData item = dataList[selectedIndex];
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
            EditorGUILayout.LabelField("移动类型:", GUILayout.Width(60));
            string[] movementOptions = { "static", "free", "walk" };
            int movementIndex = System.Array.IndexOf(movementOptions, item.movementType);
            if (movementIndex < 0) movementIndex = 1;
            movementIndex = EditorGUILayout.Popup(movementIndex, movementOptions);
            item.movementType = movementOptions[movementIndex];
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("位置类型:", GUILayout.Width(60));
            string[] positionOptions = { "bottom", "water" };
            int positionIndex = System.Array.IndexOf(positionOptions, item.positionType);
            if (positionIndex < 0) positionIndex = 1;
            positionIndex = EditorGUILayout.Popup(positionIndex, positionOptions);
            item.positionType = positionOptions[positionIndex];
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
        EditorGUILayout.LabelField("移动类型:", GUILayout.Width(60));
        string[] movementOptions = { "static", "free", "walk" };
        int movementIndex = EditorGUILayout.Popup(System.Array.IndexOf(movementOptions, editMovementType), movementOptions);
        editMovementType = movementOptions[movementIndex >= 0 ? movementIndex : 1];
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("位置类型:", GUILayout.Width(60));
        string[] positionOptions = { "bottom", "water" };
        int positionIndex = EditorGUILayout.Popup(System.Array.IndexOf(positionOptions, editPositionType), positionOptions);
        editPositionType = positionOptions[positionIndex >= 0 ? positionIndex : 1];
        EditorGUILayout.LabelField("描述:", GUILayout.Width(30));
        editDescription = EditorGUILayout.TextField(editDescription, GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("新增", GUILayout.Width(100))) AddQuickItem();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("提示：移动类型 - static(静止) free(自由移动) walk(行走)；位置类型 - bottom(底部) water(水中)", MessageType.Info);
    }

    private void LoadData()
    {
        if (File.Exists(FullPath))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<FishSpeciesListWrapper>(File.ReadAllText(FullPath));
                dataList = wrapper?.fishSpecies ?? new List<FishSpeciesData>();
                if (dataList.Count > 0) Debug.Log($"加载成功，共{dataList.Count}条数据");
            }
            catch (System.Exception e) { Debug.LogError($"加载失败: {e.Message}"); dataList = new List<FishSpeciesData>(); }
        }
        else
        {
            Debug.LogWarning($"文件不存在: {FullPath}，创建空列表");
            dataList = new List<FishSpeciesData>();
        }
        Repaint();
    }

    private void SaveData()
    {
        string directory = Path.GetDirectoryName(FullPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(FullPath, JsonUtility.ToJson(new FishSpeciesListWrapper { fishSpecies = dataList }, true));
        AssetDatabase.Refresh();
        Debug.Log($"保存成功: {FullPath}");
    }

    private void AddNewItem()
    {
        int newId = 601;
        if (dataList.Count > 0)
        {
            int maxId = 0;
            foreach (var item in dataList) if (item.id > maxId) maxId = item.id;
            newId = maxId + 1;
        }
        dataList.Add(new FishSpeciesData { id = newId, name = "新品种", description = "描述", movementType = "free", positionType = "water" });
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
        dataList.Add(new FishSpeciesData { id = editId, name = editName, description = editDescription, movementType = editMovementType, positionType = editPositionType });
        dataList = dataList.OrderBy(item => item.id).ToList();
        SaveData();
        LoadData();
        int nextId = 601;
        if (dataList.Count > 0)
        {
            int maxId = 0;
            foreach (var item in dataList) if (item.id > maxId) maxId = item.id;
            nextId = maxId + 1;
        }
        editId = nextId;
        editName = "";
        editDescription = "";
        editMovementType = "free";
        editPositionType = "water";
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


}
#endif