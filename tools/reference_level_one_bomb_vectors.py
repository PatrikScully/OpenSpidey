"""Compile the primary CL1A3Bomb overrides and compare them with the genuine PC instructions."""
import argparse
import json
from pathlib import Path
import random
import shutil
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    bomb = (root / 'src/game/objects/level_one_bomb.cpp').read_text()
    maths = (root / 'src/engine/rendering/ps2funcs.cpp').read_text()
    manip = (root / 'src/game/objects/manipob.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <cmath>
#include <cassert>
#include <cstring>
#include <iostream>
#include <sstream>
#include <vector>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
using VECTOR=Vector;
std::vector<long long> events;
void emit(std::initializer_list<long long> values){events.insert(events.end(),values);}
int cursor=0;
int Rnd(int bound){int value=bound?(cursor++*7+1)%bound:0;emit({6,bound,value});return value;}
int my_abs(int value){return value<0?-value:value;}
VECTOR G_GENERAL_LONG_VECTOR,G_OP12_RESULT;
void gte_stlvnl(VECTOR*value){*value=G_GENERAL_LONG_VECTOR;}
struct CItem{} world;
struct SLineInfo{CVector StartCoords,EndCoords;unsigned char pad18[0x50]={};CItem*pItem=nullptr;CVector Position;CSVector Normal;unsigned char tail7E[0x26]={};};
static_assert(sizeof(SLineInfo)==0xA4);
int groundResult=-1,hasHit=0;CSVector hitNormal;
int Utils_GetGroundHeight(CVector*position,int above,int below,void*){emit({1,position->vx,position->vy,position->vz,above,below});return groundResult;}
void M3dColij_InitLineInfo(SLineInfo*){}
void M3dZone_LineToItem(SLineInfo*line,int mode){emit({4,line->StartCoords.vx,line->StartCoords.vy,line->StartCoords.vz,line->EndCoords.vx,line->EndCoords.vy,line->EndCoords.vz,mode});line->pItem=hasHit?&world:nullptr;line->Normal=hitNormal;}
struct Camera{void Shake(CVector position,int magnitude){emit({2,position.vx,position.vy,position.vz,magnitude});}} camera;
Camera*G_CAMERA_LIST=&camera;
const int CAMERASHAKE_SMALL=2;
int SFX_PlayPos(unsigned sound,CVector*position,int pitch){emit({3,sound,position->vx,position->vy,position->vz,pitch});return 1;}
unsigned short links[1];
unsigned short*Trig_GetLinksPointer(unsigned node){links[0]=node;return links;}
void Trig_SendSignalToLinks(unsigned short*link){emit({5,link[0]});}
void Trig_SendPulse(unsigned short*link){emit({7,link[0]});}
struct Player{unsigned char prefix[0xC6C]={};CVector mForwardDirection;unsigned char middle[0xC]={};CVector mUpDirection;} player;
Player*G_MECHLIST_PLAYER=nullptr;
unsigned gBombRelated,gBombAIRelated,gBombDieTimerRelated,G_TIMER_RELATED;
unsigned char gBombDieRelatedOne,gBombDieRelatedTwo;
int DifficultyLevel;
struct CManipOb{
 unsigned vt=0;unsigned short mFlags=0,inquiry=0;CVector mPos;unsigned char pad14[0x24]={};unsigned short mType=401,pad3A=0;unsigned pad3C=0,list=0;
 unsigned short mInputFlags=0,mCBodyFlags=0x16;unsigned char pad48[24]={};CVector mVel,mAcc;unsigned char pad78[8]={};int mElapsedTicks=0;
 int accumulated=0;CSVector mAngVel;unsigned char pad8E[0x4E]={};unsigned short radius=0,mNode=0;unsigned char padE0[0x14]={};
 unsigned scratch=0;int mNumVectors=0;CVector*pVectors=nullptr;int field_100=0;short field_104=0,field_106=0,field_108=0,pad10A=0;int field_10C=0;CVector field_110;unsigned field_11C=0,field_120=0;unsigned char field_124=0,pad125[3]={};
 CManipOb(short*,int){}void Throw(CVector*);
};
static_assert(sizeof(CManipOb)==0x128);static_assert(offsetof(CManipOb,mNode)==0xDE);
struct CL1A3Bomb:CManipOb{unsigned char field_128=0,field_129=0,field_12A=0,pad12B=0;CL1A3Bomb(short*,int);~CL1A3Bomb();void AI();void DoPhysics();void Die();void Smash();};
static_assert(sizeof(CL1A3Bomb)==0x12C);
'''
    for signature in ['i32 M3dMaths_SquareRoot0(', 'void VectorNormal(', 'void gte_ldlvl(', 'void gte_sqr0(']:
        code += controller_method(maths, signature) + '\n'
    code += controller_method((root / 'src/compatibility/fixed_vector_bridge.cpp').read_text(), 'int CVector::Length(') + '\n'
    code += controller_method(manip, 'void CManipOb::Throw(') + '\n'
    for signature in ['CL1A3Bomb::CL1A3Bomb(', 'CL1A3Bomb::~CL1A3Bomb(', 'void CL1A3Bomb::AI(',
                      'void CL1A3Bomb::DoPhysics(', 'void CL1A3Bomb::Die(', 'void CL1A3Bomb::Smash(']:
        method = controller_method(bomb, signature)
        if signature == 'void CL1A3Bomb::DoPhysics(':
            # Actual447302/44731A read mPos for all endpoint axes, unlike recovered source captured X/Z.
            method = method.replace('line.EndCoords.vx = capturedPos.vx', 'line.EndCoords.vx = this->mPos.vx')
            method = method.replace('line.EndCoords.vz = capturedPos.vz', 'line.EndCoords.vz = this->mPos.vz')
        code += method + '\n'
    code += r'''
void vec(CVector value){std::cout<<value.vx<<','<<value.vy<<','<<value.vz;}
void output(CL1A3Bomb&s){std::cout<<'[';vec(s.mPos);std::cout<<',';vec(s.mVel);std::cout<<',';vec(s.mAcc);std::cout<<','<<s.mAngVel.vx<<','<<s.mAngVel.vy<<','<<s.mAngVel.vz<<','<<s.mFlags<<','<<s.field_10C<<','<<int(s.field_128)<<','<<int(s.field_129)<<','<<int(s.field_12A)<<','<<s.mInputFlags<<','<<gBombRelated<<','<<gBombAIRelated<<','<<int(gBombDieRelatedOne)<<','<<int(gBombDieRelatedTwo)<<','<<gBombDieTimerRelated<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}
std::string capture(CL1A3Bomb&s){std::ostringstream text;auto*old=std::cout.rdbuf(text.rdbuf());output(s);std::cout.rdbuf(old);return text.str();}
#ifdef VALIDATE_ORIGINAL
void patch(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
void*__attribute__((thiscall)) baseCtor(void*p,short*,int){return p;}
void __attribute__((thiscall)) baseDelete(void*){}
void __attribute__((thiscall)) shake(void*,CVector&position,int magnitude){camera.Shake(position,magnitude);}
void __attribute__((thiscall)) throwVelocity(void*p,CVector*velocity){static_cast<CManipOb*>(p)->Throw(velocity);}
#endif
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x456920,reinterpret_cast<void*>(&baseCtor));patch(0x456A80,reinterpret_cast<void*>(&baseDelete));
patch(0x4E6840,reinterpret_cast<void*>(&Utils_GetGroundHeight));patch(0x416880,reinterpret_cast<void*>(&shake));patch(0x471EA0,reinterpret_cast<void*>(&SFX_PlayPos));
patch(0x4524C0,reinterpret_cast<void*>(&M3dColij_InitLineInfo));patch(0x4549A0,reinterpret_cast<void*>(&M3dZone_LineToItem));
patch(0x4E3880,reinterpret_cast<void*>(&Trig_GetLinksPointer));patch(0x4DFFE0,reinterpret_cast<void*>(&Trig_SendSignalToLinks));patch(0x4DFD30,reinterpret_cast<void*>(&Trig_SendPulse));
// Inherited Throw is independently source-verified by manipulation.json. Preserve its real state/RNG changes here.
patch(0x456CF0,reinterpret_cast<void*>(&throwVelocity));*reinterpret_cast<Camera**>(0x56F3B8)=&camera;
#endif
long long input[39];while(std::cin>>input[0]){for(int i=1;i<39;i++)std::cin>>input[i];
CL1A3Bomb source(nullptr,0);source.mPos=CVector(input[1],input[2],input[3]);source.mVel=CVector(input[4],input[5],input[6]);source.mAcc=CVector(input[7],input[8],input[9]);source.mElapsedTicks=input[10];source.mFlags=input[16];source.field_10C=input[17];source.field_128=input[18];source.field_129=input[19];source.field_12A=input[20];source.mInputFlags=input[21];source.mNode=input[37];source.mAngVel=CSVector(input[38],-7,123);
groundResult=input[11];hasHit=input[12];hitNormal=CSVector(input[13],input[14],input[15]);DifficultyLevel=input[22];G_TIMER_RELATED=input[23];
player.mForwardDirection=CVector(input[30],input[31],input[32]);player.mUpDirection=CVector(input[33],input[34],input[35]);G_MECHLIST_PLAYER=input[29]?&player:nullptr;
auto resetGlobals=[&](){gBombRelated=input[24];gBombAIRelated=input[25];gBombDieRelatedOne=input[26];gBombDieRelatedTwo=input[27];gBombDieTimerRelated=input[28];cursor=input[36];events.clear();};
CL1A3Bomb actual=source;resetGlobals();int op=input[0];if(op==0){source.field_128=0;source.field_129=0;gBombRelated=4096;}else if(op==1)source.AI();else if(op==2)source.DoPhysics();else if(op==3)source.Smash();else if(op==4)source.Die();else if(op==5)source.~CL1A3Bomb();
std::string expected=capture(source);
#ifdef VALIDATE_ORIGINAL
resetGlobals();*reinterpret_cast<unsigned*>(0x54E8D4)=gBombRelated;*reinterpret_cast<unsigned*>(0x60F774)=gBombAIRelated;*reinterpret_cast<unsigned char*>(0x60F771)=gBombDieRelatedOne;*reinterpret_cast<unsigned char*>(0x60F772)=gBombDieRelatedTwo;*reinterpret_cast<unsigned*>(0x60F778)=gBombDieTimerRelated;*reinterpret_cast<unsigned*>(0x6B4CA8)=G_TIMER_RELATED;*reinterpret_cast<int*>(0x54D474)=DifficultyLevel;*reinterpret_cast<Player**>(0x6A9038)=G_MECHLIST_PLAYER;
unsigned addresses[6]={0x446E70,0x446FF0,0x4470A0,0x446F40,0x446F00,0x446ED0};
if(op==0)reinterpret_cast<void(__attribute__((thiscall)) *)(void*,short*,int)>(addresses[op])(&actual,nullptr,actual.mNode);
else reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(addresses[op])(&actual);
gBombRelated=*reinterpret_cast<unsigned*>(0x54E8D4);gBombAIRelated=*reinterpret_cast<unsigned*>(0x60F774);gBombDieRelatedOne=*reinterpret_cast<unsigned char*>(0x60F771);gBombDieRelatedTwo=*reinterpret_cast<unsigned char*>(0x60F772);gBombDieTimerRelated=*reinterpret_cast<unsigned*>(0x60F778);
auto got=capture(actual);if(got!=expected){std::cerr<<"Bomb op "<<op<<"\nsource="<<expected<<"\nactual="<<got<<'\n';abort();}
#endif
std::cout<<expected<<'\n';}}
'''
    rng = random.Random(0x4470A0)
    cases = []
    for op in range(6):
        for index in range(96):
            pos = [rng.randint(-18000000, 18000000) for _ in range(3)]
            if index < 16:
                pos = [rng.choice([-16384001, -16384000, -11673600, -11673599]), 123456,
                       rng.choice([0x79DFFF, 0x79E000, 0xAF0000, 0xAF0001])]
            velocity = [rng.randint(-90000, 90000) for _ in range(3)]
            if index < 24:
                velocity = [rng.choice([-8193, -8192, -1, 0, 1, 8191, 8192, 8193]),
                            rng.choice([-8192, 0, 8192]), rng.choice([-8192, -1, 0, 8191, 8192])]
            normals = [[4096, 0, 0], [0, 4096, 0], [0, -4096, 0], [1234, 3800, -800],
                       [1234, 3801, -800], [1234, -3800, -800], [1234, -3801, -800],
                       [-2500, 2048, 2048], [65535, 32768, -65536]]
            row = [op, *pos, *velocity, *[rng.randint(-5000, 5000) for _ in range(3)],
                   rng.choice([-1, 0, 1, 2, 6]), rng.choice([-1, 1000000, -2000000]), rng.randrange(2),
                   *rng.choice(normals), rng.randrange(65536), rng.choice([0, 1, 5, 255]),
                   rng.choice([0, 1, 255]), rng.choice([0, 1, 255]), rng.choice([0, 1, 255]), rng.randrange(65536),
                   rng.choice([-1, 0, 1, 2, 3, 4]), rng.choice([0, 2, 123456789, 0xFFFFFFFF]),
                   rng.choice([0, 1, 4096, 8191, 8192, 0xFFFFFFFF]), rng.choice([0, 1, 4260, 7260, 0xFFFFFFFF]),
                   rng.choice([0, 1, 255]), rng.choice([0, 1, 255]), rng.randrange(0x100000000), rng.randrange(2),
                   *[rng.randint(-4096, 4096) for _ in range(6)], rng.randrange(4096), rng.randrange(65536), rng.randint(-32768, 32767)]
            cases.append(row)
    with tempfile.TemporaryDirectory(prefix='openspidey-level-one-bomb-') as directory:
        cpp = Path(directory) / 'reference.cpp'; binary = Path(directory) / 'reference'; cpp.write_text(code)
        flags = ['-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', *flags,
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp),
                        str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        command = [str(binary)] + ([str(args.original_exe.resolve())] if args.original_exe else [])
        inputs = ('\n'.join(' '.join(map(str, row)) for row in cases) + '\n').encode()
        try:
            lines = subprocess.check_output(command, input=inputs).decode().splitlines()
        except subprocess.CalledProcessError:
            shutil.copyfile(cpp, '/tmp/openspidey-level-one-bomb-reference.cpp')
            shutil.copyfile(binary, '/tmp/openspidey-level-one-bomb-reference')
            Path('/tmp/openspidey-level-one-bomb-cases.txt').write_bytes(inputs)
            raise
    (root / 'modern/OpenSpidey.Tests/Fixtures/level-one-bomb.json').write_text(json.dumps(
        [dict(input=row, expected=json.loads(line)) for row, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Generated {len(cases)} complete original bomb ctor/AI/physics/Smash/Die/destructor cases; original PC verified={bool(args.original_exe)}.')


if __name__ == '__main__':
    main()
