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

# Run real Logic/Display/presentation, rather than manually invoking player AI.
# SPIDEY_60FPS selects the existing opt-in timing policy in the second run.
python
import gdb
import os

minimum_ticks = 1 if os.environ.get('SPIDEY_60FPS') else 2
frame_start = 0
frame_count = 0
present_count = 0

class AdvanceFrame(gdb.Breakpoint):
    def stop(self):
        global frame_start, frame_count
        frame_count += 1
        frame_start = int(gdb.parse_and_eval('*(unsigned int*)0x6b4ca0'))
        if frame_count == 4:
            gdb.execute('set gLevelStatus = 7')
        return False

class PresentFrame(gdb.Breakpoint):
    def stop(self):
        global present_count
        frame = gdb.newest_frame().older()
        while frame:
            if 'LevelSession::presentFrame' in (frame.name() or ''):
                tick = int(gdb.parse_and_eval('*(unsigned int*)0x6b4ca0'))
                elapsed = (tick - frame_start) & 0xffffffff
                assert elapsed >= minimum_ticks, (elapsed, minimum_ticks)
                present_count += 1
                break
            frame = frame.older()
        return False

end

break PlayAway
run
disable breakpoints
# The viewer path must report its exit without running gameplay or cleanup.
set *(int*)0x54d38c = 1
call PlayAway()
python
assert int(gdb.parse_and_eval('gLevelStatus')) == 11
end
# A forced level skips the shell's reset of its viewer/abort flags. Clear
# those flags here so this fixture exercises the playable frame loop.
set *(int*)0x54d38c = 0
set *(int*)0x68293c = 0
python
AdvanceFrame('spidey::application::LevelSession::advanceFrame')
PresentFrame('PCGfx_EndScene')
gdb.Breakpoint('spidey::application::LevelSession::finish')
end

continue
python
assert frame_count == 4, frame_count
assert present_count == 3, present_count
assert int(gdb.parse_and_eval('gLevelStatus')) == 7
end
disable breakpoints
tbreak SFX_StopAll
continue
finish
python
assert int(gdb.parse_and_eval('*(short*)0x660f78')) == 1
print('PASS viewer return, frame updates, presentation pacing, exit and audio cleanup')
end
quit
