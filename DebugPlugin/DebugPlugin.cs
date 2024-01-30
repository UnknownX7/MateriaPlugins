using ImGuiNET;
using Materia.Attributes;
using Materia.Plugin;

namespace DebugPlugin;

public class Configuration : PluginConfiguration
{
}

[Injection]
public unsafe class DebugPlugin : IMateriaPlugin
{
    public string Name => "Debug Plugin";
    public string Description => "Testing";
    public static PluginServiceManager PluginServiceManager { get; private set; } = null!;
    public static Configuration Config { get; } = PluginConfiguration.Load<Configuration>();

    private bool draw = true;

    public DebugPlugin(PluginServiceManager pluginServiceManager)
    {
        pluginServiceManager.EventHandler.Update += Update;
        pluginServiceManager.EventHandler.Draw += Draw;
        pluginServiceManager.EventHandler.ToggleMenu = () => draw ^= true;
        pluginServiceManager.EventHandler.Dispose += Dispose;

        PluginServiceManager = pluginServiceManager;
    }

    public void Update()
    {

    }

    public void Draw()
    {
        if (!draw) return;

        ImGui.Begin("Debug", ref draw);
        ImGui.End();
    }

    public void Dispose()
    {
        Config?.Save();
    }
}