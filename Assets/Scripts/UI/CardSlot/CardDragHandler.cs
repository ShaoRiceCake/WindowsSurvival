using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler,
                                IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    // 动画管理器引用
    private AnimationManager Anim => AnimationManager.Instance;

    // 拖拽移动插值系数
    private float followSpeed = 30f;
    private Vector2 dragPosOffset;

    private CardSlot sourceSlot;

    private CardSlot cursorSlot;

    private int pickedCount;

    private Vector3 dragEndPosition;

    private bool isDragging;

    private void Awake()
    {
        sourceSlot = GetComponentInParent<CardSlot>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;

        MouseManager.Instance.StartDragging();

        // 创建临时卡牌槽
        cursorSlot = Anim.CreateTempSlot(sourceSlot.transform.position);
        dragPosOffset = (Vector2)cursorSlot.transform.position - Anim.ScreenToCanvasPosition(eventData.position);

        // 计算拖动数量
        if (IsLeftButtonPressed(eventData))
        {
            // ctrl + 左键 = 拖动半组
            if (IsCtrlPressed())
                pickedCount = Mathf.CeilToInt((float)sourceSlot.StackNum / 2);
            // 左键拖动一组
            else
                pickedCount = sourceSlot.StackNum;
        }
        else
        {
            // 右键拖拽
            pickedCount = 1;
        }

        var card = sourceSlot.PeekCard();
        cursorSlot.DisplayCard(card, pickedCount);

        // 更新源卡槽显示
        if (sourceSlot.StackNum - pickedCount > 0)
            sourceSlot.DisplayCard(sourceSlot.Cards[pickedCount], sourceSlot.StackNum - pickedCount);
        else
            sourceSlot.Clear();

        // 让sourceSlot暂时不要刷新显示
        sourceSlot.DontRefresh = true;

        SoundManager.Instance.PlaySound(Anim.GetCardPickSoundName(card.TextureType), true);

        EventManager.Instance.TriggerEvent(EventType.PickUpCard, card);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 不能移除此方法，否则无法拖拽卡牌
    }

    Vector2 mousePosition;
    private void Update()
    {
        if (isDragging)
        {
            mousePosition = Anim.ScreenToCanvasPosition(Input.mousePosition);

            cursorSlot.transform.position = Vector3.Lerp(
                cursorSlot.transform.position,
                mousePosition + dragPosOffset,
                followSpeed * Time.deltaTime
            );
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;

        MouseManager.Instance.EndDragging();

        dragEndPosition = cursorSlot.transform.position;
        ObjectBufferPool.Instance.Restore(cursorSlot.gameObject);

        var currentObject = eventData.pointerCurrentRaycast.gameObject;
        if (currentObject == null)
        {
            AnimateCardReturn(pickedCount);
            EventManager.Instance.TriggerEvent(EventType.PutDownCard);
            return;
        }

        // 处理快捷交互
        var modification = currentObject.GetComponentInParent<ModificationRow>();
        if (modification != null)
        {
            int before = sourceSlot.StackNum;
            modification.HandleDrop(sourceSlot.Cards, pickedCount, out string hint);
            int remaining = pickedCount - (before - sourceSlot.StackNum);
            if (remaining > 0) AnimateCardReturn(remaining, hint);
            else sourceSlot.DontRefresh = false;
            EventManager.Instance.TriggerEvent(EventType.PutDownCard);
            return;
        }
        var targetSlot = currentObject.GetComponentInParent<CardSlot>();
        if (targetSlot != null && targetSlot.Interactable)
        {
            HandleQuickInteract(targetSlot);
            EventManager.Instance.TriggerEvent(EventType.PutDownCard);
            return;
        }

        BagWindow targetWindow = currentObject.GetComponentInParent<BagWindow>();
        BagWindow sourceWindow = sourceSlot.GetComponentInParent<BagWindow>();

        // 能够放置
        if (targetWindow != null && targetWindow.Bag != null)
        {
            // 同背包放置
            if (targetWindow == sourceWindow)
            {
                // 不能在装备背包里转移卡牌
                if (targetWindow is EquipmentWindow)
                {
                    AnimateCardReturn(pickedCount);
                }
                // 放在同背包的不同格子里
                else if (targetSlot != null && targetSlot != sourceSlot)
                {
                    PlaceCardInSameBag(targetSlot, pickedCount);
                }
                // 放在同背包的相同格子里
                else
                {
                    AnimateCardReturn(pickedCount);
                }
            }
            // 跨背包放置
            else if (targetWindow.Bag is not EnvironmentBag && !sourceSlot.PeekCard().Moveable)
            {
                AnimateCardReturn(pickedCount, "不能移动该卡牌");
            }
            else if (sourceWindow.Bag is InnerBag s && !s.AllowRemove)
            {
                AnimateCardReturn(pickedCount, string.IsNullOrEmpty(s.NotAllowRemoveReason) ? "不能取出卡牌" : s.NotAllowRemoveReason);
            }
            else if (targetWindow.Bag is InnerBag t && !t.AllowAdd)
            {
                AnimateCardReturn(pickedCount, string.IsNullOrEmpty(t.NotAllowAddReason) ? "不能放入卡牌" : t.NotAllowAddReason);
            }
            else if (targetWindow is DetailsWindow dw && dw.CurrentDisplayPart != "内容物")
            {
                AnimateCardReturn(pickedCount);
            }
            else
            {
                PlaceCardInDifferentBag(targetWindow.Bag, ref pickedCount, dragEndPosition);
            }
        }
        // 不能放置
        else
        {
            AnimateCardReturn(pickedCount);
        }

        EventManager.Instance.TriggerEvent(EventType.PutDownCard);
    }

    private void HandleQuickInteract(CardSlot targetSlot)
    {
        var left = sourceSlot.StackNum - pickedCount;
        targetSlot.PeekCard().QuickIneract(sourceSlot.Cards, pickedCount);
        var toReturn = sourceSlot.StackNum - left; // toReturn一定>=0
        if (toReturn > 0)
            AnimateCardReturn(toReturn);
        else
            sourceSlot.DontRefresh = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 左键点击
        if (IsLeftButtonPressed(eventData))
        {
            // shift + 左键 = 快速移动一组
            if (IsShiftPressed())
                HandleQuickMove(sourceSlot.StackNum);
            // ctrl + 左键 = 快速移动半组
            else if (IsCtrlPressed())
                HandleQuickMove(Mathf.CeilToInt((float)sourceSlot.StackNum / 2));
            // 打开详情
            else
            {
                Anim.PlayBounce(transform, duration: 0.05f); // 卡牌的 bounce 效果
                (WindowsManager.Instance.OpenWindow("Details") as DetailsWindow).Display(sourceSlot.Cards);
            }

            return;
        }
        // 右键点击
        if (IsRightButtonPressed(eventData))
        {
            // 快速移动一个
            HandleQuickMove(1);
            return;
        }
    }

    /// <summary>
    /// 处理卡牌的快速移动
    /// </summary>
    private void HandleQuickMove(int count)
    {
        // 不可移动的卡牌
        var card = sourceSlot.PeekCard();
        if (!card.Moveable)
        {
            Anim.ShowFloatingTipAbove(sourceSlot.transform, "不能移动该卡牌");
            return;
        }

        var sourceBag = sourceSlot.GetComponentInParent<BagWindow>().Bag;

        // 对于装备卡牌，快速穿上和脱下
        if (card.TryGetComponent<EquipmentComponent>(out var equ))
        {
            if (equ.isEquipped)
                GameManager.Instance.Unequip(card);
            else if (GameManager.Instance.CanEquip(card, out string tip))
                GameManager.Instance.Equip(card, card.Slot.transform.position);
            else
                Anim.ShowFloatingTipAbove(sourceSlot.transform, tip);
        }
        // 从内容物中快速移出
        else if (sourceBag is InnerBag innerBag)
        {
            // 不允许取出内容物的情况
            if (!innerBag.AllowRemove)
            {
                Anim.ShowFloatingTipAbove(sourceSlot.transform, string.IsNullOrEmpty(innerBag.NotAllowRemoveReason) ? "不能取出卡牌" : innerBag.NotAllowRemoveReason);
                return;
            }

            // 先尝试移入玩家背包
            if (WindowsManager.Instance.IsWindowOpen("PlayerBag"))
            {
                PlaceCardInDifferentBag(GameManager.Instance.PlayerBag, ref count, sourceSlot.transform.position, false);
                sourceSlot.RefreshDisplay();
            }

            // 再移入地点
            if (WindowsManager.Instance.IsWindowOpen("EnvironmentBag"))
            {
                PlaceCardInDifferentBag(GameManager.Instance.CurEnvironmentBag, ref count, sourceSlot.transform.position, false);
                sourceSlot.RefreshDisplay();
            }
        }
        // 在背包和地点之间移动
        else if (sourceBag is PlayerBag)
        {
            if (WindowsManager.Instance.IsWindowOpen("EnvironmentBag"))
            {
                PlaceCardInDifferentBag(GameManager.Instance.CurEnvironmentBag, ref count, sourceSlot.transform.position, false);
                sourceSlot.RefreshDisplay();
            }
        }
        else if (sourceBag is EnvironmentBag)
        {
            if (WindowsManager.Instance.IsWindowOpen("PlayerBag"))
            {
                PlaceCardInDifferentBag(GameManager.Instance.PlayerBag, ref count, sourceSlot.transform.position, false);
                sourceSlot.RefreshDisplay();
            }
        }

        if (sourceSlot.IsEmpty)
        {
            transform.DOKill();
            transform.localScale = Vector3.one;
        }
    }

    private bool IsShiftPressed()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    private bool IsCtrlPressed()
    {
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    }

    private bool IsLeftButtonPressed(PointerEventData eventData)
    {
        return eventData.button == PointerEventData.InputButton.Left;
    }

    private bool IsRightButtonPressed(PointerEventData eventData)
    {
        return eventData.button == PointerEventData.InputButton.Right;
    }

    /// <summary>
    /// 放置卡牌动画
    /// </summary>
    /// <param name="placementAction"></param>
    /// <param name="startPos"></param>
    /// <param name="endPos"></param>
    /// <param name="count"></param>
    private void AnimateCardPlacement(Card card, Vector3 startPos, int count)
    {
        var targetSlot = card.Slot;
        targetSlot.DontRefresh = true;
        Anim.PlayCardMove(
            card,
            count,
            startPos,
            onComplete: () =>
            {
                sourceSlot.DontRefresh = false;
                targetSlot.DontRefresh = false;
            }
        );
    }

    /// <summary>
    /// 播放卡牌返回动画
    /// </summary>
    private void AnimateCardReturn(int count, string tip = "")
    {
        Anim.PlayCardMove(
            sourceSlot.PeekCard(),
            count,
            dragEndPosition,
            onComplete: () =>
            {
                // 刷新源卡槽显示
                sourceSlot.DontRefresh = false;
                // 显示提示
                if (!string.IsNullOrEmpty(tip))
                    Anim.ShowFloatingTipAbove(sourceSlot.transform, tip);
            }
        );
    }

    /// <summary>
    /// 同背包放置
    /// </summary>
    /// <param name="targetSlot"></param>
    /// <param name="count"></param>
    private void PlaceCardInSameBag(CardSlot targetSlot, int count)
    {
        List<Card> movedCard = new();
        for (int i = 0; i < count; i++)
        {
            if (!targetSlot.CanAddCard(sourceSlot.PeekCard())) break;
            var toMove = sourceSlot.RemoveCard();
            targetSlot.AddCard(toMove);
            movedCard.Add(toMove);
        }

        if (movedCard.Count > 0)
        {
            AnimateCardPlacement(
                movedCard[0],
                dragEndPosition,
                movedCard.Count
            );
        }

        int leftCount = count - movedCard.Count;
        if (leftCount > 0)
            AnimateCardReturn(leftCount);
    }

    /// <summary>
    /// 跨背包放置
    /// </summary>
    /// <param name="targetBag"></param>
    /// <param name="count"></param>
    /// <param name="startPos"></param>
    private void PlaceCardInDifferentBag(Bag targetBag, ref int count, Vector3 startPos, bool needReturnAnim = true)
    {
        string tip = string.Empty;
        List<Card> movedCard = new();
        for (int i = 0; i < count; i++)
        {
            if (!targetBag.CanAddCard(sourceSlot.PeekCard(), out tip)) break;
            var toMove = sourceSlot.RemoveCard();
            targetBag.AddCard(toMove);
            movedCard.Add(toMove);
        }

        if (movedCard.Count > 0)
        {
            // 将移动了的卡牌按照slot进行分组
            var groups = movedCard.GroupBy(c => c.Slot);

            foreach (var group in groups)
            {
                AnimateCardPlacement(
                    group.ToList()[0],
                    startPos,
                    group.Count()
                );
            }
        }

        int leftCount = count - movedCard.Count;
        if (leftCount > 0 && needReturnAnim)
            AnimateCardReturn(leftCount, tip);

        count = leftCount;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Anim.PlayHoverEnter(transform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Anim.PlayHoverExit(transform);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging)
            Anim.PlayPointerUp(transform);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Anim.PlayPointerDown(transform);
    }
}
