"""Capture CVenom node-travel states, the AI fade tail, scripted walkabout exit and PulseL6A4Node from SpideyPC.exe."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path

# Node table shared with VenomAiTests: [type, subtype, x, y, z, linkCount, linkTarget] in whole units.
NODES = [
    [0, 0, 0, 0, 0, 0, 0],
    [1002, 1, 100, 0, 100, 1, 2],
    [5, 0, 300, 0, 100, 1, 3],
    [5, 0, 300, 0, 400, 0, 0],
    [1002, 1, -500, 0, 200, 1, 5],
    [5, 0, -500, 0, 600, 0, 0],
    [1002, 2, 2000, 0, 0, 1, 7],
    [5, 0, 2100, 0, 0, 1, 8],
    [5, 0, 2100, 0, 300, 0, 0],
    [1002, 2, -100, 0, -100, 1, 10],
    [5, 0, -100, 0, -300, 0, 0],
    [1002, 4, 50, 0, 800, 1, 12],
    [5, 0, 50, 0, 900, 0, 0],
    [1002, 4, 900, 0, 50, 1, 14],
    [5, 0, 950, 0, 50, 0, 0],
    [1, 0, 1000, 0, 0, 0, 0],
    [1, 0, 0, 0, 0, 0, 0],
    [1, 0, 7, 0, 0, 0, 0],
]
RANDOM = [7, 3, 11, 0, 5, 2, 9, 14, 1, 6, 13, 8, 4, 12, 10, 15, 3, 3, 0, 1, 2, 7, 7, 5,
          19, 22, 31, 17, 2, 2, 8, 9, 6, 0, 1, 1, 4, 4, 3, 2, 11, 12, 5, 5, 5, 9, 10, 0,
          1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]
FIELDS = ['mode', 'substate', 'flags218', 'level', 'ticks', 'los', 'ground', 'frame', 'finished', 'f3B4',
          'switchCount', 's0', 's1', 's2', 's3', 'px', 'py', 'pz', 'angX', 'angY', 'qx', 'qy', 'qz', 'tx', 'ty', 'tz',
          'node', 'f224', 'l0', 'l1', 'l2', 'l3', 'l4', 'seed', 'itemFlags', 'bodyFlags', 'f3AC', 'f454', 'f458',
          'f45C', 'f33D', 'f388', 'anim', 'f324', 'lastStep', 'difficulty', 'f34D', 'f34C', 'minor', 'pulse',
          'f3B8', 'f330']

CPP = r'''
#include "original_executable.h"
#include <cmath>
#include <iostream>
#include <vector>
#include <string>
#define TC __attribute__((thiscall))
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
void nullsub(){}
struct Node{short w[8];short links[2];int x,y,z;};
std::vector<Node> nodes;std::vector<short*> nodePtrs;
std::vector<int> R;int seed,used,level,los,ground;
std::vector<std::string> events;
alignas(4) unsigned char venom[0x600],player[0x1000],pose[0x100],sw[5][0x200];
int* I(void*b,int o){return (int*)((unsigned char*)b+o);} short* S(void*b,int o){return (short*)((unsigned char*)b+o);}
void L(const std::string&s){events.push_back(s);}
int* getPos(int*out,int n){out[0]=nodes[n].x<<12;out[1]=nodes[n].y<<12;out[2]=nodes[n].z<<12;return out;}
short* getLinks(int n){return nodes[n].links;}
int groundH(int*,int,int,void*){return ground;}
int lineOfSight(int*,int*,int*,int){return los;}
int rnd(int n){int v=R[(seed+used++)%R.size()];return n>0?v%n:0;}
unsigned char xa(int bank,int track,int*,int pri){L("X"+std::to_string(bank)+":"+std::to_string(track)+":"+std::to_string(pri));return 1;}
int sfx(unsigned s,int*,int f){L("S"+std::to_string(s)+":"+std::to_string(f));return 1;}
void TC runAnim(void*self,int a,int b,int c){L("A"+std::to_string(a)+":"+std::to_string(b)+":"+std::to_string(c));*(unsigned short*)((unsigned char*)self+0x12A)=a;}
int levelId(){return level;}
void TC flick(void*self){for(int i=0;i<5;i++)if(self==sw[i])L("F"+std::to_string(i));}
void TC focus(void*,void*t){L(t==venom?"L":"L?");}
void compass(int*p){L("C"+std::to_string(p[0])+":"+std::to_string(p[2]));}
void pulse(int n){L("P"+std::to_string(n));}
void TC die(void*,int a){L("D"+std::to_string(a));}
void TC synth(void*){L("Y");}
void* bitNew(unsigned){return nullptr;}
void TC outlineOn(void*self){*I(self,0x12C)|=4;}
void TC nop1(void*){}
void nopC(){}
void TC nopPose(void*,void*){}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x4015B0,(void*)nullsub);patch(0x4E3940,(void*)getPos);patch(0x4E3880,(void*)getLinks);patch(0x4E6840,(void*)groundH);
patch(0x4E67A0,(void*)lineOfSight);patch(0x4E5DA0,(void*)rnd);patch(0x479D30,(void*)xa);patch(0x471EA0,(void*)sfx);
patch(0x460C10,(void*)runAnim);patch(0x4DE770,(void*)levelId);patch(0x4D1870,(void*)flick);patch(0x4B9E30,(void*)focus);
patch(0x463800,(void*)compass);patch(0x4DFC20,(void*)pulse);patch(0x404320,(void*)die);patch(0x4E9B00,(void*)synth);
patch(0x4088A0,(void*)bitNew);patch(0x460880,(void*)outlineOn);patch(0x4EBDD0,(void*)nop1);patch(0x475F50,(void*)nopC);
patch(0x460E80,(void*)nopPose);patch(0x4F1040,(void*)nop1);
// Disable CVenom::AI's Windows SEH frame bookkeeping only.
std::memset((void*)0x4EC040,0x90,6);*(unsigned short*)0x4EC040=0xC031;std::memset((void*)0x4EC04E,0x90,7);std::memset((void*)0x4ECB22,0x90,7);
int nn;std::cin>>nn;for(int i=0;i<nn;i++){Node n{};int t,s,lc,lt;std::cin>>t>>s>>n.x>>n.y>>n.z>>lc>>lt;n.w[0]=t;n.w[1]=s;n.links[0]=lc;n.links[1]=lt;nodes.push_back(n);}
for(auto&n:nodes)nodePtrs.push_back(n.w);
*(void**)0x6B466C=nodePtrs.data();*(int*)0x6B4670=nn;*(void**)0x6A9038=player;*(int*)0x6B4CA8=4242;
int nr;std::cin>>nr;R.resize(nr);for(int&v:R)std::cin>>v;
int f[52];
while(std::cin>>f[0]){for(int i=1;i<52;i++)std::cin>>f[i];
 memset(venom,0,sizeof venom);memset(player,0,sizeof player);memset(sw,0,sizeof sw);events.clear();used=0;
 int mode=f[0];seed=f[33];level=f[3];los=f[5];ground=f[6];
 int*v=I(venom,0);
 *I(venom,0x320)=f[1];*I(venom,0x218)=f[2];*I(venom,0x80)=f[4];*S(venom,0x128)=f[7];venom[0x142]=f[8];*I(venom,0x3B4)=f[9];
 *I(venom,0x3CC)=f[10];for(int i=0;i<4;i++){*I(sw[i],0x100)=f[11+i];*I(venom,0x3D0+4*i)=(int)sw[i];*I(venom,0x3BC+4*i)=11+2*(i&1);}
 *I(venom,8)=f[15];*I(venom,12)=f[16];*I(venom,16)=f[17];*S(venom,0x14)=f[18];*S(venom,0x16)=f[19];
 *I(player,8)=f[20];*I(player,12)=f[21];*I(player,16)=f[22];*I(venom,0x240)=f[23];*I(venom,0x244)=f[24];*I(venom,0x248)=f[25];
 *S(venom,0x222)=f[26];*S(venom,0x224)=f[27];for(int i=0;i<5;i++)*I(venom,0x438+4*i)=f[28+i];
 *S(venom,0x21E)=100;*(unsigned short*)(venom+4)=f[34];*(unsigned short*)(venom+0x46)=f[35];*I(venom,0x3AC)=f[36];*I(venom,0x454)=f[37];
 *I(venom,0x458)=f[38];*I(venom,0x45C)=f[39];venom[0x33D]=f[40];*I(venom,0x388)=f[41];*(unsigned short*)(venom+0x12A)=f[42];*I(venom,0x324)=f[43];
 *(int*)0x6B4E58=f[44];*(int*)0x54D474=f[45];venom[0x34D]=f[46];venom[0x34C]=f[47];*S(venom,0xDC)=f[48];*I(venom,0x3B8)=f[50];*I(venom,0x330)=f[51];
 *I(venom,0x188)=(int)pose;*I(venom,0x31C)=mode==0?2:mode==1?4:mode==2?8:0x4000;
 if(mode==0)reinterpret_cast<void(TC*)(void*)>(0x4ED790)(venom);
 else if(mode==1)reinterpret_cast<void(TC*)(void*)>(0x4EDBC0)(venom);
 else if(mode==2)reinterpret_cast<void(TC*)(void*)>(0x4EDFF0)(venom);
 else if(mode==3)reinterpret_cast<void(TC*)(void*)>(0x4EC040)(venom);
 else if(mode==4)reinterpret_cast<void(TC*)(void*)>(0x4EB530)(venom);
 else reinterpret_cast<void(TC*)(void*,int)>(0x4F0F00)(venom,f[49]);
 std::cout<<"{\"s\":["<<*I(venom,0x31C)<<','<<*I(venom,0x320)<<','<<*I(venom,0x218)<<','<<*I(venom,8)<<','<<*I(venom,12)<<','<<*I(venom,16)
 <<','<<*I(venom,0x60)<<','<<*I(venom,0x64)<<','<<*I(venom,0x68)<<','<<*S(venom,0x16)<<','<<*S(venom,0x222)<<','<<*S(venom,0x224)
 <<','<<*I(venom,0x240)<<','<<*I(venom,0x244)<<','<<*I(venom,0x248)<<','<<*I(venom,0x3E0)<<','<<*I(venom,0x3B8);
 for(int i=0;i<5;i++)std::cout<<','<<*I(venom,0x438+4*i);
 std::cout<<','<<*(unsigned short*)(venom+4)<<','<<*(unsigned short*)(venom+0x46)<<','<<*I(venom,0x3AC)<<','<<*I(venom,0x454)<<','<<*I(venom,0x458)
 <<','<<*I(venom,0x45C)<<','<<(int)venom[0x33D]<<','<<*S(venom,0xDC)<<','<<*I(venom,0x24)<<','<<((*I(venom,0x12C)>>2)&1)<<','<<((*I(venom,0x120)>>25)&1)
 <<','<<(int)venom[0x124]<<','<<(int)venom[0x125]<<','<<(int)venom[0x126]<<','<<*I(venom,0x388)<<','<<*I(venom,0x330)<<','<<*I(venom,0x324)
 <<','<<*(int*)0x6B4E58<<','<<(int)venom[0x34D]<<','<<(int)venom[0x354]<<','<<*I(venom,0x338)<<','<<used<<"],\"log\":[";
 for(size_t i=0;i<events.size();i++)std::cout<<(i?",":"")<<'"'<<events[i]<<'"';std::cout<<"]}"<<std::endl;}
}
'''


def base(rng, mode):
    c = dict.fromkeys(FIELDS, 0)
    c.update(mode=mode, level=0x604, ticks=2, los=1, ground=-1, minor=0xC0, seed=rng.randrange(64))
    return c


def cases(rng):
    out = []
    for mode in (0, 1, 2):
        for sub in (0, 1, 2, 3, 4):
            if mode != 2 and sub > 2:
                continue
            for variant in range(6):
                c = base(rng, mode)
                c['substate'] = sub
                c['level'] = rng.choice([0x502, 0x604, 0x603])
                c['flags218'] = rng.choice([0, 0x80, 0x81, 0x100, 0x401])
                c['ground'] = rng.choice([-1, 30 << 12, -(20 << 12)])
                c['los'] = rng.choice([0, 1])
                c['ticks'] = rng.choice([1, 2, 4])
                c['frame'] = rng.choice([5, 19, 20, 33])
                c['finished'] = rng.choice([0, 1])
                c['f3B4'] = rng.choice([0, 0, 9])
                c['switchCount'] = 2
                c['s0'], c['s1'] = rng.choice([(1, 1), (1, 2), (2, 1)])
                c['px'], c['py'], c['pz'] = (rng.randrange(-600, 600) << 12, rng.randrange(-50, 50) << 12, rng.randrange(-600, 600) << 12)
                c['angX'], c['angY'] = rng.choice([0, 100, 4000]), rng.randrange(4096)
                if variant < 3:
                    d = rng.randrange(0, 1500)
                    c['qx'], c['qy'], c['qz'] = c['px'] + (d << 12) // 3, c['py'], c['pz'] - (d << 12)
                else:
                    c['qx'], c['qy'], c['qz'] = rng.randrange(-3000, 3000) << 12, 0, rng.randrange(-3000, 3000) << 12
                target_node = rng.choice([2, 3, 7, 8, 10, 12, 14])
                c['node'] = target_node
                c['f224'] = rng.choice([0, 1])
                near = rng.choice([True, False])
                c['tx'] = c['px'] + (rng.randrange(-40, 40) << 12 if near else 900 << 12)
                c['ty'], c['tz'] = c['py'], c['pz'] + (rng.randrange(-40, 40) << 12 if near else 0)
                if mode == 0 and near:
                    c['tx'] = c['px'] + rng.randrange(0, 30 << 12)
                for k in range(5):
                    c[f'l{k}'] = rng.choice([0, 0x32, 0x10032, 0x50032, 0x40033])
                out.append(c)
    # Player straight ahead (aim yaw 2048) to exercise the spot-and-fight exit.
    for mode in (0, 1, 2):
        for sub, los, yaw, ahead in ((2, 1, 2048, 300), (2, 0, 2048, 300), (3, 1, 2048, 1900), (2, 1, 900, 300),
                                     (1, 1, 2048, 300), (2, 1, 2048, 2100)):
            c = base(rng, mode)
            c.update(substate=sub, los=los, angY=yaw, flags218=1, node=3, switchCount=2, s0=1, s1=1)
            c['px'], c['pz'] = 100 << 12, 100 << 12
            c['qx'], c['qz'] = c['px'], c['pz'] + (ahead << 12)
            c['tx'], c['tz'] = c['px'] + (900 << 12), c['pz']
            out.append(c)
    # Fade tail through CVenom::AI with a parked 0x4000 substate.
    for variant in range(40):
        c = base(rng, 3)
        c['substate'] = 7
        c['flags218'] = rng.choice([0x100, 0x80, 0x180, 0, 0x8000, 0x8080])
        c['ticks'] = rng.choice([1, 2, 3, 8, 30])
        c['itemFlags'] = rng.choice([0x480, 0x481, 0xC00, 0x800, 0x485, 0])
        c['bodyFlags'] = rng.choice([0x216, 0x206])
        c['f3AC'] = rng.choice([0, 3, 60, 128, 250])
        c['f454'] = rng.choice([0, 5, 128, 254, 255])
        c['f458'] = rng.choice([0, 30, 49, 60])
        c['f45C'] = rng.choice([0, 30, 49, 60])
        c['f33D'] = rng.choice([0, 1])
        c['f388'] = rng.choice([0, 1, 10])
        c['anim'] = rng.choice([5, 5, 7, 12])
        c['frame'] = rng.choice([3, 8, 18, 25])
        c['f324'] = rng.choice([0, 1, 2, 3])
        c['lastStep'] = rng.choice([245, 246, 247, 0])
        c['level'] = rng.choice([0x502, 0x604, 0x501])
        c['f330'] = rng.choice([0, 5, 40])
        c['px'], c['pz'] = rng.randrange(-3000, 3000) << 12, rng.randrange(-3000, 3000) << 12
        out.append(c)
    # Scripted walkabout (state 0x4000) incl. the l5a1 escape and its exit hooks.
    for variant in range(30):
        c = base(rng, 4)
        c['substate'] = rng.choice([0, 1, 1, 2])
        c['level'] = rng.choice([0x501, 0x501, 0x502, 0x604, 0x505])
        c['flags218'] = rng.choice([0, 0x400, 0x8000])
        c['difficulty'] = rng.choice([0, 1, 2, 3])
        c['f34D'] = rng.choice([0, 1])
        c['f34C'] = rng.choice([0, 1])
        c['switchCount'] = 3
        c['s2'] = rng.choice([1, 2])
        c['px'], c['pz'] = rng.randrange(-200, 200) << 12, rng.randrange(-200, 200) << 12
        dist = rng.choice([5000, 6500, 8000, 9500])
        c['qx'], c['qz'] = c['px'] + (dist << 12) + rng.randrange(-4095, 4095), c['pz'] + rng.randrange(-4095, 4095)
        out.append(c)
    for flag in (0, 1):
        c = base(rng, 5)
        c['pulse'] = flag
        out.append(c)
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x313A1)
    rows = cases(rng)
    with tempfile.TemporaryDirectory(prefix='openspidey-venom-ai-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), '-o', str(binary)], check=True)
        payload = f'{len(NODES)}\n' + '\n'.join(' '.join(map(str, n)) for n in NODES) + '\n'
        payload += f'{len(RANDOM)} ' + ' '.join(map(str, RANDOM)) + '\n'
        payload += '\n'.join(' '.join(str(c[k]) for k in FIELDS) for c in rows) + '\n'
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=payload.encode())
    lines = result.decode().splitlines()
    assert len(lines) == len(rows)
    fixture = dict(nodes=NODES, random=RANDOM, fields=FIELDS,
                   cases=[dict(input=[c[k] for k in FIELDS], **json.loads(line)) for c, line in zip(rows, lines)])
    path = root / 'modern/OpenSpidey.Tests/Fixtures/venom-ai.json'
    path.write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    counts = {}
    for c in rows:
        counts[c['mode']] = counts.get(c['mode'], 0) + 1
    print(f'Captured {len(rows)} CVenom AI cases by mode {counts}.')


if __name__ == '__main__':
    main()
