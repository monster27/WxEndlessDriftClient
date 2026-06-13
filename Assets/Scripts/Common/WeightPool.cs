using System;
using System.Collections.Generic;

public class WeightPool<T>
{
    private List<WeightItem<T>> items = new List<WeightItem<T>>();
    private int totalWeight = 0;
    private Random random = new Random();

    public void Add(T item, int weight)
    {
        if (weight <= 0)
            return;

        items.Add(new WeightItem<T>(item, weight));
        totalWeight += weight;
    }

    public T Get()
    {
        if (items.Count == 0 || totalWeight <= 0)
            return default(T);

        int randomValue = random.Next(totalWeight);
        int currentWeight = 0;

        foreach (var item in items)
        {
            currentWeight += item.weight;
            if (randomValue < currentWeight)
            {
                return item.value;
            }
        }

        return items[items.Count - 1].value;
    }

    public int Count => items.Count;

    public void Clear()
    {
        items.Clear();
        totalWeight = 0;
    }

    public bool Contains(T item)
    {
        foreach (var weightItem in items)
        {
            if (EqualityComparer<T>.Default.Equals(weightItem.value, item))
            {
                return true;
            }
        }
        return false;
    }

    private struct WeightItem<TValue>
    {
        public TValue value;
        public int weight;

        public WeightItem(TValue value, int weight)
        {
            this.value = value;
            this.weight = weight;
        }
    }
}