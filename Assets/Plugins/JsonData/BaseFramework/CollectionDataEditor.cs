// ==================== CollectionDataEditor.cs ====================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class CollectionDataEditor : EditorWindow
{
    private const string RELATIVE_PATH = "Addressables/JsonData/BaseFramework/collection.json";

    private List<CollectionCategory> categories = new List<CollectionCategory>();
    private Vector2 listScrollPosition = Vector2.zero;
    private Vector2 editScrollPosition = Vector2.zero;

    private int selectedCategoryIndex = -1;
    private int selectedPageIndex = -1;

    private enum EditMode { List, CategoryEdit, PageEdit }
    private EditMode currentMode = EditMode.List;

    // 新增分类字段
    private int newCategoryId = 1;
    private string newCategoryName = "";
    private string newCategoryIcon = "";

    // 新增页面字段
    private int newPageId = 7101;
    private string newPageName = "";

    // 批量添加条目
    private string batchEntryInput = "";

    [MenuItem("Tools/游戏内容/2.物品内部数据(记得编辑通用数据)/7100_图鉴情报")]
    public static void ShowWindow()
    {
        CollectionDataEditor window = GetWindow<CollectionDataEditor>("图鉴数据编辑器");
        window.minSize = new Vector2(1100, 750);
        window.Show();
    }

    private void OnEnable() => LoadData();

    private void OnGUI()
    {
        if (currentMode == EditMode.List)
            DrawListMode();
        else
            DrawEditMode();
    }

    #region List Mode

    private void DrawListMode()
    {
        DrawTopToolbar();
        EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

        EditorGUILayout.BeginVertical(GUILayout.Width(500));
        DrawCategoryList();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
        DrawPageDetailPreview();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        DrawQuickCreateCategory();
    }

    private void DrawTopToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("📚 图鉴编辑器", EditorStyles.boldLabel, GUILayout.Width(120));
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("🔄 刷新", EditorStyles.toolbarButton, GUILayout.Width(70))) LoadData();
        if (GUILayout.Button("➕ 新增分类", EditorStyles.toolbarButton, GUILayout.Width(90))) AddNewCategory();

        int totalPages = categories.Sum(c => c.pages.Count);
        EditorGUILayout.LabelField($"共 {categories.Count} 个分类, {totalPages} 个页面", EditorStyles.toolbarButton, GUILayout.Width(180));
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    private void DrawCategoryList()
    {
        EditorGUILayout.LabelField("分类列表", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));

        listScrollPosition = EditorGUILayout.BeginScrollView(listScrollPosition);

        for (int i = 0; i < categories.Count; i++)
        {
            DrawCategoryItem(i);
        }

        if (categories.Count == 0)
            EditorGUILayout.LabelField("暂无分类，点击\"新增分类\"添加", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(50));

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawCategoryItem(int index)
    {
        CollectionCategory cat = categories[index];
        bool isSelected = selectedCategoryIndex == index;

        EditorGUILayout.BeginHorizontal(isSelected ? "SelectionRect" : "box", GUILayout.Height(28));

        EditorGUILayout.LabelField($"📁", GUILayout.Width(30));
        EditorGUILayout.LabelField($"[{cat.id}]", GUILayout.Width(45));
        EditorGUILayout.LabelField(cat.name, EditorStyles.boldLabel, GUILayout.Width(120));
        EditorGUILayout.LabelField($"页数:{cat.pages.Count}", GUILayout.Width(60));

        EditorGUILayout.BeginHorizontal(GUILayout.Width(100));
        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
        if (GUILayout.Button("编辑", EditorStyles.miniButton, GUILayout.Width(45)))
        {
            selectedCategoryIndex = index;
            currentMode = EditMode.CategoryEdit;
        }
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(45)))
        {
            if (EditorUtility.DisplayDialog("确认删除", $"确定删除分类 [{cat.id}] {cat.name} 及其所有页面吗？", "删除", "取消"))
            {
                categories.RemoveAt(index);
                if (selectedCategoryIndex == index) selectedCategoryIndex = -1;
                SaveData();
                LoadData();
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndHorizontal();

        Rect lastRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && lastRect.Contains(Event.current.mousePosition))
        {
            selectedCategoryIndex = index;
            Event.current.Use();
            Repaint();
        }
    }

    private void DrawPageDetailPreview()
    {
        if (selectedCategoryIndex < 0 || selectedCategoryIndex >= categories.Count)
        {
            EditorGUILayout.LabelField("请从左侧选择一个分类", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        var cat = categories[selectedCategoryIndex];

        EditorGUILayout.LabelField($"📁 [{cat.id}] {cat.name}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"图标: {cat.icon}", EditorStyles.miniLabel);
        GUILayout.Space(10);

        EditorGUILayout.LabelField($"页面列表 (共 {cat.pages.Count} 页)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandHeight(true));

        // 页面列表
        for (int i = 0; i < cat.pages.Count; i++)
        {
            DrawPageItem(cat.pages[i], i);
        }

        // 新增页面
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("新增页面:", GUILayout.Width(60));
        newPageId = EditorGUILayout.IntField(newPageId, GUILayout.Width(60));
        newPageName = EditorGUILayout.TextField(newPageName, GUILayout.Width(120));
        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
        if (GUILayout.Button("添加", GUILayout.Width(50)))
            AddPageToCategory(selectedCategoryIndex);
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // 按岛屿自动添加鱼类按钮（仅鱼类图鉴）
        if (cat.id == 1)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
            if (GUILayout.Button("🐟 按岛屿自动添加鱼类", GUILayout.Width(180)))
            {
                AddFishesByIsland(selectedCategoryIndex);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.LabelField("根据fishes.json中的islandId按岛屿分组添加", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPageItem(CollectionPage page, int index)
    {
        bool isSelected = selectedPageIndex == index;

        EditorGUILayout.BeginHorizontal(isSelected ? "SelectionRect" : "box", GUILayout.Height(24));

        EditorGUILayout.LabelField($"📄", GUILayout.Width(25));
        EditorGUILayout.LabelField($"[{page.id}]", GUILayout.Width(50));
        EditorGUILayout.LabelField(page.pageName, GUILayout.Width(120));
        EditorGUILayout.LabelField($"条目:{page.entries.Count}", GUILayout.Width(60));

        EditorGUILayout.BeginHorizontal(GUILayout.Width(200));
        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
        if (GUILayout.Button("编辑", EditorStyles.miniButton, GUILayout.Width(40)))
        {
            selectedPageIndex = index;
            currentMode = EditMode.PageEdit;
        }
        GUI.backgroundColor = Color.white;

        if (index > 0 && GUILayout.Button("↑", EditorStyles.miniButton, GUILayout.Width(25)))
        {
            var temp = categories[selectedCategoryIndex].pages[index];
            categories[selectedCategoryIndex].pages[index] = categories[selectedCategoryIndex].pages[index - 1];
            categories[selectedCategoryIndex].pages[index - 1] = temp;
            SaveData();
            LoadData();
        }
        if (index < categories[selectedCategoryIndex].pages.Count - 1 && GUILayout.Button("↓", EditorStyles.miniButton, GUILayout.Width(25)))
        {
            var temp = categories[selectedCategoryIndex].pages[index];
            categories[selectedCategoryIndex].pages[index] = categories[selectedCategoryIndex].pages[index + 1];
            categories[selectedCategoryIndex].pages[index + 1] = temp;
            SaveData();
            LoadData();
        }

        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(25)))
        {
            if (EditorUtility.DisplayDialog("确认删除", $"确定删除页面 [{page.id}] {page.pageName} 吗？", "删除", "取消"))
            {
                categories[selectedCategoryIndex].pages.RemoveAt(index);
                if (selectedPageIndex == index) selectedPageIndex = -1;
                SaveData();
                LoadData();
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndHorizontal();

        Rect lastRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && lastRect.Contains(Event.current.mousePosition))
        {
            selectedPageIndex = index;
            Event.current.Use();
            Repaint();
        }
    }

    private void DrawQuickCreateCategory()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("快速新增分类", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ID:", GUILayout.Width(25));
        newCategoryId = EditorGUILayout.IntField(newCategoryId, GUILayout.Width(60));
        EditorGUILayout.LabelField("名称:", GUILayout.Width(30));
        newCategoryName = EditorGUILayout.TextField(newCategoryName, GUILayout.Width(150));
        EditorGUILayout.LabelField("图标:", GUILayout.Width(30));
        newCategoryIcon = EditorGUILayout.TextField(newCategoryIcon, GUILayout.Width(120));
        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
        if (GUILayout.Button("创建分类", GUILayout.Width(80))) AddQuickCategory();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
        EditorGUILayout.HelpBox("提示：双击分类或页面可快速编辑，点击\"编辑\"进入详细编辑模式", MessageType.Info);
    }

    #endregion

    #region Edit Mode

    private void DrawEditMode()
    {
        if (currentMode == EditMode.CategoryEdit)
            DrawCategoryEditMode();
        else if (currentMode == EditMode.PageEdit)
            DrawPageEditMode();
    }

    #region Category Edit Mode

    private void DrawCategoryEditMode()
    {
        if (selectedCategoryIndex < 0 || selectedCategoryIndex >= categories.Count)
        {
            currentMode = EditMode.List;
            return;
        }

        var cat = categories[selectedCategoryIndex];

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f);
        if (GUILayout.Button("← 返回列表", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            currentMode = EditMode.List;
            SaveData();
            Repaint();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.LabelField($"编辑分类: [{cat.id}] {cat.name}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("💾 保存", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            SaveData();
            EditorUtility.DisplayDialog("保存成功", "数据已保存！", "确定");
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);

        editScrollPosition = EditorGUILayout.BeginScrollView(editScrollPosition);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("分类信息", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ID:", GUILayout.Width(60));
        int newId = EditorGUILayout.IntField(cat.id, GUILayout.Width(80));
        if (newId != cat.id && !IsCategoryIdDuplicate(newId, selectedCategoryIndex))
            cat.id = newId;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("名称:", GUILayout.Width(60));
        cat.name = EditorGUILayout.TextField(cat.name);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("图标:", GUILayout.Width(60));
        cat.icon = EditorGUILayout.TextField(cat.icon);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"页面列表 (共 {cat.pages.Count} 页)", EditorStyles.boldLabel);

        for (int i = 0; i < cat.pages.Count; i++)
        {
            DrawPageEditItem(cat.pages[i], i);
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();
    }

    private void DrawPageEditItem(CollectionPage page, int index)
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"页面 {index + 1}", EditorStyles.boldLabel, GUILayout.Width(60));
        EditorGUILayout.LabelField("ID:", GUILayout.Width(25));
        int newId = EditorGUILayout.IntField(page.id, GUILayout.Width(60));
        if (newId != page.id && !IsPageIdDuplicate(selectedCategoryIndex, newId, index))
            page.id = newId;
        EditorGUILayout.LabelField("名称:", GUILayout.Width(30));
        page.pageName = EditorGUILayout.TextField(page.pageName, GUILayout.Width(150));
        EditorGUILayout.LabelField($"条目数: {page.entries.Count}", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        // 奖励列表
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("奖励:", GUILayout.Width(40));
        string rewardStr = "";
        foreach (var r in page.rewards)
            rewardStr += $"{r.percent}%→({r.rewardId}×{r.rewardAmount}) ";
        EditorGUILayout.LabelField(rewardStr, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("编辑奖励", GUILayout.Width(80)))
            OpenRewardEditor(selectedCategoryIndex, index);
        EditorGUILayout.EndHorizontal();

        // 条目列表
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("条目:", GUILayout.Width(40));
        string entryStr = string.Join(", ", page.entries.Take(10));
        if (page.entries.Count > 10) entryStr += "...";
        EditorGUILayout.LabelField(entryStr, GUILayout.ExpandWidth(true));
        if (GUILayout.Button("编辑条目", GUILayout.Width(80)))
            OpenPageEntryEditor(selectedCategoryIndex, index);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    #endregion

    #region Page Edit Mode

    private void DrawPageEditMode()
    {
        if (selectedCategoryIndex < 0 || selectedCategoryIndex >= categories.Count ||
            selectedPageIndex < 0 || selectedPageIndex >= categories[selectedCategoryIndex].pages.Count)
        {
            currentMode = EditMode.List;
            return;
        }

        var cat = categories[selectedCategoryIndex];
        var page = cat.pages[selectedPageIndex];

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f);
        if (GUILayout.Button("← 返回列表", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            currentMode = EditMode.List;
            SaveData();
            Repaint();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.LabelField($"编辑页面: [{page.id}] {page.pageName}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("💾 保存", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            SaveData();
            EditorUtility.DisplayDialog("保存成功", "数据已保存！", "确定");
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);

        editScrollPosition = EditorGUILayout.BeginScrollView(editScrollPosition);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("页面信息", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ID:", GUILayout.Width(60));
        int newId = EditorGUILayout.IntField(page.id, GUILayout.Width(80));
        if (newId != page.id && !IsPageIdDuplicate(selectedCategoryIndex, newId, selectedPageIndex))
            page.id = newId;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("名称:", GUILayout.Width(60));
        page.pageName = EditorGUILayout.TextField(page.pageName);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);

        // 奖励编辑
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("奖励列表 (每10%一个)", EditorStyles.boldLabel);

        for (int i = 0; i < page.rewards.Count; i++)
        {
            var reward = page.rewards[i];
            EditorGUILayout.BeginHorizontal(i % 2 == 0 ? "box" : GUIStyle.none);

            EditorGUILayout.LabelField($"{reward.percent}%", GUILayout.Width(50));
            EditorGUILayout.LabelField("奖励ID:", GUILayout.Width(55));
            reward.rewardId = EditorGUILayout.IntField(reward.rewardId, GUILayout.Width(80));
            EditorGUILayout.LabelField("数量:", GUILayout.Width(35));
            reward.rewardAmount = EditorGUILayout.IntField(reward.rewardAmount, GUILayout.Width(60));

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);

        // 条目编辑
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"条目列表 (共 {page.entries.Count} 个)", EditorStyles.boldLabel);

        // 批量添加
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("批量添加(逗号分隔):", GUILayout.Width(130));
        batchEntryInput = EditorGUILayout.TextField(batchEntryInput, GUILayout.Width(300));
        if (GUILayout.Button("批量添加", GUILayout.Width(80)))
        {
            AddBatchEntries(page);
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);

        // 条目列表
        for (int i = 0; i < page.entries.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(30));
            EditorGUILayout.LabelField(page.entries[i].ToString(), GUILayout.Width(80));

            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("移除", GUILayout.Width(50)))
            {
                page.entries.RemoveAt(i);
                SaveData();
                Repaint();
                break;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        if (page.entries.Count == 0)
            EditorGUILayout.LabelField("暂无条目", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();
    }

    #endregion

    #endregion

    #region Data Operations

    private void LoadData()
    {
        string fullPath = Path.Combine(Application.dataPath, RELATIVE_PATH);
        categories = new List<CollectionCategory>();

        if (File.Exists(fullPath))
        {
            try
            {
                string json = File.ReadAllText(fullPath);
                var wrapper = JsonUtility.FromJson<CollectionWrapper>(json);
                if (wrapper?.collection?.categories != null)
                {
                    categories = wrapper.collection.categories.ToList();
                    foreach (var cat in categories)
                    {
                        foreach (var page in cat.pages)
                        {
                            if (page.rewards == null || page.rewards.Count == 0)
                                page.rewards = CreateDefaultRewards();
                            if (page.entries == null)
                                page.entries = new List<int>();
                        }
                    }
                    Debug.Log($"加载成功，共 {categories.Count} 个分类");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"加载失败: {e.Message}");
                categories = new List<CollectionCategory>();
            }
        }

        if (categories.Count == 0)
            AddDefaultData();

        Repaint();
    }

    private void SaveData()
    {
        string fullPath = Path.Combine(Application.dataPath, RELATIVE_PATH);
        string directory = Path.GetDirectoryName(fullPath);

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var wrapper = new CollectionWrapper
        {
            collection = new CollectionRoot { categories = categories }
        };

        File.WriteAllText(fullPath, JsonUtility.ToJson(wrapper, true));
        AssetDatabase.Refresh();
        Debug.Log($"保存成功: {fullPath}");
    }

    private void AddDefaultData()
    {
        categories = new List<CollectionCategory>
        {
            new CollectionCategory
            {
                id = 1,
                name = "鱼类图鉴",
                icon = "icon_fish",
                pages = new List<CollectionPage>
                {
                    new CollectionPage { id = 7101, pageName = "融冠群岛", rewards = CreateDefaultRewards(), entries = new List<int>{ 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009, 1010, 1011, 1012, 1013, 1014, 1015 } },
                    new CollectionPage { id = 7102, pageName = "珊瑚环心岛", rewards = CreateDefaultRewards(), entries = new List<int>{ 1021, 1022, 1023, 1024 } }
                }
            },
            new CollectionCategory
            {
                id = 2,
                name = "幻鱼图鉴",
                icon = "icon_mythical",
                pages = new List<CollectionPage>()
            },
            new CollectionCategory
            {
                id = 3,
                name = "昆虫图鉴",
                icon = "icon_insect",
                pages = new List<CollectionPage>()
            },
            new CollectionCategory
            {
                id = 4,
                name = "人物图鉴",
                icon = "icon_character",
                pages = new List<CollectionPage>()
            },
            new CollectionCategory
            {
                id = 5,
                name = "宠物图鉴",
                icon = "icon_pet",
                pages = new List<CollectionPage>()
            },
            new CollectionCategory
            {
                id = 6,
                name = "皮肤图鉴",
                icon = "icon_Skin",
                pages = new List<CollectionPage>()
            }
        };

        SaveData();
    }

    private List<Reward> CreateDefaultRewards()
    {
        return new List<Reward>
        {
            new Reward { percent = 10, rewardId = 0, rewardAmount = 0 },
            new Reward { percent = 20, rewardId = 0, rewardAmount = 0 },
            new Reward { percent = 30, rewardId = 0, rewardAmount = 0 },
            new Reward { percent = 40, rewardId = 0, rewardAmount = 0 },
            new Reward { percent = 50, rewardId = 0, rewardAmount = 0 },
            new Reward { percent = 60, rewardId = 0, rewardAmount = 0 },
            new Reward { percent = 70, rewardId = 0, rewardAmount = 0 },
            new Reward { percent = 80, rewardId = 0, rewardAmount = 0 },
            new Reward { percent = 90, rewardId = 0, rewardAmount = 0 },
            new Reward { percent = 100, rewardId = 0, rewardAmount = 0 }
        };
    }

    private void AddNewCategory()
    {
        int newId = categories.Count > 0 ? categories.Max(c => c.id) + 1 : 1;
        categories.Add(new CollectionCategory
        {
            id = newId,
            name = "新分类",
            icon = "icon_default",
            pages = new List<CollectionPage>()
        });
        selectedCategoryIndex = categories.Count - 1;
        SaveData();
        LoadData();
        currentMode = EditMode.CategoryEdit;
    }

    private void AddQuickCategory()
    {
        if (string.IsNullOrEmpty(newCategoryName))
        {
            EditorUtility.DisplayDialog("错误", "名称不能为空", "确定");
            return;
        }
        if (IsCategoryIdDuplicate(newCategoryId, -1))
        {
            EditorUtility.DisplayDialog("错误", $"ID {newCategoryId} 已存在", "确定");
            return;
        }

        categories.Add(new CollectionCategory
        {
            id = newCategoryId,
            name = newCategoryName,
            icon = string.IsNullOrEmpty(newCategoryIcon) ? "icon_default" : newCategoryIcon,
            pages = new List<CollectionPage>()
        });

        categories = categories.OrderBy(c => c.id).ToList();
        SaveData();
        LoadData();

        newCategoryId = categories.Count > 0 ? categories.Max(c => c.id) + 1 : 1;
        newCategoryName = "";
        newCategoryIcon = "";
        EditorUtility.DisplayDialog("成功", "新增分类成功", "确定");
    }

    private void AddPageToCategory(int categoryIndex)
    {
        if (categoryIndex < 0 || categoryIndex >= categories.Count) return;

        if (string.IsNullOrEmpty(newPageName))
        {
            EditorUtility.DisplayDialog("错误", "页面名称不能为空", "确定");
            return;
        }

        if (IsPageIdDuplicate(categoryIndex, newPageId, -1))
        {
            EditorUtility.DisplayDialog("错误", $"页面ID {newPageId} 已存在", "确定");
            return;
        }

        categories[categoryIndex].pages.Add(new CollectionPage
        {
            id = newPageId,
            pageName = newPageName,
            rewards = CreateDefaultRewards(),
            entries = new List<int>()
        });

        SaveData();
        LoadData();

        newPageId = categories.SelectMany(c => c.pages).Max(p => p.id) + 1;
        newPageName = "";
        EditorUtility.DisplayDialog("成功", "新增页面成功", "确定");
    }

    private void AddBatchEntries(CollectionPage page)
    {
        if (string.IsNullOrEmpty(batchEntryInput)) return;

        string[] parts = batchEntryInput.Split(new char[] { ',', '，', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        int addedCount = 0;
        foreach (string part in parts)
        {
            if (int.TryParse(part.Trim(), out int id) && !page.entries.Contains(id))
            {
                page.entries.Add(id);
                addedCount++;
            }
        }
        if (addedCount > 0)
        {
            page.entries.Sort();
            SaveData();
            batchEntryInput = "";
            EditorUtility.DisplayDialog("成功", $"成功添加 {addedCount} 个条目", "确定");
            Repaint();
        }
        else
        {
            EditorUtility.DisplayDialog("提示", "没有有效的条目ID被添加", "确定");
        }
    }

    private void AddFishesByIsland(int categoryIndex)
    {
        string fishesPath = Path.Combine(Application.dataPath, "Addressables/JsonData/Game/BagItem/fishes.json");

        if (!File.Exists(fishesPath))
        {
            EditorUtility.DisplayDialog("错误", $"未找到鱼类数据文件: {fishesPath}", "确定");
            return;
        }

        try
        {
            string json = File.ReadAllText(fishesPath);
            var wrapper = JsonUtility.FromJson<FishWrapper>(json);

            if (wrapper == null || wrapper.fishes == null || wrapper.fishes.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "鱼类数据为空", "确定");
                return;
            }

            var fishesByIsland = wrapper.fishes.GroupBy(f => f.islandId)
                .OrderBy(g => g.Key)
                .ToList();

            if (fishesByIsland.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "未找到任何鱼类数据", "确定");
                return;
            }

            var cat = categories[categoryIndex];
            int newPageId = cat.pages.Count > 0 ? cat.pages.Max(p => p.id) + 1 : 7101;
            int totalAdded = 0;

            foreach (var islandGroup in fishesByIsland)
            {
                int islandId = islandGroup.Key;
                var fishIds = islandGroup.Select(f => f.id).OrderBy(id => id).ToList();

                string pageName = $"岛屿{islandId}鱼类";
                var existingPage = cat.pages.FirstOrDefault(p => p.pageName == pageName);

                if (existingPage != null)
                {
                    foreach (int fishId in fishIds)
                    {
                        if (!existingPage.entries.Contains(fishId))
                        {
                            existingPage.entries.Add(fishId);
                            totalAdded++;
                        }
                    }
                    existingPage.entries.Sort();
                }
                else
                {
                    cat.pages.Add(new CollectionPage
                    {
                        id = newPageId++,
                        pageName = pageName,
                        rewards = CreateDefaultRewards(),
                        entries = fishIds
                    });
                    totalAdded += fishIds.Count;
                }
            }

            cat.pages = cat.pages.OrderBy(p => p.id).ToList();
            SaveData();
            LoadData();
            EditorUtility.DisplayDialog("成功", $"按岛屿分组添加完成！共添加 {totalAdded} 条鱼类，生成 {fishesByIsland.Count} 个页面", "确定");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("错误", $"解析鱼类数据失败: {e.Message}", "确定");
            Debug.LogError($"AddFishesByIsland error: {e}");
        }
    }

    private bool IsCategoryIdDuplicate(int id, int excludeIndex)
    {
        for (int i = 0; i < categories.Count; i++)
        {
            if (i != excludeIndex && categories[i].id == id) return true;
        }
        return false;
    }

    private bool IsPageIdDuplicate(int categoryIndex, int id, int excludePageIndex)
    {
        if (categoryIndex < 0 || categoryIndex >= categories.Count) return false;
        var pages = categories[categoryIndex].pages;
        for (int i = 0; i < pages.Count; i++)
        {
            if (i != excludePageIndex && pages[i].id == id) return true;
        }
        return false;
    }

    #endregion

    #region Editor Windows

    private void OpenPageEntryEditor(int categoryIndex, int pageIndex)
    {
        var page = categories[categoryIndex].pages[pageIndex];
        PageEntryEditorWindow.Open(page, () =>
        {
            SaveData();
            LoadData();
        });
    }

    private void OpenRewardEditor(int categoryIndex, int pageIndex)
    {
        var page = categories[categoryIndex].pages[pageIndex];
        RewardEditorWindow.Open(page, () =>
        {
            SaveData();
            LoadData();
        });
    }

    #endregion

    //#region Data Classes

    //[System.Serializable]
    //public class Reward
    //{
    //    public int percent;
    //    public int rewardId;
    //    public int rewardAmount;
    //}

    //[System.Serializable]
    //public class CollectionPage
    //{
    //    public int id;
    //    public string pageName;
    //    public List<Reward> rewards;
    //    public List<int> entries;
    //}

    //[System.Serializable]
    //public class CollectionCategory
    //{
    //    public int id;
    //    public string name;
    //    public string icon;
    //    public List<CollectionPage> pages;
    //}

    //[System.Serializable]
    //public class CollectionRoot
    //{
    //    public List<CollectionCategory> categories;
    //}

    //[System.Serializable]
    //public class CollectionWrapper
    //{
    //    public CollectionRoot collection;
    //}

    //[System.Serializable]
    //public class FishWrapper
    //{
    //    public List<FishData> fishes;
    //}

    //#endregion
}

// ==================== PageEntryEditorWindow.cs ====================
public class PageEntryEditorWindow : EditorWindow
{
    private CollectionPage targetPage;
    private System.Action onSaveCallback;
    private string entryInput = "";
    private Vector2 scrollPosition;
    private int newEntryId = 0;

    public static void Open(CollectionPage page, System.Action onSave)
    {
        PageEntryEditorWindow window = GetWindow<PageEntryEditorWindow>("编辑条目");
        window.targetPage = page;
        window.onSaveCallback = onSave;
        window.minSize = new Vector2(450, 500);
        window.Show();
    }

    private void OnGUI()
    {
        if (targetPage == null)
        {
            EditorGUILayout.LabelField("数据丢失，请关闭重新打开", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        EditorGUILayout.LabelField($"编辑页面: [{targetPage.id}] {targetPage.pageName}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"当前条目数: {targetPage.entries.Count}", EditorStyles.miniLabel);
        GUILayout.Space(10);

        // 新增条目
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("新增条目ID:", GUILayout.Width(80));
        newEntryId = EditorGUILayout.IntField(newEntryId, GUILayout.Width(80));
        if (GUILayout.Button("添加", GUILayout.Width(50)))
        {
            if (newEntryId > 0 && !targetPage.entries.Contains(newEntryId))
            {
                targetPage.entries.Add(newEntryId);
                targetPage.entries.Sort();
                onSaveCallback?.Invoke();
                Repaint();
            }
            else if (targetPage.entries.Contains(newEntryId))
            {
                EditorUtility.DisplayDialog("错误", $"条目ID {newEntryId} 已存在", "确定");
            }
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);

        // 批量添加
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("批量添加(逗号分隔):", GUILayout.Width(120));
        entryInput = EditorGUILayout.TextField(entryInput, GUILayout.Width(200));
        if (GUILayout.Button("批量添加", GUILayout.Width(80)))
        {
            AddBatchEntries();
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);

        // 条目列表
        EditorGUILayout.LabelField("条目列表:", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box", GUILayout.Height(300));
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < targetPage.entries.Count; i++)
        {
            int entryId = targetPage.entries[i];
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(30));
            EditorGUILayout.LabelField(entryId.ToString(), GUILayout.Width(80));

            if (GUILayout.Button("移除", GUILayout.Width(50)))
            {
                targetPage.entries.RemoveAt(i);
                onSaveCallback?.Invoke();
                Repaint();
                break;
            }

            EditorGUILayout.EndHorizontal();
        }
        if (targetPage.entries.Count == 0)
            EditorGUILayout.LabelField("暂无条目", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("保存并关闭", GUILayout.Height(30)))
        {
            onSaveCallback?.Invoke();
            Close();
        }
        if (GUILayout.Button("取消", GUILayout.Height(30)))
            Close();
        EditorGUILayout.EndHorizontal();
    }

    private void AddBatchEntries()
    {
        if (string.IsNullOrEmpty(entryInput)) return;

        string[] parts = entryInput.Split(new char[] { ',', '，', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        int addedCount = 0;
        foreach (string part in parts)
        {
            if (int.TryParse(part.Trim(), out int id) && !targetPage.entries.Contains(id))
            {
                targetPage.entries.Add(id);
                addedCount++;
            }
        }
        if (addedCount > 0)
        {
            targetPage.entries.Sort();
            onSaveCallback?.Invoke();
            entryInput = "";
            EditorUtility.DisplayDialog("成功", $"成功添加 {addedCount} 个条目", "确定");
            Repaint();
        }
        else
        {
            EditorUtility.DisplayDialog("提示", "没有有效的条目ID被添加", "确定");
        }
    }
}

// ==================== RewardEditorWindow.cs ====================
public class RewardEditorWindow : EditorWindow
{
    private CollectionPage targetPage;
    private System.Action onSaveCallback;
    private Vector2 scrollPosition;

    public static void Open(CollectionPage page, System.Action onSave)
    {
        RewardEditorWindow window = GetWindow<RewardEditorWindow>("编辑奖励");
        window.targetPage = page;
        window.onSaveCallback = onSave;
        window.minSize = new Vector2(500, 500);
        window.Show();
    }

    private void OnGUI()
    {
        if (targetPage == null)
        {
            EditorGUILayout.LabelField("数据丢失，请关闭重新打开", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        EditorGUILayout.LabelField($"编辑奖励: [{targetPage.id}] {targetPage.pageName}", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.LabelField("奖励列表 (每10%一个奖励)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box", GUILayout.Height(380));
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        int[] percents = { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

        for (int i = 0; i < targetPage.rewards.Count && i < percents.Length; i++)
        {
            var reward = targetPage.rewards[i];
            int percent = percents[i];

            EditorGUILayout.BeginHorizontal(i % 2 == 0 ? "box" : GUIStyle.none);

            EditorGUILayout.LabelField($"{percent}%", GUILayout.Width(50));
            EditorGUILayout.LabelField("奖励ID:", GUILayout.Width(55));
            reward.rewardId = EditorGUILayout.IntField(reward.rewardId, GUILayout.Width(80));
            EditorGUILayout.LabelField("数量:", GUILayout.Width(35));
            reward.rewardAmount = EditorGUILayout.IntField(reward.rewardAmount, GUILayout.Width(60));

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("保存并关闭", GUILayout.Height(30)))
        {
            onSaveCallback?.Invoke();
            Close();
        }
        if (GUILayout.Button("重置所有奖励为0", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("确认重置", "确定要将所有奖励重置为0吗？", "确定", "取消"))
            {
                foreach (var r in targetPage.rewards)
                {
                    r.rewardId = 0;
                    r.rewardAmount = 0;
                }
                onSaveCallback?.Invoke();
                Repaint();
            }
        }
        if (GUILayout.Button("取消", GUILayout.Height(30)))
            Close();
        EditorGUILayout.EndHorizontal();
    }
}
#endif
