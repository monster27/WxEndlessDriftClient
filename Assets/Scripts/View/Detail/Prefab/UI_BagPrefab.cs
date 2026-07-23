using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text;
using SharedModels;

namespace View.Detail
{
    public class UI_BagPrefab : MonoBehaviour
    {
        public Image iconImage;
        public Text quantityText;
        public Text nameText;
        public Button itemButton;
        public Image equippedMarker;  // 已装备标记图标

        private int itemId;
        private int quantity;
        private ItemData itemData;
        private bool isEquipped;

        void Start()
        {
            if (itemButton != null)
            {
                itemButton.onClick.AddListener(OnItemClick);
            }
            
            // 默认隐藏已装备标记
            if (equippedMarker != null)
            {
                equippedMarker.gameObject.SetActive(false);
            }
        }

        public void Init(int id, int qty, ItemData data, bool equipped = false)
        {
            itemId = id;
            quantity = qty;
            itemData = data;
            isEquipped = equipped;

            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            // 【修复】添加null检查，防止物品数据为null时崩溃
            if (itemData == null)
            {
                Debug.LogWarning($"[UI_BagPrefab] UpdateDisplay - itemData为null，itemId={itemId}");
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
                // 无鱼饵（itemId=0）、鱼类（categoryId=1）不显示数量文本
                if (itemId == 0 || itemData.categoryId == 1)
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

            if (iconImage != null)
            {
                LoadIcon();
            }

            // 更新已装备标记显示
            if (equippedMarker != null)
            {
                equippedMarker.gameObject.SetActive(isEquipped);
                Debug.Log($"[UI_BagPrefab] 更新装备标记 - itemId={itemId}, name={itemData.name}, isEquipped={isEquipped}, equippedMarker.active={equippedMarker.gameObject.activeSelf}");
            }
            else
            {
                Debug.LogError($"[UI_BagPrefab] equippedMarker 为 null - itemId={itemId}, name={itemData.name}");
            }
        }

        private void LoadIcon()
        {
            if (!string.IsNullOrEmpty(itemData.iconPath))
            {
                Sprite icon = Resources.Load<Sprite>(itemData.iconPath);
                if (icon != null)
                {
                    iconImage.sprite = icon;
                    iconImage.color = Color.white;
                }
                else
                {
                    Debug.LogError($"[UI_BagPrefab] 图标加载失败 - 物品ID: {itemId}, 名称: {itemData.name}, 路径: {itemData.iconPath}");
                    iconImage.sprite = null;
                    iconImage.color = Color.gray;
                }
            }
            else
            {
                Debug.LogError($"[UI_BagPrefab] 图标路径为空 - 物品ID: {itemId}, 名称: {itemData.name}");
                iconImage.sprite = null;
                iconImage.color = Color.gray;
            }
        }

        private void OnItemClick()
        {
            Debug.Log($"[UI_BagPrefab] 点击物品: ID={itemId}, 名称={itemData.name}, 数量={quantity}, 是否已装备={isEquipped}, categoryId={itemData?.categoryId}");

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
        }

        /// <summary>
        /// 将饵料装备到鱼饵槽位（如果已装备则卸下）
        /// </summary>
        private void EquipBaitToSlot()
        {
            if (itemId == 0)
            {
                CommunicateEvent.Modify<EquipmentSlotType>(CommunicateEvent.EVENT_UNEQUIP_BAIT, EquipmentSlotType.Bait);
                Debug.Log($"[UI_BagPrefab] 已发送卸下鱼饵请求（选择无鱼饵）");
                isEquipped = true;
                UpdateDisplay();
            }
            else if (isEquipped)
            {
                CommunicateEvent.Modify<EquipmentSlotType>(CommunicateEvent.EVENT_UNEQUIP_BAIT, EquipmentSlotType.Bait);
                Debug.Log($"[UI_BagPrefab] 已发送卸下鱼饵请求: {itemData?.name}");
                isEquipped = false;
                UpdateDisplay();
            }
            else
            {
                CommunicateEvent.Modify<int>(CommunicateEvent.EVENT_EQUIP_BAIT, itemId);
                Debug.Log($"[UI_BagPrefab] 已发送装备鱼饵请求: {itemData?.name}");
                isEquipped = true;
                UpdateDisplay();
            }
        }

        /// <summary>
        /// 使用窝料：消耗一个窝料，增加连续模式时间
        /// </summary>
        private void UseNestBait()
        {
            if (quantity <= 0)
            {
                GameUIManager.ShowMessage("窝料数量不足");
                return;
            }

            Debug.Log($"[UI_BagPrefab] 使用窝料: {itemData?.name}, 剩余数量: {quantity}");
            CommunicateEvent.Modify(CommunicateEvent.EVENT_CONSUME_BAIT_AND_ENTER_CONTINUOUS_MODE);
        }

        /// <summary>
        /// 设置物品是否已装备
        /// </summary>
        public void SetEquipped(bool equipped)
        {
            isEquipped = equipped;
            UpdateDisplay();
        }

        /// <summary>
        /// 获取物品是否已装备
        /// </summary>
        public bool IsEquipped()
        {
            return isEquipped;
        }
    }
}
