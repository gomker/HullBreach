using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("HullBreach")]
[assembly: AssemblyDescription("Damage-driven flooding and sinking for ships in KSP, using BDArmory hitpoints and armour")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Lane Brothers LLC")]
[assembly: AssemblyProduct("HullBreach")]
[assembly: AssemblyCopyright("Original work by gomker (github.com/gomker/HullBreach), CC BY-NC-SA 3.0. KSP 1.12 / BDArmory Plus 1.13 port: modified work, same license.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("9146805b-7081-493a-bde9-0eb5cd6eef70")]

[assembly: AssemblyVersion("1.8.0.0")]
[assembly: AssemblyFileVersion("1.8.0.0")]

[assembly: KSPAssembly("HullBreach", 1, 8)]
// BDArmory Plus declares KSPAssembly("BDArmory", 1, 0). This makes KSP load BDArmory first and
// skip HullBreach (with a log line) instead of throwing type-load errors if BDArmory is missing.
[assembly: KSPAssemblyDependency("BDArmory", 1, 0)]
