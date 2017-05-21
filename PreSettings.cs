//using System;
//using UnityEngine;

//namespace HullBreach
//{
//    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
//    public class HullBreachSettings : MonoBehaviour
//    {
//        public static string SettingsConfigUrl = "GameData/HullBreach/settings.cfg";
         
//        public static double _flowRate { get; set; }
//        public static double _critFlowRate { get; set; }
//        public static double _breachTemp { get; set; }
//        public static double _critBreachTemp { get; set; }

//        public static bool _ecDrain { get; set; }
//        public static bool _isHullBreachEnabled { get; set; }


//        void Awake()
//        {
//            LoadConfig();
//        }

//        public static void LoadConfig()
//        {
//            try
//            {
//                Debug.Log("[HullBreach]: Loading settings.cfg ");

//                ConfigNode fileNode = ConfigNode.Load(SettingsConfigUrl);
//                if (!fileNode.HasNode("HullBreachSettings")) return;

//                ConfigNode settings = fileNode.GetNode("HullBreachSettings");

//                _flowRate            = double.Parse(settings.GetValue("flowRate"));
//                _critFlowRate        = double.Parse(settings.GetValue("critFlowRate"));
//                _breachTemp          = double.Parse(settings.GetValue("breachTemp"));
//                _critBreachTemp      = double.Parse(settings.GetValue("critBreachTemp"));

//                _ecDrain             = bool.Parse(settings.GetValue("ecDrain"));
//                _isHullBreachEnabled = bool.Parse(settings.GetValue("isHullBreachEnabled"));

//            }
//            catch (Exception ex)
//            {
//                Debug.Log("[HullBreach]: Failed to load settings config:" + ex.Message);
//            }
//        }

//        public static void SaveConfig()
//        {
//            try
//            {
//                Debug.Log("[HullBreach]: Saving settings.cfg ==");
//                ConfigNode fileNode = ConfigNode.Load(SettingsConfigUrl);

//                if (!fileNode.HasNode("HullBreachSettings")) return;

//                ConfigNode settings = fileNode.GetNode("PreSettings");

//                settings.SetValue("flowRate", _flowRate);
//                settings.SetValue("critFlowRate", _critBreachTemp);
//                settings.SetValue("breachTemp", _breachTemp);
//                settings.SetValue("critBreachTemp", _critBreachTemp);
//                settings.SetValue("ecDrain", _flowRate, _ecDrain);
//                settings.SetValue("isHullBreachEnabled", _isHullBreachEnabled);
                
//                fileNode.Save(SettingsConfigUrl);
//            }
//            catch (Exception ex)
//            {
//                Debug.Log("[HullBreach]: Failed to save settings config:" + ex.Message); throw;
//            }
//        }


//    }
//}
