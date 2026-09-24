using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 卡牌工厂，用于创建卡牌的实例
/// </summary>
public static class CardFactory
{
    // 键是卡牌ID，值是卡牌配置
    private static Dictionary<string, CardConfig> configCache = null;
    // 键是卡牌ID，值是对应的卡牌类类型（通过反射自动扫描建立）
    private static Dictionary<string, Type> classTypes = null;

    // 每种卡牌的实例
    private static Dictionary<string, Card> cardInstances = new();

    public static void Init()
    {
        if (!configCache.IsNullOrEmpty()) return;

        // 通过反射扫描所有带有CardIdAttribute的类，自动建立映射
        ScanCardTypes();

        configCache = ExcelReader.ReadCardConfig("CardConfig");
        foreach (var cardId in classTypes.Keys)
        {
            if (!configCache.ContainsKey(cardId)) continue;

            cardInstances.Add(cardId, CreateCard(cardId));
        }
    }

    /// <summary>
    /// 扫描程序集中所有带有CardIdAttribute的Card子类，自动建立ID到Type的映射
    /// </summary>
    private static void ScanCardTypes()
    {
        classTypes = new Dictionary<string, Type>();

        // 获取当前程序集中所有类型
        var assembly = Assembly.GetExecutingAssembly();
        var cardBaseType = typeof(Card);

        foreach (var type in assembly.GetTypes())
        {
            // 跳过抽象类和非Card子类
            if (type.IsAbstract || !cardBaseType.IsAssignableFrom(type))
                continue;

            // 获取CardIdAttribute
            var attribute = type.GetCustomAttribute<CardIdAttribute>();
            if (attribute == null)
                continue;

            var cardId = attribute.CardId;
            if (string.IsNullOrEmpty(cardId))
            {
                Debug.LogWarning($"[CardFactory] {type.Name} 的 CardIdAttribute 卡牌ID为空");
                continue;
            }

            if (classTypes.ContainsKey(cardId))
            {
                Debug.LogWarning($"[CardFactory] 卡牌ID '{cardId}' 重复注册: {classTypes[cardId].Name} 和 {type.Name}");
                continue;
            }

            classTypes.Add(cardId, type);
        }

        Debug.Log($"[CardFactory] 扫描完成，共注册 {classTypes.Count} 种卡牌类型");
    }

    /// <summary>
    /// 得到一个静态的卡牌实例
    /// </summary>
    /// <param name="cardId"></param>
    /// <returns></returns>
    public static Card GetStaticCardInstance(string cardId)
    {
        if (cardInstances.ContainsKey(cardId))
        {
            return cardInstances[cardId];
        }
        else
        {
            var card = CreateCard(cardId);
            cardInstances.Add(cardId, card);
            return card;
        }
    }

    /// <summary>
    /// 根据组件类型得到特定的一部分卡牌实例
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static List<Card> GetStaticCardInstancesByComponent<T>() where T : CardComponent
    {
        return cardInstances.Values.Where(c => c.TryGetComponent<T>(out _)).ToList();
    }

    public static bool ContainsCard(string cardId)
    {
        return configCache.ContainsKey(cardId);
    }

    public static Sprite GetCardImage(string cardId, string imagePath = null)
    {
        if (configCache.TryGetValue(cardId, out var config))
        {
            // 获取图集的所有图片
            if (string.IsNullOrEmpty(imagePath))
                imagePath = config.CardImagePath;
            string atlas = config.CardType.ToString();
            if (imagePath.Contains("/"))
            {
                var parts = imagePath.Split('/');
                atlas = parts[0]; imagePath = parts[1];
            }
            var sprites = Resources.LoadAll<Sprite>("Sprites/" + atlas);

            // 找到图片的索引
            if (int.TryParse(imagePath, out var index) && index >= 0 && index < sprites.Length)
            {
                return sprites[index];
            }
            return null;
        }
        throw new ArgumentException($"不存在ID为{cardId}的卡牌");
    }

    public static bool GetIsBigIcon(string cardId)
    {
        if (configCache.TryGetValue(cardId, out var config))
        {
            return config.IsBigIcon;
        }
        throw new ArgumentException($"不存在ID为{cardId}的卡牌");
    }

    public static string GetCardName(string cardId)
    {
        if (configCache.TryGetValue(cardId, out var config))
        {
            return config.CardName;
        }
        throw new ArgumentException($"不存在ID为{cardId}的卡牌");
    }

    public static string GetCardDesc(string cardId)
    {
        if (configCache.TryGetValue(cardId, out var config))
        {
            return config.CardDesc;
        }
        throw new ArgumentException($"不存在ID为{cardId}的卡牌");
    }

    public static CardTextureType GetCardTextureType(string cardId)
    {
        if (configCache.TryGetValue(cardId, out var config))
        {
            return config.TextureType;
        }
        throw new ArgumentException($"不存在ID为{cardId}的卡牌");
    }

    public static CardType GetCardType(string cardId)
    {
        if (configCache.TryGetValue(cardId, out var config))
        {
            return config.CardType;
        }
        throw new ArgumentException($"不存在ID为{cardId}的卡牌");
    }

    public static int GetMaxStackNum(string cardId)
    {
        if (configCache.TryGetValue(cardId, out var config))
        {
            return config.MaxStackNum;
        }
        throw new ArgumentException($"不存在ID为{cardId}的卡牌");
    }

    public static bool GetMoveable(string cardId)
    {
        if (configCache.TryGetValue(cardId, out var config))
        {
            return config.Moveable;
        }
        throw new ArgumentException($"不存在ID为{cardId}的卡牌");
    }

    public static float GetWeight(string cardId)
    {
        if (configCache.TryGetValue(cardId, out var config))
        {
            return config.Weight;
        }
        throw new ArgumentException($"不存在ID为{cardId}的卡牌");
    }

    public static List<CardTag> GetTags(string cardId)
    {
        if (configCache.TryGetValue(cardId, out var config))
        {
            return config.Tags;
        }
        throw new ArgumentException($"不存在ID为{cardId}的卡牌");
    }

    public static string GetExtraInfo(string cardId)
    {
        if (configCache.TryGetValue(cardId, out var config))
        {
            return config.CardExtraInfo;
        }
        throw new ArgumentException($"不存在ID为{cardId}的卡牌");
    }

    // Restore only a newly introduced component in trial saves; preserve any existing cooking progress.
    public static void RestoreMissingCookComponent(Card card)
    {
        if (card.TryGetComponent<CookComponent>(out _) || !configCache.TryGetValue(card.CardId, out var config) || !config.CanCook) return;
        card.AddComponent(new CookComponent(config.CookTime, config.OutcomeCardId));
        card.AssignComponentValues();
    }

    public static Card CreateCard(string cardId)
    {
        // 从缓存中获取配置
        if (!configCache.TryGetValue(cardId, out var config))
        {
            throw new ArgumentException($"[CardFactory] 配置表中不存在ID为 '{cardId}' 的卡牌");
        }
        
        if (!classTypes.TryGetValue(cardId, out var classType))
        {
            throw new ArgumentException($"[CardFactory] 未找到ID为 '{cardId}' 的卡牌类，请确保对应的类已添加 [CardId(\"{cardId}\")] 特性");
        }

        // 创建卡牌实例
        Card card = Activator.CreateInstance(classType, true) as Card;

        // 配置基础属性
        card.SetCardId(cardId);

        // 配置可变属性
        if (config.HasFreshness)
        {
            card.AddComponent(new FreshnessComponent(config.MaxFreshness));
        }
        if (config.HasDurability)
        {
            card.AddComponent(new DurabilityComponent(config.MaxDurability));
        }
        if (config.HasGrowth)
        {
            card.AddComponent(new GrowthComponent(config.MaxGrowth));
        }
        if (config.HasProgress)
        {
            card.AddComponent(new ProgressComponent(config.MaxProgress));
        }
        if (config.IsEquipment)
        {
            card.AddComponent(new EquipmentComponent(config.EquipmentType));
        }
        if (config.IsTool)
        {
            card.AddComponent(new ToolComponent(config.ToolTypes));
        }
        if (config.HasInnerContents)
        {
            card.AddComponent(new InnerContentsComponent(config.InnerContentSlotCount, config.IsCraftMaterialSource));
        }
        if (config.IsFuel)
        {
            card.AddComponent(new FuelComponent(config.FuelValue));
        }
        if (config.HasFuelStorage)
        {
            card.AddComponent(new FuelStorageComponent(config.FuelStorageCapacity));
        }
        if (config.IsPassage)
        {
            card.AddComponent(new PassageComponent(config.TargetPlace, config.MoveTime, config.InteractAudio));
        }
        if (config.CanCook)
        {
            card.AddComponent(new CookComponent(config.CookTime, config.OutcomeCardId));
        }
        if (config.IsConstruction)
        {
            card.AddComponent(new ConstructionComponent(config.OnlyInWater, config.OnlyOutWater, config.OnlyInDoor, config.OnlyOutDoor, config.NeedCable, config.CanBeDemolished, config.DemolitionDebris));
        }
        if (config.HasFoodProperty)
        {
            card.AddComponent(new FoodPropertyComponent(config.FoodPropertyDict));
        }
        if (config.IsPlant)
        {
            card.AddComponent(new PlantGrowthComponent(config.GrowthRate, config.MinConfortTempreture, config.MaxConfortTempreture, config.MinGrowTempture, config.MaxGrowTempture, config.MinLiveTempture, config.MaxLiveTempture, config.DeadcardName, config.Pressures));
        }
        if (config.HasCoordinate)
        {
            card.AddComponent(new CoordinateComponent(config.Position));
        }
        if (config.IsWeapon)
        {
            card.AddComponent(new WeaponComponent(config.WeaponAtk, config.MinAtkDist, config.MaxAtkDist, config.AtkForm, config.AtkTime, config.AtkSound));
        }
        if (config.IsEntity)
        {
            card.AddComponent(new EntityComponent(config.MaxHealth, config.EntityAtk, config.MoveDistPerMin, config.AIRefreshInterval, config.BehavioralTendency, config.DeadDrops));
        }
        if (config.HasMultipleStates)
        {
            card.AddComponent(new StateMachineComponent(config.States));
        }

        card.LateConstrcutor();

        return card;
    }

    public static List<Card> CreateCards(string cardId, int num)
    {
        List<Card> cards = new();
        for (int i = 0; i < num; i++)
        {
            cards.Add(CreateCard(cardId));
        }
        return cards;
    }

    public static Card DeepCopyCard(Card card)
    {
        var copied = JsonManager.DeepCopy(card);
        copied.AssignComponentValues();
        return copied;
    }
}
