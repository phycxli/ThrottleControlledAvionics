using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AT_Utils;
using AT_Utils.UI;
using KSP.Localization;
using ThrottleControlledAvionics;
using UnityEngine;
using UnityEngine.UI;

[KSPAddon(KSPAddon.Startup.MainMenu, true)]
public class LocalizationRuntime : MonoBehaviour
{
#if TCA_BASELINE
    const string TestPrefix = "baseline-";
#else
    const string TestPrefix = "";
#endif
    string output;
    string craftFile;
    Action nextLayout;

    void OnGUI()
    {
        if (Event.current.type != EventType.Layout || nextLayout == null) return;
        var action = nextLayout;
        nextLayout = null;
        action();
    }

    IEnumerator OnLayout(Action action)
    {
        nextLayout = action;
        while (nextLayout != null) yield return null;
    }

    void Record(string text)
    {
        Debug.Log("[TCA Localization Test] " + text);
        File.AppendAllText(Path.Combine(output, TestPrefix + "runtime-check.log"), text + "\n");
    }

    void Require(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
    }

    IEnumerator Start()
    {
        if (!Environment.GetCommandLineArgs().Contains("-tca-localization-test")
            && !Directory.Exists(Path.Combine(KSPUtil.ApplicationRootPath, "saves", "TCALocalizationTest"))) yield break;
        DontDestroyOnLoad(gameObject);
        output = Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath, ".."));
        File.WriteAllText(Path.Combine(output, TestPrefix + "runtime-check.log"), "");
        yield return new WaitForSeconds(2);
        try
        {
#if !TCA_BASELINE
            CheckLoadedText();
#else
            Record("PASS official v3.8.0.1 baseline loaded.");
#endif
            PrepareCraft();
        }
        catch (Exception ex) { Record("FAIL " + ex); Application.Quit(); yield break; }
        HighLogic.SaveFolder = "TCALocalizationTest";
        HighLogic.CurrentGame = GamePersistence.LoadGame("persistent", HighLogic.SaveFolder, true, false);
        HighLogic.CurrentGame.startScene = GameScenes.SPACECENTER;
        HighLogic.CurrentGame.Start();
        while (HighLogic.LoadedScene != GameScenes.SPACECENTER) yield return null;
        yield return new WaitForSeconds(4);
        FlightDriver.StartWithNewLaunch(craftFile, "Squad/Flags/default", "LaunchPad", new VesselCrewManifest());
        float deadline = Time.realtimeSinceStartup + 60;
        while (!HighLogic.LoadedSceneIsFlight || !FlightGlobals.ready || FlightGlobals.ActiveVessel == null
            || FlightGlobals.ActiveVessel.packed)
        {
            if (Time.realtimeSinceStartup > deadline) { Record("FAIL Flight readiness timeout."); Application.Quit(); yield break; }
            yield return null;
        }
        yield return new WaitForSeconds(3);
        TCAManual.ShowInstance(false);
        var vessel = FlightGlobals.ActiveVessel;
        var tca = vessel.parts.SelectMany(p => p.Modules.OfType<ModuleTCA>()).First();
        if (!tca.CFG.Enabled) tca.ToggleTCA();
        yield return OnLayout(() => TCAGui.ShowInstance(true));
        tca.CFG.BlockThrottle = false;
        tca.CFG.DisableVSC();
        tca.CFG.StabilizeFlight = false;
        tca.CFG.VTOLAssistON = false;
        tca.CFG.AT.XOn(Attitude.HoldAttitude);
        var engines = vessel.parts.SelectMany(p => p.Modules.OfType<ModuleEngines>()).ToArray();
        foreach (var engine in engines) engine.Activate();
        double fuelBefore = vessel.parts.Sum(p => p.Resources.Cast<PartResource>().Where(r => r.resourceName == "LiquidFuel").Sum(r => r.amount));
        float until = Time.time + 5;
        while (Time.time < until)
        {
            FlightInputHandler.state.mainThrottle = 1;
            vessel.ctrlState.mainThrottle = 1;
            yield return new WaitForFixedUpdate();
        }
        try
        {
            double fuelAfter = vessel.parts.Sum(p => p.Resources.Cast<PartResource>().Where(r => r.resourceName == "LiquidFuel").Sum(r => r.amount));
            Require(engines.All(e => e.finalThrust > 0) && fuelAfter < fuelBefore, "Engine ignition and fuel consumption.");
            var wrappers = tca.VSL.Engines.All.ToArray();
            var fullTorque = wrappers.Aggregate(Vector3.zero, (sum, e) => sum + e.specificTorque * e.nominalFullThrust);
            var balancedTorque = wrappers.Aggregate(Vector3.zero, (sum, e) => sum + e.specificTorque * e.finalThrust);
            Record("PASS engine ignition; fuel used=" + (fuelBefore - fuelAfter)
                + "; limits=" + string.Join(",", engines.Select(e => e.thrustPercentage.ToString("F2")))
                + "; full torque=" + fullTorque.magnitude + "; balanced torque=" + balancedTorque.magnitude);
            Require(balancedTorque.magnitude < fullTorque.magnitude * 0.2f, "Asymmetric engine balancing reduced torque.");
            Record("PASS asymmetric engine torque reduced by more than 80 percent.");
#if !TCA_BASELINE
            CheckHud();
#endif
            ArrangeHud();
        }
        catch (Exception ex) { Record("FAIL " + ex); }
        FlightInputHandler.state.mainThrottle = 0;
        vessel.ctrlState.mainThrottle = 0;
        yield return OnLayout(() => TCAGui.ShowInstance(true));
        yield return new WaitForSeconds(1);
        var gui = TCAGui.Instance;
        gui.Collapsed = false;
        gui.ShowOnHover = false;
        gui.ModulesGraph.Show(false);
        TCAGui.ClearStatus();
        var tabs = new ControlTab[] { gui.ENG, gui.ADV, gui.NAV, gui.ORB, gui.MCR };
        var names = new[] { "engines", "advanced", "navigation", "orbital", "macros" };
        for (int i = 0; i < tabs.Length; i++)
        {
            var selectedTab = tabs[i];
            yield return OnLayout(() => gui.ActiveTab = selectedTab);
            yield return new WaitForSeconds(1);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, TestPrefix + "ui-" + names[i] + ".png"));
            yield return new WaitForSeconds(0.25f);
        }
        yield return OnLayout(() => TCAManual.ShowInstance(true));
        yield return new WaitForSeconds(1);
        ScreenCapture.CaptureScreenshot(Path.Combine(output, TestPrefix + "ui-manual.png"));
        yield return new WaitForSeconds(0.25f);
#if TCA_BASELINE
        Record("PASS captured five baseline flight tabs and the original manual.");
#else
        Record("PASS captured five flight tabs and the Chinese manual.");
#endif
        Record("READY for interactive UI inspection.");
    }

#if !TCA_BASELINE
    void CheckLoadedText()
    {
        Require(Localizer.CurrentLanguage == "zh-cn", "Game language must be zh-cn.");
        int count = 0;
        foreach (string file in Directory.GetFiles(Path.Combine(KSPUtil.ApplicationRootPath,
            "GameData", "ThrottleControlledAvionics", "Localization"), "*.cfg"))
        {
            var node = ConfigNode.Load(file).GetNode("Localization");
            var chinese = node.GetNode("zh-cn");
            if (chinese == null) continue;
            foreach (ConfigNode.Value value in chinese.values)
            {
                string actual;
                Require(Localizer.TryGetStringByTag(value.name, out actual) && !actual.StartsWith("#LOC"), "Unresolved " + value.name);
                var sample = Loc.F(value.name.Substring(Loc.TagPrefix.Length), value.value, Enumerable.Repeat<object>(12.5, 10).ToArray());
                Require(!Regex.IsMatch(sample, @"\{\d+(?::[^}]+)?\}|<<\d+>>"), "Unexpanded placeholders " + value.name);
                count++;
            }
        }
        foreach (var name in new[] { "TCAModuleVTOLControl", "ThrottleControlledAvionics", "TCAModuleEngineOptimizer" })
        {
            var part = PartLoader.getPartInfoByName(name);
            if (part == null && name == "TCAModuleEngineOptimizer") continue;
            Require(part != null && Regex.IsMatch(part.title, @"[\u3400-\u9fff]"), "Research title " + name);
        }
        var glb = typeof(TCAGui).GetProperty("GLB", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null, null);
        var manual = (MDSection)glb.GetType().GetField("Manual").GetValue(glb);
        Require(manual.Title.Contains("手册"), "Chinese manual loaded.");
        var profiles = new EnginesProfileDB();
        profiles.AddProfile(new List<EngineWrapper>());
        Require(profiles.Default.Name == "Default" && profiles.Default.DisplayName == "默认", "Canonical saved default profile.");
        profiles.CopyActive();
        Require(profiles.DB[1].Name == "Default (Copy)", "Canonical copied profile name.");
        var saved = new ConfigNode("ENGPROFILES");
        profiles.Save(saved);
        var restored = new EnginesProfileDB();
        restored.Load(saved);
        Require(restored.Default.Name == "Default" && restored.DB[1].Name == "Default (Copy)", "Profile save and reload.");
        var named = Loc.F("WaypointNotEditable", "<<1>> cannot be edited.", "测试飞船{1}");
        TCAGui.Status(named);
        Require(TCAGui.StatusMessage == named, "Localized messages preserve braces in vessel names.");
        Require(TCAEngineInfo.RoleLabel(TCARole.MAIN).Contains("推力"), "Localized engine role.");
        Record("PASS " + count + " Chinese localization entries resolve and format in KSP; Chinese manual, research names and canonical profile verified.");
    }

    void CheckHud()
    {
        var roots = Resources.FindObjectsOfTypeAll<MonoBehaviour>().Where(c => c != null
            && c.GetType().Namespace == "TCA.UI" && c is ScreenBoundRect).ToArray();
        int chinese = 0;
        foreach (var root in roots)
        {
            if (!root.gameObject.scene.IsValid()) continue;
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (Regex.IsMatch(text.text ?? "", @"[A-Za-z]{2,}") && !Regex.IsMatch(text.text, @"[\u3400-\u9fff]"))
                    Record("HUD TEXT " + root.GetType().Name + "/" + text.name + ": " + text.text + "; active=" + text.gameObject.activeInHierarchy);
                if (!Regex.IsMatch(text.text ?? "", @"[\u3400-\u9fff]")) continue;
                Require(text.font != null && text.font.HasCharacter('推'), "HUD Chinese font " + text.name);
                chinese++;
            }
        }
        Require(chinese > 5, "Chinese HUD labels instantiated.");
        Record("PASS " + roots.Length + " HUD roots and " + chinese + " Chinese labels with CJK font support.");
    }
#endif

    void ArrangeHud()
    {
        foreach (var root in Resources.FindObjectsOfTypeAll<ScreenBoundRect>())
        {
            if (!root.gameObject.scene.IsValid() || root.GetType().Namespace != "TCA.UI") continue;
            Vector2 center;
            switch (root.GetType().Name)
            {
                case "VFlightUI": center = new Vector2(940, 570); break;
                case "AttitudeUI": center = new Vector2(1130, 390); break;
                case "StatusUI": center = new Vector2(900, 360); break;
                case "ManeuverUI": center = new Vector2(940, 200); break;
                default: center = new Vector2(920, 90); break;
            }
            var rect = root.GetComponent<RectTransform>();
            var canvas = root.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var currentCenter = (RectTransformUtility.WorldToScreenPoint(camera, corners[0])
                + RectTransformUtility.WorldToScreenPoint(camera, corners[2])) / 2;
            var desiredPivot = RectTransformUtility.WorldToScreenPoint(camera, rect.position) + center - currentCenter;
            Vector3 position;
            RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, desiredPivot, camera, out position);
            rect.position = position;
        }
    }

    void PrepareCraft()
    {
        var craft = new ConfigNode();
        craft.AddValue("ship", "TCA asymmetric localization test");
        craft.AddValue("version", "1.12.5"); craft.AddValue("type", "VAB");
        craft.AddValue("persistentId", 790000); craft.AddValue("rot", "0,0,0,1");
        var names = new[] { "probeCoreOcto.v2", "fuelTank", "liquidEngineMini.v2", "liquidEngineMini.v2", "launchClamp1" };
        var prefabs = names.Select(n => PartLoader.getPartInfoByName(n).partPrefab).ToArray();
        var refs = names.Select((n, i) => n + "_" + (790001 + i)).ToArray();
        var positions = new[] { new Vector3(0, 20, 0), new Vector3(0, 19.3f, 0),
            new Vector3(1, 18, 0), new Vector3(-2, 18, 0), new Vector3(0, 18, -3) };
        var nodes = new List<ConfigNode>();
        for (int i = 0; i < names.Length; i++)
        {
            var node = craft.AddNode("PART"); nodes.Add(node);
            node.AddValue("part", refs[i]); node.AddValue("partName", "Part");
            node.AddValue("persistentId", 790010 + i);
            node.AddValue("pos", KSPUtil.WriteVector(positions[i]));
            node.AddValue("attPos0", KSPUtil.WriteVector(positions[i]));
            node.AddValue("rot", "0,0,0,1"); node.AddValue("attRot0", "0,0,0,1");
            node.AddValue("attPos", "0,0,0"); node.AddValue("attRot", "0,0,0,1");
            node.AddValue("mir", "1,1,1"); node.AddValue("symMethod", "Radial");
            foreach (var field in new[] { "istg", "dstg", "attm", "modCost", "modMass" }) node.AddValue(field, 0);
            foreach (var field in new[] { "sidx", "sqor", "sepI" }) node.AddValue(field, -1);
            node.AddValue("flag", "Squad/Flags/default");
            node.AddNode("EVENTS"); node.AddNode("ACTIONS"); node.AddNode("PARTDATA");
            foreach (PartModule module in prefabs[i].Modules) module.Save(node.AddNode("MODULE"));
            foreach (PartResource resource in prefabs[i].Resources) resource.Save(node.AddNode("RESOURCE"));
        }
        nodes[0].AddValue("link", refs[1]); nodes[0].AddValue("attN", "bottom," + refs[1]);
        nodes[1].AddValue("attN", "top," + refs[0]);
        for (int i = 2; i < names.Length; i++) { nodes[1].AddValue("link", refs[i]); nodes[i].AddValue("srfN", "srfAttach," + refs[1]); }
        craftFile = Path.Combine(KSPUtil.ApplicationRootPath, "saves", "TCALocalizationTest", "Ships", "VAB", "TCA-test.craft");
        Directory.CreateDirectory(Path.GetDirectoryName(craftFile)); craft.Save(craftFile);
    }
}
