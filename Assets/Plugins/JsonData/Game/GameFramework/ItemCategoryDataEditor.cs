#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class ItemCategoryDataEditor : EditorWindow
{
    private Vector2 scrollPosition;
    private ItemCategoryListWrapper categoryWrapper;
    private bool isLoaded = false;

    private const string DATA_PATH = "Addressables/JsonData/Game/GameFramework/itemCategories.json";

    [MenuItem("Tools/游戏内容/1.游戏框架数据/物品分类", false)]
    public static void ShowWindow()
    {
        ItemCategoryDataEditor window = GetWindow<ItemCategoryDataEditor>("物品分类框架");
        window.minSize = new Vector2(750, 600);
        window.Show();
    }

    private void OnEnable()
    {
        LoadData();
    }

    private void OnGUI()
    {
        DrawHeader();
        DrawCategoryTree();
        DrawHelpInfo();
        DrawNotes();
    }

    private void LoadData()
    {
        string fullPath = Path.Combine(Application.dataPath, DATA_PATH);

        if (File.Exists(fullPath))
        {
            try
            {
                string jsonContent = File.ReadAllText(fullPath);
                categoryWrapper = JsonUtility.FromJson<ItemCategoryListWrapper>(jsonContent);
                isLoaded = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"加载物品分类数据失败: {e.Message}");
                isLoaded = false;
            }
        }
        else
        {
            Debug.LogWarning($"物品分类数据文件不存在: {fullPath}");
            isLoaded = false;
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("物品分类框架", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("此框架数据用于系统解析，仅作查看用途", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    private void DrawCategoryTree()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("=== 物品分类体系 ===", EditorStyles.boldLabel);
        GUILayout.Space(5);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (isLoaded && categoryWrapper?.categories != null)
        {
            foreach (CategoryData category in categoryWrapper.categories)
            {
                DrawMainCategory(category);
            }
        }
        else
        {
            EditorGUILayout.LabelField("数据加载失败", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawMainCategory(CategoryData category)
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.85f, 0.9f, 1f);
        EditorGUILayout.LabelField($"（{category.id}）{category.code}.{category.name} 【ID范围: {category.startId} - {category.endId}】", EditorStyles.boldLabel, GUILayout.Height(25));
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(category.description))
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"说明: {category.description}", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }

        if (category.subCategories != null && category.subCategories.Count > 0)
        {
            EditorGUILayout.BeginVertical();
            EditorGUI.indentLevel++;

            foreach (SubCategoryData subCat in category.subCategories)
            {
                DrawSubCategory(subCat);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(3);
    }

    private void DrawSubCategory(SubCategoryData subCat)
    {
        EditorGUILayout.BeginHorizontal();

        // 根据ID显示不同的图标
        string icon = subCat.id switch
        {
            71 => "📖",  // 图鉴情报
            72 => "🏝️",  // 岛屿情报
            _ => "📄"
        };

        EditorGUILayout.LabelField($"  {icon} {subCat.name}（{subCat.id}）【{subCat.startId} - {subCat.endId}】", GUILayout.Height(20));
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(subCat.description))
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"      说明: {subCat.description}", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }
    }

    private void DrawHelpInfo()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("=== 分类规则 ===", EditorStyles.boldLabel);

        string helpText = @"（1）A.水产      【1001 - 1999】（分类ID: 11）
（2）B.饵料      鱼饵（21）【2001-2499】- 窝料（22）【2501-2799】
（3）C.装备      钓竿（31）【3001-3099】- 钓线（32）【3101-3199】- 钓钩（33）【3201-3299】- 技能一（34）【3301-3399】- 技能二（35）【3401-3499】- 人物（36）【3501-3599】
（4）D.室外装饰皮肤  鱼篓装饰（41）【4001-4099】- 帐篷装饰（42）【4101-4199】- 提示器装饰（43）【4201-4299】
（5）E.室内装饰皮肤  墙壁（51）【5000-5049】- 地板（52）【5050-5099】- 楼梯（53）【5100-5149】- 灯带（54）【5150-5199】- 挂饰（55）【5200-5249】- 望远镜（56）【5250-5299】- 昆虫房（57）【5300-5349】- 宠物屋（58）【5350-5399】- 鱼缸（59）【5400-5449】- 熊猫（60）【5450-5499】- 鹦鹉（61）【5500-5549】- 桌子（62）【5550-5599】
（6）G.情报      岛屿情报（70）【7001-7099】- 图鉴情报（71）【7101-7199】
（7）I.垃圾      【9001 - 9020】
（8）S.特殊      不在其他分类范围内的物品（分类ID: 99）";

        EditorGUILayout.HelpBox(helpText, MessageType.Info);
        EditorGUILayout.EndVertical();
    }

    private void DrawNotes()
    {
        if (!isLoaded || categoryWrapper?.notes == null || categoryWrapper.notes.Count == 0)
            return;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("=== 备注说明 ===", EditorStyles.boldLabel);

        foreach (string note in categoryWrapper.notes)
        {
            EditorGUILayout.HelpBox(note, MessageType.Info);
        }

        EditorGUILayout.EndVertical();
    }
}

#endif
