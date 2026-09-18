using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Editor-only authoring. The resulting nested prefabs are the runtime source of truth.
public static partial class SaveHubBuilder
{
    private const string SettingsFolder = "Assets/Resources/Prefabs/UI/Settings";

    public static void BuildPrefabBatch()
    {
        if (!Application.isBatchMode) throw new System.InvalidOperationException("此入口仅用于隔离工程。");
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/StartScene.unity");
        Build();
    }

    private static SaveSettingsView SettingsPanel(RectTransform window, Slider sliderTemplate)
    {
        Directory.CreateDirectory(SettingsFolder);
        var rowPrefab = SettingRowPrefab(sliderTemplate);
        var root = Rect("SaveSettingsPanel", null); root.sizeDelta = new Vector2(1290, 652);
        var view = root.gameObject.AddComponent<SaveSettingsView>();
        view.settingsIcon = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Textures/Icons.psd").OfType<Sprite>().First(s => s.name == "Icons_Settings");
        Divider(root,"SidebarDivider",248,0,1,634);
        view.navigationScroll = SettingsScroll("Categories",root,24,30,204,570);
        var navLayout = view.navigationScroll.content.gameObject.AddComponent<VerticalLayoutGroup>();
        navLayout.spacing=16; navLayout.padding=new RectOffset(2,2,2,2);
        navLayout.childControlWidth=navLayout.childControlHeight=true;navLayout.childForceExpandWidth=true;navLayout.childForceExpandHeight=false;
        view.navigationScroll.content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        view.categorySelection=SelectionFrame(view.navigationScroll.content,true);
        var templates = Rect("Templates",root);templates.gameObject.SetActive(false);
        view.categoryTemplate=PositionedButton(templates,"Category","游戏",0,0,190,60,26);
        var tabLayout=view.categoryTemplate.gameObject.AddComponent<LayoutElement>();tabLayout.preferredHeight=60;
        view.categoryTemplate.text.alignment=TextAnchor.MiddleLeft;Stretch(view.categoryTemplate.text.rectTransform,26,8);
        var mark=Rect("SelectionMark",view.categoryTemplate.transform);mark.anchorMin=mark.anchorMax=new Vector2(1,.5f);mark.sizeDelta=new Vector2(6,6);mark.anchoredPosition=new Vector2(-23,0);
        var markImage=mark.gameObject.AddComponent<Image>();markImage.color=SaveHubButton.Accent;markImage.raycastTarget=false;
        view.rowTemplate=rowPrefab.GetComponent<SaveSettingRow>();

        var right=Rect("Page",root);Place(right,286,28,962,584);view.pageFade=right.gameObject.AddComponent<CanvasGroup>();
        var game=Rect("Game",right);Stretch(game);view.gamePage=game.gameObject;
        Caption(game,"NameLabel","时间线名称：",0,0,930,32,21,true);
        view.worldName=Caption(game,"WorldlineName","麦麦带着废铁刀寻找白爆矿",0,40,940,100,30);
        view.worldName.alignment=TextAnchor.UpperLeft;
        var summary=Surface("LatestSave",game);Place(summary,0,164,948,190);
        var icon=Rect("PlaceIcon",summary);Place(icon,26,28,66,74);
        view.placeIcon=icon.gameObject.AddComponent<Image>();view.placeIcon.preserveAspect=true;view.placeIcon.raycastTarget=false;
        view.place=Caption(summary,"Place","驾驶舱",120,24,800,43,29);
        view.gameTime=Caption(summary,"GameTime","温和季 · 第14天 14:30",120,72,800,35,24,true);
        Divider(summary,"RecentDivider",27,121,894,1);
        view.recent=Caption(summary,"Recent","最近保存  18:01  ·  自动保存",28,140,890,30,21,true);
        view.save=PositionedButton(game,"ManualSave","手动保存",0,380,463,60,26);
        view.load=PositionedButton(game,"LoadHistory","读取保存点",485,380,463,60,26);Arrow(view.load);

        var options=Rect("Options",right);Stretch(options);view.optionsPage=options.gameObject;
        view.categoryHeading=Caption(options,"Heading","声音",0,0,930,42,29);
        view.optionsScroll=SettingsScroll("SettingRows",options,0,62,958,394);
        var rowsLayout=view.optionsScroll.content.gameObject.AddComponent<VerticalLayoutGroup>();
        rowsLayout.childControlHeight=rowsLayout.childControlWidth=true;rowsLayout.childForceExpandHeight=false;rowsLayout.childForceExpandWidth=true;
        view.optionsScroll.content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        Divider(right,"FooterDivider",0,478,948,1);
        var exits=Rect("GameActions",right);Place(exits,0,514,374,50);view.gameActions=exits.gameObject;
        view.menu=PositionedButton(exits,"ReturnToMenu","返回主菜单",0,5,168,47,22);view.menu.secondary=true;
        view.quit=PositionedButton(exits,"QuitGame","退出游戏",184,5,142,47,22);view.quit.secondary=true;view.quit.dangerous=true;
        view.resume=PositionedButton(right,"Resume","继续游戏",758,512,190,57,25);view.resume.primary=true;Arrow(view.resume);
        view.reset=PositionedButton(right,"Reset","恢复本页默认",0,519,208,47,22);view.reset.secondary=true;
        view.apply=PositionedButton(right,"ApplyDisplay","应用显示设置",502,515,232,54,24);
        options.gameObject.SetActive(false);view.reset.gameObject.SetActive(false);view.apply.gameObject.SetActive(false);
        var asset=PrefabUtility.SaveAsPrefabAsset(root.gameObject,SettingsFolder+"/SaveSettingsPanel.prefab");
        Object.DestroyImmediate(root.gameObject);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,window);
        Place((RectTransform)instance.transform,0,68,1290,652);instance.SetActive(false);
        return instance.GetComponent<SaveSettingsView>();
    }

    private static GameObject SettingRowPrefab(Slider sliderTemplate)
    {
        var root=Rect("SaveSettingRow",null);root.sizeDelta=new Vector2(934,96);
        root.gameObject.AddComponent<LayoutElement>().preferredHeight=96;
        var view=root.gameObject.AddComponent<SaveSettingRow>();
        view.label=Caption(root,"Label","设置名称",0,15,490,38,27);
        view.help=Caption(root,"Help","",0,58,490,27,19,true);
        var divider=Rect("Divider",root);divider.anchorMin=Vector2.zero;divider.anchorMax=new Vector2(1,0);divider.offsetMin=new Vector2(0,0);divider.offsetMax=new Vector2(0,1);
        var line=divider.gameObject.AddComponent<Image>();line.color=new Color32(57,57,57,255);line.raycastTarget=false;

        var controls=Rect("Controls",root);controls.anchorMin=controls.anchorMax=new Vector2(1,.5f);controls.pivot=new Vector2(1,.5f);controls.anchoredPosition=Vector2.zero;controls.sizeDelta=new Vector2(366,80);
        var choice=Rect("Choice",controls);Stretch(choice);view.choiceGroup=choice.gameObject;
        view.choice=PositionedButton(choice,"Value","开启",146,14,220,49,23);
        var enabled=Rect("EnabledMark",view.choice.transform);Place(enabled,20,21,7,7);
        view.enabledMark=enabled.gameObject.AddComponent<Image>();view.enabledMark.color=SaveHubButton.Accent;view.enabledMark.raycastTarget=false;

        var stepper=Rect("Stepper",controls);Stretch(stepper);view.stepperGroup=stepper.gameObject;
        view.minus=PositionedButton(stepper,"Minus","-",98,17,44,44,25);
        view.value=Caption(stepper,"Value","10 分钟",148,17,166,44,24);view.value.alignment=TextAnchor.MiddleCenter;
        view.plus=PositionedButton(stepper,"Plus","+",322,17,44,44,25);

        var sliderGroup=Rect("Slider",controls);Stretch(sliderGroup);view.sliderGroup=sliderGroup.gameObject;
        view.slider=Object.Instantiate(sliderTemplate,sliderGroup);view.slider.name="Volume";view.slider.gameObject.SetActive(true);
        Place((RectTransform)view.slider.transform,0,20,268,35);
        view.percentage=Caption(sliderGroup,"Value","70%",278,15,88,45,23,true);view.percentage.alignment=TextAnchor.MiddleRight;
        view.help.gameObject.SetActive(false);stepper.gameObject.SetActive(false);sliderGroup.gameObject.SetActive(false);enabled.gameObject.SetActive(false);
        var asset=PrefabUtility.SaveAsPrefabAsset(root.gameObject,SettingsFolder+"/SaveSettingRow.prefab");
        Object.DestroyImmediate(root.gameObject);return asset;
    }

    private static ScrollRect SettingsScroll(string name,Transform parent,float x,float y,float width,float height)
    {
        var scroll=Scroll(name,parent);Place((RectTransform)scroll.transform,x,y,width,height);
        scroll.viewport.offsetMin=Vector2.zero;scroll.viewport.offsetMax=new Vector2(-18,0);
        var bar=(RectTransform)scroll.verticalScrollbar.transform;bar.anchoredPosition=Vector2.zero;bar.sizeDelta=new Vector2(14,-4);
        scroll.scrollSensitivity=42;return scroll;
    }

    private static void Arrow(SaveHubButton button)
    {
        var arrow=Caption(button.transform,"Arrow",">",0,0,22,30,22,true);
        arrow.alignment=TextAnchor.MiddleCenter;
        arrow.rectTransform.anchorMin=arrow.rectTransform.anchorMax=new Vector2(1,.5f);arrow.rectTransform.pivot=new Vector2(1,.5f);
        arrow.rectTransform.anchoredPosition=new Vector2(-18,0);arrow.rectTransform.sizeDelta=new Vector2(18,32);
        button.feedbackGlyph=arrow.rectTransform;
        button.text.rectTransform.offsetMin=new Vector2(16,6);button.text.rectTransform.offsetMax=new Vector2(-34,-6);
        if(button.primary)arrow.color=SaveHubButton.Accent;
    }
}
