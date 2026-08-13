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
    private string editType = "FullScreenSwim";

    private const string RELATIVE_PATH = "Resources/JsonData/BaseFramework/fishSpecies.json";

    // 表头宽度
    private float col1 = 60;   // ID
    private float col2 = 120;  // 名称
    private float col3 = 150;  // 类型
    private float col4 = 300;  // 描述

    // 预设类型选项
    private string[] typeOptions = { "FullScreenSwim", "FullScreenStatic", "BottomSwim", "BottomStatic" };
    private string[] typeDisplayNames = { "全屏游动", "全屏静止", "底沙游动", "底沙静止" };
    private string[] typeNameOptions = { "全屏游动类", "全屏静止类", "底沙游动类", "底沙静止类" };
    private int[] typeIds = { 601, 602, 603, 604 };

    public FishSpeciesDataEditor() : base(RELATIVE_PATH) { }

    [MenuItem("Tools/基础框架/601_鱼类品种")]
    public static void ShowWindow()
    {
        FishSpeciesDataEditor window = GetWindow<FishSpeciesDataEditor>("鱼类品种数据编辑器");
        window.minSize = new Vector2(750, 600);
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
        DrawResizableColumn("类型", ref col3, "col3");
        DrawResizableColumn("描述", ref col4, "col4");

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

        // 显示类型（带颜色标识）
        GUI.color = GetTypeColor(item.type);
        EditorGUILayout.LabelField(item.type, GUILayout.Width(col3));
        GUI.color = Color.white;

        EditorGUILayout.LabelField(item.description, GUILayout.Width(col4));
        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("编辑", GUILayout.Width(50)))
        {
            selectedIndex = index;
            LoadItemToEdit(item);
        }

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

    private Color GetTypeColor(string type)
    {
        switch (type)
        {
            case "FullScreenSwim": return Color.cyan;
            case "FullScreenStatic": return Color.yellow;
            case "BottomSwim": return Color.green;
            case "BottomStatic": return new Color(0.8f, 0.5f, 0.2f);
            default: return Color.white;
        }
    }

    private void LoadItemToEdit(FishSpeciesData item)
    {
        editId = item.id;
        editName = item.name;
        editDescription = item.description;
        editType = item.type;
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
            if (newId != item.id && !IsIdDuplicate(newId, selectedIndex))
            {
                item.id = newId;
            }
            else if (newId != item.id) EditorUtility.DisplayDialog("错误", $"ID {newId} 已存在", "确定");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("名称:", GUILayout.Width(40));
            item.name = EditorGUILayout.TextField(item.name);
            EditorGUILayout.EndHorizontal();

            // 类型下拉选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("类型:", GUILayout.Width(40));
            int currentTypeIndex = System.Array.IndexOf(typeOptions, item.type);
            if (currentTypeIndex < 0) currentTypeIndex = 0;
            int newTypeIndex = EditorGUILayout.Popup(currentTypeIndex, typeOptions, GUILayout.Width(150));
            if (newTypeIndex != currentTypeIndex)
            {
                item.type = typeOptions[newTypeIndex];
                item.name = typeNameOptions[newTypeIndex];
                item.id = typeIds[newTypeIndex];
                item.description = GetDefaultDescription(item.type);
            }
            EditorGUILayout.LabelField("(对应枚举: FishSpeciesType)", GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("描述:", GUILayout.Width(40));
            item.description = EditorGUILayout.TextArea(item.description, GUILayout.Height(60));
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
        EditorGUILayout.LabelField("类型:", GUILayout.Width(40));
        int currentTypeIndex = System.Array.IndexOf(typeOptions, editType);
        if (currentTypeIndex < 0) currentTypeIndex = 0;
        int newTypeIndex = EditorGUILayout.Popup(currentTypeIndex, typeOptions, GUILayout.Width(150));
        if (newTypeIndex != currentTypeIndex)
        {
            editType = typeOptions[newTypeIndex];
            editId = typeIds[newTypeIndex];
            editName = typeNameOptions[newTypeIndex];
            editDescription = GetDefaultDescription(editType);
        }

        EditorGUILayout.LabelField("ID:", GUILayout.Width(30));
        editId = EditorGUILayout.IntField(editId, GUILayout.Width(60));
        EditorGUILayout.LabelField("名称:", GUILayout.Width(30));
        editName = EditorGUILayout.TextField(editName, GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("描述:", GUILayout.Width(40));
        editDescription = EditorGUILayout.TextField(editDescription, GUILayout.Width(400));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("新增", GUILayout.Width(100))) AddQuickItem();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "提示：\n" +
            "• 类型对应 FishSpeciesType 枚举\n" +
            "• FullScreenSwim: 全屏游动 | FullScreenStatic: 全屏静止\n" +
            "• BottomSwim: 底沙游动 | BottomStatic: 底沙静止\n" +
            "• 品种ID用于关联FishData中的fishSpeciesId字段",
            MessageType.Info
        );
    }

    private string GetDefaultDescription(string type)
    {
        switch (type)
        {
            case "FullScreenSwim":
                return "该类生物活跃于鱼缸的各个水层与区域，会持续在屏幕可见范围内自由穿梭游动，不会固定于某一位置，为鱼缸带来灵动与生机。";
            case "FullScreenStatic":
                return "该类生物在打开鱼缸时，会随机选取屏幕内的一处位置进行固定摆放，此后保持静止不动，如同精美的装饰或摆件，为鱼缸增添静态的观赏元素。";
            case "BottomSwim":
                return "该类生物紧贴鱼缸底部的沙层或基质表面活动，以爬行或缓慢游动的方式在底沙区域来回移动，不会离开底部区域，适合作为底层景观的活跃点缀。";
            case "BottomStatic":
                return "该类生物在鱼缸开启时，会随机选择底沙区域的一个固定点进行安置，随后保持静止，如同沉入沙中的化石或静卧的底栖生物，为底层景观提供稳定的视觉焦点。";
            default:
                return "";
        }
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
        dataList.Add(new FishSpeciesData
        {
            id = 601,
            name = "全屏游动类",
            description = GetDefaultDescription("FullScreenSwim"),
            type = "FullScreenSwim"
        });
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

        dataList.Add(new FishSpeciesData
        {
            id = editId,
            name = editName,
            description = editDescription,
            type = editType
        });
        dataList = dataList.OrderBy(item => item.id).ToList();
        SaveData();
        LoadData();

        // 重置为默认值
        editId = 601;
        editName = "全屏游动类";
        editDescription = GetDefaultDescription("FullScreenSwim");
        editType = "FullScreenSwim";
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
