"""Compile original ceiling/fence activation and complete2000 transition with source pose/world/camera boundaries."""
import json,random,subprocess,tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent;player=(root/'src/game/player/spidey.cpp').read_text();actions=(root/'src/game/player/player_locomotion_actions.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
using VECTOR=CVector;using i32=int;using i16=short;using u16=unsigned short;using u8=unsigned char;
std::vector<long long>events;void emit(std::initializer_list<long long>v){events.insert(events.end(),v);}
struct Frame{short value=12;Frame&operator=(int v){value=v;emit({5,value});return *this;}operator short()const{return value;}};
int G_UNK_POSE=1,frameCount=25,groundAt=0,groundCalls=0;
int words[4]={65543,131081,-1,0};int*G_SPIDEY_SFX_ENTRY[300]={};
struct SoundSlot{int*value=nullptr;SoundSlot&operator=(int*p){value=p;emit({3,93});return *this;}};
unsigned char animationTable[0x2000]={};struct Tables{void*operator[](int){emit({4,93});return animationTable;}}Animations;
struct SLineInfo{void*pItem=nullptr;CVector Position{123456,-234567,345678};CSVector Normal{3500,0,-900};unsigned*pFace=nullptr;};unsigned face[4]={};
struct Camera{int field_12C=44;void SetTripodMotion(CVector p,unsigned time){emit({8,p.vx,p.vy,p.vz,time});}}camera;Camera*G_CAMERA_LIST=&camera;
namespace spidey{namespace player{
enum class PlayerAction:unsigned{Idle=1,GroundMovement=16};unsigned actionWord(PlayerAction a){return unsigned(a);}
struct CPlayer{CVector mPos{12345,-54321,67890},mVel{12345,0,67890},mForwardDirection{4096,0,0},mUpDirection{11,-4096,13},mRightDirection{0,0,4096},field_A8{111,-222,333},mOrientationReferenceDirection{1,2,3};int mIsCrawling=1,mAnim=93,mRegion=0,mHeadingTurnTicksRemaining=99,field_E84=222,field_E88=333,slide=0;unsigned mActionState=0x400;u16 mCollision=0,mSurfaceClearance=96;short field_EA6=77;u8 field_54C=23,field_AE5=24,field_8E8=1,field_8E9=1,mFenceSurfaceDetected=1,field_AD6=6,field_AD8=7,field_AD9=8,field_ADA=1,field_ADC=1;Frame mFrame;SoundSlot field_350;SLineInfo mLineInfo,mLineInfo2;u8 buttons[0x140]={};void*field_E0C=buttons;
int CheckStickToCeiling();int CheckFenceSurfaceTransition();
void OrientToNormal(bool target,CVector*p){emit({1,target,p->vx,p->vy,p->vz});}
void PlaySingleAnim(int a,int first,int last){emit({2,a,first,last});mAnim=a;mFrame.value=0;}
void RunAnim(int a,int first,int last){emit({7,a,first,last});mAnim=a;mFrame.value=0;}
void ApplyPose(int pose){emit({6,pose,mAnim,mFrame.value});}
void SetFloorCamera(int time){emit({12,time});}
void LockTargetTorsoAngle(){emit({13});}
void TidyUpZipWebLandingPosition(int n){emit({9,n});}
void SwitchToStandMode(){emit({11});mActionState=1;}
};
void M3dUtils_GetHookPosition(VECTOR*out,CPlayer*,int hook){emit({14,hook});*out=CVector(123456,-234567,345678);}
void SFX_Play(int sound,int volume,int pitch){emit({15,sound,volume,pitch});}
void SFX_PlayPos(int sound,CVector*p,int pitch){emit({16,sound,p->vx,p->vy,p->vz,pitch});}
int Utils_GetGroundHeight(CVector*p,int above,int below,int flag){emit({10,p->vx,p->vy,p->vz,above,below,flag});return ++groundCalls==groundAt?-400000:-1;}
struct PlayerBehaviorController{CPlayer&player_;int completedAnimation_;void updateSurfaceTransition();};
#define PLR_I32(p,o) ((p)->slide)
'''
    code+=controller_method(player,'i32 CPlayer::CheckStickToCeiling(').replace('this->field_A8 = this->mLineInfo2.Normal;', 'this->field_A8 = CVector(this->mLineInfo2.Normal.vx,this->mLineInfo2.Normal.vy,this->mLineInfo2.Normal.vz);')+'\n'+controller_method(player,'i32 CPlayer::CheckFenceSurfaceTransition(')+'\n'+controller_method(actions,'void PlayerBehaviorController::updateSurfaceTransition(')+r'''
}}
int main(){using namespace spidey::player;long long v[17];while(std::cin>>v[0]){for(int i=1;i<17;i++)std::cin>>v[i];CPlayer p;p.mVel.vy=v[1];p.mCollision=v[2];p.mLineInfo2.pItem=v[3]?reinterpret_cast<void*>(1):nullptr;p.buttons[0x100]=v[4];p.mLineInfo2.Normal.vy=v[5];face[3]=v[6];p.mLineInfo2.pFace=face;p.mAnim=v[7];p.mActionState=v[8];p.mFenceSurfaceDetected=v[9];p.field_8E8=v[10];p.field_8E9=v[11];p.mIsCrawling=v[12];p.mLineInfo.Normal.vy=v[5];frameCount=v[13];*reinterpret_cast<int*>(animationTable+8*93+8)=frameCount;G_SPIDEY_SFX_ENTRY[93]=words;p.slide=v[14];groundAt=v[16];groundCalls=0;camera.field_12C=44;events.clear();int result=-1;if(v[0]==0)result=p.CheckStickToCeiling();else if(v[0]==1)result=p.CheckFenceSurfaceTransition();else{PlayerBehaviorController c{p,int(v[15])};c.updateSurfaceTransition();}
std::cout<<'['<<result<<','<<p.mActionState<<','<<p.mAnim<<','<<p.mFrame.value<<','<<p.mPos.vx<<','<<p.mPos.vy<<','<<p.mPos.vz<<','<<p.field_A8.vx<<','<<p.field_A8.vy<<','<<p.field_A8.vz<<','<<p.mOrientationReferenceDirection.vx<<','<<p.mOrientationReferenceDirection.vy<<','<<p.mOrientationReferenceDirection.vz<<','<<p.mIsCrawling<<','<<int(p.field_54C)<<','<<int(p.field_AE5)<<','<<int(p.field_8E8)<<','<<int(p.field_8E9)<<','<<p.field_E84<<','<<p.field_E88<<','<<camera.field_12C<<','<<p.mHeadingTurnTicksRemaining<<','<<p.mLineInfo.Normal.vx<<','<<p.mLineInfo.Normal.vy<<','<<p.mLineInfo.Normal.vz<<','<<int(p.field_AD6)<<','<<int(p.field_AD8)<<','<<int(p.field_AD9)<<','<<p.slide<<','<<p.field_EA6<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    rng=random.Random(0x4BE8C0);cases=[]
    for mode in range(3):
        for _ in range(250):
            cases.append([mode,rng.choice([-1,0,1]),rng.choice([0,0x100,0x101]),rng.randrange(2),rng.randrange(2),
                rng.choice([-32768,-2601,-2600,0,3400,3401,32767]),rng.choice([0,0x40000]),rng.choice([93,232,273]),
                rng.choice([1,0x100,0x200,0x300,0x400,0x2000]),rng.randrange(2),rng.randrange(2),rng.randrange(2),rng.randrange(2),
                rng.choice([1,25,65537,65535]),rng.choice([-8,0,1,7,8,9,100]),rng.choice([65535,0x3B,0x3F,0x56,0x5D,0x99]),rng.choice([0,1,2,3,10,19])])
    # Ensure every real ceiling predicate has a positive complete path.
    cases += [[0,-1,0x100,1,1,3500,0,animation,action,1,1,1,0,25,0,65535,0] for animation in [93,232,273] for action in [1,0x100,0x200,0x300,0x400]]
    with tempfile.TemporaryDirectory(prefix='openspidey-surface-transition-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-surface-transitions.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} complete original ceiling/fence/source2000 cases, including real masked frame-count/pose camera order and allground recovery branches.')

if __name__=='__main__':main()
