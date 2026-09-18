using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShortcutsController : MonoBehaviour
{
    [SerializeField] private RectTransform layoutTransform;
    [SerializeField] private RectTransform selectRect;

    private Dictionary<string, HoverableButton> shortcuts = new();

    private string selectedAppName;
    private CustomMenuItem settingsShortcut;
    private bool settingsOpen;

    #region 临时
    [SerializeField] private GameObject restButton;
    #endregion

    public HoverableButton this[string appName]
    {
        get
        {
            if (shortcuts.ContainsKey(appName))
                return shortcuts[appName];
            return null;
        }
    }

    private void Start()
    {
        for (int i = 0; i < layoutTransform.childCount; i++)
        {
            if (layoutTransform.GetChild(i).TryGetComponent<CustomMenuItem>(out var shortcut))
            {
                // Settings shares the travelling frame, but stays outside desktop-window unlocks.
                if (shortcut.name == "Settings") { settingsShortcut = shortcut; continue; }
                shortcuts.Add(shortcut.name, shortcut);
                SetOpened(shortcut, false);
                shortcut.onClick.AddListener(() =>
                {
                    if (shortcut.name != selectedAppName)
                        WindowsManager.Instance.OpenWindow(shortcut.name);
                    else
                        WindowsManager.Instance.MinimizeWindow(shortcut.name);
                });
            }
        }
        selectRect.gameObject.SetActive(false);

        LayoutRebuilder.ForceRebuildLayoutImmediate(layoutTransform);
        if (settingsOpen && settingsShortcut != null) SelectWithTween(settingsShortcut.rectTransform);

        #region 新手教程
        var unlockedShortcuts = GameDataManager.Instance.WindowsData.unlockedShortcuts;
        if (!GameDataManager.Instance.CurLoad.skipGuide) // 如果新手教程未跳过
        {
            unlockedShortcuts.Add("Chat");
            // 显示已解锁的快捷方式
            foreach (var appName in shortcuts.Keys)
            {
                SetLocked(appName, !unlockedShortcuts.Contains(appName), false);
            }

            #region 临时
            restButton.SetActive(GameDataManager.Instance.WindowsData.unlockedShortcuts.Contains("Rest"));
            #endregion
        }
        #endregion
    }

    public void SelectAppShortcut(string appName)
    {
        if (appName == selectedAppName) return;

        if (!shortcuts.ContainsKey(appName)) return;

        // 快捷方式未解锁
        if (!shortcuts[appName].gameObject.activeSelf) return;

        if (!settingsOpen) SelectWithTween(appName);

        selectedAppName = appName;
    }

    private void SelectWithTween(string appName)
    {
        SelectWithTween(shortcuts[appName].rectTransform);
    }

    private void SelectWithTween(RectTransform target)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(layoutTransform);
        selectRect.gameObject.SetActive(true);
        // Keep the existing OutBack motion and retarget from its current position, even while paused.
        AnimationManager.Instance.PlayAnchorMove(selectRect, target.anchoredPosition, ease: Ease.OutBack).SetUpdate(true);
    }

    public void SetSettingsOpen(bool value)
    {
        if (settingsOpen == value) return;
        settingsOpen = value;
        if (value && settingsShortcut != null) SelectWithTween(settingsShortcut.rectTransform);
        else if (!value)
        {
            if (!string.IsNullOrEmpty(selectedAppName) && shortcuts.ContainsKey(selectedAppName)
                && shortcuts[selectedAppName].gameObject.activeSelf) SelectWithTween(selectedAppName);
            else { selectRect.DOKill(); selectRect.gameObject.SetActive(false); }
        }
    }

    public void ClearSelection()
    {
        selectedAppName = null;
        if (!settingsOpen) { selectRect.DOKill(); selectRect.gameObject.SetActive(false); }
    }

    public void SetOpened(string appName, bool value)
    {
        if (shortcuts.ContainsKey(appName))
            SetOpened(shortcuts[appName], value);
    }

    private void SetOpened(HoverableButton shortcut, bool value)
    {
        var color = value? ColorManager.White: ColorManager.DarkGrey;
        shortcut.currentColor = color;
        shortcut.ChangeColor(color);
    }

    public void SetLocked(string appName, bool value, bool blink)
    {
        #region 临时
        if (appName == "Rest")
        {
            restButton.SetActive(true);
            return;
        }
        #endregion

        if (!shortcuts.ContainsKey(appName)) return;

        shortcuts[appName].gameObject.SetActive(!value);
        MonoUtility.UpdateHorizontalLayoutSize(layoutTransform.GetComponent<HorizontalLayoutGroup>());
        (transform as RectTransform).sizeDelta = new Vector2(layoutTransform.sizeDelta.x, (transform as RectTransform).sizeDelta.y);

        if (settingsOpen && settingsShortcut != null)
            SelectWithTween(settingsShortcut.rectTransform);
        else if (!string.IsNullOrEmpty(selectedAppName))
            SelectWithTween(selectedAppName);

        if (!value && blink)
            // 按钮闪烁
            shortcuts[appName].StartBlinking();
    }

    public List<string> GetUnlockedShortcuts()
    {
        List<string> list = new();
        foreach (var shortcut in shortcuts.Values)
        {
            if (shortcut.gameObject.activeSelf)
                list.Add(shortcut.name);
        }

        #region 临时
        if (restButton.activeSelf)
        {
            list.Add("Rest");
        }
        #endregion

        return list;
    }

    private void OnDisable() { if (selectRect != null) selectRect.DOKill(); }
}
