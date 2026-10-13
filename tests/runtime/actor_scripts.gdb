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
set *(unsigned char*)0x60cfc4 = 0
set $factory = (unsigned int*)DCMem_New(128, 0, 1, 0, 1)
python
import gdb
import struct
inferior = gdb.selected_inferior()
factory = int(gdb.parse_and_eval('$factory'))
inferior.write_memory(factory + 100, b'blackcat\x00')
end
call Spool_PSX((char*)$factory + 100, 0)

python
# Use actual actor factories, initialized model data, animation routines,
# command allocation and destructors. No level/save data is modified.
def field(name):
    return int(gdb.parse_and_eval('$actor->' + name))
def assign(name, value):
    gdb.execute('set $actor->' + name + ' = ' + str(value))
def tick():
    gdb.execute('call $actor->SynthesizeAnalogueInput()')
def blocks():
    result = []
    pointer = field('mScriptCommandHead')
    while pointer:
        opcode, count = struct.unpack('<ii', inferior.read_memory(pointer, 8))
        result.append(opcode)
        pointer = struct.unpack('<I', inferior.read_memory(pointer + (count - 1) * 4, 4))[0]
    return result
for actor_type, factory_method, clear_method, animation, correct_signal, wrong_signal, speed in (
        ('CBlackCat', 'BlackCat_CreateBlackCat', 'BlackCat_RelocatableModuleClear', 12,
         'mCBodyFlags', 'mInputFlags', 5),
        ('CSpClone', 'SpClone_CreateSpClone', 'SpClone_RelocatableModuleClear', 0,
         'mInputFlags', 'mCBodyFlags', 1)):
    head_before = int(gdb.parse_and_eval('*(unsigned int*)0x56e990'))
    words = [4, 6, animation, 2, -1, 8, 80, 2, -1, 15, animation]
    if actor_type == 'CBlackCat':
        words += [-1, 16, 80]
    words += [-1, 255]
    payload = struct.pack('<iii3h', 0, 0, 0, 0, 0, 0)
    payload += struct.pack('<' + 'h' * len(words), *words)
    inferior.write_memory(factory + 16, payload)
    inferior.write_memory(factory, struct.pack('<II', factory + 16, 0))
    gdb.execute('call ' + factory_method + '($factory, $factory + 2)')
    gdb.execute('set $actor = (' + actor_type + '*)$factory[2]')
    assert int(gdb.parse_and_eval('PSXRegion[$actor->mRegion].Usable')) == 1, actor_type
    assert field('mScriptCursorAddress') == factory + 34
    assign('mElapsedTicks', 2)
    assign('mScriptElapsedTicks', 0)
    assign('mScriptRunning', 1)
    assign('mScriptBytecodeActive', 1)
    assign(correct_signal, 0)
    assign(wrong_signal, 0)
    tick()
    assert blocks() == [] and field('mScriptElapsedTicks') == 2
    tick()
    assert blocks() == [6, 8, 15], (actor_type, blocks())
    assert field('mLookTargetNode') == 80
    assign(wrong_signal, 1)
    tick()
    assert blocks() == [6, 8, 15], (actor_type, blocks())
    tick()
    assert blocks() == [15] and field('mLookTargetNode') == 0
    assert field('mScriptBytecodeActive') == 0
    assign(correct_signal, 1)
    tick()
    assert blocks() == [] and field('mScriptBytecodeActive') == 1
    assert field(correct_signal) & 1 == 0
    assert field(wrong_signal) & 1 == 1
    tick()
    assert field('mScriptRunning') == 0 and field('mScriptBytecodeActive') == 0
    if actor_type == 'CBlackCat':
        assert field('mScriptCheckpointNode') == 80
    print('PASS ' + actor_type + ' delay, concurrent timers, signal source and completion')

    # Identical move commands preserve each actor's original speed policy.
    gdb.execute('set $command = $actor->GetNewCommandBlock(7)')
    pointer = int(gdb.parse_and_eval('$command'))
    x, y, z = (field('mPos.v' + axis) for axis in ('x', 'y', 'z'))
    inferior.write_memory(pointer, struct.pack('<6i', 5, 7, x + 200 * 4096, y, z, 5))
    assign('mScriptRunning', 1)
    tick()
    assert field('mVel.vx') == speed * 4096, (actor_type, field('mVel.vx'))
    assert field('mVel.vy') == 0 and field('mVel.vz') == 0
    if actor_type == 'CSpClone':
        # The global skip flag affects Black Cat's VM only.
        inferior.write_memory(0x60cfc4, b'\x01')
        tick()
        assert blocks() == [5]
        assert field('mVel.vx') == 4096
        inferior.write_memory(0x60cfc4, b'\x00')
    else:
        inferior.write_memory(0x60cfc4, b'\x01')
        tick()
        assert blocks() == [] and field('mScriptRunning') == 0
        assert field('mVel.vx') == 0
        gdb.execute('call Trig_GetPosition((CVector*)((char*)$factory + 80), 80)')
        checkpoint = struct.unpack('<iii', inferior.read_memory(factory + 80, 12))
        assert tuple(field('mPos.v' + axis) for axis in ('x', 'y', 'z')) == checkpoint
        inferior.write_memory(0x60cfc4, b'\x00')
    print('PASS ' + actor_type + ' movement scaling and skip policy')
    gdb.execute('call $actor->KillAllCommandBlocks()')
    gdb.execute('set $walk = $actor->GetNewCommandBlock(5)')
    pointer = int(gdb.parse_and_eval('$walk'))
    x, z = field('mPos.vx'), field('mPos.vz')
    inferior.write_memory(pointer, struct.pack('<4i', 2, 5, x + 200 * 4096, z))
    walk_start, walk_loop, walk_end, walk_speed = ((2, 3, 4, 3)
        if actor_type == 'CBlackCat' else (1, 21, 13, 8))
    gdb.execute('call $actor->RunAnim(' + str(walk_start) + ', 0, -1)')
    assign('mAnimFinished', 1)
    tick()
    assert field('mAnim') == walk_loop, (actor_type, field('mAnim'))
    # The original sine table's cardinal magnitude can be 4095.
    assert abs(field('mVel.vx') - walk_speed * 4096) <= walk_speed, (actor_type, field('mVel.vx'))
    assign('mPos.vx', x + 200 * 4096)
    tick()
    assert blocks() == [] and field('mScriptBytecodeActive') == 1
    assert field('mAnim') == walk_end
    for axis in ('x', 'y', 'z'):
        assert field('mVel.v' + axis) == 0
        assert field('mAngVel.v' + axis) == 0
        assert field('mAngAcc.v' + axis) == 0
    print('PASS ' + actor_type + ' walking speed, animation cycle and arrival')
    # Remove the fixture's artificial signal bit before normal actor teardown.
    assign(wrong_signal, 0)
    gdb.execute('call ' + clear_method + '()')
    assert int(gdb.parse_and_eval('*(unsigned int*)0x56e990')) == head_before
end
call (void)Mem_Delete($factory)
printf "PASS real actor factories, shared script controller and actor cleanup\n"
quit
