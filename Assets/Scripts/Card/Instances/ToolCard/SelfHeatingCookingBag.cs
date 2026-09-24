/// <summary>
/// 自热烹饪袋
/// </summary>
[CardId("自热烹饪袋")]
public class SelfHeatingCookingBag : Card
{
    public override bool CanQuickInteract(Card card, out string tip)
    {
        return CanCook(card, out tip);
    }

    public override void QuickIneract(SlotCards slot, int count)
    {
        Cook(slot.PeekCard());
    }

    public bool CanCook(Card card, out string tip)
    {
        tip = string.Empty;
        if (card.CardId == "盐水") { tip = "需要在野炊营火中加热制盐"; return false; }
        if (card.TryGetComponent<CookComponent>(out var cook) && cook.leftCookTime > 0)
        {
            tip = "煮熟食物";
            return true;
        }
        return false;
    }

    public void Cook(Card food)
    {
        if (!CanCook(food, out _)) return;
        PlaySound("点火_02", true);
        Use();
        food.LockThis();
        TimeManager.Instance.AddTime(15, () =>
        {
            food.TryGetComponent<CookComponent>(out var cook);
            cook.HandleCookComplete();
        });
    }
}
