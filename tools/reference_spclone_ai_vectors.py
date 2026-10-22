"""Capture original CSpClone::AI (0x4AFF30) sequences with its real physics, analogue-input bytecode and CBaddy::Die0."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path
from reference_cinematic_actor_vectors import CPP

CODE = CPP[:CPP.index('int main(')].replace('return 160*4096;', 'return groundResult;')
CODE = CODE.replace('std::vector<int> events;', 'int groundResult;std::vector<int> events;') + r'''
short joints[96];unsigned char* current;
void pulse(unsigned short*p){for(int i=1;i<=p[0];i++)event({7,p[i]});}
void build(void*){event({9});}
void __attribute__((thiscall)) pose(void*self,short*packet){assert(packet==(short*)0x555888);auto*b=(unsigned char*)self;if(!*(void**)(b+0x188)){std::memset(joints,0,sizeof joints);*(void**)(b+0x188)=joints;}event({11});}
void __attribute__((thiscall)) shadow(void*){event({10});}
void diagnostic(int,const char*){}
void hook(int*out,void*,short*h){assert(h[3]==7);out[0]=out[1]=out[2]=0;event({13});}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);*(unsigned char*)0x46cd51=0xc3;initHeading();
patch(0x4581a0,(void*)&alloc);patch(0x458210,(void*)&release);patch(0x4e3940,(void*)&position);patch(0x4e3880,(void*)&getLinks);patch(0x4e6840,(void*)&ground);patch(0x460c10,(void*)&run);patch(0x460d00,(void*)&cycle);patch(0x479ee0,(void*)&voice);
patch(0x4dfd30,(void*)&pulse);patch(0x475f50,(void*)&build);patch(0x460e80,(void*)&pose);patch(0x4b0ef0,(void*)&shadow);patch(0x4015b0,(void*)&diagnostic);patch(0x453ee0,(void*)&hook);
int count;
while(std::cin>>count){
std::vector<short>script(count);int x;for(auto&word:script){std::cin>>x;word=x;}
alignas(4) static unsigned char b[0x400];std::memset(b,0,sizeof b);alignas(4) unsigned char player[0x100]={};
int stage,sub,anim,finished,groundY,gravity,flags,combat,prev,older,running,hasJoints,headX,headY,frames;
std::cin>>stage>>sub>>anim>>finished>>groundY>>gravity>>flags>>combat>>prev>>older>>running>>hasJoints>>headX>>headY;
for(int offset:{8,0x60})for(int axis=0;axis<3;axis++){std::cin>>x;*(int*)(b+offset+axis*4)=x;}
for(int axis=0;axis<3;axis++){std::cin>>x;*(short*)(b+0x14+axis*2)=x;}
for(int axis=0;axis<3;axis++){std::cin>>x;*(int*)(player+8+axis*4)=x;}
*(void**)0x6a9038=player;*(int*)(b+0x31c)=stage;*(int*)(b+0x320)=sub;*(unsigned short*)(b+0x12a)=anim;b[0x142]=finished;
*(short*)(b+0x222)=groundY;*(short*)(b+0x21e)=100;*(short*)(b+0xde)=100;*(int*)(b+0x328)=gravity;*(unsigned short*)(b+4)=flags;*(unsigned short*)(b+0x46)=0x216;*(unsigned*)(b+0x2a8)=combat;
*(int*)(b+0x32c)=prev;*(int*)(b+0x330)=older;b[0x33c]=running;b[0x344]=running;*(short**)(b+0x348)=script.data();
for(int axis=0;axis<3;axis++)b[0x94+axis]=1;
std::memset(joints,0,sizeof joints);if(hasJoints){*(void**)(b+0x188)=joints;joints[0x12]=headX;joints[0x13]=headY;}
std::cin>>frames;std::cout<<'[';
for(int f=0;f<frames;f++){int ticks,abort,fin,signal;std::cin>>ticks>>abort>>fin>>signal>>groundResult;
*(int*)(b+0x80)=ticks;*(unsigned char*)0x60cfc4=abort;if(fin>=0)b[0x142]=fin;*(unsigned short*)(b+0x44)|=signal;events.clear();
reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x4aff30)(b);
short*j=*(short**)(b+0x188);
if(f)std::cout<<',';
std::cout<<'['<<*(int*)(b+0x31c)<<','<<*(int*)(b+0x320)<<','<<*(int*)(b+0x328)<<','<<*(short*)(b+0x222)<<','<<*(int*)(b+0x32c)<<','<<*(int*)(b+0x330)<<','<<*(int*)(b+0x334)<<','
<<*(unsigned short*)(b+4)<<','<<*(unsigned short*)(b+0x46)<<','<<int(b[0x211])<<','<<*(unsigned short*)(b+0x12a)<<','<<int(b[0x142])<<','<<int(b[0x33c])<<','<<int(b[0x344])<<','
<<*(int*)(b+0x340)<<','<<(*(short**)(b+0x348)-script.data())*2<<','<<*(int*)(b+0x324)<<','<<*(unsigned short*)(b+0x44)<<','<<(j?1:0)<<','<<(j?j[0x12]:0)<<','<<(j?j[0x13]:0)<<',';
motion(b);std::cout<<",[";int n=0;for(int*c=*(int**)(b+0x34c);c;c=(int*)c[c[1]-1]){if(n++)std::cout<<',';std::cout<<c[0];}
std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}
std::cout<<"]\n";
while(*(void**)(b+0x34c))reinterpret_cast<void*(__attribute__((thiscall))*)(void*,void*)>(0x4b0350)(b,*(void**)(b+0x34c));
}}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x4AFF30)
    programs = [[0, 255], [0, 6, 1, 4, 0, 6, 8, 0, 0, 255], [0, 8, 7, 4, 0, 6, 3, 6, 0, 255],
                [0, 1, 3, 0, 4, 2, -1, 5, 0, 255], [0, 15, 12, 0, 255], [0, 16, 3, 0, 6, 1, 10, 0, 255],
                [0, 6, 1, 100, 0, 2, 1, 0, 255], [0, 5, 1, 48, 0, 255], [0, 5, 2, 3, 0, 255], [5, 6, 3, 2, 0, 255],
                [-2, 6, 3, 2, 0, 255], [0, 99, 0, 6, 2, 1, 0, 255], [0, 2, 1, 0, 8, 2, 30, 0, 15, 40, 0, 255],
                [3, 2, 2, 0, 5, 1, 7, 0, 255]]
    cases = []
    for program in programs:
        for stage, sub in [(1, 0), (1, 1), (1, 2), (2, 0), (2, 1)]:
            for variation in range(4):
                frames = []
                for frame in range(rng.randint(4, 14)):
                    frames.append([rng.choice([1, 2, 2, 3, 4]), int(variation == 3 and frame == 6),
                                   rng.choice([-1, -1, 0, 1]), int(rng.random() < 0.2),
                                   rng.choice([-1, 160 * 4096, 300 * 4096])])
                cases.append(dict(program=program, start=[
                    stage, sub, rng.choice([0, 1, 13, 21, 0xCA, 203]), rng.randrange(2), rng.choice([-32768, 160, 32767]),
                    rng.choice([0, 8192]), rng.choice([0x482, 0x486]), rng.choice([0, 0x4000]), rng.randrange(5), rng.randrange(5),
                    int(stage == 2 and sub == 1), rng.randrange(2), rng.randint(-200, 200), rng.randint(-200, 200),
                    *[rng.randint(-2000 * 4096, 2000 * 4096) for _ in range(3)], *[rng.randint(-20000, 20000) for _ in range(3)],
                    *[rng.randint(-2048, 2047) for _ in range(3)], *[rng.randint(-2000 * 4096, 2000 * 4096) for _ in range(3)]],
                    frames=frames))
    rows = [' '.join(map(str, [len(case['program']), *case['program'], *case['start'], len(case['frames']),
                                *[v for frame in case['frames'] for v in frame]])) for case in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-spclone-ai-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CODE)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows) + '\n').encode())
    fixture = [dict(**case, expected=json.loads(line)) for case, line in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/spclone-ai.json').write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} original CSpClone::AI sequences ({sum(len(c["frames"]) for c in cases)} AI calls).')


if __name__ == '__main__':
    main()
