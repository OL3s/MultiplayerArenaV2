using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Godot;

public enum GameLogScope {
    General,
    Networking,
    Lobby,
    MatchSetup,
    PlayerItemRoom,
    PlayerSpawn,
    PlayerInput,
    PlayerMovement,
    PlayerAim,
    PlayerLoadout,
    PlayerItemUse,
    Projectile,
    Damage,
    DestructibleMap,
    Props,
    UI,
    Settings,
}

public enum GameLogType {
    Lifecycle,
    ApiCall,
    StateChange,
    Validation,
    Authority,
    RpcSend,
    RpcReceive,
    Sync,
    Input,
    Spawn,
    Despawn,
    Movement,
    Aim,
    UI,
    ItemEquip,
    ItemUse,
    Projectile,
    Damage,
    Warning,
    Error,
}

public enum GameLogRole {
    Unknown,
    Local,
    Server,
    Client,
    HeadlessServer,
}

public static class GameLog {
    private static long _sequence;
    private static Networking _networking;
    private static bool? _verboseOutputEnabled;

    public static void RegisterNetworking(Networking networking) {
        _networking = networking;
    }

    public static void Print(
        GameLogScope scope,
        GameLogType type,
        string eventName,
        string details = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "") {
        GD.Print(BuildLine(scope, type, eventName, details, filePath, lineNumber, memberName));
    }

    public static void Warn(
        GameLogScope scope,
        string eventName,
        string details = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "") {
        GD.PushWarning(BuildLine(scope, GameLogType.Warning, eventName, details, filePath, lineNumber, memberName));
    }

    public static void Error(
        GameLogScope scope,
        string eventName,
        string details = "",
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "") {
        GD.PushError(BuildLine(scope, GameLogType.Error, eventName, details, filePath, lineNumber, memberName));
    }

    private static string BuildLine(GameLogScope scope, GameLogType type, string eventName, string details, string filePath, int lineNumber, string memberName) {
        var sequence = Interlocked.Increment(ref _sequence);
        var networking = GetNetworking();
        var mode = networking?.CurrentMode.ToString() ?? "Unknown";
        var role = GetRole(networking);
        var peerId = GetPeerId(networking);
        var time = DateTime.Now.ToString("HH:mm:ss.fff");
        var suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" {details}";

        if (IsVerboseOutputEnabled())
            return $"[{sequence:000000}][{time}][Pid={OS.GetProcessId()}][Role={role}][Mode={mode}][Peer={peerId}][Source={FormatSource(filePath, lineNumber, memberName)}][Scope={scope}][Type={type}][Event={eventName}]{suffix}";

        return $"[{role}][{scope}/{type}][{eventName}]{suffix}";
    }

    private static string FormatSource(string filePath, int lineNumber, string memberName) {
        var fileName = string.IsNullOrWhiteSpace(filePath) ? "unknown" : Path.GetFileName(filePath);
        var member = string.IsNullOrWhiteSpace(memberName) ? "unknown" : memberName;
        return $"{fileName}:{lineNumber}:{member}";
    }

    private static bool IsVerboseOutputEnabled() {
        if (_verboseOutputEnabled.HasValue)
            return _verboseOutputEnabled.Value;

        foreach (var argument in OS.GetCmdlineUserArgs()) {
            if (argument == "--verbose") {
                _verboseOutputEnabled = true;
                return true;
            }
        }

        _verboseOutputEnabled = false;
        return false;
    }

    private static Networking GetNetworking() {
        if (_networking != null && GodotObject.IsInstanceValid(_networking))
            return _networking;

        return null;
    }

    private static GameLogRole GetRole(Networking networking) {
        if (networking == null)
            return GameLogRole.Unknown;

        if (networking.IsLocal)
            return GameLogRole.Local;

        if (networking.IsClient)
            return GameLogRole.Client;

        if (networking.IsServer)
            return IsHeadlessRun() ? GameLogRole.HeadlessServer : GameLogRole.Server;

        return GameLogRole.Unknown;
    }

    private static int GetPeerId(Networking networking) {
        if (networking == null || !networking.HasActiveNetworkPeer)
            return -1;

        return networking.Multiplayer.GetUniqueId();
    }

    private static bool IsHeadlessRun() {
        if (DisplayServer.GetName() == "headless")
            return true;

        foreach (var argument in OS.GetCmdlineArgs()) {
            if (argument == "--headless")
                return true;
        }

        return false;
    }
}
