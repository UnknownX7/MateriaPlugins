using System;
using ImGuiNET;
using Materia.Game;
using Materia.Plugin;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Materia.Utilities;

namespace Infomania;

public abstract class Info
{
    public abstract bool Enabled { get; }
    public bool LastFrameActive { get; set; }
    public virtual void Activate() { }
    public virtual void Update() { }
    public virtual void Dispose() { }
}

public abstract class ScreenInfo : Info
{
    public abstract Type[] ValidScreens { get; }
    public virtual bool ShowOnModal => false;
    public abstract void Draw(Screen screen);
}

public abstract class ModalInfo : Info
{
    public abstract Type[] ValidModals { get; }
    public abstract void Draw(Modal modal);
}

public unsafe class Infomania : IMateriaPlugin
{
    public string Name => "Infomania";
    public string Description => "Displays extra information on certain menus";
    public static Configuration Config { get; } = PluginConfiguration.Load<Configuration>();
    private readonly List<ScreenInfo> screenInfos = Assembly.GetExecutingAssembly().GetTypes<ScreenInfo>().Select(t => (ScreenInfo?)Activator.CreateInstance(t)).Where(i => i != null).ToList()!;
    private readonly List<ModalInfo> modalInfos = Assembly.GetExecutingAssembly().GetTypes<ModalInfo>().Select(t => (ModalInfo?)Activator.CreateInstance(t)).Where(i => i != null).ToList()!;

    private bool draw = false;

    public Infomania(PluginServiceManager pluginServiceManager)
    {
        pluginServiceManager.EventHandler.Update += Update;
        pluginServiceManager.EventHandler.Draw += Draw;
        pluginServiceManager.EventHandler.ToggleMenu = () => draw ^= true;
        pluginServiceManager.EventHandler.Dispose += Dispose;
    }

    public void Update()
    {
        var currentModal = ModalManager.Instance?.CurrentModal;
        var isModalActive = currentModal != null;
        if (isModalActive)
        {
            var modalName = currentModal!.Type.Name;
            foreach (var modalInfo in modalInfos)
            {
                if (!modalInfo.Enabled || modalInfo.ValidModals.All(t => t.Name != modalName))
                {
                    modalInfo.LastFrameActive = false;
                    continue;
                }

                if (!modalInfo.LastFrameActive)
                {
                    modalInfo.Activate();
                    modalInfo.LastFrameActive = true;
                }

                modalInfo.Update();
            }
        }

        var screenName = ScreenManager.Instance?.CurrentScreen?.Type.Name;
        if (screenName == null) return;

        foreach (var screenInfo in screenInfos)
        {
            if (!screenInfo.Enabled || screenInfo.ValidScreens.All(t => t.Name != screenName))
            {
                screenInfo.LastFrameActive = false;
                continue;
            }

            if (!screenInfo.LastFrameActive)
            {
                screenInfo.Activate();
                screenInfo.LastFrameActive = true;
            }

            screenInfo.Update();
        }
    }

    public void Draw()
    {
        var isModalActive = false;
        if (ModalManager.Instance?.CurrentModal is { } modal)
        {
            isModalActive = true;
            foreach (var modalInfo in modalInfos.Where(modalInfo => modalInfo.LastFrameActive))
                modalInfo.Draw(modal);
        }

        if (ScreenManager.Instance?.CurrentScreen is { } screen)
        {
            foreach (var screenInfo in screenInfos.Where(screenInfo => screenInfo.LastFrameActive && (!isModalActive || screenInfo.ShowOnModal)))
                screenInfo.Draw(screen);
        }

        if (draw)
            DrawSettings();
    }

    private void DrawSettings()
    {
        ImGui.SetNextWindowSizeConstraints(new Vector2(250, 200) * ImGuiEx.Scale, new Vector2(10000));
        ImGui.Begin("Infomania", ref draw);

        var b = Config.EnableHomeInfo;
        if (ImGui.Checkbox("Enable Home Info", ref b))
        {
            Config.EnableHomeInfo = b;
            Config.Save();
        }

        b = Config.EnablePartySelectInfo;
        if (ImGui.Checkbox("Enable Party Select Stats", ref b))
        {
            Config.EnablePartySelectInfo = b;
            Config.Save();
        }

        b = Config.EnablePartyEditInfo;
        if (ImGui.Checkbox("Enable Party Edit Stats", ref b))
        {
            Config.EnablePartyEditInfo = b;
            Config.Save();
        }

        b = Config.EnableGiftInfo;
        if (ImGui.Checkbox("Enable Gift Count", ref b))
        {
            Config.EnableGiftInfo = b;
            Config.Save();
        }

        b = Config.EnableBossDetailInfo;
        if (ImGui.Checkbox("Enable Enemy Detail Stats", ref b))
        {
            Config.EnableBossDetailInfo = b;
            Config.Save();
        }

        ImGui.End();
    }

    public void Dispose()
    {
        Config?.Save();
        foreach (var modalInfo in modalInfos)
            modalInfo.Dispose();
        foreach (var screenInfo in screenInfos)
            screenInfo.Dispose();
    }
}