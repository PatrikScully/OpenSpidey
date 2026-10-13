set pagination off
set confirm off
set env SPIDEY_SKIP_MOVIES 1
set env SDL_AUDIODRIVER dummy
set env SPIDEY_TRACE_PARTS 1
set env SPIDEY_QUIT_MS 20000
set env SPIDEY_KEYS 1000:enter,1000:enter,1000:enter
break SpideyMain
commands
silent
set *(int*)0x568fb0 = 0
continue
end
break PlayAway
run
set $pl = (CPlayer*)*(int*)0x6a9038
set $anim = (char*)PSXRegion[$pl->mRegion].pAnimFile
set $start = $anim + ((int*)$anim)[541]
set $end = $anim + ((int*)$anim)[543]
set $buffer = (SDoubleBuffer*)0x56fb60
set $i = 0
while $i < 2
if $start < (char*)$buffer[$i].Polys + 0x17000 && $end > (char*)$buffer[$i].Polys
error "Render buffer overlaps the E transition animation"
end
set $i = $i + 1
end
python
before = bytes(gdb.selected_inferior().read_memory(int(gdb.parse_and_eval('$start')), int(gdb.parse_and_eval('$end - $start'))))
end
set $i = 0
while $i < 8
call PCGfx_BeginScene(3,-1)
call Db_FlipClear()
call CalcPolyBufferEnd()
call Display()
call PCGfx_EndScene(0)
set $i = $i + 1
end
python
after = bytes(gdb.selected_inferior().read_memory(int(gdb.parse_and_eval('$start')), len(before)))
assert before == after, 'Rendering changed the compressed E animation'
end
set $pl->mFlags = $pl->mFlags | 4
call $pl->PlaySingleAnim(270,13,-1)
call $pl->ApplyPose((short*)0x5564e4)
set $i = 0
while $i < 18
set $pose = $pl->mpPoseBuffer[$i]
if $pose.t[0] > 20000 || $pose.t[0] < -20000 || $pose.t[1] > 20000 || $pose.t[1] < -20000 || $pose.t[2] > 20000 || $pose.t[2] < -20000
error "E transition produces invalid limb offsets"
end
set $i = $i + 1
end
printf "PASS render allocations, immutable E animation, and airborne transition pose\n"
quit
