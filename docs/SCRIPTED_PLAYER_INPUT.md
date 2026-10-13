# Scripted player input

The modern implementation is `PlayerScriptProgram` in `OpenSpidey.Assets` and
`PlayerScriptRuntime` in `OpenSpidey.Core/Player/Scripting`. It uses managed
command records and an injected `IPlayerScriptHost` for world/actor effects.
`PlayerScriptCommandHandler` starts it through mission opcode 199. Scheduling,
emitted input and callback state match 96 compiled recovered C++ sequences
(3,456 updates); all 167 archived player-control streams decode. The Godot
player host and original script-entry cleanup are still pending. See
`GODOT_MIGRATION.md` for the verified boundaries and remaining gameplay work.

The notes below describe the recovered reference implementation.

The cutscene/demo input controller consumes signed 16-bit records containing
an elapsed-time threshold, an opcode and its parameters. A due time of -1 runs
immediately. Each executed instruction resets the elapsed-time accumulator.
Blocking instructions suspend decoding while concurrent command blocks continue
updating; completion resumes decoding on the following frame.

The player owns the elapsed time, decoding flag, encoded cursor and command
head. The controller borrows these fields and adds nothing to CPlayer's layout.
Command blocks retain their original variable-length word encoding: type in
word 0, word count in word 1 and the next pointer in the last word. The shared `ScriptCommandList` manages these lifetimes through a borrowed head
and an injected allocator. Original member functions remain compatibility
adapters. `ScriptCommandView` names the type, length, parameters and encoded
next link; the input controller creates records by type and parameter count.

| Opcode | Operation |
| --- | --- |
| 1 | Teleport to a trigger node and reset attached movement effects |
| 2 | Walk toward a trigger position; suspend decoding until arrival |
| 3 | Hold a digital input register for a duration |
| 4 | Play XA voice/audio |
| 5 | Move toward a trigger position; suspend decoding until arrival |
| 6 | Keep an animation playing for a duration |
| 7 | Force stand mode after a delay |
| 8 | Hold the recovered player value at offset E00 for a duration |
| 9 | Wait, suspending decoding until the timer expires |
| 15 | Play an animation and wait for input flag bit 0 |
| 16 | Remember a trigger node for checkpoint restoration |
| 17 | Hold interaction register 16 when the recovered AD4 flag is set |
| 18 | Set the original script-control byte |
| 255 | Stop instruction decoding |

Timed commands are active at remaining time zero and expire below zero.
Most durations include the current frame's elapsed ticks when enqueued,
so the first command update does not shorten the requested duration.
Move-to-target commands preserve the original distance threshold and direction
calculation. The recovered duration parameter on opcode 5 is stored unchanged;
the command's current implementation does not decrement it.

Input publication preserves twenty 16-byte registers and copies their first
three state bytes to the player's control record. Registers 14 and 15 are
skipped, preserving Start/Select. Direction registers override the synthesized
stick axes. The original completion branch retains its level 1795 boss-area
exception, pending menu exit and conditional checkpoint restore.

Unverified player members and fixed-address bindings remain explicit. Their
purpose must be established before replacing their storage or guessing names.
