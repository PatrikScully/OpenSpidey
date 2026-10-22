"""Capture CLizMan::Guard, CalculateJumpPositionArray and Jump from SpideyPC.exe."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path

FIELDS = ['mode', 'substate', 'f1F8', 'timer', 'f374', 'pending', 'anim', 'frame', 'ticks', 'scan', 'seed',
          'px', 'py', 'pz', 'tx', 'ty', 'tz', 'ground', 'f944', 'f1F0', 'f1F4', 'minor', 'bodyFlags', 'f394']
RANDOM = [5, 2, 7, 1, 0, 3, 9, 4, 6, 8, 1, 1, 0, 2, 3, 5]

CPP = r'''
#include "original_executable.h"
#include <cmath>
#include <iostream>
#include <string>
#include <vector>
#define TC __attribute__((thiscall))
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
void nullsub(){}
alignas(4) unsigned char liz[0x400],player[0x800],dummy[0x100],arr[0x300];
int*I(void*b,int o){return(int*)((unsigned char*)b+o);} short*S(void*b,int o){return(short*)((unsigned char*)b+o);}
std::vector<std::string> events;std::vector<int> R;int seed,used,scan,ground,target[3];
void L(const std::string&s){events.push_back(s);}
void TC neutralize(void*){L("N");}
void*classNew(unsigned){return dummy;}
void* TC lookAt(void*self,void*,void*body,int*pos,int mask,int rate,int tol){L("K"+std::to_string(body?257:258)+":"+std::to_string(mask)+":"+std::to_string(rate)+":"+std::to_string(tol)+(pos?":"+std::to_string(pos[0])+":"+std::to_string(pos[2]):""));return self;}
void TC runAnim(void*self,int a,int b,int c){L("A"+std::to_string(a));*(unsigned short*)((unsigned char*)self+0x12A)=a;}
int TC scanNodes(void*){L("Q");return scan;}
int rnd(int n){int v=R[(seed+used++)%R.size()];return n>0?v%n:0;}
int sfx(unsigned s,int*,int){L("S"+std::to_string(s));return 1;}
int*getPos(int*out,int n){L("T"+std::to_string(n));out[0]=target[0];out[1]=target[1];out[2]=target[2];return out;}
int groundH(int*,int a,int b,void*){L("g"+std::to_string(a)+":"+std::to_string(b));return ground;}
void*memNew(unsigned,int,int,void*,bool){return arr;}
void memDelete(void*){L("F");}
int TC setHeight(void*,int a,int b,int c){L("H"+std::to_string(a)+":"+std::to_string(b)+":"+std::to_string(c));return 2;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
for(int i=0;i<4096;i++){double a=i*0.001536096911877394;*(short*)(0x610C48+i*4)=(short)(sin(a)*4096);*(short*)(0x610C4A+i*4)=(short)(cos(a)*4096);}
patch(0x4015B0,(void*)nullsub);patch(0x4040E0,(void*)neutralize);patch(0x455390,(void*)classNew);patch(0x401180,(void*)lookAt);
patch(0x460C10,(void*)runAnim);patch(0x44F190,(void*)scanNodes);patch(0x4E5DA0,(void*)rnd);patch(0x471EA0,(void*)sfx);
patch(0x4E3940,(void*)getPos);patch(0x4E6840,(void*)groundH);patch(0x4581A0,(void*)memNew);patch(0x458210,(void*)memDelete);
patch(0x4041C0,(void*)setHeight);*(void**)0x6A9038=player;
// Disable the Windows SEH frame bookkeeping in Guard and Jump only.
for(unsigned a:{0x4500E0u,0x44F427u}){std::memset((void*)a,0x90,6);*(unsigned short*)a=0xC031;}
for(unsigned a:{0x4500EEu,0x450125u,0x4501F3u,0x45025Fu,0x44F42Eu,0x44F477u,0x44F4BDu,0x44F573u,0x44F5E1u,0x44F631u})std::memset((void*)a,0x90,7);
int nr;std::cin>>nr;R.resize(nr);for(int&v:R)std::cin>>v;
int f[24];
while(std::cin>>f[0]){for(int i=1;i<24;i++)std::cin>>f[i];
 memset(liz,0,sizeof liz);memset(arr,0,sizeof arr);events.clear();used=0;seed=f[10];scan=f[9];ground=f[17];
 target[0]=f[14];target[1]=f[15];target[2]=f[16];
 *I(liz,0x320)=f[1];*I(liz,0x1F8)=f[2];*(int*)0x6B4CA8=f[3];*I(liz,0x374)=f[4];*(short*)0x5FBC0C=f[5];*(unsigned short*)(liz+0x12A)=f[6];
 *S(liz,0x128)=f[7];*I(liz,0x80)=f[8];*I(liz,8)=f[11];*I(liz,12)=f[12];*I(liz,16)=f[13];*S(liz,0x21E)=100;
 *I(liz,0x1F0)=f[19];*I(liz,0x1F4)=f[20];*S(liz,0xDC)=f[21];*(unsigned short*)(liz+0x46)=f[22];*I(liz,0x394)=f[23];
 if(f[0]==0){*I(liz,0x31C)=1;reinterpret_cast<void(TC*)(void*)>(0x4500E0)(liz);}
 else{int t[3]={f[14],f[15],f[16]};reinterpret_cast<void(TC*)(void*,int*)>(0x44F2E0)(liz,t);
   if(f[0]==1){events.clear();*I(liz,0x31C)=23;*I(liz,0x3B0)=f[18];reinterpret_cast<void(TC*)(void*)>(0x44F420)(liz);}}
 std::cout<<"{\"s\":["<<*I(liz,0x31C)<<','<<*I(liz,0x320)<<','<<*I(liz,0x1F8)<<','<<*I(liz,0x1F0)<<','<<*I(liz,0x1F4)<<','<<*I(liz,8)<<','<<*I(liz,12)<<','<<*I(liz,16)
 <<','<<*S(liz,0xDC)<<','<<*I(liz,0x394)<<','<<*(unsigned short*)(liz+0x46)<<','<<*I(liz,0x3B0)<<','<<*S(liz,0x128)<<','<<(*I(liz,0x3B4)!=0)<<"],\"arr\":[";
 if(f[0]==2)for(int i=0;i<144;i++)std::cout<<(i?",":"")<<*I(arr,4*i);
 std::cout<<"],\"log\":[";for(size_t i=0;i<events.size();i++)std::cout<<(i?",":"")<<'"'<<events[i]<<'"';std::cout<<"]}"<<std::endl;}
}
'''


def cases(rng):
    out = []
    for mode, count in ((0, 40), (1, 40), (2, 12)):
        for _ in range(count):
            c = dict.fromkeys(FIELDS, 0)
            c.update(mode=mode, seed=rng.randrange(16), ticks=rng.choice([1, 2, 3, 6]), timer=10000,
                     f374=10000 - rng.choice([0x131, 0, 299, 300]), pending=rng.choice([-1, 5, 11]),
                     anim=rng.choice([5, 11, 0, 2]), frame=rng.choice([3, 11, 12, 20, 33, 40]),
                     px=rng.randrange(-500, 500) << 12, py=rng.randrange(-100, 100) << 12, pz=rng.randrange(-500, 500) << 12,
                     tx=rng.randrange(-900, 900) << 12, ty=rng.randrange(-200, 0) << 12, tz=rng.randrange(-900, 900) << 12,
                     ground=rng.choice([-1, rng.randrange(-100, 100) << 12]), minor=0x80, bodyFlags=rng.choice([0x214, 0x21C]),
                     f1F0=rng.choice([0, 2]), f1F4=rng.choice([0, 9]))
            if mode == 0:
                c.update(substate=rng.choice([0, 1, 1]), f1F8=rng.choice([0, 100, 119, 121, 300]), scan=rng.choice([0, 7, 12]))
            elif mode == 1:
                c.update(substate=rng.choice([0, 0, 1, 1, 2]), f944=rng.choice([0, 20, 45, 46, 47]))
            out.append(c)
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rows = cases(random.Random(0x317B2))
    with tempfile.TemporaryDirectory(prefix='openspidey-lizman-guard-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), '-o', str(binary)], check=True)
        payload = f'{len(RANDOM)} ' + ' '.join(map(str, RANDOM)) + '\n'
        payload += '\n'.join(' '.join(str(c[k]) for k in FIELDS) for c in rows) + '\n'
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=payload.encode())
    lines = result.decode().splitlines()
    assert len(lines) == len(rows)
    fixture = dict(fields=FIELDS, random=RANDOM,
                   cases=[dict(input=[c[k] for k in FIELDS], **json.loads(line)) for c, line in zip(rows, lines)])
    path = root / 'modern/OpenSpidey.Tests/Fixtures/lizman-guard.json'
    path.write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    print(f'Captured {len(rows)} CLizMan guard/jump cases.')


if __name__ == '__main__':
    main()
