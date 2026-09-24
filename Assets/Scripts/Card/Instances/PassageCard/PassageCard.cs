using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public abstract class PassageCard : Card
{
    private const float MAX_AVAILABLE_DIST = 3.0f; // 小于等于该距离时可以使用通道
    [JsonIgnore] public PassageConnection Connection => Bag is EnvironmentBag env && passage != null
        ? ClimateManager.Instance.ConnectionForCard(CardId, env.PlaceData.placeType) : null;
    public override string ExtraInfo => Connection is { ice: > 0 } c ? $"{(c.IsFrozen ? "已冰封" : "结冰中")} {c.ice:0}/200" : base.ExtraInfo;
    public override string CardDesc => base.CardDesc + (Connection is { IsWater: true } c ?
        $"\n\n冰封值：{c.ice:0}/200（100起封锁）\n通道温度：{c.Temperature:F1}℃\n来回通道共用冰封值及融冰浮标。\n低于0℃积冰，高于0℃融冰，0℃保持不变。" : "");

    protected override void RegisterCardEvents()
    {
        AddCardEvent("通过",
            () => "前往" + ParsePlaceEnum(passage.targetPlace) + MoveExploreManager.Instance.GetMoveEffects(passage.time, passage.targetPlace).desc,
            Event_Enter, Judge_Enter,
            () => MoveExploreManager.Instance.GetMoveEffects(passage.time, passage.targetPlace).time,
            () => MoveExploreManager.Instance.GetMoveEffects(passage.time, passage.targetPlace).playerStateChanges,
            sound: passage.audioClip);
        AddCardEvent("移至附近",
            () => "移动到通道的附近" + MoveExploreManager.Instance.GetMoveEffects(GetNearestAvailablePosition()).desc,
            Event_MoveNear, Judge_MoveNear,
            () => MoveExploreManager.Instance.GetMoveEffects(GetNearestAvailablePosition()).time,
            () => MoveExploreManager.Instance.GetMoveEffects(GetNearestAvailablePosition()).playerStateChanges);
        AddCardEvent("用手除冰", "冰封值-7，体温-0.5℃。", _ => Deice(null), Judge_Deice, () => 15,
            shouldHideThis: () => Connection == null || Connection.ice <= 0);
        AddCardEvent("用工具除冰", () => FindDeicingTool()?.CardId == "钢锤" ? "钢锤强力除冰：冰封值-50，耐久-1。" : "冰封值-30，工具耐久-1。",
            _ => { var tool = FindDeicingTool(); if (tool != null) Deice(tool); }, Judge_ToolDeice,
            () => FindDeicingTool()?.CardId == "钢锤" ? 5 : 15,
            shouldHideThis: () => Connection == null || Connection.ice <= 0 || FindDeicingTool() == null);
    }

    protected override void OnInit()
    {
        EventManager.Instance.AddListener<PlayerStateEnum>(EventType.RefreshPlayerState, OnLoadChange);
    }

    protected override void OnDestroy()
    {
        EventManager.Instance.RemoveListener<PlayerStateEnum>(EventType.RefreshPlayerState, OnLoadChange);
    }

    /// <summary>
    /// 载重变化时刷新卡槽
    /// </summary>
    /// <param name="state"></param>
    private void OnLoadChange(PlayerStateEnum state)
    {
        if (state == PlayerStateEnum.Load)
        {
            RefreshSlot();
        }
    }

    private string ParsePlaceEnum(PlaceEnum place)
    {
        return GameManager.Instance.PlaceDataDict[place].placeName;
    }

    protected virtual void Event_Enter(CardEvent e)
    {
        if (!Judge_Enter(out _)) return;
        MoveExploreManager.Instance.Move(passage.targetPlace, passage.time);
    }

    protected virtual bool Judge_Enter(out string hint)
    {
        hint = string.Empty;
        if (Connection?.IsFrozen == true)
        {
            hint = "通道已被冰封，需要用武器或工具除冰";
            return false;
        }
        if (!IsPlayerNear())
        {
            hint = "距离太远，无法通过";
            return false;
        }

        if (!MoveExploreManager.Instance.CanMoveExplore())
        {
            hint = "身上太重了，无法通过";
            return false;
        }
        return true;
    }

    protected virtual void Event_MoveNear(CardEvent e)
    {
        // 移动到最近的可用使用通道的坐标
        MoveExploreManager.Instance.Move(GetNearestAvailablePosition());
    }

    protected virtual bool Judge_MoveNear(out string hint)
    {
        hint = string.Empty;

        if (IsPlayerNear())
        {
            hint = "已经在附近了，无需移动";
            return false;
        }

        if (!MoveExploreManager.Instance.CanMoveExplore())
        {
            hint = "身上太重了，无法移动";
            return false;
        }

        return true;
    }

    private bool IsPlayerNear()
    {
        return Bag == GameManager.Instance.CurEnvironmentBag && coordinate.DistanceTo(Player.Instance) <= MAX_AVAILABLE_DIST;
    }

    private bool Judge_Deice(out string hint)
    {
        hint = !IsPlayerNear() ? "距离太远，请移至附近" : TimeManager.Instance.IsAdvancing ? "正在进行其他行动" : Connection == null || Connection.ice <= 0 ? "无需除冰" : "";
        return hint.Length == 0;
    }
    private bool Judge_ToolDeice(out string hint)
    {
        if (!Judge_Deice(out hint)) return false;
        if (FindDeicingTool() != null) return true;
        hint = "需要可除冰的武器或工具"; return false;
    }
    public static bool IsDeicingTool(Card card) => card != null && !card.Destroyed && !card.Locked &&
        card.CardId != "自热烹饪袋" && card.CardId != "凝胶装瓶器" &&
        (card.CardType == CardType.Tool || card.TryGetComponent<WeaponComponent>(out _)) &&
        (!card.TryGetComponent<DurabilityComponent>(out var d) || d.value > 0);
    private Card FindDeicingTool() => GameManager.Instance.PlayerBag.GetAllCards()
        .Concat(GameManager.Instance.CurEnvironmentBag.GetAllCards()).Where(IsDeicingTool).OrderByDescending(c => c.CardId == "钢锤").FirstOrDefault();
    public bool InstallBuoy(Card card, out string hint)
    {
        if (!CanInstallBuoy(card, out hint)) return false;
        return Connection.buoy.Install(card);
    }
    public bool CanInstallBuoy(Card card, out string hint)
    {
        hint = Connection?.IsWater != true ? "此通道无需融冰浮标" : !IsPlayerNear() ? "距离太远，请移至附近" : TimeManager.Instance.IsAdvancing ? "正在进行其他行动" : "";
        return hint.Length == 0 && Connection.buoy.CanAddCard(card, out hint);
    }
    private void Deice(Card tool)
    {
        if (!Judge_Deice(out _) || tool != null && !IsDeicingTool(tool)) return;
        var connection = Connection;
        int minutes = tool?.CardId == "钢锤" ? 5 : 15;
        float amount = tool == null ? 7 : tool.CardId == "钢锤" ? 50 : 30;
        var start = TimeManager.Instance.CurTime;
        tool?.LockThis();
        TimeManager.Instance.AddTime(minutes, () =>
        {
            tool?.UnlockThis();
            if ((TimeManager.Instance.CurTime - start).TotalMinutes < minutes) return;
            connection.ChangeIce(-amount);
            if (tool == null) StateManager.Instance.ChangePlayerState(PlayerStateEnum.BodyTemperature, -.5f);
            else if (!tool.Destroyed && tool.TryGetComponent<DurabilityComponent>(out var d)) d.Use(1);
        });
    }
    public override bool CanQuickInteract(Card card, out string tip)
    {
        if (card.CardId == "融冰浮标") return CanInstallBuoy(card, out tip);
        if (Connection?.buoy.Installed is Card buoy && buoy.CanQuickInteract(card, out tip)) return true;
        tip = "";
        if (!IsDeicingTool(card) || !Judge_Deice(out tip)) return false;
        tip = card.CardId == "钢锤" ? "强力除冰：5分钟，冰封-50，耐久-1" : "用工具除冰：15分钟，冰封-30，耐久-1";
        return true;
    }
    public override void QuickIneract(SlotCards slot, int count)
    {
        if (slot.IsEmpty || !CanQuickInteract(slot.PeekCard(), out _)) return;
        var card = slot.PeekCard();
        if (card.CardId == "融冰浮标") InstallBuoy(card, out _);
        else if (Connection?.buoy.Installed is Card buoy && buoy.CanQuickInteract(card, out _)) buoy.QuickIneract(slot, count);
        else Deice(card);
    }

    private float GetNearestAvailablePosition()
    {
        var playerPos = Player.Instance.Coordinate.Position;
        var passagePos = coordinate.Position;
        return playerPos > passagePos ? passagePos + MAX_AVAILABLE_DIST : passagePos - MAX_AVAILABLE_DIST;
    }
}
