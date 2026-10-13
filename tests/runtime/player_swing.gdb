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
set $bitsBefore = *(int*)0x56eb48
set $linesBefore = *(int*)0x56e9d8
set $memory = (char*)DCMem_New(512, 0, 1, 0, 1)
python
import gdb
gdb.selected_inferior().write_memory(int(gdb.parse_and_eval("$memory")), b"\0" * 512)
end
set $swinger = (CSwinger*)$memory
set $anchor = (CVector*)($memory + 0x190)
set $direction = (CVector*)($memory + 0x19c)
set $basis = (MATRIX*)($memory + 0x1a8)
set $anchor->vx = -375 * 4096
set $anchor->vy = -500 * 4096
set $anchor->vz = 2900 * 4096
set $direction->vx = 0
set $direction->vy = -4096
set $direction->vz = 0
set $basis->m[0][0] = 4096
set $basis->m[1][1] = 4096
set $basis->m[2][2] = 4096
call ((void(*)(CSwinger*,CVector*,int,MATRIX*,CVector*)) &'CSwinger::CSwinger(CVector*, int, MATRIX const*, CVector*)')($swinger,$anchor,96,$basis,$direction)
if $swinger->mPhase != 2048 || $swinger->mLength != 96 || !$swinger->mpLine
error "Swing constructor state or line allocation failed"
end
set $savedObject = $player->field_E64
set $player->field_E64 = (int*)$swinger
set $swinger->mAngles.vx = 512
set $swinger->mPhaseSpeed = 32
set $savedTick = *(int*)0x6b4ca8
set *(int*)0x6b4ca8 = $swinger->mLastUpdateTick + 2
call $swinger->AI()
if $swinger->mPhase != 2112
error "Swing phase did not advance by elapsed ticks"
end
python
import gdb

def vector(expression):
    return tuple(int(gdb.parse_and_eval(expression + '.' + axis)) for axis in ('vx','vy','vz'))
anchor = vector('$swinger->mAnchor')
down = vector('$swinger->mDown')
expected = tuple(a - 96 * d for a, d in zip(anchor, down))
segments = int(gdb.parse_and_eval('$swinger->mpLine->mNumSegs'))
rounded_end = tuple(a + (abs(e-a)//segments) * (1 if e >= a else -1) * segments for a,e in zip(anchor,expected))
assert vector('$swinger->mpLine->mSegs[' + str(segments-1) + '].End') == rounded_end
for index in range(segments):
    assert vector('((CKnottedWeb*)$swinger->mpLine)->mpExtraSegs[' + str(index) + '].mPos') == vector('$swinger->mpLine->mSegs[' + str(index) + '].End')
print('PASS actual swing constructor, phase/orientation and synchronized web segments')

class TraceWatch(gdb.Breakpoint):
    def __init__(self):
        super().__init__('M3dZone_LineToItem', internal=True)
        self.calls = 0
    def stop(self):
        self.calls += 1
        return False
watch = TraceWatch()
end
call $player->DoSwingingPhysics()
python
assert 1 <= watch.calls <= 3, watch.calls
watch.delete()
if int(gdb.parse_and_eval('$player->mLineInfo.pItem')):
    assert vector('$player->mVel') == (0,0,0)
else:
    assert int(gdb.parse_and_eval('$player->mPos.vx')) == expected[0]
    assert int(gdb.parse_and_eval('$player->mPos.vy')) == expected[1]
    assert int(gdb.parse_and_eval('$player->mPos.vz')) == expected[2]
print('PASS player swing integration with actual hook sampling and world collision probes')
end
python
old_end = vector('$swinger->mpLine->mSegs[' + str(segments-1) + '].End')
render_end = vector('$player->mPos')
end
call $swinger->SetRenderEnd($player->mPos)
python
assert vector('$swinger->mRenderEndOffset') == tuple(a-b for a,b in zip(render_end,old_end))
for index in range(segments):
    assert vector('((CKnottedWeb*)$swinger->mpLine)->mpExtraSegs[' + str(index) + '].mPos') == vector('$swinger->mpLine->mSegs[' + str(index) + '].End')
print('PASS render-end offset and shared web-line update')
end
call $swinger->SetSpideyAnimFrame(24)
if $player->mFrame != 0
error "Swing animation phase mapping failed"
end
set $swinger->mPhase = 4096
set $complete = $swinger->IsOneTimeToDie()
if !$complete
error "Swing completion threshold failed"
end
call $swinger->SetSpideyAnimFrame(24)
if $player->mFrame != 24
error "Final swing animation frame failed"
end
set $player->field_E64 = $savedObject
set *(int*)0x6b4ca8 = $savedTick
# Use the deleting destructor: it releases the web and this allocation.
call ((void(*)(CSwinger*)) &'_ZN8CSwingerD0Ev')($swinger)
if *(int*)0x56eb48 != $bitsBefore || *(int*)0x56e9d8 != $linesBefore
error "Swing destruction did not restore web bit ownership"
end
printf "PASS real web-swing cleanup and original player object restoration\n"
quit
