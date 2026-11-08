using System;
using System.Globalization;
using System.Text;
using BDArmory.Damage;
using UnityEngine;

namespace HullBreach
{
    public enum BreachState
    {
        Intact = 0,
        Leaking = 1,
        Critical = 2,
    }

    /// <summary>
    /// Per-part HullBreach configuration and state.
    ///
    /// This module has no Update/FixedUpdate/LateUpdate of its own: all simulation is driven
    /// at a fixed interval by <see cref="HullBreachVesselModule"/> (one per loaded vessel), so the
    /// per-frame cost of hundreds of hull parts in a fleet is zero and the per-tick cost is linear
    /// in the number of HullBreach parts, with no allocations.
    /// </summary>
    public class ModuleHullBreach : PartModule
    {
        internal const string SeaWaterName = "SeaWater";
        const string ElectricChargeName = "ElectricCharge";

        const string StatusIntact = "Intact";
        const string StatusLeaking = "Leaking";
        const string StatusCritical = "Critical breach";
        const string StatusFlooded = "Flooded (sealed)";
        const string StatusCrushing = "Crushing";

        #region Config fields (part cfg / MM patch)
        // These are intentionally NOT persistent: in 1.7 they were, which meant craft files saved
        // with an old patch kept the old role forever. The part config is now authoritative.

        /// <summary>Watertight compartment: floods when damaged below the waterline.</summary>
        [KSPField] public bool hull = false;

        /// <summary>Non-floodable part (superstructure, turret, mast...): loses buoyancy when its vessel founders.</summary>
        [KSPField] public bool crushable = false;

        /// <summary>Heats up (and eventually explodes) when fully submerged on a foundered vessel.</summary>
        [KSPField] public bool hydroExplosive = false;

        /// <summary>Explodes when it sinks below crushDepth (legacy depth-charge behaviour).</summary>
        [KSPField] public bool DepthCharge = false;

        /// <summary>Marks the vessel as a submarine even without a BDA Surface AI in Submarine mode.</summary>
        [KSPField] public bool submarine = false;

        /// <summary>Flood capacity in SeaWater units. &lt;= 0: derived from KSP's own buoyancy volume for the part.</summary>
        [KSPField] public float floodCapacity = -1f;

        /// <summary>Per-part breach threshold override (0..1). &lt;= 0: use global setting.</summary>
        [KSPField] public float breachThreshold = -1f;

        /// <summary>Per-part critical threshold override (0..1). &lt;= 0: use global setting.</summary>
        [KSPField] public float criticalThreshold = -1f;

        /// <summary>
        /// Internal subdivision of the part (&gt;= 1). Use ~2 for single-part ships/submarines so light
        /// damage floods a small fraction of the whole vessel; critical damage still floods it all.
        /// </summary>
        [KSPField] public float subdivision = 1f;
        #endregion

        #region Tweakables (persistent)
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Crush Depth", guiUnits = " m"),
         UI_FloatRange(minValue = 0f, maxValue = 600f, stepIncrement = 10f)]
        public float crushDepth = 200f;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Flood Rate", guiFormat = "F2"),
         UI_FloatRange(minValue = 0.25f, maxValue = 4f, stepIncrement = 0.25f)]
        public float flowMultiplier = 1f;

        /// <summary>BDA armour thickness when first seen in flight, so armour loss survives save/load.</summary>
        [KSPField(isPersistant = true)]
        public float armorBaseline = -1f;
        #endregion

        #region Display fields
        [KSPField(guiActive = true, guiName = "Hull")]
        public string hullStatus = StatusIntact;

        [KSPField(guiActive = true, guiName = "Flooded", guiFormat = "P0")]
        public float floodedDisplay = 0f;

        [KSPField(guiActive = true, guiName = "Inflow", guiUnits = " t/s", guiFormat = "F2")]
        public float inflowDisplay = 0f;

        [KSPField(guiActive = false, guiName = "Breach Severity", guiFormat = "P0")]
        public float severityDisplay = 0f;

        [KSPField(guiActive = false, guiName = "Depth", guiUnits = " m", guiFormat = "F1")]
        public float depthDisplay = 0f;
        #endregion

        #region Runtime state
        HitpointTracker hp;
        UI_ProgressBar hpBar;
        bool hpBound;
        PartResource water;
        PartResource ec;
        PartResourceDefinition waterDef;
        double waterDensity = 1.0;

        internal bool initialized;
        internal float severity;
        internal BreachState state;
        internal BreachState previousState;
        internal double capacityUnits;
        internal double waterUnits;
        internal double floodFraction;
        internal double inflowUnitsPerSec;
        /// <summary>Metres of depth left before this part starts being crushed (large if not applicable).</summary>
        internal double crushMargin = double.MaxValue;
        internal bool crushing;

        float forcedSeverity;
        bool buoyancyOverridden;
        float savedBuoyancy;
        float crushTimer;
        bool exploded;
        bool debugShown;
        bool started;

        BaseField fFlooded, fInflow, fSeverity, fDepth, fFlow;
        BaseEvent eTest;

        static bool loggedMissingResource;
        #endregion

        internal double WaterDensity => waterDensity;

        public override void OnStart(StartState startState)
        {
            base.OnStart(startState);
            ApplyPrefabRoles();
            flowMultiplier = Mathf.Clamp(flowMultiplier, 0.25f, 4f); // 1.7 allowed up to 100x

            fFlooded = Fields[nameof(floodedDisplay)];
            fInflow = Fields[nameof(inflowDisplay)];
            fSeverity = Fields[nameof(severityDisplay)];
            fDepth = Fields[nameof(depthDisplay)];
            fFlow = Fields[nameof(flowMultiplier)];
            eTest = Events[nameof(ToggleTestBreach)];

            if (fFlooded != null) fFlooded.guiActive = hull;
            if (fInflow != null) fInflow.guiActive = hull;
            if (fFlow != null) { fFlow.guiActiveEditor = hull; fFlow.guiActive = false; }
            started = true;

            if (!HighLogic.LoadedSceneIsFlight || vessel == null) return;

            waterDef = PartResourceLibrary.Instance != null ? PartResourceLibrary.Instance.GetDefinition(SeaWaterName) : null;
            if (waterDef != null && waterDef.density > 0f) waterDensity = waterDef.density;
            else if (waterDef == null && hull && !loggedMissingResource)
            {
                loggedMissingResource = true;
                Debug.LogError("[HullBreach]: RESOURCE_DEFINITION SeaWater is missing; hull flooding is disabled. Is GameData/HullBreach/Resources.cfg installed?");
            }

            water = part.Resources.Get(SeaWaterName);
            ec = part.Resources.Get(ElectricChargeName);
            ApplyDebugVisibility(true);
            initialized = true;

            HullBreachVesselModule vm = vessel.GetComponent<HullBreachVesselModule>();
            if (vm != null) vm.MarkDirty();
        }

        /// <summary>
        /// HullBreach 1.7 saved hull/crushable/hydroExplosive/DepthCharge into craft files and saves, and KSP
        /// loads any matching value from there. Re-apply the role fields from the part prefab (i.e. the
        /// current part config / MM patch) so old craft pick up the 1.8 patches.
        /// </summary>
        void ApplyPrefabRoles()
        {
            Part prefab = part != null && part.partInfo != null ? part.partInfo.partPrefab : null;
            if ((object)prefab == null || prefab == part) return;
            ModuleHullBreach src = prefab.Modules.GetModule<ModuleHullBreach>();
            if ((object)src == null || src == this) return;
            hull = src.hull;
            crushable = src.crushable;
            hydroExplosive = src.hydroExplosive;
            DepthCharge = src.DepthCharge;
            submarine = src.submarine;
            floodCapacity = src.floodCapacity;
            breachThreshold = src.breachThreshold;
            criticalThreshold = src.criticalThreshold;
            subdivision = src.subdivision;
        }

        void OnDestroy()
        {
            if (buoyancyOverridden && part != null) part.buoyancy = savedBuoyancy;
        }

        public override string GetInfo()
        {
            StringBuilder sb = new StringBuilder();
            if (hull) sb.AppendLine("Watertight compartment: floods when BDArmory damage (hitpoints and armour) opens the hull below the waterline.");
            if (submarine) sb.AppendLine("Submarine pressure hull.");
            if (crushable) sb.AppendLine("Loses buoyancy when its ship founders.");
            if (hydroExplosive) sb.AppendLine("Explodes if it goes under with the ship.");
            if (DepthCharge) sb.AppendLine("Detonates at the set depth.");
            if (crushDepth > 0f) sb.Append("Crush depth: ").Append(crushDepth.ToString("F0", CultureInfo.InvariantCulture)).AppendLine(" m");
            return sb.ToString();
        }

        [KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "HullBreach: Test Breach")]
        public void ToggleTestBreach()
        {
            forcedSeverity = forcedSeverity > 0f ? 0f : 0.75f;
            ScreenMessages.PostScreenMessage(forcedSeverity > 0f ? "HullBreach: test breach ON (75% severity)" : "HullBreach: test breach OFF",
                3f, ScreenMessageStyle.UPPER_CENTER);
        }

        #region Simulation (called by HullBreachVesselModule)

        internal void Tick(float dt, bool isSubmarine, bool foundered)
        {
            if (!initialized || exploded) return;

            double sp = part.submergedPortion;
            if (sp < 0) sp = 0; else if (sp > 1) sp = 1;
            double depth = sp > 0 ? Math.Max(0.0, part.depth) : 0.0;
            bool fullySubmerged = sp >= 0.999;

            if (hull)
            {
                UpdateSeverity(); // only hulls flood, so only hulls need the BDA damage state
                FloodTick(dt, isSubmarine, sp, depth);
            }
            else { state = BreachState.Intact; floodFraction = 0; inflowUnitsPerSec = 0; waterUnits = 0; capacityUnits = 0; }

            if (crushable) SetFounderBuoyancy(foundered && fullySubmerged);

            if (hydroExplosive && fullySubmerged && (foundered || floodFraction > 0.9))
                part.temperature += HullBreachSettings.HydroHeatRate * depth * dt;

            if (hull && HullBreachSettings.EcDrain && ec != null && floodFraction >= HullBreachSettings.EcShortFraction && ec.amount > 0)
                ec.amount = 0; // flooded compartment: its batteries are shorted out

            CrushTick(dt, isSubmarine, fullySubmerged, depth);

            if (debugShown != HullBreachSettings.Debug) ApplyDebugVisibility(false);
            UpdateDisplay(depth);
        }

        /// <summary>Vessel left the water: undo temporary changes.</summary>
        internal void OutOfWater()
        {
            SetFounderBuoyancy(false);
            crushTimer = 0f;
            crushing = false;
            crushMargin = double.MaxValue;
            inflowUnitsPerSec = 0;
            inflowDisplay = 0f;
        }

        void UpdateSeverity()
        {
            float s = 0f;
            if (BindTracker() && hp.Ready)
            {
                float hpFrac = 1f;
                float maxHp = hpBar != null ? hpBar.maxValue : 0f;
                if (!hp.ArmorPanel && maxHp > 0f) hpFrac = hp.Hitpoints / maxHp;

                float armor = hp.Armor;
                if (armor > armorBaseline) armorBaseline = armor;
                float armorFrac = armorBaseline > 0.5f ? armor / armorBaseline : 1f;

                s = FloodModel.Severity(hpFrac, armorFrac, HullBreachSettings.ArmorWeight);
            }
            severity = Mathf.Max(s, forcedSeverity);
        }

        bool BindTracker()
        {
            if (!hpBound)
            {
                hpBound = true;
                try
                {
                    hp = part.Modules.GetModule<HitpointTracker>();
                    if (hp != null)
                    {
                        BaseField f = hp.Fields[nameof(HitpointTracker.Hitpoints)];
                        hpBar = f != null ? f.uiControlEditor as UI_ProgressBar : null;
                    }
                }
                catch (Exception ex)
                {
                    hp = null;
                    Debug.LogWarning("[HullBreach]: Could not bind BDArmory HitpointTracker on " + part.partInfo?.name + ": " + ex.Message);
                }
            }
            return (object)hp != null && hp; // reference check first, then Unity liveness
        }

        void FloodTick(float dt, bool isSubmarine, double sp, double depth)
        {
            float bt = breachThreshold > 0f ? breachThreshold : HullBreachSettings.BreachThreshold;
            float ct = criticalThreshold > 0f ? criticalThreshold : HullBreachSettings.CriticalThreshold;
            if (ct <= bt) ct = Mathf.Min(1f, bt + 0.01f);
            float s = severity;
            state = s < bt ? BreachState.Intact : (s < ct ? BreachState.Leaking : BreachState.Critical);

            double capacity = waterDef != null ? ComputeCapacity() : 0.0;
            capacityUnits = capacity;
            inflowUnitsPerSec = 0;

            if (water == null && capacity > 0 && state != BreachState.Intact) EnsureWaterResource(capacity);
            if (water == null) { waterUnits = 0; floodFraction = 0; return; }

            if (capacity > 0 && water.maxAmount < capacity) water.maxAmount = capacity;
            double amount = water.amount;

            if (state != BreachState.Intact && capacity > 0)
            {
                double target = FloodModel.TargetWater(capacity, sp, s, bt, ct, HullBreachSettings.CriticalOverfill, subdivision);
                double df = FloodModel.DepthFactor(depth, HullBreachSettings.RefDepth, HullBreachSettings.MinDepthFactor, HullBreachSettings.MaxDepthFactor);
                double mult = flowMultiplier * HullBreachSettings.GlobalFlowMultiplier * (isSubmarine ? HullBreachSettings.SubmarineFlowMultiplier : 1f);
                double rate = FloodModel.FlowRate(capacity, s, bt, df, mult, HullBreachSettings.FloodTime);
                double next = FloodModel.Step(amount, target, rate, dt, HullBreachSettings.DrainFactor);
                if (isSubmarine && state == BreachState.Leaking)
                    next = FloodModel.DamageControl(next, capacity, HullBreachSettings.SubmarineDamageControlRate, dt);
                if (next > water.maxAmount) next = water.maxAmount;
                if (next < 0) next = 0;
                if (next != amount)
                {
                    water.amount = next;
                    if (dt > 0f) inflowUnitsPerSec = (next - amount) / dt;
                    amount = next;
                }
            }
            // Intact (or repaired): the hole is sealed, water stays until pumped out.

            waterUnits = amount;
            double denom = capacity > 0 ? capacity : water.maxAmount;
            floodFraction = denom > 0 ? amount / denom : 0;
        }

        /// <summary>
        /// Water (in SeaWater units) that exactly cancels this part's buoyancy when fully submerged,
        /// using the same quantities as KSP's PartBuoyancy: displacement * oceanDensity * buoyancyScalar * part.buoyancy.
        /// Recomputed every tick so ballast modules that change part.buoyancy (KerbalPowers Naval) stay consistent.
        /// </summary>
        double ComputeCapacity()
        {
            if (floodCapacity > 0f) return floodCapacity;
            PartBuoyancy pb = part.partBuoyancy;
            if ((object)pb == null) return water != null ? water.maxAmount : 0.0;
            double disp = pb.displacement;
            double b = part.buoyancy;
            if (disp <= 0 || b <= 0) return 0.0;
            double rho = vessel.mainBody != null && vessel.mainBody.oceanDensity > 0 ? vessel.mainBody.oceanDensity : 1.0;
            return disp * rho * PhysicsGlobals.BuoyancyScalar * b / waterDensity;
        }

        void EnsureWaterResource(double capacity)
        {
            water = part.Resources.Get(SeaWaterName);
            if (water != null) return;
            ConfigNode n = new ConfigNode("RESOURCE");
            n.AddValue("name", SeaWaterName);
            n.AddValue("amount", "0");
            n.AddValue("maxAmount", capacity.ToString("R", CultureInfo.InvariantCulture));
            n.AddValue("flowState", "True");
            n.AddValue("isTweakable", "False");
            water = part.AddResource(n); // fires onPartResourceListChange so the vessel's resource sets (pumps) see it
        }

        void SetFounderBuoyancy(bool apply)
        {
            if (apply)
            {
                if (!buoyancyOverridden && part.buoyancy > HullBreachSettings.FounderBuoyancy)
                {
                    savedBuoyancy = part.buoyancy;
                    part.buoyancy = HullBreachSettings.FounderBuoyancy;
                    buoyancyOverridden = true;
                }
            }
            else if (buoyancyOverridden)
            {
                part.buoyancy = savedBuoyancy;
                buoyancyOverridden = false;
            }
        }

        void CrushTick(float dt, bool isSubmarine, bool fullySubmerged, double depth)
        {
            crushing = false;
            if (crushDepth <= 0f || !fullySubmerged)
            {
                crushTimer = 0f;
                crushMargin = double.MaxValue;
                return;
            }

            if (DepthCharge)
            {
                crushMargin = double.MaxValue;
                if (depth > crushDepth)
                {
                    exploded = true;
                    if (vessel.isActiveVessel && HullBreachSettings.Messages)
                        ScreenMessages.PostScreenMessage(crushDepth.ToString("F0", CultureInfo.InvariantCulture) + " m charge detonated!", 3f, ScreenMessageStyle.LOWER_CENTER);
                    part.explode();
                }
                return;
            }

            double effective = isSubmarine ? Math.Max(crushDepth, HullBreachSettings.SubmarineMinCrushDepth) : crushDepth;
            crushMargin = effective - depth;
            if (depth <= effective)
            {
                crushTimer = 0f;
                return;
            }

            crushing = true;
            crushTimer += dt;
            double over = depth - effective;
            if (BindTracker() && hpBar != null && !hp.ArmorPanel && hpBar.maxValue > 0f)
                hp.AddDamage((float)(hpBar.maxValue * HullBreachSettings.CrushDamageRate * (1.0 + over / 25.0) * dt));

            if (crushTimer >= HullBreachSettings.CrushFailsafeTime)
            {
                exploded = true;
                part.explode();
            }
        }

        void UpdateDisplay(double depth)
        {
            severityDisplay = severity;
            depthDisplay = (float)depth;
            floodedDisplay = (float)floodFraction;
            inflowDisplay = (float)(inflowUnitsPerSec * waterDensity);

            if (crushing) hullStatus = StatusCrushing;
            else if (!hull) hullStatus = StatusIntact;
            else if (state == BreachState.Critical) hullStatus = StatusCritical;
            else if (state == BreachState.Leaking) hullStatus = StatusLeaking;
            else hullStatus = floodFraction > 0.01 ? StatusFlooded : StatusIntact;
        }

        void ApplyDebugVisibility(bool force)
        {
            if (!started) return;
            bool dbg = HullBreachSettings.Debug;
            if (!force && dbg == debugShown) return;
            debugShown = dbg;
            if (fSeverity != null) fSeverity.guiActive = dbg && hull;
            if (fDepth != null) fDepth.guiActive = dbg;
            if (eTest != null) eTest.guiActive = dbg && hull;
        }

        #endregion
    }
}
