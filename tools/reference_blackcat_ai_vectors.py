"""Capture original Black Cat AI/CBaddy Die0 and mutable head joints, without replacing behavior/math."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path
from reference_cinematic_actor_vectors import CPP

CODE = CPP[:CPP.index('int main(')].replace('return 160*4096;', 'return groundResult;')
CODE = CODE.replace('std::vector<int> events;', 'int groundResult;std::vector<int> events;') + r'''
int level;int getLevel(){return level;}
void pulse(unsigned short*p){for(int i=1;i<=p[0];i++)event({7,p[i]});}
void build(void*){event({9});}
void __attribute__((thiscall)) pose(void*,short*){event({11});}
void __attribute__((thiscall)) shadow(void*){event({10});}
void sound(unsigned sound,Vec*p,int pitch){event({12,int(sound),p->x,p->y,p->z,pitch});}
void diagnostic(int,const char*){}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);*(unsigned char*)0x46cd51=0xc3;initHeading();
patch(0x4581a0,(void*)&alloc);patch(0x458210,(void*)&release);patch(0x4e3940,(void*)&position);patch(0x4e3880,(void*)&getLinks);patch(0x4e6840,(void*)&ground);patch(0x460c10,(void*)&run);patch(0x460d00,(void*)&cycle);patch(0x479ee0,(void*)&voice);
patch(0x4de770,(void*)&getLevel);patch(0x4dfd30,(void*)&pulse);patch(0x475f50,(void*)&build);patch(0x460e80,(void*)&pose);patch(0x414c50,(void*)&shadow);patch(0x471ea0,(void*)&sound);patch(0x4015b0,(void*)&diagnostic);
int stage,sub,ticks,abort,anim,frame,finished,groundY,gravity,flags,behavior,scriptRunning,headX,headY,hasJoints;
while(std::cin>>level>>stage>>sub>>ticks>>abort>>anim>>frame>>finished>>groundY>>gravity>>flags>>behavior>>scriptRunning>>headX>>headY>>hasJoints>>groundResult){
alignas(4) unsigned char b[0x400]={},player[0x100]={};short joints[96]={},program[]={0,255};int x;
for(int offset:{8,0x60})for(int axis=0;axis<3;axis++){std::cin>>x;*(int*)(b+offset+axis*4)=x;}
for(int axis=0;axis<3;axis++){std::cin>>x;*(int*)(player+8+axis*4)=x;}
*(void**)0x6a9038=player;*(unsigned char*)0x60cfc4=abort;*(int*)(b+0x31c)=stage;*(int*)(b+0x320)=sub;*(int*)(b+0x80)=ticks;*(unsigned short*)(b+0x12a)=anim;*(short*)(b+0x128)=frame;b[0x142]=finished;
*(short*)(b+0x222)=groundY;*(short*)(b+0x21e)=100;*(short*)(b+0xde)=100;*(int*)(b+0x32c)=gravity;*(unsigned short*)(b+4)=flags;*(unsigned short*)(b+0x46)=0x216;*(unsigned*)(b+0x218)=behavior;
b[0x340]=scriptRunning;b[0x348]=scriptRunning;*(short**)(b+0x34c)=program;
for(int axis=0;axis<3;axis++)b[0x94+axis]=1;
if(hasJoints){*(void**)(b+0x188)=joints;joints[0x12]=headX;joints[0x13]=headY;}
events.clear();reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x413b60)(b);
std::cout<<'['<<*(int*)(b+0x31c)<<','<<*(int*)(b+0x320)<<','<<*(int*)(b+0x32c)<<','<<*(short*)(b+0x222)<<','<<*(unsigned*)(b+0x218)<<','<<*(unsigned short*)(b+4)<<','<<*(unsigned short*)(b+0x46)<<','<<int(b[0x211])<<','<<*(unsigned short*)(b+0x12a)<<','<<int(b[0x142])<<','<<int(b[0x340])<<','<<int(b[0x348])<<','<<(*(short**)(b+0x34c)-program)*2<<','<<joints[0x12]<<','<<joints[0x13]<<',';
motion(b);std::cout<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
}}
'''


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args(); root=Path(__file__).resolve().parent.parent
    rng=random.Random(0x413B60); cases=[]
    for level in [257,0x803]:
        for stage,sub in [(1,0),(1,1),(1,2),(2,0),(2,1),(4,0)]:
            for abort in [0,1]:
                for finished in [0,1]:
                    cases.append([level,stage,sub,2,abort,8,18,finished,160,8192,0x486,0,1,0,0,1,160*4096,
                                  400*4096,61*4096,130*4096,512,4096,-512,0,0,0])
    for _ in range(200):
        stage,sub=rng.choice([(1,0),(1,1),(1,2),(2,0),(2,1),(4,0)])
        cases.append([rng.choice([257,0x803]),stage,sub,rng.randrange(5),rng.randrange(2),rng.choice([0,7,8,12]),rng.choice([0,17,18,30]),
                      rng.randrange(2),rng.choice([-32768,160,32767]),rng.choice([0,8192]),rng.choice([0x482,0x486]),rng.randrange(2),rng.randrange(2),
                      rng.randint(-32768,32767),rng.randint(-32768,32767),rng.randrange(2),rng.choice([-1,160*4096,32768*4096]),
                      *[rng.randint(-20000*4096,20000*4096) for _ in range(3)],*[rng.randint(-20000,20000) for _ in range(3)],
                      *[rng.randint(-20000*4096,20000*4096) for _ in range(3)]])
    with tempfile.TemporaryDirectory(prefix='openspidey-blackcat-ai-') as folder:
        cpp,binary=Path(folder)/'reference.cpp',Path(folder)/'reference';cpp.write_text(CODE)
        subprocess.run(['g++','-m32','-std=c++17','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        result=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,row)) for row in cases)+'\n').encode())
    fixture=[dict(input=row,expected=json.loads(line)) for row,line in zip(cases,result.decode().splitlines(),strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/blackcat-ai.json').write_text(json.dumps(fixture,separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} untouched original Black Cat AI cases with real custom physics/VM/Die0/head easing; rendering/world/audio leaves traced.')


if __name__=='__main__':main()
