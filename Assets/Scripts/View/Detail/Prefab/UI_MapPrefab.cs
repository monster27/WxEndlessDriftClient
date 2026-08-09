using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 地图岛屿按钮预制体脚本
/// </summary>
public class UI_MapPrefab : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text islandNameText;
    [SerializeField] private Button clickButton;
    [SerializeField] private Image islandIcon;

    private int islandId;
    private string islandName;
    private System.Action onClickCallback;

    void Awake()
    {
        if (clickButton == null)
            clickButton = GetComponent<Button>();

        if (islandNameText == null)
            islandNameText = GetComponentInChildren<Text>();

        if (clickButton != null)
        {
            clickButton.onClick.AddListener(OnButtonClick);
        }
    }

    /// <summary>
    /// 设置岛屿信息
    /// </summary>
    public void SetIslandInfo(int id, string name)
    {
        islandId = id;
        islandName = name;

        if (islandNameText != null)
        {
            islandNameText.text = name;
        }
    }

    /// <summary>
    /// 设置岛屿名称
    /// </summary>
    public void SetIslandName(string name)
    {
        islandName = name;
        if (islandNameText != null)
        {
            islandNameText.text = name;
        }
    }

    /// <summary>
    /// 设置岛屿ID
    /// </summary>
    public void SetIslandId(int id)
    {
        islandId = id;
    }

    /// <summary>
    /// 设置点击回调
    /// </summary>
    public void SetOnClickCallback(System.Action callback)
    {
        onClickCallback = callback;
    }

    private void OnButtonClick()
    {
        Debug.Log($"[UI_MapPrefab] 点击岛屿: {islandName} (ID: {islandId})");
        onClickCallback?.Invoke();
    }

    public int GetIslandId()
    {
        return islandId;
    }

    public string GetIslandName()
    {
        return islandName;
    }
}
