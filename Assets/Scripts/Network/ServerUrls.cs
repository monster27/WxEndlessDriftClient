using System;

public static class ServerUrls
{
    //private const string RemoteBaseUrl = "https://81.70.1.164:5001";
    //private const string RemoteBaseUrl = "https://endlessdriftfish.top:5001";
    private const string RemoteBaseUrl = "https://endlessdriftfish.top";


    private const string LocalBaseUrl = "https://localhost:5001";

    private static bool _isLocalMode = false;

    public static bool IsLocalMode => _isLocalMode;

    public static void SetLocalMode(bool isLocal)
    {
        _isLocalMode = isLocal;
        //Debug.Log($"[ServerUrls] 切换到{(isLocal ? "本地" : "远程")}模式: {GetCurrentBaseUrl()}");
    }

    private static string GetCurrentBaseUrl()
    {
        return _isLocalMode ? LocalBaseUrl : RemoteBaseUrl;
    }

    public static string GetFullUrl(string path)
    {
        return GetCurrentBaseUrl() + path;
    }

    public static class Auth
    {
        public const string Login = "/api/auth/login";
        public static string LoginFull => GetFullUrl(Login);
    }

    public static class Player
    {
        public const string Base = "/api/player";
        public static string GetById(int playerId) => $"{Base}/{playerId}";
        public static string Exit(int playerId) => $"{Base}/{playerId}/exit";
        public static string Reconnect(int playerId) => $"{Base}/{playerId}/reconnect";
        public static string Heartbeat(int playerId) => $"{Base}/{playerId}/heartbeat";
        public static string UnlockedEquipment(int playerId) => $"{Base}/{playerId}/unlocked-equipment";

        public const string Inventory = "/api/player/inventory";
        public static string InventoryById(int playerId) => $"{Inventory}/{playerId}";

        public const string Equipment = "/api/player/equipment";
        public static string EquipmentById(int playerId) => $"{Equipment}/{playerId}";
        public static string EquipItem(int playerId, int slotType, int itemId) => $"{Equipment}/{playerId}/{slotType}/equip/{itemId}";
        public static string UnequipItem(int playerId, int slotType) => $"{Equipment}/{playerId}/{slotType}/unequip";

        public const string Character = "/api/player/character";
        public static string CharacterById(int playerId) => $"{Character}/{playerId}";
        public static string Characters(int playerId) => $"/api/player/characters/{playerId}";

        public const string Gold = "/api/player/gold";
        public static string GoldById(int playerId) => $"{Gold}/{playerId}";

        public const string Scene = "/api/player/scene";
        public static string SceneById(int playerId) => $"{Scene}/{playerId}";

        public const string FishBag = "/api/player/fish-bag";
        public static string FishBagById(int playerId) => $"{FishBag}/{playerId}";
        public static string SellFish(int playerId) => $"{FishBag}/{playerId}/sell";
        public static string FishBagLevel(int playerId) => $"{FishBag}/{playerId}/level";
        public static string FishBagUpgrade(int playerId) => $"{FishBag}/{playerId}/upgrade";
        public static string FishBagAutoSellTimer(int playerId) => $"{FishBag}/{playerId}/auto-sell-timer";
        public static string FishBagAutoSellStatus(int playerId) => $"{FishBag}/{playerId}/auto-sell-status";
        public static string SetFishLocked(int playerId) => $"{FishBag}/{playerId}/fish/lock";
        public static string ToggleAutoSell(int playerId) => $"{FishBag}/{playerId}/toggle-auto-sell";
        public static string FishBagFilterConfig(int playerId) => $"{FishBag}/{playerId}/filter-config";

        public const string InventoryAdd = "/api/player/inventory/add";
        public const string CharacterAdd = "/api/player/character/add";
        public const string MallItems = "/api/player/mall/items";
        public const string MallPurchase = "/api/player/mall/purchase";
    }

    public static class Fishing
    {
        public const string Catch = "/api/fishing/catch";
        public const string Status = "/api/fishing/status";
        public static string StatusByPlayerId(int playerId) => $"{Status}?playerId={playerId}";

        public const string AutoStart = "/api/fishing/auto/start";
        public const string AutoStop = "/api/fishing/auto/stop";
        public const string AutoStatus = "/api/fishing/auto/status";
        public static string AutoStatusByPlayerId(int playerId) => $"{AutoStatus}?playerId={playerId}";

        public const string UnlockEquipment = "/api/fishing/unlock-equipment";
    }

    public static class Game
    {
        public const string ContinuousModeStatus = "/api/game/continuous-mode/status";
        public const string ContinuousModeRemainingTime = "/api/game/continuous-mode/remaining-time";
        public const string ContinuousModeEnter = "/api/game/continuous-mode/enter";
        public const string ContinuousModeAddTime = "/api/game/continuous-mode/add-time";
        public const string ContinuousModeUpdate = "/api/game/continuous-mode/update";

        public const string BaitCount = "/api/game/bait/count";
        public static string BaitCountById(int playerId) => $"/api/game/bait/count/{playerId}";
        public static string BaitSet(int playerId) => $"/api/game/bait/set/{playerId}";

        public const string AddBaitTime = "/api/game/add-bait-time";
        public const string EnterContinuousMode = "/api/game/enter-continuous-mode";
        public const string ContinuousModeStatusLegacy = "/api/game/continuous-mode-status";
    }

    public static class Inventory
    {
        public const string FishCapacity = "/api/inventory/fish";
        public static string FishCapacityById(int playerId) => $"{FishCapacity}/{playerId}/capacity";

        public const string FishInventory = "/api/player/fish-inventory";
        public static string FishInventoryById(int playerId) => $"{FishInventory}/{playerId}";

        public const string FishBagCapacity = "/api/player/fish-bag-capacity";
        public static string FishBagCapacityById(int playerId) => $"{FishBagCapacity}/{playerId}";
    }

    public static class Equipment
    {
        public const string Unlock = "/api/equipment/unlock";
        public const string Upgrade = "/api/player/equipment/upgrade";
    }

    public static class Heartbeat
    {
        public const string Ping = "/api/ping";
        public const string HeartbeatApi = "/api/heartbeat";
    }

    public static class Skill
    {
        public const string Unlock = "/api/player/skills/unlock";
        public const string Upgrade = "/api/player/skills/upgrade";
    }
}