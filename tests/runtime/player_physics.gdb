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
python
import gdb, struct
inferior = gdb.selected_inferior()
def vector(expression):
    return tuple(int(gdb.parse_and_eval(expression + '.' + axis)) for axis in ('vx','vy','vz'))
def assign_vector(expression,values):
    for axis,value in zip(('vx','vy','vz'),values):
        gdb.execute('set ' + expression + '.' + axis + ' = ' + str(value))
def set_field(name,value):
    gdb.execute('set $player->' + name + ' = ' + str(value))
def reset(position,velocity):
    set_field('mIsCrawling',0)
    set_field('field_E64',0)
    set_field('mArenaConstraintActive',0)
    set_field('mActionState',0)
    set_field('mElapsedTicks',2)
    set_field('mSupportBody',0)
    assign_vector('$player->mPos',position)
    assign_vector('$player->mVel',velocity)
    assign_vector('$player->mAcc',(0,0,0))
    for axis in ('vx','vy','vz'):
        gdb.execute('set $player->mFric.' + axis + ' = 31')
    watch.rays=[]
class PhysicsProbeWatch(gdb.Breakpoint):
    def __init__(self):
        super().__init__('M3dZone_LineToItem',internal=True)
        self.rays=[]
    def stop(self):
        pointer=int(gdb.parse_and_eval('pInfo'))
        self.rays.append(struct.unpack('<6i',inferior.read_memory(pointer,24)))
        return False
watch=PhysicsProbeWatch()
reset((-375*4096,-96*4096,2900*4096),(0,0,0))
start=vector('$player->mPos')
gdb.execute('call $player->DoPhysics()')
assert len(watch.rays)==1,watch.rays
assert watch.rays[0]==(start[0],start[1]-0x28000,start[2],start[0],start[1]-0x28000+0x8C000,start[2])
assert vector('$player->mVel')==(0,0,0)
assert int(gdb.parse_and_eval('$player->mGroundContactGraceUpdates'))==4
assert vector('$player->mPos')[1]==vector('$player->mShadowPos')[1]-96*4096
print('PASS stationary player has no uninitialized sweep hit and lands on actual ground')

air=(-375*4096,-10000*4096,2900*4096)
reset(air,(0,-4096,0))
gdb.execute('call $player->DoPhysics()')
assert vector('$player->mPos')==(air[0],air[1]-4095,air[2])
assert vector('$player->mVel')==(0,-4095,0)
assert len(watch.rays)==1
assert int(gdb.parse_and_eval('$player->mCollision'))==0
print('PASS vertical-only upward travel avoids a false horizontal collision')

reset(air,(4096,8192,0))
gdb.execute('call $player->DoPhysics()')
assert vector('$player->mPos')==(air[0]+4096,air[1]+8192,air[2])
assert vector('$player->mVel')==(4096,8192,0)
assert len(watch.rays)==4,watch.rays
print('PASS moving fall uses three motion probes then restores vertical velocity')

reset(air,(4096,8192,0))
wide_before=int(gdb.parse_and_eval('*(int*)0x60f894'))
gdb.execute('set *(int*)0x60f894 = 1')
gdb.execute('call $player->DoPhysics()')
gdb.execute('set *(int*)0x60f894 = ' + str(wide_before))
assert len(watch.rays)==13,len(watch.rays)
main=watch.rays[3]
offsets=((10,0),(-10,0),(0,10),(0,-10),(5,5),(5,-5),(-5,5),(-5,-5),(0,0))
for ray,(x,z) in zip(watch.rays[4:],offsets):
    assert ray==(main[0]+x*4096,main[1],main[2]+z*4096,main[3]+x*4096,main[4],main[5]+z*4096),(ray,x,z)
print('PASS optional ground fan preserves all eight offsets and final repeated probe')

reset(air,(4096,0,8192))
set_field('mActionState',0x40000)
set_field('mAnim',270)
set_field('mFrame',13)
set_field('mElapsedTicks',6)
gdb.execute('call $player->DoPhysics()')
assert not watch.rays
assert vector('$player->mPos')==(air[0]+4096*5,air[1],air[2]+8192*5)
print('PASS collision-free zip animation bypasses probes and retains original timestep policy')

reset(air,(4096,0,0))
boss_before=int(gdb.parse_and_eval('gBossRelated'))
gdb.execute('set gBossRelated = (int)$player')
set_field('mArenaConstraintActive',1)
set_field('mArenaConstraintRadius',300)
gdb.execute('call $player->DoPhysics()')
gdb.execute('set gBossRelated = ' + str(boss_before))
assert vector('$player->mPos')==(air[0]+300*4096,air[1],air[2])
print('PASS arena constraint projects movement onto the configured radius')

reset(air,(0,0,0))
set_field('mActionState',1)
set_field('mWallProbeHeading',0)
assign_vector('$player->mWallProbeDirection',(4096,0,0))
gdb.execute('call $player->DoPhysics()')
heading=int(gdb.parse_and_eval('$player->mWallProbeHeading'))
assert 256<=heading<320,heading
assert len(watch.rays)==2
assert vector('$player->mWallProbeDirection')[0]>0 and vector('$player->mWallProbeDirection')[2]>0
watch.delete()
print('PASS wall-probe miss updates heading and direction independently of the ground query')
end
quit
