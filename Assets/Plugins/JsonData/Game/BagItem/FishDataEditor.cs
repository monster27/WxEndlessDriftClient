#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class FishDataEditor : BaseDataEditor<FishData>
{
    // 编辑变量
    private int editId = 1001;
    private string editName = "";
    private string editDescription = "";
    private int editIslandId = 0;
    private int editRarityId = 201;
    private string editPreferredIslandIds = "";
    private string editPreferredTimeIds = "";
    private string editPreferredBaitIds = "";
    private string editPreferredWeatherIds = "";
    private int editFishSpeciesId = 602;
    private int editStruggleTime = 5;
    private float editFlashProbability = 0.5f;
    private float editBaseWeight = 2.5f;
    private int editBaseExp = 10;
    private float editScale = 1.0f;  // 新增 Scale

    private const string RELATIVE_PATH = "Resources/JsonData/Game/BagItem/fishes.json";

    // 表头宽度
    private float col1 = 50;   // ID
    private float col2 = 80;   // 名称
    private float col3 = 100;  // 存在岛屿
    private float col4 = 70;   // 稀有度
    private float col5 = 130;  // 偏向岛屿
    private float col6 = 80;   // 重量
    private float col7 = 70;   // 经验
    private float col8 = 150;  // 时间偏向
    private float col9 = 150;  // 鱼饵偏向
    private float col10 = 150; // 天气偏向
    private float col11 = 80;  // 挣扎时间
    private float col12 = 80;  // 闪光概率
    private float col13 = 80;  // 品种ID
    private float col14 = 200; // 描述
    private float col15 = 60;  // Scale（新增）

    public FishDataEditor() : base(RELATIVE_PATH) { }

    [MenuItem("Tools/游戏内容/2.物品内部数据(记得编辑通用数据)/1001_水产")]
    public static void ShowWindow()
    {
        FishDataEditor window = GetWindow<FishDataEditor>("水产数据编辑器");
        window.minSize = new Vector2(1460, 600);
        window.Show();
    }

    private void OnEnable() => LoadData();

    private void Update() => HandleColumnResize();

    private void OnGUI()
    {
        DrawToolbar();
        DrawDataTable();
        DrawEditPanel();
        DrawBottomButtons();
        HandleMouseUp();
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

    private void DrawDataTable()
    {
        EditorGUILayout.LabelField("水产列表", EditorStyles.boldLabel);

        // ==================== 表头 ====================
        EditorGUILayout.BeginHorizontal("box");

        DrawResizableColumn("ID", ref col1, "col1");
        DrawResizableColumn("稀有度", ref col4, "col4");
        DrawResizableColumn("名称", ref col2, "col2");
        DrawResizableColumn("存在岛屿", ref col3, "col3");
        DrawResizableColumn("偏向岛屿", ref col5, "col5");
        DrawResizableColumn("重量(kg)", ref col6, "col6");
        DrawResizableColumn("经验", ref col7, "col7");
        DrawResizableColumn("时间偏向", ref col8, "col8");
        DrawResizableColumn("鱼饵偏向", ref col9, "col9");
        DrawResizableColumn("天气偏向", ref col10, "col10");
        DrawResizableColumn("挣扎时间", ref col11, "col11");
        DrawResizableColumn("闪光概率", ref col12, "col12");
        DrawResizableColumn("品种ID", ref col13, "col13");
        DrawResizableColumn("Scale", ref col15, "col15");  // 新增
        DrawResizableColumn("描述", ref col14, "col14");

        EditorGUILayout.LabelField("操作", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        // ==================== 数据行 ====================
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
        FishData item = dataList[index];

        // 奇偶行不同底色
        if (selectedIndex == index)
            GUI.backgroundColor = Color.cyan;
        else if (index % 2 == 0)
            GUI.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        else
            GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        EditorGUILayout.BeginHorizontal("box");

        // ID
        EditorGUILayout.LabelField(item.id.ToString(), GUILayout.Width(col1));

        // 稀有度
        EditorGUILayout.LabelField(item.rarityId.ToString(), GUILayout.Width(col4));

        // 名称
        EditorGUILayout.LabelField(item.name, GUILayout.Width(col2));

        // 存在岛屿
        string islandStr = item.islandId == 0 ? "所有岛屿" : item.islandId.ToString();
        EditorGUILayout.LabelField(islandStr, GUILayout.Width(col3));

        // 偏向岛屿
        string preferredStr = item.preferredIslandIds.Count > 0 ? string.Join(",", item.preferredIslandIds) : "无";
        EditorGUILayout.LabelField(preferredStr, GUILayout.Width(col5));

        // 重量
        EditorGUILayout.LabelField($"{item.baseWeight}", GUILayout.Width(col6));

        // 经验
        EditorGUILayout.LabelField(item.baseExp.ToString(), GUILayout.Width(col7));

        // 时间偏向
        string timeStr = item.preferredTimeIds.Count > 0 ? string.Join(",", item.preferredTimeIds) : "无";
        EditorGUILayout.LabelField(timeStr, GUILayout.Width(col8));

        // 鱼饵偏向
        string baitStr = item.preferredBaitIds.Count > 0 ? string.Join(",", item.preferredBaitIds) : "无";
        EditorGUILayout.LabelField(baitStr, GUILayout.Width(col9));

        // 天气偏向
        string weatherStr = item.preferredWeatherIds.Count > 0 ? string.Join(",", item.preferredWeatherIds) : "无";
        EditorGUILayout.LabelField(weatherStr, GUILayout.Width(col10));

        // 挣扎时间
        EditorGUILayout.LabelField($"{item.struggleTime}秒", GUILayout.Width(col11));

        // 闪光概率
        EditorGUILayout.LabelField($"{item.flashProbability}", GUILayout.Width(col12));

        // 品种ID
        EditorGUILayout.LabelField(item.fishSpeciesId.ToString(), GUILayout.Width(col13));

        // Scale（新增）
        EditorGUILayout.LabelField($"{item.scale:F2}", GUILayout.Width(col15));

        // 描述
        string descStr = item.description.Length > 15 ? item.description.Substring(0, 15) + "..." : item.description;
        EditorGUILayout.LabelField(descStr, GUILayout.Width(col14));

        // 操作按钮
        EditorGUILayout.BeginHorizontal(GUILayout.Width(100));
        if (GUILayout.Button("编辑", GUILayout.Width(45)))
        {
            selectedIndex = index;
            LoadItemToEdit(item);
        }

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("删除", GUILayout.Width(45)) && EditorUtility.DisplayDialog("确认删除", $"确定要删除鱼类 [{item.id}] {item.name} 吗？", "删除", "取消"))
        {
            dataList.RemoveAt(index);
            if (selectedIndex >= dataList.Count) selectedIndex = -1;
            SaveData();
            LoadData();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = Color.white;
    }

    private void LoadItemToEdit(FishData item)
    {
        editId = item.id;
        editName = item.name;
        editDescription = item.description;
        editIslandId = item.islandId;
        editRarityId = item.rarityId;
        editPreferredIslandIds = string.Join(",", item.preferredIslandIds);
        editPreferredTimeIds = string.Join(",", item.preferredTimeIds);
        editPreferredBaitIds = string.Join(",", item.preferredBaitIds);
        editPreferredWeatherIds = string.Join(",", item.preferredWeatherIds);
        editFishSpeciesId = item.fishSpeciesId;
        editStruggleTime = item.struggleTime;
        editFlashProbability = item.flashProbability;
        editBaseWeight = item.baseWeight;
        editBaseExp = item.baseExp;
        editScale = item.scale;  // 新增
    }

    private void DrawEditPanel()
    {
        EditorGUILayout.LabelField("编辑区域", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (selectedIndex >= 0 && selectedIndex < dataList.Count)
        {
            EditorGUILayout.LabelField($"正在编辑: [{editId}] {editName}", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // ==================== 基础信息区域 ====================
            EditorGUILayout.LabelField("【基础信息】", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ID:", GUILayout.Width(40));
            int newId = EditorGUILayout.IntField(editId, GUILayout.Width(80));
            if (newId != editId && !IsIdDuplicate(newId, selectedIndex)) editId = newId;
            else if (newId != editId) EditorUtility.DisplayDialog("错误", $"ID {newId} 已存在", "确定");

            EditorGUILayout.LabelField("名称:", GUILayout.Width(40));
            editName = EditorGUILayout.TextField(editName, GUILayout.Width(150));

            EditorGUILayout.LabelField("描述:", GUILayout.Width(40));
            editDescription = EditorGUILayout.TextField(editDescription, GUILayout.Width(300));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);

            // ==================== 岛屿信息区域 ====================
            EditorGUILayout.LabelField("【岛屿信息】", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("存在岛屿ID:", GUILayout.Width(80));
            editIslandId = EditorGUILayout.IntField(editIslandId, GUILayout.Width(60));
            EditorGUILayout.LabelField("(0=所有岛屿，-1=无)", GUILayout.Width(120));

            EditorGUILayout.LabelField("偏向岛屿ID:", GUILayout.Width(80));
            editPreferredIslandIds = EditorGUILayout.TextField(editPreferredIslandIds, GUILayout.Width(200));
            EditorGUILayout.LabelField("(逗号分隔)", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);

            // ==================== 稀有度与品种区域 ====================
            EditorGUILayout.LabelField("【稀有度与品种】", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("稀有度ID:", GUILayout.Width(70));
            editRarityId = EditorGUILayout.IntField(editRarityId, GUILayout.Width(60));

            EditorGUILayout.LabelField("鱼类品种ID:", GUILayout.Width(80));
            editFishSpeciesId = EditorGUILayout.IntField(editFishSpeciesId, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);

            // ==================== 偏好设置区域 ====================
            EditorGUILayout.LabelField("【偏好设置】", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("偏向时间ID:", GUILayout.Width(80));
            editPreferredTimeIds = EditorGUILayout.TextField(editPreferredTimeIds, GUILayout.Width(200));
            EditorGUILayout.LabelField("(逗号分隔)", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("偏向鱼饵ID:", GUILayout.Width(80));
            editPreferredBaitIds = EditorGUILayout.TextField(editPreferredBaitIds, GUILayout.Width(200));
            EditorGUILayout.LabelField("(逗号分隔)", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("偏向天气ID:", GUILayout.Width(80));
            editPreferredWeatherIds = EditorGUILayout.TextField(editPreferredWeatherIds, GUILayout.Width(200));
            EditorGUILayout.LabelField("(逗号分隔)", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(5);

            // ==================== 属性参数区域 ====================
            EditorGUILayout.LabelField("【属性参数】", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("挣扎时间(秒):", GUILayout.Width(80));
            editStruggleTime = EditorGUILayout.IntField(editStruggleTime, GUILayout.Width(60));

            EditorGUILayout.LabelField("闪光概率:", GUILayout.Width(70));
            editFlashProbability = EditorGUILayout.Slider(editFlashProbability, 0f, 1f, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("基础重量(kg):", GUILayout.Width(80));
            editBaseWeight = EditorGUILayout.FloatField(editBaseWeight, GUILayout.Width(80));

            EditorGUILayout.LabelField("基础经验值:", GUILayout.Width(80));
            editBaseExp = EditorGUILayout.IntField(editBaseExp, GUILayout.Width(80));

            // Scale 新增
            EditorGUILayout.LabelField("Scale:", GUILayout.Width(50));
            editScale = EditorGUILayout.FloatField(editScale, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("保存修改", GUILayout.Width(100)))
            {
                SaveCurrentEdit();
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

    private void SaveCurrentEdit()
    {
        if (selectedIndex >= 0 && selectedIndex < dataList.Count)
        {
            FishData item = dataList[selectedIndex];
            item.id = editId;
            item.name = editName;
            item.description = editDescription;
            item.islandId = editIslandId;
            item.rarityId = editRarityId;

            // 解析偏向岛屿
            item.preferredIslandIds = new List<int>();
            if (!string.IsNullOrEmpty(editPreferredIslandIds))
            {
                foreach (string idStr in editPreferredIslandIds.Split(','))
                {
                    if (int.TryParse(idStr.Trim(), out int id))
                        item.preferredIslandIds.Add(id);
                }
            }

            // 解析偏向时间
            item.preferredTimeIds = new List<int>();
            if (!string.IsNullOrEmpty(editPreferredTimeIds))
            {
                foreach (string idStr in editPreferredTimeIds.Split(','))
                {
                    if (int.TryParse(idStr.Trim(), out int id))
                        item.preferredTimeIds.Add(id);
                }
            }

            // 解析偏向鱼饵
            item.preferredBaitIds = new List<int>();
            if (!string.IsNullOrEmpty(editPreferredBaitIds))
            {
                foreach (string idStr in editPreferredBaitIds.Split(','))
                {
                    if (int.TryParse(idStr.Trim(), out int id))
                        item.preferredBaitIds.Add(id);
                }
            }

            // 解析偏向天气
            item.preferredWeatherIds = new List<int>();
            if (!string.IsNullOrEmpty(editPreferredWeatherIds))
            {
                foreach (string idStr in editPreferredWeatherIds.Split(','))
                {
                    if (int.TryParse(idStr.Trim(), out int id))
                        item.preferredWeatherIds.Add(id);
                }
            }

            item.fishSpeciesId = editFishSpeciesId;
            item.struggleTime = editStruggleTime;
            item.flashProbability = editFlashProbability;
            item.baseWeight = editBaseWeight;
            item.baseExp = editBaseExp;
            item.scale = editScale;  // 新增
        }
    }

    private void DrawBottomButtons()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("快速新增", EditorStyles.boldLabel);

        // 第一行：基础信息
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ID:", GUILayout.Width(30));
        editId = EditorGUILayout.IntField(editId, GUILayout.Width(60));
        EditorGUILayout.LabelField("名称:", GUILayout.Width(30));
        editName = EditorGUILayout.TextField(editName, GUILayout.Width(100));
        EditorGUILayout.LabelField("描述:", GUILayout.Width(30));
        editDescription = EditorGUILayout.TextField(editDescription, GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();

        // 第二行：岛屿信息
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("存在岛屿:", GUILayout.Width(60));
        editIslandId = EditorGUILayout.IntField(editIslandId, GUILayout.Width(60));
        EditorGUILayout.LabelField("偏向岛屿:", GUILayout.Width(60));
        editPreferredIslandIds = EditorGUILayout.TextField(editPreferredIslandIds, GUILayout.Width(150));
        EditorGUILayout.LabelField("稀有度:", GUILayout.Width(50));
        editRarityId = EditorGUILayout.IntField(editRarityId, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        // 第三行：偏好设置
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("时间偏向:", GUILayout.Width(60));
        editPreferredTimeIds = EditorGUILayout.TextField(editPreferredTimeIds, GUILayout.Width(150));
        EditorGUILayout.LabelField("鱼饵偏向:", GUILayout.Width(60));
        editPreferredBaitIds = EditorGUILayout.TextField(editPreferredBaitIds, GUILayout.Width(150));
        EditorGUILayout.LabelField("天气偏向:", GUILayout.Width(60));
        editPreferredWeatherIds = EditorGUILayout.TextField(editPreferredWeatherIds, GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        // 第四行：属性参数
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("品种ID:", GUILayout.Width(50));
        editFishSpeciesId = EditorGUILayout.IntField(editFishSpeciesId, GUILayout.Width(60));
        EditorGUILayout.LabelField("挣扎时间:", GUILayout.Width(60));
        editStruggleTime = EditorGUILayout.IntField(editStruggleTime, GUILayout.Width(60));
        EditorGUILayout.LabelField("闪光概率:", GUILayout.Width(60));
        editFlashProbability = EditorGUILayout.Slider(editFlashProbability, 0f, 1f, GUILayout.Width(150));
        EditorGUILayout.LabelField("基础重量:", GUILayout.Width(60));
        editBaseWeight = EditorGUILayout.FloatField(editBaseWeight, GUILayout.Width(80));
        EditorGUILayout.LabelField("基础经验:", GUILayout.Width(60));
        editBaseExp = EditorGUILayout.IntField(editBaseExp, GUILayout.Width(80));
        EditorGUILayout.LabelField("Scale:", GUILayout.Width(45));
        editScale = EditorGUILayout.FloatField(editScale, GUILayout.Width(60));
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
            "• 存在岛屿ID：0=所有岛屿可钓到，-1=无（不适用于任何岛屿）\n" +
            "• 偏向岛屿列表：为空表示无偏向，多个ID请用英文逗号分隔\n" +
            "• 其他偏向列表（时间/鱼饵/天气）：用法同上\n" +
            "• Scale：鱼的显示大小缩放，默认1.0",
            MessageType.Info
        );
    }

    private void LoadData()
    {
        if (File.Exists(FullPath))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<FishListWrapper>(File.ReadAllText(FullPath));
                dataList = wrapper?.fishes ?? new List<FishData>();
                if (dataList.Count > 0) Debug.Log($"加载成功，共{dataList.Count}条数据");
            }
            catch (System.Exception e) { Debug.LogError($"加载失败: {e.Message}"); dataList = new List<FishData>(); }
        }
        else
        {
            Debug.LogWarning($"文件不存在: {FullPath}，创建空列表");
            dataList = new List<FishData>();
        }
        Repaint();
    }

    private void SaveData()
    {
        string directory = Path.GetDirectoryName(FullPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(FullPath, JsonUtility.ToJson(new FishListWrapper { fishes = dataList }, true));
        AssetDatabase.Refresh();
        Debug.Log($"保存成功: {FullPath}");
    }

    private void AddNewItem()
    {
        int newId = 1001;
        if (dataList.Count > 0)
        {
            int maxId = 0;
            foreach (var item in dataList) if (item.id > maxId) maxId = item.id;
            newId = maxId + 1;
        }

        FishData newFish = new FishData
        {
            id = newId,
            name = "新鱼类",
            description = "描述",
            islandId = 0,
            rarityId = 201,
            preferredIslandIds = new List<int>(),
            preferredTimeIds = new List<int>(),
            preferredBaitIds = new List<int>(),
            preferredWeatherIds = new List<int>(),
            fishSpeciesId = 602,
            struggleTime = 5,
            flashProbability = 0.5f,
            baseWeight = 1.0f,
            baseExp = 10,
            scale = 1.0f
        };

        dataList.Add(newFish);
        selectedIndex = dataList.Count - 1;
        LoadItemToEdit(newFish);
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

        FishData newFish = new FishData
        {
            id = editId,
            name = editName,
            description = editDescription,
            islandId = editIslandId,
            rarityId = editRarityId,
            preferredIslandIds = new List<int>(),
            preferredTimeIds = new List<int>(),
            preferredBaitIds = new List<int>(),
            preferredWeatherIds = new List<int>(),
            fishSpeciesId = editFishSpeciesId,
            struggleTime = editStruggleTime,
            flashProbability = editFlashProbability,
            baseWeight = editBaseWeight,
            baseExp = editBaseExp,
            scale = editScale
        };

        // 解析偏向岛屿
        if (!string.IsNullOrEmpty(editPreferredIslandIds))
        {
            foreach (string idStr in editPreferredIslandIds.Split(','))
            {
                if (int.TryParse(idStr.Trim(), out int id))
                    newFish.preferredIslandIds.Add(id);
            }
        }

        // 解析偏向时间
        if (!string.IsNullOrEmpty(editPreferredTimeIds))
        {
            foreach (string idStr in editPreferredTimeIds.Split(','))
            {
                if (int.TryParse(idStr.Trim(), out int id))
                    newFish.preferredTimeIds.Add(id);
            }
        }

        // 解析偏向鱼饵
        if (!string.IsNullOrEmpty(editPreferredBaitIds))
        {
            foreach (string idStr in editPreferredBaitIds.Split(','))
            {
                if (int.TryParse(idStr.Trim(), out int id))
                    newFish.preferredBaitIds.Add(id);
            }
        }

        // 解析偏向天气
        if (!string.IsNullOrEmpty(editPreferredWeatherIds))
        {
            foreach (string idStr in editPreferredWeatherIds.Split(','))
            {
                if (int.TryParse(idStr.Trim(), out int id))
                    newFish.preferredWeatherIds.Add(id);
            }
        }

        dataList.Add(newFish);

        // 按ID排序
        dataList = dataList.OrderBy(f => f.id).ToList();

        SaveData();
        LoadData();

        // 重置输入
        editId = GetNextAvailableId();
        editName = "";
        editDescription = "";
        editIslandId = 0;
        editRarityId = 201;
        editPreferredIslandIds = "";
        editPreferredTimeIds = "";
        editPreferredBaitIds = "";
        editPreferredWeatherIds = "";
        editFishSpeciesId = 602;
        editStruggleTime = 5;
        editFlashProbability = 0.5f;
        editBaseWeight = 1.0f;
        editBaseExp = 10;
        editScale = 1.0f;

        EditorUtility.DisplayDialog("成功", "新增成功", "确定");
    }

    private int GetNextAvailableId()
    {
        if (dataList.Count == 0) return 1001;
        int maxId = 0;
        foreach (var item in dataList) if (item.id > maxId) maxId = item.id;
        return maxId + 1;
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
