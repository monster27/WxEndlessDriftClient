#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

/// <summary>
/// 单一钓鱼技能编辑器（ID范围：701-799）
/// 单一技能是构成钓鱼能力的基础单元，可以被完整钓鱼技能挂载和组合
/// 每个单一技能代表一个独立的钓鱼效果参数
/// </summary>
public class AbilityDataJsonEditor : BaseDataEditor<AbilityData>
{
    private int selectedIndex = -1;
    private int editId = 701;
    private string editName = "";
    private string editDescription = "";
    private string editAbilityType = "RarityWeight";
    private int editTargetRarityId = 0;

    private const string RELATIVE_PATH = "Resources/JsonData/Ability/abilities.json";

    // 表头宽度
    private float col1 = 50;    // ID
    private float col2 = 120;   // 名称（增加宽度）
    private float col3 = 100;   // 类型（增加宽度）
    private float col4 = 60;    // 数值
    private float col5 = 280;   // 描述（增加宽度）

    // 列表高度（可拖拽调整，支持自动拉伸）
    private float listHeight = 280f;
    private bool isDraggingHeight = false;
    private Vector2 lastMousePosition;
    private bool autoResize = true;  // 是否自动调整高度

    // 能力类型选项
    private string[] abilityTypes = new string[] 
    { 
        "RarityWeight",        // 稀有度权重加成
        "WeightBias",          // 重量倾向调整
        "StruggleTime",        // 挣扎时间减少
        "CatchRate",           // 咬钩概率加成
        "FishWeight",          // 鱼类权重加成
        "ShinyRate",           // 闪光率加成
        "MinigameDifficulty",  // 小游戏难度降低
        "TrashProtection"      // 钓鱼保底（连续钓到垃圾次数上限）
    };

    // 稀有度选项（普通、罕见、稀有、史诗、传说、幻想）
    private string[] rarityOptions = new string[] 
    { 
        "无", 
        "普通(1)", 
        "罕见(2)", 
        "稀有(3)", 
        "史诗(4)", 
        "传说(5)",
        "幻想(6)" 
    };

    public AbilityDataJsonEditor() : base(RELATIVE_PATH) { }

    [MenuItem("Tools/基础框架/701_单一钓鱼技能（需挂载）")]
    public static void ShowWindow() => GetWindow<AbilityDataJsonEditor>("单一钓鱼技能编辑器").minSize = new Vector2(600, 700);

    private void OnEnable() => LoadData();

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        
        DrawToolbar();
        DrawDataList();
        DrawEditPanel();
        DrawBottomButtons();
        
        EditorGUILayout.EndVertical();
        
        // 在布局完成后处理拖拽事件
        HandleHeightDrag();
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
        // 自动调整列表高度以适应窗口大小
        if (autoResize && !isDraggingHeight)
        {
            // 计算可用空间：窗口高度 - 工具栏 - 编辑区域 - 底部区域 - 边距
            float availableHeight = position.height - 50 - 250 - 100;
            listHeight = Mathf.Clamp(availableHeight, 100f, 600f);
        }

        EditorGUILayout.LabelField("单一钓鱼技能列表（拖拽下方手柄调整高度）", EditorStyles.boldLabel);

        // ==================== 表头 ====================
        EditorGUILayout.BeginHorizontal("box");

        DrawResizableColumn("ID", ref col1, "col1");
        DrawResizableColumn("名称", ref col2, "col2");
        DrawResizableColumn("类型", ref col3, "col3");
        DrawResizableColumn("数值", ref col4, "col4");
        DrawResizableColumn("描述", ref col5, "col5");

        EditorGUILayout.EndHorizontal();

        // ==================== 数据行 ====================
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(listHeight), GUILayout.ExpandWidth(true));

        for (int i = 0; i < dataList.Count; i++) DrawListItem(i);
        if (dataList.Count == 0) EditorGUILayout.LabelField("暂无数据，点击\"新增\"添加", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // 绘制高度调整手柄
        DrawResizeHandle();
        GUILayout.Space(10);
    }

    private void DrawResizeHandle()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = isDraggingHeight ? Color.cyan : Color.gray;
        GUILayout.Button("⋮⋮⋮", EditorStyles.helpBox, GUILayout.Width(60), GUILayout.Height(8));
        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void HandleHeightDrag()
    {
        Event e = Event.current;
        Rect handleRect = GUILayoutUtility.GetLastRect();
        handleRect.y += handleRect.height;
        handleRect.height = 10;

        if (e.type == EventType.MouseDown && handleRect.Contains(e.mousePosition))
        {
            isDraggingHeight = true;
            lastMousePosition = e.mousePosition;
            e.Use();
        }
        else if (e.type == EventType.MouseUp && isDraggingHeight)
        {
            isDraggingHeight = false;
            e.Use();
        }
        else if (isDraggingHeight && e.type == EventType.MouseDrag)
        {
            float delta = e.mousePosition.y - lastMousePosition.y;
            listHeight = Mathf.Clamp(listHeight + delta, 100f, 600f);
            lastMousePosition = e.mousePosition;
            Repaint();
        }
    }

    private void DrawListItem(int index)
    {
        AbilityData item = dataList[index];
        EditorGUILayout.BeginHorizontal();

        if (selectedIndex == index) GUI.backgroundColor = Color.cyan;
        EditorGUILayout.LabelField($"[{item.id}]", GUILayout.Width(col1));
        EditorGUILayout.LabelField(item.name, GUILayout.Width(col2));
        EditorGUILayout.LabelField(item.abilityType, GUILayout.Width(col3));
        
        // 目标稀有度（仅RarityWeight类型显示）
        if (item.abilityType == "RarityWeight" && item.targetRarityId > 0)
        {
            EditorGUILayout.LabelField($"稀有度{item.targetRarityId}", GUILayout.Width(col4));
        }
        else
        {
            GUILayout.Space(col4);
        }
        
        // 描述列，超长省略（根据列宽调整）
        string shortDesc = item.description;
        if (shortDesc != null && shortDesc.Length > 40) shortDesc = shortDesc.Substring(0, 37) + "...";
        EditorGUILayout.LabelField(shortDesc ?? "", GUILayout.Width(col5));
        
        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("编辑", GUILayout.Width(50))) selectedIndex = index;

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("删除", GUILayout.Width(50)) && EditorUtility.DisplayDialog("确认删除", $"确定要删除能力 [{item.id}] {item.name} 吗？", "删除", "取消"))
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
            AbilityData item = dataList[selectedIndex];
            EditorGUILayout.LabelField($"正在编辑: [{item.id}] {item.name}", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // ID
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ID:", GUILayout.Width(80));
            int newId = EditorGUILayout.IntField(item.id);
            if (newId != item.id && !IsIdDuplicate(newId, selectedIndex)) item.id = newId;
            else if (newId != item.id) EditorUtility.DisplayDialog("错误", $"ID {newId} 已存在", "确定");
            EditorGUILayout.EndHorizontal();

            // 名称
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("名称:", GUILayout.Width(80));
            item.name = EditorGUILayout.TextField(item.name);
            EditorGUILayout.EndHorizontal();

            // 描述
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("描述:", GUILayout.Width(80));
            item.description = EditorGUILayout.TextField(item.description);
            EditorGUILayout.EndHorizontal();

            // 能力类型
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("能力类型:", GUILayout.Width(80));
            int typeIndex = EditorGUILayout.Popup(Array.IndexOf(abilityTypes, item.abilityType), abilityTypes);
            if (typeIndex >= 0) item.abilityType = abilityTypes[typeIndex];
            EditorGUILayout.EndHorizontal();

            // 目标稀有度
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标稀有度:", GUILayout.Width(80));
            int rarityIndex = EditorGUILayout.Popup(item.targetRarityId, rarityOptions);
            item.targetRarityId = rarityIndex;
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
        EditorGUILayout.LabelField("快速新增单一钓鱼技能:", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ID:", GUILayout.Width(30));
        editId = EditorGUILayout.IntField(editId, GUILayout.Width(60));
        EditorGUILayout.LabelField("名称:", GUILayout.Width(40));
        editName = EditorGUILayout.TextField(editName, GUILayout.Width(120));
        EditorGUILayout.LabelField("类型:", GUILayout.Width(40));
        int quickTypeIndex = EditorGUILayout.Popup(Array.IndexOf(abilityTypes, editAbilityType), abilityTypes, GUILayout.Width(100));
        if (quickTypeIndex >= 0) editAbilityType = abilityTypes[quickTypeIndex];
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("目标稀有度:", GUILayout.Width(80));
        int quickRarityIndex = EditorGUILayout.Popup(editTargetRarityId, rarityOptions, GUILayout.Width(100));
        editTargetRarityId = quickRarityIndex;
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("新增", GUILayout.Width(80))) AddQuickItem();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);
        EditorGUILayout.HelpBox("=== 单一钓鱼技能说明 ===\n\n" +
            "单一钓鱼技能（ID范围：701-799）是构成钓鱼能力系统的基础单元。\n\n" +
            "【用途】\n" +
            "- 单个技能代表一个独立的钓鱼效果参数\n" +
            "- 需挂载到完整钓鱼技能（801-899）中才能生效\n" +
            "- 支持自由组合，形成多样化的技能套装\n\n" +
            "【技能类型】\n" +
            "- RarityWeight: 稀有度权重加成（value=加成值, targetRarityId=目标稀有度1-6）\n" +
            "- WeightBias: 重量倾向调整（value=倾向系数，越小越偏基础重量）\n" +
            "- StruggleTime: 挣扎时间减少（value=减少百分比）\n" +
            "- CatchRate: 咬钩概率加成（value=加成百分比）\n" +
            "- FishWeight: 鱼类权重加成（value=权重倍率）\n" +
            "- ShinyRate: 闪光率加成（value=加成百分比）\n" +
            "- MinigameDifficulty: 小游戏难度降低（value=降低等级数）\n" +
            "- TrashProtection: 钓鱼保底（value=连续垃圾次数上限，超过后必定钓非垃圾）\n\n" +
            "【稀有度对应】\n" +
            "1=普通 | 2=罕见 | 3=稀有 | 4=史诗 | 5=传说 | 6=幻想", MessageType.Info);
    }

    private void LoadData()
    {
        if (File.Exists(FullPath))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<AbilityListWrapper>(File.ReadAllText(FullPath));
                dataList = wrapper?.abilities ?? new List<AbilityData>();
                if (dataList.Count > 0) Debug.Log($"[AbilityDataJsonEditor] 加载成功，共{dataList.Count}条数据");
            }
            catch (System.Exception ex) { Debug.LogError($"[AbilityDataJsonEditor] 加载失败: {ex.Message}"); dataList = new List<AbilityData>(); }
        }
        else
        {
            Debug.LogWarning($"[AbilityDataJsonEditor] 文件不存在: {FullPath}，创建空列表");
            dataList = new List<AbilityData>();
        }
        Repaint();
    }

    private void SaveData()
    {
        string directory = Path.GetDirectoryName(FullPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        File.WriteAllText(FullPath, JsonUtility.ToJson(new AbilityListWrapper { abilities = dataList }, true));
        AssetDatabase.Refresh();
        Debug.Log($"[AbilityDataJsonEditor] 保存成功: {FullPath}");
    }

    private void AddNewItem()
    {
        int newId = 701;
        if (dataList.Count > 0)
        {
            int maxId = 700;
            foreach (var ability in dataList)
            {
                if (ability.id > maxId) maxId = ability.id;
            }
            newId = maxId + 1;
        }

        dataList.Add(new AbilityData
        { 
            id = newId, 
            name = "新元参数", 
            description = "元参数描述",
            abilityType = "RarityWeight",
            targetRarityId = 0
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

        string description = GetAbilityDescription(editAbilityType, editTargetRarityId);

        dataList.Add(new AbilityData
        {
            id = editId,
            name = editName,
            description = description,
            abilityType = editAbilityType,
            targetRarityId = editTargetRarityId
        });

        dataList = dataList.OrderBy(item => item.id).ToList();

        SaveData();
        LoadData();

        int nextId = 701;
        if (dataList.Count > 0)
        {
            int maxId = 700;
            foreach (var ability in dataList)
            {
                if (ability.id > maxId) maxId = ability.id;
            }
            nextId = maxId + 1;
        }
        editId = nextId;
        editName = "";

        EditorUtility.DisplayDialog("成功", "新增成功", "确定");
    }

    private string GetAbilityDescription(string type, int rarityId)
    {
        string[] rarityNames = { "无", "普通", "罕见", "稀有", "史诗", "传说", "幻想" };
        switch (type)
        {
            case "RarityWeight":
                string rarityName = rarityId >= 0 && rarityId < rarityNames.Length ? rarityNames[rarityId] : "未知";
                return $"增加{rarityName}鱼类的权重（具体数值由完整钓鱼技能等级配置）";
            case "WeightBias":
                return "调整重量随机偏向（具体数值由完整钓鱼技能等级配置）";
            case "StruggleTime":
                return "减少鱼类挣扎时间（具体数值由完整钓鱼技能等级配置）";
            case "CatchRate":
                return "增加鱼类咬钩概率（具体数值由完整钓鱼技能等级配置）";
            case "FishWeight":
                return "增加鱼类权重（具体数值由完整钓鱼技能等级配置）";
            case "ShinyRate":
                return "增加闪光鱼概率（具体数值由完整钓鱼技能等级配置）";
            case "MinigameDifficulty":
                return "降低小游戏难度（具体数值由完整钓鱼技能等级配置）";
            case "TrashProtection":
                return "钓鱼保底机制（具体数值由完整钓鱼技能等级配置）";
            default:
                return "未知能力类型";
        }
    }

    private bool IsIdDuplicate(int id, int excludeIndex)
    {
        for (int i = 0; i < dataList.Count; i++)
        {
            if (i != excludeIndex && dataList[i].id == id)
            {
                return true;
            }
        }
        return false;
    }
}
#endif