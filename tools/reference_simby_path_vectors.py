"""Capture CSimby target planning against SpideyPC.exe: GetToTarget (0x4A9F00), SetUpJumpPath (0x4A9A10),
RegisterTargetPos (0x4A2680), the attack-slot pick (0x4A94B0), AttemptAttackMethod (0x4A9620) and
MoveInForAttack (0x4A9830).

The original functions run with their real vector math, Utils_CrapDist/CrapXZDist, Utils_CalcAim,
Utils_GetVecFromMagDir, DistanceToPlayer cache and the shared slot globals. PathCheck, ground, line-of-sight,
object-collision and LineToItem queries, Neutralize and RunAnim are transport boundaries: they answer from
scripted queues and every call is recorded.
"""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path

CPP = r'''
#include "original_executable.h"
#include <cmath>
#include <cstring>
#include <iostream>
#include <vector>
struct Vec{int x,y,z;};
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
template<class T>T&mem(unsigned n){return *reinterpret_cast<T*>(n);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
alignas(4)unsigned char actor[0x480],player[0xF20];
std::vector<std::vector<long long>>events;
std::vector<int>pathQ,groundQ,losQ,objQ,lineQ;unsigned pathI,groundI,losI,objI,lineI;
void ev(std::initializer_list<long long>v){events.emplace_back(v);}
void push(std::vector<long long>&v,const Vec&p){v.push_back(p.x);v.push_back(p.y);v.push_back(p.z);}
int __attribute__((thiscall)) pathCheck(void*self,Vec*a,Vec*b,Vec*out,int range){
 unsigned k=(pathI++%(pathQ.size()/4))*4;std::vector<long long>e{1};push(e,*a);push(e,*b);e.push_back(out!=nullptr);e.push_back(range);
 e.push_back(at<unsigned>(self,0x2A8));Vec initial=out?*out:Vec{0,0,0};push(e,initial);events.push_back(e);
 if(out)*out={pathQ[k+1],pathQ[k+2],pathQ[k+3]};return pathQ[k];}
int ground(Vec*p,int above,int below,void**support){unsigned k=(groundI++%(groundQ.size()/2))*2;
 ev({2,p->x,p->y,p->z,above,below,support!=nullptr});if(support)*support=groundQ[k+1]?(void*)player:nullptr;return groundQ[k];}
int los(Vec*a,Vec*b,Vec*out,int flags){unsigned k=(losI++%(losQ.size()/4))*4;
 ev({3,a->x,a->y,a->z,b->x,b->y,b->z,flags,out!=nullptr});if(!losQ[k]&&out)*out={losQ[k+1],losQ[k+2],losQ[k+3]};return losQ[k];}
void*objectCollision(Vec*a,Vec*b,void*list,void*self){unsigned k=objI++%objQ.size();
 ev({4,a->x,a->y,a->z,b->x,b->y,b->z,list==(void*)player,self==(void*)actor});return objQ[k]?(void*)player:nullptr;}
void initLine(int*info){}
void lineToItem(int*info,int mode){unsigned k=(lineI++%(lineQ.size()/2))*2;
 ev({5,info[0],info[1],info[2],info[3],info[4],info[5],mode});info[26]=lineQ[k];info[28]=lineQ[k+1];}
void __attribute__((thiscall)) neutralize(void*){ev({6});}
void __attribute__((thiscall)) runAnim(void*,int id,int from,int to){ev({7,id,from,to});}
void diagnostic(){}
std::vector<int>readInts(int n){std::vector<int>v(n);for(int&x:v)std::cin>>x;return v;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 for(int i=0;i<4096;i++){reinterpret_cast<short*>(0x610C48)[2*i]=sin(i*0.001536096911877394)*4096;reinterpret_cast<short*>(0x610C48)[2*i+1]=cos(i*0.001536096911877394)*4096;}
 patch(0x403310,(void*)&pathCheck);patch(0x4E6840,(void*)&ground);patch(0x4E67A0,(void*)&los);patch(0x4E6470,(void*)&objectCollision);
 patch(0x4524C0,(void*)&initLine);patch(0x4549A0,(void*)&lineToItem);patch(0x4040E0,(void*)&neutralize);
 patch(0x460C10,(void*)&runAnim);patch(0x4015B0,(void*)&diagnostic);
 int fn;while(std::cin>>fn){
  std::memset(actor,0,sizeof actor);std::memset(player,0,sizeof player);events.clear();pathI=groundI=losI=objI=lineI=0;
  auto h=readInts(78);int arg=h[0];
  at<int>(actor,0x31C)=h[1];at<int>(actor,0x320)=h[2];at<unsigned>(actor,0x218)=h[3];at<unsigned>(actor,0x2A8)=h[4];
  at<unsigned short>(actor,0xDC)=h[5];at<short>(actor,0x21E)=h[6];at<int>(actor,0x298)=h[7];at<int>(actor,0x294)=h[8];
  at<Vec>(actor,8)={h[9],h[10],h[11]};at<short>(actor,0x16)=h[12];at<Vec>(actor,0x35C)={h[13],h[14],h[15]};
  at<Vec>(actor,0x368)={h[16],h[17],h[18]};at<Vec>(actor,0x374)={h[19],h[20],h[21]};at<Vec>(actor,0x380)={h[22],h[23],h[24]};
  at<int>(actor,0x398)=h[25];at<int>(actor,0x39C)=h[26];at<int>(actor,0x3AC)=h[27];at<int>(actor,0x3F0)=h[28];
  at<void*>(actor,0x2A4)=h[29]?(void*)player:nullptr;at<unsigned char>(actor,0x142)=h[30];at<int>(actor,0x208)=h[31];at<int>(actor,0x204)=h[32];
  at<Vec>(player,8)={h[33],h[34],h[35]};at<short>(player,0xE2)=h[36];at<unsigned char>(player,0x57C)=h[37];
  mem<int>(0x5FCCF4)=h[38];mem<int>(0x682C60)=h[39];mem<unsigned>(0x682C68)=h[40];
  for(int i=0;i<8;i++)mem<Vec>(0x6A7B10+i*12)={h[41+i*3],h[42+i*3],h[43+i*3]};mem<Vec>(0x6A7B04)={0,0,0};
  mem<short>(0x6B4C50)=h[65];mem<short>(0x6B4C52)=h[66];mem<short>(0x6B4C54)=h[67];mem<void*>(0x6A9038)=player;
  at<int>(actor,0x3A8)=h[68];at<int>(actor,0x3CC)=h[69];at<int>(actor,0x3D0)=h[70];at<int>(actor,0x23C)=0;
  auto q=[](int n){return readInts(n);};
  int n;std::cin>>n;pathQ=q(n*4);std::cin>>n;groundQ=q(n*2);std::cin>>n;losQ=q(n*4);std::cin>>n;objQ=q(n);std::cin>>n;lineQ=q(n*2);
  long long result=0;
  if(fn==0)result=reinterpret_cast<int(__attribute__((thiscall))*)(void*,int)>(0x4A9F00)(actor,arg);
  else if(fn==1)result=reinterpret_cast<int(__attribute__((thiscall))*)(void*)>(0x4A9A10)(actor);
  else if(fn==2){Vec t=at<Vec>(actor,0x35C);result=reinterpret_cast<int(__attribute__((thiscall))*)(void*,Vec*)>(0x4A2680)(actor,&t);}
  else if(fn==3)result=reinterpret_cast<int(__attribute__((thiscall))*)(void*)>(0x4A94B0)(actor);
  else if(fn==4)result=reinterpret_cast<int(__attribute__((thiscall))*)(void*,int)>(0x4A9620)(actor,arg);
  else reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x4A9830)(actor);
  std::vector<long long>s{result,at<int>(actor,0x31C),at<int>(actor,0x320),at<unsigned>(actor,0x218),at<unsigned>(actor,0x2A8),
   at<int>(actor,0x294),at<int>(actor,0x298)};
  for(unsigned o:{0x35Cu,0x368u,0x374u,0x380u})push(s,at<Vec>(actor,o));
  for(unsigned o:{0x398u,0x39Cu,0x3A8u,0x3ACu,0x3CCu,0x3D0u,0x3F0u,0x204u,0x208u})s.push_back(at<int>(actor,o));
  s.push_back(mem<int>(0x682C60));s.push_back(mem<unsigned>(0x682C68));
  for(int i=0;i<8;i++)push(s,mem<Vec>(0x6A7B10+i*12));push(s,mem<Vec>(0x6A7B04));
  std::cout<<"{\"state\":[";for(unsigned i=0;i<s.size();i++)std::cout<<(i?",":"")<<s[i];std::cout<<"],\"events\":[";
  for(unsigned i=0;i<events.size();i++){std::cout<<(i?",":"")<<'[';for(unsigned j=0;j<events[i].size();j++)std::cout<<(j?",":"")<<events[i][j];std::cout<<']';}
  std::cout<<"]}\n";
 }}
'''

U = 4096


def vec(rng, spread=2000):
    return [rng.randint(-spread, spread) * U + rng.randint(-2048, 2048) for _ in range(3)]


def make_case(rng, fn):
    pos = vec(rng)
    player = [pos[0] + rng.randint(-1600, 1600) * U, pos[1] + rng.choice([0, 0, rng.randint(-200, 200)]) * U,
              pos[2] + rng.randint(-1600, 1600) * U]
    yaw = rng.randrange(4096)
    unit = [rng.randint(-4096, 4096), rng.choice([0, 0, rng.randint(-200, 200)]), rng.randint(-4096, 4096)]
    target = [pos[0] + rng.randint(-1500, 1500) * U, pos[1] + rng.randint(-100, 100) * U,
              pos[2] + rng.randint(-1500, 1500) * U]
    stage = rng.choice([2, 4, 9, 9, 6])
    behavior = rng.choice([0, 0, 0x200000, 0x400000, 0x1, 0x8]) | rng.choice([0, 0x100])
    combat = rng.choice([0x201, 0x10201, 0x2201])
    setup_high = rng.choice([261, 261, 261, 0x10105])
    mask = rng.randrange(256) if rng.random() < 0.7 else 0xFF
    slot = rng.choice([0, 0, rng.randint(1, 8)])
    slots = []
    for _ in range(8):
        near = rng.random() < 0.3
        base = target if near else vec(rng)
        slots += [base[0] + rng.randint(-200, 200) * U, base[1], base[2] + rng.randint(-200, 200) * U]
    clock = rng.randint(10, 5000)
    dist_clock = rng.choice([0, clock - rng.randint(0, 6)])
    header = [rng.choice([0, 1, 2]) if fn == 0 else rng.choice([0, 1]),
              stage, rng.choice([0, 1, 2, 10]) if fn == 5 else 0, behavior, combat,
              rng.choice([140, 140, 100, 60]), rng.choice([100, 100, 60, 150]), setup_high, 84215815,
              *pos, yaw, *target, *unit, rng.randint(-4096, 4096), 0, rng.randint(-4096, 4096),
              *vec(rng), rng.randrange(4096), rng.choice([0, 1365, 2730]), slot,
              rng.choice([0, 1, 2, 4]), int(rng.random() < 0.2), rng.choice([0, 1]), dist_clock, rng.randint(0, 3000),
              *player, rng.choice([100, 100, 0]), rng.choice([0, 0, 0, 1]), clock,
              rng.choice([0, 0, 1, 2, 4, 3, 5, 6, 7]), mask, *slots,
              rng.randint(-4096, 4096), rng.randint(-4096, 4096), rng.randint(-4096, 4096),
              rng.randint(0, 900), rng.randint(0, 60), rng.randint(0, 300), 0, 0, 0, 0, 0, 0, 0]
    header = header[:78]
    assert len(header) == 78
    paths = []
    for _ in range(rng.randint(1, 4)):
        hit = [target[0] + rng.randint(-1200, 1200) * U, target[1] + rng.randint(-1500, 300) * U,
               target[2] + rng.randint(-1200, 1200) * U]
        paths += [rng.choice([0, 0, 2, 2, 1, 4]), *hit]
    base = target[1] + rng.randint(-50, 50) * U
    grounds = []
    for _ in range(rng.randint(1, 5)):
        y = -1 if rng.random() < 0.12 else base + rng.choice([0, 0, rng.randint(-0x3000, 0x3000)])
        grounds += [y, int(rng.random() < 0.15)]
    loses = []
    for _ in range(rng.randint(1, 2)):
        loses += [int(rng.random() < 0.6), *vec(rng)]
    objs = [int(rng.random() < 0.15) for _ in range(rng.randint(1, 2))]
    lines = []
    for _ in range(1):
        lines += [int(rng.random() < 0.5), pos[1] - rng.randint(500, 3000) * U]
    tail = [len(paths) // 4, *paths, len(grounds) // 2, *grounds, len(loses) // 4, *loses, len(objs), *objs,
            len(lines) // 2, *lines]
    return dict(fn=fn, header=header, queues=tail)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    parser.add_argument('--cases', type=int, default=150)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x5135B7)
    cases = [make_case(rng, fn) for fn in range(6) for _ in range(args.cases)]
    with tempfile.TemporaryDirectory(prefix='openspidey-simby-path-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-O0', '-fno-pie', '-no-pie', '-fwrapv',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), '-o', str(binary)], check=True)
        stdin = '\n'.join(' '.join(str(v) for v in [c['fn'], *c['header'], *c['queues']]) for c in cases) + '\n'
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=stdin.encode())
    lines = result.decode().splitlines()
    assert len(lines) == len(cases), (len(lines), len(cases))
    fixtures = []
    for case, line in zip(cases, lines, strict=True):
        output = json.loads(line)
        fixtures.append(dict(fn=case['fn'], header=case['header'], queues=case['queues'],
                             state=output['state'], events=output['events']))
    out = root / 'modern/OpenSpidey.Tests/Fixtures/simby-path.json'
    out.write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    names = ['GetToTarget', 'SetUpJumpPath', 'RegisterTargetPos', 'PickAttackSlot', 'AttemptAttackMethod', 'MoveInForAttack']
    summary = ', '.join(f"{sum(1 for c in fixtures if c['fn'] == i)} {name}" for i, name in enumerate(names))
    returns = sum(1 for c in fixtures if c['state'][0] != 0)
    print(f'Captured {len(fixtures)} Simby path cases ({summary}); {returns} returned non-zero.')


if __name__ == '__main__':
    main()
