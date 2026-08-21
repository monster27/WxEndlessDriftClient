using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text;
using UnityEngine.ResourceManagement.AsyncOperations;
//using SharedModels;

namespace View.Detail
{
    public class UI_BagPrefab : MonoBehaviour
    {
        public Image iconImage;
        public Text quantityText;
        public Text nameText;
        public Button itemButton;
        public Image equippedMarker;

        private int itemId;
        private int quantity;
        private ItemData itemData;
        private bool isEquipped;
        private AsyncOperationHandle<Sprite> _iconHandle;

        public void Init(int id, int qty, ItemData data, bool equipped = false)
        {
            if (itemButton != null)
            {
                itemButton.onClick.RemoveAllListeners();
                itemButton.onClick.AddListener(OnItemClick);
            }
            itemId = id;
            quantity = qty;
            itemData = data;
            isEquipped = equipped;

            UpdateDisplay();
        }

        private void OnDestroy()
        {
            AssetManager.ReleaseAddressable(_iconHandle);
        }

        private void UpdateDisplay()
        {
            if (itemData == null)
            {
                Z_Logger.LogWarning($"[UI_BagPrefab] UpdateDisplay - itemData为null，itemId={itemId}");
                if (nameText != null) nameText.text = "";
                if (quantityText != null) quantityText.text = "";
                if (iconImage != null) iconImage.sprite = null;
                if (equippedMarker != null) equippedMarker.gameObject.SetActive(false);
                return;
            }

            if (nameText != null)
            {
                nameText.text = itemData.name;
            }

            if (quantityText != null)
            {
                if (itemId == 0 || itemData.categoryId == 1 || itemData.isUnique)
                {
                    quantityText.text = "";
                    quantityText.gameObject.SetActive(false);
                }
                else
                {
                    quantityText.text = quantity.ToString();
                    quantityText.gameObject.SetActive(true);
                }
            }

            LoadIcon();

            if (equippedMarker != null)
            {
                equippedMarker.gameObject.SetActive(isEquipped);
                Z_Logger.Log($"[UI_BagPrefab] 更新装备标记 - itemId={itemId}, name={itemData.name}, isEquipped={isEquipped}");
            }
        }

        private void LoadIcon()
        {
            if (string.IsNullOrEmpty(itemData?.iconPath))
            {
                Z_Logger.LogError($"[UI_BagPrefab] 图标路径为空 - 物品ID: {itemId}");
                iconImage.sprite = null;
                iconImage.color = Color.gray;
                return;
            }

            // ✅ 改为 AA 异步加载
            AssetManager.LoadFromAddressables<Sprite>(itemData.iconPath, (sprite, handle) =>
            {
                _iconHandle = handle;
                if (sprite != null)
                {
                    iconImage.sprite = sprite;
                    iconImage.color = Color.white;
                }
                else
                {
                    Z_Logger.LogError($"[UI_BagPrefab] 图标加载失败 - 物品ID: {itemId}, 路径: {itemData.iconPath}");
                    iconImage.sprite = null;
                    iconImage.color = Color.gray;
                }
            });
        }

        private void OnItemClick()
        {
            Z_Logger.Log($"[UI_BagPrefab] 点击物品: ID={itemId}, 名称={itemData.name}, 数量={quantity}, 是否已装备={isEquipped}");

            if (itemData != null && itemData.itemType == 2)
            {
                if (itemData.categoryId == 21)
                {
                    EquipBaitToSlot();
                }
                else if (itemData.categoryId == 22)
                {
                    //UseNestBait();
                }
            }
            else if (itemData != null && (itemData.itemType == 4 || itemData.itemType == 5))
            {
                EquipSkin();
            }
        }

        private void EquipSkin()
        {
            if (itemData == null) return;

            int slotType = itemData.categoryId;
            Z_Logger.Log($"[UI_BagPrefab] 装备皮肤: itemId={itemId}, slotType={slotType}, name={itemData.name}");

            NetServerManager.Instance?.RequestEquipSkin(slotType, itemId);

            isEquipped = true;
            UpdateDisplay();
        }

        private void EquipBaitToSlot()
        {
            if (itemId == 0)
            {
                CommunicateEvent.Modify<EquipmentSlotType>(CommunicateEvent.EVENT_UNEQUIP_BAIT, EquipmentSlotType.Bait);
                Z_Logger.Log($"[UI_BagPrefab] 已发送卸下鱼饵请求（选择无鱼饵）");
                isEquipped = true;
                UpdateDisplay();
            }
            else if (isEquipped)
            {
                CommunicateEvent.Modify<EquipmentSlotType>(CommunicateEvent.EVENT_UNEQUIP_BAIT, EquipmentSlotType.Bait);
                Z_Logger.Log($"[UI_BagPrefab] 已发送卸下鱼饵请求: {itemData?.name}");
                isEquipped = false;
                UpdateDisplay();
            }
            else
            {
                CommunicateEvent.Modify<int>(CommunicateEvent.EVENT_EQUIP_BAIT, itemId);
                Z_Logger.Log($"[UI_BagPrefab] 已发送装备鱼饵请求: {itemData?.name}");
                isEquipped = true;
                UpdateDisplay();
            }
        }

        private void UseNestBait()
        {
            if (quantity <= 0)
            {
                GameUIManager.ShowMessage("窝料数量不足");
                return;
            }

            Z_Logger.Log($"[UI_BagPrefab] 使用窝料: {itemData?.name}, 剩余数量: {quantity}");
            CommunicateEvent.Modify(CommunicateEvent.EVENT_CONSUME_BAIT_AND_ENTER_CONTINUOUS_MODE);
        }

        public void SetEquipped(bool equipped)
        {
            isEquipped = equipped;
            UpdateDisplay();
        }

        public bool IsEquipped()
        {
            return isEquipped;
        }
    }
}
