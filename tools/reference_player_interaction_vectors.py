"""Compile exact recovered player pickup/throw/switch slices and held transform; prove corrected /24 with original PC instructions."""
import argparse,json,random,subprocess,tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--original-exe',type=Path,required=True);a=p.parse_args();root=Path(__file__).resolve().parent.parent
 source=(root/'src/game/player/player_interaction_actions.cpp').read_text()
 code=r'''
#include "core/distance.h"
#include "original_executable.h"
#include <cmath>
#include <iostream>
#include <vector>
using namespace spidey::core;
std::vector<long long>events;void emit(std::initializer_list<long long>v){events.insert(events.end(),v);}struct SHandle{int target,validation;};
struct CBody{CVector mPos;};struct CManipOb:CBody{int id=1;void Pickup(){emit({5,id,6,id});}void Throw(CVector*p){emit({10,id,p->vx,p->vy,p->vz});}void ThrowPos(CVector*p,int count){emit({11,id,p->vx,p->vy,p->vz,count});}};
struct CSwitch{int id=2;void Flick(){emit({13,id});}};CManipOb object;CSwitch switchObject;int valid;
void*Mem_RecoverPointer(SHandle*h){emit({h->target==1?1:2,h->target,h->validation});if(!valid){h->target=0;return nullptr;}return h->target==1?(void*)&object:(void*)&switchObject;}
void SFX_Play(unsigned short sound,int volume,int pitch){emit({4,sound,volume,pitch});}void SFX_PlayPos(unsigned short sound,CVector*p,int pitch){emit({3,sound,p->vx,p->vy,p->vz,pitch});}
struct CPlayer{unsigned char raw[0xE60]={};short field_EA6=77;unsigned short mAnim=0;int mFrame=0,mAnimFinished=0,field_E20=0;CVector mPos,mForwardDirection,mUpDirection;CManipOb*mHeldObject=nullptr;CBody*field_DCC=nullptr;
 void SwitchToStandMode(){emit({7});}void CheckJump(){emit({8});}void PutCameraBehind(int n){emit({9,n});}};
class PlayerBehaviorController{public:CPlayer&player_;unsigned short completedAnimation_=0xFFFF;PlayerBehaviorController(CPlayer&p):player_(p){}void updatePickupReach();void updateObjectThrow();void updateSwitchPull();};
'''
 vector=(root/'src/core/fixed_vector.cpp').read_text()
 for operator in ['CVector operator+(', 'CVector operator-(', 'CVector operator*(const CVector&', 'CVector operator/(const CVector&', 'CVector operator>>(const CVector&', 'CVector operator*(const int&']:
  code+=controller_method(vector,operator)+'\n'
 for method in ['void PlayerBehaviorController::updatePickupReach(', 'void PlayerBehaviorController::updateObjectThrow(', 'void PlayerBehaviorController::updateSwitchPull(']:
  body=controller_method(source,method).replace('reinterpret_cast<char*>(pPlayer) + 0xE4C','pPlayer->raw + 0xE4C').replace('reinterpret_cast<char*>(pPlayer) + 0xE54','pPlayer->raw + 0xE54')
  if 'updateObjectThrow' in method:body=body.replace('pPlayer->mHeldObject = 0;','emit({12,pPlayer->mHeldObject->id});pPlayer->mHeldObject = 0;')
  code+=body+'\n'
 code+=controller_method((root/'src/engine/rendering/ps2funcs.cpp').read_text(),'void VectorNormal(').replace('VECTOR','CVector')+'\n'
 code+=r'''
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Actual 4B60E0..4B60FB instructions: shl12, signed magic multiply, sign correction; resultEdx then return.
 unsigned char*divide=(unsigned char*)mmap(nullptr,64,PROT_READ|PROT_WRITE|PROT_EXEC,MAP_PRIVATE|MAP_ANONYMOUS,-1,0);assert(divide!=MAP_FAILED);std::memcpy(divide,(void*)0x4B60E0,28);divide[28]=0x89;divide[29]=0xD0;divide[30]=0xC3;
 int mode;while(std::cin>>mode){events.clear();if(mode==1){CVector player,left,right,forward;int reach,yaw,delta;std::cin>>player.vx>>player.vy>>player.vz>>left.vx>>left.vy>>left.vz>>right.vx>>right.vy>>right.vz>>forward.vx>>forward.vy>>forward.vz>>reach>>yaw>>delta;
 CVector mid=left+((right-left)/2),dir=(mid-player)>>6;VectorNormal(&dir,&dir);CVector pos=(mid+(dir*reach))+(forward*32);emit({pos.vx,pos.vy,pos.vz,(short)(yaw+(short)delta)});
 }else{CPlayer player;int anim,held,finished,frame,progress,completion;CBody target;int hasTarget;std::cin>>anim>>frame>>finished>>held>>valid>>progress>>completion>>hasTarget>>player.mPos.vx>>player.mPos.vy>>player.mPos.vz>>object.mPos.vx>>object.mPos.vy>>object.mPos.vz>>target.mPos.vx>>target.mPos.vy>>target.mPos.vz>>player.mForwardDirection.vx>>player.mForwardDirection.vy>>player.mForwardDirection.vz>>player.mUpDirection.vx>>player.mUpDirection.vy>>player.mUpDirection.vz;
 player.mAnim=anim;player.mFrame=frame;player.mAnimFinished=finished;player.mHeldObject=held?&object:nullptr;player.field_DCC=hasTarget?&target:nullptr;player.field_E20=progress;*reinterpret_cast<SHandle*>(player.raw+0xE4C)={1,123};*reinterpret_cast<SHandle*>(player.raw+0xE54)={2,456};PlayerBehaviorController controller(player);controller.completedAnimation_=completion;
 if(mode==0)controller.updatePickupReach();else if(mode==2)controller.updateObjectThrow();else controller.updateSwitchPull();
 int distance=(int)spidey::math::approximateHorizontalDistance(player.mPos,target.mPos),actual;asm volatile("call *%2":"=a"(actual):"a"(distance),"r"(divide):"ecx","edx","memory");assert(actual==(distance<<12)/24);
 auto manip=reinterpret_cast<SHandle*>(player.raw+0xE4C),sw=reinterpret_cast<SHandle*>(player.raw+0xE54);events.insert(events.begin(),{player.field_EA6,player.mHeldObject!=nullptr,player.field_E20,actual,manip->target,manip->validation,sw->target,sw->validation});}
 std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]\n";
 }}
'''
 cases=[]
 def row(mode,anim,frame,finished=0,held=0,valid=1,progress=0,completion=65535,target=0,distance=0):return [mode,anim,frame,finished,held,valid,progress,completion,target,0,0,0,distance*4096,0,0,distance*4096,32768,0,0,0,4096,0,-4096,0]
 for anim in [0,189,190,196]:
  for frame in [9,10,11]:
   for held in [0,1]:
    for valid in [0,1]:
     for distance in [767,768]:cases.append(row(0,anim,frame,held=held,valid=valid,distance=distance))
 for anim in [0,195,201]:
  for frame in [5,6,12,13,17,18]:
   for held in [0,1]:
    for distance in [0,479,480,1023,1024,2048]:
     for target in [0,1]:cases.append(row(2,anim,frame,held=held,distance=distance,target=target))
 for frame in [11,12,13]:
  for valid in [0,1]:
   for progress in [0,1,-1]:
    for completion in [65535,0,37]:cases.append(row(3,37,frame,valid=valid,progress=progress,completion=completion))
 for mode,anim in [(0,190),(2,195),(3,37)]:cases.append(row(mode,anim,20,finished=1,held=1))
 rng=random.Random(0x4B60FD)
 for _ in range(80):cases.append([1,*[rng.randint(-2147483648,2147483647) for _ in range(9)],0,0,4096,rng.randint(-32768,32767),rng.randint(-32768,32767),rng.randint(-2147483648,2147483647)])
 with tempfile.TemporaryDirectory(prefix='player-interaction-source-') as t:
  path=Path(t);(path/'reference.cpp').write_text(code)
  subprocess.run(['g++','-m32','-std=c++17','-O0','-fwrapv','-fno-pie','-no-pie','-mfpmath=sse','-msse2','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(path/'reference.cpp'),'-o',str(path/'reference')],check=True)
  result=subprocess.run([str(path/'reference'),str(a.original_exe)],input='\n'.join(' '.join(map(str,c)) for c in cases),text=True,capture_output=True,check=True)
 (root/'modern/OpenSpidey.Tests/Fixtures/player-interaction.json').write_text(json.dumps({'originalPcHardThrowDivisionVerified':True,'sourceActionSlicesVerified':True,'cases':[{'input':c,'events':json.loads(s)} for c,s in zip(cases,result.stdout.splitlines(),strict=True)]},separators=(',',':'))+'\n')
 print(f'Compiled source interaction slices / original PC divide: {len(cases)} cases.')
if __name__=='__main__':main()
