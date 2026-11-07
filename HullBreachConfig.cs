using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace HullBreach
{
    /// <summary>
    /// Global tuning. Defaults live in code, are overridden by the MM-patchable
    /// GameData/HullBreach/Settings.cfg (HullBreachSettings node) and then by the
    /// user's saved overrides in GameData/HullBreach/PluginData/settings.cfg
    /// (PluginData is ignored by ModuleManager, so saving does not invalidate the MM cache).
    /// </summary>
    public static class HullBreachSettings
    {
        public const string NodeName = "HullBreachSettings";

        public static bool Enabled = true;
        /// <summary>Seconds to flood a whole compartment at 100% severity at the reference depth.</summary>
        public static float FloodTime = 60f;
        public static float BreachThreshold = 0.10f;
        public static float CriticalThreshold = 0.60f;
        public static float CriticalOverfill = 0.20f;
        /// <summary>How much armour loss counts toward breach severity (0..1).</summary>
        public static float ArmorWeight = 0.35f;
        public static float GlobalFlowMultiplier = 1f;
        /// <summary>Fraction of the inflow rate at which water drains back out when above the equilibrium level.</summary>
        public static float DrainFactor = 0.5f;
        /// <summary>
        /// Submarine trim/damage-control pumping per non-critical breached compartment, fraction of capacity
        /// per second. Surface ships do not get this: their reserve buoyancy absorbs small flooding instead.
        /// </summary>
        public static float SubmarineDamageControlRate = 0.002f;

        public static float RefDepth = 4f;
        public static float MinDepthFactor = 0.5f;
        public static float MaxDepthFactor = 2.5f;

        public static float SubmarineFlowMultiplier = 0.5f;
        public static float SubmarineMinCrushDepth = 350f;

        /// <summary>Fraction of max HP per second removed at crush depth (grows with over-depth).</summary>
        public static float CrushDamageRate = 0.1f;
        /// <summary>Parts that BDA cannot damage are exploded after this many seconds past crush depth.</summary>
        public static float CrushFailsafeTime = 20f;

        /// <summary>Vessel CoM altitude below which a flooding non-submarine counts as foundered.</summary>
        public static float FounderAltitude = -20f;
        /// <summary>Buoyancy given to 'crushable' parts of a foundered vessel (was hard-coded -1 in 1.7).</summary>
        public static float FounderBuoyancy = 0f;
        /// <summary>Kelvin per second per metre of depth for hydroExplosive parts (1.7 used 0.1 K/frame/m).</summary>
        public static float HydroHeatRate = 5f;

        /// <summary>Flooded compartments lose the ElectricCharge stored in them.</summary>
        public static bool EcDrain = true;
        public static float EcShortFraction = 0.5f;

        public static float UpdateInterval = 0.1f;
        public static bool Messages = true;
        public static bool Debug = false;

        static string UserFile => Path.Combine(KSPUtil.ApplicationRootPath, "GameData/HullBreach/PluginData/settings.cfg");

        public static void Load()
        {
            try
            {
                if (GameDatabase.Instance != null)
                {
                    ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes(NodeName);
                    for (int i = 0; i < nodes.Length; i++) Apply(nodes[i]);
                }
                if (File.Exists(UserFile))
                {
                    ConfigNode file = ConfigNode.Load(UserFile);
                    ConfigNode node = file != null ? (file.HasNode(NodeName) ? file.GetNode(NodeName) : file) : null;
                    if (node != null) Apply(node);
                }
                Sanitize();
                UnityEngine.Debug.Log("[HullBreach]: Settings loaded (floodTime=" + FloodTime.ToString(CultureInfo.InvariantCulture)
                    + " breach=" + BreachThreshold.ToString(CultureInfo.InvariantCulture)
                    + " critical=" + CriticalThreshold.ToString(CultureInfo.InvariantCulture) + ")");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[HullBreach]: Failed to load settings: " + ex);
            }
        }

        public static void Save()
        {
            try
            {
                ConfigNode root = new ConfigNode();
                ConfigNode n = root.AddNode(NodeName);
                n.AddValue("enabled", Enabled);
                n.AddValue("floodTime", F(FloodTime));
                n.AddValue("breachThreshold", F(BreachThreshold));
                n.AddValue("criticalThreshold", F(CriticalThreshold));
                n.AddValue("criticalOverfill", F(CriticalOverfill));
                n.AddValue("armorWeight", F(ArmorWeight));
                n.AddValue("globalFlowMultiplier", F(GlobalFlowMultiplier));
                n.AddValue("drainFactor", F(DrainFactor));
                n.AddValue("submarineDamageControlRate", F(SubmarineDamageControlRate));
                n.AddValue("refDepth", F(RefDepth));
                n.AddValue("minDepthFactor", F(MinDepthFactor));
                n.AddValue("maxDepthFactor", F(MaxDepthFactor));
                n.AddValue("submarineFlowMultiplier", F(SubmarineFlowMultiplier));
                n.AddValue("submarineMinCrushDepth", F(SubmarineMinCrushDepth));
                n.AddValue("crushDamageRate", F(CrushDamageRate));
                n.AddValue("crushFailsafeTime", F(CrushFailsafeTime));
                n.AddValue("founderAltitude", F(FounderAltitude));
                n.AddValue("founderBuoyancy", F(FounderBuoyancy));
                n.AddValue("hydroHeatRate", F(HydroHeatRate));
                n.AddValue("ecDrain", EcDrain);
                n.AddValue("ecShortFraction", F(EcShortFraction));
                n.AddValue("updateInterval", F(UpdateInterval));
                n.AddValue("messages", Messages);
                n.AddValue("debug", Debug);
                Directory.CreateDirectory(Path.GetDirectoryName(UserFile));
                root.Save(UserFile);
                UnityEngine.Debug.Log("[HullBreach]: Settings saved to " + UserFile);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[HullBreach]: Failed to save settings: " + ex);
            }
        }

        static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        static void Apply(ConfigNode n)
        {
            B(n, "enabled", ref Enabled);
            R(n, "floodTime", ref FloodTime);
            R(n, "breachThreshold", ref BreachThreshold);
            R(n, "criticalThreshold", ref CriticalThreshold);
            R(n, "criticalOverfill", ref CriticalOverfill);
            R(n, "armorWeight", ref ArmorWeight);
            R(n, "globalFlowMultiplier", ref GlobalFlowMultiplier);
            R(n, "drainFactor", ref DrainFactor);
            R(n, "submarineDamageControlRate", ref SubmarineDamageControlRate);
            R(n, "refDepth", ref RefDepth);
            R(n, "minDepthFactor", ref MinDepthFactor);
            R(n, "maxDepthFactor", ref MaxDepthFactor);
            R(n, "submarineFlowMultiplier", ref SubmarineFlowMultiplier);
            R(n, "submarineMinCrushDepth", ref SubmarineMinCrushDepth);
            R(n, "crushDamageRate", ref CrushDamageRate);
            R(n, "crushFailsafeTime", ref CrushFailsafeTime);
            R(n, "founderAltitude", ref FounderAltitude);
            R(n, "founderBuoyancy", ref FounderBuoyancy);
            R(n, "hydroHeatRate", ref HydroHeatRate);
            B(n, "ecDrain", ref EcDrain);
            R(n, "ecShortFraction", ref EcShortFraction);
            R(n, "updateInterval", ref UpdateInterval);
            B(n, "messages", ref Messages);
            B(n, "debug", ref Debug);
        }

        static void Sanitize()
        {
            FloodTime = Mathf.Max(1f, FloodTime);
            BreachThreshold = Mathf.Clamp(BreachThreshold, 0f, 0.95f);
            CriticalThreshold = Mathf.Clamp(CriticalThreshold, BreachThreshold + 0.01f, 1f);
            CriticalOverfill = Mathf.Clamp(CriticalOverfill, 0f, 1f);
            ArmorWeight = Mathf.Clamp01(ArmorWeight);
            GlobalFlowMultiplier = Mathf.Max(0f, GlobalFlowMultiplier);
            DrainFactor = Mathf.Clamp01(DrainFactor);
            SubmarineDamageControlRate = Mathf.Clamp(SubmarineDamageControlRate, 0f, 0.1f);
            RefDepth = Mathf.Max(0.1f, RefDepth);
            MinDepthFactor = Mathf.Max(0f, MinDepthFactor);
            MaxDepthFactor = Mathf.Max(MinDepthFactor, MaxDepthFactor);
            SubmarineFlowMultiplier = Mathf.Max(0f, SubmarineFlowMultiplier);
            CrushDamageRate = Mathf.Max(0f, CrushDamageRate);
            CrushFailsafeTime = Mathf.Max(1f, CrushFailsafeTime);
            EcShortFraction = Mathf.Clamp01(EcShortFraction);
            UpdateInterval = Mathf.Clamp(UpdateInterval, 0.02f, 1f);
        }

        static void R(ConfigNode n, string key, ref float field)
        {
            string v = n.GetValue(key);
            if (v != null && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) field = f;
        }

        static void B(ConfigNode n, string key, ref bool field)
        {
            string v = n.GetValue(key);
            if (v != null && bool.TryParse(v.Trim(), out bool b)) field = b;
        }
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class HullBreachSettingsLoader : MonoBehaviour
    {
        void Start()
        {
            HullBreachSettings.Load();
        }
    }
}
