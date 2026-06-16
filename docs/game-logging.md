# Game Logging

This document describes the shared runtime logging style used by MultiplayerArenaV2.

## Goal

Logs are a development API for understanding the game runtime from terminal output, especially during LAN tests where several Godot instances can write into the same shell. Important systems should log their public/API-facing events, authority decisions, state transitions, RPC send/receive points, validation failures, and gameplay effects in a consistent format.

Use the shared `GameLog` API instead of calling `GD.Print()` directly from gameplay, UI, networking, or test-scene code.

## Format

`GameLog` prints one short event per line by default:

```text
[Server][PlayerItemRoom/RpcReceive][RpcRequestUsePlayerItem] global=1 item=pistol_t1 aim=(0.980,-0.180) strength=1.000
```

Pass `--verbose` as a Godot user argument after `--` to include sequence, timestamp, process id, peer id, source file/line/member, and explicit field names:

```bash
godot --path . -- --verbose
```

```text
[000421][12:04:31.482][Pid=18422][Role=Server][Mode=Lan][Peer=1][Source=ArenaMatch.cs:1245:RpcRequestUsePlayerItem][Scope=PlayerItemRoom][Type=RpcReceive][Event=RpcRequestUsePlayerItem] global=1 item=pistol_t1 aim=(0.980,-0.180) strength=1.000
```

Fields:

- Sequence: local monotonically increasing event number for that process.
- Time: local wall-clock time with milliseconds.
- `Pid`: OS process id, used to separate logs when multiple `godot` processes share a terminal.
- `Role`: `Local`, `Server`, `Client`, `HeadlessServer`, or `Unknown`.
- `Mode`: current `Networking.NetworkMode` when available.
- `Peer`: local Godot multiplayer peer id, or `-1` before a network peer exists.
- `Source`: source file, line, and member that emitted the log. Verbose output only.
- `Scope`: system that produced the event.
- `Type`: event category.
- `Event`: stable short event name.
- Details: key/value-style free text specific to the event.

Use default short output when reading normal development logs by eye. Use `--verbose` when mixed host/client processes in one terminal need process id, timestamp, peer id, mode context, and source location.

## API

Use:

```csharp
GameLog.Print(GameLogScope.Networking, GameLogType.RpcSend, "RpcUpdatePlayer", $"global={globalId} peer={peerId}");
GameLog.Warn(GameLogScope.PlayerLoadout, "EquipItemRejected", $"global={globalId} item={itemId} reason=noCapacity");
GameLog.Error(GameLogScope.Networking, "ConnectionFailed", LastConnectionError);
```

Prefer stable event names in PascalCase. Put changing values in the details string instead of changing the event name.

## What To Log

Log most essential API-driven events:

- Network mode changes, host/client start, connection success/failure, peer connect/disconnect.
- Public networking state update methods and corresponding RPC send/receive methods.
- Lobby player/peer registration, team changes, setup config apply/revert, and validation failures.
- Test-scene lifecycle: scene ready, room built, host/client start result, spawned/removed runtime players.
- Input state transitions after quantization, not every raw input sample.
- Player movement and aim state changes when they cause API/RPC traffic.
- Item/armor equip requests, accepted changes, capacity rejections, and sync calls.
- Item-use requests, server validation, remaining-use rejections, accepted uses, and spawned projectile/throwable objects.
- Destructible map and prop damage requests, authority rejections, RPC application, reset/rebuild events.
- UI actions that affect gameplay/control state, such as debug equipment menu open/close.
- UI local-lobby events such as player join attempts, accepted joins, rejected joins, and reset players.
- Root scene changes, logged centrally when `SceneTree.CurrentScene` changes.

## What Not To Log

Do not log every `_Process()` or `_PhysicsProcess()` tick. Do not log every continuous replicated position update unless diagnosing a specific bug. Continuous frame logs make mixed host/client terminal output unreadable and can affect runtime behavior.

For high-frequency systems, log state transitions and request boundaries:

- Good: movement changed from no input to direction bucket `3`.
- Good: client sent `RpcRequestSetPlayerMovementVector`.
- Bad: current position every physics frame.
- Bad: raw input axis every frame.

## Scope And Type Usage

Choose the narrowest useful `GameLogScope`, such as `Networking`, `PlayerItemRoom`, `PlayerLoadout`, `PlayerItemUse`, `Projectile`, `Damage`, or `DestructibleMap`.

Choose `GameLogType` by intent:

- `Lifecycle`: startup/shutdown/connect/disconnect/readiness.
- `ApiCall`: public method or system boundary called.
- `StateChange`: durable state changed.
- `Validation`: accepted/rejected input or config.
- `Authority`: server/client ownership or authority decision.
- `RpcSend`: about to send an RPC.
- `RpcReceive`: received/applying an RPC.
- `Sync`: applying a replicated authoritative state.
- `Input`, `Movement`, `Aim`: gameplay input state transitions.
- `ItemEquip`, `ItemUse`, `Projectile`, `Damage`: gameplay effects.
- `Warning`, `Error`: exceptional or broken behavior.

## Current Implementation Notes

- `Networking` registers itself with `GameLog` so log lines can include role, mode, and peer id.
- If `Networking` is not ready yet, logs still print with `Role=Unknown`, `Mode=Unknown`, and `Peer=-1`.
- Existing LAN test scenes use `GameLog` so host and client output can be compared in one terminal.
- Runtime logging should stay concise, event-based, and searchable.

## UI Icon Loading Note

Runtime UI icon loading should use `UiResourceLoader.LoadIconTexture()` instead of direct `GD.Load<Texture2D>()` or `ResourceLoader.Load<Texture2D>()`. The helper follows Godot's runtime image-loading direction by reading SVG icon files into `Image.LoadSvgFromBuffer()` and converting them to `ImageTexture`, avoiding C# resource-cache bridge issues seen during menu scene changes.
