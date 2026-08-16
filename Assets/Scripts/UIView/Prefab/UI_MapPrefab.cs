using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class UI_MapPrefab : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text islandNameText;
    [SerializeField] private Button clickButton;
    [SerializeField] private Image islandIcon;
    [SerializeField] private string iconAddressPrefix = "UI/Icon/IslandInfoIcons/";

    private int islandId;
    private string islandName;
    private System.Action onClickCallback;
    private AsyncOperationHandle<Sprite> _iconHandle;

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

    void OnDestroy()
    {
        AssetManager.ReleaseAddressable(_iconHandle);
    }

    public void SetIslandInfo(int id, string name)
    {
        islandId = id;
        islandName = name;

        if (islandNameText != null)
        {
            islandNameText.text = name;
        }

        // ✅ 异步加载岛屿图标
        string iconPath = $"{iconAddressPrefix}{id}";
        AssetManager.LoadFromAddressables<Sprite>(iconPath, (sprite, handle) =>
        {
            _iconHandle = handle;
            if (islandIcon != null && sprite != null)
            {
                islandIcon.sprite = sprite;
                islandIcon.color = Color.white;
            }
        });
    }

    public void SetIslandName(string name)
    {
        islandName = name;
        if (islandNameText != null)
        {
            islandNameText.text = name;
        }
    }

    public void SetIslandId(int id)
    {
        islandId = id;
    }

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
