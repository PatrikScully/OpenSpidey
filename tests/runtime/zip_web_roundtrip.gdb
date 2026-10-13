set pagination off
set confirm off
set env SPIDEY_SKIP_MOVIES 1
set env SDL_AUDIODRIVER dummy
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
set $pl = (CPlayer*)*(int*)0x6a9038
set $pl->mSynthesizedInputActive = 1
set *(unsigned char*)((char*)$pl+0x553) = 1
set $pl->mElapsedTicks = 2
set $pl->field_1A4 = 1
set $pl->mPos.vx=-375*4096
set $pl->mPos.vy=-128*4096
set $pl->mPos.vz=2900*4096
set $pl->mActionState = 1
set $pl->field_550=0
set $pl->mIsCrawling=0
set $pl->field_A8.vx=0
set $pl->field_A8.vy=-4096
set $pl->field_A8.vz=0
call $pl->OrientToNormal(false,&ZeroVector)
set *(unsigned char*)((char*)$pl->field_E0C+0x60)=1
p $pl->mPos
p $pl->mUpDirection
set $started=$pl->CheckJumpingR1ZipWeb()
if !$started
error "Zip input did not start a transition"
end
set *(unsigned char*)((char*)$pl->field_E0C+0x60)=0
p $pl->mInteractionTargetPosition
disable breakpoints
set $cam=(CCamera*)*(int*)0x56f3b8
set *(int*)0x54d350=0
set *(int*)0x54d354=0
set *(int*)0x54d374=0
set *(int*)0x54d368=0
call $pl->SetFloorCamera(0)
call $pl->PutCameraBehind(0)
call $cam->SetStartPosition()
set $reachedCeiling=0
set $t=0
while $t<120
if $t==60
set *(unsigned char*)((char*)$pl->field_E0C+0x60)=1
set $pl->field_550=0
set $started=$pl->CheckJumpingR1ZipWeb()
if !$started
error "Zip input did not start a transition"
end
set *(unsigned char*)((char*)$pl->field_E0C+0x60)=0
end
call $pl->UpdateFrame()
call SpideyAI0($pl)
call $cam->AI()
printf "ZIP t=%d anim=%d frame=%d state=%x normal=%d,%d,%d pos=%d,%d,%d\n",$t,$pl->mAnim,$pl->mFrame,$pl->mActionState,$pl->field_A8.vx,$pl->field_A8.vy,$pl->field_A8.vz,$pl->mPos.vx>>12,$pl->mPos.vy>>12,$pl->mPos.vz>>12
if $t%4==0 || $t==30 || $t==31
call PCGfx_BeginScene(3,-1)
call Db_FlipClear()
call CalcPolyBufferEnd()
call Display()
call PCGfx_EndScene(0)
set $part=0
while $part<18
set $pose=$pl->mpPoseBuffer[$part]
if $pose.t[0]>20000 || $pose.t[0]<-20000 || $pose.t[1]>20000 || $pose.t[1]<-20000 || $pose.t[2]>20000 || $pose.t[2]<-20000
error "Zip transition corrupted the body pose"
end
set $part=$part+1
end
printf "SHOT t=%d anim=%d frame=%d\n",$t,$pl->mAnim,$pl->mFrame
end
if $pl->field_A8.vy>4000
set $reachedCeiling=1
end
set $t=$t+1
end
if !$reachedCeiling || $pl->field_A8.vy>-4000 || ($pl->mPos.vy>>12)<-200
error "Player did not travel from ground to ceiling and back"
end
printf "PASS ceiling/ground zip roundtrip and rendered limb poses\n"
quit
