using UnityEngine;
using UnityEngine.UI;

public class ModificationDetailsView : MonoBehaviour
{
    public Text summary;
    public ModificationRow rope, cable, cotton, buoy;
    private EnvironmentBag environment;
    private PassageCard passage;
    public void Bind(EnvironmentBag env, PassageCard card)
    {
        bool changed = environment != env || passage != card;
        environment = env; passage = card;
        Refresh();
        if (changed) GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
    }
    private void OnEnable()
    {
        EventManager.Instance.AddListener(EventType.EndChangeTime, Refresh);
        EventManager.Instance.AddListener<Card>(EventType.ChangeCardProperty, CardChanged);
        EventManager.Instance.AddListener<AddRemoveCardArgs>(EventType.AddRemoveCard, CardsChanged);
    }
    private void OnDisable()
    {
        EventManager.Instance.RemoveListener(EventType.EndChangeTime, Refresh);
        EventManager.Instance.RemoveListener<Card>(EventType.ChangeCardProperty, CardChanged);
        EventManager.Instance.RemoveListener<AddRemoveCardArgs>(EventType.AddRemoveCard, CardsChanged);
    }
    private void CardChanged(Card _) => Refresh();
    private void CardsChanged(AddRemoveCardArgs _) => Refresh();
    public void Refresh()
    {
        if (environment == null || ClimateManager.Instance.Data == null) return;
        var climate = ClimateManager.Instance;
        var mods = climate.Modifications(environment);
        bool connection = passage != null;
        rope.gameObject.SetActive(!connection && environment.PlaceData.supportsTowRope && environment.PlaceData.isInWater);
        cable.gameObject.SetActive(!connection && environment.PlaceData.supportsCable);
        cotton.gameObject.SetActive(!connection && environment.PlaceData.supportsInsulation && environment.PlaceData.isIndoor);
        buoy.gameObject.SetActive(connection && passage.Connection?.IsWater == true);
        if (connection)
        {
            var c = passage.Connection;
            summary.text = c == null ? "此通道不支持改装" : $"冰封 {c.ice:0}/200　通道温度 {c.Temperature:F1}℃\n两端共用浮标，每回合仅结算一次。";
            if (buoy.gameObject.activeSelf) buoy.Bind(c.buoy, environment, passage,
                "每回合耐久-0.2；融冰共-1。\n融冰-15。布置后不能取回。", this);
        }
        else
        {
            summary.text = $"{environment.PlaceName}　{environment.StateDict[EnvironmentStateEnum.RoomTemperature].CurValue:F1}℃\n面积 {environment.PlaceData.maxCoord:0}　隔热 {climate.InsulationLevel(environment)}级（{ClimateRules.InsulationRate(climate.InsulationLevel(environment)) * 100:0}%）";
            if (rope.gameObject.activeSelf) rope.Bind(mods.towRope, environment, null, "地点内移动速度+50%。\n每5分钟原始路程扣1耐久。", this);
            if (cable.gameObject.activeSelf) cable.Bind(mods.cable, environment, null, "每回合50%概率耐久-1。\n损坏断电，不可修补。", this);
            if (cotton.gameObject.activeSelf) cotton.Bind(mods.insulation, environment, null, "隔热+1；同地点不可叠加。\n拆除15分钟，返还原卡牌。", this);
        }
        LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
    }
}
