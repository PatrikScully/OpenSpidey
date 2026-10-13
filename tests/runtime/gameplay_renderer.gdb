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

python
import gdb
phases = []
checks = []
# Observe actual subsystem calls, including stages inlined by optimized builds.
class Phase(gdb.Breakpoint):
    def __init__(self, method, label):
        super().__init__(method)
        self.label = label
    def stop(self):
        phases.append(self.label)
        return False
for method, label in (
        ('spidey::application::WorldSimulation::updateCamera', 'camera'),
        ('Panel_Display', 'hud'), ('M3d_RenderSetup', 'world-setup'),
        ('Screen_DrawTarget', 'target'), ('M3d_RenderCleanup', 'world-cleanup'),
        ('Bit_Display', 'particles'), ('Front_Display', 'front'),
        ('Flash_Display', 'flash')):
    Phase(method, label)
class CoordinateMode(gdb.Breakpoint):
    def __init__(self, method, hud):
        super().__init__(method)
        self.hud = hud
        self.method = method
    def stop(self):
        assert int(gdb.parse_and_eval('gGameView')) == 1, self.method
        assert int(gdb.parse_and_eval('gHudView')) == self.hud, self.method
        checks.append(self.method)
        return False
CoordinateMode('Panel_Display', 1)
CoordinateMode('M3d_RenderSetup', 0)
end
set gRenderTest = 0
set *(int*)0x5fae98 = 0
call Display()
python
assert phases == ['camera', 'hud', 'world-setup', 'target', 'world-cleanup',
                  'particles', 'front', 'flash'], phases
assert checks == ['Panel_Display', 'M3d_RenderSetup'], checks
print('PASS gameplay rendering phases and HUD/world coordinate modes')
end
quit
