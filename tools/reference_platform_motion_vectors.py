"""Compile recovered platform physics and scale commands to produce motion fixtures."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/objects/platform.cpp').read_text()
    physics = controller_method(source, 'void CPlatform::DoPhysics(void)')
    ai = controller_method(source, 'void CPlatform::AI(void)')
    scale_update = ai[ai.index('\ti32 half ='):ai.index('\tthis->mAcc', ai.index('\ti32 half ='))]
    commands = source[source.index('\t\t\tcase 0x4306:'):source.index('\t\t\tcase 0x430A:')]
    harness = r'''
#include "core/fixed_vector.h"
#include <iostream>
CSVector gTrajectoryVector; int level;
int Trig_GetLevelID(){return level;}
struct CBaddy {void DoPhysics(int){throw 1;}};
struct CPlatform: CBaddy {
 CVector mPos,mVel,mAcc;CSVector mAngles,mAngVel,mAngAcc,mScale,field_A8;
 int field_2B0=0,field_2B4=0,mElapsedTicks;short attributeArr[6]={};
 unsigned short field_324=0,field_326=0,field_328=0,mFlags=0;
 short field_32A=0,field_32C=0,field_32E=0;short*field_24C;
 void DoPhysics();void AdvanceScale();int ChangeScale(int);
};
'''
    harness += physics + '\nvoid CPlatform::AdvanceScale(){\n' + scale_update + '}\n'
    harness += 'int CPlatform::ChangeScale(int command){switch(command){\n' + commands + '}return 0;}\n'
    harness += r'''
void emit(CPlatform&p){std::cout<<'['<<p.mPos.vx<<','<<p.mPos.vy<<','<<p.mPos.vz
 <<','<<p.mVel.vx<<','<<p.mVel.vy<<','<<p.mVel.vz
 <<','<<p.mAngles.vx<<','<<p.mAngles.vy<<','<<p.mAngles.vz
 <<','<<p.mAngVel.vx<<','<<p.mAngVel.vy<<','<<p.mAngVel.vz
 <<','<<p.mScale.vx<<','<<p.mScale.vy<<','<<p.mScale.vz
 <<','<<p.field_324<<','<<p.field_326<<','<<p.field_328
 <<','<<p.field_32A<<','<<p.field_32C<<','<<p.field_32E<<']';}
int main(){int ticks,mode,percent,duration;while(std::cin>>ticks>>mode>>level>>percent>>duration){
 CPlatform p;p.mElapsedTicks=ticks;p.attributeArr[2]=mode;
 std::cin>>p.mPos.vx>>p.mPos.vy>>p.mPos.vz>>p.mVel.vx>>p.mVel.vy>>p.mVel.vz>>p.mAcc.vx>>p.mAcc.vy>>p.mAcc.vz
 >>p.mAngles.vx>>p.mAngles.vy>>p.mAngles.vz>>p.mAngVel.vx>>p.mAngVel.vy>>p.mAngVel.vz>>p.mAngAcc.vx>>p.mAngAcc.vy>>p.mAngAcc.vz;
 short args[2]={(short)percent,(short)duration};
 for(int axis=0;axis<3;axis++){p.field_24C=args;p.ChangeScale(0x4306+axis);}
 std::cout<<'[';emit(p);for(int frame=0;frame<10;frame++){p.AdvanceScale();p.DoPhysics();std::cout<<',';emit(p);}std::cout<<"]\n";
}}
'''
    rng = random.Random(402)
    cases = []
    for ticks in [1,2,3,4,7]:
        for mode in [0,1,3]:
            for level in [0x101,0x301]:
                for percent,duration in [(100,0),(25,3),(-100,5),(32767,-1),(-32768,32767)]:
                    values = [rng.randint(-2147483648,2147483647) for _ in range(9)]
                    values += [rng.randint(-32768,32767) for _ in range(9)]
                    cases.append(dict(ticks=ticks,mode=mode,level=level,percent=percent,duration=duration,initial=values))
    # Inclusive dead zones, fractional boundaries, short overflow and source wrapping.
    for velocity in [-2049,-2048,-1,0,1,2048,2049]:
        cases.append(dict(ticks=2,mode=0,level=0x101,percent=10,duration=2,
                          initial=[0,0,0,velocity,velocity,velocity,0,0,0,4095,32767,-32768,1,-1,32767,0,0,2]))
    lines = [' '.join(map(str,[c['ticks'],c['mode'],c['level'],c['percent'],c['duration'],*c['initial']])) for c in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-platform-reference-') as directory:
        cpp = Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(harness)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        results = subprocess.check_output([str(binary)],input='\n'.join(lines).encode()).decode().splitlines()
    for case,result in zip(cases,results,strict=True):case['expected']=json.loads(result)
    (root/'modern/OpenSpidey.Tests/Fixtures/platform-motion.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} platform sequences / {len(cases)*10} updates.')


if __name__ == '__main__':main()
