set pagination off
set confirm off
set env SPIDEY_SKIP_MOVIES 1
set env SPIDEY_SKIP fades
set env SPIDEY_QUIT_MS 20000
set env SPIDEY_KEYS 1000:enter,1000:enter,1000:enter
break SpideyMain
commands
silent
set *(int*)0x568fb0 = 2
continue
end
break PlayAway
run
disable breakpoints
set $player = (CPlayer*)*(int*)0x6a9038
call $player->KillAllCommandBlocks()
set $player->mElapsedTicks = 2
set $player->mScriptElapsedTicks = 0
set $player->mSynthesizedInputActive = 1
set $player->mScriptBytecodeActive = 1
set $player->mScriptCheckpointNode = 0
set *(unsigned char*)0x60cfc4 = 0
set *(unsigned char*)0x60f770 = 0
set *(int*)0x68293c = 0
set $stream = (short*)DCMem_New(40, 0, 1, 0, 1)
set $player->mScriptCursorAddress = (int)$stream
python
import gdb
import struct
inferior = gdb.selected_inferior()
# A delayed hold, a concurrent direction override and a blocking wait, followed
# by checkpoint/flag instructions and stop. Timers stay active at zero.
words = [4, 3, 0, 4, -1, 3, 8, 2, -1, 9, 4,
         -1, 16, 80, -1, 18, 1, -1, 255]
stream_address = int(gdb.parse_and_eval('$stream'))
inferior.write_memory(stream_address, struct.pack('<' + 'h' * len(words), *words))
pad_address = int(gdb.parse_and_eval('$player->field_E0C'))
for index in (14, 15):
    inferior.write_memory(pad_address + index * 16, b'\x05\x06\x07')
def field(name):
    return int(gdb.parse_and_eval('$player->' + name))
def blocks():
    result = []
    block = field('mScriptCommandHead')
    while block:
        command, size = struct.unpack('<ii', inferior.read_memory(block, 8))
        result.append(command)
        block = struct.unpack('<I', inferior.read_memory(block + 4 * (size - 1), 4))[0]
    return result
def pressed(index):
    return bytes(inferior.read_memory(pad_address + index * 16, 1))[0]
end
call $player->SynthesizeAnalogueInput()
python
assert field('mScriptElapsedTicks') == 2
assert field('mScriptCursorAddress') == stream_address
assert blocks() == []
end
call $player->SynthesizeAnalogueInput()
python
assert blocks() == [3, 3, 9], blocks()
assert field('mScriptBytecodeActive') == 0
assert field('mSynthesizedInputActive') == 1
assert pressed(0) == 1 and pressed(8) == 1
assert field('field_E2E') == -127
end
call $player->SynthesizeAnalogueInput()
python
assert blocks() == [3, 3, 9], blocks()
assert pressed(0) == 1 and pressed(8) == 1
end
call $player->SynthesizeAnalogueInput()
python
assert blocks() == [3, 9], blocks()
assert pressed(0) == 1 and pressed(8) == 0
end
call $player->SynthesizeAnalogueInput()
python
assert blocks() == []
assert pressed(0) == 0
assert field('mScriptBytecodeActive') == 1
assert field('mSynthesizedInputActive') == 1
assert field('mScriptCheckpointNode') == 0
end
call $player->SynthesizeAnalogueInput()
python
assert blocks() == []
assert field('mScriptBytecodeActive') == 0
assert field('mSynthesizedInputActive') == 0
assert field('mScriptCheckpointNode') == 80
assert field('mScriptCursorAddress') == stream_address + len(words) * 2
assert bytes(inferior.read_memory(0x60f770, 1)) == b'\x01'
for index in (14, 15):
    assert bytes(inferior.read_memory(pad_address + index * 16, 3)) == b'\x05\x06\x07'
print('PASS delayed instructions, concurrent input, timer boundaries, resume and Start/Select preservation')
end
# Animation/value holders remain concurrent with a signal-blocked animation.
set $player->mScriptElapsedTicks = 0
set $player->mSynthesizedInputActive = 1
set $player->mScriptBytecodeActive = 1
set $player->mScriptCursorAddress = (int)$stream
set $player->mInputFlags = $player->mInputFlags & ~1
python
words = [-1, 6, 0, 2, -1, 8, 123, 2, -1, 15, 1, -1, 255]
inferior.write_memory(stream_address, struct.pack('<' + 'h' * len(words), *words))
end
call $player->SynthesizeAnalogueInput()
python
assert blocks() == [6, 8, 15], blocks()
assert field('mAnim') == 0
assert field('field_E00') == 123
expected_sound = struct.unpack('<I', inferior.read_memory(0x6a82b8, 4))[0]
assert field('field_350') == expected_sound
end
call $player->SynthesizeAnalogueInput()
python
assert blocks() == [6, 8, 15], blocks()
assert field('field_E00') == 123
end
call $player->SynthesizeAnalogueInput()
python
assert blocks() == [15], blocks()
assert field('field_E00') == 0
assert field('mScriptBytecodeActive') == 0
end
set $player->mInputFlags = $player->mInputFlags | 1
call $player->SynthesizeAnalogueInput()
python
assert blocks() == []
assert field('mInputFlags') & 1 == 0
assert field('mScriptBytecodeActive') == 1
end
call $player->SynthesizeAnalogueInput()
python
assert field('mScriptBytecodeActive') == 0
assert field('mSynthesizedInputActive') == 0
print('PASS animation/sound pairing, concurrent value timer and animation signal resume')
end
call (void)Mem_Delete($stream)
quit
