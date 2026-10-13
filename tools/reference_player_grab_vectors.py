"""Compile CPlayer::GrabUpdate and compare original PC0x4BB810 over recorded handle/hook boundaries."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
using VECTOR=Vector;struct SHandle{unsigned a,b;};struct SHook{CSVector Part;u16 Offset;};
struct CItem{u16 mType;};using CSuper=CItem;CItem target;bool hasTarget;CVector hookPosition;std::vector<int>events;
void*Mem_RecoverPointer(SHandle*h){events.insert(events.end(),{1,int(h->a),int(h->b)});return hasTarget?&target:nullptr;}
void M3dUtils_GetDynamicHookPosition(VECTOR*out,CSuper*,SHook*h){events.insert(events.end(),{2,h->Part.vx,h->Part.vy,h->Part.vz,h->Offset});out->vx=hookPosition.vx;out->vy=hookPosition.vy;out->vz=hookPosition.vz;}
void print_if_false(int,const char*){events.push_back(3);}
struct CPlayer{unsigned mActionState=0;SHandle field_DD8;CVector mPos,mUpDirection,mForwardDirection;CSVector mAngles;u8 GrabUpdate(CVector*,i16*);};
'''
    code+=controller_method((root/'src/game/player/spidey.cpp').read_text(),'u8 CPlayer::GrabUpdate(')+'\n'
    code+=r'''
#ifdef VALIDATE_ORIGINAL
unsigned char originalTarget[0x40]={};
void*originalRecover(SHandle*h){events.insert(events.end(),{1,int(h->a),int(h->b)});return hasTarget?originalTarget:nullptr;}
void patchJump(unsigned address,void*function){auto p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
#endif
void emit(CPlayer&p,CVector out,int angle,int result){std::cout<<'['<<result<<','<<p.mPos.vx<<','<<p.mPos.vy<<','<<p.mPos.vz<<','<<out.vx<<','<<out.vy<<','<<out.vz<<','<<angle<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patchJump(0x4583F0,reinterpret_cast<void*>(&originalRecover));patchJump(0x453EE0,reinterpret_cast<void*>(&M3dUtils_GetDynamicHookPosition));patchJump(0x4015B0,reinterpret_cast<void*>(&print_if_false));
#endif
unsigned action;while(std::cin>>action){CPlayer p;int has,type,yaw,outAngle;CVector out;std::cin>>has>>type>>p.field_DD8.a>>p.field_DD8.b>>p.mPos.vx>>p.mPos.vy>>p.mPos.vz
>>p.mUpDirection.vx>>p.mUpDirection.vy>>p.mUpDirection.vz>>p.mForwardDirection.vx>>p.mForwardDirection.vy>>p.mForwardDirection.vz>>yaw
>>hookPosition.vx>>hookPosition.vy>>hookPosition.vz>>out.vx>>out.vy>>out.vz>>outAngle;p.mActionState=action;p.mAngles.vy=yaw;hasTarget=has;target.mType=type;
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0xE30]={};std::memcpy(raw+8,&p.mPos,12);std::memcpy(raw+0xC84,&p.mUpDirection,12);std::memcpy(raw+0xC6C,&p.mForwardDirection,12);std::memcpy(raw+0xDD8,&p.field_DD8,8);*reinterpret_cast<unsigned*>(raw+0xE1C)=action;*reinterpret_cast<short*>(raw+0x16)=yaw;*reinterpret_cast<short*>(originalTarget+0x38)=type;
CVector originalOut=out;short originalAngle=outAngle;
#endif
short angle=outAngle;std::cout<<'[';for(int frame=0;frame<4;frame++){events.clear();int result=p.GrabUpdate(&out,&angle);auto expectedEvents=events;
#ifdef VALIDATE_ORIGINAL
 events.clear();auto actual=reinterpret_cast<unsigned char(__attribute__((thiscall)) *)(void*,CVector*,short*)>(0x4BB810)(raw,&originalOut,&originalAngle);
assert(actual==result&&!std::memcmp(raw+8,&p.mPos,12)&&!std::memcmp(&originalOut,&out,12)&&originalAngle==angle&&events==expectedEvents);
assert(*reinterpret_cast<unsigned*>(raw+0xE1C)==action&&!std::memcmp(raw+0xC84,&p.mUpDirection,12)&&!std::memcmp(raw+0xC6C,&p.mForwardDirection,12)&&!std::memcmp(raw+0xDD8,&p.field_DD8,8));
#endif
 events=expectedEvents;if(frame)std::cout<<',';emit(p,out,angle,result);hookPosition+=CVector(11,-13,17);
}std::cout<<"]\n";}}
'''
    rng=random.Random(0x4BB810);cases=[]
    for action in [0,1,0x2000000,0x4000000,0x8000000,0xe000000,0xffffffff]:
        for kind in [0,304,306,312,314,317,320,324,65535]:
            for has in [0,1]:cases.append([action,has,kind,17,19,100,200,300,0,4096,0,0,0,4096,4000,123456,-345678,567890,7,11,13,-99])
    for _ in range(256):cases.append([rng.randrange(2**32),rng.randrange(2),rng.choice([0,304,306,312,314,317,320,324,65535]),rng.randrange(2**32),rng.randrange(2**32),
                      *[rng.randint(-2147483648,2147483647) for _ in range(9)],rng.randint(-32768,32767),*[rng.randint(-2147483648,2147483647) for _ in range(6)],rng.randint(-32768,32767)])
    with tempfile.TemporaryDirectory(prefix='openspidey-grab-reference-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        cmd=[str(binary)]
        if args.original_exe:cmd.append(str(args.original_exe.resolve()))
        lines=subprocess.check_output(cmd,input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-grab.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated{len(cases)} player grab cases; original PC all four updates verified when requested.')

if __name__=='__main__':main()
