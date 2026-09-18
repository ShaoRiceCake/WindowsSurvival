using System.Collections.Generic;
using UnityEngine;

public static class AfterChatFactory
{
    public static void TriggerEffect(string EventName)
    {
        //根据EventName创建对应的事件;英文分号隔开两个事件
        //音效：音效_音效名_是否随机
        //音乐：音乐_音乐名_是否循环
        //状态：状态_目标状态位置（玩家/当前环境/维生舱/驾驶室/动力舱/珊瑚礁海域）_状态名(健康/饱食/口渴/精神/氧气/疲劳/电力/氧气/压力/高度/电缆/水域)_数值
        //时间：时间_数值
        //解锁：解锁_目标窗口名称
        //添加：添加_玩家（玩家/场景）_压缩饼干（物品名称）
        //计数：计数_计数名_+1/-1/=1(_后面填的是变化或等于的值，如+1表示使该计数增加1，-1表示使该计数减少1，=1表示使该计数等于1)
        //科技：科技_科技名（直接完成指定科技的研究，解锁该科技及其配方）
        //其他：其他_其他名
        if(EventName=="")return;
        // 使用 StringSplitOptions.RemoveEmptyEntries 移除空条目，并 Trim 每个事件项以支持换行
        List<string> eventList = new List<string>();
        foreach (string item in EventName.Split(';'))
        {
            string trimmedItem = item.Trim(); // 移除前后空白字符（包括换行符）
            if (!string.IsNullOrEmpty(trimmedItem))
            {
                eventList.Add(trimmedItem);
            }
        }
        List<List<string>> eventListList = new List<List<string>>();
        foreach (string eventItem in eventList)
        {
            List<string> eventItemList = new List<string>(eventItem.Split('_'));
            eventListList.Add(eventItemList);
        }
        foreach (List<string> eventItemList in eventListList)
        {
            switch (eventItemList[0])
            {
                case "音效":
                    SoundManager.Instance.PlaySound(eventItemList[1], eventItemList[2] == "true");
                    break;
                case "音乐":
                    SoundManager.Instance.PlayBGM(eventItemList[1], eventItemList[2] == "true");
                    break;
                case "状态":
                    ChangeState(eventItemList);
                    break;
                case "时间":
                    TimeManager.Instance.AddTime(int.Parse(eventItemList[1]));
                    break;
                case "解锁":
                    UnlockWindow(eventItemList[1], eventItemList[2]);
                    break;
                case "添加":
                    AddCardEvent(eventItemList[1], eventItemList[2]); 
                    break;
                case "计数":
                    ChangeCount(eventItemList);
                    break;
                case "科技":
                    CompleteTechnology(eventItemList);
                    break;
                case "其他":
                    OtherEvent(eventItemList);
                    break;
            }
        }
    }

    private static void AddCardEvent(string PlayerOrScene, string CardName)
    {
        GameManager.Instance.AddCardWithTween(CardFactory.CreateCard(CardName), PlayerOrScene == "玩家", new Vector2(0,-700));
    }

    private static void UnlockWindow(string WindowName,string blink)
    {
        bool Addblink = blink == "true" ? true : false;
        string windowName = WindowName switch
        {
            "背包" => "PlayerBag",
            "摄像头"=>"Camera",
            "状态" => "State",
            "研究" => "Study",
            "地点" => "EnvironmentBag",
            "休息" => "Rest",
            "装备" => "Equipment",
            "制作"=>"Craft",
            "详情"=>"Details",
            _ => throw new System.Exception("WindowName Error")
        };
        WindowsManager.Instance.UnlockShortcut(windowName,Addblink);
        //解锁窗口逻辑
    }

    public static void ChangeState(List<string> eventItemList)
    {
        switch (eventItemList[1])
        {
            case "玩家":
                ChangePlayerStateByString(eventItemList[2], float.Parse(eventItemList[3]));
                break;
            case "当前环境":
                ChangeEnvironmentStateByString(GameManager.Instance.CurEnvironmentBag.PlaceData.placeType, eventItemList[2], float.Parse(eventItemList[3]));
                break;
            case "维生舱":
                ChangeEnvironmentStateByString(PlaceEnum.LifeSupportCabin, eventItemList[2], float.Parse(eventItemList[3]));
                break;
            case "驾驶室":
                ChangeEnvironmentStateByString(PlaceEnum.Cockpit, eventItemList[2], float.Parse(eventItemList[3]));
                break;
            case "动力舱":
                ChangeEnvironmentStateByString(PlaceEnum.PowerCabin, eventItemList[2], float.Parse(eventItemList[3]));
                break;
            case "珊瑚礁海域":
                ChangeEnvironmentStateByString(PlaceEnum.CoralCoast, eventItemList[2], float.Parse(eventItemList[3]));
                break;
        }

    }

    private static void ChangePlayerStateByString(string stateName, float delta)
    {
        // 状态名到枚举的映射
        var stateMap = new Dictionary<string, PlayerStateEnum>
        {
            { "健康", PlayerStateEnum.Health },
            { "饱食", PlayerStateEnum.Hunger },
            { "口渴", PlayerStateEnum.Hydration },
            { "精神", PlayerStateEnum.Sanity },
            { "氧气", PlayerStateEnum.Oxygen },
            { "清醒", PlayerStateEnum.Sobriety }
        };
        
        if (!stateMap.TryGetValue(stateName, out var stateEnum)) return;
        
        var state = StateManager.Instance.PlayerStateDict[stateEnum];
        float oldValue = state.CurValue;
        StateManager.Instance.ChangePlayerState(stateEnum, delta);
        float newValue = state.CurValue;
        
        UnityEngine.Debug.Log($"[状态变化] 玩家-{stateName}: {oldValue:F1} → {newValue:F1} (变化: {(delta >= 0 ? "+" : "")}{delta:F1})");
    }

    private static void ChangeEnvironmentStateByString(PlaceEnum placeType, string stateName, float delta)
    {
        var env = GameManager.Instance.EnvironmentBags[placeType];
        string placeName = GetPlaceName(placeType);
        float oldValue = 0;
        float newValue = 0;
        string displayName = placeName;
        
        switch (stateName)
        {
            case "电力":
                oldValue = ElectricPowerManager.Instance.Power.CurValue;
                ElectricPowerManager.Instance.ChangePower(delta);
                newValue = ElectricPowerManager.Instance.Power.CurValue;
                break;
            case "氧气":
                oldValue = env.StateDict[EnvironmentStateEnum.Oxygen].CurValue;
                env.ChangeEnvironmentState(EnvironmentStateEnum.Oxygen, delta);
                newValue = env.StateDict[EnvironmentStateEnum.Oxygen].CurValue;
                break;
            case "压力":
                oldValue = env.StateDict[EnvironmentStateEnum.Oxygen].CurValue;
                env.ChangeEnvironmentState(EnvironmentStateEnum.Oxygen, delta);
                newValue = env.StateDict[EnvironmentStateEnum.Oxygen].CurValue;
                break;
            case "高度":
                oldValue = StateManager.Instance.WaterLevel.CurValue;
                StateManager.Instance.ChangeWaterLevel(delta);
                newValue = StateManager.Instance.WaterLevel.CurValue;
                displayName = "水平面";
                break;
            default:
                return;
        }
        
        UnityEngine.Debug.Log($"[状态变化] {displayName}-{stateName}: {oldValue:F1} → {newValue:F1} (变化: {(delta >= 0 ? "+" : "")}{delta:F1})");
    }
    
    private static string GetPlaceName(PlaceEnum placeType)
    {
        return placeType switch
        {
            PlaceEnum.LifeSupportCabin => "维生舱",
            PlaceEnum.Cockpit => "驾驶室",
            PlaceEnum.PowerCabin => "动力舱",
            PlaceEnum.CoralCoast => "珊瑚礁海域",
            _ => "当前环境"
        };
    }

    private static void ChangeCount(List<string> eventItemList)
    {
        if (eventItemList.Count < 3)
        {
            UnityEngine.Debug.LogError($"[计数效果格式错误] 参数不足，需要3个参数：计数_计数名_操作值");
            return;
        }

        string countName = eventItemList[1];
        string operation = eventItemList[2];

        // 验证计数是否已定义（即使未定义也继续执行，但会报错）
        if (!CountDefinition.IsCountDefined(countName))
        {
            UnityEngine.Debug.LogError($"[计数效果错误] 计数 \"{countName}\" 未在 CountDefinition.cs 中定义！请在 CountDefinition.DefinedCounts 中添加该计数。");
            // 继续执行，不return，让计数操作能够执行
        }

        // 验证操作格式
        if (!operation.StartsWith("+") && !operation.StartsWith("-") && !operation.StartsWith("="))
        {
            UnityEngine.Debug.LogError($"[计数效果格式错误] 操作值格式不正确：\"{operation}\"。应使用 +数字、-数字 或 =数字 格式。");
            return;
        }

        try
        {
            int oldValue = CountManager.Instance.GetCount(countName);
            int newValue = oldValue;
            
            if (operation.StartsWith("+"))
            {
                // 增加计数，格式：+1, +2 等
                int delta = int.Parse(operation.Substring(1));
                CountManager.Instance.ChangeCount(countName, delta);
                newValue = CountManager.Instance.GetCount(countName);
                // 触发计数变化事件，用于检查段落条件
                CheckCountParagraphConditions(countName);
            }
            else if (operation.StartsWith("-"))
            {
                // 减少计数，格式：-1, -2 等
                int delta = int.Parse(operation);
                CountManager.Instance.ChangeCount(countName, delta);
                newValue = CountManager.Instance.GetCount(countName);
                // 触发计数变化事件，用于检查段落条件
                CheckCountParagraphConditions(countName);
            }
            else if (operation.StartsWith("="))
            {
                // 设置计数，格式：=1, =2 等
                int value = int.Parse(operation.Substring(1));
                CountManager.Instance.SetCount(countName, value);
                newValue = value;
                // 触发计数变化事件，用于检查段落条件
                CheckCountParagraphConditions(countName);
            }
            
            // 输出计数变化调试信息
            if (oldValue != newValue)
            {
                UnityEngine.Debug.Log($"[计数变化] {countName}: {oldValue} → {newValue}");
            }
        }
        catch (System.FormatException)
        {
            UnityEngine.Debug.LogError($"[计数效果格式错误] 操作值 \"{operation}\" 无法解析为数字。应使用 +数字、-数字 或 =数字 格式。");
        }
    }

    private static void CompleteTechnology(List<string> eventItemList)
    {
        if (eventItemList.Count < 2)
        {
            UnityEngine.Debug.LogError($"[科技效果格式错误] 参数不足，需要2个参数：科技_科技名");
            return;
        }

        string techName = eventItemList[1];
        
        // 获取科技节点
        var techNode = TechnologyManager.Instance.GetTechNodeByName(techName);
        if (techNode == null)
        {
            UnityEngine.Debug.LogError($"[科技效果错误] 科技 \"{techName}\" 不存在！");
            return;
        }

        TechnologyManager.Instance.AddStudyProgress(techNode, 9999);
        
        // 输出科技解锁调试信息
        UnityEngine.Debug.Log($"[科技解锁] 已解锁科技：{techName}");
    }

    public static void OtherEvent(List<string> eventItemList)
    {
        switch (eventItemList[1])
        {
            case "死亡":
                Die();
                break;
        }
    }
    private static void CheckCountParagraphConditions(string countName)
    {
        // 检查所有计数相关的段落条件
        if (ChatConditionManager.Instance != null)
        {
            foreach (var condition in ChatConditionManager.Instance.DetectedParagraphConditions.Values)
            {
                if (condition is CountParagraphCondition countCondition)
                {
                    countCondition.UpdateCountCheck();
                }
            }
        }
    }

    public static void Die()
    {
        ChatManager.Instance.ParagraphToTriggeer.Clear();
        ChatManager.Instance.InterruptParagraphData=null;
        // 普通世界线允许回档；硬核世界线死亡后删除。
        SaveSystem.Die();

    }
}
