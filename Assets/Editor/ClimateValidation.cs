using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Isolated real Play Mode regression. Never accesses the player's saves.</summary>
[InitializeOnLoad]
public static class ClimateValidation
{
    private const string Active = "ClimateValidationActive";
    private static readonly List<string> checks = new();
    private static readonly Stack<IEnumerator> routines = new();
    private static bool started;
    private static double deadline;
    static ClimateValidation()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (SessionState.GetBool(Active, false)) File.AppendAllText("Logs/ClimateValidation/progress.txt", "Play mode: " + state + "\n");
        };
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (!SessionState.GetBool(Active, false) || type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            File.AppendAllText("Logs/ClimateValidation/errors.txt", message + "\n" + stack + "\n");
            SessionState.SetBool("ClimateValidationFailed", true);
        };
    }
    public static void Run()
    {
        Directory.CreateDirectory("Logs/ClimateValidation");
        File.WriteAllText("Logs/ClimateValidation/progress.txt", "Begin " + DateTime.Now + "\n");
        File.WriteAllText("Logs/ClimateValidation/errors.txt", "");
        ClimateAssetBuilder.Run();
        SessionState.SetBool("ClimateValidationFailed", false);
        SessionState.SetBool("ClimateValidationDone", false);
        SessionState.SetBool(Active, true);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        EditorApplication.isPlaying = true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Isolate()
    {
        if (!SessionState.GetBool(Active, false)) return;
        SaveSystem.StorageRootOverride = Path.Combine(Application.temporaryCachePath, "ClimateValidation-" + Guid.NewGuid().ToString("N"));
        GameSettings.StorageRootOverride = SaveSystem.StorageRootOverride;
    }
    private static void Tick()
    {
        if (SessionState.GetBool("ClimateValidationDone", false) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetBool("ClimateValidationDone", false);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool("ClimateValidationFailed", false) ? 1 : 0);
            return;
        }
        if (!SessionState.GetBool(Active, false) || EditorApplication.isCompiling) return;
        if (started && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.AppendAllText("Logs/ClimateValidation/errors.txt", "Play mode stopped before validation completed\n");
            SessionState.SetBool("ClimateValidationFailed", true); Finish(); return;
        }
        if (!EditorApplication.isPlaying) return;
        try
        {
            if (!started)
            {
                if (!SaveSystem.Ready) return;
                started = true; deadline = EditorApplication.timeSinceStartup + 240;
                routines.Push(Checks());
            }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Climate validation timed out");
            while (routines.Count > 0)
            {
                var current = routines.Peek();
                if (!current.MoveNext()) { routines.Pop(); continue; }
                if (current.Current is IEnumerator nested) { routines.Push(nested); continue; }
                return;
            }
            Finish();
        }
        catch (Exception e)
        {
            File.AppendAllText("Logs/ClimateValidation/errors.txt", e + "\n");
            SessionState.SetBool("ClimateValidationFailed", true); Finish();
        }
    }
    private static void Finish()
    {
        File.WriteAllLines("Logs/ClimateValidation/tests.txt", checks);
        Debug.Log("CLIMATE_VALIDATION " + checks.Count + " checks; failed=" + SessionState.GetBool("ClimateValidationFailed", false));
        SessionState.SetBool(Active, false); SessionState.SetBool("ClimateValidationDone", true);
        EditorApplication.isPlaying = false;
    }
    private static void Check(bool result, string name)
    {
        if (!result) throw new Exception("FAIL " + name);
        checks.Add("PASS " + name);
        File.AppendAllText("Logs/ClimateValidation/progress.txt", "PASS " + name + "\n");
    }
    private static void Near(float actual, float expected, string name) => Check(Mathf.Abs(actual - expected) < .002f, name + $" ({actual:0.###})");
    private static Card Add(string id, Bag bag = null)
    {
        var card = CardFactory.CreateCard(id); GameManager.Instance.AddCard(card, bag ?? GameManager.Instance.CurEnvironmentBag); return card;
    }
    private static T Component<T>(Card card) where T : CardComponent { card.TryGetComponent<T>(out var c); return c; }
    private static void Temperature(EnvironmentBag env, float value)
    { var state = env.StateDict[EnvironmentStateEnum.RoomTemperature]; env.ChangeEnvironmentState(EnvironmentStateEnum.RoomTemperature, value - state.CurValue); }
    private static IEnumerator Idle() { while (TimeManager.Instance.IsAdvancing) yield return null; yield return null; }
    private static IEnumerator Checks()
    {
        yield return new WaitForSecondsRealtime(1);
        var game = GameManager.Instance; var climate = ClimateManager.Instance;
        var cabin = game.EnvironmentBags[PlaceEnum.PowerCabin]; var water = game.EnvironmentBags[PlaceEnum.CoralCoast];
        var body = StateManager.Instance.PlayerStateDict[PlayerStateEnum.BodyTemperature];
        Near(body.CurValue, 36.5f, "new world Celsius body");
        Near(ClimateRules.EnvironmentDelta(20, 0, 0, 15, 2), -1.04f, "L2 heat exchange");
        Near(ClimateRules.EnvironmentDelta(0, 0, 18, 15, 0), 1.2f, "heat divided by area");
        Near(ClimateRules.EnvironmentDelta(200, -100, 0, 15, 0), -4, "environment step capped");
        Near(ClimateRules.DayCorrection(6), -4, "diurnal interpolation");
        Near(ClimateRules.SeasonCorrection(7, -8), -26, "glacier severe weather");
        Near(ClimateRules.SeasonCorrection(24, -8), 0, "glacier recovery end");
        Near(ClimateRules.BodyDelta(36.5f, -100, true, true), -1.35f, "cold water and resting cap");
        Near(ClimateRules.BodyDelta(35, 20, false, false), .15f, "comfort recovery");
        Near(ClimateRules.BodyDelta(36, 100, true, false), .81f, "heat water cap");
        Near(ClimateRules.NaturalIceDelta(0, 10), 0, "zero Celsius neutral ice");
        Near(ClimateRules.NaturalIceDelta(.1f, 10), -1, "positive Celsius minimum melting");
        Near(ClimateRules.NaturalIceDelta(-.1f, 10), 10, "negative Celsius freezing");
        foreach (var point in new[] { (31f, 0), (32f, 1), (35f, 2), (35.5f, 3), (37.5f, 3), (37.501f, 4), (38.5f, 5), (40f, 6) })
        { body.AddValue(point.Item1 - body.CurValue); Check(body.StateLevel == point.Item2, "body threshold " + point.Item1); }
        body.AddValue(36.5f - body.CurValue);
        Check(climate.Data.connections.Count == 8 && climate.Data.connections.Count(c => c.IsWater) == 6, "eight undirected links, six water links");
        foreach (var place in new[] { PlaceEnum.PowerCabin, PlaceEnum.Cockpit, PlaceEnum.LifeSupportCabin })
        {
            var env = game.EnvironmentBags[place]; var cable = climate.Modifications(env).cable.Installed;
            Check(cable != null && Component<DurabilityComponent>(cable).value >= 200 && Component<DurabilityComponent>(cable).value <= 600 && env.HasCable, "initial cabin cable " + place);
            Check(climate.InsulationLevel(env) == 2, "initial cabin insulation " + place);
        }
        foreach (var id in new[] { "融冰浮标", "牵引绳", "防水电缆", "隔热棉", "保温服", "暖暖贴", "盐", "暖绒" })
        {
            var card = CardFactory.CreateCard(id);
            Check(card != null && card.CardImage == CardFactory.GetCardImage("废金属"), "table type and placeholder " + id);
        }
        var mods = climate.Modifications(cabin); var cotton = Add("隔热棉");
        Check(mods.insulation.Install(cotton) && !cotton.Moveable && climate.InsulationLevel(cabin) == 3, "cotton adds one insulation");
        Check(!mods.insulation.CanAddCard(CardFactory.CreateCard("隔热棉"), out _), "cotton cannot stack");
        var details = (DetailsWindow)WindowsManager.Instance.OpenWindow("Details");
        details.DisplayEnvironment(cabin); yield return new WaitForSecondsRealtime(.3f);
        var view = details.GetComponentInChildren<ModificationDetailsView>(true);
        Check(view.gameObject.activeInHierarchy && !view.rope.gameObject.activeSelf && view.cable.gameObject.activeInHierarchy && view.cotton.gameObject.activeInHierarchy, "dry indoor eligible rows only, first-open visible");
        Capture("cabin-modifications", details.GetComponentInParent<Canvas>().rootCanvas);
        view.cotton.actionOne.onClick.Invoke(); yield return Idle();
        Check(mods.insulation.Installed == null && !cotton.Destroyed && cotton.Moveable && climate.InsulationLevel(cabin) == 2, "cotton removal returns original card");
        var suit = Add("保温服", game.EquipmentBag); var suitD = Component<DurabilityComponent>(suit);
        float before = body.CurValue; StateManager.Instance.ChangePlayerState(PlayerStateEnum.BodyTemperature, -1);
        Near(body.CurValue, before - .15f, "suit routes direct body cold"); Near(suitD.value, 1431.5f, "suit consumes 10 durability per blocked degree");
        suitD.SetValue(1); Near(ThermalSuit.Protect(-1), -.9f, "suit insufficient durability overflow"); Check(suit.Destroyed, "suit depleted destroyed");
        body.AddValue(37.2f - body.CurValue); var patch = Add("暖暖贴"); patch.Events[0].Inovke(); yield return Idle();
        Near(body.CurValue, 37.5f, "warming patch caps at 37.5"); Check(patch.Destroyed, "warming patch consumed"); body.AddValue(36.5f - body.CurValue);
        var recipe = Resources.Load<ScriptableRecipe>("ScriptableObject/Craft/Recipes/暖暖贴");
        Add("韧性胶管"); Add("燃素"); List<Card> crafted = null;
        CraftManager.Instance.Craft(recipe, cards => { crafted = cards; foreach (var card in cards) game.AddCard(card, cabin); }, null); yield return Idle();
        Check(crafted?.Count == 3 && crafted.All(c => c.CardId == "暖暖贴"), "recipe produces three patches once");
        foreach (var pair in new[] { ("牵引绳", "水下运输"), ("防水电缆", "电学常识"), ("融冰浮标", "基建"), ("保温服", "极寒应对措施"), ("隔热棉", "基建"), ("暖暖贴", "极寒应对措施") })
        {
            var r = Resources.Load<ScriptableRecipe>("ScriptableObject/Craft/Recipes/" + pair.Item1);
            Check(Resources.LoadAll<ScriptableTechnologyNode>("ScriptableObject/Technology").Single(t => t.name == pair.Item2).recipes.Contains(r), "recipe technology " + pair.Item1);
        }
        var connection = climate.Connection(PlaceEnum.CoralCoast, PlaceEnum.Cockpit);
        Check(ReferenceEquals(connection, climate.Connection(PlaceEnum.Cockpit, PlaceEnum.CoralCoast)), "reverse lookup identical connection");
        game.ChangeEnv(PlaceEnum.CoralCoast, false); yield return null;
        var forward = (PassageCard)Add("从珊瑚礁海域到驾驶室", water);
        var backward = (PassageCard)Add("从驾驶室到珊瑚礁海域", game.EnvironmentBags[PlaceEnum.Cockpit]);
        Player.Instance.MoveTo(Component<CoordinateComponent>(forward).Position);
        var buoy = Add("融冰浮标"); var buoyD = Component<DurabilityComponent>(buoy);
        Check(forward.InstallBuoy(buoy, out _) && ReferenceEquals(backward.Connection.buoy.Installed, buoy), "both passage cards share original buoy");
        Check(!forward.CanInstallBuoy(CardFactory.CreateCard("融冰浮标"), out _) && !buoy.Moveable, "shared slot rejects second buoy");
        Temperature(water, 0); connection.ice = 0; connection.Tick(); Near(buoyD.value, 863.8f, "deployed idle buoy wears once");
        connection.ice = 150; connection.Tick(); Near(connection.ice, 135, "buoy removes 15 ice"); Near(buoyD.value, 862.8f, "melting buoy wears once not per direction");
        var enter = forward.Events.First(e => e.Name == "通过"); Check(!enter.Judge() && enter.Hint.Contains("冰封"), "frozen passage blocks entry");
        connection.ChangeIce(-36); Check(enter.Judge(), "below 100 reopens entry");
        var salt = Add("盐"); var time = TimeManager.Instance.CurTime; buoyD.SetValue(700);
        ClimateInteractions.Repair(buoy, salt, 150, 0); Near(buoyD.value, 850, "salt adds 150 buoy durability"); Check(salt.Destroyed && time == TimeManager.Instance.CurTime, "refill consumes one with no time");
        var fur = Add("暖绒"); ClimateInteractions.Repair(buoy, fur, 200, 0); Near(buoyD.value, 864, "refill clamped"); Check(!ClimateInteractions.CanRepair(buoy, "盐", out _), "full refill disabled");
        Check(!PassageCard.IsDeicingTool(CardFactory.CreateCard("自热烹饪袋")) && !PassageCard.IsDeicingTool(CardFactory.CreateCard("凝胶装瓶器")), "excluded tools do not deice");
        var hammer = Add("钢锤"); Check(PassageCard.IsDeicingTool(hammer), "steel hammer deices");
        connection.ice = 180; details.Display(forward); details.DisplayModifications(); yield return new WaitForSecondsRealtime(.3f);
        Check(view.buoy.gameObject.activeSelf && !view.cable.gameObject.activeSelf, "passage shows shared buoy row only");
        Capture("passage-modifications", details.GetComponentInParent<Canvas>().rootCanvas);
        float originalWidth = details.RectTransform.sizeDelta.x;
        foreach (int rowWidth in new[] { 320, 375, 430 })
        {
            float difference = rowWidth - ((RectTransform)view.buoy.transform).rect.width;
            details.RectTransform.sizeDelta += new Vector2(difference, 0); Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases();
            var rowRect = (RectTransform)view.buoy.transform;
            var action = (RectTransform)view.buoy.actionTwo.transform;
            var bodyRect = view.buoy.description.rectTransform;
            Check(action.anchoredPosition.x + action.rect.width <= rowRect.rect.width - 7, "modification controls fit row width " + rowWidth);
            Check(rowRect.rect.height + bodyRect.offsetMin.y >= action.anchoredPosition.y + action.rect.height + 7, "modification text reserves action area " + rowWidth);
            var footer = (RectTransform)new SerializedObject(details).FindProperty("buttonLayout").objectReferenceValue;
            var viewport = (RectTransform)footer.parent;
            var corners = new Vector3[4];
            bool contained = true;
            foreach (var control in footer.GetComponentsInChildren<HoverableButton>())
            {
                control.rectTransform.GetWorldCorners(corners);
                contained &= corners.All(p => { var point = viewport.InverseTransformPoint(p); return point.x >= viewport.rect.xMin - 1 && point.x <= viewport.rect.xMax + 1; });
            }
            Check(contained, "all four deicing actions fit viewport at row width " + rowWidth);
            if (rowWidth == 320) Capture("passage-narrow-row", details.GetComponentInParent<Canvas>().rootCanvas);
        }
        details.RectTransform.sizeDelta = new Vector2(originalWidth, details.RectTransform.sizeDelta.y); Canvas.ForceUpdateCanvases(); yield return null;
        var hammerDurability = Component<DurabilityComponent>(hammer).value;
        forward.QuickIneract(hammer.SlotCards, 1); yield return Idle();
        Near(Component<DurabilityComponent>(hammer).value, hammerDurability - 1, "strong deicing wears one durability");
        Check(connection.ice <= 130 && connection.ice >= 100, "strong deicing subtracts fifty after time");
        var rope = Add("牵引绳"); var ropeD = Component<DurabilityComponent>(rope); var waterMods = climate.Modifications(water);
        float target = Mathf.Min(water.PlaceData.maxCoord, Player.Instance.Coordinate.Position + 10);
        int oldTime = MoveExploreManager.Instance.GetMoveEffects(target).time;
        Check(waterMods.towRope.Install(rope), "water rope installs");
        Check(MoveExploreManager.Instance.GetMoveEffects(target).time == Mathf.CeilToInt(oldTime / 1.5f), "rope local move speed bonus");
        var savedRopeD = ropeD.value; climate.Tick(); Near(ropeD.value, savedRopeD, "rope has no idle wear");
        details.DisplayEnvironment(water); yield return new WaitForSecondsRealtime(.3f);
        Check(view.rope.gameObject.activeSelf && !view.cotton.gameObject.activeSelf, "water outdoor eligible rows only");
        Check(!view.rope.actionOne.gameObject.activeSelf && !view.rope.actionTwo.gameObject.activeSelf, "rope modification has no salt refill button");
        Capture("water-modifications", details.GetComponentInParent<Canvas>().rootCanvas);
        Capture("water-modifications-1280", details.GetComponentInParent<Canvas>().rootCanvas, 1280, 720);
        var droppedCable = Add("防水电缆", water);
        Check(view.cable.HandleDrop(droppedCable.SlotCards, 1, out _) && ReferenceEquals(waterMods.cable.Installed, droppedCable), "actual modification slot accepts dragged original cable");
        ropeD.SetValue(200); var ropeSalt = Add("盐", water);
        Check(!view.rope.HandleDrop(ropeSalt.SlotCards, 1, out _) && !ropeSalt.Destroyed, "rope slot rejects salt without consuming material");
        Check(!rope.CanQuickInteract(ropeSalt, out _) && !rope.Events.Any(e => e.Name.StartsWith("填充")), "rope card has no salt quick action or refill event");
        Near(ropeD.value, 200, "salt does not restore rope durability"); savedRopeD = ropeD.value;
        MoveExploreManager.Instance.Move(target); yield return Idle();
        Near(ropeD.value, savedRopeD - Mathf.CeilToInt(oldTime / 5f), "rope charges pre-speed time wear once per trip");
        Near(Player.Instance.Coordinate.Position, target, "rope movement reaches exact target");
        var payload = GameDataManager.Instance.CaptureSnapshot();
        var restored = (ClimateData)JsonManager.Deserialize(payload.files["ClimateData"], typeof(ClimateData));
        var savedConnection = restored.connections.Single(c => c.id == connection.id);
        Check(savedConnection.buoy.Slots[0].PeekCard().Uuid == buoy.Uuid && savedConnection.ice == connection.ice, "save preserves buoy UUID and shared ice");
        Check(restored.places[PlaceEnum.CoralCoast].towRope.Slots[0].PeekCard().Uuid == rope.Uuid, "save preserves place attachment UUID");
        var fresh = JsonConvert.DeserializeObject<RunData>("{\"name\":\"old\"}"); Check(!fresh.IsCompatible, "missing schema rejects old manifest");
        var oldPayload = JsonConvert.DeserializeObject<SavePayload>("{\"version\":1,\"files\":{}}");
        bool rejected = false; try { SaveDataContract.Validate(oldPayload); } catch (InvalidDataException e) { rejected = e.Message == SaveDataContract.IncompatibleMessage; }
        Check(rejected, "old payload rejected before gameplay deserialization");
        SaveSystem.Repository.Update(fresh); Check(SaveSystem.Repository.List().Any(r => r.id == fresh.id), "incompatible worldline remains listed");
        var hub = SaveHubUI.Instance; hub.ShowRuns(); yield return new WaitForSecondsRealtime(.3f);
        hub.worldlines.Bind(SaveSystem.Repository.List(), fresh.id, () => { }, () => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
        yield return new WaitForSecondsRealtime(.2f);
        Check(hub.worldlines.time.text == SaveDataContract.IncompatibleMessage && !hub.worldlines.load.Interactable && !hub.worldlines.copy.Interactable && hub.worldlines.delete.Interactable, "old worldline UI blocks loading not deletion");
        Capture("incompatible-worldline", hub.GetComponent<Canvas>()); hub.Close();
        SaveSystem.Repository.DeleteRun(fresh); Check(!SaveSystem.Repository.List().Any(r => r.id == fresh.id), "explicit incompatible worldline deletion works");
        buoyD.SetValue(.1f); connection.Tick(); Check(buoy.Destroyed && connection.buoy.Installed == null, "depleted buoy clears shared slot");
        var appliance = Add("冰箱", cabin); var power = Component<PowerConsumptionComponent>(appliance); power.ConnectPower(); Check(power.Connected, "intact cable allows electrical device");
        var cableCard = mods.cable.Installed; Component<DurabilityComponent>(cableCard).Use(800); Check(!cabin.HasCable && mods.cable.Installed == null && !power.Connected, "broken cable disconnects actual device");
        Check(!power.CanConnectPower(out _) , "broken cable prevents reconnect");
        game.ChangeEnv(PlaceEnum.PowerCabin, false); yield return null;
        var crop = Add("水壶兰", cabin); var growth = Component<PlantGrowthComponent>(crop); growth.pressureList.Add(cabin.PressureLevel); growth.SetValue(0);
        Temperature(cabin, 12); growth.OnUpdateBegin(); growth.Update(); Near(growth.value, .6f, "crop comfort inclusive and 1.2 multiplier");
        Temperature(cabin, 5); growth.OnUpdateBegin(); growth.Update(); Near(growth.value, 1.1f, "crop growth inclusive Celsius bound");
        Temperature(cabin, -5); growth.OnUpdateBegin(); growth.Update(); Near(growth.value, 1.1f, "survival band no growth");
        growth.SetValue(100); Temperature(cabin, -11);
        for (int i = 0; i < 4; i++) { growth.OnUpdateBegin(); growth.Update(); }
        Check(crop.Destroyed, "mature crop dies at survival pressure eight");
        Temperature(cabin, 18);
        Player.Instance.MoveTo(5);
        var fire = Add("野炊营火", cabin); var fuel = Component<FuelStorageComponent>(fire); fuel.AddValue(100); fuel.Ignite();
        Near(Component<CoordinateComponent>(fire).Position, 5, "heat source deployed at player position");
        Near(climate.Heat(cabin).heat, 18, "active campfire heats environment"); Near(climate.Heat(cabin).local, 12, "nearby campfire warms player");
        Player.Instance.MoveTo(8); Near(climate.Heat(cabin).local, 6, "local heat falls linearly with distance");
        var oldTemperature = cabin.StateDict[EnvironmentStateEnum.RoomTemperature].CurValue;
        var expectedTemperature = oldTemperature + ClimateRules.EnvironmentDelta(oldTemperature, climate.NaturalTemperature(cabin), 18, 15, 2);
        climate.Tick(); Near(cabin.StateDict[EnvironmentStateEnum.RoomTemperature].CurValue, expectedTemperature, "real manager settles environment heat");
        fuel.Extinguish(); Near(climate.Heat(cabin).heat, 0, "extinguished source supplies no heat");
        foreach (var sea in new[] { water, game.EnvironmentBags[PlaceEnum.PhosphorTomb], game.EnvironmentBags[PlaceEnum.SpaceshipOuterHull] })
            Check(sea.PlaceData.climateMaterialId != "盐" && !sea.DisposableDropList.dropList.Any(d => d.dropConfig.Any(c => c.ContainsCard("盐"))) &&
                !sea.DeepExploreDropList.populationList.Any(p => p.cardTemplate?.CardId == "盐"), "no direct exploration salt source " + sea.PlaceName);
        var saline = Add("盐水", cabin); var salineCook = Component<CookComponent>(saline);
        Check(salineCook != null && salineCook.totalCookTime == 15 && salineCook.outcomeCardId == "盐", "table defines fifteen minute saline to salt cooking");
        var heatingBag = (SelfHeatingCookingBag)Add("自热烹饪袋", cabin);
        Check(!heatingBag.CanCook(saline, out _), "self heating bag cannot replace campfire salt production");
        Check(fire.CanQuickInteract(saline, out _), "campfire accepts dragged saline water");
        fire.QuickIneract(saline.SlotCards, 1);
        var fireContents = Component<InnerContentsComponent>(fire);
        Check(saline.Bag == fireContents.bag, "saline water goes into campfire content slot");
        TimeManager.Instance.AddTime(15); yield return Idle();
        Check(!saline.Destroyed && salineCook.leftCookTime == 15, "unlit campfire does not evaporate saline");
        fuel.Ignite();
        Check(Component<TimerComponent>(saline)?.tipText == "制盐", "campfire displays salt making timer");
        TimeManager.Instance.AddTime(15); yield return Idle();
        var producedSalt = fireContents.GetAllCards().SingleOrDefault(c => c.CardId == "盐");
        Check(saline.Destroyed && producedSalt != null && fireContents.GetAllCards().Count == 1, "burning campfire converts one saline water to exactly one salt");
        TimeManager.Instance.AddTime(30); yield return Idle();
        Check(!producedSalt.Destroyed && producedSalt.Bag == fireContents.bag && Component<TimerComponent>(producedSalt) == null, "salt remains in campfire without burning into another card");
        fuel.Extinguish();
        // Simulate the withdrawn trial loot source and verify the next load removes only that source.
        water.DisposableDropList.dropList.Add(new Drop(10, "盐", 3)); water.DisposableDropList.maxCount++;
        water.DeepExploreDropList.populationList.Add(new Population { cardTemplate = CardFactory.CreateCard("盐"), dropNum = 2, curSize = 12, maxSize = 12 });
        string ownedSaltUuid = ropeSalt.Uuid;
        ropeSalt.RemoveComponent<CookComponent>();
        var priorSaline = Add("盐水", water); priorSaline.RemoveComponent<CookComponent>(); string priorSalineUuid = priorSaline.Uuid;
        climate.Data.glacierStartDay = 25; // Prior trial's future default, not an active season.
        var newCable = Add("防水电缆"); mods.cable.Install(newCable); Component<DurabilityComponent>(newCable).SetValue(222);
        var newBuoy = Add("融冰浮标"); connection.buoy.Install(newBuoy); Component<DurabilityComponent>(newBuoy).SetValue(456); connection.ice = 123;
        var originalData = climate.Data; string buoyUuid = newBuoy.Uuid; float savedTemperature = cabin.StateDict[EnvironmentStateEnum.RoomTemperature].CurValue;
        while (!SaveSystem.Safe) yield return null;
        Check(SaveSystem.SaveNow(SaveKind.Manual), "actual current worldline saves");
        var run = SaveSystem.Current; var savedPoint = run.snapshots.Single(p => p.id == SaveSystem.LoadedPointId);
        SaveSystem.Enter(run, savedPoint);
        while (!SaveSystem.Ready || ReferenceEquals(climate.Data, originalData)) yield return null;
        cabin = game.EnvironmentBags[PlaceEnum.PowerCabin]; connection = climate.Connection(PlaceEnum.CoralCoast, PlaceEnum.Cockpit);
        water = game.EnvironmentBags[PlaceEnum.CoralCoast];
        Check(!water.DisposableDropList.dropList.Any(d => d.dropConfig.Any(c => c.ContainsCard("盐"))) && !water.DeepExploreDropList.populationList.Any(p => p.cardTemplate?.CardId == "盐"), "load removes obsolete trial salt drop sources");
        Check(water.GetAllCards().Any(c => c.Uuid == ownedSaltUuid), "load preserves already owned salt cards");
        var restoredSaline = water.GetAllCards().Single(c => c.Uuid == priorSalineUuid);
        Check(Component<CookComponent>(restoredSaline)?.totalCookTime == 15 && Component<CookComponent>(restoredSaline)?.outcomeCardId == "盐", "previously owned saline gains cooking recipe after load");
        Check(Component<CookComponent>(water.GetAllCards().Single(c => c.Uuid == ownedSaltUuid))?.totalCookTime == -1, "previously owned salt gains terminal cooking state after load");
        Check(climate.Data.glacierStartDay == 11, "load updates prior unstarted trial season schedule to day11");
        Check(connection.buoy.Installed.Uuid == buoyUuid && connection.buoy.Installed.Bag == connection.buoy, "scene reload restores one owned original buoy");
        Near(connection.ice, 123, "scene reload preserves ice"); Near(Component<DurabilityComponent>(connection.buoy.Installed).value, 456, "scene reload preserves buoy durability");
        Near(Component<DurabilityComponent>(climate.Modifications(cabin).cable.Installed).value, 222, "scene reload does not reroll initial cable");
        Near(cabin.StateDict[EnvironmentStateEnum.RoomTemperature].CurValue, savedTemperature, "scene reload preserves Celsius temperature");
        var originalRunId = SaveSystem.Current.id;
        while (SaveRuntime.Instance.TransitionActive) yield return null;
        SaveSystem.StartNewRun(new NewRunOptions { name = "Climate fresh world regression", skipGuide = true });
        while (!SaveSystem.Ready || SaveSystem.Current.id == originalRunId) yield return null;
        Near(StateManager.Instance.PlayerStateDict[PlayerStateEnum.BodyTemperature].CurValue, 36.5f, "new world unaffected by incompatible worlds");
        Check(climate.Data.connections.All(c => c.ice == 0 && c.buoy.Installed == null), "new world has no previous attachments or ice");
        while (SaveRuntime.Instance.TransitionActive) yield return null;
        var clockProperty = typeof(TimeManager).GetProperty("CurTime"); var originalTime = TimeManager.Instance.CurTime;
        clockProperty.SetValue(TimeManager.Instance, originalTime.AddDays(9));
        Check(climate.GlacierDay == 0 && climate.SeasonName == "温和季", "day10 is before first ice season");
        clockProperty.SetValue(TimeManager.Instance, originalTime.AddDays(10));
        Check(climate.GlacierDay == 1 && climate.SeasonName == "冰层季·初临期", "first ice season begins on day11");
        yield return new WaitForSecondsRealtime(.2f);
        var seasonLabel = Object.FindObjectOfType<ClimateSeasonLabel>();
        Check(seasonLabel != null && seasonLabel.GetComponent<Text>().text == "冰层季 第1天", "calendar UI uses ice season name");
        clockProperty.SetValue(TimeManager.Instance, originalTime.AddDays(16));
        Check(climate.GlacierDay == 7 && climate.SeasonName == "冰层季·极寒期", "actual calendar reaches extreme phase on day17");
        float weatherOffset = climate.SeasonOffset; Near(climate.SeasonOffset, weatherOffset, "same-day weather does not reroll");
        var weatherSave = (ClimateData)JsonManager.Deserialize(JsonManager.Serialize(climate.Data), typeof(ClimateData));
        Near(weatherSave.weatherByDay[17], climate.Data.weatherByDay[17], "daily weather survives serialization");
        clockProperty.SetValue(TimeManager.Instance, originalTime.AddDays(33)); Check(climate.GlacierDay == 24 && climate.SeasonName == "冰层季·回暖期", "day34 is final ice season day");
        clockProperty.SetValue(TimeManager.Instance, originalTime.AddDays(34)); Check(climate.GlacierDay == 0, "actual calendar ends ice season after day34");
        clockProperty.SetValue(TimeManager.Instance, originalTime);
        var environmentWindow = (EnvironmentBagWindow)WindowsManager.Instance.OpenWindow("EnvironmentBag");
        var stateWindow = WindowsManager.Instance.OpenWindow("State"); yield return new WaitForSecondsRealtime(.4f);
        var temperatures = new[] { environmentWindow.continuousValueStates[EnvironmentStateEnum.RoomTemperature], ((StateWindow)stateWindow).stateSliders[PlayerStateEnum.BodyTemperature] };
        Capture("temperature-windows", environmentWindow.GetComponentInParent<Canvas>().rootCanvas);
        foreach (var buttonName in new[] { "ExploreButton", "ModificationsButton" })
        {
            var label = environmentWindow.GetComponentsInChildren<HoverableButton>().Single(b => b.name == buttonName).text;
            Check(label.rectTransform.rect.width >= label.preferredWidth && label.cachedTextGenerator.vertexCount > 4,
                $"visible environment button label {buttonName}: rect={label.rectTransform.rect.size}, textWidth={label.preferredWidth}, vertices={label.cachedTextGenerator.vertexCount}");
        }
        var discoveryDisplay = typeof(EnvironmentBagWindow).GetMethod("DisplayDiscoveryDegree", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var exploreLabel = environmentWindow.GetComponentsInChildren<HoverableButton>().Single(b => b.name == "ExploreButton").text;
        foreach (bool complete in new[] { false, true })
        {
            discoveryDisplay.Invoke(environmentWindow, new object[] { 1f, complete, false });
            Check(exploreLabel.preferredWidth <= exploreLabel.rectTransform.rect.width, "deep exploration and completed labels fit: " + exploreLabel.text);
        }
        discoveryDisplay.Invoke(environmentWindow, new object[] { 0f, false, false });
        Check(temperatures.Length == 2 && temperatures.All(t => t.valueText.text.EndsWith("℃")), "actual state and environment UI render Celsius: " + string.Join(" / ", temperatures.Select(t => t.name + "=" + t.valueText.text)));
        var placeButton = environmentWindow.GetComponentsInChildren<HoverableButton>().Single(b => b.name == "ModificationsButton"); placeButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.3f);
        Check(Object.FindObjectOfType<ModificationDetailsView>() != null, "actual environment button opens modification page");
        Check(!SessionState.GetBool("ClimateValidationFailed", false), "no Unity Error Exception Assert during gameplay");
    }
    private static void Capture(string name, Canvas canvas, int width = 1920, int height = 1080)
    {
        var cameraObject = new GameObject("Climate verification camera"); var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .04f); camera.cullingMask = 1 << 31;
        var texture = new RenderTexture(width, height, 24); camera.targetTexture = texture;
        var transforms = canvas.GetComponentsInChildren<Transform>(true); var layers = transforms.Select(t => t.gameObject.layer).ToArray();
        foreach (var t in transforms) t.gameObject.layer = 31;
        var scaler = canvas.GetComponent<CanvasScaler>(); var mode = scaler.uiScaleMode; var factor = scaler.scaleFactor;
        var renderMode = canvas.renderMode; var oldCamera = canvas.worldCamera; var distance = canvas.planeDistance;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = Mathf.Min(width / 1920f, height / 1080f);
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases(); camera.Render(); var active = RenderTexture.active; RenderTexture.active = texture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/ClimateValidation/" + name + ".png", image.EncodeToPNG());
        canvas.renderMode = renderMode; canvas.worldCamera = oldCamera; canvas.planeDistance = distance; scaler.uiScaleMode = mode; scaler.scaleFactor = factor;
        for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
        RenderTexture.active = active; camera.targetTexture = null;
        Object.DestroyImmediate(image); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraObject);
    }
}
