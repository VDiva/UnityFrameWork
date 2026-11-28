using System;
using Cysharp.Threading.Tasks;

namespace FrameWork.Net.Network
{
    /// <summary>
    /// Steam 大厅与身份认证服务预留接口。
    /// 后续接入 Steamworks.NET / Facepunch.Steamworks 时实现此接口并注册到 <see cref="SteamNetworkBridge"/>，
    /// 即可在 Steam P2P 房主联机或 Steam Relay + 自建服模式之间无缝切换。
    /// </summary>
    public interface ISteamLobbyService
    {
        bool IsInitialized { get; }
        ulong LocalSteamId { get; }
        string LocalPersonaName { get; }
        ulong CurrentLobbyId { get; }

        /// <summary>好友通过 Steam 覆盖层点击“加入游戏”时触发，参数为目标大厅 ID。</summary>
        event Action<ulong> OnLobbyJoinRequested;

        /// <summary>创建 Steam 大厅并返回 LobbyId。</summary>
        UniTask<ulong> CreateLobbyAsync(int maxPlayers, bool friendsOnly = false);

        /// <summary>加入指定 Steam 大厅并返回房主 SteamId（可用于 GameNet.ConnectSteam）。</summary>
        UniTask<ulong> JoinLobbyAsync(ulong lobbyId);

        /// <summary>离开当前 Steam 大厅。</summary>
        void LeaveLobby();

        /// <summary>获取 Steam Auth Session Ticket（Hex/Base64），用于向自建服校验 Steam 账号身份。</summary>
        UniTask<string> GetAuthSessionTicketAsync();
    }

    /// <summary>Steam 网络服务全局桥接入口（未接入 Steam SDK 时为 null，不影响现有框架运行）。</summary>
    public static class SteamNetworkBridge
    {
        public static ISteamLobbyService LobbyService { get; private set; }

        public static bool IsAvailable => LobbyService != null && LobbyService.IsInitialized;

        public static void RegisterLobbyService(ISteamLobbyService service)
        {
            LobbyService = service;
        }
    }
}
