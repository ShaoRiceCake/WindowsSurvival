using UnityEngine;
using UnityEngine.UI;

/// <summary>An attachment slot view; never duplicates or takes ownership of the installed card.</summary>
public class ModificationRow : MonoBehaviour
{
    public Text title, status, description, slotLabel;
    public Image icon;
    public HoverableButton install, actionOne, actionTwo;
    private ModificationBag bag;
    private EnvironmentBag environment;
    private PassageCard passage;
    private ModificationDetailsView owner;
    private void OnRectTransformDimensionsChange() => RefreshLayout();
    private void RefreshLayout()
    {
        if (description == null || install == null || actionOne == null || actionTwo == null) return;
        float width = ((RectTransform)transform).rect.width;
        if (width < 150) return;
        const float left = 100, right = 8, gap = 8, actionHeight = 32;
        float textHeight = Mathf.Max(42, description.preferredHeight);
        var desc = description.rectTransform; desc.offsetMin = new(left, -56 - textHeight);
        float rowHeight = Mathf.Max(150, 56 + textHeight + gap + actionHeight + 8);
        var layout = GetComponent<LayoutElement>();
        if (!Mathf.Approximately(layout.preferredHeight, rowHeight)) layout.preferredHeight = rowHeight;
        float buttonWidth = Mathf.Min(120, (width - left - right - gap) / 2);
        foreach (var button in new[] { install, actionOne, actionTwo })
        {
            button.minWidth = buttonWidth;
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new(buttonWidth, actionHeight);
            rect.anchoredPosition = new(button == actionTwo ? left + buttonWidth + gap : left, 8);
        }
    }
    public void Bind(ModificationBag attachment, EnvironmentBag env, PassageCard card, string text, ModificationDetailsView view)
    {
        bag = attachment; environment = env; passage = card; owner = view;
        title.text = bag.acceptedId;
        icon.sprite = CardFactory.GetCardImage(bag.acceptedId);
        icon.color = bag.Installed == null ? new Color(1, 1, 1, .3f) : Color.white;
        slotLabel.text = bag.Installed == null ? "拖入布置" : "已布置";
        description.text = text;
        RefreshLayout();
        var installed = bag.Installed;
        status.text = installed == null ? "未改装" : installed.TryGetComponent<DurabilityComponent>(out var d) ? $"耐久 {d.value:0.#}/{d.maxValue:0}" : "隔热等级 +1";
        actionOne.gameObject.SetActive(false); actionTwo.gameObject.SetActive(false);
        install.gameObject.SetActive(installed == null);
        if (installed == null)
        {
            var material = ClimateInteractions.FindMaterial(bag.acceptedId);
            bool available = material != null && CanInstall(material, out _);
            Setup(install, "布置", available, () => { var c = ClimateInteractions.FindMaterial(bag.acceptedId); if (c != null) Install(c); }, "消耗1" + bag.acceptedId + "，不消耗时间");
        }
        else if (bag.acceptedId == "隔热棉")
            Setup(actionOne, "拆除", !TimeManager.Instance.IsAdvancing, RemoveCotton, "消耗15分钟，返还1隔热棉");
        else if (bag.acceptedId == "融冰浮标")
        { RepairButton(actionOne, "盐", 150); RepairButton(actionTwo, "暖绒", 200); }
    }
    private void Setup(HoverableButton button, string text, bool enabled, UnityEngine.Events.UnityAction action, string tip)
    {
        button.gameObject.SetActive(true); button.text.text = text;
        button.Interactable = enabled;
        button.text.color = enabled ? ColorManager.White : ColorManager.DarkGrey;
        button.onClick.RemoveAllListeners(); button.onClick.AddListener(action);
        button.GetComponent<HoverTipController>()?.SetTip(tip);
    }
    private void RepairButton(HoverableButton button, string id, int value)
    {
        bool enabled = ClimateInteractions.CanRepair(bag.Installed, id, out string hint);
        Setup(button, "填充" + id, enabled, () =>
        {
            ClimateInteractions.Repair(bag.Installed, ClimateInteractions.FindMaterial(id), value, 0); owner.Refresh();
        }, enabled ? $"消耗1{id}，耐久+{value}，不消耗时间" : hint);
    }
    private bool CanInstall(Card card, out string hint)
    {
        hint = "正在进行其他行动";
        if (TimeManager.Instance.IsAdvancing || environment != GameManager.Instance.CurEnvironmentBag) return false;
        return passage != null ? passage.CanInstallBuoy(card, out hint) : bag.CanAddCard(card, out hint);
    }
    private void Install(Card card)
    {
        if (!CanInstall(card, out _)) return;
        bag.Install(card); owner.Refresh();
    }
    public bool HandleDrop(SlotCards source, int count, out string hint)
    {
        hint = "";
        if (bag == null || source.IsEmpty || environment != GameManager.Instance.CurEnvironmentBag) return false;
        var card = source.PeekCard();
        if (bag.Installed != null)
        {
            if (!bag.Installed.CanQuickInteract(card, out hint)) return false;
            bag.Installed.QuickIneract(source, count); owner.Refresh(); return true;
        }
        if (!CanInstall(card, out hint)) return false;
        Install(card); return true;
    }
    private void RemoveCotton()
    {
        var card = bag.Installed;
        if (card == null || card.CardId != "隔热棉" || TimeManager.Instance.IsAdvancing) return;
        var start = TimeManager.Instance.CurTime;
        card.LockThis();
        TimeManager.Instance.AddTime(15, () =>
        {
            card.UnlockThis();
            if ((TimeManager.Instance.CurTime - start).TotalMinutes < 15 || card.Destroyed) return;
            card.SlotCards.RemoveCard(card);
            GameManager.Instance.AddCard(card, true);
            owner.Refresh();
        });
    }
}
