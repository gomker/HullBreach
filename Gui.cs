//using System;
//using KSP.UI.Screens;
//using UnityEngine;

//namespace HullBreach
//{
//    [KSPAddon(KSPAddon.Startup.Flight, false)]
//    public class Gui : MonoBehaviour
//    {

//        private const float WindowWidth = 250;
//        private const float DraggableHeight = 40;
//        private const float LeftIndent = 12;
//        private const float ContentTop = 20;
//        public static Gui Fetch;
//        public static bool GuiEnabled;
//        public static bool HasAddedButton;
//        private readonly float _incrButtonWidth = 26;
//        private readonly float contentWidth = WindowWidth - 2 * LeftIndent;
//        private readonly float entryHeight = 20;
//        private float _contentWidth;
//        private bool _gameUiToggle;
//        private float _windowHeight = 250;
//        private Rect _windowRect;

//        private string _gui_ecDrain = string.Empty;
//        private string _gui_isHullBreachEnabled = string.Empty;
        
//        private void Awake()
//        {
//            if (Fetch)
//                Destroy(Fetch);

//            Fetch = this;
//        }

//        private void Start()
//        {
//            _windowRect = new Rect(Screen.width - WindowWidth - 40, 100, WindowWidth, _windowHeight);
//            AddToolbarButton();
//            GameEvents.onHideUI.Add(GameUiDisable);
//            GameEvents.onShowUI.Add(GameUiEnable);
//            _gameUiToggle = true;

//            _gui_ecDrain = HullBreachSettings._ecDrain.ToString();
//            _gui_isHullBreachEnabled = HullBreachSettings._isHullBreachEnabled.ToString();
//        }

//        private void OnGUI()
//        {
//            if (GuiEnabled && _gameUiToggle)
//                _windowRect = GUI.Window(320, _windowRect, GuiWindow, "");
//        }

//        private void GuiWindow(int windowId)
//        {
//            GUI.DragWindow(new Rect(0, 0, WindowWidth, DraggableHeight));
//            float line = 0;
//            _contentWidth = WindowWidth - 2 * LeftIndent;

//            DrawTitle();
            
//            if (ModuleHullBreach.Enabled)
//            {
//                line++;
//                line++;
//                DrawHullBreachOptions(line);
//                DrawSaveButton(line);
//            }

//            _windowHeight = ContentTop + line * entryHeight + entryHeight + entryHeight;
//            _windowRect.height = _windowHeight;
//        }

//        private void DrawHullBreachOptions(float line)
//        {
//            var leftLabel = new GUIStyle();
//            leftLabel.alignment = TextAnchor.UpperLeft;
//            leftLabel.normal.textColor = Color.white;
//            float textFieldWidth = 42;

//            //GUI.Label(new Rect(LeftIndent, ContentTop + line * entryHeight, 60, entryHeight), "Global range:", leftLabel);

//            //var fwdFieldRect = new Rect(LeftIndent + contentWidth - textFieldWidth - 3 * _incrButtonWidth,
//            //    ContentTop + line * entryHeight, textFieldWidth, entryHeight);
//            //_guiGlobalRangeForVessels = GUI.TextField(fwdFieldRect, _guiGlobalRangeForVessels);

//            var saveRect = new Rect(LeftIndent, ContentTop + line * entryHeight, contentWidth / 2, entryHeight);

//        }

//        private void DrawSaveButton(float line)
//        {
//            var saveRect = new Rect(LeftIndent, ContentTop + line * entryHeight, contentWidth / 2, entryHeight);
//            if (GUI.Button(saveRect, "Apply"))
//                Apply();
//        }

//        private void Apply()
//        {
//            bool ecDrain;
//            bool isHullBreachEnabled;

//            if (bool.TryParse(_gui_ecDrain, out ecDrain))
//            {
//                HullBreachSettings._ecDrain = ecDrain;
//                _gui_ecDrain = HullBreachSettings._ecDrain.ToString();
//            }

//            if (bool.TryParse(_gui_isHullBreachEnabled, out isHullBreachEnabled))
//            {
//                HullBreachSettings._isHullBreachEnabled = isHullBreachEnabled;
//                _gui_isHullBreachEnabled = HullBreachSettings._isHullBreachEnabled.ToString();
//            }

//            HullBreachSettings.SaveConfig();            
//        }

//        private void DrawTitle()
//        {
//            var centerLabel = new GUIStyle
//            {
//                alignment = TextAnchor.UpperCenter,
//                normal = { textColor = Color.white }
//            };
//            var titleStyle = new GUIStyle(centerLabel)
//            {
//                fontSize = 10,
//                alignment = TextAnchor.MiddleCenter
//            };
//            GUI.Label(new Rect(0, 0, WindowWidth, 20), "HullBreach", titleStyle);
//        }

//        private void AddToolbarButton()
//        {
//            if (!HasAddedButton)
//            {
//                Texture buttonTexture = GameDatabase.Instance.GetTexture("HullBreach/Icon/HullBreach", false);
//                ApplicationLauncher.Instance.AddModApplication(EnableGui, DisableGui, Dummy, Dummy, Dummy, Dummy,
//                    ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
//                HasAddedButton = true;
//            }
//        }

//        private void EnableGui()
//        {
//            GuiEnabled = true;
//            Debug.Log("Showing HullBreach GUI");
//        }

//        private void DisableGui()
//        {
//            GuiEnabled = false;
//            Debug.Log("Hiding HullBreach GUI");
//        }

//        private void Dummy()
//        {
//        }

//        private void GameUiEnable()
//        {
//            _gameUiToggle = true;
//        }

//        private void GameUiDisable()
//        {
//            _gameUiToggle = false;
//        }
//    }


//}

