/// <summary>
/// 野炊营火
/// </summary>
[CardId("野炊营火")]
public class Campfire : ConstructionCard
{
	public override bool HasLoopSound => true;

	protected override void RegisterCardEvents()
	{
		AddCardEvent("点燃", $"点燃{CardName}\n点燃后可以烧烤部分食物，或将1盐水加热15分钟得到1盐\n{ColorManager.Warning("会导致室内氧气加速消耗与一氧化碳增加")}",
			fuelStorage.Ignite, fuelStorage.CanIgnite, sound: "点火_02");
		AddCardEvent("熄灭", "", fuelStorage.Extinguish, fuelStorage.CanExtinguish, sound: "熄灭");
		base.RegisterCardEvents(); // 拆毁
	}

	protected override void OnLateConstructor()
	{
		// 每个卡牌槽的最大堆叠数都为1
		foreach (var slot in innerContents.bag.Slots)
		{
			slot.SetMaxStackNum(1);
		}
	}

	protected override void OnInit()
	{
		// 放入内容物时，暂停卡牌每回合更新
		innerContents.onAddCard = (c) =>
		{
			if (fuelStorage.isBurning)
			{
				c.FreezeUpdate();
				c.TryGetComponent<CookComponent>(out var cook);
				if (cook.leftCookTime < 0) return;

				var timer = new TimerComponent(cook.leftCookTime, cook.totalCookTime);
				if (cook.outcomeCardId == "烧焦的食物")
					timer.tipText = "烧焦";
				else
					timer.tipText = cook.outcomeCardId == "盐" ? "制盐" : "烤熟";

				c.AddComponent(timer);
			}
		};

		// 取出时恢复每回合更新
		innerContents.onRemoveCard = (c) =>
		{
			c.UnfreezeUpdate();
			c.RemoveComponent<TimerComponent>();
		};
	}

	/// <summary>
	/// 点燃时触发
	/// </summary>
	private void OnIgnite()
	{
		// 只有玩家在同一地点且点燃时才播放循环音效
		if (GameManager.Instance.IsCurrentEnvironment(Bag))
			SoundManager.Instance.PlayCardLoopSound(CardId, "野炊营火音效", 0.3f);

		// 点燃后暂停所有卡牌每回合更新
		innerContents.FreezeUpdate();

		// 显示烹饪计时器
		innerContents.ForEachCard(c =>
		{
			c.TryGetComponent<CookComponent>(out var cook);
			if (cook.leftCookTime < 0) return;

			var timer = new TimerComponent(cook.leftCookTime, cook.totalCookTime);
			if (cook.outcomeCardId == "烧焦的食物")
				timer.tipText = "烧焦";
			else
				timer.tipText = cook.outcomeCardId == "盐" ? "制盐" : "烤熟";
			c.AddComponent(timer);
			c.RefreshSlot();
		});

		stateMachine.ChangeState("点燃");
	}

	/// <summary>
	/// 熄灭时触发
	/// </summary>
	private void OnExtinguish()
	{
		// 只有玩家在同一地点时才停止音效
		if (GameManager.Instance.IsCurrentEnvironment(Bag))
			SoundManager.Instance.StopCardLoopSound(CardId);

		// 熄灭后恢复所有卡牌每回合更新
		innerContents.UnfreezeUpdate();

		// 移除计时器组件
		innerContents.ForEachCard(c =>
		{
			c.RemoveComponent<TimerComponent>();
			c.RefreshSlot();
		});

		stateMachine.ChangeState("熄灭");
	}

	/// <summary>
	/// 点燃时每回合触发
	/// </summary>
	private void OnBurning()
	{
		// 内容物增加烹饪进度
		foreach (var card in innerContents.GetAllCards())
		{
			card.TryGetComponent(out CookComponent cook);
			cook.Cook();

			if (card.TryGetComponent<TimerComponent>(out var timer) && cook.leftCookTime >= 0)
			{
				timer.SetValue(cook.leftCookTime);
			}
		}
	}

	private bool ContentFilter(Card c, out string s)
	{
		s = string.Empty;
		if (!c.TryGetComponent<CookComponent>(out _))
		{
			s = "只能放入可烹饪的物品";
			return false;
		}
		return true;
	}

	public override bool CanQuickInteract(Card card, out string tip)
	{
		// 添加燃料
		if (fuelStorage.CanQuickInteract(card))
		{
			tip = "添加燃料";
			return true;
		}
		// 放入内容物
		if (innerContents.CanQuickInteract(card, out tip)) return true;
		// 拆毁
		return base.CanQuickInteract(card, out tip);
	}

	public override void QuickIneract(SlotCards slot, int count)
	{
		var card = slot.PeekCard();

		// 添加燃料
		if (fuelStorage.CanQuickInteract(card))
		{
			fuelStorage.QuickIneract(slot, count);
			return;
		}

		// 放入内容物
		if (innerContents.CanQuickInteract(card, out _))
		{
			innerContents.QuickIneract(slot, count);
			return;
		}

		// 拆毁
		base.QuickIneract(slot, count);
	}
	public override void OnEnterEnvironment()
	{
		// 只有点燃状态才播放音效
		if (fuelStorage.isBurning)
			SoundManager.Instance.PlayCardLoopSound(CardId, "野炊营火音效", 0.3f);
	}
	public override void OnLeaveEnvironment()
	{
		SoundManager.Instance.StopCardLoopSound(CardId);
	}
	public override void OnDetailOpen()
	{
		SoundManager.Instance.SetCardLoopVolume(CardId, 1.0f); // 音量调高
	}
	public override void OnDetailClose()
	{
		SoundManager.Instance.SetCardLoopVolume(CardId, 0.3f); // 恢复正常
	}
}
