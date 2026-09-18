using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class SaveHubBuilder
{
    private static SelectionFrameMotion SelectionFrame(Transform parent, bool marker = false)
    {
        if (frameSprite == null)
            frameSprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Textures/UI.psd").OfType<Sprite>().First(s => s.name == "UI_HoveredFrame");
        var rect = Rect("SelectionFrame", parent);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(130, 44);
        Image(rect, frameSprite);
        var image = rect.GetComponent<Image>(); image.color = SaveHubButton.Accent; image.raycastTarget = false;
        rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var motion = rect.gameObject.AddComponent<SelectionFrameMotion>();
        if (marker)
        {
            var dot = Rect("SelectionMark", rect);
            dot.anchorMin = dot.anchorMax = dot.pivot = new Vector2(1, .5f);
            dot.sizeDelta = new Vector2(6, 6); dot.anchoredPosition = new Vector2(-20, 0);
            var graphic = dot.gameObject.AddComponent<Image>(); graphic.color = SaveHubButton.Accent; graphic.raycastTarget = false;
        }
        rect.gameObject.SetActive(false);
        return motion;
    }

    // Upgrade only the two affected prefabs; do not regenerate scenes or unrelated UI.
    [MenuItem("Tools/存档/更新选择动效")]
    public static void UpgradeSelectionMotion()
    {
        const string settingsPath = "Assets/Resources/Prefabs/UI/Settings/SaveSettingsPanel.prefab";
        var settingsRoot = PrefabUtility.LoadPrefabContents(settingsPath);
        try
        {
            var settings = settingsRoot.GetComponent<SaveSettingsView>();
            if (settings.categorySelection == null) settings.categorySelection = SelectionFrame(settings.navigationScroll.content, true);
            PrefabUtility.SaveAsPrefabAsset(settingsRoot, settingsPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(settingsRoot); }

        var hubRoot = PrefabUtility.LoadPrefabContents(Output);
        try
        {
            var hub = hubRoot.GetComponent<SaveHubUI>();
            if (hub.filterSelection == null) hub.filterSelection = SelectionFrame(hub.filterBar);
            if (hub.worldlines.selectionFrame == null) hub.worldlines.selectionFrame = SelectionFrame(hub.worldlines.scroll.content);
            PrefabUtility.SaveAsPrefabAsset(hubRoot, Output);
        }
        finally { PrefabUtility.UnloadPrefabContents(hubRoot); }
        AssetDatabase.SaveAssets();
    }
}
