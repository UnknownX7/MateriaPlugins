using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Materia.Plugin;
using Materia.Utilities;

namespace MicroMacros;

public interface IMicroMacro
{
    public ref bool Enabled { get; }
    public void Update();
    public void Draw();
}

public class MicroMacros : IMateriaPlugin
{
    public string Name => "Micro Macros";
    public string Description => "Automates simple tasks";
    private readonly List<IMicroMacro> macros = Assembly.GetExecutingAssembly().GetTypes<IMicroMacro>().Select(t => (IMicroMacro?)Activator.CreateInstance(t)).Where(i => i != null).ToList()!;

    public MicroMacros(PluginServiceManager pluginServiceManager)
    {
        pluginServiceManager.EventHandler.Update += Update;
        pluginServiceManager.EventHandler.Draw += Draw;
    }

    public void Update()
    {
        foreach (var microMacro in macros.Where(microMacro => microMacro.Enabled))
            microMacro.Update();
    }

    public void Draw()
    {
        foreach (var microMacro in macros)
            microMacro.Draw();
    }
}