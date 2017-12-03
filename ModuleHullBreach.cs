using System;
using UnityEngine;
using BDArmory.Core.Extension;
using BDArmory.Core.Module;

namespace HullBreach
{   
    public class ModuleHullBreach : PartModule
    {
        static ModuleHullBreach instance;
        public static ModuleHullBreach Instance => instance;

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

        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Crush Depth", isPersistant = true), UI_FloatRange(minValue = 0f, maxValue = 600f, stepIncrement = 1f)]
        public float crushDepth = 200f;
                
        [KSPField(isPersistant = true)]
        public bool DepthCharge = false;

        #region Debug Fields

        [KSPField(isPersistant = true)]
        public bool partDebug = true;

        //[KSPField(guiActive = true, isPersistant = false, guiName = "Submerged Portion")]
        //public double sumergedPortion;

        //[KSPField(guiActive = true, isPersistant = false, guiName = "Current Situation")]
        //public string vesselSituation;

        //[KSPField(guiActive = true, isPersistant = false, guiName = "Heat Level")]
        //public double pctHeat = 0;

        [KSPField(guiActive = true, isPersistant = false, guiName = "Current Depth")]
        public double currentDepth = 0;

        [KSPField(guiActive = true, isPersistant = false, guiName = "Vessel Mass")]
        public double VesselMass;

        #endregion DebugFields

        [UI_FloatRange(minValue = 1, maxValue = 100, stepIncrement = 1)] [KSPField(guiActive = true, guiActiveEditor = true,isPersistant = true, guiName = "Flow Rate")]
        public float flowMultiplier = 1;

        [KSPField(isPersistant = true, guiActive = true,guiActiveEditor = false, guiName = "Test Breach")]
        public static Boolean forceHullBreach;

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Test Breach")]


        #endregion KSPFields

        public void ToggleHullBreach()
        {
            //if (!(vessel.id == FlightGlobals.ActiveVessel.id)) { return; }
            //if (!(vessel.id == FlightGlobals.ActiveVessel.id)) { return; }
            if (!vessel.isActiveVessel) { return; }

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

        #region GameEvents

        public override void OnStart(StartState state)
        {
            if (state != StartState.Editor && vessel != null)
            {
                part.force_activate();
                instance = this;
                if(part.FindModulesImplementing<ModuleHullBreach>().Count !=0)
                    config.Instance.vesselHullBreach = this;
            }

            if (!crushable)
            {
                Fields["crushDepth"].guiActive = false;
                Fields["crushDepth"].guiActiveEditor = false;
            }
            else
            {
                Fields["flowMultiplier"].guiActive = false;
                Fields["flowMultiplier"].guiActiveEditor = false;
            }

            //if (state != StartState.Editor & vessel != null & partDebug == false)
            //hiding info fields for troubleshooting
            //foreach (BaseField f in Fields) { f.guiActive = false; } ???
            //{
            //Fields["Submerged Portion"].guiActive = false;
            //Fields["Current Situation"].guiActive = false;
            //Fields["Heat Level"].guiActive = false;
            //Fields["Current Depth"].guiActive = false;
            //Fields["Current Altitude"].guiActive = false;
            //}

            // GameEvents.onVesselStandardModification.Add(triggerCatastrophicBreach);
            //  onVesselStandardModification collects various vessel events and fires them off with a single one.
            //  Specifically - onPartAttach,onPartRemove,onPartCouple,onPartDie,onPartUndock,onVesselWasModified,onVesselPartCountChanged
            // List<Part> HullParts = new List<Part>();

            //  GameEvents.onVesselPartCountChanged.Add(triggerCatastrophicBreach);

            //if(part.Modules.Contains("ModuleHullBreach")) GameEvents.onPartJointBreak.Add(CheckCatastrophicBreach);
        }

        public void CheckCatastrophicBreach(PartJoint partJoint, float breakForce)
        {
            if (vessel.situation != Vessel.Situations.SPLASHED) return;

            //if (hull)
            //{
                // ScreenMessages.PostScreenMessage("Catastrophic Hull Damage", 30.0f, ScreenMessageStyle.UPPER_CENTER);
            //}
        }

        public void FixedUpdate()
        {
           if (!HighLogic.LoadedSceneIsFlight) return;

           try
            {
                if (part == null ||
                    !part.Modules.Contains("ModuleHullBreach") ||
                    !part.Modules.Contains("HitpointTracker")                    
                    )
                    return;
            }
            catch (Exception)
            { }

            part.rigidAttachment = true;

            if (vessel.situation != Vessel.Situations.SPLASHED) return;

            if (part.WaterContact & ShipIsDamaged() & isHullBreached & hull)
            {
                if (FlightGlobals.ActiveVessel)
                {
                    if (DamageState == "Critical")
                    {
                        ScreenMessages.PostScreenMessage("Warning: Critical Hull Breach", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    }
                    else
                    {
                        ScreenMessages.PostScreenMessage("Warning: Hull Breach", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                    }
                }

                switch (DamageState)
                {
                    case "Normal":
                        vessel.IgnoreGForces(240);
                        part.RequestResource("SeaWater", (0 - (flowRate*(0.1 + part.submergedPortion)*flowMultiplier)));
                        break;
                    case "Critical":
                        vessel.IgnoreGForces(240);
                        part.RequestResource("SeaWater",
                            (0 - (critFlowRate*(0.1 + part.submergedPortion)*flowMultiplier)));
                        break;
                }
            }

            //If part underwater add heat (damage) at a greater rate based on depth to simulate pressure
            //sumergedPortion = Math.Round(this.part.submergedPortion, 4);

            if (part.submergedPortion == 1.00 & hydroExplosive)
            {
                part.temperature += (0.1*part.depth);
            }
            else if (crushable && part.submergedPortion == 1.00 && !part.localRoot.name.StartsWith("Sub"))
            {

                if(config.ecDrain)
                    part.RequestResource("ElectricCharge", 1000); //kill EC if sumberged

                if (crushable) part.buoyancy = -1.0f; // trying to kill floaty bits that never sink 

                if (warnTimer > 0f) warnTimer -= Time.deltaTime;
                if (part.depth > warnDepth && oldVesselDepth > warnDepth && warnTimer <= 0)
                {
                    if (FlightGlobals.ActiveVessel)
                    {
                        if (DepthCharge == false)
                        {
                            ScreenMessages.PostScreenMessage(
                            "Warning! Vessel will be crushed at " + (crushDepth) + "m depth!", 3,
                            ScreenMessageStyle.LOWER_CENTER);
                        }
                        else
                        {
                            ScreenMessages.PostScreenMessage(
                            (crushDepth) + "m Charge Deployed!", 3,
                            ScreenMessageStyle.LOWER_CENTER);
                        }
                    }
                    warnTimer = 5;
                }
                oldVesselDepth = part.depth;
                crushingDepth();
            }
        }

        public void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            try
            {
                if (part == null ||
                    !part.Modules.Contains("ModuleHullBreach") ||
                    !part.Modules.Contains("HitpointTracker")
                    )
                    return;
            }
            catch (Exception)
            { }
            
            //vesselSituation = vessel.situation.ToString();
            //currentAlt = Math.Round(TrueAlt(),2);
            //pctHeat = Math.Round((part.temperature/part.maxTemp)*100);
            currentDepth = Math.Round(part.depth, 2);
            VesselMass = Math.Round(vessel.totalMass);
        }
        
        public void OnDestroy()
        {
            instance = null;
        }

        #endregion

        #region HullBreach Events

        public bool ShipIsDamaged()
        {
            //Check Damage Based on Heat
            //Increase DamageState Nomal/Crit Level
            //Flip isHullBreached to Trigger adding SeaWater

            //if (part.temperature >= (part.maxTemp*breachTemp))
            float damage = part.Damage();
            float max_damage = part.MaxDamage();
            float dmg_pct = damage / max_damage;

            if (dmg_pct >= critBreachTemp && dmg_pct <= breachTemp )
            {
                isHullBreached = true;
                DamageState = "Normal";
            }
            else if (dmg_pct >= 0 && dmg_pct <= critBreachTemp)
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

        [KSPField(isPersistant = true)] public bool crushable = false;

        public double warnTimer = 0;
        public double warnDepth = 100;
        public double oldVesselDepth;

        private void crushingDepth()
        {
            //Nothing crushed unless : Vessel is under water, part is crushable,part is fully submerged, part is not a hull and part is not hydroexplosive
            // Any of these true do not crush
            if (!crushable || hull || hydroExplosive || part.submergedPortion != 1.00 || TrueAlt() > 0.01) return;

            if (crushable & part.depth > crushDepth & (TrueAlt()*-1) > crushDepth)
            {
                part.explode();
            }
        }

        public double TrueAlt()
        {
            Vector3 pos = vessel.GetWorldPos3D();
            double ASL = FlightGlobals.getAltitudeAtPos(pos);
            if (vessel.mainBody.pqsController == null)
            {
                return ASL;
            }
            double terrainAlt = vessel.pqsAltitude;
            if (vessel.mainBody.ocean && terrainAlt <= 0)
            {
                return ASL;
            } //Checks for oceans
            return ASL - terrainAlt;
        }

        #endregion

        #endregion
    }
}