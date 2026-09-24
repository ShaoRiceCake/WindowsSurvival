using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Idempotent authoring of V6 configuration and prefab-backed UI.</summary>
public static class ClimateAssetBuilder
{
    [MenuItem("Tools/Climate/Apply V6 configuration and UI")]
    public static void Run()
    {
        ConfigurePlaces(); ConfigureBody(); ConfigureRecipes();
        var climate = GetOrCreate<ClimateConfig>("Assets/Resources/Config/ClimateConfig.asset");
        if (climate.connections == null || climate.connections.Length == 0) climate.connections = ClimateConfig.DefaultConnections();
        EditorUtility.SetDirty(climate);
        AssetDatabase.SaveAssets();
        BuildDetails(); BuildEnvironment(); BuildIce();
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("CLIMATE_ASSETS_READY");
    }
    private static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        var value = AssetDatabase.LoadAssetAtPath<T>(path);
        if (value != null) return value;
        value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); return value;
    }
    private static void ConfigurePlaces()
    {
        float[] temperature = { 18, 16, 20, 14, 12, 8, 6, 4, 10 };
        float[] area = { 15, 20, 15, 60, 60, 50, 15, 50, 15 };
        int[] insulation = { 2, 2, 2, 0, 0, 0, 1, 1, 1 };
        float[] season = { .25f, .25f, .2f, 1, 1, 1, .45f, .5f, .35f };
        float[] day = { .15f, .15f, .1f, 1, 1, 1, .2f, .2f, .15f };
        foreach (var place in Resources.LoadAll<PlaceData>("ScriptableObject/Place"))
        {
            int i = (int)place.placeType;
            place.initialBagStateConfig.roomTemperature = temperature[i];
            place.minCoord = 0; place.maxCoord = area[i]; place.insulationLevel = insulation[i];
            place.seasonInfluence = season[i]; place.dayInfluence = day[i];
            place.supportsCable = true; place.supportsTowRope = place.isInWater; place.supportsInsulation = place.isIndoor;
            place.climateMaterialId = i >= 6 ? "暖绒" : "";
            EditorUtility.SetDirty(place);
        }
    }
    private static void ConfigureBody()
    {
        var state = AssetDatabase.LoadAssetAtPath<PlayerStateConfigSO>("Assets/Resources/Config/PlayerStates/BodyTemperatureConfig.asset");
        state.initialValue = 36.5f; state.minValue = 30; state.maxValue = 42; state.normParam = 0; state.precision = 3; state.basicChangeRate = 0;
        state.thresholds = new();
        void Band(float low, float high, string name, float health, float hunger, float hydration, bool includeLow = true, bool excludeHigh = true)
        {
            state.thresholds.Add(new StateThresholdConfig { minValueExclude = low, maxValueInclude = high, levelName = name,
                includeMinimum = includeLow, excludeMaximum = excludeHigh, effect = new StateEffectConfig { healthRate = health, fulnessRate = hunger, thirstRate = hydration } });
        }
        Band(30, 32, "严重失温", -2, -1.5f, 0);
        Band(32, 35, "失温", -.5f, -1, 0);
        Band(35, 35.5f, "偏冷", 0, -.4f, 0);
        Band(35.5f, 37.5f, "体温舒适", 0, 0, 0, true, false);
        Band(37.5f, 38.5f, "偏热", 0, 0, -.4f, false);
        Band(38.5f, 40, "过热", -.5f, 0, -1);
        Band(40, 42, "严重过热", -2, 0, -2, true, false);
        state.lowDangerLevels = new() { 1, 2, 4, 5 }; state.highDangerLevels = new() { 0, 6 };
        EditorUtility.SetDirty(state);
    }
    private static void ConfigureRecipes()
    {
        var techs = Resources.LoadAll<ScriptableTechnologyNode>("ScriptableObject/Technology");
        var libraries = Resources.LoadAll<ScriptableRecipeLibrary>("ScriptableObject/Craft/Libraries");
        void Recipe(string id, RecipeType type, string tech, int minutes, int count, params RecipeMaterial[] materials)
        {
            var recipe = GetOrCreate<ScriptableRecipe>($"Assets/Resources/ScriptableObject/Craft/Recipes/{id}.asset");
            recipe.cardId = id; recipe.craftType = type; recipe.craftTime = minutes; recipe.outputCount = count; recipe.materials = materials.ToList();
            EditorUtility.SetDirty(recipe);
            var library = libraries.Single(l => l.craftType == type);
            if (!library.recipes.Contains(recipe)) library.recipes.Add(recipe);
            EditorUtility.SetDirty(library);
            var node = techs.Single(t => t.name == tech);
            // A recipe belongs to exactly one technology. Remove stale links left by
            // previous V6 configurations before assigning its current unlock node.
            foreach (var other in techs)
            {
                if (other == node) continue;
                if (other.recipes != null && other.recipes.RemoveAll(r => r == recipe) > 0)
                    EditorUtility.SetDirty(other);
            }
            node.recipes ??= new(); if (!node.recipes.Contains(recipe)) node.recipes.Add(recipe);
            EditorUtility.SetDirty(node);
        }
        Recipe("融冰浮标", RecipeType.Tool, "基建", 30, 1, new("韧性胶管", 2), new("盐", 3), new("暖绒", 2));
        Recipe("牵引绳", RecipeType.Tool, "水下运输", 30, 1, new("韧性胶管", 2), new("盐", 3), new("纤维", 12));
        Recipe("防水电缆", RecipeType.Tool, "电学常识", 15, 1, new("韧性胶管", 2), new("磁性触手", 2));
        Recipe("隔热棉", RecipeType.Tool, "基建", 30, 1, new("暖绒", 5), new("纤维", 6));
        Recipe("保温服", RecipeType.Equipment, "极寒应对措施", 60, 1, new("暖绒", 4), new("纤维", 12), new("燃素", 6));
        Recipe("暖暖贴", RecipeType.Medic, "极寒应对措施", 15, 3, new("韧性胶管", 1), new("燃素", 1));
    }
    private static T Read<T>(SerializedObject obj, string name) where T : Object => (T)obj.FindProperty(name).objectReferenceValue;
    private static void Write(SerializedObject obj, string name, Object value) => obj.FindProperty(name).objectReferenceValue = value;
    private static RectTransform Rect(string name, Transform parent)
    {
        var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); result.SetParent(parent, false); return result;
    }
    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }
    private static Text Text(string name, Transform parent, Font font, int size, string value = "")
    {
        var text = Rect(name, parent).gameObject.AddComponent<Text>(); text.font = font; text.fontSize = size; text.text = value;
        text.color = Color.white; text.raycastTarget = false; text.alignment = TextAnchor.UpperLeft; text.horizontalOverflow = HorizontalWrapMode.Wrap; return text;
    }
    private static HoverableButton Button(HoverableButton template, Transform parent, string name)
    {
        var result = Object.Instantiate(template, parent); result.name = name; result.gameObject.SetActive(true);
        if (result.text == null) result.text = result.GetComponentInChildren<Text>(true);
        result.useUnscaledTime = true; return result;
    }
    private static void RemoveExisting(Transform root, string name)
    { var child = root.Find(name); if (child != null) Object.DestroyImmediate(child.gameObject); }
    private static void BuildDetails()
    {
        const string path = "Assets/Resources/Prefabs/UI/Windows/DetailsWindow.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var window = root.GetComponent<DetailsWindow>(); var so = new SerializedObject(window);
            var details = Read<HoverableButton>(so, "detailsButton"); var menu = Read<Transform>(so, "menuLayout");
            RemoveExisting(menu, "ModificationsButton");
            var tab = Button(details, menu, "ModificationsButton"); tab.text.text = "改装";
            Write(so, "modificationsButton", tab);
            var body = Read<RectTransform>(so, "contentsView").parent;
            RemoveExisting(body, "Modifications");
            var rect = Rect("Modifications", body); Stretch(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var view = rect.gameObject.AddComponent<ModificationDetailsView>();
            var scroll = rect.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true;
            var viewport = Rect("Viewport", rect); Stretch(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .01f); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport); content.anchorMin = new(0, 1); content.anchorMax = Vector2.one; content.pivot = new(.5f, 1); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.padding = new RectOffset(8, 16, 6, 6);
            layout.childControlWidth = true; layout.childForceExpandWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            var track = Rect("ScrollTrack", rect).gameObject.AddComponent<Image>(); track.color = new Color(.3f, .3f, .3f, .4f);
            Stretch(track.rectTransform, new(1, 0), Vector2.one, new(-8, 4), new(-2, -4));
            var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var handle = Rect("Handle", track.transform).gameObject.AddComponent<Image>(); handle.color = new Color(.65f, .65f, .65f);
            Stretch(handle.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            scrollbar.handleRect = handle.rectTransform; scrollbar.targetGraphic = handle;
            scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            var font = Read<Text>(so, "detailsText").font;
            view.summary = Text("Summary", content, font, 18); view.summary.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;
            var eventTemplate = Read<GameObject>(so, "eventButtonPrefab").GetComponent<HoverableButton>();
            view.rope = Row(content, font, eventTemplate, "TowRope"); view.cable = Row(content, font, eventTemplate, "Cable");
            view.cotton = Row(content, font, eventTemplate, "Cotton"); view.buoy = Row(content, font, eventTemplate, "Buoy");
            Write(so, "modificationsView", view); rect.gameObject.SetActive(false);
            var slot = Read<CardSlot>(so, "slot"); var parent = slot.transform.parent;
            RemoveExisting(parent, "PlaceDisplay");
            var place = Rect("PlaceDisplay", parent); Stretch(place, Vector2.zero, Vector2.one, new(8, 8), new(-8, -8));
            var image = Rect("PlaceImage", place).gameObject.AddComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
            Stretch(image.rectTransform, new(0, .3f), new(1, .9f), Vector2.zero, Vector2.zero);
            var title = Text("PlaceName", place, font, 18); title.alignment = TextAnchor.MiddleCenter;
            Stretch(title.rectTransform, new(0, .05f), new(1, .3f), Vector2.zero, Vector2.zero);
            Write(so, "placeDisplay", place.gameObject); Write(so, "placeImage", image); Write(so, "placeTitle", title); place.gameObject.SetActive(false);
            so.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static ModificationRow Row(Transform parent, Font font, HoverableButton template, string name)
    {
        // Narrow widths grow vertically and scroll instead of shrinking the pixel text.
        var rect = Rect(name, parent); rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 150;
        rect.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .045f);
        var row = rect.gameObject.AddComponent<ModificationRow>();
        row.title = Text("Title", rect, font, 22); Stretch(row.title.rectTransform, new(0, 1), new(1, 1), new(100, -34), new(-8, -8));
        row.status = Text("Status", rect, font, 18); row.status.color = new(.48f, .85f, .94f);
        Stretch(row.status.rectTransform, new(0, 1), new(1, 1), new(100, -54), new(-8, -34));
        row.description = Text("Description", rect, font, 20); Stretch(row.description.rectTransform, new(0, 1), new(1, 1), new(100, -98), new(-8, -56));
        var frame = Rect("AttachmentSlot", rect).gameObject.AddComponent<Image>(); frame.color = new Color(.4f, .75f, .85f, .15f);
        Stretch(frame.rectTransform, new(0, 1), new(0, 1), new(8, -100), new(90, -8));
        row.icon = Rect("Icon", frame.transform).gameObject.AddComponent<Image>(); row.icon.preserveAspect = true;
        Stretch(row.icon.rectTransform, Vector2.zero, Vector2.one, new(10, 24), new(-10, -4));
        row.slotLabel = Text("SlotLabel", frame.transform, font, 16); row.slotLabel.alignment = TextAnchor.MiddleCenter;
        Stretch(row.slotLabel.rectTransform, Vector2.zero, new(1, 0), new(0, 0), new(0, 24));
        row.install = Button(template, rect, "Install"); row.actionOne = Button(template, rect, "Primary"); row.actionTwo = Button(template, rect, "Secondary");
        foreach (var button in new[] { row.install, row.actionOne, row.actionTwo })
        {
            button.text.fontSize = 20;
            var br = button.transform as RectTransform; br.anchorMin = br.anchorMax = new(0, 0); br.pivot = Vector2.zero;
            br.sizeDelta = new(120, 32); br.anchoredPosition = new(button == row.actionTwo ? 238 : 100, 8);
            button.minWidth = 120;
        }
        return row;
    }
    private static void BuildEnvironment()
    {
        const string path = "Assets/Resources/Prefabs/UI/Windows/EnvironmentBagWindow.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var so = new SerializedObject(root.GetComponent<EnvironmentBagWindow>()); var explore = Read<HoverableButton>(so, "exploreButton");
            RemoveExisting(explore.transform.parent, "ModificationsButton");
            var button = Button(explore, explore.transform.parent, "ModificationsButton"); button.text.text = "改装";
            if (explore.text == null) explore.text = explore.GetComponentInChildren<Text>(true);
            explore.text.text = "探索";
            var left = explore.transform as RectTransform; var right = button.transform as RectTransform;
            left.sizeDelta = right.sizeDelta = new(132, 44); left.anchoredPosition = new(-70, left.anchoredPosition.y); right.anchoredPosition = new(70, left.anchoredPosition.y);
            explore.minWidth = button.minWidth = 132;
            foreach (var control in new[] { explore, button })
            {
                control.text.alignment = TextAnchor.MiddleCenter;
                // The existing icon/text HorizontalLayoutGroup reads intrinsic sizes.
                var label = control.text.rectTransform;
                label.anchorMin = label.anchorMax = new(.5f, .5f);
                label.sizeDelta = new(64, 40);
            }
            Write(so, "modificationsButton", button); so.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void BuildIce()
    {
        const string path = "Assets/Resources/Prefabs/UI/Controls/CardSlot/CardSlot.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var overlay = root.GetComponent<PassageIceOverlay>() ?? root.AddComponent<PassageIceOverlay>();
            RemoveExisting(root.transform, "IceFrame"); RemoveExisting(root.transform, "IceProgress");
            var font = root.GetComponentsInChildren<Text>(true).First().font;
            var sprite = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath("463677557ce92964a8482ac800b7428d")).OfType<Sprite>().FirstOrDefault(s => s.name == "UI_HoveredFrame");
            overlay.frame = Rect("IceFrame", root.transform).gameObject.AddComponent<Image>(); overlay.frame.sprite = sprite; overlay.frame.type = Image.Type.Sliced;
            overlay.frame.color = new(.45f, .85f, 1, .8f); overlay.frame.raycastTarget = false;
            Stretch(overlay.frame.rectTransform, Vector2.zero, Vector2.one, new(-3, -3), new(3, 3));
            overlay.extraInfo = Read<Text>(new SerializedObject(root.GetComponent<CardSlot>()), "moreInfoText");
            var progress = Rect("IceProgress", root.transform); Stretch(progress, Vector2.zero, new(1, 0), new(7, 0), new(-7, 12));
            progress.gameObject.AddComponent<Image>().color = new Color(.02f, .09f, .13f, .95f);
            overlay.progress = progress.gameObject.AddComponent<Slider>(); overlay.progress.minValue = 0; overlay.progress.maxValue = 200; overlay.progress.interactable = false;
            var fill = Rect("Fill", progress).gameObject.AddComponent<Image>(); fill.color = new(.2f, .55f, .7f, .8f); fill.raycastTarget = false; Stretch(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            overlay.progress.fillRect = fill.rectTransform;
            var marker = Rect("BlockedAt100", progress).gameObject.AddComponent<Image>(); marker.color = Color.white; marker.raycastTarget = false;
            Stretch(marker.rectTransform, new(.5f, 0), new(.5f, 1), new(-1, 0), new(1, 0));
            overlay.label = Text("IceLabel", progress, font, 11); overlay.label.alignment = TextAnchor.MiddleCenter; Stretch(overlay.label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            overlay.Clear(); PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
