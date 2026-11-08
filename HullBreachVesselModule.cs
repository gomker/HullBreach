using System.Collections.Generic;
using BDArmory.Control;
using UnityEngine;

namespace HullBreach
{
    /// <summary>
    /// Drives every ModuleHullBreach on one loaded vessel at a fixed interval.
    ///
    /// Cost model: the module list is rebuilt only when the vessel changes structurally
    /// (O(parts) on onVesselWasModified / onVesselPartCountChanged), and each tick is
    /// O(HullBreach parts on this vessel) with no allocations. Across a fleet the total is
    /// linear in the number of parts; nothing iterates over other vessels.
    /// </summary>
    public class HullBreachVesselModule : VesselModule
    {
        const float SubmarineRecheckInterval = 2f;
        const float BreachMessageInterval = 3f;
        const float CrushWarnInterval = 5f;

        readonly List<ModuleHullBreach> modules = new List<ModuleHullBreach>(8);
        BDModuleSurfaceAI surfaceAI;
        bool hasSubmarinePart;
        bool dirty = true;
        bool wasInWater;
        float accumulator;
        float subRecheckTimer;
        float breachMsgTimer;
        float crushWarnTimer;
        bool founderAnnounced;

        public bool IsSubmarine { get; private set; }
        public bool Foundered { get; private set; }
        public int HullCount { get; private set; }
        public int ModuleCount => modules.Count;
        public int LeakingCount { get; private set; }
        public int CriticalCount { get; private set; }
        public double WaterTonnes { get; private set; }
        public double CapacityTonnes { get; private set; }
        public double InflowTonnesPerSec { get; private set; }

        public override Activation GetActivation()
        {
            return Activation.FlightScene | Activation.LoadedVessels;
        }

        protected override void OnStart()
        {
            GameEvents.onVesselWasModified.Add(OnVesselModified);
            GameEvents.onVesselPartCountChanged.Add(OnVesselModified);
        }

        void OnDestroy()
        {
            GameEvents.onVesselWasModified.Remove(OnVesselModified);
            GameEvents.onVesselPartCountChanged.Remove(OnVesselModified);
        }

        void OnVesselModified(Vessel v)
        {
            if (v == vessel) dirty = true;
        }

        internal void MarkDirty()
        {
            dirty = true;
        }

        public override void OnGoOnRails()
        {
            accumulator = 0f;
        }

        void Rebuild()
        {
            dirty = false;
            modules.Clear();
            surfaceAI = null;
            hasSubmarinePart = false;
            HullCount = 0;
            if (vessel == null || vessel.parts == null) return;

            List<Part> parts = vessel.parts;
            for (int i = 0; i < parts.Count; i++)
            {
                Part p = parts[i];
                if (p == null) continue;
                PartModuleList pm = p.Modules;
                for (int j = 0; j < pm.Count; j++)
                {
                    PartModule m = pm[j];
                    ModuleHullBreach hb = m as ModuleHullBreach;
                    if (hb != null)
                    {
                        modules.Add(hb);
                        if (hb.submarine) hasSubmarinePart = true;
                        if (hb.hull) HullCount++;
                        continue;
                    }
                    if ((object)surfaceAI == null)
                    {
                        BDModuleSurfaceAI ai = m as BDModuleSurfaceAI;
                        if (ai != null) surfaceAI = ai;
                    }
                }
            }
            subRecheckTimer = 0f; // re-evaluate submarine status on the next tick
        }

        void UpdateSubmarineStatus()
        {
            // SurfaceTypeName is compared as a string: BDModuleSurfaceAI.SurfaceType does an Enum.Parse (allocates) on every call.
            bool aiSub = (object)surfaceAI != null && surfaceAI && surfaceAI.SurfaceTypeName == "Submarine";
            IsSubmarine = aiSub || hasSubmarinePart;
        }

        void FixedUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight || vessel == null || !vessel.loaded || vessel.packed) return;
            if (dirty) Rebuild();
            if (modules.Count == 0) return;

            accumulator += TimeWarp.fixedDeltaTime;
            float interval = HullBreachSettings.UpdateInterval;
            if (accumulator < interval) return;
            float dt = accumulator;
            accumulator = 0f;

            CelestialBody body = vessel.mainBody;
            bool inWater = HullBreachSettings.Enabled && body != null && body.ocean && (vessel.Splashed || vessel.altitude < 0.0);
            if (!inWater)
            {
                if (wasInWater) LeaveWater();
                return;
            }
            wasInWater = true;

            subRecheckTimer -= dt;
            if (subRecheckTimer <= 0f)
            {
                subRecheckTimer = SubmarineRecheckInterval;
                UpdateSubmarineStatus();
            }

            bool foundered = Foundered;
            bool sub = IsSubmarine;
            int leaking = 0, critical = 0, newBreaches = 0, newCritical = 0;
            double water = 0, capacity = 0, inflow = 0, minCrushMargin = double.MaxValue;

            for (int i = 0; i < modules.Count; i++)
            {
                ModuleHullBreach m = modules[i];
                if ((object)m == null || !m || m.vessel != vessel)
                {
                    dirty = true; // destroyed or decoupled; rebuild next tick
                    continue;
                }

                m.Tick(dt, sub, foundered);

                if (m.hull)
                {
                    double density = m.WaterDensity;
                    water += m.waterUnits * density;
                    capacity += m.capacityUnits * density;
                    inflow += m.inflowUnitsPerSec * density;
                    if (m.state == BreachState.Leaking) leaking++;
                    else if (m.state == BreachState.Critical) critical++;
                    if (m.state > m.previousState)
                    {
                        if (m.state == BreachState.Critical) newCritical++;
                        else newBreaches++;
                    }
                    m.previousState = m.state;
                }
                if (m.crushMargin < minCrushMargin) minCrushMargin = m.crushMargin;
            }

            LeakingCount = leaking;
            CriticalCount = critical;
            WaterTonnes = water;
            CapacityTonnes = capacity;
            InflowTonnesPerSec = inflow;

            // Foundered: a non-submarine that is both under water and flooding. Requiring flooding keeps
            // intentionally submerged craft (manual subs without a BDA AI) from being treated as wrecks.
            Foundered = !sub && water > 0.0 && vessel.altitude < HullBreachSettings.FounderAltitude;

            if (HullBreachSettings.Messages && vessel.isActiveVessel) PostMessages(dt, newBreaches, newCritical, minCrushMargin);
            else { breachMsgTimer -= dt; crushWarnTimer -= dt; }
        }

        void PostMessages(float dt, int newBreaches, int newCritical, double minCrushMargin)
        {
            breachMsgTimer -= dt;
            crushWarnTimer -= dt;

            if ((newBreaches > 0 || newCritical > 0) && breachMsgTimer <= 0f)
            {
                breachMsgTimer = BreachMessageInterval;
                ScreenMessages.PostScreenMessage(newCritical > 0 ? "Warning: Critical Hull Breach" : "Warning: Hull Breach", 4f, ScreenMessageStyle.UPPER_CENTER);
            }

            if (Foundered && !founderAnnounced)
            {
                founderAnnounced = true;
                ScreenMessages.PostScreenMessage("Abandon ship! " + vessel.GetDisplayName() + " is foundering", 5f, ScreenMessageStyle.UPPER_CENTER);
            }
            else if (!Foundered) founderAnnounced = false;

            if (minCrushMargin < 50.0 && crushWarnTimer <= 0f)
            {
                crushWarnTimer = CrushWarnInterval;
                ScreenMessages.PostScreenMessage(minCrushMargin <= 0.0 ? "Warning: hull is being crushed!" : "Warning: approaching crush depth!", 3f, ScreenMessageStyle.LOWER_CENTER);
            }
        }

        void LeaveWater()
        {
            wasInWater = false;
            Foundered = false;
            founderAnnounced = false;
            InflowTonnesPerSec = 0;
            for (int i = 0; i < modules.Count; i++)
            {
                ModuleHullBreach m = modules[i];
                if ((object)m != null && m) m.OutOfWater();
            }
        }
    }
}
