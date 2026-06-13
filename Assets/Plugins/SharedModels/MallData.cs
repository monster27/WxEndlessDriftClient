using System;

namespace SharedModels
{
    /// <summary>
    /// 商城分类数据结构
    /// </summary>
    [Serializable]
    public class MallCategoryData
    {
        public int id;              // 分类ID
        public string name;         // 分类名称
        public string iconName;     // 图标名称
        public bool isDefault;      // 是否默认分类
    }

    /// <summary>
    /// 商城配置数据结构
    /// </summary>
    [Serializable]
    public class MallConfigData
    {
        public int mallId;                  // 商城ID
        public string mallName;             // 商城名称
        public MallCategoryData[] categories; // 分类列表
        public MallItemData[] items;        // 物品列表
    }
}