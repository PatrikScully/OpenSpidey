"""Compile CBaddy::SetHeight with tracked ground queries and mutable support bodies."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    source=(root/'src/game/actors/baddy.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
struct CBody{CVector mVel;};
struct CBaddy{
 CVector mPos;unsigned int field_2A8=0,field_2F0=0;int field_2A0=0,mElapsedTicks=0;
 CBody*field_2A4=nullptr;i16 field_21E=0;u8 field_212=0,field_213=0;
 int SetHeight(int,int,int);
};
CBody support1,support2;int queryCalls=0,responseHeight=0,responseSupport=0,lastAbove=0,lastBelow=0;CVector lastPosition;
CBody* recover(int id){return id==1?&support1:id==2?&support2:nullptr;}
int Utils_GetGroundHeight(CVector*position,int above,int below,CBody**support){
 queryCalls++;lastPosition=*position;lastAbove=above;lastBelow=below;*support=recover(responseSupport);return responseHeight;
}
'''
    export=(root/'src/compatibility/export.h').read_text()
    code+=next(line for line in export.splitlines() if line.startswith('#define my_abs'))+'\n'
    code+=controller_method(source,'int CBaddy::SetHeight(')+'\n'
    code+=r'''
int main(){CBaddy b;int clearance,interval,elapsed,supportId;
while(std::cin>>b.mPos.vx>>b.mPos.vy>>b.mPos.vz>>b.field_2A0>>clearance>>interval>>elapsed>>b.field_2F0>>b.field_2A8>>supportId){
 b.field_21E=clearance;b.field_212=interval;b.field_213=elapsed;b.field_2A4=recover(supportId);
 queryCalls=0;lastAbove=0;lastBelow=0;lastPosition=CVector();std::cout<<'[';
 for(int frame=0;frame<8;frame++){int force,above,below;
 std::cin>>force>>above>>below>>b.mElapsedTicks>>responseHeight>>responseSupport>>support1.mVel.vy>>support2.mVel.vy>>b.field_2A8>>b.field_2F0;
 int result=b.SetHeight(force,above,below);if(frame)std::cout<<',';
 std::cout<<'['<<result<<','<<b.mPos.vy<<','<<b.field_2A0<<','<<(b.field_2A4==&support1?1:b.field_2A4==&support2?2:0)
 <<','<<int(b.field_212)<<','<<int(b.field_213)<<','<<queryCalls<<','<<lastPosition.vx<<','<<lastPosition.vy<<','<<lastPosition.vz
 <<','<<lastAbove<<','<<lastBelow<<']';}
 std::cout<<"]\n";
}}
'''
    cases=[]
    for behavior in [0,0x8000,0x40000,0x8000000,0x28000000]:
        for route in [0,4]:
            for support in [0,1]:
                for interval in [0,30,255]:
                    frames=[]
                    for frame in range(8):
                        frames.extend([int(frame==6),100,500,[1,2,255,256,30,0,1,1][frame],
                                       [-1,409600,819200,819200,819200,0,409600,-1][frame],
                                       [1,1,2,0,1,1,2,2][frame],4096 if frame%2 else 0,0,behavior,route])
                    cases.append([17,0,23,409600,100,interval,29,route,behavior,support,*frames])
    rng=random.Random(0x4041C0)
    behaviors=[0,0x8000,0x40000,0x8000000,0x28000000,0xffffffff,0x80000000]
    for _ in range(450):
        initial=[rng.randint(-2147483648,2147483647) for _ in range(4)]
        initial.extend([rng.choice([-32768,-100,0,100,32767]),rng.choice([0,1,30,255]),rng.choice([0,1,29,254,255]),
                        rng.choice([0,4,0xffffffff]),rng.choice(behaviors),rng.randrange(3)])
        frames=[]
        for frame in range(8):
            frames.extend([rng.randrange(4)==0,rng.randint(-1000,1000),rng.randint(-1000,1000),
                           rng.choice([-2147483648,-1,0,1,2,30,255,256,2147483647]),
                           rng.choice([-1,-2147483648,-122881,-122880,-12289,-12288,0,12288,122880,2147483647]),
                           rng.randrange(3),rng.choice([-2147483648,-4096,0,4096,2147483647]),
                           rng.choice([-2147483648,-4096,0,4096,2147483647]),rng.choice(behaviors),rng.choice([0,4,0xffffffff])])
        cases.append([*initial,*[int(v) for v in frames]])
    with tempfile.TemporaryDirectory(prefix='openspidey-height-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        result=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode())
    output=[dict(input=c,expected=json.loads(e)) for c,e in zip(cases,result.decode().splitlines(),strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/actor-height.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} source SetHeight sequences / {len(cases)*8} updates.')


if __name__=='__main__':main()
