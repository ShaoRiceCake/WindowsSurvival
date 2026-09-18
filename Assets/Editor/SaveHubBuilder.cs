using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Generates authored, editable prefab assets using the project's existing pixel UI sprites and font.
public static partial class SaveHubBuilder
{
    private const string Output = "Assets/Resources/Prefabs/UI/SaveHub.prefab";
    private static Font font;
    private static Sprite panelSprite, frameSprite;
    public static void BuildBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("此入口仅用于隔离工程的批处理生成。");
        EditorSceneManager.OpenScene("Assets/Scenes/StartScene.unity");
        ConfigureGameScene();
        Build();
    }
    [MenuItem("Tools/存档/生成界面")]
    public static void Build()
    {
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Font/tianwangxingxiangsu.fontsettings");
        panelSprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Textures/UI.psd").OfType<Sprite>().First(s => s.name == "UI_Panel");
        frameSprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Textures/UI.psd").OfType<Sprite>().First(s => s.name == "UI_HoveredFrame");
        var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(buildScene);
        var root = Rect("SaveHub", null);
        var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
        var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; scaler.referencePixelsPerUnit = 2;
        root.gameObject.AddComponent<GraphicRaycaster>();
        var hub = root.gameObject.AddComponent<SaveHubUI>();
        var overlay = (RectTransform)Clone("ModalBackdrop", root).transform;
        overlay.name="ModalBlocker";overlay.gameObject.SetActive(true);Stretch(overlay);hub.panel=overlay.gameObject;

        // Derive from the shipped window: preserve its frame, title bar and authored artwork.
        var window = Clone("Windows/CustomWindow", overlay);
        PrefabUtility.UnpackPrefabInstance(window, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        UnityEngine.Object.DestroyImmediate(window.GetComponent<CustomWindow>());
        UnityEngine.Object.DestroyImmediate(window.transform.Find("Content").gameObject);
        UnityEngine.Object.DestroyImmediate(window.transform.Find("ButtonLayout").gameObject);
        var panel = (RectTransform)window.transform; window.name = "SaveWindow"; panel.sizeDelta = new Vector2(1240, 820); panel.anchoredPosition = Vector2.zero; hub.window = panel;
        window.AddComponent<CanvasGroup>();
        var top = window.transform.Find("TopBar");
        foreach (var drag in window.GetComponentsInChildren<DragMoveHandler>(true)) UnityEngine.Object.DestroyImmediate(drag);
        foreach (var click in window.GetComponentsInChildren<DoubleClickHandler>(true)) UnityEngine.Object.DestroyImmediate(click);
        PrepareButtons(window);
        top.gameObject.AddComponent<SaveWindowDrag>().window = panel;
        top.Find("MaximizeButton").gameObject.SetActive(false); top.Find("MinimizeButton").gameObject.SetActive(false);
        var organize = top.Find("OrganizeButton"); if (organize != null) organize.gameObject.SetActive(false);
        hub.closeButton = top.Find("CloseButton").GetComponent<HoverableButton>();
        var names = top.GetComponentsInChildren<Text>(true).Where(t => t.name == "Name").ToArray();
        hub.title = names.First(t => t.gameObject.activeInHierarchy);
        foreach (var name in names) { name.text = "航行记录"; name.rectTransform.sizeDelta = new Vector2(800, 40); }
        window.transform.Find("Frame").gameObject.SetActive(true);
        var sub = Label("Subtitle", panel, "", 24); Place(sub.rectTransform, 30, 86, 1180, 42); hub.subtitle = sub;
        var filters = Rect("Filters", panel); Place(filters, 30, 137, 475, 44);
        var fl = filters.gameObject.AddComponent<HorizontalLayoutGroup>(); fl.spacing = 8; fl.childControlWidth = true; fl.childForceExpandWidth = false; fl.childControlHeight = true; fl.childForceExpandHeight = true;
        filters.gameObject.SetActive(false); hub.filterBar = filters;
        var scroll = Scroll("RunList", panel); Place((RectTransform)scroll.transform, 30, 138, 485, 604); hub.listScroll = scroll; hub.listContent = scroll.content;
        var detail = Scroll("Details", panel); Place((RectTransform)detail.transform, 545, 138, 665, 604); hub.detailScroll = detail; hub.detailContent = detail.content;
        var layout = hub.detailContent.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 12; layout.padding = new RectOffset(12, 12, 8, 16); layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.childControlWidth = true; layout.childForceExpandWidth = true;
        hub.detailContent.gameObject.AddComponent<CanvasGroup>();
        hub.detailContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var status = Label("Status", panel, "", 20); Place(status.rectTransform, 30, 758, 1180, 40); hub.status = status;
        var templates = Rect("Templates", root); templates.gameObject.SetActive(false);
        var row = Button("SaveRow", templates, "驾驶舱\n温和季·第14天 14:30");
        var rr = (RectTransform)row.transform; rr.anchorMin = new Vector2(0, 1); rr.anchorMax = Vector2.one; rr.pivot = new Vector2(.5f, 1); rr.sizeDelta = new Vector2(-16, 96); row.minWidth = -16;
        row.text.alignment = TextAnchor.MiddleLeft; Stretch(row.text.rectTransform, 12, 6); row.text.fontSize = 24; hub.rowTemplate = row;
        var action = Button("Action", templates, "按钮"); action.gameObject.AddComponent<LayoutElement>().preferredHeight = 44; hub.actionTemplate = action;
        var text = Label("Info", templates, "", 24); text.gameObject.AddComponent<LayoutElement>().preferredHeight = 70; hub.textTemplate = text;
        var horizontal = Rect("Horizontal", templates); horizontal.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
        var hl = horizontal.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 16; hl.childControlWidth = true; hl.childForceExpandWidth = false; hl.childControlHeight = true; hl.childForceExpandHeight = true; hub.horizontalTemplate = horizontal;
        var inputRect = Rect("NameInput", templates); Image(inputRect, panelSprite); var input = inputRect.gameObject.AddComponent<InputField>(); inputRect.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
        var inputText = Label("Text", inputRect, "", 24); Stretch(inputText.rectTransform, 12, 6); input.textComponent = inputText; input.targetGraphic = inputRect.GetComponent<Image>(); input.selectionColor = new Color(1, 1, 1, .35f); hub.inputTemplate = input;
        // Retain the authored track and grip artwork of the game's horizontal scrollbar.
        var sliderObject = Clone("Controls/ScrollView/ScrollbarHorizontal", templates); sliderObject.name = "Slider"; PrepareButtons(sliderObject);
        var old = sliderObject.GetComponent<Scrollbar>(); var handle = old.handleRect; var graphic = old.targetGraphic;
        UnityEngine.Object.DestroyImmediate(old);
        handle.sizeDelta = new Vector2(24, -4);
        sliderObject.transform.Find("LeftArrow").gameObject.SetActive(false); sliderObject.transform.Find("RightArrow").gameObject.SetActive(false);
        var slider = sliderObject.AddComponent<Slider>(); slider.handleRect = handle; slider.targetGraphic = graphic; slider.transition = Selectable.Transition.None;
        sliderObject.AddComponent<LayoutElement>().preferredHeight = 36; hub.sliderTemplate = slider;
        var footer = Rect("Footer", panel); Place(footer, 30, 744, 1180, 48);
        var footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>(); footerLayout.spacing = 14; footerLayout.childAlignment = TextAnchor.MiddleRight; footerLayout.childControlWidth = true; footerLayout.childForceExpandWidth = false; footerLayout.childControlHeight = true; footerLayout.childForceExpandHeight = true;
        hub.footer = footer;
        var surface = Rect("SettingRow", templates); Image(surface, panelSprite); surface.GetComponent<Image>().raycastTarget = false;
        surface.gameObject.AddComponent<LayoutElement>().preferredHeight = 92;
        var sl = surface.gameObject.AddComponent<HorizontalLayoutGroup>(); sl.padding = new RectOffset(24,24,16,16); sl.spacing = 20; sl.childControlWidth = sl.childControlHeight = true; sl.childForceExpandWidth = false; sl.childForceExpandHeight = true; sl.childAlignment = TextAnchor.MiddleLeft;
        hub.settingRowTemplate = surface;
        hub.journalTemplate = Journal(templates);
        var toast=Surface("AutoSaveToast",root);toast.anchorMin=toast.anchorMax=toast.pivot=Vector2.one;
        toast.anchoredPosition=new Vector2(-36,-88);toast.sizeDelta=new Vector2(410,62);
        hub.autoSaveToast=toast.gameObject.AddComponent<CanvasGroup>();hub.autoSaveToast.alpha=0;
        hub.autoSaveToast.blocksRaycasts=false;hub.autoSaveToast.interactable=false;
        hub.autoSaveToastText=Caption(toast,"Message","自动保存完成",20,14,370,34,19);
        foreach(var toastGraphic in toast.GetComponentsInChildren<Graphic>())toastGraphic.raycastTarget=false;
        hub.worldlines = Worldlines(panel, templates);
        hub.settingsView = SettingsPanel(panel, hub.sliderTemplate);
        Directory.CreateDirectory(Path.GetDirectoryName(Output)); PrefabUtility.SaveAsPrefabAsset(root.gameObject, Output);
        BuildTransition();
        UnityEngine.Object.DestroyImmediate(root.gameObject); UnityEngine.SceneManagement.SceneManager.SetActiveScene(originalScene); EditorSceneManager.CloseScene(buildScene, true); AssetDatabase.SaveAssets();
        Debug.Log("SaveHub rebuilt from CustomWindow, TopBar, CustomWindowButton and ScrollView prefabs.");
    }
    private static void ConfigureGameScene()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity",OpenSceneMode.Additive);
        var objects=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        var manager=objects.Select(t=>t.GetComponent<WindowsManager>()).First(c=>c!=null);
        var settings=objects.First(t=>t.name=="Settings" && t.GetComponent<HoverableButton>()!=null).GetComponent<HoverableButton>();
        var serialized=new SerializedObject(manager);serialized.FindProperty("settingsButton").objectReferenceValue=settings;serialized.ApplyModifiedPropertiesWithoutUndo();
        foreach(var item in objects.Where(t=>t!=null && (t.name=="SaveButton" || t.name=="QuitButton")).ToArray())UnityEngine.Object.DestroyImmediate(item.gameObject);
        var group=objects.Select(t=>t!=null?t.GetComponent<WindowGroup>():null).First(c=>c!=null);
        var modal=(Transform)new SerializedObject(group).FindProperty("modal").objectReferenceValue;
        const string backdrop="Assets/Resources/Prefabs/UI/ModalBackdrop.prefab";
        if(!File.Exists(backdrop))PrefabUtility.SaveAsPrefabAssetAndConnect(modal.gameObject,backdrop,InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorSceneManager.CloseScene(scene,true);
    }
    private static GameObject Clone(string path, Transform parent) => (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UI/" + path + ".prefab"), parent);
    private static void BuildTransition()
    {
        var root = Rect("SaveTransition", null);
        var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1000;
        var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; scaler.referencePixelsPerUnit = 2;
        root.gameObject.AddComponent<GraphicRaycaster>();
        var view = root.gameObject.AddComponent<SaveTransitionUI>(); view.group = root.gameObject.AddComponent<CanvasGroup>();
        var shade = Rect("InputBlocker",root); Stretch(shade); shade.gameObject.AddComponent<Image>().color = new Color(0,0,0,.9f);
        var card = Surface("LoadingWindow",root); card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,.5f); card.sizeDelta=new Vector2(720,252);
        view.heading = Caption(card,"Heading","正在创建世界线",36,28,648,42,28);
        view.worldName = Caption(card,"WorldlineName","",36,83,648,58,21,true);
        var track = Rect("ProgressTrack",card); Place(track,36,167,648,6); track.gameObject.AddComponent<Image>().color=new Color32(65,65,65,255);
        var fill = Rect("Progress",track); Stretch(fill); view.progress=fill.gameObject.AddComponent<Image>();
        fill.anchorMax=new Vector2(0,1); view.progress.type=UnityEngine.UI.Image.Type.Simple;
        view.progress.color=SaveHubButton.Accent; view.progress.raycastTarget=false;
        view.phase = Caption(card,"Phase","正在准备…",36,195,600,28,19,true);
        var activity = Rect("Activity",card); Place(activity,674,205,6,6); activity.gameObject.AddComponent<Image>().color=SaveHubButton.Accent; view.activity=activity;
        // A batch editor can report a zero-sized display while authoring an overlay Canvas.
        // Do not serialize the CanvasScaler's temporary zero scale into the prefab.
        root.localScale = Vector3.one;
        PrefabUtility.SaveAsPrefabAsset(root.gameObject,"Assets/Resources/Prefabs/UI/SaveTransition.prefab"); UnityEngine.Object.DestroyImmediate(root.gameObject);
    }
    private static SaveWorldlineView Worldlines(RectTransform window, RectTransform templates)
    {
        var root = Rect("Worldlines", window); Place(root, 0, 68, 1240, 590);
        var view = root.gameObject.AddComponent<SaveWorldlineView>();
        Divider(root, "SidebarDivider", 332, 0, 1, 578);
        view.create = PositionedButton(root, "CreateWorldline", "+ 新建世界线", 28, 24, 186, 42, 19);
        view.import = PositionedButton(root, "ImportWorldline", "导入", 226, 24, 78, 42, 19);
        Caption(root, "ListHeading", "我的世界线", 30, 86, 204, 28, 18, true);
        view.count = Caption(root, "WorldlineCount", "0", 254, 86, 48, 28, 17, true);
        view.count.alignment = TextAnchor.MiddleRight;
        view.scroll = Scroll("WorldlineList", root); Place((RectTransform)view.scroll.transform, 24, 122, 290, 392);
        Stretch(view.scroll.viewport); view.scroll.viewport.offsetMin = new Vector2(4, 0); view.scroll.viewport.offsetMax = new Vector2(-22, 0);
        var bar = (RectTransform)view.scroll.verticalScrollbar.transform; bar.sizeDelta = new Vector2(18, -8); bar.anchoredPosition = Vector2.zero;
        view.scroll.scrollSensitivity = 44;
        var row = (SaveHubButton)Button("WorldlineRow", templates, "");
        var rowRect = (RectTransform)row.transform;
        rowRect.anchorMin = new Vector2(0, 1); rowRect.anchorMax = Vector2.one; rowRect.pivot = new Vector2(.5f, 1); rowRect.sizeDelta = new Vector2(0, SaveWorldlineView.RowHeight - 12);
        row.minWidth = 0; row.text.fontSize = 18; row.text.alignment = TextAnchor.UpperLeft;
        Stretch(row.text.rectTransform); row.text.rectTransform.offsetMin = new Vector2(14, 40); row.text.rectTransform.offsetMax = new Vector2(-14, -12);
        var summary = Label("Summary", rowRect, "", 15); summary.color = new Color32(172,172,172,255);
        summary.rectTransform.anchorMin = Vector2.zero; summary.rectTransform.anchorMax = new Vector2(1,0); summary.rectTransform.pivot = Vector2.zero;
        summary.rectTransform.offsetMin = new Vector2(14, 8); summary.rectTransform.offsetMax = new Vector2(-14, 42);
        view.rowTemplate = row;

        var details = Rect("WorldlineDetails", root); Place(details, 368, 22, 828, 460); view.details = details.gameObject;
        view.detailFade = details.gameObject.AddComponent<CanvasGroup>();
        view.mode = Caption(details, "Mode", "普通模式", 0, 0, 828, 30, 18, true);
        view.worldName = Caption(details, "WorldlineName", "", 0, 44, 828, 76, 26); view.worldName.alignment = TextAnchor.UpperLeft;
        var location = Surface("LatestProgress", details); Place(location, 0, 132, 828, 154);
        var icon = Rect("PlaceIcon", location); Place(icon, 24, 40, 68, 68);
        view.placeIcon = icon.gameObject.AddComponent<Image>(); view.placeIcon.preserveAspect = true; view.placeIcon.raycastTarget = false;
        view.place = Caption(location, "Place", "", 118, 38, 682, 34, 24);
        view.time = Caption(location, "GameTime", "", 118, 79, 682, 32, 20, true);
        Caption(details, "PlayedHeading", "累计游玩", 0, 318, 218, 30, 17, true);
        Caption(details, "RecordsHeading", "保存记录", 254, 318, 200, 30, 17, true);
        Caption(details, "RecentHeading", "最近游玩", 508, 318, 320, 30, 17, true);
        view.played = Caption(details, "Played", "", 0, 351, 240, 34, 21);
        view.records = Caption(details, "Records", "", 254, 351, 220, 34, 21);
        view.recent = Caption(details, "Recent", "", 508, 351, 320, 34, 20);
        view.hardRule = Caption(details, "HardcoreRule", "只保留最新进度，死亡后删除世界线。", 0, 412, 828, 36, 18, true);

        var empty = Rect("Empty", root); Place(empty, 368, 100, 828, 340); view.empty = empty.gameObject;
        var emptyTitle = Caption(empty, "Title", "麦麦根本不存在", 0, 52, 828, 50, 26); emptyTitle.alignment = TextAnchor.MiddleCenter;
        var emptyHint = Caption(empty, "Hint", "创建一条世界线，或导入一份已有的故事。", 0, 114, 828, 40, 20, true); emptyHint.alignment = TextAnchor.MiddleCenter;
        view.emptyCreate = PositionedButton(empty, "Create", "+ 新建世界线", 294, 190, 240, 48, 22); view.emptyCreate.primary = true;

        Divider(root, "ActionDivider", 368, 518, 828, 1);
        view.manage = PositionedButton(root, "ManageWorldline", "管理世界线", 368, 540, 152, 42, 19);
        view.load = PositionedButton(root, "LoadGame", "载入游戏", 1006, 538, 190, 46, 23); view.load.primary = true;
        view.load.text.rectTransform.offsetMax = new Vector2(-32, -6);
        var arrow = Caption(view.load.transform, "Arrow", "→", 154, 9, 24, 28, 21);
        view.load.feedbackGlyph = arrow.rectTransform;

        var menu = Rect("ManageMenu", root); Stretch(menu); view.menu = menu.gameObject;
        var blocker = menu.gameObject.AddComponent<Image>(); blocker.color = Color.clear;
        view.dismissMenu = menu.gameObject.AddComponent<Button>(); view.dismissMenu.transition = Selectable.Transition.None;
        var popup = Surface("Popup", menu); Place(popup, 368, 276, 226, 248); view.menuFade = popup.gameObject.AddComponent<CanvasGroup>();
        view.rename = PositionedButton(popup, "Rename", "修改名称", 16, 16, 194, 42, 19);
        view.copy = PositionedButton(popup, "Copy", "复制世界线", 16, 70, 194, 42, 19);
        view.share = PositionedButton(popup, "Share", "分享世界线", 16, 124, 194, 42, 19);
        Divider(popup, "DangerDivider", 16, 181, 194, 1);
        view.delete = PositionedButton(popup, "Delete", "删除世界线", 16, 194, 194, 38, 19); view.delete.dangerous = true;
        menu.gameObject.SetActive(false); root.gameObject.SetActive(false);
        return view;
    }
    private static Text Caption(Transform parent, string name, string value, float x, float y, float w, float h, int size, bool muted = false)
    {
        var text = Label(name, parent, value, size); Place(text.rectTransform, x, y, w, h);
        if (muted) text.color = new Color32(169,169,169,255); return text;
    }
    private static SaveHubButton PositionedButton(Transform parent, string name, string label, float x, float y, float w, float h, int size)
    {
        var button = (SaveHubButton)Button(name, parent, label); Place((RectTransform)button.transform,x,y,w,h);
        button.text.fontSize = size; return button;
    }
    private static RectTransform Surface(string name, Transform parent)
    {
        var root = Rect(name, parent); var fill = root.gameObject.AddComponent<Image>(); fill.color = new Color32(17,17,17,255);
        var border = Rect("Frame", root); Stretch(border); Image(border, frameSprite);
        border.GetComponent<Image>().color = new Color32(90,90,90,255); border.GetComponent<Image>().raycastTarget = false;
        return root;
    }
    private static void Divider(Transform parent, string name, float x, float y, float w, float h)
    {
        var rect = Rect(name, parent); Place(rect,x,y,w,h); var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color32(65,65,65,255); image.raycastTarget = false;
    }
    private static void PrepareButtons(GameObject root)
    {
        // Main-menu modal controls do not have gameplay MouseManager/WindowsManager dependencies.
        foreach (var mouse in root.GetComponentsInChildren<ChangeMouse>(true)) UnityEngine.Object.DestroyImmediate(mouse);
        foreach (var b in root.GetComponentsInChildren<HoverableButton>(true)) b.useUnscaledTime = true;
    }
    private static RectTransform Rect(string name, Transform parent) { var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false); return r; }
    private static void Stretch(RectTransform r, float x = 0, float y = 0) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(x, y); r.offsetMax = new Vector2(-x, -y); }
    private static void Place(RectTransform r, float x, float y, float w, float h) { r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); }
    private static void Image(RectTransform r, Sprite sprite) { var image = r.gameObject.AddComponent<Image>(); image.sprite = sprite; image.type = UnityEngine.UI.Image.Type.Sliced; }
    private static Text Label(string name, Transform parent, string value, int size)
    {
        var r = Rect(name, parent); var t = r.gameObject.AddComponent<Text>(); t.font = font; t.fontSize = size; t.text = value; t.color = Color.white; t.alignment = TextAnchor.MiddleLeft; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; t.raycastTarget = false; t.supportRichText = false; return t;
    }
    private static HoverableButton Button(string name, Transform parent, string label)
    {
        var obj = Clone("Controls/Button/CustomWindowButton", parent); obj.name = name; PrepareButtons(obj);
        PrefabUtility.UnpackPrefabInstance(obj, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        var previous = obj.GetComponent<HoverableButton>(); var caption = previous.text;
        UnityEngine.Object.DestroyImmediate(previous);
        UnityEngine.Object.DestroyImmediate(obj.transform.Find("Hovered").gameObject);
        var background = obj.AddComponent<Image>(); background.color = new Color32(17,17,17,255);
        var border = Rect("Frame", obj.transform); Stretch(border); Image(border, frameSprite);
        border.GetComponent<Image>().raycastTarget = false; border.GetComponent<Image>().color = new Color32(118,118,118,255); border.SetAsFirstSibling();
        var b = obj.AddComponent<SaveHubButton>(); b.text = caption; b.frame = border.GetComponent<Image>();
        b.hoveredGraphics = new System.Collections.Generic.List<Graphic>(); b.textsNeedToReverseColor = Array.Empty<Text>(); b.imagseNeedToReverseColor = Array.Empty<Image>();
        b.useUnscaledTime = true; b.text.text = label; b.text.supportRichText = false; b.text.fontSize = 24;
        b.text.raycastTarget = false; Stretch(b.text.rectTransform,12,6);
        var nav = obj.AddComponent<Selectable>(); nav.transition = Selectable.Transition.None; nav.targetGraphic = background;
        return b;
    }
    private static SaveJournalRow Journal(Transform templates)
    {
        var r = Rect("JournalRow", templates); r.sizeDelta = new Vector2(1120,84);
        var row = r.gameObject.AddComponent<SaveJournalRow>(); row.height = r.gameObject.AddComponent<LayoutElement>(); row.height.preferredHeight = 84;
        row.day = Label("Day",r,"第14天",26); Place(row.day.rectTransform,0,2,112,35);
        row.time = Label("Time",r,"14:30",22); Place(row.time.rectTransform,0,42,112,30);
        var rail=Rect("Rail",r); Stretch(rail); rail.anchorMin=Vector2.zero; rail.anchorMax=new Vector2(0,1); rail.pivot=new Vector2(0,.5f); rail.anchoredPosition=new Vector2(120,0); rail.sizeDelta=new Vector2(2,0);
        var railImage=rail.gameObject.AddComponent<Image>(); railImage.color=new Color32(65,65,65,255); railImage.raycastTarget=false;
        var marker=Rect("Marker",r); Place(marker,117,28,8,8); row.marker=marker.gameObject.AddComponent<Image>(); row.marker.raycastTarget=false;
        row.button=(SaveHubButton)Button("Record",r,"驾驶舱"); var card=(RectTransform)row.button.transform; Stretch(card); card.offsetMin=new Vector2(144,0);
        Place(row.button.text.rectTransform,76,12,700,45); row.button.text.alignment=TextAnchor.MiddleLeft;
        var icon=Rect("Place",card); Place(icon,20,17,40,40); row.icon=icon.gameObject.AddComponent<Image>(); row.icon.preserveAspect=true; row.icon.raycastTarget=false;
        row.kind=Label("Kind",card,"手动保存",20); row.kind.alignment=TextAnchor.MiddleRight;
        row.kind.rectTransform.anchorMin=row.kind.rectTransform.anchorMax=Vector2.one; row.kind.rectTransform.pivot=Vector2.one; row.kind.rectTransform.anchoredPosition=new Vector2(-22,-22); row.kind.rectTransform.sizeDelta=new Vector2(140,32);
        var detail=Rect("Expanded",card); Stretch(detail); detail.offsetMin=new Vector2(76,10); detail.offsetMax=new Vector2(-22,-66); row.details=detail.gameObject.AddComponent<CanvasGroup>();
        row.information=Label("Information",detail,"",21); Place(row.information.rectTransform,0,0,770,64);
        row.retain=(SaveHubButton)Button("Retain",detail,"保留为手动记录"); Place((RectTransform)row.retain.transform,0,76,230,38); row.retain.text.fontSize=20;
        r.gameObject.AddComponent<RectMask2D>();
        return row;
    }
    private static ScrollRect Scroll(string name, Transform parent)
    {
        var obj = Clone("Controls/ScrollView/ScrollView", parent); obj.name = name; PrepareButtons(obj);
        var scrollBackground = obj.GetComponent<Image>(); if(scrollBackground!=null) scrollBackground.color=Color.clear;
        var scroll = obj.GetComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 36;
        scroll.horizontalScrollbar.gameObject.SetActive(false); scroll.horizontalScrollbar = null;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        var bar = (RectTransform)scroll.verticalScrollbar.transform;
        bar.anchorMin = new Vector2(1, 0); bar.anchorMax = Vector2.one; bar.pivot = new Vector2(1, .5f); bar.anchoredPosition = new Vector2(-6, 0); bar.sizeDelta = new Vector2(32, -12);
        Stretch(scroll.viewport); scroll.viewport.offsetMin = new Vector2(8, 8); scroll.viewport.offsetMax = new Vector2(-42, -8);
        scroll.content.anchorMin = new Vector2(0, 1); scroll.content.anchorMax = Vector2.one; scroll.content.pivot = new Vector2(.5f, 1); scroll.content.sizeDelta = Vector2.zero; scroll.content.anchoredPosition = Vector2.zero;
        return scroll;
    }
}

