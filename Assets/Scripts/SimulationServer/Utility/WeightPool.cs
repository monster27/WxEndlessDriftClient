// ========================================================
// 模拟服务器已被移除 - 客户端现在仅使用网络服务器模式
// 此文件中的所有代码已被注释，以支持纯在线模式
// ========================================================
/*
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 权重池工具类
/// 用于根据权重随机选择元素
/// </summary>
public class WeightPool<T>
{
    private List<WeightedItem> items = new List<WeightedItem>();
    private int totalWeight = 0;
    private System.Random random = new System.Random();

    /// <summary>
    /// 添加元素到权重池
    /// </summary>
    /// <param name="item">元素</param>
    /// <param name="weight">权重</param>
    public void Add(T item, int weight)
    {
        if (weight <= 0)
        {
            Debug.LogWarning("[WeightPool] 权重必须大于0");
            return;
        }

        items.Add(new WeightedItem(item, weight));
        totalWeight += weight;
    }

    /// <summary>
    /// 根据权重随机获取一个元素
    /// </summary>
    /// <returns>随机选中的元素</returns>
    public T Get()
    {
        if (items.Count == 0)
        {
            Debug.LogWarning("[WeightPool] 权重池为空");
            return default(T);
        }

        int randomWeight = random.Next(0, totalWeight);
        int currentWeight = 0;

        foreach (var item in items)
        {
            currentWeight += item.weight;
            if (randomWeight < currentWeight)
            {
                return item.value;
            }
        }

        return items[items.Count - 1].value;
    }

    /// <summary>
    /// 清空权重池
    /// </summary>
    public void Clear()
    {
        items.Clear();
        totalWeight = 0;
    }

    /// <summary>
    /// 获取权重池中的元素数量
    /// </summary>
    /// <returns>元素数量</returns>
    public int Count()
    {
        return items.Count;
    }

    /// <summary>
    /// 获取总权重
    /// </summary>
    /// <returns>总权重</returns>
    public int GetTotalWeight()
    {
        return totalWeight;
    }

    /// <summary>
    /// 内部加权项结构
    /// </summary>
    private struct WeightedItem
    {
        public T value;
        public int weight;

        public WeightedItem(T value, int weight)
        {
            this.value = value;
            this.weight = weight;
        }
    }
}
*/
