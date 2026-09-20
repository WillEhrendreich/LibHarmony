// Usage: harmony-probe <expectedRuntimeMajor>. Exit 0 = ran on that runtime major AND a Harmony prefix patch
// changed the result and unpatching restored it. ci-pipeline.fsx runs it on .NET 10 and on the newest .NET 11.
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HarmonyLib;

Console.WriteLine($"runtime: {RuntimeInformation.FrameworkDescription}");
if (args is not [var expected] || Environment.Version.Major.ToString() != expected)
{
    Console.Error.WriteLine($"expected to run on .NET {args.FirstOrDefault() ?? "?"} but this is .NET {Environment.Version.Major}");
    return 3;
}

var harmony = new Harmony("harmony-probe");
harmony.Patch(
    typeof(Probe).GetMethod(nameof(Probe.Foo))!,
    prefix: new HarmonyMethod(typeof(Probe).GetMethod(nameof(Probe.Prefix))));
var patched = Probe.Foo();
Console.WriteLine($"patched   Foo() = {patched} (expect 99)");
if (patched != 99) return 1;

harmony.UnpatchAll("harmony-probe");
var unpatched = Probe.Foo();
Console.WriteLine($"unpatched Foo() = {unpatched} (expect 1)");
return unpatched == 1 ? 0 : 2;

public static class Probe
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Foo() => 1;

    public static bool Prefix(ref int __result)
    {
        __result = 99;
        return false;
    }
}
