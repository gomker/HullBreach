//Toolbar pattern originally adapted from https://github.com/BahamutoD/VesselMover/blob/master/VesselMoverToolbar.cs

using System.Globalization;
using KSP.UI.Screens;
using UnityEngine;

namespace HullBreach
{
    /// <summary>Flight-scene status window and quick settings, on the stock application launcher.</summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class HullBreachToolBar : MonoBehaviour
    {
        const int WindowId = 302115;
        const float StatusRefreshInterval = 0.5f;

        ApplicationLauncherButton button;
        bool showWindow;
        bool hideUI;
        Rect windowRect = new Rect(Screen.width - 380, 250, 300, 10);

        // Status strings are rebuilt twice a second while the window is open, not every OnGUI call.
        string statusLine1 = string.Empty, statusLine2 = string.Empty, statusLine3 = string.Empty;
        float refreshTimer;

        void Start()
        {
            if (ApplicationLauncher.Ready) AddButton();
            else GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
            GameEvents.onHideUI.Add(OnHideUI);
            GameEvents.onShowUI.Add(OnShowUI);
        }

        void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(AddButton);
            GameEvents.onHideUI.Remove(OnHideUI);
            GameEvents.onShowUI.Remove(OnShowUI);
            if (button != null && ApplicationLauncher.Instance != null)
                ApplicationLauncher.Instance.RemoveModApplication(button);
            button = null;
        }

        static void Noop() { }
        void OnHideUI() { hideUI = true; }
        void OnShowUI() { hideUI = false; }

        void AddButton()
        {
            if (button != null || ApplicationLauncher.Instance == null) return;
            Texture tex = GameDatabase.Instance.GetTexture("HullBreach/Icon/HullBreach", false);
            button = ApplicationLauncher.Instance.AddModApplication(
                () => showWindow = true, () => showWindow = false,
                Noop, Noop, Noop, Noop,
                ApplicationLauncher.AppScenes.FLIGHT, tex);
        }

        void Update()
        {
            if (!showWindow) return;
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer > 0f) return;
            refreshTimer = StatusRefreshInterval;
            RefreshStatus();
        }

        void RefreshStatus()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            HullBreachVesselModule vm = v != null ? v.GetComponent<HullBreachVesselModule>() : null;
            if (vm == null || vm.ModuleCount == 0)
            {
                statusLine1 = "No HullBreach parts on this vessel";
                statusLine2 = statusLine3 = string.Empty;
                return;
            }
            statusLine1 = (vm.IsSubmarine ? "Submarine" : "Surface vessel") + (vm.Foundered ? " - FOUNDERING" : string.Empty)
                + "   hull parts: " + vm.HullCount.ToString(CultureInfo.InvariantCulture);
            statusLine2 = "Breached: " + vm.LeakingCount.ToString(CultureInfo.InvariantCulture)
                + "   critical: " + vm.CriticalCount.ToString(CultureInfo.InvariantCulture);
            statusLine3 = "Water aboard: " + vm.WaterTonnes.ToString("F0", CultureInfo.InvariantCulture) + " t"
                + (vm.CapacityTonnes > 0 ? " / " + vm.CapacityTonnes.ToString("F0", CultureInfo.InvariantCulture) + " t" : string.Empty)
                + "   inflow: " + vm.InflowTonnesPerSec.ToString("F1", CultureInfo.InvariantCulture) + " t/s";
        }

        void OnGUI()
        {
            if (!showWindow || hideUI) return;
            windowRect = GUILayout.Window(WindowId, windowRect, DrawWindow, "HullBreach", HighLogic.Skin.window, GUILayout.Width(300));
        }

        void DrawWindow(int id)
        {
            GUILayout.Label(statusLine1, HighLogic.Skin.label);
            if (statusLine2.Length > 0) GUILayout.Label(statusLine2, HighLogic.Skin.label);
            if (statusLine3.Length > 0) GUILayout.Label(statusLine3, HighLogic.Skin.label);

            GUILayout.Space(6);
            HullBreachSettings.Enabled = GUILayout.Toggle(HullBreachSettings.Enabled, " Flooding enabled", HighLogic.Skin.toggle);
            HullBreachSettings.EcDrain = GUILayout.Toggle(HullBreachSettings.EcDrain, " Flooded compartments lose EC", HighLogic.Skin.toggle);
            HullBreachSettings.Messages = GUILayout.Toggle(HullBreachSettings.Messages, " Screen messages", HighLogic.Skin.toggle);
            HullBreachSettings.Debug = GUILayout.Toggle(HullBreachSettings.Debug, " Debug fields / test breach", HighLogic.Skin.toggle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Flood speed x" + HullBreachSettings.GlobalFlowMultiplier.ToString("F2", CultureInfo.InvariantCulture), HighLogic.Skin.label, GUILayout.Width(120));
            HullBreachSettings.GlobalFlowMultiplier = Mathf.Round(GUILayout.HorizontalSlider(HullBreachSettings.GlobalFlowMultiplier, 0.25f, 4f) * 4f) / 4f;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save settings", HighLogic.Skin.button)) HullBreachSettings.Save();
            if (GUILayout.Button("Reload", HighLogic.Skin.button)) HullBreachSettings.Load();
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }
    }
}
