using System;

namespace SharedModels
{
    [Serializable]
    public class MallItemData
    {
        public int id;             // 物品ID（兼容命名）
        public int itemId;         // 物品ID（备用命名）
        public int price;          // 物品价格
        public int stock;          // 库存数量
        
        // 扩展字段（用于商城详情显示）
        public string name;        // 物品名称
        public string description; // 物品描述
        public int type;           // 物品类型
        public int count;          // 购买数量
        public int iconId;         // 图标ID
        public bool isHot;         // 是否热门
        public bool isNew;         // 是否新品
    }
}