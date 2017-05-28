using System;
using KSP.UI.Screens;
using UnityEngine;

namespace HullBreach
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class config : MonoBehaviour
    {

        public static string SettingsConfigUrl = "GameData/HullBreach/settings.cfg";

        public static double flowRate { get; set; }
        public static double critFlowRate { get; set; }
        public static double breachTemp { get; set; }
        public static double critBreachTemp { get; set; }

        public static bool ecDrain { get; set; }
        public static bool isHullBreachEnabled { get; set; }

        public ModuleHullBreach vesselHullBreach = null;
        public static config Instance;

        void Awake()
        {
            LoadConfig();
            Instance = this;
        }

        public static void LoadConfig()
        {
            try
            {
                Debug.Log("[HullBreach]: Loading settings.cfg ");

                ConfigNode fileNode = ConfigNode.Load(SettingsConfigUrl);
                if (!fileNode.HasNode("HullBreachSettings")) return;

                ConfigNode settings = fileNode.GetNode("HullBreachSettings");

                flowRate = double.Parse(settings.GetValue("flowRate"));
                critFlowRate = double.Parse(settings.GetValue("critFlowRate"));
                breachTemp = double.Parse(settings.GetValue("breachTemp"));
                critBreachTemp = double.Parse(settings.GetValue("critBreachTemp"));

                ecDrain = bool.Parse(settings.GetValue("ecDrain"));
                isHullBreachEnabled = bool.Parse(settings.GetValue("isHullBreachEnabled"));

            }
            catch (Exception ex)
            {
                Debug.Log("[HullBreach]: Failed to load settings config:" + ex.Message);
            }
        }

        public static void SaveConfig()
        {
            try
            {
                Debug.Log("[HullBreach]: Saving settings.cfg ==");
                ConfigNode fileNode = ConfigNode.Load(SettingsConfigUrl);

                if (!fileNode.HasNode("HullBreachSettings")) return;

                ConfigNode settings = fileNode.GetNode("HullBreachSettings");

                settings.SetValue("flowRate", flowRate);
                settings.SetValue("critFlowRate", critBreachTemp);
                settings.SetValue("breachTemp", breachTemp);
                settings.SetValue("critBreachTemp", critBreachTemp);
                settings.SetValue("ecDrain", ecDrain);
                settings.SetValue("isHullBreachEnabled", isHullBreachEnabled);

                fileNode.Save(SettingsConfigUrl);
            }
            catch (Exception ex)
            {
                Debug.Log("[HullBreach]: Failed to save settings config:" + ex.Message); throw;
            }
        }

    }
}

