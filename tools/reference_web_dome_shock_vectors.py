"""Verify CDomeShockWave ctor, full Move and both hit-list walks against actual PC instructions."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True);args=parser.parse_args()
    root=Path(__file__).resolve().parent.parent
    code=r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
#include <sstream>
#include <cassert>
#include <cstring>
struct Bit{unsigned vt=0,previous=0,next=0;short age=0;unsigned short lifetime=0;CVector pos,vel,acc;unsigned char friction[3]={1,1,1},dead=0;unsigned short depth=0;unsigned char protect=0,type=0;};
struct Shock:Bit{int unused=0,height=0;CVector colour{240,240,240};int phases[16]={};int fire=0,radius=0;};static_assert(sizeof(Shock)==0x98);
struct Target{unsigned char raw[0xF4]={};CVector&pos(){return *reinterpret_cast<CVector*>(raw+8);}unsigned short&flags(){return *reinterpret_cast<unsigned short*>(raw+0x46);}unsigned short&type(){return *reinterpret_cast<unsigned short*>(raw+0x38);}unsigned&next(){return *reinterpret_cast<unsigned*>(raw+0x20);}};
struct Hit{unsigned char flags=0,part=0;unsigned short pad=0;int kind=0;unsigned short damage=0,padA=0;CVector direction;short slide=0;unsigned short frames=0;};
std::vector<int>events;int cursor;Target targets[7];int lowMemory;
void emit(std::initializer_list<int>v){events.insert(events.end(),v);}
int Rnd(int bound){int value=(cursor++*7+1)%bound;emit({1,bound,value});return value;}
int identity(void*p){for(int i=0;i<7;i++)if(p==&targets[i])return i;assert(false);return-1;}
// Original4FAF40 writes only flagsbyte, not the uninitialized partbyte. Compare every written field; modern defaults part to0.
void __attribute__((thiscall)) hitTarget(void*p,Hit*h){emit({5,identity(p),h->flags,0,h->kind,h->damage,h->direction.vx,h->direction.vy,h->direction.vz,h->slide,h->frames});}
void __attribute__((thiscall)) setFlaming(void*p,int frames,int r,int g,int b){emit({4,identity(p),frames,r,g,b});}
void glow(CVector*p,int size,unsigned char r,unsigned char g,unsigned char b,int growth,int a,int d){emit({3,identity(reinterpret_cast<unsigned char*>(p)-8),size,r,g,b,growth,a,d});}
void __attribute__((thiscall)) shake(void*,CVector*p,int magnitude){emit({2,p->vx,p->vy,p->vz,magnitude});}
unsigned distance(CVector a,CVector b){CVector d=a-b;int x=d.vx<0?-d.vx:d.vx,y=d.vy<0?-d.vy:d.vy,z=d.vz<0?-d.vz:d.vz;if(x<y)std::swap(x,y);if(y<z)std::swap(y,z);if(x<y)std::swap(x,y);return unsigned((x+(y>>1)+(z>>2))>>12);}
void normalize(CVector&v){reinterpret_cast<void(*)(CVector*,CVector*)>(0x470430)(&v,&v);}
void hitList(Shock&s,int first,int last,bool baddy){for(int i=first;i<last;i++){auto&t=targets[i];auto flags=t.flags();if(!(flags&0x10)||(flags&0x100)||t.type()==401)continue;int height=(s.pos.vy-t.pos().vy)>>12;if(height<=-300||height>=300||int(distance(s.pos,t.pos()))>s.radius)continue;
 if(s.fire&&t.type()==324)setFlaming(&t,20,255,123,0);else if(baddy&&!lowMemory)glow(&t.pos(),70,128,128,128,5,0,100);
 Hit h;h.flags=30;h.kind=s.fire?22:21;h.damage=s.radius>500?0:200-200*s.radius/500;h.direction=t.pos()-s.pos;h.direction.vy=0;h.direction>>=12;normalize(h.direction);h.slide=512;h.frames=15;hitTarget(&t,&h);t.flags()|=0x100;}}
void init(Shock&s,CVector player,int fire){s.type=8;s.pos=player;s.pos.vy+=204800;s.fire=fire;for(auto&t:targets)t.flags()&=~0x100;for(auto&p:s.phases)p=Rnd(4096);shake(nullptr,&s.pos,1);}
void move(Shock&s){s.radius+=140;if(s.radius>2000)s.height+=100;s.colour-=CVector(16,16,16);if(s.colour.vx<=0&&s.colour.vy<=0&&s.colour.vz<=0)s.dead=1;hitList(s,0,4,true);hitList(s,4,7,false);}
void output(Shock&s){std::cout<<'['<<s.pos.vx<<','<<s.pos.vy<<','<<s.pos.vz<<','<<s.colour.vx<<','<<s.colour.vy<<','<<s.colour.vz<<','<<s.fire<<','<<s.radius<<','<<s.height<<','<<int(s.dead)<<','<<int(s.type)<<",[";for(int i=0;i<16;i++){if(i)std::cout<<',';std::cout<<s.phases[i];}std::cout<<"],[";for(int i=0;i<7;i++){if(i)std::cout<<',';std::cout<<targets[i].flags();}std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}
std::string capture(Shock&s){std::ostringstream t;auto*old=std::cout.rdbuf(t.rdbuf());output(s);std::cout.rdbuf(old);events.clear();return t.str();}
void patch(unsigned a,void*t){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(t)-a-5;}
void*__attribute__((thiscall)) baseCtor(void*p){*static_cast<Bit*>(p)=Bit();return p;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x408B10,reinterpret_cast<void*>(&baseCtor));patch(0x4E5DA0,reinterpret_cast<void*>(&Rnd));patch(0x416880,reinterpret_cast<void*>(&shake));patch(0x43C150,reinterpret_cast<void*>(&glow));
for(auto range:std::vector<std::pair<unsigned,int>>{{0x4FAE27,6},{0x4FAE2E,7},{0x4FAEFA,7}})std::memset(reinterpret_cast<void*>(range.first),0x90,range.second);
void*table[18]={};table[3]=reinterpret_cast<void*>(&hitTarget);table[17]=reinterpret_cast<void*>(&setFlaming);
CVector player;int fire,seed;while(std::cin>>player.vx>>player.vy>>player.vz>>fire>>lowMemory>>seed){unsigned short flags[7];for(int i=0;i<7;i++){int x,y,z,type,f;std::cin>>x>>y>>z>>type>>f;targets[i].pos()=player+CVector(x<<12,(y+50)<<12,z<<12);targets[i].type()=type;targets[i].flags()=flags[i]=f;*reinterpret_cast<void***>(targets[i].raw)=table;targets[i].next()=i==3||i==6?0:reinterpret_cast<unsigned>(&targets[i+1]);}
events.clear();cursor=seed;Shock source;init(source,player,fire);std::vector<std::string>states{capture(source)};for(int frame=1;frame<=17;frame++){if(!source.dead)move(source);states.push_back(capture(source));}
for(int i=0;i<7;i++)targets[i].flags()=flags[i];events.clear();cursor=seed;unsigned char playerRaw[0xF4]={};*reinterpret_cast<CVector*>(playerRaw+8)=player;*reinterpret_cast<void**>(0x6A9038)=playerRaw;*reinterpret_cast<Target**>(0x56E990)=targets;*reinterpret_cast<Target**>(0x60DAAC)=targets+4;*reinterpret_cast<int*>(0x60D224)=lowMemory;
Shock actual;reinterpret_cast<void(__attribute__((thiscall)) *)(void*,int)>(0x4FAE20)(&actual,fire);assert(capture(actual)==states[0]);for(int frame=1;frame<=17;frame++){if(!actual.dead)reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4FB0E0)(&actual);auto value=capture(actual);if(value!=states[frame]){std::cerr<<"Shock frame "<<frame<<"\nsource="<<states[frame]<<"\nactual="<<value<<'\n';abort();}}
std::cout<<'[';for(unsigned i=0;i<states.size();i++){if(i)std::cout<<',';std::cout<<states[i];}std::cout<<"]\n";}}
'''
    rng=random.Random(0x4FB0E0);cases=[]
    for fire in [0,1,255]:
        for low in [0,1]:
            for seed in [0,1,73,511]:
                row=[12345,-54321,67890,fire,low,seed]
                for x,y,z,kind,flags in [(0,0,0,304,0x116),(140,0,0,324,0x116),(141,299,0,304,0x116),
                    (0,300,0,305,0x116),(0,-300,0,402,0x116),(350,0,0,401,0x116),(2100,0,0,402,0x116)]:row.extend([x,y,z,kind,flags])
                cases.append(row)
    for _ in range(40):
        row=[*[rng.randint(-2000000000,2000000000) for _ in range(3)],rng.choice([0,1,255]),rng.randrange(2),rng.randrange(4096)]
        for _ in range(7):row.extend([rng.randint(-2200,2200),rng.choice([-301,-300,-299,0,299,300,301]),rng.randint(-2200,2200),rng.choice([304,305,324,401,402]),rng.choice([0,2,0x10,0x40,0x116])])
        cases.append(row)
    with tempfile.TemporaryDirectory(prefix='openspidey-dome-shock-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie',
            '-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/web-dome-shocks.json').write_text(json.dumps(
        [dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} CDomeShockWave originalPC ctor/17Move sequences, fullboth-list collision/effect/RNG verified.')


if __name__=='__main__':main()
