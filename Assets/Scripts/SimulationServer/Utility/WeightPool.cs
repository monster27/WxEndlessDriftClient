// 权重数据存储类
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class WeightData<T>
{
    public T item;
    public float weight;

    public WeightData(T item, float weight)
    {
        this.item = item;
        this.weight = weight;
    }
}

// 权重池子核心类
[System.Serializable]
public class WeightPool<T>
{
    [SerializeField] private List<WeightData<T>> dataList = new List<WeightData<T>>();
    private float totalWeight;

    // 添加权重数据
    public void Add(T item, float weight)
    {
        if (weight <= 0) return;
        dataList.Add(new WeightData<T>(item, weight));
        totalWeight += weight;
    }

    // 批量添加
    public void AddRange(params (T item, float weight)[] items)
    {
        foreach (var item in items)
            Add(item.item, item.weight);
    }

    // 移除
    public void Remove(T item)
    {
        var target = dataList.Find(d => d.item.Equals(item));
        if (target != null)
        {
            totalWeight -= target.weight;
            dataList.Remove(target);
        }
    }

    // 随机获取（核心）
    public T Get()
    {
        if (dataList.Count == 0) return default;

        float random = Random.Range(0, totalWeight);
        float current = 0;

        foreach (var data in dataList)
        {
            current += data.weight;
            if (random <= current)
                return data.item;
        }

        return dataList.Last().item;
    }

    // 清空
    public void Clear()
    {
        dataList.Clear();
        totalWeight = 0;
    }

    // 获取所有数据
    public List<WeightData<T>> GetAll() => dataList;

    // 修改权重
    public void UpdateWeight(T item, float newWeight)
    {
        var target = dataList.Find(d => d.item.Equals(item));
        if (target != null)
        {
            totalWeight = totalWeight - target.weight + newWeight;
            target.weight = newWeight;
        }
    }
}