"""Compare recovered flaming impact-web constructor/Move and fiery sparks with actual PC instructions."""
import argparse
import json
import random
import shutil
import subprocess
import tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    simby = (root / 'src/game/actors/simby.cpp').read_text()
    utils = (root / 'src/core/utils.cpp').read_text()
    maths = (root / 'src/engine/rendering/ps2funcs.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <cmath>
#include <iostream>
#include <vector>
#include <sstream>
#include <cassert>
#include <cstring>
#include <cstdlib>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
using VECTOR=CVector;using SVECTOR=CSVector;
struct SSinCos{short sin,cos;}G_RCOSSIN_TBL[4096];
VECTOR G_OP12_RESULT,G_GENERAL_LONG_VECTOR,G_WTF_OP12,G_FTW_OP12;
void gte_stlvnl(VECTOR*v){*v=G_GENERAL_LONG_VECTOR;}
void print_if_false(bool,const char*){}void DoAssert(bool,const char*){}
std::vector<long long>events;void emit(std::initializer_list<long long>values){events.insert(events.end(),values);}
int G_TIMER_RELATED,G_LINE_OF_SIGHT_CHECK,randomIndex,initialHit,initialDistance,bodyFrame,updateIndex;
int Rnd(int bound){int value=(randomIndex++*7+1)%bound;emit({1,bound,value});return value;}
struct SHitInfo{unsigned char flags=0,part=0;unsigned short pad=0;int kind=0;unsigned short damage=0,padA=0;CVector direction;short slide=0;unsigned short frames=0;};
struct CItem{void**table=nullptr;unsigned short mFlags=0,pad=0;};
struct CBody:CItem{void Hit(SHitInfo*h){emit({7,h->flags,h->kind,h->damage,h->direction.vx,h->direction.vy,h->direction.vz});}};
CBody environment,body;
struct SLineInfo{CVector StartCoords,EndCoords;char pad18[0x28]={};int Distance=0,Length=0;char matrix[0x20]={};CItem*pItem=nullptr;CVector Position;CSVector Normal;unsigned short pad7E=0;unsigned*pFace=nullptr;}G_LINE_INFO;
static_assert(offsetof(SLineInfo,pItem)==0x68);static_assert(offsetof(SLineInfo,Distance)==0x40);
unsigned face[4]={};CVector hitPosition{12345,-54321,67890};CSVector hitNormal{0,-4096,0};
void M3dColij_InitLineInfo(SLineInfo*l){l->pItem=nullptr;}
void M3dZone_LineToItem(SLineInfo*l,int mode){emit({3,mode,G_LINE_OF_SIGHT_CHECK,l->StartCoords.vx,l->StartCoords.vy,l->StartCoords.vz,l->EndCoords.vx,l->EndCoords.vy,l->EndCoords.vz});
l->pItem=initialHit?&environment:nullptr;l->Distance=initialDistance;l->pFace=face;l->Position=hitPosition;l->Normal=hitNormal;}
CBody*Utils_CheckObjectCollision(CVector*a,CVector*b,void*,void*){emit({4,a->vx,a->vy,a->vz,b->vx,b->vy,b->vz});return updateIndex==bodyFrame?&body:nullptr;}void*G_BADDY_LIST=nullptr;
void SFX_PlayPos(unsigned sound,CVector*p,int pitch){emit({5,sound,p->vx,p->vy,p->vz,pitch});}
struct CSwitch{unsigned char pad[0x100]={};int field_100=1;void Flick(){emit({9});}}control;
CSwitch*Switch_GetCSwitchObjectFromItem(CItem*){emit({8});return &control;}
int Web_GetGroundY(const CVector*p){emit({10,p->vx,p->vy,p->vz});return p->vy+0x1388000;}
struct CBit{unsigned vtable=0,previous=0,next=0;short mAge=0;unsigned short mLifetime=0;CVector mPos,mVel,mAcc;unsigned char friction[3]={1,1,1},mDead=0;unsigned short mFrigDeltaZ=0;unsigned char protectedBit=0,mType=0;void Die(){mDead=1;}};
struct CFlatBit:CBit{short mTransDecay=0;unsigned short field_3E=0;unsigned mCodeBGR=0x2C808080;unsigned deleteAnim=0;void*mpPSXAnim=nullptr,*mpPSXFrame=nullptr;unsigned char flags=0,mNumFrames=2; signed char mFrame=0;unsigned char frac=0;short speed=128,mScale=400,mAngle=0,field_5A=0;unsigned short pad5C=0,frictionAngle=1;unsigned mPostScale=0x10001000;unsigned char pad64=0,semi=32;unsigned short clut=0;
void SetAnim(int){}void SetSemiTransparent(){mCodeBGR|=0x02000000;}void SetTransparency(unsigned char value){mCodeBGR=(mCodeBGR&0xFF000000)|value|(value<<8)|(value<<16);}void SetScale(unsigned short value){mScale=value;}};
struct CFlamingImpactWeb:CFlatBit{int field_68=0,field_6C=0,field_70=0;CItem*pItem=nullptr;unsigned*pFace=nullptr;CVector mLinePos;CSVector mLineNormal;unsigned short pad8E=0;
CFlamingImpactWeb(CVector*,CSVector*,int);void Move();};
struct CPixel:CBit{unsigned tag=0;unsigned char r0=0,g0=0,b0=0,code=0;unsigned mWidthHeight=0;};
struct CFireySpark:CPixel{int field_48=0,field_4C=0;CFireySpark(CVector*,CVector*,int);void Move();};
static_assert(sizeof(CBit)==0x3C);static_assert(sizeof(CFlatBit)==0x68);static_assert(sizeof(CFlamingImpactWeb)==0x90);static_assert(sizeof(CFireySpark)==0x50);
struct CMotionBlur:CFlatBit{CMotionBlur(CVector*p,CVector*v,int animation,int scale,int decay,int colour){emit({11,p->vx,p->vy,p->vz,v->vx,v->vy,v->vz,animation,scale,decay,colour});mScale=scale;}};
void emitSpark(CVector position,CVector velocity,int ground){emit({12,position.vx,position.vy,position.vz,velocity.vx,velocity.vy,velocity.vz,ground});CFireySpark spark(&position,&velocity,ground);}
'''
    for signature in ['i32 M3dMaths_SquareRoot0(', 'void gte_ldlvl(', 'void gte_ldopv1(',
                      'void gte_ldopv2(', 'void gte_op12(', 'void gte_sqr0(', 'void VectorNormal(']:
        code += controller_method(maths, signature) + '\n'
    code += controller_method((root / 'src/compatibility/fixed_vector_bridge.cpp').read_text(), 'int CVector::Length(') + '\n'
    code += controller_method(utils, 'void Utils_CalcPerps(') + '\n'
    code += controller_method(utils, 'void Utils_GetVecFromMagDir(') + '\n'
    code += controller_method(simby, 'CFireySpark::CFireySpark(') + '\n'
    code += controller_method(simby, 'void CFireySpark::Move(') + '\n'
    code += controller_method(simby, 'CFlamingImpactWeb::CFlamingImpactWeb(') + r'''
// Direct translation of actual PC4A40D0, checked below against those original instructions.
void CFlamingImpactWeb::Move(){if(field_68)field_68--;else{field_68=1;CVector velocity=(mVel*128)>>8;
 auto*trail=new CMotionBlur(&mPos,&velocity,16,mScale,100,20);trail->mAngle=Rnd(4096);delete trail;}
mScale+=1500;if(mScale>1000)mScale=1000;mAngle+=field_5A;
CVector previous=mPos;int elapsed=G_TIMER_RELATED-field_70;field_70=G_TIMER_RELATED;mPos+=mVel*elapsed;
CBody*target=Utils_CheckObjectCollision(&previous,&mPos,G_BADDY_LIST,nullptr);int mode=0;
if(target)mode=2;else{mAge+=elapsed;if(mAge>=mLifetime)mode=pItem?3:1;}
if(mode==0)return;
if(mode==2){SHitInfo hit;CVector direction=mVel>>12;VectorNormal(&direction,&direction);hit.flags=14;hit.kind=7;hit.damage=field_6C;hit.direction=direction;
 target->Hit(&hit);SFX_PlayPos(16,&mPos,0);}
else if(mode==3&&(pFace[3]&0x02000000)){auto*s=Switch_GetCSwitchObjectFromItem(pItem);if(s&&s->field_100)s->Flick();}
CVector normal=mode==2?CVector(0,-4096,0):mode==3?CVector(mLineNormal.vx,mLineNormal.vy,mLineNormal.vz):CVector();
CVector first,second;Utils_CalcPerps(&normal,&first,&second);CVector offset=mPos+normal*100;int ground=Web_GetGroundY(&offset);
for(int i=0;i<20;i++){int angle=(Rnd(1601)-1824)&4095;auto sc=G_RCOSSIN_TBL[angle];CVector velocity=(first*sc.cos+second*sc.sin)>>12;
 angle=Rnd(512)&4095;sc=G_RCOSSIN_TBL[angle];velocity=(normal*sc.sin+velocity*sc.cos)>>12;velocity*=Rnd(40)+30;emitSpark(mPos,velocity,ground);}Die();}
void vec(CVector value){std::cout<<value.vx<<','<<value.vy<<','<<value.vz;}
void output(CFlamingImpactWeb&s){std::cout<<'[';vec(s.mPos);std::cout<<',';vec(s.mVel);std::cout<<','<<s.field_6C<<','<<unsigned(s.field_70)<<','<<s.mLifetime<<','<<s.mAge<<','<<s.mScale<<','<<s.mAngle<<','<<s.field_5A<<','<<s.field_68<<','<<s.mCodeBGR<<','<<int(s.mDead)<<",[";
for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}
std::string capture(CFlamingImpactWeb&s){std::ostringstream text;auto*old=std::cout.rdbuf(text.rdbuf());output(s);std::cout.rdbuf(old);events.clear();return text.str();}
#ifdef VALIDATE_ORIGINAL
std::vector<void*>allocations;bool sparkCall=false;
void patch(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
void*allocate(unsigned bytes){void*p=calloc(1,bytes);allocations.push_back(p);return p;}
void __attribute__((thiscall)) flatConstructor(void*p){*static_cast<CFlatBit*>(p)=CFlatBit();}
void __attribute__((thiscall)) setAnim(void*,int){}
void* __attribute__((thiscall)) constructBlur(void*p,CVector*position,CVector*velocity,int animation,int scale,int decay,int colour){
 emit({11,position->vx,position->vy,position->vz,velocity->vx,velocity->vy,velocity->vz,animation,scale,decay,colour});static_cast<CFlatBit*>(p)->mScale=scale;return p;}
void __attribute__((thiscall)) constructSpark(void*p,CVector*position,CVector*velocity,int ground){emitSpark(*position,*velocity,ground);}
void __attribute__((thiscall)) hitBody(void*,SHitInfo*hit){body.Hit(hit);}
#endif
int main(int argc,char**argv){for(int i=0;i<4096;i++){double angle=i*0.001536096911877394;G_RCOSSIN_TBL[i]={short(std::sin(angle)*4096),short(std::cos(angle)*4096)};}
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);std::memcpy(reinterpret_cast<void*>(0x610C48),G_RCOSSIN_TBL,sizeof(G_RCOSSIN_TBL));
patch(0x40DA90,reinterpret_cast<void*>(&flatConstructor));patch(0x408CF0,reinterpret_cast<void*>(&setAnim));patch(0x4088A0,reinterpret_cast<void*>(&allocate));
patch(0x40FA80,reinterpret_cast<void*>(&constructBlur));patch(0x4A3CB0,reinterpret_cast<void*>(&constructSpark));patch(0x4E5DA0,reinterpret_cast<void*>(&Rnd));
patch(0x4524C0,reinterpret_cast<void*>(&M3dColij_InitLineInfo));patch(0x4549A0,reinterpret_cast<void*>(&M3dZone_LineToItem));
patch(0x4E6470,reinterpret_cast<void*>(&Utils_CheckObjectCollision));patch(0x471EA0,reinterpret_cast<void*>(&SFX_PlayPos));
patch(0x4D1490,reinterpret_cast<void*>(&Switch_GetCSwitchObjectFromItem));patch(0x4D1870,reinterpret_cast<void*>(&CSwitch::Flick));
patch(0x4F54A0,reinterpret_cast<void*>(&Web_GetGroundY));
for(auto range:std::vector<std::pair<unsigned,int>>{{0x4A3E77,6},{0x4A3E7E,7},{0x4A4084,7},{0x4A40D0,6},{0x4A40DE,7},{0x4A45BF,7}})std::memset(reinterpret_cast<void*>(range.first),0x90,range.second);
void*bodyTable[4]={nullptr,nullptr,nullptr,reinterpret_cast<void*>(&hitBody)};body.table=bodyTable;
#endif
CVector position;CSVector direction;int damage,seed,enabled;unsigned initialClock,delta;
while(std::cin>>position.vx>>position.vy>>position.vz>>direction.vx>>direction.vy>>direction.vz>>damage>>initialClock>>initialHit>>initialDistance>>face[3]>>bodyFrame>>enabled>>delta>>seed){
control.field_100=enabled;randomIndex=seed;G_TIMER_RELATED=int(initialClock);events.clear();CFlamingImpactWeb source(&position,&direction,damage);
std::vector<std::string>states{capture(source)};for(updateIndex=0;updateIndex<9;updateIndex++){G_TIMER_RELATED=int(initialClock+unsigned(updateIndex+1)*delta);if(!source.mDead)source.Move();states.push_back(capture(source));}
#ifdef VALIDATE_ORIGINAL
randomIndex=seed;*reinterpret_cast<unsigned*>(0x6B4CA8)=initialClock;events.clear();
alignas(4)unsigned char raw[0x90]={};auto&actual=*reinterpret_cast<CFlamingImpactWeb*>(raw);
// Original helpers access the original global line/LOS storage; redirect the host observer to those values.
reinterpret_cast<void(__attribute__((thiscall)) *)(void*,CVector*,CSVector*,int)>(0x4A3E70)(raw,&position,&direction,damage);
auto compare=[&](int index){auto value=capture(actual);if(value!=states[index]){std::cerr<<"Mismatch flaming stage"<<index<<"\nsource="<<states[index]<<"\nactual="<<value<<'\n';abort();}};compare(0);
for(updateIndex=0;updateIndex<9;updateIndex++){*reinterpret_cast<unsigned*>(0x6B4CA8)=initialClock+unsigned(updateIndex+1)*delta;if(!actual.mDead)reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4A40D0)(raw);compare(updateIndex+1);}
for(void*memory:allocations)free(memory);allocations.clear();
#endif
std::cout<<'[';for(unsigned i=0;i<states.size();i++){if(i)std::cout<<',';std::cout<<states[i];}std::cout<<"]\n";}}
'''
    # The patched host needs the mapped LOS bit on PC calls; the source calls use their local observer value.
    code = code.replace('int Rnd(int bound)', 'int Rnd(int bound)')
    code = code.replace('void SFX_PlayPos(unsigned', 'void SFX_PlayPos(unsigned')
    # Flick's source ABI is thiscall; a static thunk preserves that ABI at the original virtual address.
    code = code.replace('void __attribute__((thiscall)) hitBody(void*,SHitInfo*hit){body.Hit(hit);}',
                        'void __attribute__((thiscall)) hitBody(void*,SHitInfo*hit){body.Hit(hit);}\n'
                        'void __attribute__((thiscall)) flickSwitch(void*){control.Flick();}')
    code = code.replace('reinterpret_cast<void*>(&CSwitch::Flick)', 'reinterpret_cast<void*>(&flickSwitch)')
    code = code.replace('emit({3,mode,G_LINE_OF_SIGHT_CHECK,',
                        'emit({3,mode,\n#ifdef VALIDATE_ORIGINAL\n'
                        '(l==reinterpret_cast<SLineInfo*>(0x5FBE38)?*reinterpret_cast<int*>(0x5FBEE4):G_LINE_OF_SIGHT_CHECK),\n'
                        '#else\nG_LINE_OF_SIGHT_CHECK,\n#endif\n')
    rng = random.Random(0x4A40D0)
    cases = [[12345, -54321, 67890, pitch, yaw, 0, damage, clock, env, distance,
              face_flags, body_frame, enabled, delta, seed]
             for pitch, yaw, damage, clock, env, distance, face_flags, body_frame, enabled, delta, seed in
             [(0, 0, 50, 100, 0, 0, 0, -1, 1, 2, 0),
              (1024, 0, 50, 100, 1, 96, 0x02000000, -1, 1, 2, 1),
              (512, 256, 65530, 4294967290, 0, 0, 0, 0, 1, 2, 3),
              (0, 0, 50, 100, 1, 0, 0x02000000, -1, 0, 0, 0)]]
    for _ in range(92):
        cases.append([*[rng.randint(-50000000, 50000000) for _ in range(3)], rng.randrange(4096),
                      rng.randrange(4096), 0, rng.choice([0, 50, 65530]), rng.choice([100, 4294967290]),
                      rng.randrange(2), rng.choice([0, 32, 96, 320, 10000]), rng.choice([0, 0x02000000]),
                      rng.choice([-1, 0, 1, 3]), rng.randrange(2), rng.choice([0, 1, 2, 40, 40000]), rng.randrange(5)])
    with tempfile.TemporaryDirectory(prefix='openspidey-flaming-web-') as directory:
        cpp = Path(directory) / 'reference.cpp'
        binary = Path(directory) / 'reference'
        cpp.write_text(code)
        flags = ['-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', *flags,
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp),
                        str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        command = [str(binary)] + ([str(args.original_exe.resolve())] if args.original_exe else [])
        inputs = ('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()
        try:
            lines = subprocess.check_output(command, input=inputs).decode().splitlines()
        except subprocess.CalledProcessError:
            shutil.copyfile(cpp, '/tmp/openspidey-flaming-web-reference.cpp')
            shutil.copyfile(binary, '/tmp/openspidey-flaming-web-reference')
            Path('/tmp/openspidey-flaming-web-cases.txt').write_bytes(inputs)
            raise
    (root / 'modern/OpenSpidey.Tests/Fixtures/flaming-web.json').write_text(json.dumps(
        [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Generated {len(cases)} flaming ctor/full Move sequences; original PC instructions verified={bool(args.original_exe)}.')


if __name__ == '__main__':
    main()
