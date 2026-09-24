/// <summary>
/// 烧焦的食物
/// </summary>
[CardId("烧焦的食物")]
public class BurntFood : CookableCard
{
    protected override void RegisterCardEvents()
    {
        AddCardEvent("食用", "", EasyEvent_Destroy, null,
            () => 15,
            () => new()
            {
                { PlayerStateEnum.Hunger, 10 },
                { PlayerStateEnum.Hydration, -20 },
                { PlayerStateEnum.Health, -5 },
                { PlayerStateEnum.BodyTemperature, 2 }
            },
            sound: "吃_01");
    }
}
