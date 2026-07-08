using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenAEC.Sheets.Revit.Helpers;

/// <summary>
/// Laadt dependencies (CommunityToolkit.Mvvm, OpenAEC.Sheets.Core/UI, ...)
/// uit de eigen addin-map, zodat Revit ze kan vinden.
/// </summary>
internal static class AssemblyResolver
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        try
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnResolve;
        }
        catch (Exception ex)
        {
            PluginLogger.LogException(ex, "ModuleInitializer");
        }
    }

    private static Assembly? OnResolve(object? sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name).Name;
        if (assemblyName is null) return null;

        var addinDir = Path.GetDirectoryName(typeof(AssemblyResolver).Assembly.Location);
        if (addinDir is null) return null;

        var assemblyPath = Path.Combine(addinDir, $"{assemblyName}.dll");
        if (!File.Exists(assemblyPath)) return null;

        return Assembly.LoadFrom(assemblyPath);
    }
}
