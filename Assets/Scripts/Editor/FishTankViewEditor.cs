#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(FishTankView))]
public class FishTankViewEditor : Editor
{
    private const string DEC_RELATIVE_PATH = "Addressables/JsonData/Game/BagItem/fishTankDec.json";
    private const string ITEMS_RELATIVE_PATH = "Addressables/JsonData/Game/Items/items.json";

    private List<FishTankDecData> allData = new List<FishTankDecData>();
    private List<FishTankDecData> filteredData = new List<FishTankDecData>();
    private FishTankDecData selectedData;

    private Dictionary<int, string> iconPathMap = new Dictionary<int, string>();

    private int filterCategory = 0;
    private Vector2 listScroll;
    private string log = "";

    private int editWidth = 100;
    private int editHeight = 100;

    // 当前选中装饰的贴图原始像素尺寸（0 表示未加载到）
    private int _spriteW = 0;
    private int _spriteH = 0;

    private void OnEnable()
    {
        LoadJson();
        LoadIconPathMap();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        DrawHeader();

        if (GUILayout.Button("📥 加载所有数据（摆设 + 挂饰）", GUILayout.Height(28)))
        {
            LoadJson();
            LoadIconPathMap();
            AddLog("重新加载 JSON 完成");
        }

        if (allData.Count == 0)
        {
            EditorGUILayout.HelpBox("没有 80/81 数据，检查 JSON 路径或内容。", MessageType.Warning);
            DrawLog();
            return;
        }

        DrawFilter();
        DrawList();
        DrawEditArea();
        DrawLog();
    }

    private void DrawHeader()
    {
        var style = new GUIStyle(EditorStyles.helpBox);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 13;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        Color old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.2f, 0.45f, 0.8f);
        EditorGUILayout.BeginVertical(style);
        EditorGUILayout.LabelField("🎏 鱼缸装饰宽高设置", style);
        EditorGUILayout.LabelField("只对 摆设(80) / 挂饰(81) 生效", style);
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = old;
        GUILayout.Space(6);
    }

    private void DrawFilter()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("筛选:", GUILayout.Width(40));
        int newFilter = GUILayout.Toolbar(filterCategory, new[] { "全部", "摆设(80)", "挂饰(81)" });
        if (newFilter != filterCategory)
        {
            filterCategory = newFilter;
            ApplyFilter();
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4);
    }

    private void ApplyFilter()
    {
        if (filterCategory == 0) filteredData = new List<FishTankDecData>(allData);
        else if (filterCategory == 1) filteredData = allData.Where(d => d.categoryId == 80).ToList();
        else filteredData = allData.Where(d => d.categoryId == 81).ToList();
    }

    private void DrawList()
    {
        EditorGUILayout.LabelField($"装饰列表（{filteredData.Count}）", EditorStyles.boldLabel);
        listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Height(180), GUILayout.ExpandWidth(true));
        foreach (var d in filteredData)
        {
            bool isSel = selectedData != null && selectedData.id == d.id;
            EditorGUILayout.BeginHorizontal(isSel ? "SelectionRect" : "box");
            EditorGUILayout.LabelField($"[{d.id}]", GUILayout.Width(55));
            EditorGUILayout.LabelField(d.name, GUILayout.Width(120));
            EditorGUILayout.LabelField(d.categoryId == 80 ? "摆设" : "挂饰", GUILayout.Width(45));
            EditorGUILayout.LabelField($"W:{d.width} H:{d.height}", GUILayout.Width(100));
            if (GUILayout.Button(isSel ? "● 已选" : "选择", GUILayout.Width(60)))
                SelectDecoration(d);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawEditArea()
    {
        if (selectedData == null)
        {
            EditorGUILayout.HelpBox("请从上方列表选择一个装饰", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"当前编辑: [{selectedData.id}] {selectedData.name}", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        // 原图像素显示
        if (_spriteW > 0 && _spriteH > 0)
            EditorGUILayout.LabelField($"默认（原图）: {_spriteW} x {_spriteH}", EditorStyles.miniLabel);
        else
            EditorGUILayout.LabelField("默认（原图）: 未加载到", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("宽:", GUILayout.Width(30));
        editWidth = EditorGUILayout.IntField(editWidth, GUILayout.Width(80));
        EditorGUILayout.LabelField("高:", GUILayout.Width(30));
        editHeight = EditorGUILayout.IntField(editHeight, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        // 3 个按比例设置按钮
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(_spriteW <= 0 || _spriteH <= 0);
        if (GUILayout.Button("根据高度设宽度", GUILayout.Height(22)))
        {
            editWidth = Mathf.RoundToInt(editHeight * (float)_spriteW / _spriteH);
            ApplyToPreview();
        }
        if (GUILayout.Button("根据宽度设高度", GUILayout.Height(22)))
        {
            editHeight = Mathf.RoundToInt(editWidth * (float)_spriteH / _spriteW);
            ApplyToPreview();
        }
        if (GUILayout.Button("使用原图像素", GUILayout.Height(22)))
        {
            editWidth = _spriteW;
            editHeight = _spriteH;
            ApplyToPreview();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
        if (GUILayout.Button("应用到场景", GUILayout.Height(24)))
            ApplyToPreview();
        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("💾 保存到 JSON", GUILayout.Height(24)))
            SaveToJson();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawLog()
    {
        if (string.IsNullOrEmpty(log)) return;
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("日志", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(log, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(40));
        EditorGUILayout.EndVertical();
    }

    // ─────────────────────────────────────────────
    private void SelectDecoration(FishTankDecData d)
    {
        selectedData = d;
        editWidth = d.width <= 0 ? 100 : d.width;
        editHeight = d.height <= 0 ? 100 : d.height;

        // 读原图像素尺寸
        _spriteW = 0;
        _spriteH = 0;
        Sprite sp = LoadIconSprite(d.id);
        if (sp != null && sp.texture != null)
        {
            _spriteW = sp.texture.width;
            _spriteH = sp.texture.height;
        }

        ApplyToPreview();
        Repaint();
        AddLog($"选中 [{d.id}] {d.name}  sprite={_spriteW}x{_spriteH}");
    }

    private void ApplyToPreview()
    {
        if (selectedData == null) return;

        var view = (FishTankView)target;
        if (view == null || view.editorPreviewDec == null)
        {
            EditorUtility.DisplayDialog("错误",
                "请先在 FishTankView.editorPreviewDec 上指定预览装饰实例。", "确定");
            AddLog("❌ editorPreviewDec 未指定");
            return;
        }

        Sprite sprite = LoadIconSprite(selectedData.id);

        view.editorPreviewDec.gameObject.SetActive(true);   // 编辑器下确保可见
        view.editorPreviewDec.EditorApplySize(
            selectedData.categoryId,
            editWidth,
            editHeight,
            sprite
        );

        EditorUtility.SetDirty(view.editorPreviewDec.gameObject);
        SceneView.RepaintAll();
    }

    private void SaveToJson()
    {
        if (selectedData == null) return;
        if (selectedData.categoryId != 80 && selectedData.categoryId != 81)
        {
            EditorUtility.DisplayDialog("提示", "只有摆设(80)/挂饰(81) 能保存宽高", "确定");
            return;
        }

        LoadJson();

        var item = allData.FirstOrDefault(d => d.id == selectedData.id);
        if (item == null)
        {
            EditorUtility.DisplayDialog("错误", $"JSON 里找不到 id={selectedData.id}", "确定");
            return;
        }

        item.width = editWidth;
        item.height = editHeight;

        WriteJson(allData);

        selectedData.width = editWidth;
        selectedData.height = editHeight;
        ApplyFilter();

        AddLog($"保存 [{selectedData.id}] W={editWidth} H={editHeight}");
        EditorUtility.DisplayDialog("保存成功",
            $"已保存 [{selectedData.id}] {selectedData.name}\nW={editWidth} H={editHeight}", "确定");
    }

    // ─────────────────────────────────────────────
    private void LoadJson()
    {
        string fullPath = Path.Combine(Application.dataPath, DEC_RELATIVE_PATH);
        allData.Clear();

        if (!File.Exists(fullPath))
        {
            AddLog($"找不到 JSON: {fullPath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var wrapper = JsonUtility.FromJson<FishTankDecListWrapper>(json);
            if (wrapper != null && wrapper.items != null)
            {
                allData = wrapper.items
                    .Where(d => d.categoryId == 80 || d.categoryId == 81)
                    .ToList();

                foreach (var d in allData)
                {
                    if (d.width <= 0) d.width = 100;
                    if (d.height <= 0) d.height = 100;
                }
            }
        }
        catch (Exception e)
        {
            AddLog($"读取失败: {e.Message}");
        }

        ApplyFilter();
    }

    private void WriteJson(List<FishTankDecData> onlyVisible)
    {
        string fullPath = Path.Combine(Application.dataPath, DEC_RELATIVE_PATH);
        string dir = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        List<FishTankDecData> full = new List<FishTankDecData>();
        if (File.Exists(fullPath))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<FishTankDecListWrapper>(File.ReadAllText(fullPath));
                if (wrapper != null && wrapper.items != null) full = wrapper.items.ToList();
            }
            catch { }
        }

        foreach (var newItem in onlyVisible)
        {
            var exist = full.FirstOrDefault(x => x.id == newItem.id);
            if (exist != null)
            {
                exist.width = newItem.width;
                exist.height = newItem.height;
            }
        }

        var outWrapper = new FishTankDecListWrapper { items = full };
        File.WriteAllText(fullPath, JsonUtility.ToJson(outWrapper, true));
        AssetDatabase.Refresh();
    }

    // ─────────────────────────────────────────────
    private void LoadIconPathMap()
    {
        iconPathMap.Clear();

        string fullPath = Path.Combine(Application.dataPath, ITEMS_RELATIVE_PATH);
        if (!File.Exists(fullPath))
        {
            AddLog($"找不到 items.json: {fullPath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var wrapper = JsonUtility.FromJson<ItemListWrapperLite>(json);
            if (wrapper != null && wrapper.items != null)
            {
                foreach (var it in wrapper.items)
                {
                    if (it != null) iconPathMap[it.id] = it.iconPath;
                }
                AddLog($"加载 iconPath 映射 {iconPathMap.Count} 条");
            }
        }
        catch (Exception e)
        {
            AddLog($"items.json 解析失败: {e.Message}");
        }
    }

    private Sprite LoadIconSprite(int decorationId)
    {
        if (!iconPathMap.TryGetValue(decorationId, out string path) || string.IsNullOrEmpty(path))
        {
            AddLog($"⚠️ id={decorationId} 找不到 iconPath");
            return null;
        }

        string assetPath = $"Assets/Addressables/{path}.png";

        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sp == null)
            AddLog($"⚠️ 加载不到 Sprite: {assetPath}");
        return sp;
    }

    private void AddLog(string msg)
    {
        log = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + log;
        if (log.Length > 2000) log = log.Substring(0, 2000);
    }

    [Serializable]
    private class ItemListWrapperLite
    {
        public List<ItemLite> items;
    }
    [Serializable]
    private class ItemLite
    {
        public int id;
        public string iconPath;
    }
}
#endif
