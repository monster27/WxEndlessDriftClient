// ==================== ItemCategoryDataEditor.cs ====================
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

    private const string DATA_PATH = "Resources/JsonData/Game/GameFramework/itemCategories.json";

    [MenuItem("Tools/游戏内容/1.游戏框架数据/物品分类", false, 201)]
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
        EditorGUILayout.LabelField($"  └─ {subCat.name}（{subCat.id}）【{subCat.startId} - {subCat.endId}】", GUILayout.Height(20));
        EditorGUILayout.EndHorizontal();
        
        if (!string.IsNullOrEmpty(subCat.description) && !subCat.description.Contains("预留"))
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

        string helpText = @"（1）A.水产      【1001 - 1999】
（2）B.饵料      鱼饵（21）【2001-2499】- 窝料（22）【2501-2799】
（3）C.装备      钓竿（31）【3001-3099】- 钓线（32）【3101-3199】- 钓钩（33）【3201-3299】- 技能（34）【3301-3399】- 人物（35）【3401-3499】
（4）D.装饰      钓鱼场景装饰（41）【4001-4299】- 帐篷内装饰（43）【4301-4499】- 鱼缸装饰（45）【4501-4599】- 宠物屋装饰（46）【4601-4699】
（5）E.宠物      蛋类（50）【5001-5499】- 已孵化（55）【5501-5899】
（6）F.特殊      垃圾（60）【6001-6299】- 进阶材料（63）【6301-6499】";

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
