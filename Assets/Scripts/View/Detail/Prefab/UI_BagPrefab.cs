using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text;

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
                // categoryId=1的鱼类不显示数量文本，其他显示
                if (itemData.categoryId != 1)
                {
                    quantityText.text = quantity.ToString();
                    quantityText.gameObject.SetActive(true);
                }
                else
                {
                    quantityText.gameObject.SetActive(false);
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
                // 区分鱼饵（categoryId=21）和窝料（categoryId=22）
                if (itemData.categoryId == 21)
                {
                    // 鱼饵：装备到槽位
                    EquipBaitToSlot();
                }
                else if (itemData.categoryId == 22)
                {
                    // 窝料：不需要装备，只显示数量
                    Debug.Log($"[UI_BagPrefab] 窝料无需装备，当前数量: {quantity}");
                }
            }
        }

        /// <summary>
        /// 将饵料装备到鱼饵槽位
        /// </summary>
        private void EquipBaitToSlot()
        {
            CommunicateEvent.Modify<int>(CommunicateEvent.EVENT_EQUIP_BAIT, itemId);
            Debug.Log($"[UI_BagPrefab] 已发送装备鱼饵请求: {itemData?.name}");
            isEquipped = true;
            UpdateDisplay();
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