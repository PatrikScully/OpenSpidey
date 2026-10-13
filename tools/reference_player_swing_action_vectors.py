"""Compile complete original100/200/400 swing branches with exact typed world/action observers."""
import json,random,subprocess,tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent;source=(root/'src/game/player/player_web_actions.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <cstring>
#include <new>
#include <cmath>
using u8=unsigned char;using u16=unsigned short;using u32=unsigned;using i32=int;using i16=short;
using VECTOR=CVector;void print_if_false(bool,const char*){}
std::vector<long long>events;void emit(std::initializer_list<long long>v){events.insert(events.end(),v);}
int ceiling,smash,zip,flail,practice,complete,hitAt,hitY,hitFlags,probe,fireVariant;unsigned G_TIMER_RELATED;int*gPracticeDifficultyFlag=&practice;
struct MATRIX{short m[3][3]={};short reserved=0;int t[3]={};};
struct SLineInfo{CVector StartCoords,EndCoords,MinCoords,MaxCoords,Position;CSVector Normal;int distance=0;void*pItem=nullptr;unsigned*pFace=nullptr;};unsigned face[4]={};
void M3dColij_InitLineInfo(SLineInfo*p){p->pItem=nullptr;}
void M3dZone_LineToItem(SLineInfo*p,int flags){emit({15,p->StartCoords.vx,p->StartCoords.vy,p->StartCoords.vz,p->EndCoords.vx,p->EndCoords.vy,p->EndCoords.vz,flags});if(++probe==hitAt){p->pItem=reinterpret_cast<void*>(1);p->Position=CVector(123456,-234567,345678);p->Normal=CSVector(3500,hitY,-900);face[3]=hitFlags;p->pFace=face;}}
int Rnd(int n){emit({12,n});return 2%n;}void SFX_Play(int sound,int volume,int pitch){emit({13,sound,volume,pitch});}
void SFX_PlayPos(int sound,CVector*p,int pitch){emit({2,sound,p->vx,p->vy,p->vz,pitch});}
CVector ZeroVector;
unsigned Utils_Dist(const CVector&a,const CVector&b){CVector d=(a-b)>>12;int squared=d.vx*d.vx+d.vy*d.vy+d.vz*d.vz;return squared<=-32768?32768:squared<0?0:unsigned(sqrt(double(squared)));}
void VectorNormal(VECTOR*p,VECTOR*out){long double x=float(p->vx),y=float(p->vy),z=float(p->vz);float squared=x*x+y*y+z*z;float length=sqrt(double(squared));if(!length){out->vx=0;out->vy=4096;out->vz=0;return;}out->vx=long(double(float(p->vx<<12))/length);out->vy=long(double(float(p->vy<<12))/length);out->vz=long(double(float(p->vz<<12))/length);}
struct CBody{CVector mVel;virtual~CBody(){emit({10});}static void*operator new(unsigned n){emit({7,n});return ::operator new(n);}static void operator delete(void*p){::operator delete(p);}};using CItem=CBody;
struct CPlayer;CPlayer*activePlayer;
struct CSwinger:CBody{int fire=0;int phase=2048;int IsOneTimeToDie(){return phase>=4096;}void SetSpideyAnimFrame(int);};
void CSwinger_SwingBack(CSwinger*){emit({9});}
void gCSwinger_ctor(void*p,CVector*a,int length,MATRIX*,CVector*n){::new(p)CSwinger();emit({8,a->vx,a->vy,a->vz,length,n->vx,n->vy,n->vz});}
struct Camera{int field_12C=44;}camera;Camera*G_CAMERA_LIST=&camera;
unsigned char animationTable[0x2000]={};void*Animations[17]={animationTable};
namespace spidey{namespace player{
enum class PlayerAction:unsigned{SwingRelease=0x200,Swinging=0x400,Falling=4,Idle=1,KnockdownRecovery=0x800000};unsigned actionWord(PlayerAction a){return unsigned(a);}
struct CPlayer{int mFrame=12,mAnim=0x111,mAnimSpeed=42,mRegion=0;short field_EA6=77;u8 field_54C=23,field_54E=24,field_550=25,field_8E8=26,field_AE5=27,field_E8D=28,field_5E8=0;int mIsCrawling=0,mFenceSurfaceDetected=0,mSynthesizedInputActive=0,mPendingWebReleaseAnimation=3;unsigned field_504=0,field_500=0,mActionState=0;int field_E80=111,field_E84=222,field_E88=333,field_E20=0,field_EBC=99;char field_E2D=9,field_E2E=10;CBody*mSupportBody=nullptr;CVector mPos{12345,-54321,67890},mVel{12345,-54321,67890},field_A8{111,-222,333},field_DAC,field_D64{100000,-200000,300000},field_D70{150000,-250000,350000},field_DA0{0,-4096,0},mForwardDirection{4096,0,0},mOrientationReferenceDirection;int*field_E64=nullptr;u8 field_D60=0;int length=0;MATRIX field_D80;u16 mSurfaceClearance=96;SLineInfo mLineInfo;u8 buttons[0x140]={};void*field_E0C=buttons;
void PlaySingleAnim(int a,int first,int last){emit({1,a,first,last});mAnim=a;mFrame=0;}
void PutCameraBehind(int n){emit({3,n});}
int ShouldPlayerDropFlail(){emit({4});return flail;}
int CheckStickToCeiling(){emit({5});return ceiling;}
int CheckJumpingSmashKick(){emit({16});return smash;}
int CheckJumpingR1ZipWeb(){emit({17});return zip;}
int DecreaseWebbing(int n){emit({6,n});return 0;}
void CalculateSwingWebParameters(CVector*a){emit({11,a->vx,a->vy,a->vz});}
void OrientToNormal(bool target,CVector*v){emit({14,target,v->vx,v->vy,v->vz});}
void LockTargetTorsoAngle(){emit({18});}
void TidyUpZipWebLandingPosition(int n){emit({19,n});}
int CheckFenceSurfaceTransition(){emit({20});return 1;}
};
struct PlayerBehaviorController{CPlayer&player_;void updateSwingWindup();void updateSwingRelease();void updateSwinging();};
#define PLR_I32(p,o) ((p)->length)
#define PLR_U8(p,o) ((p)->field_54E)
'''
    code=code.replace('struct CPlayer;CPlayer*activePlayer;', 'namespace spidey{namespace player{struct CPlayer;}}spidey::player::CPlayer*activePlayer;')
    for signature in ['void PlayerBehaviorController::updateSwingWindup(', 'void PlayerBehaviorController::updateSwingRelease(', 'void PlayerBehaviorController::updateSwinging(']:code+=controller_method(source,signature)+'\n'
    code+=r'''
}}
void CSwinger::SetSpideyAnimFrame(int scale){activePlayer->mFrame=short((scale*(phase-2048))>>11);emit({21,activePlayer->mFrame});}
void M3dUtils_GetHookPosition(VECTOR*out,spidey::player::CPlayer*,int hook){emit({22,hook});*out=CVector(123456,-234567,345678);}
int main(){using namespace spidey::player;long long v[26];while(std::cin>>v[0]){for(int i=1;i<26;i++)std::cin>>v[i];CPlayer p;CBody support;p.mActionState=v[0]==0?0x100:v[0]==1?0x200:0x400;p.mFrame=v[1];p.field_DAC=v[2]?CVector(-123456,0,345678):CVector();p.field_E20=v[3];p.field_504=v[4];G_TIMER_RELATED=v[5];p.field_500=v[6];p.mVel.vy=v[7];p.mSupportBody=v[8]?&support:nullptr;support.mVel.vy=v[9];flail=v[10];ceiling=v[11];smash=v[12];zip=v[13];CSwinger*initial=v[14]?new CSwinger():nullptr;if(initial)initial->phase=v[15]?4288:2048;p.field_E64=reinterpret_cast<int*>(initial);p.field_D60=v[16];p.mCollision=v[17];p.mLineInfo.pItem=v[18]?reinterpret_cast<void*>(1):nullptr;p.mLineInfo.Normal=CSVector(3500,v[19],-900);face[3]=v[20];p.mLineInfo.pFace=face;practice=v[21];p.mSynthesizedInputActive=v[22];for(int i=0;i<4;i++)p.buttons[0x101+i*16]=(v[23]>>i)&1;hitAt=v[24];hitY=v[19];hitFlags=v[20];p.field_5E8=v[25];probe=0;camera.field_12C=44;activePlayer=&p;for(int a=0;a<400;a++)*reinterpret_cast<int*>(animationTable+8*a+8)=25;events.clear();PlayerBehaviorController c{p};if(v[0]==0)c.updateSwingWindup();else if(v[0]==1)c.updateSwingRelease();else c.updateSwinging();auto*w=reinterpret_cast<CSwinger*>(p.field_E64);
std::cout<<'['<<p.field_EA6<<','<<int(p.field_54C)<<','<<p.field_E80<<','<<p.field_E84<<','<<p.field_E88<<','<<p.mActionState<<','<<p.field_E20<<','<<p.mVel.vx<<','<<p.mVel.vy<<','<<p.mVel.vz<<','<<p.mPos.vx<<','<<p.mPos.vy<<','<<p.mPos.vz<<','<<p.field_A8.vx<<','<<p.field_A8.vy<<','<<p.field_A8.vz<<','<<int(p.field_8E8)<<','<<p.mPendingWebReleaseAnimation<<','<<int(p.field_54E)<<','<<int(p.field_550)<<','<<int(p.field_AE5)<<','<<p.mIsCrawling<<','<<p.mFenceSurfaceDetected<<','<<int(p.field_E8D)<<','<<int(p.field_E2D)<<','<<int(p.field_E2E)<<','<<p.field_EBC<<','<<p.mFrame<<','<<p.mAnim<<','<<p.mAnimSpeed<<','<<int(p.field_D60)<<','<<camera.field_12C<<','<<(w?1:0)<<','<<p.length<<','<<p.mOrientationReferenceDirection.vx<<','<<p.mOrientationReferenceDirection.vy<<','<<p.mOrientationReferenceDirection.vz<<','<<p.mLineInfo.Normal.vx<<','<<p.mLineInfo.Normal.vy<<','<<p.mLineInfo.Normal.vz<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    # Hook routine declaration and actual collision byte belong to the fixture player.
    code=code.replace('struct PlayerBehaviorController{CPlayer&player_;', 'void M3dUtils_GetHookPosition(VECTOR*,CPlayer*,int);\nstruct PlayerBehaviorController{CPlayer&player_;')
    code=code.replace('u16 mSurfaceClearance=96;', 'u16 mSurfaceClearance=96,mCollision=0;')
    code=code.replace('void M3dUtils_GetHookPosition(VECTOR*out,spidey::player::CPlayer*,int hook)', 'void spidey::player::M3dUtils_GetHookPosition(VECTOR*out,CPlayer*,int hook)')
    rng=random.Random(0x4B32A1);cases=[]
    for mode in range(3):
        for _ in range(300):
            cases.append([mode,rng.choice([12,13,14]),rng.randrange(2),rng.choice([0,1,2]),rng.choice([0,0x200,0x400,0x600]),
                rng.choice([0,5,6,100,4294967295]),rng.choice([0,99,4294967292]),rng.choice([-1,0,1]),rng.randrange(2),12345,
                rng.randrange(2),rng.randrange(2),rng.randrange(2),rng.randrange(2),rng.randrange(2),rng.randrange(2),rng.randrange(2),
                rng.choice([0,1,2,3]),rng.randrange(2),rng.choice([-32768,-2601,-2600,0,3400,3401,32767]),rng.choice([0,0x40000,0x8040000]),
                rng.randrange(2),rng.randrange(2),rng.randrange(16),rng.choice([0,1,2,3]),rng.choice([0,1,255])])
    with tempfile.TemporaryDirectory(prefix='openspidey-swing-actions-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-swing-actions.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} complete actual-source100/200/400 swing branch cases; numerical swing/basis/physics independently PC-verified.')

if __name__=='__main__':main()
