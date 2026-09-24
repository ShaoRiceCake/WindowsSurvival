using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class CraftWindow : WindowBase
{
    [SerializeField] private Transform recipeLibraryLayout;
    [SerializeField] private Transform recipeLayout;
    [SerializeField] private Transform materialLayout;
    [SerializeField] private CardSlot slot;
    [SerializeField] private Text craftTimeText;
    [SerializeField] private CraftButton craftButton;
    [SerializeField] private RectTransform recipeLibrarySelectRect; // 配方库选择框
    [SerializeField] private RectTransform recipeItemSelectRect; // 配方选择框
    [SerializeField] private RectTransform limitationLayout; // 建筑放置限制
    [SerializeField] private GameObject cannotMove; // 不能移动的标志

    [SerializeField] private GameObject recipeMaterialPrefab;
    [SerializeField] private GameObject recipeItemPrefab;

    private RecipeType currentRecipeType; // 记录当前选择的配方库
    private ScriptableRecipe currentSelectedRecipe; // 记录当前选中的配方

    private Dictionary<RecipeType, RectTransform> recipeLibraryItemTransforms = new(); // 记录配方库图标的位置
    private Dictionary<string, RectTransform> recipeItemTransforms = new(); // 记录配方图标的位置

    protected override void Awake()
    {
        base.Awake();

        // 注册背包变化事件
        EventManager.Instance.AddListener<AddRemoveCardArgs>(EventType.AddRemoveCard, RefreshDisplay);
        EventManager.Instance.AddListener(EventType.ChangeDisplayedCard, RefreshDisplay);
        EventManager.Instance.AddListener<EnvironmentBag>(EventType.ChangeCurrentEnvironment, RefreshDisplay);
        EventManager.Instance.AddListener(EventType.UnlockRecipe, RefreshDisplay);
        // 触发或结束制作激励事件时，刷新显示
        EventManager.Instance.AddListener<GameEvent>(EventType.GameEventBegin, OnCraftIncentiveBeginEnd);
        EventManager.Instance.AddListener<GameEvent>(EventType.GameEventEnd, OnCraftIncentiveBeginEnd);

        // 建筑制作限制
        foreach (Transform child in limitationLayout)
        {
            child.gameObject.AddComponent<HoverTipController>().SetTip(child.GetComponentInChildren<Text>(true).text);
        }
        cannotMove.AddComponent<HoverTipController>().SetTip(cannotMove.GetComponentInChildren<Text>(true).text);
        cannotMove.SetActive(false);
        limitationLayout.gameObject.SetActive(false);

        // 添加制作事件
        craftButton.onClick.AddListener(CraftCard);

        // 显示左侧配方库
        currentRecipeType = (RecipeType)Enum.Parse(typeof(RecipeType), recipeLibraryLayout.GetChild(0).name);
        DisplayRecipeLibraries();
    }

    private void OnDestroy()
    {
        EventManager.Instance.RemoveListener<AddRemoveCardArgs>(EventType.AddRemoveCard, RefreshDisplay);
        EventManager.Instance.RemoveListener(EventType.ChangeDisplayedCard, RefreshDisplay); // 详情窗口显示的卡牌改变时触发
        EventManager.Instance.RemoveListener<EnvironmentBag>(EventType.ChangeCurrentEnvironment, RefreshDisplay);
        EventManager.Instance.RemoveListener(EventType.UnlockRecipe, RefreshDisplay);
        EventManager.Instance.RemoveListener<GameEvent>(EventType.GameEventBegin, OnCraftIncentiveBeginEnd);
        EventManager.Instance.RemoveListener<GameEvent>(EventType.GameEventEnd, OnCraftIncentiveBeginEnd);
    }

    protected override void Init()
    {
        DisplayRecipesByType(currentRecipeType);
    }

    private void RefreshDisplay(AddRemoveCardArgs args)
    {
        var sourceBags = CraftManager.Instance.GetCraftMaterialSourceBags();

        if (!sourceBags.Contains(args.AffectedBag))
        {
            // 不是来自可作为材料来源的背包，不刷新显示
            return;
        }

        if (sourceBags.Contains(args.fromBag) && sourceBags.Contains(args.toBag))
        {
            // 在可作为材料来源的背包之间转移卡牌，不刷新显示
            return;
        }

        RefreshDisplay();
    }

    private void RefreshDisplay(EnvironmentBag env) => RefreshDisplay();

    private void RefreshDisplay() => DisplayRecipesByType(currentRecipeType, true); // 传递true表示是刷新操作

    private void OnCraftIncentiveBeginEnd(GameEvent gameEvent)
    {
        if (gameEvent.GetType() != typeof(CraftIncentive)) return;

        RefreshDisplay();
    }

    /// <summary>
    /// 显示配方类别
    /// </summary>
    private void DisplayRecipeLibraries()
    {
        for (int i = 0; i < recipeLibraryLayout.childCount; i++)
        {
            var button = recipeLibraryLayout.GetChild(i).GetComponent<HoverableButton>();
            button.onClick.RemoveAllListeners();
            RecipeType type = (RecipeType)Enum.Parse(typeof(RecipeType), button.name);

            // 记录配方库图标的位置
            recipeLibraryItemTransforms.Add(type, button.transform as RectTransform);

            button.onClick.AddListener(() =>
            {
                currentRecipeType = type;
                currentSelectedRecipe = null; // 切换类型时清空选中记录
                DisplayRecipesByType(type);
            });
        }
    }

    /// <summary>
    /// 显示某一类的所有配方
    /// </summary>
    /// <param name="recipeType"></param>
    /// <param name="isRefresh"></param>
    private void DisplayRecipesByType(RecipeType recipeType, bool isRefresh = false)
    {
        currentRecipeType = recipeType;

        // 清空位置记录字典
        recipeItemTransforms.Clear();

        ObjectBufferPool.Instance.RestoreAllChildren(recipeLayout);

        // 获取当前类型的配方列表
        var recipes = CraftManager.Instance.LibraryDict[recipeType].recipes;

        // 对配方进行排序：可合成 > 不可合成 > 未解锁
        var sortedRecipes = recipes.OrderBy(recipe =>
        {
            if (CraftManager.Instance.IsRecipeLocked(recipe))
            {
                return 2; // 未解锁的排在最后
            }
            else if (!CraftManager.Instance.CanCrfat(recipe, out _, out _))
            {
                return 1; // 不可合成的排在中间
            }
            else
            {
                return 0; // 可合成的排在最前
            }
        }).ToList();

        UIRecipeItem recipeItem;
        // 创建所有配方按钮
        foreach (var recipe in sortedRecipes)
        {
            recipeItem = ObjectBufferPool.Instance.Get(recipeItemPrefab, recipeLayout).GetComponent<UIRecipeItem>();
            recipeItem.DisplayRecipe(
                recipe.CardImage,
                CraftManager.Instance.IsRecipeLocked(recipe),
                CraftManager.Instance.CanCrfat(recipe, out _, out _)
                );
            recipeItem.button.onClick.RemoveAllListeners();
            recipeItem.button.onClick.AddListener(() =>
            {
                currentSelectedRecipe = recipe; // 记录选中的配方
                DisplayRecipeDetails(recipe);
            });

            recipeItem.transform.SetAsLastSibling();

            // 记录配方的位置
            recipeItemTransforms.Add(recipe.cardId, recipeItem.transform as RectTransform);
        }

        MonoUtility.UpdateLayoutSize(recipeLayout.GetComponent<GridLayoutGroup>());

        // 如果是刷新，继续选中上一个选中的配方
        if (isRefresh)
            DisplayRecipeDetails(currentSelectedRecipe);
        else if (currentSelectedRecipe == null)
        {
            currentSelectedRecipe = sortedRecipes[0];
            DisplayRecipeDetails(sortedRecipes[0]);
        }

        // 播放选择动效
        SelectRecipeLibraryWithTween(recipeType);
    }

    private void SelectRecipeLibraryWithTween(RecipeType type)
    {

        LayoutRebuilder.ForceRebuildLayoutImmediate(recipeLibraryLayout as RectTransform);

        Vector2 targetPos = new(recipeLibrarySelectRect.anchoredPosition.x, recipeLibraryItemTransforms[type].anchoredPosition.y);

        // 播放高亮框移动动画
        AnimationManager.Instance.PlayAnchorMove(recipeLibrarySelectRect, targetPos);
    }

    /// <summary>
    /// 显示具体的配方信息
    /// </summary>
    /// <param name="recipe"></param>
    private void DisplayRecipeDetails(ScriptableRecipe recipe)
    {
        currentSelectedRecipe = recipe;

        ObjectBufferPool.Instance.RestoreAllChildren(materialLayout);

        // 显示卡牌
        slot.Clear();
        slot.DisplayCard(recipe.CardInstance, Mathf.Max(1, recipe.outputCount), recipe.outputCount > 1);

        slot.GetComponentInChildren<HoverableButton>().onClick.RemoveAllListeners();
        slot.GetComponentInChildren<HoverableButton>().onClick.AddListener(() =>
        {
            (WindowsManager.Instance.OpenWindow("Details") as DetailsWindow).Display(recipe.CardInstance, DisplayType.OnlyDetails);
        });

        UIRecipeMaterial recipeMaterial;
        // 显示所需材料
        foreach (var material in CraftManager.Instance.GetMaterials(recipe))
        {
            recipeMaterial = ObjectBufferPool.Instance.Get(recipeMaterialPrefab, materialLayout).GetComponent<UIRecipeMaterial>();
            recipeMaterial.DisplayMaterial(
                material.CardImage,
                material.requiredNum,
                CraftManager.Instance.GetTotalCraftMaterialCount(material.cardId)
                );

            recipeMaterial.tipController.SetTip(material.CardInstance.CardName);
            recipeMaterial.button.onClick.RemoveAllListeners();
            recipeMaterial.button.onClick.AddListener(() =>
            {
                (WindowsManager.Instance.OpenWindow("Details") as DetailsWindow).Display(material.CardInstance, DisplayType.OnlyDetails);
            });

            recipeMaterial.transform.SetAsLastSibling();
        }

        // 显示制作时间
        var craftTime = CraftManager.Instance.GetCraftTime(recipe);
        int hour = craftTime / 60;
        int minute = craftTime % 60;
        StringBuilder sb = new();
        sb.Append(hour > 0 ? $"{hour}h" : "");
        sb.Append(minute > 0 ? $"{minute}min" : "");
        craftTimeText.text = sb.ToString();

        // 不可制作的卡牌，卡牌槽变灰
        bool canCraft = CraftManager.Instance.CanCrfat(recipe, out var limitations, out var hint);
        slot.GetComponent<CanvasGroup>().alpha = canCraft ? 1f : 0.14f;

        // 显示制作按钮
        craftButton.DisplayButton(CraftManager.Instance.IsRecipeLocked(recipe), canCraft, hint);

        // 显示放置限制
        if (limitations != null && limitations.Count > 0)
        {
            limitationLayout.gameObject.SetActive(true);

            foreach (Transform child in limitationLayout)
            {
                if (limitations.TryGetValue(child.name, out bool met))
                {
                    child.gameObject.SetActive(true);
                    var color = met ? ColorManager.White : ColorManager.DarkGrey;
                    var button = child.GetComponent<HoverableButton>();
                    button.currentColor = color;
                    button.ChangeColor(color);
                }
                else
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            limitationLayout.gameObject.SetActive(false);
        }

        // 显示能否移动
        cannotMove.SetActive(!recipe.CardInstance.Moveable);

        // 播放选择动效
        SelectRecipeWithTween(recipe.cardId);
    }

    private void SelectRecipeWithTween(string cardId)
    {
        Vector2 targetPos = recipeItemTransforms[cardId].anchoredPosition;

        // 播放高亮框移动动画
        AnimationManager.Instance.PlayAnchorMove(recipeItemSelectRect, targetPos);
    }

    private void CraftCard()
    {
        if (currentSelectedRecipe == null) return;

        // 制作成功，掉落卡牌
        void CraftSucceeded(List<Card> outcomeCards)
        {
            AnimationManager.Instance.PlayDropCards(slot.transform, () =>
            {
                SoundManager.Instance.PlaySound("制作_03", true);

                // 掉落制作出的卡牌
                // 如果是建筑卡牌或者是有内容物的卡牌，则优先掉落到环境里
                var outcomeCard = outcomeCards[0];
                var toPlayerBag = outcomeCard.CardType != CardType.Construction && !outcomeCard.TryGetComponent<InnerContentsComponent>(out _);
                GameManager.Instance.AddCardsWithTween(outcomeCards, toPlayerBag, slot.transform.position);

                // 刷新显示
                RefreshDisplay();
            });
        }

        // 制作失败，返还材料
        void CraftFailed(List<Card> toReturn)
        {
            AnimationManager.Instance.ShowFloatingTipAbove(slot.transform, "制作中断了！");
            SoundManager.Instance.PlaySound("错误提示");
            GameManager.Instance.AddCardsWithTween(toReturn, true, slot.transform.position);

            // 刷新显示
            RefreshDisplay();
        }
        
        // 合成卡牌
        CraftManager.Instance.Craft(currentSelectedRecipe, CraftSucceeded, CraftFailed);
    }

    public void DisplayRecipe(string cardId)
    {
        foreach (var (type, library) in CraftManager.Instance.LibraryDict)
        {
            foreach (var recipe in library.recipes)
            {
                // 找到id对应的配方
                if (recipe.cardId == cardId)
                {
                    DisplayRecipesByType(type);
                    DisplayRecipeDetails(recipe);
                    return;
                }
            }
        }
    }
}
