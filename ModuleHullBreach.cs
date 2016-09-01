﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HullBreach
{
    public class ModuleHullBreach : PartModule
    {
        public bool HullisBreached;
        public bool ShipisWrecked;
        public bool TakingOnWater;
        public string DamageState ="None"; //None, Normal,Critical

        [KSPField(isPersistant = false)]
        public double flowRate = .5;

        [KSPField(isPersistant = false)]
        public double critFlowRate = 1;

        [KSPField(isPersistant = false)]
        public double breachTemp = 0.6;

        [KSPField(isPersistant = false)]
        public double critBreachTemp = 0.9;

        [UI_FloatRange(minValue = 1, maxValue = 100, stepIncrement = 1)]
        [KSPField(guiActive = true, guiActiveEditor = true, guiFormat = "P0", isPersistant = true, guiName = "FlowRateModifier")]
        public float flowMultiplier = 1;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Test Hull Breach")]
        Boolean HullBreachTest;

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Toggle HullBreach")]
        public void ToggleHullBreach()
        {
            if (HullisBreached)
            {
                HullisBreached = false;
                ShipisWrecked = false;
                TakingOnWater = false;
                HullBreachTest = false;
            }
            else
            {
                HullisBreached = true;
                ShipisWrecked = true;
                TakingOnWater = true;
                HullBreachTest = true;
            }
        }

        public override void OnFixedUpdate()
        {
            
            if (!HighLogic.LoadedSceneIsFlight) return;

            if (this.part.WaterContact && HullisBreached)
            { 
            //Water Should Come In 
                switch (DamageState)
                {
                    case "Normal":
                        this.part.RequestResource("SeaWater", (0 - (critFlowRate * (0.1 + this.part.submergedPortion))));
                        break;
                    case "Critical":
                        this.part.RequestResource("SeaWater", (0 - (flowRate * (0.1 + this.part.submergedPortion))));
                        break;
                }
            }
        }

        public bool ShipIsDamaged()
        {
            
            return true;

        }
        public bool HullBreachState()
        {

            
            HullisBreached = true;
            return true;
        }
    }
}