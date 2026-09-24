using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class UpdateManager : IManager
{
    public static UpdateManager Instance { get; } = new();

    public UnityEvent GameEventUpdate { get; private set; } = new();
    public UnityEvent PlayerUpdate { get; private set; } = new();
    public UnityEvent PowerUpdate {  get; private set; } = new();
    public UnityEvent EnvironmentUpdate { get; private set; } = new();
    public UnityEvent PopulationUpdate { get; private set; } = new();
    public UnityEvent TechnologyUpdate { get; private set; } = new();
    public UnityEvent SunlightUpdate { get; private set; } = new();

    private SortedList<int, UnityAction> sortedCardUpdates = new();
    private SortedList<int, UnityAction> sortedEntityUpdates = new();
    private int currentOrder = 0;

    public void Init()
    {
        EventManager.Instance.AddListener(EventType.Update, Update);
        EventManager.Instance.AddListener(EventType.FineUpdate, FineUpdate);
    }

    public void Reset()
    {
        Clear();
        currentOrder = 0;
        EventManager.Instance.RemoveListener(EventType.Update, Update);
        EventManager.Instance.RemoveListener(EventType.FineUpdate, FineUpdate);
    }

    public void AddCardUpdateListener(ref int order, UnityAction update)
    {
        if (order <= 0)
            order = currentOrder + 1;
        
        sortedCardUpdates.Add(order, update);
        currentOrder = Mathf.Max(currentOrder, order);
    }

    public void RemoveCardUpdateListener(int order)
    {
        sortedCardUpdates.Remove(order);
    }

    private void CardUpdate()
    {
        foreach (var update in sortedCardUpdates.Values.ToList()) // 这里需要tolist是因为update可能会修改sortedCardUpdates
        {
            update();
        }
    }

    private void Update()
    {
        ClimateManager.Instance.Tick();
        EventManager.Instance.TriggerEvent(EventType.UpdateBegin);
        // 顺序很重要
        TechnologyUpdate.Invoke();
        //CardUpdate.Invoke();
        CardUpdate();
        PowerUpdate.Invoke();
        EnvironmentUpdate.Invoke();
        PlayerUpdate.Invoke();
        PopulationUpdate.Invoke();
        GameEventUpdate.Invoke();
        SunlightUpdate.Invoke();
    }

    public void AddEntityUpdateListener(ref int order, UnityAction update)
    {
        if (order <= 0)
            order = currentOrder + 1;

        sortedEntityUpdates.Add(order, update);
        currentOrder = Mathf.Max(currentOrder, order);
    }

    public void RemoveEntityUpdateListener(int order)
    {
        sortedEntityUpdates.Remove(order);
    }

    private void EntityUpdate()
    {
        foreach (var update in sortedEntityUpdates.Values.ToList())
        {
            update();
        }
    }

    private void FineUpdate()
    {
        EntityUpdate();
    }

    private void Clear()
    {
        sortedCardUpdates.Clear();
        sortedEntityUpdates.Clear();
        GameEventUpdate.RemoveAllListeners();
        PlayerUpdate.RemoveAllListeners();
        EnvironmentUpdate.RemoveAllListeners();
        //CardUpdate.RemoveAllListeners();
        PopulationUpdate.RemoveAllListeners();
        TechnologyUpdate.RemoveAllListeners();
        SunlightUpdate.RemoveAllListeners();
        PowerUpdate.RemoveAllListeners();
    }
}
