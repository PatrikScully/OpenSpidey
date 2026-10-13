"""Verify full CDome ctor/AI/Burst against original PC instructions with observed source model/effect leaves."""
import argparse
import json
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
#include <cstdlib>
struct Body{unsigned vt=0;unsigned short flags=0,inquiry=0;CVector pos;short angles[3]={};unsigned short model=0;unsigned char pad1C[3]={},region=0;unsigned next=0,rgb=0;short scale[3]={4096,4096,4096};unsigned char pad2E[0x18]={};unsigned short bodyFlags=0x16;unsigned char tail[0xAC]={};};
struct Piece:Body{int scratch=0,delay=0,step=4;};
struct Handle{void*pointer=nullptr;unsigned validation=0;};
struct Dome:Body{int scratch=0;Handle player;int mode=0,fire=0;Piece*pieces[5]={};};static_assert(sizeof(Dome)==0x11C);
struct Vertex{short x,y,z,pad;};struct Model{unsigned short flags=8,count=5,normals=0,faces=0;char pad8[20]={};Vertex vertices[5];};
std::vector<void*>allocations;std::vector<int>events;int cursor,expired;unsigned char playerRaw[0xF2C]={};CVector playerPos;unsigned short clearance;int playerFrame;Model model;Vertex*currentVertex;
void emit(std::initializer_list<int>v){events.insert(events.end(),v);}
int Rnd(int bound){int value=(cursor++*7+1)%bound;emit({1,bound,value});return value;}
void*allocate(unsigned bytes){void*p=calloc(1,bytes);allocations.push_back(p);return p;}
unsigned long long makeHandle(void*pointer){emit({2});return static_cast<unsigned long long>(999)<<32|reinterpret_cast<unsigned>(pointer);}
void*recover(Handle*){emit({3,expired?0:1});return expired?nullptr:playerRaw;}
void*__attribute__((thiscall)) baseCtor(void*p){*static_cast<Body*>(p)=Body();return p;}
void __attribute__((thiscall)) initItem(void*,const char*name){emit({4,int(std::strcmp(name,"firedome")==0)});}
void __attribute__((thiscall)) attach(void*,void*){emit({5});}
void printAssert(unsigned char value,const char*){assert(value);}
void pieceMove(Piece&p){if(p.delay)p.delay--;else{int value=p.step+int(p.rgb&255);value=value>32?32:value<0?0:value;p.rgb=value|(value<<8)|(value<<16);}}
Piece*__attribute__((thiscall)) constructPiece(void*raw,CVector*position,int index,int delay,int fire){auto*p=new(raw)Piece;p->pos=*position;p->model=index;p->delay=delay;p->flags=0x400;emit({6,index,delay,fire});return p;}
void __attribute__((thiscall)) burstItem(void*p,int a,int b){emit({7,a,b,static_cast<Body*>(p)->flags});}
void __attribute__((thiscall)) constructShock(void*,int fire){emit({8,fire});}
void __attribute__((thiscall)) constructRing(void*,CVector*p,int fire){emit({9,p->vx,p->vy,p->vz,fire});}
void pulse(CVector p,int scale){emit({10,p.vx,p.vy,p.vz,scale});}
void relocate(const char*,unsigned function,const unsigned*args,unsigned*){assert(function==5);pulse(*reinterpret_cast<CVector*>(args[0]),args[1]);}
void rotMatrix(void*,void*){}void setMatrix(void*){}void setTranslation(void*){}void loadVertex(Vertex*v){currentVertex=v;}
void rotateVertex(){}void storeVertex(CVector*out){*out=(playerPos+CVector(0,int(clearance)<<12,0))>>12;*out+=CVector(currentVertex->x,currentVertex->y,currentVertex->z);}
void init(Dome&s,CVector p,unsigned short gap,int fire){makeHandle(playerRaw);s.player={playerRaw,999};s.pos=p;s.pos.vy+=int(gap)<<12;s.fire=fire;initItem(&s,fire?"firedome":"webdome2");if(fire){s.model=1;s.flags|=0x200;s.scale[1]=0;}s.flags|=1;attach(&s,nullptr);}
void burst(Dome&s){if(!s.fire&&s.pieces[2]){s.pieces[2]->flags&=~0x400;burstItem(s.pieces[2],30,30);}s.mode=3;constructShock(nullptr,s.fire);constructRing(nullptr,&s.pos,s.fire);s.bodyFlags|=0x40;}
void createPiece(Dome&s,int slot,int index,int delay){s.pieces[slot]=constructPiece(allocate(0x100),&s.pos,index,delay,int(s.fire!=0));s.pieces[slot]->bodyFlags|=0x20;}
void move(Dome&s){recover(&s.player);if(expired)burst(s);else{s.pos=playerPos;s.pos.vy+=int(clearance)<<12;}
if(s.fire){if(s.mode==0&&!expired&&playerFrame>=4){createPiece(s,0,0,0);createPiece(s,1,2,2);createPiece(s,2,3,4);s.mode=1;}else if(s.mode==1){s.scale[1]+=600;if(s.scale[1]>4096)s.scale[1]=4096;s.flags&=~1;int index=Rnd(5);currentVertex=&model.vertices[index];CVector p;storeVertex(&p);p<<=12;pulse(p,256);}}
else if(s.mode==0&&!expired&&playerFrame>=4){createPiece(s,0,3,0);createPiece(s,1,0,2);createPiece(s,2,4,4);createPiece(s,3,1,6);createPiece(s,4,2,8);s.mode=1;}
else if(s.mode==1&&s.pieces[3]->rgb==0x202020){for(auto*p:s.pieces)p->step=-10;s.flags&=~1;s.mode=2;}
for(auto*p:s.pieces)if(p)p->pos=s.pos;}
void output(Dome&s){std::cout<<'['<<s.pos.vx<<','<<s.pos.vy<<','<<s.pos.vz<<','<<s.scale[0]<<','<<s.scale[1]<<','<<s.scale[2]<<','<<s.model<<','<<s.fire<<','<<s.mode<<','<<s.flags<<','<<s.bodyFlags<<",[";
for(int i=0;i<5;i++){if(i)std::cout<<',';auto*p=s.pieces[i];if(!p){std::cout<<"null";continue;}std::cout<<'['<<p->model<<','<<p->delay<<','<<p->step<<','<<p->pos.vx<<','<<p->pos.vy<<','<<p->pos.vz<<','<<p->rgb<<','<<p->flags<<','<<p->bodyFlags<<']';}std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}
std::string capture(Dome&s){std::ostringstream t;auto*old=std::cout.rdbuf(t.rdbuf());output(s);std::cout.rdbuf(old);events.clear();return t.str();}
void patch(unsigned a,void*t){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(t)-a-5;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x460080,reinterpret_cast<void*>(&baseCtor));patch(0x458360,reinterpret_cast<void*>(&makeHandle));patch(0x4583F0,reinterpret_cast<void*>(&recover));patch(0x460020,reinterpret_cast<void*>(&initItem));patch(0x460260,reinterpret_cast<void*>(&attach));patch(0x4015B0,reinterpret_cast<void*>(&printAssert));patch(0x455390,reinterpret_cast<void*>(&allocate));patch(0x4088A0,reinterpret_cast<void*>(&allocate));patch(0x4FA480,reinterpret_cast<void*>(&constructPiece));patch(0x45FDC0,reinterpret_cast<void*>(&burstItem));patch(0x4FAE20,reinterpret_cast<void*>(&constructShock));patch(0x4F5510,reinterpret_cast<void*>(&constructRing));patch(0x4E5DA0,reinterpret_cast<void*>(&Rnd));patch(0x47D470,reinterpret_cast<void*>(&relocate));
patch(0x46E730,reinterpret_cast<void*>(&rotMatrix));patch(0x46D7B0,reinterpret_cast<void*>(&setMatrix));patch(0x46E770,reinterpret_cast<void*>(&setTranslation));patch(0x46D8A0,reinterpret_cast<void*>(&loadVertex));patch(0x46DAF0,reinterpret_cast<void*>(&rotateVertex));patch(0x46D790,reinterpret_cast<void*>(&storeVertex));
for(auto range:std::vector<std::pair<unsigned,int>>{{0x4FA647,6},{0x4FA64E,7},{0x4FA731,7},{0x4FA830,6},{0x4FA83E,7},{0x4FAD23,7},{0x4FAD57,6},{0x4FAD5E,7},{0x4FAE12,7}})std::memset(reinterpret_cast<void*>(range.first),0x90,range.second);
for(int i=0;i<5;i++)model.vertices[i]={short(i*23-31),short(i*17-13),short(i*11+7),0};Model*models[4]={&model,&model,&model,&model};*reinterpret_cast<Model***>(0x6B2454)=models;
CVector initial;int fire,startFrame,expireAt,burstAt,seed;while(std::cin>>initial.vx>>initial.vy>>initial.vz>>clearance>>fire>>startFrame>>expireAt>>burstAt>>seed){
events.clear();cursor=seed;expired=0;playerPos=initial;Dome source;init(source,initial,clearance,fire);std::vector<std::string>states{capture(source)};
for(int frame=1;frame<=24;frame++){if(!(source.bodyFlags&0x40)){playerPos=initial+CVector(frame*1234,frame*-567,frame*890);playerFrame=startFrame+frame;expired=frame==expireAt;for(auto*p:source.pieces)if(p)pieceMove(*p);move(source);if(frame==burstAt&&!(source.bodyFlags&0x40))burst(source);}states.push_back(capture(source));}
events.clear();cursor=seed;expired=0;playerPos=initial;*reinterpret_cast<CVector*>(playerRaw+8)=initial;*reinterpret_cast<unsigned short*>(playerRaw+0xEA8)=clearance;*reinterpret_cast<int*>(0x6B55A4)=0;*reinterpret_cast<int*>(0x6B55A8)=0;Dome actual;
reinterpret_cast<void(__attribute__((thiscall)) *)(void*,void*,int)>(0x4FA640)(&actual,playerRaw,fire);assert(*reinterpret_cast<int*>(0x6B55A4)==int(fire!=0)&&*reinterpret_cast<int*>(0x6B55A8)==1);
auto compare=[&](int frame){auto value=capture(actual);if(value!=states[frame]){std::cerr<<"Dome frame "<<frame<<"\nsource="<<states[frame]<<"\nactual="<<value<<'\n';abort();}};compare(0);
for(int frame=1;frame<=24;frame++){if(!(actual.bodyFlags&0x40)){playerPos=initial+CVector(frame*1234,frame*-567,frame*890);playerFrame=startFrame+frame;expired=frame==expireAt;*reinterpret_cast<CVector*>(playerRaw+8)=playerPos;*reinterpret_cast<short*>(playerRaw+0x128)=playerFrame;for(auto*p:actual.pieces)if(p)reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4FA5E0)(p);reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4FA830)(&actual);if(frame==burstAt&&!(actual.bodyFlags&0x40))reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4FAD50)(&actual);}compare(frame);}
std::cout<<'[';for(unsigned i=0;i<states.size();i++){if(i)std::cout<<',';std::cout<<states[i];}std::cout<<"]\n";for(void*p:allocations)free(p);allocations.clear();}}
'''
    cases=[[12345,-54321,67890,clearance,fire,start,expire,burst,seed]
        for clearance in [0,96,65535] for fire in [0,1,255] for start in [0,3]
        for expire,burst in [(-1,0),(7,0),(-1,20)] for seed in [0,73]]
    with tempfile.TemporaryDirectory(prefix='openspidey-dome-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie',
            '-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/web-domes.json').write_text(json.dumps(
        [dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} complete CDome ctor/24AI/Burst originalPC sequences; source model/effect leaves explicitly observed.')


if __name__=='__main__':main()
