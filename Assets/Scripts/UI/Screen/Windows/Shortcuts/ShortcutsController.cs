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
                if (shortcut.name == "Settings") continue; // Global settings are not a desktop window or tutorial unlock.
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

        SelectWithTween(appName);

        selectedAppName = appName;
    }

    private void SelectWithTween(string appName)
    {
        Vector2 startPos = string.IsNullOrEmpty(selectedAppName) ?
            selectRect.anchoredPosition :
            (shortcuts[selectedAppName].transform as RectTransform).anchoredPosition;

        selectRect.anchoredPosition = startPos;

        Vector2 targetPos = (shortcuts[appName].transform as RectTransform).anchoredPosition;

        // 显示选中框
        selectRect.gameObject.SetActive(true);

        // 播放选中框移动动画
        AnimationManager.Instance.PlayAnchorMove(selectRect, targetPos, ease: Ease.OutBack);
    }

    public void ClearSelection()
    {
        selectedAppName = null;
        selectRect.gameObject.SetActive(false);
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

        if (!string.IsNullOrEmpty(selectedAppName))
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
}
