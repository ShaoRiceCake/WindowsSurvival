using DanielLochner.Assets.SimpleScrollSnap;
using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

public class DateTimeScrollSnap : MonoBehaviour
{
    [SerializeField] private SimpleScrollSnap dateScrollSnap;
    [SerializeField] private SimpleScrollSnap hourScrollSnap;
    [SerializeField] private SimpleScrollSnap minuteScrollSnap;
    [SerializeField] private RectTransform timePeriodRectTransform;

    private DateTime lastDateTime;
    private float speed = 50f / (12 * 60);

    private void Awake()
    {
        foreach (Transform child in hourScrollSnap.Content)
        {
            if (child.TryGetComponent<Text>(out var text))
            {
                text.text = $"{child.GetSiblingIndex():D2}";
            }
        }
        foreach (Transform child in minuteScrollSnap.Content)
        {
            if (child.TryGetComponent<Text>(out var text))
            {
                text.text = $"{child.GetSiblingIndex():D2}";
            }
        }

        EventManager.Instance.AddListener(EventType.EndChangeTime, UpdateTime);
    }

    private void OnDestroy()
    {
        EventManager.Instance.RemoveListener(EventType.EndChangeTime, UpdateTime);
    }

    private void Start()
    {
        var season = transform.root.GetComponentsInChildren<Text>(true);
        foreach (var label in season)
            if (label.name == "SeasonText" && label.GetComponent<ClimateSeasonLabel>() == null) label.gameObject.AddComponent<ClimateSeasonLabel>();
        Init();
        dateScrollSnap.OnPanelCentered.AddListener((_, _) =>
        {
            for (int i = 0; i < dateScrollSnap.Content.childCount; i++)
            {
                SetChildText(dateScrollSnap.CenteredPanel + i, (TimeManager.Instance.Days + i).ToString());
            }
        });
    }

    public void Init()
    {
        lastDateTime = TimeManager.Instance.CurTime;

        // 初始化日期
        for (int i = 0; i < dateScrollSnap.Content.childCount; i++)
        {
            SetChildText(dateScrollSnap.CenteredPanel + i, (TimeManager.Instance.Days + i).ToString());
        }

        // 初始化小时和分钟
        hourScrollSnap.GoToPanel(GetChildIndex(hourScrollSnap, lastDateTime.Hour));
        minuteScrollSnap.GoToPanel(GetChildIndex(minuteScrollSnap, lastDateTime.Minute));

        // 初始化日月图标
        int totalMinutes = lastDateTime.Hour * 60 + lastDateTime.Minute;
        var newAnchoredPos = new Vector2(timePeriodRectTransform.anchoredPosition.x, timePeriodRectTransform.anchoredPosition.y - totalMinutes * speed);
        timePeriodRectTransform.anchoredPosition = newAnchoredPos;
        foreach (RectTransform child in timePeriodRectTransform)
        {
            while (child.anchoredPosition.y + timePeriodRectTransform.anchoredPosition.y <= -100f)
            {
                child.anchoredPosition = new Vector2(child.anchoredPosition.x, child.anchoredPosition.y + 200);
            }
        }
    }

    private void SetChildText(int index, string text)
    {
        index = GetChildIndex(dateScrollSnap, index);
        dateScrollSnap.Content.GetChild(index).GetComponent<Text>().text = text;
    }

    private void UpdateTime()
    {
        DateTime curTime = TimeManager.Instance.CurTime;

        var m = curTime.Minute - lastDateTime.Minute;
        var h = curTime.Hour - lastDateTime.Hour;
        var d = curTime.Day - lastDateTime.Day;
        
        minuteScrollSnap.GoToPanel(GetChildIndex(minuteScrollSnap, m + minuteScrollSnap.CenteredPanel));
        hourScrollSnap.GoToPanel(GetChildIndex(hourScrollSnap, h + hourScrollSnap.CenteredPanel));
        dateScrollSnap.GoToPanel(GetChildIndex(dateScrollSnap, d + dateScrollSnap.CenteredPanel));

        // 实现无限滚动日月图标
        float totalMinutes = (float)(curTime - lastDateTime).TotalMinutes;
        var newAnchoredPos = new Vector2(timePeriodRectTransform.anchoredPosition.x, timePeriodRectTransform.anchoredPosition.y - totalMinutes * speed);
        AnimationManager.Instance.PlayAnchorMove(timePeriodRectTransform, newAnchoredPos, totalMinutes / 100).OnComplete(() =>
        {
            foreach (RectTransform child in timePeriodRectTransform)
            {
                while (child.anchoredPosition.y + timePeriodRectTransform.anchoredPosition.y <= -100f)
                {
                    child.anchoredPosition = new Vector2(child.anchoredPosition.x, child.anchoredPosition.y + 200);
                }
            }
        });

        lastDateTime = curTime;
    }

    private int GetChildIndex(SimpleScrollSnap scroll, int index)
    {
        return (index % scroll.Content.childCount + scroll.Content.childCount) % scroll.Content.childCount;
    }
}
