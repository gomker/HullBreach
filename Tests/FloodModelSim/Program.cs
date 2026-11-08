using System;
using System.Globalization;
using HullBreach;

// Offline check of the FloodModel with the default settings (mirrors HullBreachSettings defaults).
// Hydrostatics are quasi-static and uniform (no trim/heel): a surface ship floats at
// immersion = (dry mass + water) / total buoyant capacity and sinks when that exceeds 1.
// A submarine is held at a fixed depth and we report how heavy it gets.
static class Program
{
    const double FloodTime = 60, Bt = 0.10, Ct = 0.60, Overfill = 0.20;
    const double RefDepth = 4, MinDF = 0.5, MaxDF = 2.5, SubMult = 0.5, Drain = 0.5, DamageControl = 0.002;
    const double Dt = 0.1;

    static int failures;

    sealed class Ship
    {
        public string Name;
        public double[] C;
        public double[] S;
        public double[] W;
        public double Mass;
        public bool Sub;
        public double SubDepth;
        public double Subdivision = 1;
        public double HullHeight = 8;
        public double TotalC { get { double c = 0; foreach (double x in C) c += x; return c; } }
        public double TotalW { get { double w = 0; foreach (double x in W) w += x; return w; } }
    }

    static Ship Make(string name, int n, double capEach, double mass, double[] severities, bool sub = false, double subDepth = 0, double subdivision = 1)
    {
        var s = new Ship { Name = name, C = new double[n], S = new double[n], W = new double[n], Mass = mass, Sub = sub, SubDepth = subDepth, Subdivision = subdivision };
        for (int i = 0; i < n; i++) { s.C[i] = capEach; s.S[i] = i < severities.Length ? severities[i] : 0; }
        return s;
    }

    static void Run(Ship sh, double tMax, bool expectSink, double maxSinkTime = double.MaxValue)
    {
        double sp0 = sh.Sub ? 1 : sh.Mass / sh.TotalC;
        double t = 0, sp = sp0, t1 = -1, t5 = -1;
        bool sunk = false;
        for (; t < tMax; t += Dt)
        {
            double raw = sh.Sub ? 1 : (sh.Mass + sh.TotalW) / sh.TotalC;
            if (!sh.Sub && raw >= 1) { sunk = true; sp = 1; break; }
            sp = Math.Min(1, raw);
            for (int i = 0; i < sh.C.Length; i++)
            {
                double depth = sh.Sub ? sh.SubDepth : Math.Max(0, (sp - 0.5) * sh.HullHeight);
                double target = FloodModel.TargetWater(sh.C[i], sp, sh.S[i], Bt, Ct, Overfill, sh.Subdivision);
                double df = FloodModel.DepthFactor(depth, RefDepth, MinDF, MaxDF);
                double rate = FloodModel.FlowRate(sh.C[i], sh.S[i], Bt, df, sh.Sub ? SubMult : 1, FloodTime);
                sh.W[i] = FloodModel.Step(sh.W[i], target, rate, Dt, Drain);
                if (sh.Sub && sh.S[i] >= Bt && sh.S[i] < Ct) sh.W[i] = FloodModel.DamageControl(sh.W[i], sh.C[i], DamageControl, Dt);
            }
            if (sh.Sub)
            {
                double heavy = (sh.Mass + sh.TotalW - sh.TotalC) / sh.TotalC;
                if (t1 < 0 && heavy >= 0.01) t1 = t;
                if (t5 < 0 && heavy >= 0.05) t5 = t;
            }
        }

        string result;
        if (sh.Sub)
        {
            double heavy = (sh.Mass + sh.TotalW - sh.TotalC) / sh.TotalC;
            result = string.Format(CultureInfo.InvariantCulture, "after {0:F0}s {1:P1} heavy; 1% heavy at {2}, 5% heavy at {3}",
                tMax, heavy, t1 < 0 ? "never" : t1.ToString("F0", CultureInfo.InvariantCulture) + "s", t5 < 0 ? "never" : t5.ToString("F0", CultureInfo.InvariantCulture) + "s");
            sunk = t5 >= 0;
        }
        else
        {
            result = sunk
                ? string.Format(CultureInfo.InvariantCulture, "SUNK after {0:F0}s (immersion {1:P0} -> 100%)", t, sp0)
                : string.Format(CultureInfo.InvariantCulture, "afloat, immersion {0:P0} -> {1:P0}, water {2:F0} t", sp0, sp, sh.TotalW);
        }

        bool ok = sunk == expectSink && (!sunk || t <= maxSinkTime || sh.Sub);
        if (!ok) failures++;
        Console.WriteLine((ok ? "  ok   " : "  FAIL ") + sh.Name.PadRight(58) + result);
    }

    static void Check(string what, bool cond)
    {
        if (!cond) failures++;
        Console.WriteLine((cond ? "  ok   " : "  FAIL ") + what);
    }

    static int Main()
    {
        Console.WriteLine("Unit checks");
        Check("intact part has zero severity", FloodModel.Severity(1, 1, 0.35f) == 0f);
        Check("destroyed part has full severity", FloodModel.Severity(0, 1, 0.35f) == 1f);
        Check("armour-only loss is capped by armorWeight", Math.Abs(FloodModel.Severity(1, 0, 0.35f) - 0.35f) < 1e-6);
        Check("no flooding below breach threshold", FloodModel.TargetWater(100, 1, 0.05, Bt, Ct, Overfill) == 0);
        Check("no flooding when out of the water", FloodModel.TargetWater(100, 0, 0.9, Bt, Ct, Overfill) == 0);
        Check("critical compartment floods to waterline", Math.Abs(FloodModel.TargetWater(100, 0.4, Ct, Bt, Ct, Overfill) - 40) < 1e-9);
        Check("destroyed compartment overfills by criticalOverfill", Math.Abs(FloodModel.TargetWater(100, 0.4, 1.0, Bt, Ct, Overfill) - 60) < 1e-9);
        Check("depth factor clamped at depth", FloodModel.DepthFactor(200, RefDepth, MinDF, MaxDF) == MaxDF);
        Check("depth factor floor at surface", FloodModel.DepthFactor(-3, RefDepth, MinDF, MaxDF) == MinDF);
        Check("step never overshoots target", FloodModel.Step(0, 5, 1000, 1, Drain) == 5);

        Console.WriteLine();
        Console.WriteLine("Destroyer: 5 x 400 t compartments, 800 t dry (40% immersion)");
        Run(Make("1 compartment @ 80% damage", 5, 400, 800, new[] { 0.8 }), 900, false);
        Run(Make("2 compartments @ 80% damage", 5, 400, 800, new[] { 0.8, 0.8 }), 900, false);
        Run(Make("3 compartments @ 80% damage", 5, 400, 800, new[] { 0.8, 0.8, 0.8 }), 900, true, 600);
        Run(Make("all compartments @ 30% damage (spread gunfire)", 5, 400, 800, new[] { 0.3, 0.3, 0.3, 0.3, 0.3 }), 900, false);
        Run(Make("all compartments @ 50% damage", 5, 400, 800, new[] { 0.5, 0.5, 0.5, 0.5, 0.5 }), 900, true, 900);
        Run(Make("2 torpedo hits: 2 compartments @ 100%", 5, 400, 800, new[] { 1.0, 1.0 }), 900, false);
        Run(Make("2 torpedo hits + gunfire (2 @ 100%, 3 @ 40%)", 5, 400, 800, new[] { 1.0, 1.0, 0.4, 0.4, 0.4 }), 900, true, 600);

        Console.WriteLine();
        Console.WriteLine("Single-part ship (e.g. MNWS 055A, subdivision = 2): 1 x 30000 t, 9000 t dry (30% immersion)");
        Run(Make("20% damage", 1, 30000, 9000, new[] { 0.2 }, false, 0, 2), 1800, false);
        Run(Make("35% damage", 1, 30000, 9000, new[] { 0.35 }, false, 0, 2), 1800, false);
        Run(Make("50% damage", 1, 30000, 9000, new[] { 0.5 }, false, 0, 2), 1800, false);
        Run(Make("60% damage", 1, 30000, 9000, new[] { 0.6 }, false, 0, 2), 1800, true, 900);
        Run(Make("90% damage", 1, 30000, 9000, new[] { 0.9 }, false, 0, 2), 1800, true, 300);

        Console.WriteLine();
        Console.WriteLine("Submarine held at 75 m: 5 x 200 t compartments, neutrally trimmed");
        Run(Make("1 compartment @ 20% damage", 5, 200, 1000, new[] { 0.2 }, true, 75), 600, false);
        Run(Make("1 compartment @ 40% damage", 5, 200, 1000, new[] { 0.4 }, true, 75), 600, true);
        Run(Make("1 compartment @ 90% damage (torpedo)", 5, 200, 1000, new[] { 0.9 }, true, 75), 600, true);
        Run(Make("all compartments @ 15% damage (light gunfire)", 5, 200, 1000, new[] { 0.15, 0.15, 0.15, 0.15, 0.15 }, true, 75), 600, false);

        Console.WriteLine();
        Console.WriteLine("Single-part submarine (e.g. MNWS SSBN002, subdivision = 2) held at 75 m: 1 x 1000 t, neutral");
        Run(Make("15% damage", 1, 1000, 1000, new[] { 0.15 }, true, 75, 2), 600, false);
        Run(Make("20% damage", 1, 1000, 1000, new[] { 0.2 }, true, 75, 2), 600, false);
        Run(Make("35% damage", 1, 1000, 1000, new[] { 0.35 }, true, 75, 2), 600, true);
        Run(Make("60% damage", 1, 1000, 1000, new[] { 0.6 }, true, 75, 2), 600, true);

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "All checks passed." : failures + " check(s) failed.");
        return failures == 0 ? 0 : 1;
    }
}
