// ============================================================
// 文件: UI_EggHatchSuccessPanel.cs
// 说明: 孵化成功展示宠物
// 路径: Assets/Scripts/UI/
// ============================================================

using UnityEngine;
using UnityEngine.UI;

public class UI_EggHatchSuccessPanel : MonoBehaviour
{
    // ============================================================
    // Inspector 引用
    // ============================================================

    public Image icon;
    public Text nameText;
    public Text descText;
    public Button returnBtn;

    // ============================================================
    // 运行时数据
    // ============================================================

    private PlayerPetData _pet;

    // ============================================================
    // 生命周期
    // ============================================================

    private void Awake()
    {
        if (returnBtn != null)
        {
            returnBtn.onClick.RemoveAllListeners();
            returnBtn.onClick.AddListener(Close);
        }
    }

    // ============================================================
    // 打开 / 关闭
    // ============================================================

    public void Open(PlayerPetData pet)
    {
        _pet = pet;
        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    // ============================================================
    // 刷新
    // ============================================================

    private void Refresh()
    {
        if (_pet == null)
        {
            Z_Logger.LogWarning("[UI_EggHatchSuccessPanel] pet 为空");
            return;
        }

        // 图标：Addressables/UI/Icon/Pet/{petId}
        if (icon != null)
        {
            string path = $"UI/Icon/PetIcons/{_pet.petId}";
            AssetManager.LoadFromAddressables<Sprite>(path, (sprite, handle) =>
            {
                if (icon != null) icon.sprite = sprite;
            });
        }

        // 名字（优先昵称）
        string displayName = string.IsNullOrEmpty(_pet.nickname) ? _pet.name : _pet.nickname;
        if (nameText != null) nameText.text = displayName;

        // 描述：稀有度 + 等级
        if (descText != null)
        {
            var rarity = LoadDataManager.Instance != null
                ? LoadDataManager.Instance.GetRarityById(_pet.rarityId) : null;
            string rarityName = rarity?.name ?? $"稀有度{_pet.rarityId}";
            descText.text = $"孵化成功！\n{rarityName}\nLv.{_pet.level}";
        }
    }
}
