//Code Adapted from https://github.com/BahamutoD/VesselMover/blob/master/VesselMoverToolbar.cs

using UnityEngine;
using KSP.UI.Screens;

namespace HullBreach
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class HullBreachToolBar : MonoBehaviour
    {

        public static bool hasAddedButton = false;
        public static bool toolbarGuiEnabled = false;

        Rect toolbarRect;
        float toolbarWidth = 280;
        float toolbarHeight = 0;
        float toolbarMargin = 6;
        float toolbarLineHeight = 20;
        float contentWidth;
        Vector2 toolbarPosition;
        Rect svRectScreenSpace;

        //bool showMoveHelp = false;
        //float helpHeight;

        void VesselChange(Vessel v)
        {
            if (!v.isActiveVessel) return;
        }

        void Start()
        {
            toolbarPosition = new Vector2(Screen.width - toolbarWidth - 80, 150);
            toolbarRect = new Rect(toolbarPosition.x, toolbarPosition.y + 100, toolbarWidth, toolbarHeight);
            contentWidth = toolbarWidth - (2 * toolbarMargin);

            AddToolbarButton();

            GameEvents.onVesselChange.Add(VesselChange);
        }

        void OnGUI()
        {
            if (toolbarGuiEnabled)
            {
                GUI.Window(302115, toolbarRect, ToolbarWindow, "HullBreach", HighLogic.Skin.window);
            }
        }

        void ToolbarWindow(int windowID)
        {
            float line = 0;
            line += 1.25f;

            if (!FlightGlobals.ActiveVessel) { return; }

            if (config.Instance.vesselHullBreach != null)
            {
                if (config.ecDrain)
                {
                    if (GUI.Button(LineRect(ref line, 1.5f), "EC Drain ON", HighLogic.Skin.button))
                    {
                        //ModuleHullBreach.Instance.ToggleHullBreach();
                        config.ecDrain = false;
                        config.SaveConfig();                        
                    }
                }
                else
                {
                    if (GUI.Button(LineRect(ref line, 2), "EC Drain OFF", HighLogic.Skin.button))
                    {
                        config.ecDrain = true;
                        config.SaveConfig();
                    }
                }
                

            }
            else
            {
                GUIStyle centerLabelStyle = new GUIStyle(HighLogic.Skin.label) { alignment = TextAnchor.UpperCenter };
                GUI.Label(LineRect(ref line), "No HullBreach Module Found", centerLabelStyle);
            }

            //line += 0.2f;
            //Rect spawnVesselRect = LineRect(ref line);
            //svRectScreenSpace = new Rect(spawnVesselRect);
            //svRectScreenSpace.x += toolbarRect.x;
            //svRectScreenSpace.y += toolbarRect.y;
            toolbarRect.height = (line * toolbarLineHeight) + (toolbarMargin * 2);
        }

        Rect LineRect(ref float currentLine, float heightFactor = 1)
        {
            Rect rect = new Rect(toolbarMargin, toolbarMargin + (currentLine * toolbarLineHeight), contentWidth, toolbarLineHeight * heightFactor);
            currentLine += heightFactor + 0.1f;
            return rect;
        }


        void LineLabel(string label, ref float line)
        {
            GUI.Label(LineRect(ref line), label, HighLogic.Skin.label);
        }

        void AddToolbarButton()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (!hasAddedButton)
                {
                    Texture buttonTexture = GameDatabase.Instance.GetTexture("HullBreach/Icon/HullBreach", false);
                    ApplicationLauncher.Instance.AddModApplication(ShowToolbarGUI, HideToolbarGUI, Dummy, Dummy, Dummy, Dummy, ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
                    hasAddedButton = true;
                }
            }
        }

        public void ShowToolbarGUI()
        {
            HullBreachToolBar.toolbarGuiEnabled = true;
        }

        public void HideToolbarGUI()
        {
            HullBreachToolBar.toolbarGuiEnabled = false;
        }

        void Dummy()
        { }

        public static bool MouseIsInRect(Rect rect)
        {
            return rect.Contains(MouseGUIPos());
        }

        public static Vector2 MouseGUIPos()
        {
            return new Vector3(Input.mousePosition.x, Screen.height - Input.mousePosition.y, 0);
        }
    }
}
