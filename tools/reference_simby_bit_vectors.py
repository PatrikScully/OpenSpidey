"""Capture the Simby slime bits against SpideyPC.exe: CSimbyShot (0x4A5FD0/0x4A6520), CSimbyDrop (0x4A57E0/0x4A5980),
CSimbyDropSplat (0x4A5470/0x4A5660), CSimbyShotSplat (0x4A5C70/0x4A5E40), Simby_MakeDroplets (0x4A5B40),
Simby_SplattyDeath (0x4A7050) and CSimbyBlob (0x4A6B00/0x4A6C90).

The original constructors and Move methods run with the real CQuadBit constructor, OrientUsing, CVector maths,
Utils_CalcUnitFacingCamera and the PC GTE emulation. Rand, Web_GetGroundY, M3dZone_LineToItem, M3dColij_LineToSphere,
SFX_PlayPos, CQuadBit::SetTexture, M3dUtils_GetDynamicHookPosition, the CBody/InitItem/AttachTo/Die base calls
and child constructors are transport boundaries answering from scripted queues;
every call is recorded.
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
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <vector>
struct Vec{int x,y,z;};
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
template<class T>T&mem(unsigned n){return *reinterpret_cast<T*>(n);}
struct Saved{unsigned a;unsigned char b[5];};std::vector<Saved>saved;
void hook(unsigned a,void*f){Saved s{a};std::memcpy(s.b,(void*)a,5);saved.push_back(s);auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
void unhookAll(){for(auto it=saved.rbegin();it!=saved.rend();++it)std::memcpy((void*)it->a,it->b,5);saved.clear();}
std::vector<std::vector<long long>>events;
void ev(std::initializer_list<long long>v){events.emplace_back(v);}
void push(std::vector<long long>&v,const Vec&p){v.push_back(p.x);v.push_back(p.y);v.push_back(p.z);}
std::vector<int>randQ,groundQ,itemQ,sphereQ;unsigned randI,groundI,itemI,sphereI;
alignas(16)unsigned char player[0x400],hitBody[0x400],item[0x200],frames[32][64];
void*hitVtable[8];std::vector<void*>glows;
int rnd(int bound){int v=randQ[randI++%randQ.size()];ev({1,bound});return bound?(int)((unsigned)v%(unsigned)bound):0;}
int ground(Vec*p){unsigned k=(groundI++%(groundQ.size()/3))*3;ev({2,p->x,p->y,p->z});
 if(groundQ[k+1]==0)mem<void*>(0x5FBEA0)=nullptr;else{mem<void*>(0x5FBEA0)=item;item[5]=groundQ[k+1]==1?1:0;at<int>(item,100)=groundQ[k+2];}
 return groundQ[k];}
void initLine(int*){}
void lineToItem(int*info,int mode){unsigned k=(itemI++%(itemQ.size()/7))*7;
 ev({3,info[0],info[1],info[2],info[3],info[4],info[5],mode,mem<int>(0x5FBEE4)});
 if(!itemQ[k]){mem<void*>(0x5FBEA0)=nullptr;return;}
 mem<void*>(0x5FBEA0)=item;at<int>(item,4)=0;mem<int>(0x5FBEA4)=itemQ[k+1];mem<int>(0x5FBEA8)=itemQ[k+2];mem<int>(0x5FBEAC)=itemQ[k+3];
 mem<short>(0x5FBEB0)=itemQ[k+4];mem<short>(0x5FBEB2)=itemQ[k+5];mem<short>(0x5FBEB4)=itemQ[k+6];}
void*sphere(Vec*a,Vec*b,Vec*out,void*list,void*exclude,int scale){unsigned k=(sphereI++%(sphereQ.size()/4))*4;
 ev({4,a->x,a->y,a->z,b->x,b->y,b->z,list==(void*)player,exclude!=nullptr,scale});
 if(!sphereQ[k])return nullptr;*out={sphereQ[k+1],sphereQ[k+2],sphereQ[k+3]};return hitBody;}
int __attribute__((thiscall))hitFn(void*self,unsigned char*h){ev({5,self==(void*)hitBody,h[0],at<int>(h,4),at<short>(h,8),at<int>(h,12),at<int>(h,16),at<int>(h,20)});return 1;}
int sfx(int id,Vec*p,int arg){ev({6,id,p->x,p->y,p->z,arg});return 1;}
void*bitNew(int size){return std::calloc(1,size+64);}
void __attribute__((thiscall))setTexture(void*,void*t){ev({9,(long long)(unsigned)t});}
void*__attribute__((thiscall))glow(void*self,Vec*p,int a2,int a3,int a4,int a5,int a6,int a7,int a8,int a9,int a10,int a11,int a12,int a13,int a14,int a15,int a16,int a17,int a18,int a19){
 ev({7,p->x,p->y,p->z,a2,a3,a4,a5,a6,a7,a8,a9,a10,a11,a12,a13,a14,a15,a16,a17,a18,a19});glows.push_back(self);return self;}
void*__attribute__((thiscall))dropCtor(void*self,Vec*p,Vec*v,int g,int vy){ev({10,0,p->x,p->y,p->z,v->x,v->y,v->z,g,vy});return self;}
void*__attribute__((thiscall))shotSplatCtor(void*self,Vec*p,short*n){ev({10,1,p->x,p->y,p->z,n[0],n[2],n[4]});return self;}
void*__attribute__((thiscall))dropSplatCtor(void*self,Vec*p,int vy){ev({10,2,p->x,p->y,p->z,vy});return self;}
void*__attribute__((thiscall))blobCtor(void*self,int part,Vec*p,Vec*v,int g){ev({10,3,part,p->x,p->y,p->z,v->x,v->y,v->z,g});return self;}
void hookPosition(Vec*out,unsigned char*super,unsigned short*hook){int part=(short)hook[3];ev({12,part,hook[0],hook[1],hook[2]});
 Vec b=at<Vec>(super,8);*out={b.x+part*4096*37,b.y-part*4096*11,b.z+part*4096*5};}
void*__attribute__((thiscall))bodyCtor(void*self){return self;}
void __attribute__((thiscall))initItem(void*,const char*name){ev({13,std::strcmp(name,"symblob")==0});}
void __attribute__((thiscall))attachTo(void*,void*list){ev({14,list==(void*)0x56EFE4});}
int dieCount;void __attribute__((thiscall))bodyDie(void*){dieCount++;ev({11});}
alignas(4)int fakeModel[4]={20,0,0,0};
void diagnostic(){}
std::vector<int>readInts(int n){std::vector<int>v(n);for(int&x:v)std::cin>>x;return v;}
std::vector<long long>snap(int kind,unsigned char*o){std::vector<long long>s;
 for(unsigned off:{16u,20u,24u,60u,64u,68u,72u,76u,80u,84u,88u,92u})s.push_back(at<int>(o,off));
 s.push_back(at<unsigned>(o,100));s.push_back(at<unsigned>(o,108));s.push_back(at<short>(o,12));s.push_back(o[55]);
 if(kind==0)for(unsigned off=132;off<=180;off+=4)s.push_back(at<int>(o,off));
 else if(kind==1)for(unsigned off:{28u,32u,36u,132u,136u,140u})s.push_back(at<int>(o,off));
 else if(kind==6){s.clear();for(unsigned off:{8u,12u,16u,96u,100u,104u})s.push_back(at<int>(o,off));
  for(unsigned off:{20u,22u,24u,40u,42u,44u,136u,138u,140u})s.push_back(at<short>(o,off));
  for(unsigned off:{248u,252u,256u})s.push_back(at<int>(o,off));s.push_back(at<unsigned short>(o,4));s.push_back(dieCount);}
 else for(unsigned off:{32u,132u,136u,140u,144u,148u,152u,156u,160u,164u,168u,172u})s.push_back(at<int>(o,off));
 return s;}
void neutralizeSeh(unsigned from,unsigned to){for(unsigned char*q=(unsigned char*)from;q<(unsigned char*)to;q++){
 if(q[0]==0x64&&q[1]==0xA1&&!*(int*)(q+2)){q[0]=0x31;q[1]=0xC0;std::memset(q+2,0x90,4);}
 else if(q[0]==0x64&&(q[1]==0x89||q[1]==0x8B)&&(q[2]&0xC7)==0x05&&!*(int*)(q+3))std::memset(q,0x90,7);}}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 for(int i=0;i<4096;i++){reinterpret_cast<short*>(0x610C48)[2*i]=std::sin(i*0.001536096911877394)*4096;reinterpret_cast<short*>(0x610C48)[2*i+1]=std::cos(i*0.001536096911877394)*4096;}
 neutralizeSeh(0x4A5400,0x4A7740);mem<void*>(0x6B2454)=&fakeModel[1];
 for(int s:{6,24,27}){for(int f=0;f<4;f++)at<unsigned>(frames[s],4+8*f)=(s<<8)|f;mem<void*>(0x56EA64+4*s)=frames[s];}
 hitVtable[3]=(void*)&hitFn;*(void***)hitBody=hitVtable;mem<int>(0x6B2EFC)=0;
 int kind;while(std::cin>>kind){
  events.clear();glows.clear();dieCount=0;randI=groundI=itemI=sphereI=0;mem<void*>(0x56EB2C)=nullptr;
  auto h=readInts(16);int steps=h[0];
  mem<int>(0x56F1B4)=h[1];mem<int>(0x56F1B8)=h[2];mem<int>(0x56F1BC)=h[3];
  std::memset(player,0,sizeof player);at<Vec>(player,8)={h[5],h[6],h[7]};mem<void*>(0x6A9038)=h[4]?(void*)player:nullptr;
  int n;std::cin>>n;randQ=readInts(n);std::cin>>n;groundQ=readInts(n*3);std::cin>>n;itemQ=readInts(n*7);std::cin>>n;sphereQ=readInts(n*4);
  hook(0x4E5DA0,(void*)&rnd);hook(0x4F54A0,(void*)&ground);hook(0x4524C0,(void*)&initLine);hook(0x4549A0,(void*)&lineToItem);
  hook(0x452C30,(void*)&sphere);hook(0x471EA0,(void*)&sfx);hook(0x4088A0,(void*)&bitNew);hook(0x455390,(void*)&bitNew);hook(0x409210,(void*)&setTexture);
  hook(0x43BEE0,(void*)&glow);hook(0x4015B0,(void*)&diagnostic);hook(0x453EE0,(void*)&hookPosition);
  hook(0x460080,(void*)&bodyCtor);hook(0x460020,(void*)&initItem);hook(0x460260,(void*)&attachTo);hook(0x4606F0,(void*)&bodyDie);
  if(kind==0){hook(0x4A57E0,(void*)&dropCtor);hook(0x4A5C70,(void*)&shotSplatCtor);}
  else if(kind==1||kind==4){if(kind==4)hook(0x4A57E0,(void*)&dropCtor);hook(0x4A5470,(void*)&dropSplatCtor);}
  else if(kind==5){hook(0x4A57E0,(void*)&dropCtor);hook(0x4A6B00,(void*)&blobCtor);}
  unsigned char*o=(unsigned char*)std::calloc(1,0x200);std::vector<std::vector<long long>>states;
  Vec a{h[8],h[9],h[10]},b{h[11],h[12],h[13]};
  if(kind==0)reinterpret_cast<void*(__attribute__((thiscall))*)(void*,Vec*)>(0x4A5FD0)(o,&a);
  else if(kind==1)reinterpret_cast<void*(__attribute__((thiscall))*)(void*,Vec*,Vec*,int,int)>(0x4A57E0)(o,&a,&b,h[14],h[15]);
  else if(kind==2)reinterpret_cast<void*(__attribute__((thiscall))*)(void*,Vec*,int)>(0x4A5470)(o,&a,h[14]);
  else if(kind==3){short w[6]={(short)h[11],0,(short)h[12],0,(short)h[13],0};reinterpret_cast<void*(__attribute__((thiscall))*)(void*,Vec*,short*)>(0x4A5C70)(o,&a,w);}
  else if(kind==4)reinterpret_cast<int(*)(Vec*,int)>(0x4A5B40)(&a,h[14]);
  else if(kind==5){at<Vec>(o,8)=a;o[31]=0;reinterpret_cast<void*(*)(void*)>(0x4A7050)(o);states.push_back({at<unsigned short>(o,4)});}
  else reinterpret_cast<void*(__attribute__((thiscall))*)(void*,int,Vec*,Vec*,int)>(0x4A6B00)(o,h[15],&a,&b,h[14]);
  if(kind<4){states.push_back(snap(kind,o));
   unsigned move=kind==0?0x4A6520:kind==1?0x4A5980:kind==2?0x4A5660:0x4A5E40;
   for(int i=0;i<steps&&!o[55];i++){reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(move)(o);states.push_back(snap(kind,o));}}
  if(kind==6){states.push_back(snap(kind,o));
   for(int i=0;i<steps&&!dieCount;i++){reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x4A6C90)(o);states.push_back(snap(kind,o));}}
  for(void*g:glows)ev({8,at<short>(g,84)});
  unhookAll();
  std::cout<<"{\"states\":[";for(unsigned i=0;i<states.size();i++){std::cout<<(i?",":"")<<'[';for(unsigned j=0;j<states[i].size();j++)std::cout<<(j?",":"")<<states[i][j];std::cout<<']';}
  std::cout<<"],\"events\":[";
  for(unsigned i=0;i<events.size();i++){std::cout<<(i?",":"")<<'[';for(unsigned j=0;j<events[i].size();j++)std::cout<<(j?",":"")<<events[i][j];std::cout<<']';}
  std::cout<<"]}\n";
 }}
'''

U = 4096


def vec(rng, spread=3000):
    return [rng.randint(-spread, spread) * U + rng.randint(-2048, 2048) for _ in range(3)]


def normal(rng):
    choice = rng.random()
    if choice < 0.25:
        return rng.choice([[0, -4096, 0], [4096, 0, 0], [0, 0, -4096], [0, 4096, 0], [-4096, 0, 0]])
    if choice < 0.35:
        return [0, 0, 0]
    x, y, z = (rng.uniform(-1, 1) for _ in range(3))
    length = max((x * x + y * y + z * z) ** 0.5, 1e-6)
    return [int(x / length * 4096), int(y / length * 4096), int(z / length * 4096)]


def make_case(rng, kind):
    camera = [rng.randint(-4000, 4000) for _ in range(3)]
    origin = vec(rng)
    player = [origin[0] + rng.randint(-1500, 1500) * U, origin[1] + rng.randint(-300, 300) * U,
              origin[2] + rng.randint(-1500, 1500) * U]
    if rng.random() < 0.04:
        player = origin[:]
    present = 0 if rng.random() < 0.04 else 1
    a = origin
    b = [0, 0, 0]
    extra = [0, 0]
    steps = 80
    if kind == 1:
        b = [rng.randint(-30 * U, 30 * U), rng.randint(-40 * U, 5 * U), rng.randint(-30 * U, 30 * U)]
        extra = [origin[1] + rng.randint(-50, 400) * U, rng.choice([0, 0, rng.randint(-8000, 8000)])]
    elif kind == 2:
        extra = [rng.choice([0, 0, rng.randint(-20000, 20000)]), 0]
    elif kind == 3:
        b = normal(rng)
    elif kind == 4:
        extra = [rng.choice([0, 1, 3, 10, 10]), 0]
    elif kind == 6:
        b = [rng.randint(-15, 15) * U, -rng.randint(0, 39) * U, rng.randint(-15, 15) * U]
        extra = [origin[1] + rng.randint(-50, 300) * U, rng.choice([-1, 2, 15])]
    header = [steps, *camera, present, *player, *a, *b, *extra]
    rand = [rng.randrange(2 ** 31) for _ in range(rng.randint(5, 60))]
    grounds = []
    for _ in range(rng.randint(1, 4)):
        grounds += [origin[1] + rng.choice([rng.randint(-50, 400) * U, rng.randint(-20, 60) * U]),
                    rng.choice([0, 0, 1, 2]), rng.randint(-20000, 20000)]
    items = []
    for _ in range(rng.randint(1, 2)):
        hit = int(rng.random() < 0.6)
        point = [player[0] + rng.randint(-400, 400) * U, player[1] + rng.randint(-200, 200) * U,
                 player[2] + rng.randint(-400, 400) * U]
        if rng.random() < 0.5:
            point = [origin[i] + (player[i] - origin[i]) // rng.randint(2, 6) for i in range(3)]
        items += [hit, *point, *normal(rng)]
    spheres = []
    for _ in range(rng.randint(1, 6)):
        spheres += [int(rng.random() < 0.15), *vec(rng)]
    queues = [len(rand), *rand, len(grounds) // 3, *grounds, len(items) // 7, *items, len(spheres) // 4, *spheres]
    return dict(kind=kind, header=header, queues=queues)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    parser.add_argument('--cases', type=int, default=120)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x4A5FD0)
    cases = [make_case(rng, kind) for kind in range(7) for _ in range(args.cases)]
    with tempfile.TemporaryDirectory(prefix='openspidey-simby-bits-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-O0', '-fno-pie', '-no-pie', '-fwrapv',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), '-o', str(binary)], check=True)
        stdin = '\n'.join(' '.join(str(v) for v in [c['kind'], *c['header'], *c['queues']]) for c in cases) + '\n'
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=stdin.encode())
    lines = result.decode().splitlines()
    assert len(lines) == len(cases), (len(lines), len(cases))
    fixtures = []
    for case, line in zip(cases, lines, strict=True):
        output = json.loads(line)
        fixtures.append(dict(kind=case['kind'], header=case['header'], queues=case['queues'],
                             states=output['states'], events=output['events']))
    out = root / 'modern/OpenSpidey.Tests/Fixtures/simby-bits.json'
    out.write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    names = ['CSimbyShot', 'CSimbyDrop', 'CSimbyDropSplat', 'CSimbyShotSplat', 'Simby_MakeDroplets',
             'Simby_SplattyDeath', 'CSimbyBlob']
    hits = sum(1 for c in fixtures if any(e[0] == 5 for e in c['events']))
    bursts = sum(1 for c in fixtures if c['kind'] == 0 and any(e[0] == 10 and e[1] == 1 for e in c['events']))
    splats = sum(1 for c in fixtures if c['kind'] == 1 and any(e[0] == 10 and e[1] == 2 for e in c['events']))
    print(f'Captured {len(fixtures)} Simby bit cases ({", ".join(names)}); {hits} player hits, '
          f'{bursts} wall bursts, {splats} drop splats.')


if __name__ == '__main__':
    main()
