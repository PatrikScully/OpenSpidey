set pagination off
set confirm off
set env SPIDEY_SKIP_MOVIES 1
set env SPIDEY_SKIP fades
set env SPIDEY_TRACE_PLAYER 1
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

python
import gdb
phases = []
class Phase(gdb.Breakpoint):
    def __init__(self, method, label):
        super().__init__('spidey::application::WorldSimulation::' + method)
        self.label = label
    def stop(self):
        phases.append(self.label)
        return False
Phase('updateActiveWorld', 'world')
Phase('updateTriggersAndUi', 'ui')
Phase('updateCameraMode', 'camera-mode')
end

set gRenderTest = 0
set *(int*)0x5fae98 = 0
set *(int*)0x60cfb0 = 0
set *(int*)0x682950 = 0
call Logic()
python
assert phases == ['world', 'ui', 'camera-mode'], phases
phases.clear()
end

# Frozen worlds still service pending commands, menus and camera mode.
set *(int*)0x60cfb0 = 1
call Logic()
python
assert phases == ['ui', 'camera-mode'], phases
phases.clear()
end
set *(int*)0x60cfb0 = 0
set *(int*)0x5fae98 = 1
call Logic()
python
assert phases == ['ui', 'camera-mode'], phases
phases.clear()
end

# Input-only debug modes must not advance the world, UI or frame counters.
set $timeBefore = TTime
set $attackBefore = gAttackRelated
set gRenderTest = 0x200
call Logic()
set gRenderTest = 0x400
call Logic()
python
assert phases == [], phases
assert int(gdb.parse_and_eval('TTime')) == int(gdb.parse_and_eval('$timeBefore'))
assert int(gdb.parse_and_eval('gAttackRelated')) == int(gdb.parse_and_eval('$attackBefore'))
print('PASS world/UI/camera phase order, frozen worlds and input-only frame gates')
end
set $traceFrame = 0
while $traceFrame < 60
call spidey::application::PlayerFrameTrace::emit()
set $traceFrame = $traceFrame + 1
end
printf "PASS player and typed environment diagnostics\n"
quit
