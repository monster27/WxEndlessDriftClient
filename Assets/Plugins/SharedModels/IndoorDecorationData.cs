using System;
using System.Collections.Generic;

namespace SharedModels
{
    /// <summary>
    /// 室内装饰数据
    /// </summary>
    [Serializable]
    public class IndoorDecorationData
    {
        public int id;
        public string name = string.Empty;
        public string description = string.Empty;
        public int categoryId;
    }

    [Serializable]
    public class IndoorDecorationListWrapper
    {
        public List<IndoorDecorationData> decorations = new List<IndoorDecorationData>();
    }

    ///// <summary>
    ///// 室内装饰子分类枚举
    ///// </summary>
    //public enum IndoorDecorationType
    //{
    //    None = 0,
    //    Wall = 51,          // 墙壁
    //    Floor = 52,         // 地板
    //    Stair = 53,         // 楼梯
    //    LightStrip = 54,    // 灯带
    //    HungDecoration = 55,    // 挂饰
    //    Telescope = 56,     // 望远镜
    //    InsectRoom = 57,    // 昆虫房
    //    PetHouse = 58,      // 宠物屋
    //    FishTank = 59,      // 鱼缸
    //    Panda = 60,         // 熊猫
    //    Parrot = 61,        // 鹦鹉
    //    Table = 62          // 桌子
    //}
}
