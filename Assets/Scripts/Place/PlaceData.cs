using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlaceData", menuName = "ScriptableObject/PlaceData")]
public class PlaceData : ScriptableObject
{
    public string placeName;                        // 地点名称
    public string placeDesc;                        // 地点描述
    public PlaceEnum placeType;                     // 地点类型
    public bool isIndoor;                           // 是否是室内
    public bool isInWater;                          // 是否是水域
    public bool isInSpacecraft;                     // 是否在飞船内
    public bool isInCave;                           // 是否在洞穴内
    public Sprite placeImage;                       // 地点图片
    public int exploreTime;                         // 探索时间(分钟)
    [HideInInspector] public float minCoord = 0;    // 最小坐标
    public float maxCoord;                          // 最大坐标
    public PlaceEnum connectedOutdoorPlace;         // 连接的户外地点
    public float sunlightInfluenceFactor;           // 受光照影响程度

    [Header("环境温度与改装")]
    [Range(0, 5)] public int insulationLevel;
    [Range(0, 1)] public float seasonInfluence = 1;
    [Range(0, 1)] public float dayInfluence = 1;
    public bool supportsTowRope;
    public bool supportsCable;
    public bool supportsInsulation;
    [Tooltip("该地点可探索到的极寒材料，留空表示无额外掉落。")]
    public string climateMaterialId;
    [Min(0)] public int climateMaterialCaches = 3;
    [Min(1)] public int climateMaterialPerCache = 3;

    public InitialBagStateConfig initialBagStateConfig; // 初始背包状态配置

    private void OnValidate()
    {
        placeName = name;
    }
}

[Serializable]
public class InitialBagStateConfig
{
    public List<string> containedCards;     // 初始包含的卡牌列表
    public bool hasCable;                   // 是否有电缆
    public PressureLevel pressureLevel;     // 初始压力等级
    public float brightness;                // 基础亮度
    public float roomTemperature = 18;      // 初始环境温度（摄氏度）
}
