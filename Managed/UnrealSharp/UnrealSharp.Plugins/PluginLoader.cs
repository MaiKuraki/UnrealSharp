﻿using System.Reflection;
using System.Runtime.Loader;
using UnrealSharp.Binds;
using UnrealSharp.Core;

namespace UnrealSharp.Plugins;

public static class PluginLoader
{
    public static readonly List<Assembly> SharedAssemblies = [];
    private static readonly List<Plugin> _loadedPlugins = [];
    public static IReadOnlyList<Plugin> LoadedPlugins => _loadedPlugins;
    
    static PluginLoader()
    {
        SharedAssemblies.Add(Assembly.GetExecutingAssembly());
        SharedAssemblies.Add(typeof(NativeBinds).Assembly);
        SharedAssemblies.Add(typeof(UnrealSharpObject).Assembly);
    }

    public static Assembly? LoadPlugin(string assemblyPath, bool isCollectible)
    {
        try
        {
            AssemblyName assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(assemblyPath));

            foreach (var loadedPlugin in _loadedPlugins)
            {
                if (!loadedPlugin.IsAssemblyAlive)
                {
                    continue;
                }
                
                if (loadedPlugin.WeakRefAssembly?.Target is not Assembly assembly)
                {
                    continue;
                }

                if (assembly.GetName() != assemblyName)
                {
                    continue;
                }

                LogUnrealSharpPlugins.Log($"Plugin {assemblyName} is already loaded.");
                return assembly;
            }

            PluginLoadContext pluginLoadContext = new PluginLoadContext(new AssemblyDependencyResolver(assemblyPath), isCollectible);
            Plugin plugin = new Plugin(assemblyName, pluginLoadContext, assemblyPath);
  
            if (plugin.Load() && plugin.WeakRefAssembly != null && plugin.WeakRefAssembly.Target is Assembly loadedAssembly)
            {
                _loadedPlugins.Add(plugin);
                LogUnrealSharpPlugins.Log($"Successfully loaded plugin: {assemblyName}");
                return loadedAssembly;
            }

            LogUnrealSharpPlugins.LogError($"Failed to load plugin: {assemblyName}");
            return null;
        }
        catch (Exception ex)
        {
            LogUnrealSharpPlugins.LogError($"An error occurred while loading the plugin: {ex.Message}");
        }

        return null;
    }

    public static bool UnloadPlugin(Plugin pluginToUnload)
    {
        try
        {
            LogUnrealSharpPlugins.Log($"Unloading plugin {pluginToUnload.AssemblyName}...");
            pluginToUnload.Unload();

            int startTimeMs = Environment.TickCount;
            bool takingTooLong = false;

            while (pluginToUnload.IsAssemblyAlive && pluginToUnload.IsLoadContextAlive)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();

                if (!pluginToUnload.IsAssemblyAlive && !pluginToUnload.IsLoadContextAlive)
                {
                    break;
                }

                int elapsedTimeMs = Environment.TickCount - startTimeMs;

                if (!takingTooLong && elapsedTimeMs >= 200)
                {
                    takingTooLong = true;
                    LogUnrealSharpPlugins.LogError("Unloading assembly is taking longer than expected...");
                }
                else if (elapsedTimeMs >= 1000)
                {
                    throw new InvalidOperationException("Failed to unload assemblies. Possible causes: Strong GC handles, running threads, etc.");
                }
            }

            _loadedPlugins.Remove(pluginToUnload);

            LogUnrealSharpPlugins.Log($"{pluginToUnload.AssemblyName} unloaded successfully!");
            return true;
        }
        catch (Exception e)
        {
            LogUnrealSharpPlugins.LogError($"An error occurred while unloading the plugin: {e.Message}");
            return false;
        }
    }
    
    public static Plugin? FindPluginByName(string assemblyName)
    {
        foreach (Plugin loadedPlugin in _loadedPlugins)
        {
            if (loadedPlugin.AssemblyName.Name == assemblyName)
            {
                return loadedPlugin;
            }
        }

        return null;
    }
}
