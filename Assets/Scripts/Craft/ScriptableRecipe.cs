using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 制作配方类型
/// </summary>
public enum RecipeType
{
    Food = 0,
    Oxygen = 1,
    Medic = 2,
    Tool = 3,
    Material = 4,
    Equipment = 5,
    Construction = 6,
    Combat = 7,
}

[Serializable]
public class RecipeMaterial
{
    public string cardId;
    public int requiredNum;
    public Sprite CardImage => CardFactory.GetCardImage(cardId);

    public Card CardInstance => CardFactory.GetStaticCardInstance(cardId);

    public RecipeMaterial(string cardId, int requiredNum)
    {
        this.cardId = cardId;
        this.requiredNum = requiredNum;
    }
}


[CreateAssetMenu(fileName = "Recipe", menuName = "ScriptableObject/Recipe")]
public class ScriptableRecipe : ScriptableObject
{
    public string cardId; // 制作出来的卡牌
    public RecipeType craftType; // 配方类型
    public List<RecipeMaterial> materials; // 制作需要的材料
    public int craftTime; // 制作时间
    [Min(1)] public int outputCount = 1;

    public Card CardInstance => CardFactory.GetStaticCardInstance(cardId);

    public Sprite CardImage => CardFactory.GetCardImage(cardId);

    private void OnValidate()
    {
        cardId = name;
    }
}
