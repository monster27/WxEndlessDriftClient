using System;

namespace SharedModels
{
    [Serializable]
    public class PlayerFishingComponent
    {
        public int id;
        public int level = 1;
        public FishingComponentConfig config;
    }
}