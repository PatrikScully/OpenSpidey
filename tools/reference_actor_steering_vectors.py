"""Compile recovered LookAt, Wait, setup and YawTowards methods for steering fixtures."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    ai = (root / 'src/engine/world/ai.cpp').read_text()
    baddy = (root / 'src/game/actors/baddy.cpp').read_text()
    utils = (root / 'src/core/utils.cpp').read_text()
    code = r'''
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <iostream>
using i32 = std::int32_t; using i16 = std::int16_t;
struct CVector { int vx=0,vy=0,vz=0; };
struct CSVector { i16 vx=11,vy=0,vz=13; };
struct CBody { CVector mPos; };
struct CBaddy : CBody {
 CSVector mAngles,mAngVel,mAngAcc;
 unsigned int field_2A8=0,field_2AC=0,field_288=0;
 int YawTowards(int,int);
};
enum AIProcType { UNK_257=257,UNK_258=258,LOOKAT_TWO=259 };
struct CAIProc {
 CBaddy* pBaddy; int field_C=0,field_10=0,field_14=0,mAIProcType;
 CAIProc* mNext=nullptr; int Wait(); virtual void Execute()=0;
};
struct Counter : CAIProc { int calls=0; void Execute(){calls++;} };
struct CAIProc_LookAt : CAIProc {
 int field_20=0,field_38=0,field_3C=0;
 CBody* field_24=nullptr; CVector field_2C;
 void SetUpVariables(int,int); void Execute();
};
CBody* Mem_RecoverPointer(CBody**p){return *p;}
int catan(int v){return int(std::atan(v/4096.0)*651.0006103515625);}
int M3dMaths_SquareRoot0(int v){return v<=-32768?32768:v<0?0:int(std::sqrt(double(v)));}
'''
    for source, signature in [(utils, 'int Utils_CalcAim('), (baddy, 'int CBaddy::YawTowards('),
                              (ai, 'i32 CAIProc::Wait('), (ai, 'void CAIProc_LookAt::SetUpVariables('),
                              (ai, 'void CAIProc_LookAt::Execute(')]:
        code += controller_method(source, signature) + '\n'
    code += r'''
int main(){int mode,target,rate,tolerance,yaw,flags,wait,valid;unsigned int behavior,complete,mask,movement;
while(std::cin>>mode>>target>>rate>>tolerance>>yaw>>flags>>wait>>valid>>behavior>>complete>>mask>>movement){
 CBaddy b;CAIProc_LookAt p;CBody targetBody;Counter next;
 std::cin>>b.mPos.vx>>b.mPos.vy>>b.mPos.vz>>targetBody.mPos.vx>>targetBody.mPos.vy>>targetBody.mPos.vz;
 b.mAngles.vy=yaw;b.mAngVel.vy=17;b.mAngAcc.vy=-23;b.field_2A8=behavior;b.field_288=complete;b.field_2AC=movement;
 p.pBaddy=&b;p.mAIProcType=mode;p.field_20=target;p.field_2C=targetBody.mPos;p.field_24=valid?&targetBody:nullptr;
 p.field_10=flags;p.field_C=wait;p.field_14=mask;p.mNext=&next;
 b.field_288&=~mask;p.SetUpVariables(tolerance,rate);
 std::cout<<'[';for(int frame=0;frame<5;frame++){p.Execute();if(frame)std::cout<<',';
 std::cout<<'['<<b.mAngles.vy<<','<<b.mAngVel.vy<<','<<b.mAngAcc.vy<<','<<b.field_2AC<<','<<b.field_288<<','<<b.field_2A8
 <<','<<p.field_10<<','<<p.field_C<<','<<p.field_14<<','<<p.field_38<<','<<p.field_3C<<','<<next.calls<<']';
 }std::cout<<"]\n";
}}
'''
    rng = random.Random(0x4013D0)
    cases = []
    for mode in [257, 258, 259]:
        for rate in [0, 1, 64, 128, 255, 256, 512, -128]:
            for yaw,target in [(0,0),(0,1024),(4090,4),(4,4090),(0,2048),(2048,0),(32760,-32760),(-32768,32767)]:
                cases.append([mode,target,rate,16,yaw,0,0,1,0,0xffffffff,0x10001,0,0,0,0,409600,0,-409600])
    for _ in range(450):
        mode = rng.choice([257,258,259]);position = [rng.randint(-8000000,8000000) for _ in range(3)]
        target_position = position.copy() if rng.randrange(5)==0 else [rng.randint(-8000000,8000000) for _ in range(3)]
        cases.append([mode,rng.randint(-65536,65535),rng.choice([-512,-128,0,1,64,128,255,256,512,65536]),
                      rng.choice([-1000,-16,0,8,16,1000]),rng.randint(-32768,32767),rng.choice([0,1,2,3,4,8]),
                      rng.choice([-1,0,1,3]),rng.randrange(2),rng.choice([0,4,0x40000000,0x40000004,0xffffffff]),
                      rng.getrandbits(32),rng.choice([0,1,0x10000,0x10101,0xffff]),rng.getrandbits(32),*position,*target_position])
    with tempfile.TemporaryDirectory(prefix='openspidey-steering-reference-') as directory:
        cpp = Path(directory)/'reference.cpp';binary = Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        result = subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode())
    expected = result.decode().splitlines()
    output = [dict(input=c,expected=json.loads(e)) for c,e in zip(cases,expected,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/actor-steering.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    yaw_cases = [[yaw, target, rate] for yaw in [-32768, -1, 0, 1, 2048, 4095, 32767]
                 for target in [-65536, -32768, -2048, 0, 2048, 4095, 32767, 65535]
                 for rate in [-65536, -512, -1, 0, 1, 128, 255, 256, 512, 65536]]
    yaw_code = code[:code.index('int main()')] + r'''
int main(){int yaw,target,rate;while(std::cin>>yaw>>target>>rate){
 CBaddy b;b.mAngles.vy=yaw;b.mAngVel.vy=17;b.mAngAcc.vy=-23;
 int residual=b.YawTowards(target,rate);
 std::cout<<residual<<' '<<b.mAngles.vy<<' '<<b.mAngVel.vy<<' '<<b.mAngAcc.vy<<'\n';
}}
'''
    with tempfile.TemporaryDirectory(prefix='openspidey-yaw-reference-') as directory:
        cpp = Path(directory)/'reference.cpp';binary = Path(directory)/'reference';cpp.write_text(yaw_code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        result = subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in yaw_cases)+'\n').encode())
    yaw_expected = [list(map(int, line.split())) for line in result.decode().splitlines()]
    yaw_output = [dict(input=c,expected=e) for c,e in zip(yaw_cases,yaw_expected,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/actor-yaw.json').write_text(json.dumps(yaw_output,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} compiled steering sequences / {len(cases)*5} updates and {len(yaw_cases)} direct yaw cases.')


if __name__ == '__main__':
    main()
