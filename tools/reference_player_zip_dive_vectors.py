"""Compile original ZipDive action/forward slices; execute original CThug::Grab machine body and CheckStateFlags."""
import argparse,json,random,subprocess,tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--original-exe',type=Path,required=True);a=p.parse_args();root=Path(__file__).resolve().parent.parent
 code=r'''
#include "core/fixed_vector.h"
#include "engine/world/stateflags.h"
#include "original_executable.h"
#include <cmath>
#include <iostream>
#include <vector>
using namespace spidey::core;
#define FASTCALL __attribute__((fastcall))
#define PLR_I32(p,o) (*reinterpret_cast<int*>(reinterpret_cast<unsigned char*>(p)+(o)))
std::vector<long long>events;void emit(std::initializer_list<long long>v){events.insert(events.end(),v);}int G_DIFFICULTY_LEVEL,G_CURRENTSUIT;unsigned G_TIMER_RELATED;int valid,grabResult,jumpResult;
void print_if_false(int,const char*){}struct SHandle{void*pWhatever;unsigned validation;};
struct SHitInfo{unsigned char field_0=0,field_1=0;int field_4=0;unsigned short field_8=0;CVector field_C;short field_18=0;unsigned short field_1A=0;};
struct CBody{unsigned char raw[0x2B0]={};CVector mPos;unsigned short mType;short mHealth;void Hit(SHitInfo*h){emit({8,h->field_0,h->field_4,h->field_8,h->field_C.vx,h->field_C.vy,h->field_C.vz,h->field_18,h->field_1A});}}target;
void*Mem_RecoverPointer(SHandle*h){emit({1});if(valid)return &target;h->pWhatever=nullptr;return nullptr;}
u8 FASTCALL grab(void*,void*,CVector*p){emit({2,p->vx,p->vy,p->vz});return grabResult;}void*targetVtable[11]={};
void SFX_PlayPos(unsigned short sound,CVector*p,int pitch){emit({9,sound,p->vx,p->vy,p->vz,pitch});}
struct CPlayer{unsigned char input[320]={};void*field_E0C=input;short field_EA6=77;unsigned short mAnim=0;int mFrame=0;unsigned mActionState=0;SHandle field_DD8={&target,55};int field_DE0=0;CVector mPos,mForwardDirection,mUpDirection,mVel;char field_E2D=0,field_E2E=0,field_A80=3;unsigned char field_2C1=1;int field_534=0,field_52C=0,field_528=0;
 void SwitchToStandMode(){emit({3});}void PlaySingleAnim(unsigned short a,int,int){emit({4,a});}int CheckJump(){emit({5});return jumpResult;}void CheckForwards(bool){emit({6});}void LockTargetTorsoAngle(){emit({7});}int GetDamageInflictedFromDifficulty(int);void CreateCombatImpactEffect(CVector*p,int k){emit({10,p->vx,p->vy,p->vz,k});}};
class PlayerBehaviorController{public:CPlayer&player_;unsigned short completedAnimation_=65535;PlayerBehaviorController(CPlayer&p):player_(p){}void updateZipDive();void updateZipDivePunch();void updateZipDiveKick();};
'''
 vector=(root/'src/core/fixed_vector.cpp').read_text()
 for op in ['CVector operator+(', 'CVector operator-(', 'CVector operator*(const CVector&', 'CVector operator*(const int&', 'CVector operator>>(const CVector&', 'CVector* CVector::operator+=(', 'CVector* CVector::operator-=(']:code+=controller_method(vector,op)+'\n'
 code+=controller_method((root/'src/engine/rendering/ps2funcs.cpp').read_text(),'i32 M3dMaths_SquareRoot0(')+'\n'
 code+=r'''unsigned Utils_Dist(const CVector&a,const CVector&b){CVector d=(a-b)>>12;return M3dMaths_SquareRoot0(d.vx*d.vx+d.vy*d.vy+d.vz*d.vz);}'''
 code+=controller_method((root/'src/core/utils.cpp').read_text(),'i32 Utils_GetValueFromDifficultyLevel(')+'\n'
 code+=controller_method((root/'src/game/player/spidey.cpp').read_text(),'i32 CPlayer::GetDamageInflictedFromDifficulty(')+'\n'
 source=(root/'src/game/player/player_web_actions.cpp').read_text()
 for method in ['void PlayerBehaviorController::updateZipDive(', 'void PlayerBehaviorController::updateZipDivePunch(', 'void PlayerBehaviorController::updateZipDiveKick(']:code+=controller_method(source,method).replace('actionWord(PlayerAction::ZipDiveKick)','0x08000000').replace('actionWord(PlayerAction::ZipDivePunch)','0x04000000').replace('actionWord(PlayerAction::Falling)','4')+'\n'
 code+=r'''
struct CThug{unsigned char raw[0x400]={};SStateFlags&field_31C=*reinterpret_cast<SStateFlags*>(raw+0x31C);short&field_314=*reinterpret_cast<short*>(raw+0x314);int&dumbAssPad=*reinterpret_cast<int*>(raw+0x320);unsigned&field_2A8=*reinterpret_cast<unsigned*>(raw+0x2A8);u16 CheckStateFlags(SStateFlags*,int);int AddPointToPath(CVector*p,int n){emit({11,p->vx,p->vy,p->vz,n});return grabResult;}u8 Grab(CVector*);};
SStateFlags*G_THUG_STATE_FLAGS=reinterpret_cast<SStateFlags*>(0x557CA0);
'''
 code+=controller_method((root/'src/game/actors/baddy.cpp').read_text(),'u16 CBaddy::CheckStateFlags(').replace('CBaddy::','CThug::')+'\n'
 code+=controller_method((root/'src/game/actors/thug.cpp').read_text(),'u8 CThug::Grab(')+'\n'
 # Source123 CheckForwards(false) branch extracted with its original thresholds.
 forwards=controller_method((root/'src/game/player/spidey.cpp').read_text(),'i32 CPlayer::CheckForwards(')
 forwards=forwards[:forwards.index('\tif (!bAllowStart)')]+'\treturn 0;\n}'
 forwards=forwards.replace('this->GetEffectiveHeading()','this->heading').replace('G_CAMERA_LIST->field_23A','cameraHeading')
 code+=r'''
struct ForwardPlayer{unsigned char input[320]={};void*field_E0C=input;unsigned mActionState=0x2000000;unsigned char field_8EA=0,field_8E8=0,field_551=5;char field_E2D=0,field_E2E=0;unsigned short mAnim=123,mTargetHeading=0,heading=0;short field_E32=0,field_EA6=77;int mHeadingTurnTicksRemaining=0;void*field_350=nullptr;int CheckForwards(bool);void RunAnim(int,int,int){}void SetTargetTorsoAngle(short n,bool){emit({12,n});}};
struct Camera{short field_23A;};int*G_SPIDEY_SFX_ENTRY[300]={};short cameraHeading=0;
'''+forwards.replace('CPlayer::','ForwardPlayer::')+r'''
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
int __attribute__((thiscall))pathBoundary(void*,CVector*p,int n){emit({11,p->vx,p->vy,p->vz,n});return grabResult;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4048F0,(void*)&pathBoundary);targetVtable[10]=(void*)&grab;*reinterpret_cast<void***>(target.raw)=targetVtable;
 int mode;while(std::cin>>mode){events.clear();if(mode==4){int state,sub;unsigned flags;std::cin>>state>>sub>>flags>>grabResult;CThug source;source.field_31C.bothFlags=state;source.dumbAssPad=sub;source.field_2A8=flags;CVector pos(1,2,3);int success=source.Grab(&pos);auto gold=events;unsigned char pc[0x400]={};*reinterpret_cast<int*>(pc+0x31C)=state;*reinterpret_cast<int*>(pc+0x320)=sub;*reinterpret_cast<unsigned*>(pc+0x2A8)=flags;events.clear();int actual=reinterpret_cast<u8(__attribute__((thiscall))*)(void*,CVector*)>(0x4D38E0)(pc,&pos);if(actual!=success||events!=gold){std::cerr<<"state"<<state<<" result"<<actual<<" source"<<success<<" path"<<grabResult;std::abort();}if(*reinterpret_cast<int*>(pc+0x31C)!=source.field_31C.bothFlags||*reinterpret_cast<int*>(pc+0x320)!=source.dumbAssPad||*reinterpret_cast<unsigned*>(pc+0x2A8)!=source.field_2A8||*reinterpret_cast<short*>(pc+0x314)!=source.field_314){std::cerr<<"state="<<state<<" path="<<grabResult<<" PC="<<*reinterpret_cast<int*>(pc+0x31C)<<','<<*reinterpret_cast<int*>(pc+0x320)<<','<<*reinterpret_cast<unsigned*>(pc+0x2A8)<<','<<*reinterpret_cast<short*>(pc+0x314)<<" SRC="<<source.field_31C.bothFlags<<','<<source.dumbAssPad<<','<<source.field_2A8<<','<<source.field_314;std::abort();}events.insert(events.begin(),{success,source.field_31C.bothFlags,source.dumbAssPad,source.field_2A8,source.field_314});}
 else if(mode==3){ForwardPlayer p;int transition,forward,side,surface,flag;std::cin>>transition>>forward>>side>>p.heading>>p.field_E32>>cameraHeading>>surface>>p.mTargetHeading>>p.mHeadingTurnTicksRemaining>>flag;p.field_8EA=transition;p.field_E2D=forward;p.field_E2E=side;p.field_8E8=surface;p.field_551=flag;p.CheckForwards(false);events.insert(events.begin(),{p.field_551});}
 else{CPlayer p;int animation,frame,completed,health,type,kick,punch,jump,forward,side;std::cin>>animation>>frame>>completed>>G_TIMER_RELATED>>p.field_DE0>>health>>type>>valid>>target.mPos.vx>>target.mPos.vy>>target.mPos.vz>>p.mPos.vx>>p.mPos.vy>>p.mPos.vz>>p.mForwardDirection.vx>>p.mForwardDirection.vy>>p.mForwardDirection.vz>>p.mUpDirection.vx>>p.mUpDirection.vy>>p.mUpDirection.vz>>kick>>punch>>jump>>forward>>side>>grabResult>>jumpResult>>G_CURRENTSUIT>>G_DIFFICULTY_LEVEL>>p.field_528>>p.mVel.vx>>p.mVel.vy>>p.mVel.vz;
 p.mAnim=animation;p.mFrame=frame;target.mHealth=health;target.mType=type;p.input[0x121]=kick;p.input[0x131]=punch;p.input[0x101]=jump;p.field_E2D=forward;p.field_E2E=side;p.mActionState=mode==0?0x2000000:mode==1?0x4000000:0x8000000;PLR_I32(&target,0x2A8)=0x51;PlayerBehaviorController controller(p);controller.completedAnimation_=completed;
 if(mode==0)controller.updateZipDive();else if(mode==1)controller.updateZipDivePunch();else controller.updateZipDiveKick();
 events.insert(events.begin(),{p.mActionState,p.field_EA6,p.field_DD8.pWhatever!=nullptr,p.field_DD8.validation,p.field_DE0,p.field_A80,p.mVel.vx,p.mVel.vy,p.mVel.vz,p.field_534,p.field_52C,p.input[0x121],p.input[0x101],p.field_2C1,(unsigned)PLR_I32(&target,0x2A8)});}
 std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]\n";
 }}
'''
 cases=[]
 def row(mode,anim,frame=0,completed=65535,clock=1000,start=1000,health=100,type=304,valid=1,kick=0,punch=0,jump=0,distance=100,grab=1,jumpResult=0,difficulty=2,suit=0):return[mode,anim,frame,completed,clock,start,health,type,valid,0,0,-distance*4096,0,0,0,0,0,4096,0,-4096,0,kick,punch,jump,0,0,grab,jumpResult,suit,difficulty,5,123,456,789]
 for completed in [120,121,122,65535]:
  for distance in [0,189,190,191,-100]:
   for valid in [0,1]:
    for grab in [0,1]:cases.append(row(0,120,completed=completed,distance=distance,valid=valid,grab=grab))
 for type in [304,314]:
  for difficulty in [0,1,2,3]:
   hold=([420,120,120,60][difficulty] if type==314 else 300)
   for age in [0,hold,hold+1]:
    for kick,punch,jump in [(0,0,0),(1,0,0),(0,1,0),(1,1,0),(0,0,1)]:cases.append(row(0,123,type=type,difficulty=difficulty,clock=1000+age,kick=kick,punch=punch,jump=jump,jumpResult=jump))
 for frame in [15,16,17]:
  for completed in [124,65535]:
   for valid in [0,1]:
    for suit in [0,2]:
     for difficulty in [0,1,2,3]:cases.append(row(1,124,frame,completed,valid=valid,suit=suit,difficulty=difficulty))
 for anim in [125,126,128]:
  for age in [0,300,301]:
   for health in [0,1]:
    for kick,punch,jump in [(0,0,0),(1,0,0),(0,0,1)]:
     for completed in [126,65535]:cases.append(row(2,anim,completed=completed,clock=1000+age,health=health,kick=kick,jump=jump))
 for delta in [0,127,128,1536,2048,3968,3969,4095]:
  for target in [0,63,64,4032,4033]:
   for remaining in [0,10]:cases.append([3,0,1,0,0,delta,0,0,target,remaining,7])
 cases += [row(0,123,clock=2,start=-16), row(2,128,clock=2,start=-16), row(2,128,clock=0x80000000,start=0)]
 cases+=[[3,1,1,0,0,128,0,0,0,0,7],[3,0,0,0,0,128,0,0,0,0,7],[3,0,1,0,0,128,1024,1,0,0,7]]
 for state in range(30):
  for success in [0,1]:cases.append([4,state,7,0x51,success])
 with tempfile.TemporaryDirectory(prefix='zip-dive-source-') as t:
  path=Path(t);(path/'reference.cpp').write_text(code)
  subprocess.run(['g++','-m32','-std=c++17','-O0','-fwrapv','-fno-pie','-no-pie','-mfpmath=sse','-msse2','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(path/'reference.cpp'),'-o',str(path/'reference')],check=True)
  result=subprocess.run([str(path/'reference'),str(a.original_exe)],input='\n'.join(' '.join(map(str,c)) for c in cases),text=True,capture_output=True)
  if result.returncode:
   print(result.stderr);print("Completedlines",len(result.stdout.splitlines()));raise RuntimeError(f"Original comparator failed{result.returncode}")
 (root/'modern/OpenSpidey.Tests/Fixtures/player-zip-dive.json').write_text(json.dumps({'compiledSourceActionsVerified':True,'originalPcThugGrabVerified':True,'cases':[{'input':c,'events':json.loads(s)} for c,s in zip(cases,result.stdout.splitlines(),strict=True)]},separators=(',',':'))+'\n')
 print(f'Compiled ZipDive/forward slices and original PC thugGrab: {len(cases)} cases.')
if __name__=='__main__':main()
