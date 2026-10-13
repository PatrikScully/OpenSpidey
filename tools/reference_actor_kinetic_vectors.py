"""Compile recovered Fall and AccZ constructors and updates with synthetic actor state."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    source=(root/'src/engine/world/ai.cpp').read_text()
    code=r'''
#include <cstdint>
#include <cstdlib>
#include <iostream>
using i32=std::int32_t;
struct CVector{int vx=17,vy=0,vz=23;};
struct CBaddy{CVector mPos,mAcc,field_27C;unsigned int field_288=0,field_2A8=0;int field_308=0;};
enum AIProcType{PROC_FALL_TYPE=770,ACCZ=1025};
struct CAIProc{
 CBaddy*pBaddy=nullptr;CAIProc*mNext=nullptr;int field_C=0,field_10=0,field_14=0;
 virtual void Execute()=0;int Wait();
 void AttachProc(AIProcType,CBaddy*b,int mask){pBaddy=b;field_14=mask;b->field_288&=~mask;}
};
struct Counter:CAIProc{int calls=0;void Execute(){calls++;}};
struct CAIProc_Fall:CAIProc{CAIProc_Fall(CBaddy*,int);void Execute();};
struct CAIProc_AccZ:CAIProc{int field_20=0,field_24=0;CAIProc_AccZ(CBaddy*,int,int,int);void Execute();};
void print_if_false(bool,const char*){}
'''
    for signature in ['i32 CAIProc::Wait(', 'CAIProc_Fall::CAIProc_Fall(', 'void CAIProc_Fall::Execute(',
                      'CAIProc_AccZ::CAIProc_AccZ(', 'void CAIProc_AccZ::Execute(']:
        code+=controller_method(source,signature)+'\n'
    code+=r'''
int main(){int mode,accel,target,local,acc,flags,wait;unsigned int complete,behavior,mask;
while(std::cin>>mode>>accel>>target>>local>>acc>>flags>>wait>>complete>>behavior>>mask){
 CBaddy b;b.field_27C.vz=local;b.mAcc.vz=acc;b.field_288=complete;b.field_2A8=behavior;Counter next;
 if(mode==0){CAIProc_Fall p(&b,mask);p.field_10=flags;p.mNext=&next;std::cout<<'[';
 for(int frame=0;frame<7;frame++){std::cin>>b.mPos.vy>>b.field_308>>b.field_2A8;p.Execute();if(frame)std::cout<<',';
 std::cout<<'['<<b.mPos.vy<<','<<b.field_288<<','<<b.field_2A8<<','<<p.field_10<<','<<unsigned(p.field_14)<<','<<next.calls<<']';}
 std::cout<<"]\n";
 }else{CAIProc_AccZ p(&b,accel,target,mask);p.field_10=flags;p.field_C=wait;p.mNext=&next;
 auto emit=[&](){std::cout<<'['<<b.field_27C.vz<<','<<b.mAcc.vz<<','<<b.field_288<<','<<b.field_2A8<<','<<p.field_10<<','<<p.field_C
 <<','<<unsigned(p.field_14)<<','<<p.field_20<<','<<p.field_24<<','<<next.calls<<']';};
 std::cout<<'[';emit();for(int frame=0;frame<7;frame++){int extraFlags;std::cin>>extraFlags;p.field_10|=extraFlags;p.Execute();std::cout<<',';emit();b.field_27C.vz+=b.mAcc.vz;}
 std::cout<<"]\n";
 }
}}
'''
    rng=random.Random(0x4017C0);fall=[];acceleration=[]
    for _ in range(250):
        frames=[]
        for frame in range(7):
            landing=rng.choice([-2147483648,-100,0,100,2147483647])
            y=rng.choice([-2147483648,-100,0,100,2147483647,landing])
            behavior=rng.choice([0,0x400,0xffffffff,0x80000400])
            frames.extend([y,landing,behavior])
        fall.append([0,0,0,0,0,rng.choice([0,1,2,4]),0,rng.getrandbits(32),0,rng.getrandbits(32),*frames])
    for accel in [-2147483648,-8388608,-65536,-17,-16,-1,0,1,15,16,17,65536,8388608,2147483647]:
        for target in [-2147483648,-32,0,32,2147483647]:
            for local in [-2147483648,-16384,0,16384,2147483647]:
                acceleration.append([1,accel,target,local,77,0,0,0xffffffff,0xffffffff,0x10101,*([0]*7)])
    for _ in range(400):
        acceleration.append([1,rng.randint(-2147483648,2147483647),rng.randint(-2147483648,2147483647),
                             rng.randint(-2147483648,2147483647),rng.randint(-2147483648,2147483647),
                             rng.choice([0,1,2,3,4,8]),rng.choice([-1,0,1,2,3]),rng.getrandbits(32),rng.getrandbits(32),
                             rng.getrandbits(32),*[rng.choice([0,0,0,1,2,4]) for _ in range(7)]])
    cases=fall+acceleration
    with tempfile.TemporaryDirectory(prefix='openspidey-kinetic-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        result=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode())
    output=[dict(input=c,expected=json.loads(e)) for c,e in zip(cases,result.decode().splitlines(),strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/actor-kinetic.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Generated {len(fall)} Fall and {len(acceleration)} AccZ sequences from recovered C++ methods.')


if __name__=='__main__':main()
