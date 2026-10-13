"""Compile recovered MoveTo construction and execution with its facing procedure."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    ai = (root/'src/engine/world/ai.cpp').read_text()
    baddy = (root/'src/game/actors/baddy.cpp').read_text()
    maths = (root/'src/engine/rendering/ps2funcs.cpp').read_text()
    utils = (root/'src/core/utils.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <cmath>
#include <cstdlib>
#include <iostream>
using VECTOR = Vector;
void print_if_false(bool,const char*){}
struct CBody {CVector mPos;};
struct CBaddy : CBody {
 CSVector mAngles,mAngAcc,mAngVel;CVector field_27C;
 unsigned int field_288=0,field_2A8=0,field_2AC=0;int mElapsedTicks=0;
 int YawTowards(int,int);
};
enum AIProcType {UNK_257=257,UNK_258=258,LOOKAT_TWO=259,MOVE_TO=1794};
struct CAIProc {
 CBaddy* pBaddy=nullptr;CAIProc* mNext=nullptr;
 int mAIProcType=0,field_C=0,field_10=0,field_14=0;
 virtual void Execute()=0;int Wait();
 void AttachProc(AIProcType type,CBaddy* b,int mask){pBaddy=b;mAIProcType=type;field_14=mask;b->field_288&=~mask;}
};
struct Counter : CAIProc {int calls=0;void Execute(){calls++;}};
struct CAIProc_LookAt;CAIProc_LookAt* facing=nullptr;
struct CAIProc_LookAt : CAIProc {
 int field_20=0,field_38=0,field_3C=0;CBody* field_24=nullptr;CVector field_2C;
 CAIProc_LookAt(CBaddy*,CBody*,CVector*,int,int,int);
 void SetUpVariables(int,int);void Execute();
};
CBody* Mem_MakeHandle(CBody*b){return b;}CBody* Mem_RecoverPointer(CBody**b){return *b;}
int catan(int v){return int(std::atan(v/4096.0)*651.0006103515625);}
struct SMoveToInfo {CVector field_0;int field_C,field_10,field_14;};
struct CAIProc_MoveTo : CAIProc {
 CVector field_20,field_30;int field_2C=0,field_3C=0;
 CAIProc_MoveTo(CBaddy*,SMoveToInfo*,int);void Execute();
};
'''
    methods = [(maths,'void VectorNormal('),(maths,'i32 M3dMaths_SquareRoot0('),
               (utils,'int Utils_CalcAim('),(baddy,'int CBaddy::YawTowards('),
               (ai,'i32 CAIProc::Wait('),(ai,'void CAIProc_LookAt::SetUpVariables('),
               (ai,'CAIProc_LookAt::CAIProc_LookAt(CBaddy* a2'),(ai,'void CAIProc_LookAt::Execute('),
               (ai,'CAIProc_MoveTo::CAIProc_MoveTo('),(ai,'void CAIProc_MoveTo::Execute(')]
    for source,signature in methods:
        method=controller_method(source,signature)
        if signature.startswith('CAIProc_LookAt::CAIProc_LookAt'):
            method=method[:-1]+'facing=this;\n}'
        code += method+'\n'
    code += r'''
void emit(CAIProc_MoveTo&p,CBaddy&b,Counter&next){
 std::cout<<'['<<b.mPos.vx<<','<<b.mPos.vy<<','<<b.mPos.vz<<','<<p.field_20.vx<<','<<p.field_20.vy<<','<<p.field_20.vz
 <<','<<b.field_288<<','<<b.field_2A8<<','<<b.field_2AC<<','<<p.field_14<<','<<p.field_10<<','<<p.field_3C<<','<<next.calls
 <<','<<b.mAngles.vy<<','<<(facing?facing->field_14:0)<<','<<(facing?facing->field_38:0)<<','<<(facing?facing->field_3C:0)<<']';
}
int main(){int speed,rate,tolerance,yaw,signalFrame,runFacing;unsigned int complete,behavior,movement,mask;CVector start,target;
while(std::cin>>start.vx>>start.vy>>start.vz>>target.vx>>target.vy>>target.vz>>speed>>rate>>tolerance>>yaw>>complete>>behavior>>movement>>mask>>signalFrame>>runFacing){
 CBaddy b;b.mPos=start;b.mAngles.vy=yaw;b.field_288=complete;b.field_2A8=behavior;b.field_2AC=movement;
 SMoveToInfo info;info.field_0=target;info.field_C=speed;info.field_10=rate;info.field_14=tolerance;facing=nullptr;
 CAIProc_MoveTo p(&b,&info,mask);Counter next;p.mNext=&next;
 std::cout<<'[';emit(p,b,next);
 for(int frame=0;frame<8;frame++){std::cin>>b.mElapsedTicks;if(signalFrame==frame)b.field_288|=0x10000;
 if(runFacing&&facing)facing->Execute();p.Execute();std::cout<<',';emit(p,b,next);}
 std::cout<<"]\n";delete facing;
}}
'''
    cases=[]
    def add(start,target,speed=16,rate=0,tolerance=16,yaw=0,complete=0,behavior=0,movement=0,mask=0x10101,signal=-1,facing=0,ticks=None):
        cases.append([*start,*target,speed,rate,tolerance,yaw,complete,behavior,movement,mask,signal,facing,*(ticks or [1]*8)])
    # At an exact endpoint, positive travel stops one frame later than negative travel.
    add([0,123,0],[4096,123,0]);add([4096,123,0],[0,123,0])
    add([0,123,0],[0,123,4096]);add([0,123,4096],[0,123,0])
    add([0,0,0],[0,0,0]);add([0,500,0],[2048,500,0]);add([0,0,0],[0,409600,0])
    for facing in [0,1]:
        for signal in [-1,0,3]:
            add([0,701,0],[409600,701,-409600],40,128,32,0,0xffffffff,0xffffffff,0,0xffff,signal,facing)
    rng=random.Random(0x401CA0)
    for _ in range(500):
        start=[rng.randint(-2147483648,2147483647),rng.randint(-50000,50000),rng.randint(-2147483648,2147483647)]
        if rng.randrange(3)==0:
            target=start.copy();target[rng.choice([0,2])]=rng.randint(-2147483648,2147483647)
        else:target=[rng.randint(-2147483648,2147483647),start[1],rng.randint(-2147483648,2147483647)]
        add(start,target,rng.choice([-2147483648,-65536,-40,0,1,16,40,65536,2147483647]),
            rng.choice([0,1,128,256,-128]),rng.choice([-16,0,16,64]),rng.randint(-32768,32767),
            rng.getrandbits(32),rng.getrandbits(32),rng.getrandbits(32),rng.choice([0,1,0x10000,0x10101,0xffff]),
            rng.choice([-1,0,1,3]),rng.randrange(2),[rng.choice([-2147483648,-1,0,1,2,5,2147483647]) for _ in range(8)])
    with tempfile.TemporaryDirectory(prefix='openspidey-move-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        result=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode())
    output=[dict(input=c,expected=json.loads(e)) for c,e in zip(cases,result.decode().splitlines(),strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/actor-move.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} compiled MoveTo sequences / {len(cases)*8} updates plus constructor states.')


if __name__=='__main__':main()
