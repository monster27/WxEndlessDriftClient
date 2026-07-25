using System;
using System.Collections.Generic;

namespace SharedModels
{
    /// <summary>
    /// 室外装饰数据
    /// </summary>
    [Serializable]
    public class OutdoorDecorationData
    {
        public int id;
        public string name = string.Empty;
        public string description = string.Empty;
        public int categoryId;
    }

    [Serializable]
    public class OutdoorDecorationListWrapper
    {
        public List<OutdoorDecorationData> decorations = new List<OutdoorDecorationData>();
    }

    /// <summary>
    /// 室外装饰子分类枚举
    /// </summary>
    public enum OutdoorDecorationType
    {
        None = 0,
        FishBag = 41,       // 鱼篓装饰
        Tent = 42,          // 帐篷装饰
        FishTip = 43        // 提示器装饰
    }
}
