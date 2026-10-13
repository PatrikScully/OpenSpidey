"""Source torso heading requests with original-PC point/angle/lock comparison; effective-heading boundary explicit."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path);args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <cmath>
#include <cstring>
#include <cstdlib>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
using VECTOR=Vector;struct MATRIX{short m[3][3];};MATRIX*rotation;VECTOR loaded,rotated;
void gte_SetRotMatrix(MATRIX*p){rotation=p;}void gte_ldlvl(VECTOR*p){loaded=*p;}void gte_rtir(){rotated.vx=(loaded.vx*rotation->m[0][0]+loaded.vy*rotation->m[0][1]+loaded.vz*rotation->m[0][2])>>12;rotated.vy=(loaded.vx*rotation->m[1][0]+loaded.vy*rotation->m[1][1]+loaded.vz*rotation->m[1][2])>>12;rotated.vz=(loaded.vx*rotation->m[2][0]+loaded.vy*rotation->m[2][1]+loaded.vz*rotation->m[2][2])>>12;}void gte_stlvnl(VECTOR*p){*p=rotated;}
void print_if_false(int,const char*){}
'''
    rendering=(root/'src/engine/rendering/ps2funcs.cpp').read_text();code+=controller_method(rendering,'int ratan2(')+'\n'
    code+=r'''
short heading;
struct CPlayer{CVector mPos;MATRIX field_89C;u8 field_8E9,mIsCrawling;unsigned mActionState;int field_DFC,mHeadingTurnTicksRemaining,mTargetHeading,mHeadingTurnStep;short GetEffectiveHeading(){return heading;}void SetTargetTorsoAngleToThisPoint(CVector*);void SetTargetTorsoAngle(short,bool);void LockTargetTorsoAngle();};
'''
    source=(root/'src/game/player/spidey.cpp').read_text()
    for signature in ['void CPlayer::SetTargetTorsoAngleToThisPoint(','void CPlayer::SetTargetTorsoAngle(','void CPlayer::LockTargetTorsoAngle(']:code+=controller_method(source,signature)+'\n'
    code+=r'''
#ifdef VALIDATE_ORIGINAL
short __attribute__((thiscall)) originalHeading(void*){return heading;}
void patch(unsigned address,void*function){auto p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
#endif
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4C6AA0,reinterpret_cast<void*>(&originalHeading));patch(0x46D130,reinterpret_cast<void*>(&ratan2));
#endif
int mode;while(std::cin>>mode){CPlayer p;int ceiling,crawling,wanted,fast;CVector target;std::cin>>heading>>ceiling>>crawling>>p.mActionState>>p.field_DFC>>p.mHeadingTurnTicksRemaining>>p.mTargetHeading>>p.mHeadingTurnStep>>wanted>>fast>>p.mPos.vx>>p.mPos.vy>>p.mPos.vz>>target.vx>>target.vy>>target.vz;for(auto&row:p.field_89C.m)for(auto&entry:row)std::cin>>entry;p.field_8E9=ceiling;p.mIsCrawling=crawling;
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0xED0]={};std::memcpy(raw+8,&p.mPos,12);std::memcpy(raw+0x89C,&p.field_89C,18);raw[0x8E9]=ceiling;raw[0xAD4]=crawling;*reinterpret_cast<unsigned*>(raw+0xE1C)=p.mActionState;*reinterpret_cast<int*>(raw+0xDFC)=p.field_DFC;*reinterpret_cast<int*>(raw+0xDF8)=p.mHeadingTurnTicksRemaining;*reinterpret_cast<int*>(raw+0xDF0)=p.mTargetHeading;*reinterpret_cast<int*>(raw+0xDF4)=p.mHeadingTurnStep;
#endif
if(mode==0)p.SetTargetTorsoAngleToThisPoint(&target);else if(mode==1)p.SetTargetTorsoAngle(wanted,fast);else p.LockTargetTorsoAngle();
#ifdef VALIDATE_ORIGINAL
if(mode==0)reinterpret_cast<void(__attribute__((thiscall)) *)(void*,CVector*)>(0x4C68A0)(raw,&target);else if(mode==1)reinterpret_cast<void(__attribute__((thiscall)) *)(void*,short,bool)>(0x4C6970)(raw,wanted,fast);else reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4C6960)(raw);
assert(*reinterpret_cast<int*>(raw+0xDF8)==p.mHeadingTurnTicksRemaining&&*reinterpret_cast<int*>(raw+0xDF0)==p.mTargetHeading&&*reinterpret_cast<int*>(raw+0xDF4)==p.mHeadingTurnStep);
#endif
std::cout<<'['<<p.mHeadingTurnTicksRemaining<<','<<p.mTargetHeading<<','<<p.mHeadingTurnStep<<"]\n";}}
'''
    rng=random.Random(0x4c68a0);cases=[];matrices=[[4096,0,0,0,4096,0,0,0,4096],[0,0,4096,0,4096,0,-4096,0,0],[-4096,0,0,0,4096,0,0,0,-4096]]
    for mode in [0,1,2]:
        for h in [0,1024,2048,4095]:
            for ceiling in [0,1]:
                for crawling in [0,1]:cases.append([mode,h,ceiling,crawling,1,1,17,19,23,h,0,0,0,0,0,0,0,*matrices[0]])
    for _ in range(400):cases.append([rng.randrange(3),rng.randrange(4096),rng.randrange(2),rng.randrange(2),rng.choice([1,2,4,0x800,0x2000000,0x2000002]),rng.randint(1,4),rng.randint(0,32),rng.randrange(4096),rng.randint(-100,100),rng.randint(-32768,32767),rng.randrange(2),*[rng.randint(-2**31,2**31-1) for _ in range(6)],*rng.choice(matrices)])
    with tempfile.TemporaryDirectory(prefix='openspidey-torso-') as folder:
        cpp=Path(folder)/'reference.cpp';exe=Path(folder)/'reference';cpp.write_text(code);flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(exe)],check=True)
        command=[str(exe)]+([str(args.original_exe.resolve())] if args.original_exe else []);lines=subprocess.check_output(command,input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-torso.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} torso point/angle/lock cases; original PC numerical turns verified when requested.')

if __name__=='__main__':main()
