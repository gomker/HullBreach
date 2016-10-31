using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HullBreach
{
    public class ModuleHullBreach : PartModule
    {
        #region KSP Fields

        public bool isHullBreached;
        public string DamageState = "None"; //None, Normal,Critical

        [KSPField(isPersistant = false)]
        public double flowRate = .5;

        [KSPField(isPersistant = false)]
        public double critFlowRate = 1;

        [KSPField(isPersistant = false)]
        public double breachTemp = 0.6;

        [KSPField(isPersistant = false)]
        public double critBreachTemp = 0.9;

        [KSPField(isPersistant = true)]
        public bool hydroExplosive = false;

        [KSPField(isPersistant = true)]
        public bool hull = false;

        #region Debug Fields

        [KSPField(isPersistant = true)]
        public bool partDebug = true;

        [KSPField(guiActive = true, isPersistant = false, guiName = "Submerged Portion")]
        public double sumergedPortion;

        [KSPField(guiActive = true, isPersistant = false, guiName = "Current Situation")]
        public string vesselSituation;

        [KSPField(guiActive = true, isPersistant = false, guiName = "Heat Level")]
        public double pctHeat = 0;

        [KSPField(guiActive = true, isPersistant = false, guiName = "Current Depth")]
        public double currentDepth = 0;

        //[KSPField(guiActive = true, isPersistant = false, guiName = "Current Altitude")]
        //public double currentAlt = 0;

        #endregion DebugFields

        //[UI_FloatRange(minValue = 1, maxValue = 10, stepIncrement = 1)]
        [UI_FloatRange(minValue = 1, maxValue = 100, stepIncrement = 1)]
        [KSPField(guiActive = true, guiActiveEditor = true, /*guiFormat = "P0",*/ isPersistant = true, guiName = "FlowRateModifier")]
        public float flowMultiplier = 1;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Test Breach")]
        Boolean forceHullBreach;

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Test Breach")]
        public void ToggleHullBreach()
        {
            if (isHullBreached)
            {
                isHullBreached = false;
                forceHullBreach = false;
                DamageState = "None";
                FixedUpdate();
            }
            else
            {
                isHullBreached = true;
                forceHullBreach = true;
                DamageState = "Critical";
                FixedUpdate();
            }
        }

        #endregion KSPFields

        #region GameEvents
        public override void OnStart(StartState state)
        {
            if (state != StartState.Editor & vessel != null & partDebug == false) //hiding info fields for troubleshooting
            //foreach (BaseField f in Fields) { f.guiActive = false; } ???
            {
                Fields["Submerged Portion"].guiActive = false;
                Fields["Current Situation"].guiActive = false;
                Fields["Heat Level"].guiActive = false;
                Fields["Current Depth"].guiActive = false;
                //Fields["Current Altitude"].guiActive = false;
            }

            rescanMethod();
            // GameEvents.onVesselStandardModification.Add(rescanMethod);
            // onVesselStandardModification collects various vessel events and fires them off with a single one.
            // Specifically - onPartAttach,onPartRemove,onPartCouple,onPartDie,onPartUndock,onVesselWasModified,onVesselPartCountChanged

            GameEvents.onVesselPartCountChanged.Add(rescanMethod);
        }

        List<Part> HullParts = new List<Part>();

        public void rescanMethod(Vessel gameEventVessel = null)
        {
            if (!(vessel.situation == Vessel.Situations.SPLASHED)) return;
            if (hull)
            {
                ScreenMessages.PostScreenMessage("Catastrophic Hull Damage", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                if (part.parent.Modules.Contains("HullBreach") & this.part.children.Count > 0)
                {
                    foreach (Part p in this.part.children)
                    {
                        isHullBreached = true;
                        forceHullBreach = true;
                        DamageState = "Critical";
                    }
                }
            }
        }

        public void FixedUpdate()
        {
            if (vessel == null) { return; }
            part.rigidAttachment = true;
            if (!(vessel.situation == Vessel.Situations.SPLASHED)) return;

            if (part.WaterContact & ShipIsDamaged() & isHullBreached & hull)
            {
                if (FlightGlobals.ActiveVessel) { ScreenMessages.PostScreenMessage("Warning: Hull Breach", 5.0f, ScreenMessageStyle.UPPER_CENTER); }

                switch (DamageState)
                {
                    case "Normal":
                        vessel.IgnoreGForces(240);
                        part.RequestResource("SeaWater", (0 - (flowRate * (0.1 + part.submergedPortion) * flowMultiplier)));
                        break;
                    case "Critical":
                        vessel.IgnoreGForces(240);
                        part.RequestResource("SeaWater", (0 - (critFlowRate * (0.1 + part.submergedPortion) * flowMultiplier)));
                        break;
                }
            }

            //If part underwater add heat (damage) at a greater rate based on depth to simulate pressure
            sumergedPortion = Math.Round(this.part.submergedPortion, 4);

            if (this.part.submergedPortion == 1 & hydroExplosive)
            {
                part.temperature += (0.1 * this.part.depth);
            }
            else if (crushable & part.submergedPortion == 1)
            {
                part.RequestResource("ElectricCharge", 1000); //kill EC if sumberged

                if (crushable) part.buoyancy = -1.0f; // trying to kill floaty bits that never sink 

                if (warnTimer > 0f) warnTimer -= Time.deltaTime;
                if (part.depth > warnDepth && oldVesselDepth > warnDepth && warnTimer <= 0)
                {
                    if (FlightGlobals.ActiveVessel) { ScreenMessages.PostScreenMessage("Warning! Vessel will be crushed at " + (crushDepth) + "m depth!", 3, ScreenMessageStyle.LOWER_CENTER); }
                    warnTimer = 5;
                }
                oldVesselDepth = part.depth;
                crushingDepth();
            }
        }

        public void LateUpdate()
        {
            if (vessel == null) { return; }

            vesselSituation = vessel.situation.ToString();
            //currentAlt = Math.Round(TrueAlt(),2);
            pctHeat = Math.Round((this.part.temperature / this.part.maxTemp) * 100);
            currentDepth = Math.Round(this.part.depth, 2);
        }

        public double TrueAlt()
        {
            Vector3 pos = this.vessel.GetWorldPos3D();
            double ASL = FlightGlobals.getAltitudeAtPos(pos);
            if (this.vessel.mainBody.pqsController == null) { return ASL; }
            double terrainAlt = this.vessel.pqsAltitude;
            if (this.vessel.mainBody.ocean && terrainAlt <= 0) { return ASL; } //Checks for oceans
            return ASL - terrainAlt;
        }

        #endregion

        #region HullBreach Events
        public bool ShipIsDamaged()
        {
            //Check Damage Based on Heat
            //Increase DamageState Nomal/Crit Level
            //Flip isHullBreached to Trigger adding SeaWater
            if (this.part.temperature >= (this.part.maxTemp * breachTemp))
            {
                isHullBreached = true;
                DamageState = "Normal";
            }
            else if (this.part.temperature >= (this.part.maxTemp * critBreachTemp))
            {
                isHullBreached = true;
                DamageState = "Critical";
            }

            if (forceHullBreach == true) //forcing if testing hull breach or if Catastrophic damage triggerd
            {
                return true;
            }
            else if (DamageState == "None")
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        #region Parts that do not take on water crushed by going below a certain depth

        [KSPField(isPersistant = true)]
        public bool crushable = false;

        public double warnTimer = 0;
        public double warnDepth = 100;
        public double oldVesselDepth;

        [KSPField(isPersistant = true)]
        public double crushDepth = 200;

        private void crushingDepth()
        {
            //Nothing crushed unless : Vessel is under water, part is crushable,part is fully submerged, part is not a hull and part is not hydroexplosive
            // Any of these true do not crush
            if (!crushable || hull || hydroExplosive || part.submergedPortion != 1 || TrueAlt() > 0.01) return;

            if (crushable & part.depth > crushDepth & (TrueAlt() * -1) > crushDepth)
            {                
                part.explode();
            }

        }

        #endregion

        #endregion

    }
}
