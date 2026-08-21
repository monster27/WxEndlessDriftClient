#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// 鱼缸配置编辑器
/// </summary>
public class FishTankConfigEditor : EditorWindow
{
    private List<FishTankData> fishTanks = new List<FishTankData>();
    private List<FishTankLevelData> fishTankLevels = new List<FishTankLevelData>();
    private string savePath = "Assets/Addressables/JsonData/BaseFramework/fish_tank_config.json";
    private Vector2 scrollPosition;
    private Vector2 levelScrollPosition;

    private readonly string[] typeOptions = { "普通", "特殊" };
    private readonly string[] typeValues = { "normal", "special" };

    [MenuItem("Tools/基础框架/鱼缸配置编辑器")]
    public static void ShowWindow()
    {
        FishTankConfigEditor window = GetWindow<FishTankConfigEditor>("鱼缸配置编辑器");
        window.minSize = new Vector2(500, 700);
        window.Show();
    }

    private void OnEnable()
    {
        LoadData();
    }

    private void LoadData()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            json = RemoveJsonComments(json);

            FishTankConfigWrapper wrapper = JsonUtility.FromJson<FishTankConfigWrapper>(json);

            if (wrapper != null)
            {
                fishTanks = wrapper.fishTanks ?? new List<FishTankData>();
                fishTankLevels = wrapper.fishTankLevels ?? new List<FishTankLevelData>();
                Z_Logger.Log("加载鱼缸配置成功");
            }
            else
            {
                ResetToDefault();
            }
        }
        else
        {
            ResetToDefault();
        }
    }

    private void ResetToDefault()
    {
        // 默认鱼缸数据
        fishTanks = new List<FishTankData>
        {
            new FishTankData { id = 1, name = "特殊鱼缸", type = "special", purchaseCost = 0 },
            new FishTankData { id = 2, name = "普通鱼缸1号", type = "normal", purchaseCost = 1000 },
            new FishTankData { id = 3, name = "普通鱼缸2号", type = "normal", purchaseCost = 2000 },
            new FishTankData { id = 4, name = "普通鱼缸3号", type = "normal", purchaseCost = 4000 },
            new FishTankData { id = 5, name = "普通鱼缸4号", type = "normal", purchaseCost = 8000 }
        };

        // 默认等级数据（5级）
        fishTankLevels = new List<FishTankLevelData>
        {
            new FishTankLevelData { level = 1, maxCount = 10, upgradeCost = 1000, bonus = 0.02f },
            new FishTankLevelData { level = 2, maxCount = 20, upgradeCost = 2000, bonus = 0.04f },
            new FishTankLevelData { level = 3, maxCount = 30, upgradeCost = 4000, bonus = 0.06f },
            new FishTankLevelData { level = 4, maxCount = 40, upgradeCost = 8000, bonus = 0.08f },
            new FishTankLevelData { level = 5, maxCount = 50, upgradeCost = 16000, bonus = 0.10f }
        };
    }

    private string RemoveJsonComments(string json)
    {
        json = System.Text.RegularExpressions.Regex.Replace(json, @"//.*", string.Empty);
        json = System.Text.RegularExpressions.Regex.Replace(json, @"/\*[\s\S]*?\*/", string.Empty);
        return json;
    }

    private void SaveData()
    {
        FishTankConfigWrapper wrapper = new FishTankConfigWrapper
        {
            fishTanks = fishTanks,
            fishTankLevels = fishTankLevels
        };

        string json = JsonUtility.ToJson(wrapper, true);

        string directory = Path.GetDirectoryName(savePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(savePath, json);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("保存成功", $"配置已保存到\n{savePath}", "确定");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("鱼缸配置编辑器", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        DrawToolbar();
        EditorGUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawFishTankSection();
        EditorGUILayout.Space(15);
        DrawFishTankLevelSection();

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);
        DrawHelpBox();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("保存配置", GUILayout.Height(30)))
        {
            SaveData();
        }

        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("刷新数据", GUILayout.Height(30)))
        {
            LoadData();
            Repaint();
        }

        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("重置默认", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("确认重置", "确定要重置为默认配置吗？", "确定", "取消"))
            {
                ResetToDefault();
                Repaint();
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawFishTankSection()
    {
        EditorGUILayout.LabelField("鱼缸列表", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginVertical("box");

        // 表头
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ID", GUILayout.Width(40));
        EditorGUILayout.LabelField("名称", GUILayout.Width(120));
        EditorGUILayout.LabelField("类型", GUILayout.Width(80));
        EditorGUILayout.LabelField("购买价格", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < fishTanks.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            // ID（只读）
            EditorGUILayout.LabelField(fishTanks[i].id.ToString(), GUILayout.Width(40));

            // 名称
            string newName = EditorGUILayout.TextField(fishTanks[i].name, GUILayout.Width(120));
            if (newName != fishTanks[i].name)
            {
                fishTanks[i].name = newName;
            }

            // 类型（下拉选择）
            int currentTypeIndex = System.Array.IndexOf(typeValues, fishTanks[i].type);
            if (currentTypeIndex < 0) currentTypeIndex = 0;
            int newTypeIndex = EditorGUILayout.Popup(currentTypeIndex, typeOptions, GUILayout.Width(80));
            if (newTypeIndex != currentTypeIndex)
            {
                fishTanks[i].type = typeValues[newTypeIndex];
            }

            // 购买价格
            int newCost = EditorGUILayout.IntField(fishTanks[i].purchaseCost, GUILayout.Width(80));
            if (newCost != fishTanks[i].purchaseCost)
            {
                fishTanks[i].purchaseCost = Mathf.Max(0, newCost);
            }

            // 删除按钮
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                if (EditorUtility.DisplayDialog("确认删除", $"确定要删除鱼缸 \"{fishTanks[i].name}\" 吗？", "确定", "取消"))
                {
                    fishTanks.RemoveAt(i);
                    i--;
                    GUI.backgroundColor = Color.white;
                    continue;
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        // 添加按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 添加鱼缸"))
        {
            int newId = fishTanks.Count > 0 ? fishTanks[fishTanks.Count - 1].id + 1 : 1;
            fishTanks.Add(new FishTankData
            {
                id = newId,
                name = $"鱼缸{newId}号",
                type = "normal",
                purchaseCost = 100 * newId
            });
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawFishTankLevelSection()
    {
        EditorGUILayout.LabelField("等级配置", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginVertical("box");

        // 表头
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("等级", GUILayout.Width(60));
        EditorGUILayout.LabelField("存储上限", GUILayout.Width(80));
        EditorGUILayout.LabelField("升级费用", GUILayout.Width(80));
        EditorGUILayout.LabelField("加成", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        levelScrollPosition = EditorGUILayout.BeginScrollView(levelScrollPosition, GUILayout.Height(200));

        for (int i = 0; i < fishTankLevels.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            // 等级（只读）
            EditorGUILayout.LabelField(fishTankLevels[i].level.ToString(), GUILayout.Width(60));

            // 存储上限
            int newMaxCount = EditorGUILayout.IntField(fishTankLevels[i].maxCount, GUILayout.Width(80));
            if (newMaxCount != fishTankLevels[i].maxCount)
            {
                fishTankLevels[i].maxCount = Mathf.Max(1, newMaxCount);
            }

            // 升级费用
            int newCost = EditorGUILayout.IntField(fishTankLevels[i].upgradeCost, GUILayout.Width(80));
            if (newCost != fishTankLevels[i].upgradeCost)
            {
                fishTankLevels[i].upgradeCost = Mathf.Max(0, newCost);
            }

            // 加成（显示为百分比）
            float newBonus = EditorGUILayout.FloatField(fishTankLevels[i].bonus * 100, GUILayout.Width(80)) / 100f;
            if (newBonus != fishTankLevels[i].bonus)
            {
                fishTankLevels[i].bonus = Mathf.Clamp(newBonus, 0f, 1f);
            }

            // 删除按钮
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                if (EditorUtility.DisplayDialog("确认删除", $"确定要删除等级 {fishTankLevels[i].level} 吗？", "确定", "取消"))
                {
                    fishTankLevels.RemoveAt(i);
                    i--;
                    GUI.backgroundColor = Color.white;
                    continue;
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        // 添加按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 添加等级"))
        {
            int newLevel = fishTankLevels.Count > 0 ? fishTankLevels[fishTankLevels.Count - 1].level + 1 : 1;
            int prevMaxCount = fishTankLevels.Count > 0 ? fishTankLevels[fishTankLevels.Count - 1].maxCount : 0;
            int prevCost = fishTankLevels.Count > 0 ? fishTankLevels[fishTankLevels.Count - 1].upgradeCost : 0;
            float prevBonus = fishTankLevels.Count > 0 ? fishTankLevels[fishTankLevels.Count - 1].bonus : 0;

            fishTankLevels.Add(new FishTankLevelData
            {
                level = newLevel,
                maxCount = prevMaxCount + 10,
                upgradeCost = prevCost == 0 ? 1000 : prevCost * 2,
                bonus = prevBonus + 0.02f
            });
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawHelpBox()
    {
        EditorGUILayout.HelpBox(
            $"配置文件路径: {savePath}\n" +
            $"鱼缸数量: {fishTanks.Count} 个\n" +
            $"等级数量: {fishTankLevels.Count} 级\n" +
            $"特殊鱼缸购买价格为0，普通鱼缸需要金币购买\n" +
            $"加成: 0.02 = 2%，最高 0.10 = 10%",
            MessageType.Info);
    }
}

// ==================== 数据结构 ====================

[System.Serializable]
public class FishTankData
{
    public int id;
    public string name;
    public string type;      // normal / special
    public int purchaseCost;
}

[System.Serializable]
public class FishTankLevelData
{
    public int level;
    public int maxCount;
    public int upgradeCost;
    public float bonus;
}

[System.Serializable]
public class FishTankConfigWrapper
{
    public List<FishTankData> fishTanks = new List<FishTankData>();
    public List<FishTankLevelData> fishTankLevels = new List<FishTankLevelData>();
}
#endif
