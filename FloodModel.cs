using System;

namespace HullBreach
{
    /// <summary>
    /// Pure flooding maths, deliberately free of KSP/Unity types so it can be unit-tested
    /// and simulated offline (see Tests/FloodModelSim).
    ///
    /// Model summary
    /// -------------
    /// * severity s in [0,1] combines BDArmory hitpoint loss and armour loss of the part.
    /// * Below breachThreshold the hull is watertight. Above it a hole exists whose
    ///   effective area grows linearly up to s = 1 (LeakArea).
    /// * The part is a compartment of capacity C (units of SeaWater). Water flows in until the
    ///   level inside matches the waterline outside, i.e. target = C * immersion, which exactly
    ///   cancels the buoyancy of the submerged portion of that part. Only part of the
    ///   compartment is open to the sea for moderate damage (CompartmentFill), reaching the
    ///   whole compartment at criticalThreshold. Past critical, up to criticalOverfill extra
    ///   flooding is allowed (progressive flooding / water over the deck), so a critically
    ///   damaged compartment becomes negatively buoyant.
    /// * Flow rate ~ C * leakArea * sqrt(head) / floodTime, with the depth factor clamped so
    ///   submarines at depth flood faster than surface ships but never "instantly".
    /// * Submarines get damage-control pumping (a small fraction of capacity per second while a breach
    ///   is not critical), so scratches do not doom a neutrally trimmed boat. Surface ships rely on
    ///   reserve buoyancy instead.
    /// </summary>
    public static class FloodModel
    {
        /// <summary>Combine hitpoint fraction and armour fraction (1 = intact) into breach severity.</summary>
        public static float Severity(float hpFraction, float armorFraction, float armorWeight)
        {
            float hpLoss = 1f - Clamp01(hpFraction);
            float armorLoss = 1f - Clamp01(armorFraction);
            float w = Clamp01(armorWeight);
            return Clamp01(1f - (1f - hpLoss) * (1f - w * armorLoss));
        }

        /// <summary>Fraction of the compartment that is open to the sea (0 at breach, 1 at critical).</summary>
        public static double CompartmentFill(double s, double breachThreshold, double criticalThreshold)
        {
            if (s < breachThreshold) return 0;
            double span = Math.Max(1e-3, criticalThreshold - breachThreshold);
            return Clamp01((s - breachThreshold) / span);
        }

        /// <summary>Normalised hole area (0 at breach threshold, 1 at full destruction).</summary>
        public static double LeakArea(double s, double breachThreshold)
        {
            if (s < breachThreshold) return 0;
            double span = Math.Max(1e-3, 1.0 - breachThreshold);
            return Clamp01((s - breachThreshold) / span);
        }

        /// <summary>Extra fill allowed past critical (0..criticalOverfill).</summary>
        public static double Overfill(double s, double criticalThreshold, double criticalOverfill)
        {
            if (s <= criticalThreshold || criticalOverfill <= 0) return 0;
            double span = Math.Max(1e-3, 1.0 - criticalThreshold);
            return criticalOverfill * Clamp01((s - criticalThreshold) / span);
        }

        /// <summary>
        /// Torricelli-style head factor: flow ~ sqrt(depth). Clamped so deep submarines are not
        /// flooded instantly (maxFactor) and holes right at the waterline still leak (minFactor).
        /// </summary>
        public static double DepthFactor(double depth, double refDepth, double minFactor, double maxFactor)
        {
            double head = Math.Max(0, depth) + 1.0;
            double f = Math.Sqrt(head / Math.Max(0.1, refDepth));
            if (f < minFactor) f = minFactor;
            if (f > maxFactor) f = maxFactor;
            return f;
        }

        /// <summary>
        /// Equilibrium water content for this compartment (units).
        /// subdivision &gt; 1 models a part that is internally divided into several watertight
        /// compartments (e.g. a whole ship modelled as one part): the open fraction becomes
        /// fill^subdivision, so light damage floods much less of it while critical damage still floods it all.
        /// </summary>
        public static double TargetWater(double capacity, double immersion, double s,
            double breachThreshold, double criticalThreshold, double criticalOverfill, double subdivision = 1.0)
        {
            if (capacity <= 0 || immersion <= 0) return 0;
            double fill = CompartmentFill(s, breachThreshold, criticalThreshold);
            if (fill <= 0) return 0;
            if (subdivision > 1.0) fill = Math.Pow(fill, subdivision);
            double level = Math.Min(1.0, Clamp01(immersion) + Overfill(s, criticalThreshold, criticalOverfill));
            return capacity * level * fill;
        }

        /// <summary>Inflow rate at full head, units per second.</summary>
        public static double FlowRate(double capacity, double s, double breachThreshold, double depthFactor,
            double multiplier, double floodTime)
        {
            if (capacity <= 0 || floodTime <= 0) return 0;
            return capacity * LeakArea(s, breachThreshold) * depthFactor * Math.Max(0, multiplier) / floodTime;
        }

        /// <summary>Move water toward target, rate-limited. Water drains out (slower) when above target.</summary>
        public static double Step(double amount, double target, double rate, double dt, double drainFactor)
        {
            double delta = target - amount;
            double step = rate * dt;
            if (step <= 0) return amount;
            if (delta > 0) return amount + Math.Min(delta, step);
            return amount + Math.Max(delta, -step * Clamp01(drainFactor));
        }

        /// <summary>
        /// Damage-control pumping: removes up to capacity * rate * dt. Applied only to non-critical
        /// breaches, so small leaks (inflow below the pump rate) are held near empty while large
        /// breaches still flood, just a little slower.
        /// </summary>
        public static double DamageControl(double amount, double capacity, double rate, double dt)
        {
            if (amount <= 0 || capacity <= 0 || rate <= 0 || dt <= 0) return amount;
            return Math.Max(0.0, amount - capacity * rate * dt);
        }

        public static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
        public static double Clamp01(double v) { return v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v); }
    }
}
