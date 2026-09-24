using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>Each installed item has exactly one owning slot, including shared passage buoys.</summary>
public class ModificationBag : Bag
{
    public string acceptedId;
    [JsonIgnore] public Action changed;
    [JsonIgnore] public Card Installed => Slots.Count == 0 || Slots[0].IsEmpty ? null : Slots[0].PeekCard();
    protected override void FirstInit() { AddSlot(); Slots[0].SetMaxStackNum(1); }
    public override bool CanAddCard(Card card, out string tip)
    {
        tip = Installed != null ? "已完成此项改装" : card.CardId != acceptedId ? "需要" + acceptedId : "";
        return tip.Length == 0 && !card.Destroyed && !card.Locked;
    }
    public bool Install(Card card)
    {
        if (!CanAddCard(card, out _)) return false;
        card.SlotCards?.RemoveCard(card);
        AddCard(card);
        card.Init();
        card.RefreshSlot();
        changed?.Invoke();
        return true;
    }
    public override void OnRemoveCard(Card card) { changed?.Invoke(); }
}

[Serializable]
public class PlaceModifications
{
    public PlaceEnum place;
    public ModificationBag towRope = new() { acceptedId = "牵引绳" };
    public ModificationBag cable = new() { acceptedId = "防水电缆" };
    public ModificationBag insulation = new() { acceptedId = "隔热棉" };
    public float eventTemperatureOffset;
    public void Init()
    {
        towRope.Init(); cable.Init(); insulation.Init();
        cable.changed = () => GameManager.Instance.EnvironmentBags[place].SetCable(cable.Installed != null);
        cable.changed();
    }
}

[Serializable]
public class PassageConnection
{
    public string id;
    public string cardA, cardB;
    public PlaceEnum a, b;
    public float ice;
    public ModificationBag buoy = new() { acceptedId = "融冰浮标" };
    [JsonIgnore] public bool IsWater => GameManager.Instance.PlaceDataDict[a].isInWater || GameManager.Instance.PlaceDataDict[b].isInWater;
    [JsonIgnore] public bool IsFrozen => IsWater && ice >= 100;
    [JsonIgnore] public float Temperature
    {
        get
        {
            var envA = GameManager.Instance.EnvironmentBags[a];
            var envB = GameManager.Instance.EnvironmentBags[b];
            float tA = envA.StateDict[EnvironmentStateEnum.RoomTemperature].CurValue;
            float tB = envB.StateDict[EnvironmentStateEnum.RoomTemperature].CurValue;
            return !envA.PlaceData.isInWater ? tB : !envB.PlaceData.isInWater ? tA : Mathf.Min(tA, tB);
        }
    }
    public void ChangeIce(float delta) { ice = Mathf.Clamp(ice + delta, 0, 200); Refresh(); }
    public void Refresh()
    {
        foreach (var env in new[] { GameManager.Instance.EnvironmentBags[a], GameManager.Instance.EnvironmentBags[b] })
            foreach (var card in env.GetAllCards(false))
                if (card is PassageCard passage && passage.Connection == this) card.RefreshSlot();
    }
    public void Tick()
    {
        if (!IsWater) { ice = 0; return; }
        float natural = ClimateRules.NaturalIceDelta(Temperature, UnityEngine.Random.Range(2, 11));
        var card = buoy.Installed;
        bool melts = card != null && ice > 0;
        // Apply both effects before clamping, so ice near 200 does not discard natural accumulation first.
        ice = Mathf.Clamp(ice + natural - (melts ? 15 : 0), 0, 200);
        if (card != null && card.TryGetComponent<DurabilityComponent>(out var durability)) durability.Use(melts ? 1 : .2f);
        Refresh();
    }
}

[Serializable]
public class ClimateData
{
    public bool initialized;
    public int weatherSeed;
    public int glacierStartDay;
    public Dictionary<int, float> weatherByDay = new();
    public Dictionary<PlaceEnum, PlaceModifications> places = new();
    public List<PassageConnection> connections = new();
}
