using System.IO;
using ECGen.Generated;
using ECGen.Generated.Command;
using ECGen.Generated.Command.KeyInput;
using ECGen.Generated.Command.Loading;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.Save;
using ECGen.Generated.Command.UserPreferences;
using ImGuiNET;
using Materia.Attributes;
using Materia.Game;
using Materia.Plugin;

namespace AccountSwap;

[Injection]
public unsafe class AccountSwap : IMateriaPlugin
{
    public string Name => "Account Swap";
    public string Description => "Makes swapping accounts a breeze";
    public static Configuration Config { get; } = PluginConfiguration.Load<Configuration>();
    public static UserInformationSaveParameter* UserInformation => ((UserInfosSystem.StaticFields*)Il2CppType<UserInfosSystem>.NativePtr->staticFields)->userInformation;

    private bool draw = false;
    private bool wasTitleScreen = false;
    private bool wasConnecting = false;

    public AccountSwap(PluginServiceManager pluginServiceManager)
    {
        pluginServiceManager.EventHandler.Draw += Draw;
        pluginServiceManager.EventHandler.ToggleMenu = () => draw ^= true;
    }

    public void Draw()
    {
        var title = Materia.Game.SceneBehaviourManager.GetCurrentSceneBehaviour<TitleSceneBehaviour>();
        var isTitleScreen = title != null;

        if (isTitleScreen)
        {
            if (wasConnecting || GameInterop.GetSharedMonoBehaviourInstance<LoadingUiManager>()->connectingPresenter->visibility->value)
            {
                wasConnecting = true;
                return;
            }

            wasTitleScreen = true;
            draw = false;

            if (Config.AccountInfos.Count < 2) return;

            ImGui.Begin("Accounts", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse);

            var delete = 0L;
            foreach (var (userId, accountInfo) in Config.AccountInfos)
            {
                ImGui.Selectable(accountInfo.ToString(), UserInformation->userId == userId);

                if (ImGuiEx.IsItemDoubleClicked())
                {
                    SetAccount(accountInfo);
                    GameInterop.TapKeyAction(KeyAction.Confirm);
                }

                if (!ImGui.BeginPopupContextItem()) continue;

                ImGui.Selectable("Right click to delete");
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                    delete = userId;

                ImGui.EndPopup();
            }

            if (delete != 0)
            {
                Config.AccountInfos.Remove(delete);
                Config.Save();
            }

            ImGui.End();
        }
        else if (wasTitleScreen && Materia.Game.SceneBehaviourManager.IsCurrentSceneBehaviour<OutGameSceneBehaviour>())
        {
            wasTitleScreen = false;
            wasConnecting = false;

            var user = UserInformation;
            var userName = WorkManager.NativePtr->user->userProfileStore->userProfile->name->ToString();

            var save = false;
            if (!Config.AccountInfos.TryGetValue(user->userId, out var accountInfo))
            {
                save = true;
                accountInfo = new Configuration.AccountInfo
                {
                    UserName = userName,
                    UserId = user->userId,
                    DisplayUserId = user->displayUserId->ToString(),
                    BattleUserId = user->battleUserId,
                    DeviceUuid = user->deviceUuid->ToString(),
                    LoginToken = user->loginToken->ToString()
                };

                BackupAccountInfo(accountInfo.DisplayUserId);
            }
            else if (accountInfo.UserName != userName)
            {
                save = true;
                accountInfo.UserName = userName;
            }

            if (save)
            {
                Config.AccountInfos[user->userId] = accountInfo;
                Config.Save();
            }
        }
        else if (draw && Materia.Game.SceneBehaviourManager.IsCurrentSceneBehaviour<OutGameSceneBehaviour>())
        {
            ImGui.Begin("###AccountSwap", ref draw, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize);
            if (ImGui.Button("Go to Title"))
            {
                resetApplication(0);
                draw = false;
            }
            ImGui.End();
        }
    }

    private static void BackupAccountInfo(string displayUserId)
    {
        var gameSaveDir = new DirectoryInfo(((SaveManager.StaticFields*)Il2CppType<SaveManager>.NativePtr->staticFields)->SaveDirectory->ToString());
        if (gameSaveDir.Name != "GameSave") return;

        const string accountInfoFileName = "754e2df433cbec9c453541431f5646f040cc2b661c69efd88f3f2eea5621dbb9.bytes";
        var accountInfoFile = new FileInfo(Path.Combine(gameSaveDir.FullName, accountInfoFileName));
        if (!accountInfoFile.Exists) return;

        var accountDir = gameSaveDir.Parent!.CreateSubdirectory("Accounts").CreateSubdirectory(displayUserId);
        accountInfoFile.CopyTo(Path.Combine(accountDir.FullName, accountInfoFileName), true);
    }

    private static void SetAccount(Configuration.AccountInfo accountInfo) =>
        SetAccount(accountInfo.UserId, accountInfo.DisplayUserId ?? string.Empty, accountInfo.BattleUserId, accountInfo.DeviceUuid ?? string.Empty, accountInfo.LoginToken ?? string.Empty);

    private static void SetAccount(long userId, string displayUserId, long battleUserId, string deviceUuid, string loginToken) =>
        setUserInfoSystem(userId, GameInterop.CreateString(displayUserId), battleUserId, GameInterop.CreateString(deviceUuid), GameInterop.CreateString(loginToken), 0);

    [GameSymbol("Command.SystemOperator$$ResetApplication")]
    private static delegate* unmanaged<nint, void> resetApplication;
    [GameSymbol("Command.SystemOperator$$SetUserInfoSystem")]
    private static delegate* unmanaged<long, Unmanaged_String*, long, Unmanaged_String*, Unmanaged_String*, nint, void> setUserInfoSystem;
}