"""Capture CLizMan::DoAISwitchLogic (fight probe, WarnOtherLizMans, PlayerIsVisible) and ProcessMessages from SpideyPC.exe."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path

FIELDS = ['mode', 'state', 'substate', 'health', 'f390', 'playerHealth', 'playerUnavailable', 'playerFlags', 'timer',
          'f374', 'f380', 'clock', 'phase', 'px', 'py', 'pz', 'angY', 'qx', 'qy', 'qz', 'f2A8', 'sx', 'sy', 'sz', 'f39C',
          'global', 'shooter', 'f1F0', 'f1F4', 'cacheDist', 'cacheClock', 'losPlayer', 'losOther',
          'path0', 'path1', 'path2', 'path3', 'add0', 'add1', 'add2', 'add3', 'hx', 'hy', 'hz',
          'o0x', 'o0y', 'o0z', 'o0t', 'o0type', 'o1x', 'o1y', 'o1z', 'o1t', 'o1type', 'others',
          'm0code', 'm0sender', 'm1code', 'm1sender', 'm2code', 'm2sender', 'messages', 'vx', 'vy', 'vz',
          'senderX', 'senderY', 'senderZ', 'f218', 'f2F0', 'f1F8']

CPP = r'''
#include "original_executable.h"
#include <iostream>
#include <string>
#include <vector>
#define TC __attribute__((thiscall))
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
void nullsub(){}
alignas(4) unsigned char liz[0x400],player[0x1000],others[2][0x400],msgs[3][0x60],sender[0x100],dummy[0x100];
int*I(void*b,int o){return(int*)((unsigned char*)b+o);} short*S(void*b,int o){return(short*)((unsigned char*)b+o);}
std::vector<std::string> events;int paths[4],adds[4],pathUsed,addUsed,losPlayer,losOther,hit[3];
void L(const std::string&s){events.push_back(s);}
int lineOfSight(int*from,int*to,int*,int){L("V"+std::to_string(to==I(player,8)?0:1));return to==I(player,8)?losPlayer:losOther;}
int TC pathCheck(void*,int*from,int*to,int*h,int c){int r=paths[pathUsed++&3];L("P"+std::to_string(to[0])+":"+std::to_string(to[2])+":"+std::to_string(r));if(h&&r){h[0]=hit[0];h[1]=hit[1];h[2]=hit[2];}return r;}
int TC addPoint(void*,int*p,unsigned lim){int r=adds[addUsed++&3];L("A"+std::to_string(p[0])+":"+std::to_string(p[1])+":"+std::to_string(p[2])+":"+std::to_string(lim)+":"+std::to_string(r));return r;}
void TC neutralize(void*){L("N");}
void TC signal(void*){L("G");}
void*classNew(unsigned){return dummy;}
void* TC message(void*self,void*from,void*to,int code,void*){for(int i=0;i<2;i++)if(to==others[i])L("M"+std::to_string(i)+":"+std::to_string(code));return self;}
void*recover(int*h){return *h?(void*)sender:nullptr;}
void TC cleanup(void*,int,int){L("U");}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x4015B0,(void*)nullsub);patch(0x4E67A0,(void*)lineOfSight);patch(0x403310,(void*)pathCheck);patch(0x403350,(void*)pathCheck);
patch(0x4048F0,(void*)addPoint);patch(0x4040E0,(void*)neutralize);patch(0x403230,(void*)signal);patch(0x455390,(void*)classNew);
patch(0x458F10,(void*)message);patch(0x4583F0,(void*)recover);patch(0x403A90,(void*)cleanup);
*(void**)0x6A9038=player;
// Disable the Windows SEH frame bookkeeping in WarnOtherLizMans and ProcessMessages only.
for(unsigned a:{0x44D527u,0x450647u}){std::memset((void*)a,0x90,6);*(unsigned short*)a=0xC031;}
for(unsigned a:{0x44D52Eu,0x44D5FEu,0x45064Eu,0x4508D9u})std::memset((void*)a,0x90,7);
int f[71];
while(std::cin>>f[0]){for(int i=1;i<71;i++)std::cin>>f[i];
 memset(liz,0,sizeof liz);memset(player,0,sizeof player);memset(others,0,sizeof others);memset(msgs,0,sizeof msgs);memset(sender,0,sizeof sender);
 events.clear();pathUsed=addUsed=0;
 *I(liz,0x31C)=f[1];*I(liz,0x320)=f[2];*S(liz,0xE2)=f[3];liz[0x390]=f[4];*S(player,0xE2)=f[5];player[0x57C]=f[6];*(unsigned short*)(player+0x46)=f[7];
 *(int*)0x6B4CA8=f[8];*I(liz,0x374)=f[9];*I(liz,0x380)=f[10];*(int*)0x5FCCF4=f[11];liz[0x21D]=f[12];
 *I(liz,8)=f[13];*I(liz,12)=f[14];*I(liz,16)=f[15];*S(liz,0x16)=f[16];*I(player,8)=f[17];*I(player,12)=f[18];*I(player,16)=f[19];
 *I(liz,0x2A8)=f[20];*I(liz,0x1A8)=f[21];*I(liz,0x1AC)=f[22];*I(liz,0x1B0)=f[23];liz[0x39C]=f[24];
 *(void**)0x682C44=f[25]==0?nullptr:f[25]==1?(void*)liz:(void*)others[0];*(void**)0x682C48=f[26]==0?nullptr:f[26]==1?(void*)liz:(void*)others[0];
 *I(liz,0x1F0)=f[27];*I(liz,0x1F4)=f[28];*I(liz,0x204)=f[29];*I(liz,0x208)=f[30];losPlayer=f[31];losOther=f[32];
 for(int i=0;i<4;i++){paths[i]=f[33+i];adds[i]=f[37+i];}hit[0]=f[41];hit[1]=f[42];hit[2]=f[43];
 int n=f[54];void*head=liz;*(void**)0x56E990=nullptr;
 // BaddyList order: self first, then the others.
 *(void**)0x56E990=liz;*S(liz,0x38)=317;void*prev=liz;
 for(int i=0;i<n;i++){*I(others[i],8)=f[44+5*i];*I(others[i],12)=f[45+5*i];*I(others[i],16)=f[46+5*i];*I(others[i],0x374)=f[47+5*i];*S(others[i],0x38)=f[48+5*i];*(void**)((unsigned char*)prev+0x20)=others[i];prev=others[i];}
 int m=f[61];for(int i=0;i<m;i++){*I(msgs[i],0x14)=f[55+2*i];*I(msgs[i],0x18)=f[56+2*i];*I(msgs[i],0x28)=f[62];*I(msgs[i],0x2C)=f[63];*I(msgs[i],0x30)=f[64];if(i)*(void**)(msgs[i-1]+8)=msgs[i];}
 *(void**)(liz+0x290)=m?(void*)msgs[0]:nullptr;*I(sender,8)=f[65];*I(sender,12)=f[66];*I(sender,16)=f[67];
 *I(liz,0x218)=f[68];*I(liz,0x2F0)=f[69];*I(liz,0x1F8)=f[70];*(void**)liz=(void*)0x53B9AC;
 if(f[0]==0)reinterpret_cast<int(TC*)(void*)>(0x451280)(liz);
 else reinterpret_cast<int(TC*)(void*)>(0x450640)(liz);
 auto ident=[](void*p){return p==nullptr?0:p==liz?1:2;};
 std::cout<<"{\"s\":["<<*I(liz,0x31C)<<','<<*I(liz,0x320)<<','<<*I(liz,0x380)<<','<<*I(liz,0x374)<<','<<*I(liz,0x2A8)<<','<<*I(liz,0x1A8)<<','<<*I(liz,0x1AC)<<','<<*I(liz,0x1B0)
 <<','<<*I(liz,0x204)<<','<<*I(liz,0x208)<<','<<ident(*(void**)0x682C44)<<','<<ident(*(void**)0x682C48)<<','<<*I(liz,0x310)<<','<<*I(liz,0x1F8)<<','<<*I(liz,0x2F0);
 for(int i=0;i<m;i++)std::cout<<','<<*I(msgs[i],0x10);
 std::cout<<"],\"log\":[";for(size_t i=0;i<events.size();i++)std::cout<<(i?",":"")<<'"'<<events[i]<<'"';std::cout<<"]}"<<std::endl;}
}
'''


def base(rng):
    c = dict.fromkeys(FIELDS, 0)
    c.update(health=100, playerHealth=100, timer=10000, f374=10000 - 0x131, clock=rng.randrange(1, 4000),
             phase=rng.randrange(256), playerFlags=0x16, hx=11 << 12, hy=0, hz=22 << 12)
    for k in ('path0', 'path1', 'path2', 'path3'):
        c[k] = rng.choice([0, 0, 1, 2])
    for k in ('add0', 'add1', 'add2', 'add3'):
        c[k] = rng.choice([0, 1, 1])
    return c


def place(rng, c, near):
    c['px'], c['py'], c['pz'] = rng.randrange(-500, 500) << 12, rng.choice([0, 20, -60]) << 12, rng.randrange(-500, 500) << 12
    dist = rng.choice([50, 400, 900, 1300, 1700, 2500]) if near else rng.randrange(0, 4000)
    c['qx'], c['qy'], c['qz'] = c['px'] + (dist << 12), c['py'] + (rng.choice([0, 0, 30, 70]) << 12), c['pz'] + (rng.randrange(-300, 300) << 12)
    c['angY'] = rng.choice([3072, 1024, 0, 2048, 3000])


def cases(rng):
    out = []
    for variant in range(140):
        c = base(rng)
        c['mode'] = 0
        c['state'] = rng.choice([1, 2, 25, 25, 3, 7, 1, 2])
        c['substate'] = rng.choice([0, 1])
        c['f380'] = rng.choice([0, 0, 3, 31])
        c['f374'] = c['timer'] - rng.choice([0x131, 10, 299, 300, 5000])
        c['clock'] = rng.choice([0x20 - c['phase'] & 0xFF, rng.randrange(4000)]) + rng.choice([0, 256])
        c['health'] = rng.choice([100, 100, 0])
        c['f390'] = rng.choice([0, 0, 0, 1])
        c['playerHealth'] = rng.choice([100, 100, 100, 0])
        c['playerUnavailable'] = rng.choice([0, 0, 0, 1])
        c['playerFlags'] = rng.choice([0x16, 0x16, 0x56])
        place(rng, c, near=True)
        c['f2A8'] = rng.choice([0, 0x800, 0x2000, 0x800])
        c['sx'], c['sy'], c['sz'] = c['px'] + (rng.randrange(-2000, 2000) << 12), c['py'], c['pz']
        c['f39C'] = rng.choice([0, 1, 2])
        c['global'] = rng.choice([0, 0, 1, 2])
        c['shooter'] = rng.choice([0, 0, 1, 2])
        c['f1F0'] = rng.choice([0, 0, 1])
        c['f1F4'] = rng.choice([0, 5, -1])
        c['cacheClock'] = rng.choice([0, c['clock'], c['clock'] - 2, c['clock'] - 5])
        c['cacheDist'] = rng.choice([123, 3000])
        c['losPlayer'] = rng.choice([0, 1, 1])
        c['losOther'] = rng.choice([0, 1])
        c['others'] = rng.choice([0, 1, 2])
        for i in range(2):
            c[f'o{i}x'] = c['px'] + (rng.choice([100, 1000, 3000]) << 12)
            c[f'o{i}y'] = c['py'] + (rng.choice([0, 255, 256, -300]) << 12)
            c[f'o{i}z'] = c['pz']
            c[f'o{i}t'] = c['timer'] - rng.choice([0, 299, 300, 9999])
            c[f'o{i}type'] = rng.choice([317, 317, 306])
        out.append(c)
    for variant in range(80):
        c = base(rng)
        c['mode'] = 1
        c['state'] = rng.choice([1, 2, 14, 20, 15, 3])
        c['substate'] = rng.choice([0, 2, 3])
        c['f390'] = rng.choice([0, 0, 1])
        c['playerUnavailable'] = rng.choice([0, 0, 1])
        place(rng, c, near=False)
        c['messages'] = rng.choice([1, 2, 3])
        for i in range(3):
            c[f'm{i}code'] = rng.choice([7, 7, 10, 10, 5, 1, 30, 6 if c['state'] == 15 else 7])
            c[f'm{i}sender'] = rng.choice([0, 1, 1])
        c['vx'], c['vy'], c['vz'] = c['px'] + (rng.randrange(-1600, 1600) << 12), c['py'], c['pz'] + (rng.randrange(-400, 400) << 12)
        c['senderX'], c['senderY'], c['senderZ'] = c['px'] + (rng.randrange(-1600, 1600) << 12), c['py'], c['pz'] + (rng.randrange(-400, 400) << 12)
        c['hx'], c['hy'], c['hz'] = c['px'] + (rng.choice([50, 300, 900]) << 12), c['py'], c['pz'] + (rng.randrange(-90, 90) << 12)
        c['f2F0'] = rng.choice([0, 2, 0x20])
        c['f2A8'] = rng.choice([0, 0x10000000])
        c['f1F8'] = rng.choice([0, 77])
        c['f218'] = rng.choice([0, 0x40])
        out.append(c)
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rows = cases(random.Random(0x317A1))
    with tempfile.TemporaryDirectory(prefix='openspidey-lizman-ai-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), '-o', str(binary)], check=True)
        payload = '\n'.join(' '.join(str(c[k]) for k in FIELDS) for c in rows) + '\n'
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=payload.encode())
    lines = result.decode().splitlines()
    assert len(lines) == len(rows)
    fixture = dict(fields=FIELDS, cases=[dict(input=[c[k] for k in FIELDS], **json.loads(line)) for c, line in zip(rows, lines)])
    path = root / 'modern/OpenSpidey.Tests/Fixtures/lizman-ai.json'
    path.write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    print(f'Captured {len(rows)} CLizMan AI cases ({sum(1 for c in rows if c["mode"] == 0)} DoAISwitchLogic, '
          f'{sum(1 for c in rows if c["mode"] == 1)} ProcessMessages).')


if __name__ == '__main__':
    main()
