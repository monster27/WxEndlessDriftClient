//using System;
//using System.Collections.Generic;
//using System.IO;
//using Newtonsoft.Json;
//using SharedModels;

//namespace ServerModels
//{
//    /// <summary>
//    /// 装备配置加载器，负责从JSON文件加载装备能力配置
//    /// 注意：此类保留在ServerModels中，但使用SharedModels中的数据类型
//    /// </summary>
//    public class FishingComponentConfigLoader
//    {
//        private static FishingComponentConfigLoader _instance;
//        private static readonly object _lock = new object();
//        private Dictionary<int, FishingComponentConfig> _configs = new Dictionary<int, FishingComponentConfig>();
//        private bool _isLoaded = false;

//        private FishingComponentConfigLoader()
//        {
//        }

//        public static FishingComponentConfigLoader Instance
//        {
//            get
//            {
//                if (_instance == null)
//                {
//                    lock (_lock)
//                    {
//                        if (_instance == null)
//                        {
//                            _instance = new FishingComponentConfigLoader();
//                        }
//                    }
//                }
//                return _instance;
//            }
//        }

//        private string GetConfigPath()
//        {
//            string[] possiblePaths = new string[]
//            {
//                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Shared", "Data", "JsonData", "Ability", "fishing_components.json"),
//                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "JsonData", "Ability", "fishing_components.json"),
//                Path.Combine(Directory.GetCurrentDirectory(), "Shared", "Data", "JsonData", "Ability", "fishing_components.json"),
//                Path.Combine(Directory.GetCurrentDirectory(), "fishing_components.json")
//            };

//            foreach (var path in possiblePaths)
//            {
//                if (File.Exists(path))
//                {
//                    return path;
//                }
//            }

//            return null;
//        }

//        public void LoadConfigs()
//        {
//            if (_isLoaded) return;

//            string configPath = GetConfigPath();

//            if (!string.IsNullOrEmpty(configPath))
//            {
//                try
//                {
//                    string json = File.ReadAllText(configPath);
//                    ParseJsonAndLoad(json);
//                    _isLoaded = true;
//                    Console.WriteLine($"[FishingComponentConfigLoader] 成功从 {configPath} 加载配置");
//                    return;
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"[FishingComponentConfigLoader] 加载配置文件失败: {ex.Message}");
//                }
//            }

//            LoadDefaultConfigs();
//            _isLoaded = true;
//            Console.WriteLine("[FishingComponentConfigLoader] 使用默认配置");
//        }

//        private void ParseJsonAndLoad(string json)
//        {
//            try
//            {
//                var wrapper = JsonConvert.DeserializeObject<FishingComponentListWrapper>(json);
//                if (wrapper?.fishingComponents != null)
//                {
//                    foreach (var config in wrapper.fishingComponents)
//                    {
//                        _configs[config.id] = config;
//                    }
//                    Console.WriteLine($"[FishingComponentConfigLoader] 解析成功，加载 {_configs.Count} 个配置");
//                }
//                else
//                {
//                    LoadDefaultConfigs();
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"[FishingComponentConfigLoader] JSON解析失败: {ex.Message}");
//                LoadDefaultConfigs();
//            }
//        }

//        private void LoadDefaultConfigs()
//        {
//            _configs.Clear();

//            _configs[3001] = new FishingComponentConfig
//            {
//                id = 3001,
//                name = "基础钓竿",
//                slotTypeId = 1,
//                trashProbability = 0f,
//                maxTrashStreak = 3,
//                fishWeightMultiplier = 1f,
//                shinyRateBonus = 0f,
//                minFishingInterval = 0,
//                maxFishingInterval = 0,
//                fishBagCapacity = 0
//            };

//            _configs[3002] = new FishingComponentConfig
//            {
//                id = 3002,
//                name = "碳素钓竿",
//                slotTypeId = 1,
//                trashProbability = 0f,
//                maxTrashStreak = 4,
//                fishWeightMultiplier = 1.05f,
//                shinyRateBonus = 0f,
//                minFishingInterval = -1,
//                maxFishingInterval = -2,
//                fishBagCapacity = 2,
//                rarityBonus = new Dictionary<int, int>(),
//                rarityWeights = new Dictionary<int, int>()
//            };

//            _configs[3101] = new FishingComponentConfig
//            {
//                id = 3101,
//                name = "基础钓线",
//                slotTypeId = 2,
//                trashProbability = 0f,
//                maxTrashStreak = 0,
//                fishWeightMultiplier = 1f,
//                shinyRateBonus = 0f,
//                minFishingInterval = 0,
//                maxFishingInterval = 0,
//                fishBagCapacity = 0,
//                rarityBonus = new Dictionary<int, int> { { 203, 5 }, { 204, 3 } }
//            };

//            _configs[3102] = new FishingComponentConfig
//            {
//                id = 3102,
//                name = "碳素鱼线",
//                slotTypeId = 2,
//                trashProbability = 0f,
//                maxTrashStreak = 1,
//                fishWeightMultiplier = 1.1f,
//                shinyRateBonus = 0.02f,
//                minFishingInterval = 0,
//                maxFishingInterval = 0,
//                fishBagCapacity = 0,
//                rarityBonus = new Dictionary<int, int> { { 203, 15 }, { 204, 10 }, { 205, 5 } }
//            };

//            _configs[3201] = new FishingComponentConfig
//            {
//                id = 3201,
//                name = "基础钓钩",
//                slotTypeId = 3,
//                trashProbability = 0f,
//                maxTrashStreak = 0,
//                fishWeightMultiplier = 1f,
//                shinyRateBonus = 0.02f,
//                minFishingInterval = 0,
//                maxFishingInterval = 0,
//                fishBagCapacity = 0
//            };

//            _configs[3202] = new FishingComponentConfig
//            {
//                id = 3202,
//                name = "精钢鱼钩",
//                slotTypeId = 3,
//                trashProbability = 0f,
//                maxTrashStreak = 2,
//                fishWeightMultiplier = 1.08f,
//                shinyRateBonus = 0.05f,
//                minFishingInterval = 0,
//                maxFishingInterval = 0,
//                fishBagCapacity = 0
//            };

//            _configs[3301] = new FishingComponentConfig
//            {
//                id = 3301,
//                name = "新手入门",
//                slotTypeId = 4,
//                trashProbability = -0.02f,
//                maxTrashStreak = 1,
//                fishWeightMultiplier = 1.02f,
//                shinyRateBonus = 0f,
//                minFishingInterval = 0,
//                maxFishingInterval = 0,
//                fishBagCapacity = 0
//            };

//            _configs[3302] = new FishingComponentConfig
//            {
//                id = 3302,
//                name = "进阶技巧",
//                slotTypeId = 4,
//                trashProbability = -0.03f,
//                maxTrashStreak = 2,
//                fishWeightMultiplier = 1.05f,
//                shinyRateBonus = 0.02f,
//                minFishingInterval = 0,
//                maxFishingInterval = 0,
//                fishBagCapacity = 0,
//                rarityBonus = new Dictionary<int, int> { { 203, 10 }, { 204, 5 } }
//            };

//            _configs[3303] = new FishingComponentConfig
//            {
//                id = 3303,
//                name = "大师技艺",
//                slotTypeId = 4,
//                trashProbability = -0.05f,
//                maxTrashStreak = 3,
//                fishWeightMultiplier = 1.1f,
//                shinyRateBonus = 0.05f,
//                minFishingInterval = 0,
//                maxFishingInterval = 0,
//                fishBagCapacity = 0,
//                rarityBonus = new Dictionary<int, int> { { 204, 15 }, { 205, 8 } }
//            };

//            _configs[3304] = new FishingComponentConfig
//            {
//                id = 3304,
//                name = "传说之路",
//                slotTypeId = 4,
//                trashProbability = -0.08f,
//                maxTrashStreak = 4,
//                fishWeightMultiplier = 1.15f,
//                shinyRateBonus = 0.08f,
//                minFishingInterval = -1,
//                maxFishingInterval = -2,
//                fishBagCapacity = 0,
//                rarityBonus = new Dictionary<int, int> { { 205, 20 }, { 206, 10 } }
//            };

//            _configs[3401] = new FishingComponentConfig
//            {
//                id = 3401,
//                name = "新手渔夫",
//                slotTypeId = 5,
//                trashProbability = -0.03f,
//                maxTrashStreak = 0,
//                fishWeightMultiplier = 1.05f,
//                shinyRateBonus = 0f,
//                minFishingInterval = 0,
//                maxFishingInterval = 0,
//                fishBagCapacity = 0
//            };

//            _configs[3402] = new FishingComponentConfig
//            {
//                id = 3402,
//                name = "钓鱼大师",
//                slotTypeId = 5,
//                trashProbability = -0.08f,
//                maxTrashStreak = 3,
//                fishWeightMultiplier = 1.15f,
//                shinyRateBonus = 0.05f,
//                minFishingInterval = -2,
//                maxFishingInterval = -3,
//                fishBagCapacity = 5,
//                rarityBonus = new Dictionary<int, int> { { 204, 10 }, { 205, 5 } }
//            };

//            Console.WriteLine($"[FishingComponentConfigLoader] 加载默认配置，共 {_configs.Count} 个");
//        }

//        public FishingComponentConfig GetConfig(int equipmentId)
//        {
//            if (!_isLoaded)
//            {
//                LoadConfigs();
//            }

//            _configs.TryGetValue(equipmentId, out var config);
//            return config;
//        }

//        public FishingComponentConfig GetConfigWithLevelScaling(int equipmentId, int level)
//        {
//            var baseConfig = GetConfig(equipmentId);
//            if (baseConfig == null) return null;

//            if (level <= 1) return baseConfig;

//            var scaledConfig = new FishingComponentConfig
//            {
//                id = baseConfig.id,
//                name = baseConfig.name,
//                description = baseConfig.description,
//                rarityId = baseConfig.rarityId,
//                slotTypeId = baseConfig.slotTypeId,
//                trashProbability = baseConfig.trashProbability * level,
//                maxTrashStreak = baseConfig.maxTrashStreak,
//                fishWeightMultiplier = 1f + (baseConfig.fishWeightMultiplier - 1f) * level,
//                shinyRateBonus = baseConfig.shinyRateBonus * level,
//                minFishingInterval = baseConfig.minFishingInterval * level,
//                maxFishingInterval = baseConfig.maxFishingInterval * level,
//                rarityBonus = new Dictionary<int, int>(),
//                rarityWeights = new Dictionary<int, int>(),
//                continuousPauseDuration = baseConfig.continuousPauseDuration,
//                normalPauseDuration = baseConfig.normalPauseDuration,
//                fishBagCapacity = baseConfig.fishBagCapacity + (level - 1)
//            };

//            foreach (var kvp in baseConfig.rarityBonus)
//            {
//                scaledConfig.rarityBonus[kvp.Key] = kvp.Value * level;
//            }

//            foreach (var kvp in baseConfig.rarityWeights)
//            {
//                scaledConfig.rarityWeights[kvp.Key] = kvp.Value * level;
//            }

//            return scaledConfig;
//        }

//        public Dictionary<int, FishingComponentConfig> GetAllConfigs()
//        {
//            if (!_isLoaded)
//            {
//                LoadConfigs();
//            }
//            return new Dictionary<int, FishingComponentConfig>(_configs);
//        }

//        public List<FishingComponentConfig> FromEquipmentInfo(PlayerEquipmentInfo equipment)
//        {
//            var configs = new List<FishingComponentConfig>();

//            if (equipment == null) return configs;

//            if (equipment.rodId > 0)
//            {
//                int itemId = 3000 + equipment.rodId;
//                var rodConfig = GetConfigWithLevelScaling(itemId, equipment.rodLevel);
//                if (rodConfig != null) configs.Add(rodConfig);
//            }

//            if (equipment.lineId > 0)
//            {
//                int itemId = 3100 + equipment.lineId;
//                var lineConfig = GetConfigWithLevelScaling(itemId, equipment.lineLevel);
//                if (lineConfig != null) configs.Add(lineConfig);
//            }

//            if (equipment.hookId > 0)
//            {
//                int itemId = 3200 + equipment.hookId;
//                var hookConfig = GetConfigWithLevelScaling(itemId, equipment.hookLevel);
//                if (hookConfig != null) configs.Add(hookConfig);
//            }

//            if (equipment.skill1Id > 0)
//            {
//                var skill1Config = GetConfigWithLevelScaling(equipment.skill1Id, equipment.skill1Level);
//                if (skill1Config != null) configs.Add(skill1Config);
//            }

//            if (equipment.skill2Id > 0)
//            {
//                var skill2Config = GetConfigWithLevelScaling(equipment.skill2Id, equipment.skill2Level);
//                if (skill2Config != null) configs.Add(skill2Config);
//            }

//            if (equipment.characterId > 0)
//            {
//                var charConfig = GetConfigWithLevelScaling(equipment.characterId, equipment.characterLevel);
//                if (charConfig != null) configs.Add(charConfig);
//            }

//            return configs;
//        }

//        public void Reload()
//        {
//            _isLoaded = false;
//            _configs.Clear();
//            LoadConfigs();
//        }
//    }
//}