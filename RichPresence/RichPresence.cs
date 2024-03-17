﻿using DiscordRPC;
using DiscordRPC.Logging;
using ECGen.Generated;
using ECGen.Generated.Command;
using ECGen.Generated.Command.Battle;
using ECGen.Generated.Command.Dungeon;
using ECGen.Generated.Command.OutGame.MultiBattle;
using ECGen.Generated.Command.UI;
using ECGen.Generated.Command.Work;
using ImGuiNET;
using Materia.Attributes;
using Materia.Game;
using Materia.Plugin;
using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using BattleSystem = Materia.Game.BattleSystem;
using DungeonSystem = Materia.Game.DungeonSystem;
using ScreenManager = Materia.Game.ScreenManager;
using WorkManager = Materia.Game.WorkManager;

namespace RichPresence;

[Injection]
public unsafe class RichPresence : IMateriaPlugin
{
    public string Name => "Rich Presence";
    public string Description => "Adds Discord Rich Presence support";
    public static PluginServiceManager PluginServiceManager { get; private set; } = null!;
    public static Configuration Config { get; } = PluginConfiguration.Load<Configuration>();
    private static DiscordRpcClient? client;

    private static readonly DiscordRPC.RichPresence defaultPresence = new()
    {
        Details = "Title Screen",
        State = "Zzz...",
        Assets = new() { LargeImageKey = "default" }
    };

    private bool draw = false;

    public RichPresence(PluginServiceManager pluginServiceManager)
    {
        pluginServiceManager.EventHandler.Update += Update;
        pluginServiceManager.EventHandler.Draw += Draw;
        pluginServiceManager.EventHandler.ToggleMenu = () => draw ^= true;
        pluginServiceManager.EventHandler.Dispose += Dispose;

        PluginServiceManager = pluginServiceManager;
        if (Config.EnableRichPresence)
            CreateClient();
    }

    public static void CreateClient()
    {
        client = new DiscordRpcClient("1218450701420855317", -1, new ConsoleLogger { Level = LogLevel.Warning }, false)
        {
            SkipIdenticalPresence = true
        };

        client.OnReady += (sender, e) => PluginServiceManager.Log.Info($"Discord Username: {e.User.Username}");
        client.OnJoin += (sender, e) =>
        {
            PluginServiceManager.Log.Info($"Joined Game: {e.Secret}");
            ImGui.SetClipboardText(e.Secret);
        };

        client.Initialize();
        client.RegisterUriScheme("2484110");
        client.SetPresence(defaultPresence);
    }

    public void Update()
    {
        if (client == null) return;

        var sceneBehaviourManager = GetSingletonMonoBehaviourInstance<SceneBehaviourManager>();
        if (sceneBehaviourManager == null) return;

        var currentScene = sceneBehaviourManager->currentSceneBehaviour->value;
        var presence = new DiscordRPC.RichPresence();
        if (DungeonSystem.Instance is { } dungeonSystem)
        {
            switch (dungeonSystem.NativePtr->dungeonType)
            {
                case DungeonType.Crisis:
                case DungeonType.ScoreDungeon:
                case DungeonType.Another:
                    if (!Il2CppType<DungeonWork.AnotherDungeonStore>.Is(dungeonSystem.NativePtr->anotherDungeonInfo, out var dungeonStore)) break;

                    var dungeonLanguageId = dungeonStore->masterDungeon->nameLanguageId;
                    var areaLanguageId = dungeonStore->masterAnotherArea->nameLanguageId;
                    if (dungeonLanguageId == 0 || areaLanguageId == 0) break;

                    presence.Details = $"{getLocalizedText(LocalizeTextCategory.AnotherArea, areaLanguageId)->ToString()} {getLocalizedText(LocalizeTextCategory.Dungeon, dungeonLanguageId)->ToString()}";
                    presence.State = dungeonSystem.IsBattling ? "In Battle" : "Wandering Around";
                    presence.Assets = new Assets { LargeImageKey = "dungeon" };
                    break;
                case DungeonType.Chocobo:
                    presence.Details = "Chocobo Farm";
                    presence.Assets = new Assets { LargeImageKey = "chocobo" };
                    break;
                case DungeonType.Story:
                case DungeonType.EventStory:
                case DungeonType.CharacterStory:
                    if (!Il2CppType<DungeonWork.StoryDungeonStore>.Is(dungeonSystem.NativePtr->storyDungeonInfo, out var storyDungeonStore)) break;
                    presence.Details = "In Cutscene";
                    presence.State = getLocalizedText(LocalizeTextCategory.Dungeon, storyDungeonStore->masterDungeon->nameLanguageId)->ToString();
                    presence.Assets = new Assets { LargeImageKey = "story" };
                    break;
            }
        }
        else if (BattleSystem.Instance is { IsBattling: true } battleSystem && Il2CppType<BattleSceneBehaviour>.Is(currentScene, out var battleSceneBehaviour))
        {
            var battlePlayer = battleSceneBehaviour->battlePlayer;
            var wave = battleSystem.NativePtr->waveModel->index->GetValue();
            if (battlePlayer != null && battlePlayer->setupParameter != null && battlePlayer->setupParameter->battleName != null && wave >= 0)
            {
                var battleName = battlePlayer->setupParameter->battleName->ToString();
                presence.Details = battleSystem.IsMultiplayer ? $"Co-op: {battleName}" : battleName;
                presence.State = battleSystem.IsBattleWon ? "Viewing Results" : $"Wave {wave + 1}";
                presence.Assets = new Assets { LargeImageKey = "battle", SmallImageKey = battleSystem.IsMultiplayer ? "multi" : "solo" };
            }
        }
        else if (ScreenManager.Instance?.CurrentScreen is { } screen)
        {
            if (Il2CppType<MultiAreaBattleMatchingRoomScreenPresenter>.Is(screen.NativePtr, out var room))
            {
                presence.Details = "In Multiplayer";
                presence.State = "Waiting...";
                presence.Assets = new Assets { LargeImageKey = "party" };

                var multiBattleStore = Il2CppType<AreaBattleWork.MultiAreaBattleStore>.As(room->param->multiAreaBattleInfo);
                var eventStore = Il2CppType<EventWork.EventMultiBattleStore>.As(room->param->eventMultiAreaBattleInfo);
                if (multiBattleStore != null)
                {
                    var multiAreaStore = WorkManager.GetMultiAreaStore(multiBattleStore->masterMultiAreaBattle->multiAreaId);
                    presence.State = multiAreaStore != null
                        ? $"{getLocalizedText(LocalizeTextCategory.MultiArea, multiAreaStore->masterMultiArea->nameLanguageId)->ToString()} {getLocalizedText(LocalizeTextCategory.MultiAreaBattle, multiBattleStore->masterMultiAreaBattle->nameLanguageId)->ToString()}"
                        : getLocalizedText(LocalizeTextCategory.MultiAreaBattle, multiBattleStore->masterMultiAreaBattle->nameLanguageId)->ToString();
                }
                else if (eventStore != null)
                {
                    presence.State = getLocalizedText(LocalizeTextCategory.EventMultiBattle, eventStore->masterEventMultiBattle->nameLanguageId)->ToString();
                }

                if (Config.EnableMultiplayerInvites && room->param->privateRoomNumber > 0)
                {
                    var id = room->param->privateRoomNumber.ToString();
                    presence.Party = new DiscordRPC.Party { ID = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(id))), Size = room->userInfos->PtrEnumerable.Count(p => p.ptr->isActive), Max = 3 };
                    presence.Secrets = new Secrets { JoinSecret = id };
                }
            }
            else
            {
                presence.Details = "Main Menu";
                presence.Assets = new Assets { LargeImageKey = "default" };
            }
        }

        if (Config.EnableOnlyInLobby && !presence.HasSecrets())
        {
            client.ClearPresence();
            return;
        }

        if (string.IsNullOrEmpty(presence.Details)) return;

        if (!Config.EnableDetailedInfo)
        {
            presence.Details = "In Game";
            presence.State = "";
            presence.Assets = new Assets { LargeImageKey = "default" };
        }

        client.SetPresence(presence);
        client.Invoke();
    }

    public void Draw()
    {
        if (!draw) return;

        ImGui.SetNextWindowSizeConstraints(new Vector2(250, 125) * ImGuiEx.Scale, new Vector2(10000));
        ImGui.Begin("Rich Presence", ref draw);

        var b = Config.EnableRichPresence;
        if (ImGui.Checkbox("Enable Rich Presence", ref b))
        {
            Config.EnableRichPresence = b;
            if (b)
            {
                CreateClient();
            }
            else
            {
                client?.Dispose();
                client = null;
            }
            Config.Save();
        }

        b = Config.EnableDetailedInfo;
        if (ImGui.Checkbox("Enable Detailed Info", ref b))
        {
            Config.EnableDetailedInfo = b;
            Config.Save();
        }

        b = Config.EnableMultiplayerInvites;
        if (ImGui.Checkbox("Enable Multiplayer Invites", ref b))
        {
            Config.EnableMultiplayerInvites = b;
            Config.Save();
        }

        b = Config.EnableOnlyInLobby;
        if (ImGui.Checkbox("Enable Only In Private Lobby", ref b))
        {
            Config.EnableOnlyInLobby = b;
            Config.Save();
        }

        ImGui.End();
    }

    public void Dispose()
    {
        Config?.Save();
        client?.Dispose();
    }

    [GameSymbol("SingletonMonoBehaviour<object>$$get_Instance", Required = true)]
    private static delegate* unmanaged<nint, nint> singletonMonoBehaviourGetInstance;
    public static nint GetSingletonMonoBehaviourInstance(string name, int symbolIndex = 0)
    {
        if (!GameData.TryGetSymbolAddress(symbolIndex > 0 ? $"Method$SingletonMonoBehaviour<{name}>.get_Instance()_{symbolIndex}" : $"Method$SingletonMonoBehaviour<{name}>.get_Instance()", out var address)) return nint.Zero;
        address = *(nint*)address;
        return (nuint)address <= uint.MaxValue ? nint.Zero : singletonMonoBehaviourGetInstance(address);
    }

    public static T* GetSingletonMonoBehaviourInstance<T>(string name, int symbolIndex = 0) where T : unmanaged => (T*)GetSingletonMonoBehaviourInstance(name, symbolIndex);
    public static T* GetSingletonMonoBehaviourInstance<T>(int symbolIndex = 0) where T : unmanaged => (T*)GetSingletonMonoBehaviourInstance(typeof(T).Name, symbolIndex);

    [Signature("E9 ?? ?? ?? ?? 89 4C 24 30", Required = true)]
    private static delegate* unmanaged<LocalizeTextCategory, long, Unmanaged_String*> getLocalizedText;
}