// ==================== StarRatingDataEditor.cs ====================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class StarRatingDataEditor : BaseDataEditor<StarRatingData>
{
    private int selectedIndex = -1;
    private int editId = 501;
    private string editName = "";
    private string editDescription = "";
    private float editMultiplier = 1.0f;
    private float editWeight = 1.0f;
    private string editColor = "#CD7F32";
    private int editSortOrder = 1;

    private const string RELATIVE_PATH = "Resources/JsonData/BaseFramework/starRatings.json";

    // 表头宽度
    private float col1 = 30;   // 颜色
    private float col2 = 60;   // ID
    private float col3 = 80;   // 名称
    private float col4 = 60;   // 倍率
    private float col5 = 60;   // 权重
    private float col6 = 70;   // 排序
    private float col7 = 100;  // 描述
    private float col8 = 100;  // 颜色码

    public StarRatingDataEditor() : base(RELATIVE_PATH) { }

    [MenuItem("Tools/基础框架/501_重量(星级)倍数")]
    public static void ShowWindow()
    {
        StarRatingDataEditor window = GetWindow<StarRatingDataEditor>("星级倍数数据编辑器");
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
        EditorGUILayout.LabelField("星级倍数列表", EditorStyles.boldLabel);

        // ==================== 表头 ====================
        EditorGUILayout.BeginHorizontal("box");

        DrawResizableColumn("颜色", ref col1, "col1");
        DrawResizableColumn("ID", ref col2, "col2");
        DrawResizableColumn("名称", ref col3, "col3");
        DrawResizableColumn("倍率", ref col4, "col4");
        DrawResizableColumn("权重", ref col5, "col5");
        DrawResizableColumn("排序", ref col6, "col6");
        DrawResizableColumn("描述", ref col7, "col7");
        DrawResizableColumn("颜色码", ref col8, "col8");

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
		StarRatingData item = dataList[index];
		EditorGUILayout.BeginHorizontal();

		if (selectedIndex == index) GUI.backgroundColor = Color.cyan;

		// 显示颜色块
		GUI.backgroundColor = GetColorFromCode(item.color);
		EditorGUILayout.LabelField("  ", GUILayout.Width(col1), GUILayout.Height(18));
		GUI.backgroundColor = Color.white;

		EditorGUILayout.LabelField($"[{item.id}]", GUILayout.Width(col2));
		EditorGUILayout.LabelField(item.name, GUILayout.Width(col3));
		EditorGUILayout.LabelField(item.multiplier.ToString(), GUILayout.Width(col4));
		EditorGUILayout.LabelField(item.weight.ToString(), GUILayout.Width(col5));
		EditorGUILayout.LabelField(item.sortOrder.ToString(), GUILayout.Width(col6));

		// 显示描述（限制长度）
		string shortDesc = item.description.Length > 12 ? item.description.Substring(0, 12) + "..." : item.description;
		EditorGUILayout.LabelField(shortDesc, GUILayout.Width(col7));

		// 显示颜色码
		EditorGUILayout.LabelField(item.color, GUILayout.Width(col8));

		GUI.backgroundColor = Color.white;
		GUILayout.FlexibleSpace();

		if (GUILayout.Button("编辑", GUILayout.Width(50))) selectedIndex = index;

		GUI.backgroundColor = Color.red;
		if (GUILayout.Button("删除", GUILayout.Width(50)) && EditorUtility.DisplayDialog("确认删除", $"确定要删除星级 [{item.id}] {item.name} 吗？", "删除", "取消"))
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
            StarRatingData item = dataList[selectedIndex];
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
            EditorGUILayout.LabelField("倍率:", GUILayout.Width(40));
            item.multiplier = EditorGUILayout.FloatField(item.multiplier);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("权重:", GUILayout.Width(40));
            item.weight = EditorGUILayout.FloatField(item.weight);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("颜色码:", GUILayout.Width(40));
            item.color = EditorGUILayout.TextField(item.color);
            GUI.backgroundColor = GetColorFromCode(item.color);
            EditorGUILayout.LabelField(" 预览  ", GUILayout.Width(50), GUILayout.Height(18));
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("排序:", GUILayout.Width(40));
            item.sortOrder = EditorGUILayout.IntField(item.sortOrder);
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
        EditorGUILayout.LabelField("倍率:", GUILayout.Width(30));
        editMultiplier = EditorGUILayout.FloatField(editMultiplier, GUILayout.Width(50));
        EditorGUILayout.LabelField("权重:", GUILayout.Width(30));
        editWeight = EditorGUILayout.FloatField(editWeight, GUILayout.Width(50));
        EditorGUILayout.LabelField("排序:", GUILayout.Width(30));
        editSortOrder = EditorGUILayout.IntField(editSortOrder, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("颜色码:", GUILayout.Width(40));
        editColor = EditorGUILayout.TextField(editColor, GUILayout.Width(100));
        GUI.backgroundColor = GetColorFromCode(editColor);
        EditorGUILayout.LabelField(" 预览  ", GUILayout.Width(50), GUILayout.Height(18));
        GUI.backgroundColor = Color.white;
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
        if (File.Exists(FullPath))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<StarRatingListWrapper>(File.ReadAllText(FullPath));
                dataList = wrapper?.starRatings ?? new List<StarRatingData>();
                if (dataList.Count > 0) Debug.Log($"加载成功，共{dataList.Count}条数据");
            }
            catch (System.Exception e) { Debug.LogError($"加载失败: {e.Message}"); dataList = new List<StarRatingData>(); }
        }
        else
        {
            Debug.LogWarning($"文件不存在: {FullPath}，创建空列表");
            dataList = new List<StarRatingData>();
        }
        Repaint();
    }

    private void SaveData()
    {
        string directory = Path.GetDirectoryName(FullPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(FullPath, JsonUtility.ToJson(new StarRatingListWrapper { starRatings = dataList }, true));
        AssetDatabase.Refresh();
        Debug.Log($"保存成功: {FullPath}");
    }

    private void AddNewItem()
    {
        int newId = 501;
        if (dataList.Count > 0)
        {
            int maxId = 0;
            foreach (var item in dataList) if (item.id > maxId) maxId = item.id;
            newId = maxId + 1;
        }
        dataList.Add(new StarRatingData { id = newId, name = "新星级", description = "描述", multiplier = 1.0f, weight = 1.0f, color = "#FFFFFF", sortOrder = dataList.Count + 1 });
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
        dataList.Add(new StarRatingData { id = editId, name = editName, description = editDescription, multiplier = editMultiplier, weight = editWeight, color = editColor, sortOrder = editSortOrder });
        dataList = dataList.OrderBy(item => item.id).ToList();
        SaveData();
        LoadData();
        int nextId = 501;
        if (dataList.Count > 0)
        {
            int maxId = 0;
            foreach (var item in dataList) if (item.id > maxId) maxId = item.id;
            nextId = maxId + 1;
        }
        editId = nextId;
        editName = "";
        editDescription = "";
        editMultiplier = 1.0f;
        editWeight = 1.0f;
        editColor = "#FFFFFF";
        editSortOrder = dataList.Count + 1;
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

    private Color GetColorFromCode(string colorCode)
    {
        if (string.IsNullOrEmpty(colorCode)) return Color.white;
        if (ColorUtility.TryParseHtmlString(colorCode, out Color color)) return color;
        return Color.white;
    }


}
#endif