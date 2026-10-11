#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

// ==================== 消消乐配置编辑器 ====================
public class GameMatch3ConfigEditor : EditorWindow
{
    private GameMatch3ConfigWrapper config = new GameMatch3ConfigWrapper();

    private string savePath = "Assets/Addressables/JsonData/BaseFramework/gameMatch3Config.json";
    private Vector2 scrollPosition;

    [MenuItem("Tools/基础框架/消消乐配置编辑器")]
    public static void ShowWindow()
    {
        GameMatch3ConfigEditor window = GetWindow<GameMatch3ConfigEditor>("消消乐配置编辑器");
        window.minSize = new Vector2(520, 720);
        window.Show();
    }

    private void OnEnable()
    {
        LoadData();
    }

    // ==================== 加载 ====================
    private void LoadData()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            json = RemoveJsonComments(json);

            GameMatch3ConfigWrapper wrapper = JsonUtility.FromJson<GameMatch3ConfigWrapper>(json);

            if (wrapper != null)
            {
                config = wrapper;

                // 防御性初始化，避免 JSON 字段缺失时为 null
                if (config.board == null) config.board = new GameMatch3BoardData();
                if (config.time == null) config.time = new GameMatch3TimeData();
                if (config.score == null) config.score = new GameMatch3ScoreData();
                if (config.specialTiles == null) config.specialTiles = new GameMatch3SpecialTilesData();
                if (config.props == null) config.props = new GameMatch3PropsData();

                Z_Logger.Log("加载消消乐配置成功");
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
        config = new GameMatch3ConfigWrapper();

        config.board = new GameMatch3BoardData
        {
            rows = 8,
            cols = 8,
            colorCount = 5
        };

        config.time = new GameMatch3TimeData
        {
            initialTime = 60f,
            timeAddSeconds = 30f
        };

        config.score = new GameMatch3ScoreData
        {
            baseMatch3 = 30,
            extraPerTile = 15,
            comboMultiplier = 0.5f
        };

        config.specialTiles = new GameMatch3SpecialTilesData
        {
            lineMultiplier = 1.5f,
            bombMultiplier = 2.0f,
            magicBirdMultiplier = 2.5f
        };

        config.props = new GameMatch3PropsData
        {
            hintBaseCount = 1,
            refreshBaseCount = 1,
            timeAddBaseCount = 1
        };
    }

    private string RemoveJsonComments(string json)
    {
        json = System.Text.RegularExpressions.Regex.Replace(json, @"//.*", string.Empty);
        json = System.Text.RegularExpressions.Regex.Replace(json, @"/\*[\s\S]*?\*/", string.Empty);
        return json;
    }

    // ==================== 保存 ====================
    private void SaveData()
    {
        string json = JsonUtility.ToJson(config, true);

        string directory = Path.GetDirectoryName(savePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(savePath, json);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("保存成功", $"配置已保存到\n{savePath}", "确定");
    }

    // ==================== GUI ====================
    private void OnGUI()
    {
        EditorGUILayout.LabelField("消消乐配置编辑器", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        DrawToolbar();
        EditorGUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawBoardSection();
        EditorGUILayout.Space(10);
        DrawTimeSection();
        EditorGUILayout.Space(10);
        DrawScoreSection();
        EditorGUILayout.Space(10);
        DrawSpecialTilesSection();
        EditorGUILayout.Space(10);
        DrawPropsSection();

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

    // ==================== 棋盘 ====================
    private void DrawBoardSection()
    {
        EditorGUILayout.LabelField("棋盘配置", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        config.board.rows = EditorGUILayout.IntSlider("行数 rows", config.board.rows, 4, 12);
        config.board.cols = EditorGUILayout.IntSlider("列数 cols", config.board.cols, 4, 12);
        config.board.colorCount = EditorGUILayout.IntSlider("颜色种类 colorCount", config.board.colorCount, 3, 8);

        EditorGUILayout.EndVertical();
    }

    // ==================== 时间 ====================
    private void DrawTimeSection()
    {
        EditorGUILayout.LabelField("时间配置", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        config.time.initialTime = EditorGUILayout.FloatField("初始时间（秒）", config.time.initialTime);
        config.time.timeAddSeconds = EditorGUILayout.FloatField("时间+道具增加（秒）", config.time.timeAddSeconds);

        EditorGUILayout.EndVertical();
    }

    // ==================== 得分 ====================
    private void DrawScoreSection()
    {
        EditorGUILayout.LabelField("得分配置", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        config.score.baseMatch3 = EditorGUILayout.IntField("基础 3 连得分", config.score.baseMatch3);
        config.score.extraPerTile = EditorGUILayout.IntField("每多 1 个元素加分", config.score.extraPerTile);
        config.score.comboMultiplier = EditorGUILayout.Slider("连锁倍数系数", config.score.comboMultiplier, 0f, 2f);

        EditorGUILayout.HelpBox(
            $"连锁公式：第 n 次连锁得分 = 基础分 × (1 + {config.score.comboMultiplier:F2} × (n-1))",
            MessageType.None);

        EditorGUILayout.EndVertical();
    }

    // ==================== 特殊元素 ====================
    private void DrawSpecialTilesSection()
    {
        EditorGUILayout.LabelField("特殊元素配置", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        config.specialTiles.lineMultiplier = EditorGUILayout.Slider("直线特效倍数", config.specialTiles.lineMultiplier, 1f, 5f);
        config.specialTiles.bombMultiplier = EditorGUILayout.Slider("爆炸特效倍数", config.specialTiles.bombMultiplier, 1f, 5f);
        config.specialTiles.magicBirdMultiplier = EditorGUILayout.Slider("魔力鸟倍数", config.specialTiles.magicBirdMultiplier, 1f, 5f);

        EditorGUILayout.EndVertical();
    }

    // ==================== 道具 ====================
    private void DrawPropsSection()
    {
        EditorGUILayout.LabelField("道具基础次数（等级 1）", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        config.props.hintBaseCount = EditorGUILayout.IntField("提示 hintBaseCount", config.props.hintBaseCount);
        config.props.refreshBaseCount = EditorGUILayout.IntField("刷新 refreshBaseCount", config.props.refreshBaseCount);
        config.props.timeAddBaseCount = EditorGUILayout.IntField("时间+ timeAddBaseCount", config.props.timeAddBaseCount);

        EditorGUILayout.HelpBox(
            "实际次数 = 基础次数 × 游戏等级（当前等级由外部传入）",
            MessageType.None);

        EditorGUILayout.EndVertical();
    }

    // ==================== 底部帮助 ====================
    private void DrawHelpBox()
    {
        EditorGUILayout.HelpBox(
            $"配置文件路径: {savePath}\n" +
            $"棋盘: {config.board.rows}×{config.board.cols}，颜色 {config.board.colorCount} 种\n" +
            $"时间: 初始 {config.time.initialTime}s，时间+道具 {config.time.timeAddSeconds}s\n" +
            $"得分: 基础 {config.score.baseMatch3}，每多 1 个 +{config.score.extraPerTile}，连锁系数 {config.score.comboMultiplier}\n" +
            $"特殊元素: 直线 ×{config.specialTiles.lineMultiplier}，爆炸 ×{config.specialTiles.bombMultiplier}，魔力鸟 ×{config.specialTiles.magicBirdMultiplier}\n" +
            $"道具基础次数: 提示 {config.props.hintBaseCount} / 刷新 {config.props.refreshBaseCount} / 时间+ {config.props.timeAddBaseCount}",
            MessageType.Info);
    }
}
#endif