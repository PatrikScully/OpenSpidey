"""Generate torso joint correction fixtures; optionally verify PC torso/ArcCos machine code.

Original ApplyPose is an explicit recorded boundary. The finite ArcCos domain is checked against
unmodified PC code; the complete torso then uses that proved helper, including MSVC NaN narrowing,
so invalid-domain CRT exception handling is not invoked in the standalone Linux process.
"""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe',type=Path)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "core/fixed_vector.h"
#include "core/distance.h"
#include <iostream>
#include <vector>
#include <cmath>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
struct SJoint{CSVector Angles,Displacement;};
struct CBaddy;using CThug=CBaddy;
CBaddy*G_MECHLIST_PLAYER=nullptr;int G_ATTACK_RELATED=0;
std::vector<int> events;
struct CBaddy{CVector mPos;int mType=304,field_204=0,field_208=0;u16 mFlags=0;SJoint*mpJoints=nullptr;
 int DistanceToPlayer(int);void RotateTorsoToAimAtPlayer(CVector&);
 void ApplyPose(i16*p){events.insert(events.end(),{mType==304?304:312,mFlags,mpJoints?1:0,mpJoints?mpJoints[1].Angles.vz:0});}
};
const float FOUR_NINETY_SIX=4096.0f,TWO_FOURTY_EIGHT=2048.0f,PI=3.1415927f;
int pcTruncate(double value){return std::isfinite(value)?int(value):0;}
'''
    arccos=controller_method((root/'src/core/utils.cpp').read_text(),'int Utils_ArcCos(')
    # PC0x4E6721 reloads res from f32, then x87 multiplies/divides without another f32 store.
    arccos=arccos.replace('return (res * TWO_FOURTY_EIGHT / PI);','return pcTruncate(double(res) * TWO_FOURTY_EIGHT / PI);')
    code+=arccos+'\n'
    code+=controller_method((root/'src/game/actors/baddy.cpp').read_text(),'i32 CBaddy::DistanceToPlayer(')+'\n'
    code+=controller_method((root/'src/game/actors/thug.cpp').read_text(),'void CThug::RotateTorsoToAimAtPlayer(')+'\n'
    code+=r'''
#ifdef VALIDATE_ORIGINAL
void patchJump(unsigned address,void*function){auto p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
void __attribute__((thiscall)) originalPose(void*actor,short*packet){auto p=static_cast<unsigned char*>(actor);auto joints=*reinterpret_cast<SJoint**>(p+0x188);
 events.insert(events.end(),{packet==reinterpret_cast<short*>(0x557CE4)?304:312,*reinterpret_cast<unsigned short*>(p+4),joints?1:0,joints?joints[1].Angles.vz:0});}
#endif
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
 assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 double invalid=std::nan("");int invalidLow;
 asm volatile("fldl %1;call *%2":"=a"(invalidLow):"m"(invalid),"r"(0x529C7C):"ecx","edx","memory","cc","st");
 assert(invalidLow==0);
 for(int value=-4096;value<=4096;value++){int original=reinterpret_cast<int(*)(int)>(0x4E6700)(value);assert(original==Utils_ArcCos(value));}
 patchJump(0x460E80,reinterpret_cast<void*>(&originalPose));
 patchJump(0x4E6700,reinterpret_cast<void*>(&Utils_ArcCos));
#endif
 int kind;while(std::cin>>kind){if(kind==-1){int value;std::cin>>value;std::cout<<Utils_ArcCos(value)<<"\n";continue;}CBaddy t,player;G_MECHLIST_PLAYER=&player;int flags,has,roll;CVector origin;SJoint joints[2]{};
 std::cin>>flags>>has>>roll>>G_ATTACK_RELATED>>t.field_208>>t.field_204
 >>t.mPos.vx>>t.mPos.vy>>t.mPos.vz>>player.mPos.vx>>player.mPos.vy>>player.mPos.vz>>origin.vx>>origin.vy>>origin.vz;
 t.mType=kind;t.mFlags=flags;joints[1].Angles.vz=roll;t.mpJoints=has?joints:nullptr;
#ifdef VALIDATE_ORIGINAL
 alignas(4)unsigned char raw[0x400]={},rawPlayer[0x600]={};SJoint originalJoints[2]{};originalJoints[1].Angles.vz=roll;
 std::memcpy(raw+8,&t.mPos,12);std::memcpy(rawPlayer+8,&player.mPos,12);*reinterpret_cast<short*>(raw+0x38)=kind;
 *reinterpret_cast<short*>(raw+4)=flags;*reinterpret_cast<SJoint**>(raw+0x188)=has?originalJoints:nullptr;
 *reinterpret_cast<int*>(raw+0x204)=t.field_204;*reinterpret_cast<int*>(raw+0x208)=t.field_208;
 *reinterpret_cast<void**>(0x6A9038)=rawPlayer;*reinterpret_cast<int*>(0x5FCCF4)=G_ATTACK_RELATED;
#endif
 std::cout<<'[';for(int frame=0;frame<6;frame++){events.clear();t.RotateTorsoToAimAtPlayer(origin);auto expectedEvents=events;
#ifdef VALIDATE_ORIGINAL
 events.clear();using Function=void(__attribute__((thiscall)) *)(void*,CVector&);reinterpret_cast<Function>(0x4D6BC0)(raw,origin);
 assert(*reinterpret_cast<unsigned short*>(raw+4)==t.mFlags && originalJoints[1].Angles.vz==joints[1].Angles.vz);
 assert(*reinterpret_cast<int*>(raw+0x204)==t.field_204 && *reinterpret_cast<int*>(raw+0x208)==t.field_208 && events==expectedEvents);
#endif
 if(frame)std::cout<<',';std::cout<<'['<<t.mFlags<<','<<joints[1].Angles.vz<<','<<t.field_208<<','<<t.field_204<<",[";
 for(unsigned i=0;i<expectedEvents.size();i++){if(i)std::cout<<',';std::cout<<expectedEvents[i];}std::cout<<"]]";
 }std::cout<<"]\n";
 }}
'''
    rng=random.Random(0x4D6BC0);cases=[]
    for kind in [304,312]:
        for roll in [-32768,-1024,-9,-8,-1,0,1,8,9,1024,32767]:
            for y in [-4096,-4095,0,4095,4096,409600]:
                for has in [0,1]:cases.append([kind,0xffff,has,roll,20,0,0,0,0,0,4096000,y,0,0,0,0])
    for _ in range(500):
        actor=[rng.randint(-100000000,100000000) for _ in range(3)]
        player=[rng.randint(-100000000,100000000) for _ in range(3)]
        origin=[rng.randint(-100000000,100000000) for _ in range(3)]
        cases.append([rng.choice([304,312]),rng.randrange(65536),rng.randrange(2),rng.randint(-32768,32767),
                      rng.choice([0,1,20,2147483647]),rng.choice([0,19,20,21]),rng.choice([-1,0,1000,2147483647]),*actor,*player,*origin])
    with tempfile.TemporaryDirectory(prefix='openspidey-torso-reference-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        command=[str(binary)];
        if args.original_exe:command.append(str(args.original_exe.resolve()))
        requests='\n'.join(' '.join(map(str,c)) for c in cases)+'\n'
        requests+=''.join(f'-1 {value}\n' for value in range(-4096,4097))
        lines=subprocess.check_output(command,input=requests.encode()).decode().splitlines()
    result=[dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines[:len(cases)],strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-torso.json').write_text(json.dumps(dict(cases=result,arcCos=[int(line) for line in lines[len(cases):]]),separators=(',',':'))+'\n')
    print(f'Generated {len(result)} torso cases / {len(result)*6} source updates.')
    if args.original_exe:print('All updates match original PC0x4D6BC0; helper finite domain8193 values matches unmodified PC0x4E6700; invalid _ftol lowword matches PC0x529C7C.')


if __name__=='__main__':main()
