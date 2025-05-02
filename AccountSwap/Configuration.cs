using System.Collections.Generic;
using Materia.Plugin;

namespace AccountSwap;

public class Configuration : PluginConfiguration
{
    public class AccountInfo
    {
        public string? UserName { get; set; }
        public long UserId { get; set; }
        public string? DisplayUserId { get; set; }
        public long BattleUserId { get; set; }
        public string? DeviceUuid { get; set; }
        public string? LoginToken { get; set; }
        public override string ToString() => !string.IsNullOrEmpty(UserName) ? $"{DisplayUserId}: {UserName}" : DisplayUserId ?? string.Empty;
    }

    public Dictionary<long, AccountInfo> AccountInfos { get; set; } = [];
}