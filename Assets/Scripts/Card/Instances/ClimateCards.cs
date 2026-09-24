using System;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public static class ClimateInteractions
{
    public static Card FindMaterial(string id) => GameManager.Instance.PlayerBag.FindCardOfId(id) ??
        GameManager.Instance.CurEnvironmentBag.FindCardOfId(id);
    public static bool CanRepair(Card target, string id, out string hint)
    {
        hint = "";
        if (target.Destroyed || target.Locked || TimeManager.Instance.IsAdvancing) hint = "正在进行其他行动";
        else if (!target.TryGetComponent<DurabilityComponent>(out var d) || d.value >= d.maxValue) hint = "耐久已满";
        else if (FindMaterial(id) == null) hint = "需要" + id;
        return hint.Length == 0;
    }
    public static void Repair(Card target, Card material, float amount, int minutes)
    {
        if (material == null || material.Destroyed || material.Locked || target.Destroyed || target.Locked || TimeManager.Instance.IsAdvancing ||
            !target.TryGetComponent<DurabilityComponent>(out var d) || d.value >= d.maxValue) return;
        if (minutes == 0) { material.DestroyThis(); d.AddValue(amount); return; }
        var start = TimeManager.Instance.CurTime;
        target.LockThis(); material.LockThis();
        TimeManager.Instance.AddTime(minutes, () =>
        {
            target.UnlockThis(); material.UnlockThis();
            if ((TimeManager.Instance.CurTime - start).TotalMinutes < minutes || target.Destroyed || material.Destroyed) return;
            material.DestroyThis(); d.AddValue(amount);
        });
    }
}

public abstract class InstalledClimateCard : Card
{
    [JsonIgnore] public bool IsDeployed => Bag is ModificationBag;
    public override bool Moveable => !IsDeployed;
    public override string ExtraInfo => IsDeployed ? "已布置" : "未布置";
    public override void OnAdd(Bag bag)
    {
        if (stateMachine != null) stateMachine.ChangeState(IsDeployed ? "已布置" : "未布置");
    }
    protected void AddRefill(string material, int value)
    {
        AddCardEvent("填充（" + material + "）", $"消耗1{material}，耐久+{value}，不消耗时间。超出耐久上限的部分会浪费。",
            _ => ClimateInteractions.Repair(this, ClimateInteractions.FindMaterial(material), value, 0),
            (out string hint) => ClimateInteractions.CanRepair(this, material, out hint));
    }
    protected virtual int RefillValue(string id) => 0;
    public override bool CanQuickInteract(Card card, out string tip)
    {
        tip = "填充";
        return RefillValue(card.CardId) > 0 && !Destroyed && !Locked && !card.Locked && durability.value < durability.maxValue && !TimeManager.Instance.IsAdvancing;
    }
    public override void QuickIneract(SlotCards slot, int count)
    {
        for (int i = 0; i < count && !slot.IsEmpty; i++)
        {
            var card = slot.PeekCard();
            if (!CanQuickInteract(card, out _)) break;
            ClimateInteractions.Repair(this, card, RefillValue(card.CardId), 0);
        }
    }
}

[CardId("融冰浮标")]
public class IceMeltBuoy : InstalledClimateCard
{
    protected override void RegisterCardEvents() { AddRefill("盐", 150); AddRefill("暖绒", 200); }
    protected override int RefillValue(string id) => id == "盐" ? 150 : id == "暖绒" ? 200 : 0;
}

[CardId("牵引绳")]
public class TowRope : InstalledClimateCard { }

[CardId("防水电缆")]
public class WaterproofCable : InstalledClimateCard { }

[CardId("隔热棉")]
public class InsulationCotton : InstalledClimateCard { }

[CardId("盐")]
public class ClimateSalt : Card
{
    protected override void OnInit() => CardFactory.RestoreMissingCookComponent(this);
}

[CardId("暖绒")]
public class WarmFur : Card { }

[CardId("保温服")]
public class ThermalSuit : EquipmentCard
{
    public override void OnEquipped() { }
    public override void OnUnEquipped() { }
    public override string GetEquipDesc() => "承受15%的体温下降，其余85%每抵消1℃消耗10耐久。";
    protected override void RegisterCardEvents()
    {
        base.RegisterCardEvents();
        AddRepair("纤维", 30); AddRepair("暖绒", 260);
    }
    private void AddRepair(string material, int amount) => AddCardEvent("修补（" + material + "）", $"消耗1{material}，耐久+{amount}。超出耐久上限的部分会浪费。",
        _ => ClimateInteractions.Repair(this, ClimateInteractions.FindMaterial(material), amount, 3),
        (out string hint) => ClimateInteractions.CanRepair(this, material, out hint), () => 3);
    public override bool CanQuickInteract(Card card, out string tip)
    {
        tip = "修补（3分钟）";
        return (card.CardId == "纤维" || card.CardId == "暖绒") && !card.Locked && !Locked && durability.value < durability.maxValue && !TimeManager.Instance.IsAdvancing;
    }
    public override void QuickIneract(SlotCards slot, int count)
    {
        if (slot.IsEmpty || !CanQuickInteract(slot.PeekCard(), out _)) return;
        var card = slot.PeekCard();
        ClimateInteractions.Repair(this, card, card.CardId == "纤维" ? 30 : 260, 3);
    }
    public static float Protect(float delta)
    {
        if (delta >= 0) return delta;
        var suit = GameManager.Instance.EquipmentBag?.FindCardOfId("保温服");
        if (suit == null || !suit.TryGetComponent<DurabilityComponent>(out var d)) return delta;
        float absorbed = ClimateRules.AbsorbCold(-delta, d.value);
        d.Use(absorbed * 10);
        return delta + absorbed;
    }
}

[CardId("暖暖贴")]
public class WarmingPatch : Card
{
    protected override void RegisterCardEvents() => AddCardEvent("贴在身上", "体温+0.7℃，最多恢复至37.5℃。", UsePatch,
        (out string hint) => { hint = "体温已达到37.5℃"; return !Locked && !TimeManager.Instance.IsAdvancing && StateManager.Instance.PlayerStateDict[PlayerStateEnum.BodyTemperature].CurValue < 37.5f; }, () => 3);
    private void UsePatch(CardEvent e)
    {
        if (!e.Judge()) return;
        var start = TimeManager.Instance.CurTime;
        LockThis();
        TimeManager.Instance.AddTime(3, () =>
        {
            UnlockThis();
            if ((TimeManager.Instance.CurTime - start).TotalMinutes < 3 || Destroyed) return;
            float t = StateManager.Instance.PlayerStateDict[PlayerStateEnum.BodyTemperature].CurValue;
            StateManager.Instance.ChangePlayerState(PlayerStateEnum.BodyTemperature, Mathf.Clamp(37.5f - t, 0, .7f));
            DestroyThis();
        });
    }
}
