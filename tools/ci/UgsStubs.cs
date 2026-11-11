// Stubs mínimos (só as assinaturas usadas pelo Craquez.Online) dos pacotes Unity Gaming Services e
// Unity Transport, para a checagem sem licença compilar o módulo on-line. As assinaturas seguem
// com.unity.services.multiplayer 2.4, com.unity.services.core/authentication e com.unity.transport 2.7.
// O build de verdade (Unity) compila contra os pacotes reais.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Networking.Transport
{
    public struct NetworkEndpoint { public static NetworkEndpoint AnyIpv4 => default; }
    public struct NetworkPipeline { }
    public struct NetworkConnection { public bool IsCreated => false; }
    public struct NetworkSettings { public NetworkSettings(Unity.Collections.Allocator a) { } }
    public static class NetworkEvent { public enum Type { Empty, Data, Connect, Disconnect } }
    public struct DataStreamReader { public int Length => 0; public void ReadBytes(Span<byte> s) { } }
    public struct DataStreamWriter { public bool WriteBytes(Span<byte> s) => true; }
    public struct ReliableSequencedPipelineStage { }
    public struct NetworkDriver : IDisposable
    {
        public bool IsCreated => false;
        public static NetworkDriver Create(NetworkSettings s) => default;
        public NetworkPipeline CreatePipeline(params Type[] stages) => default;
        public int Bind(NetworkEndpoint e) => 0;
        public int Listen() => 0;
        public NetworkConnection Accept() => default;
        public NetworkConnection Connect(NetworkEndpoint e) => default;
        public int Disconnect(NetworkConnection c) => 0;
        public Unity.Jobs.JobHandle ScheduleUpdate(Unity.Jobs.JobHandle dep = default) => default;
        public NetworkEvent.Type PopEventForConnection(NetworkConnection c, out DataStreamReader r) { r = default; return NetworkEvent.Type.Empty; }
        public int BeginSend(NetworkPipeline p, NetworkConnection c, out DataStreamWriter w, int requiredPayloadSize = 0) { w = default; return 0; }
        public int EndSend(DataStreamWriter w) => 0;
        public void Dispose() { }
    }
}

namespace Unity.Networking.Transport.Relay
{
    public struct RelayServerData { public Unity.Networking.Transport.NetworkEndpoint Endpoint; }
    public static class RelayParameterExtensions
    {
        public static ref Unity.Networking.Transport.NetworkSettings WithRelayParameters(ref this Unity.Networking.Transport.NetworkSettings s, ref RelayServerData d, int relayConnectionTimeMS = 3000) => ref s;
    }
}

namespace Unity.Networking.Transport.Utilities
{
    public static class ReliableStageParameterExtensions
    {
        public static ref Unity.Networking.Transport.NetworkSettings WithReliableStageParameters(ref this Unity.Networking.Transport.NetworkSettings s, int windowSize = 32, int minimumResendTime = 64, int maximumResendTime = 200) => ref s;
    }
}

namespace Unity.Services.Core
{
    public enum ServicesInitializationState { Uninitialized, Initializing, Initialized }
    public static class UnityServices
    {
        public static ServicesInitializationState State => default;
        public static Task InitializeAsync() => Task.CompletedTask;
    }
}

namespace Unity.Services.Authentication
{
    public interface IAuthenticationService { bool IsSignedIn { get; } Task SignInAnonymouslyAsync(); }
    public static class AuthenticationService { public static IAuthenticationService Instance => null; }
}

namespace Unity.Services.Multiplayer
{
    public enum NetworkRole { Client, Server, Host }
    public enum NetworkType { Direct, Relay, DistributedAuthority }
    public class NetworkConfiguration
    {
        public NetworkRole Role { get; }
        public NetworkType Type { get; }
        public Unity.Networking.Transport.Relay.RelayServerData RelayServerData { get; }
        public Unity.Networking.Transport.Relay.RelayServerData RelayClientData { get; }
    }
    public interface INetworkHandler
    {
        Task StartAsync(NetworkConfiguration configuration);
        Task StartAsync(NetworkConfiguration configuration, CancellationToken cancellationToken) => StartAsync(configuration);
        Task StopAsync();
    }
    public enum VisibilityPropertyOptions { Public, Member, Private }
    public enum PropertyIndex { None, String1, String2, String3, String4, String5, Number1 }
    public class SessionProperty { public SessionProperty(string value, VisibilityPropertyOptions visibility = VisibilityPropertyOptions.Public, PropertyIndex index = PropertyIndex.None) { } }
    public enum FilterField { MaxPlayers, AvailableSlots, Name, Created, LastUpdated, StringIndex1 }
    public enum FilterOperation { Contains, Equal, NotEqual }
    public class FilterOption { public FilterOption(FilterField field, string value, FilterOperation operation) { } }
    public abstract class BaseSessionOptions { }
    public class SessionOptions : BaseSessionOptions
    {
        public bool IsLocked { get; set; }
        public bool IsPrivate { get; set; }
        public int MaxPlayers { get; set; }
        public string Name { get; set; }
        public Dictionary<string, SessionProperty> SessionProperties { get; set; }
    }
    public class JoinSessionOptions : BaseSessionOptions { }
    public class QuickJoinOptions
    {
        public bool CreateSession { get; set; }
        public List<FilterOption> Filters { get; set; }
        public TimeSpan Timeout { get; set; }
    }
    public static class SessionOptionsExtensions
    {
        public static T WithRelayNetwork<T>(this T options, string region = default) where T : SessionOptions => options;
        public static T WithNetworkHandler<T>(this T options, INetworkHandler networkHandler) where T : BaseSessionOptions => options;
    }
    public class SessionException : Exception { }
    public interface ISession
    {
        event Action<string> PlayerJoined;
        event Action<string> PlayerHasLeft;
        event Action RemovedFromSession;
        string Code { get; }
        string Id { get; }
        bool IsHost { get; }
        int PlayerCount { get; }
        Task LeaveAsync();
    }
    public interface IHostSession : ISession { }
    public interface IMultiplayerService
    {
        Task<IHostSession> CreateSessionAsync(SessionOptions sessionOptions);
        Task<ISession> JoinSessionByCodeAsync(string sessionCode, JoinSessionOptions sessionOptions);
        Task<ISession> MatchmakeSessionAsync(QuickJoinOptions quickJoinOptions, SessionOptions sessionOptions);
    }
    public static class MultiplayerService { public static IMultiplayerService Instance { get; set; } }
}
