using System.Collections.Generic;
using System;

//服务器专属

[Serializable]
public class CollectionReward
{
    public int percent;
    public int rewardId;
    public int rewardAmount;
    public bool claimed;
}

[Serializable]
public class CollectionPageConfig
{
    public int id;
    public string pageName;
    public List<CollectionReward> rewards;
    public List<int> entries;
}

[Serializable]
public class CollectionCategoryConfig
{
    public int id;
    public string name;
    public string icon;
    public List<CollectionPageConfig> pages;
}

[Serializable]
public class CollectionRootConfig
{
    public List<CollectionCategoryConfig> categories;
}

[Serializable]
public class CollectionWrapperConfig
{
    public CollectionRootConfig collection;
}

[Serializable]
public class PlayerCollectionProgress
{
    public int categoryId;
    public int pageId;
    public float completionPercent;
    public int completedCount;
    public int totalCount;
    public List<CollectionReward> availableRewards;
    public List<CollectionReward> claimedRewards;
}

