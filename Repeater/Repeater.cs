using ImGuiNET;
using Materia;
using Materia.Game;
using Materia.Plugin;
using System.Diagnostics;

namespace Repeater;

public unsafe class Repeater : IMateriaPlugin
{
    public string Name => "Repeater";
    public string Description => "Repeats battles for you";

    private bool draw = false;
    private readonly Stopwatch repeatStopwatch = new();

    public Repeater(PluginServiceManager pluginServiceManager)
    {
        pluginServiceManager.EventHandler.Update += Update;
        pluginServiceManager.EventHandler.Draw += Draw;
        pluginServiceManager.EventHandler.ToggleMenu = () => draw ^= true;
    }

    public void Update()
    {
        if (repeatStopwatch is not { IsRunning: true, ElapsedMilliseconds: > 500 }) return;

        if (GameInterop.IsDefeated)
        {
            repeatStopwatch.Reset();
            return;
        }

        if (GameInterop.IsBattleWon)
            GameInterop.SendKey(VirtualKey.VK_RETURN);

        repeatStopwatch.Restart();
    }

    public void Draw()
    {
        if (!draw)
        {
            if (repeatStopwatch.IsRunning)
                repeatStopwatch.Reset();
            return;
        }

        ImGui.Begin("Repeater", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);
        var _ = repeatStopwatch.IsRunning;
        if (ImGui.Checkbox("##Auto", ref _))
        {
            if (_)
                repeatStopwatch.Start();
            else
                repeatStopwatch.Reset();
        }
        ImGui.End();
    }
}