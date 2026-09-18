using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TimeManager : IManager
{
    public static TimeManager Instance { get; } = new();

    public const int SETTLEMENT_INTERVAL = 15; // 结算间隔
    public DateTime StartDateTime { get; private set; } = new(2020, 1, 1, 0, 0, 0);
    public DateTime CurTime { get; private set; }
    public int CurInterval { get; private set; }

    private DateTime lastDay;

    private Queue<EntityIntention> intentionQueue = new();  // 实体意图执行队列

    public int Days => (CurTime - StartDateTime).Days + 1;
    public double TotalDays => (CurTime - StartDateTime).TotalDays;

    private bool timePassShut = false;      // 时间流逝停止
    private float unfreezeTime = 0;         // 时间流逝暂停结束时间

    private int activeAdvances;
    public bool IsAdvancing => activeAdvances > 0 || intentionQueue.Count > 0;

    public void Init()
    {
        var timeData = GameDataManager.Instance.TimeData;

        if (!timeData.init)
        {
            //默认初始化
            CurTime = StartDateTime;
            CurInterval = SETTLEMENT_INTERVAL;
        }
        else
        {
            //从存档初始化
            CurTime = GameDataManager.Instance.TimeData.curTime;
            CurInterval = GameDataManager.Instance.TimeData.curIntervel;
        }

        // 初始化lastDay
        lastDay = CurTime.Date;
    }

    public void Reset()
    {
        activeAdvances = 0; intentionQueue.Clear(); timePassShut = false; unfreezeTime = 0;
    }

    public void AddTime(int minutes, UnityAction onEnd = null)
    {
        PublicMono.Instance.StartCoroutine(AddTimeCo(minutes, onEnd));
    }

    private IEnumerator AddTimeCo(int minutes, UnityAction onEnd = null)
    {
        activeAdvances++;
        try
        {
        timePassShut = false;

        EventManager.Instance.TriggerEvent(EventType.StartChangeTime);

        WaitForTimePass(minutes);

        int timespan = minutes;

        // 以一分钟为粒度流逝时间
        while (!timePassShut && timespan > 0)
        {
            bool triggered = false;
            while (Time.time < unfreezeTime)
            {
                if (!triggered)
                {
                    EventManager.Instance.TriggerEvent(EventType.EndChangeTime);
                    triggered = true;
                }
                MouseManager.Instance.Wait(Time.deltaTime);
                yield return null;
            }

            timespan--;
            CurInterval--;
            CurTime = CurTime.AddMinutes(1);
            // 处理一分钟更新
            HandleFineUpdate();
            // 处理实体意图的执行
            ExecuteIntentionInQueue();
            // 等待所有意图执行完毕
            while (intentionQueue.Count > 0)
            {
                if (!triggered)
                {
                    EventManager.Instance.TriggerEvent(EventType.EndChangeTime);
                    triggered = true;
                }
                MouseManager.Instance.Wait(Time.deltaTime);
                yield return null;
            }
            // 处理十五分钟更新
            HandleUpdate();
            // 处理天更新
            HandleAnotherDay();
        }

        EventManager.Instance.TriggerEvent(EventType.EndChangeTime);

        // 一次完整的时间流逝结束
        onEnd?.Invoke();
        }
        finally { activeAdvances = Mathf.Max(0, activeAdvances - 1); }
    }

    private void WaitForTimePass(int minutes)
    {
        float multiplier;

        if (minutes <= 15)
            multiplier = 1;
        else if (minutes <= 30)
            multiplier = 1.5f;
        else if (minutes <= 60)
            multiplier = 2;
        else if (minutes <= 120)
            multiplier = 4;
        else if (minutes <= 240)
            multiplier = 6;
        else
            multiplier = 9;

        var minWaitTime = MouseManager.DEFAULT_WAIT_TIME * multiplier;

        MouseManager.Instance.Wait(minWaitTime);
    }

    private void HandleUpdate()
    {
        if (CurInterval <= 0)
        {
            // 每15分钟触发一次Update事件
            CurInterval = SETTLEMENT_INTERVAL;
            EventManager.Instance.TriggerEvent(EventType.Update);
        }
    }

    private void HandleFineUpdate()
    {
        EventManager.Instance.TriggerEvent(EventType.FineUpdate);
        
        // 检查剧情触发条件
        if (ChatConditionManager.Instance != null)
        {
            ChatConditionManager.Instance.TrackCurrentStatus();
        }
    }

    /// <summary>
    /// 实体意图入队
    /// </summary>
    /// <param name="intention"></param>
    public void EnqueueIntention(EntityIntention intention)
    {
        intentionQueue.Enqueue(intention);
    }

    public void DequeueIntention()
    {
        if (intentionQueue.Count > 0)
            intentionQueue.Dequeue();

        // 执行下一个意图
        ExecuteIntentionInQueue();
    }

    private void ExecuteIntentionInQueue()
    {
        if (intentionQueue.Count > 0)
        {
            // 取得优先级最高的意图
            var intention = intentionQueue.Peek();
            // 检查意图是否有效
            if (!intention.IsValid)
            {
                // 当前意图无效，执行下一个
                DequeueIntention();
                return;
            }

            intention.TryExecute();

            // 意图执行完毕后会在内部自动调用 DequeueIntention 方法
        }
    }

    private bool HandleAnotherDay()
    {
        if (CurTime.Date == lastDay) return false;

        lastDay = CurTime.Date;
        EventManager.Instance.TriggerEvent(EventType.AnotherDay);
        return true;
    }

    /// <summary>
    /// 停止时间流逝
    /// </summary>
    public void ShutTimePass()
    {
        timePassShut = true;
    }

    /// <summary>
    /// 暂停时间流逝
    /// </summary>
    public void FreezeTimePass(float duration)
    {
        MouseManager.Instance.Wait(duration);
        unfreezeTime = Mathf.Max(unfreezeTime, Time.time + duration);
    }
}