"""Capture the original PC CCop constructor, SetCopType, SetParamByIndex, SetAnimMode and multi-frame AI (0x42FA10).

The AI runs the genuine DoAISwitchLogic, DetermineFightState, PlayerIsVisible, sight cone, distance cache,
timers, physics, Neutralize, AI procedures (MoveTo/LookAt) and every ported leaf. World queries, animation,
sound, messages, waypoint traversal, Rnd and the unported leaves are transport boundaries whose calls are
recorded. A frame that reaches an unported leaf or a diagnostic stops the case.
"""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path

HEADER = ['type', 'state', 'substate', 'behavior', 'completion', 'combat', 'bodyFlags', 'alert', 'timer328',
          'closeTicks', 'suppression', 'hitKind', 'poll', 'counter', 'pause', 'pathIndex', 'phase', 'route', 'mode',
          'shadow', 'anim', 'frame', 'finished', 'itemFlags', 'health', 'ownership', 'slotMask', 'copGlobal',
          'copList', 'sharedSlots', 'snapshot', 'x', 'y', 'z', 'vx', 'vy', 'vz', 'yaw', 'seenX', 'seenY', 'seenZ',
          'actionFlags', 'playerX', 'playerY', 'playerZ', 'playerHealth', 'playerFlags', 'unavailable', 'held',
          'playerClearance', 'seed', 'messages', 'far', 'vertical', 'cone', 'near', 'claim', 'distanceClock',
          'distance', 'clearance']
FRAME = ['clock', 'ticks', 'finished', 'los', 'path', 'add', 'next', 'routeAfter', 'ground', 'recover']

CPP = r'''
#include "original_executable.h"
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <vector>
struct Vec{int x,y,z;};
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
template<class T>T&mem(unsigned n){return *reinterpret_cast<T*>(n);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
void stripSeh(unsigned from,unsigned to){for(unsigned char*c=(unsigned char*)from;c<(unsigned char*)to;c++){
 if(c[0]==0x64&&c[1]==0xA1&&!*(unsigned*)(c+2)){c[0]=0x31;c[1]=0xC0;std::memset(c+2,0x90,4);}
 else if(c[0]==0x64&&c[1]==0x89&&(c[2]==0x25||c[2]==0x0D||c[2]==0x15)&&!*(unsigned*)(c+3))std::memset(c,0x90,7);}}
alignas(4)unsigned char actor[0x400],player[0xF20];
std::vector<std::vector<long long>>events;bool stopped;unsigned rngSeed;
int frameLos,framePath,frameAdd,frameNext,frameRoute,frameGround,frameRecover,losCalls,pathCalls,addCalls;
void event(std::initializer_list<long long>v){events.emplace_back(v);}
void*alloc(unsigned n,int,int,int,int){return std::calloc(1,n);}void release(void*p){std::free(p);}
int rnd(int bound){rngSeed=rngSeed*1103515245u+12345u;int r=int((rngSeed>>16)&0x7FFF)%bound;event({8,bound,r});return r;}
void build(void*p){event({1});}
void __attribute__((thiscall)) messages(void*){event({2});}
int ground(Vec*p,int a,int b,int f){event({3,p->x,p->y,p->z,a,b,f});return frameGround;}
void __attribute__((thiscall)) run(void*p,int id,int from,int to){at<unsigned short>(p,0x12A)=id;at<short>(p,0x128)=from;at<unsigned char>(p,0x142)=0;event({9,id,from,to});}
void __attribute__((thiscall)) cycle(void*p,int id,int direction){at<unsigned short>(p,0x12A)=id;at<unsigned char>(p,0x142)=0;event({10,id,(signed char)direction});}
void sound(unsigned id,Vec*p,int pitch){event({11,id&0xFFFF,p->x,p->y,p->z,pitch});}
int __attribute__((stdcall)) pathCheck(Vec*a,Vec*b,void*out,int clearance){int r=(framePath>>pathCalls++)&1;event({12,a->x,a->y,a->z,b->x,b->y,b->z,(long long)(unsigned)out,clearance,mem<int>(0x5FBEE0),r});return r;}
int los(Vec*a,Vec*b,void*out,int flags){int r=(frameLos>>losCalls++)&1;event({13,a->x,a->y,a->z,b->x,b->y,b->z,(long long)(unsigned)out,flags,r});return r;}
int __attribute__((thiscall)) height(void*,int initial,int above,int below){event({14,initial,above,below});return 0;}
int __attribute__((thiscall)) nextWaypoint(void*p){event({15,frameNext});if(frameNext){at<int>(p,0x1F4)++;at<unsigned>(p,0x2F0)=frameRoute;}return frameNext;}
Vec*position(Vec*out,int node){*out={100000+node*40960,-200000,300000-node*20480};event({16,node});return out;}
int __attribute__((thiscall)) addPoint(void*p,Vec*v,int arg){int r=(frameAdd>>addCalls++)&1;event({17,v->x,v->y,v->z,arg,r});
 if(r&&at<int>(p,0x1F0)<5){int n=++at<int>(p,0x1F0);at<Vec>(p,0x1A8+n*12)=*v;}return r;}
void __attribute__((thiscall)) sendSignal(void*){event({18});}
int __attribute__((thiscall)) hit(void*,unsigned char*info){event({19,info[0],*(short*)(info+8),*(int*)(info+0xC),*(int*)(info+0x10),*(int*)(info+0x14)});return 0;}
void __attribute__((thiscall)) warn(void*){event({20});}
int __attribute__((thiscall)) nearTarget(void*,Vec*t,int radius,int phase,Vec*out){*out={t->x+phase*4096,t->y,t->z-phase*8192};event({21,t->x,t->y,t->z,radius,phase});return 1;}
void __attribute__((thiscall)) ring(void*,Vec*o){event({22,o->x,o->y,o->z});}
void*recover(void*handle){if(handle==actor+0x34C){event({23,frameRecover});if(!frameRecover)*(void**)handle=nullptr;return *(void**)handle;}return *(void**)handle;}
unsigned long long makeHandle(void*p){return (0x4321ull<<32)|(unsigned)p;}
std::vector<long long>snap();
void stop(std::vector<long long>v){auto s=snap();v.insert(v.end(),s.begin(),s.end());events.push_back(v);stopped=true;}
void diagnostic(int condition,const char*message){if(condition&0xFF)return;int code=!std::strncmp(message,"Unknown",7)?1:!std::strncmp(message,"Shouldn",7)?2:0;
 if(code&&!stopped)stop({99,code});}
void unported(unsigned address){if(!stopped)stop({98,address});}
#define UNPORTED(name,address) void __attribute__((thiscall)) name(void*){unported(address);}
#define UNPORTED1(name,address) void __attribute__((thiscall)) name(void*,int){unported(address);}
UNPORTED(caution,0x429F00) UNPORTED1(chase,0x42A060) UNPORTED(attack,0x42AEA0) UNPORTED(shoot,0x42BFC0) UNPORTED(yank,0x42D2B0)
UNPORTED(whip,0x42D060) UNPORTED(fly,0x42CA70) UNPORTED(webThrow,0x42C750) UNPORTED(grabbed,0x42E1D0) UNPORTED(fall,0x42DD50) UNPORTED1(die,0x42D9E0)
void __attribute__((thiscall)) baddyCtor(void*p){event({30});}
void __attribute__((thiscall)) shadowOn(void*){event({31});}
void __attribute__((thiscall)) attach(void*,void*list){event({32,(long long)(unsigned)list});}
void __attribute__((thiscall)) parseScript(void*,void*script){event({33,(long long)(unsigned)script});}
void __attribute__((thiscall)) initItem(void*,const char*name){event({34,!std::strcmp(name,"police")?1:!std::strcmp(name,"swat")?2:0});}
void hooks(void*,void*packet){event({35,(long long)(unsigned)packet});}
void numbers(const std::vector<long long>&v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
void eventsOut(){std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';numbers(events[i]);}std::cout<<']';}
void vec(std::vector<long long>&v,unsigned o){for(int i=0;i<3;i++)v.push_back(at<int>(actor,o+i*4));}
void ang(std::vector<long long>&v,unsigned o){for(int i=0;i<3;i++)v.push_back(at<short>(actor,o+i*2));}
std::vector<long long>snap(){std::vector<long long>v;
 for(unsigned o:{0x31Cu,0x320u})v.push_back(at<int>(actor,o));
 for(unsigned o:{0x2A8u,0x288u,0x218u})v.push_back(at<unsigned>(actor,o));v.push_back(at<unsigned short>(actor,0x46));
 for(unsigned o:{0x324u,0x328u,0x32Cu,0x1A4u,0x310u,0x318u,0x35Cu,0x208u,0x204u,0x1F8u,0x230u,0x1F0u})v.push_back(at<int>(actor,o));
 v.push_back(at<unsigned char>(actor,0x21D));for(unsigned o:{0x29Cu,0x2A0u})v.push_back(at<int>(actor,o));
 for(unsigned o:{0x2F0u,0x294u,0x298u})v.push_back(at<unsigned>(actor,o));v.push_back(at<unsigned short>(actor,0xD0));
 for(unsigned o:{0xB8u,8u,0x60u,0x6Cu,0x27Cu})vec(v,o);for(unsigned o:{0x14u,0x88u,0x8Eu})ang(v,o);
 v.push_back(at<unsigned short>(actor,0x12A));v.push_back(at<short>(actor,0x128));v.push_back(at<unsigned char>(actor,0x142));
 v.push_back(at<unsigned>(actor,0x2AC));vec(v,0x1A8);v.push_back(at<unsigned short>(actor,0xDC));v.push_back(at<unsigned>(actor,0x38C));
 v.push_back(at<unsigned char>(actor,0x390));v.push_back(at<unsigned char>(actor,0x391));
 v.push_back(mem<void*>(0x682C3C)==actor?1:mem<void*>(0x682C3C)?2:0);v.push_back(mem<void*>(0x682C40)==actor?1:mem<void*>(0x682C40)?2:0);
 v.push_back(mem<unsigned char>(0x682C18));v.push_back(at<int>(actor,0x1F4));
 std::vector<void*>procs;for(void*p=at<void*>(actor,0x28C);p;p=at<void*>(p,0x1C))procs.push_back(p);v.push_back(procs.size());
 for(void*p:procs){v.push_back(at<int>(p,8));v.push_back(at<int>(p,0x10));v.push_back(at<int>(p,0x14));}
 return v;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 stripSeh(0x401000,0x402000);stripSeh(0x428750,0x42FC00);
 for(int i=0;i<4096;i++){mem<short>(0x610C48+i*4)=short(__builtin_cos(i*0.001536096911877394)*4096.0);mem<short>(0x610C4A+i*4)=short(__builtin_sin(i*0.001536096911877394)*4096.0);}
 patch(0x4581A0,(void*)&alloc);patch(0x458210,(void*)&release);patch(0x4E5DA0,(void*)&rnd);patch(0x475F50,(void*)&build);patch(0x42F470,(void*)&messages);
 patch(0x4E6840,(void*)&ground);patch(0x460C10,(void*)&run);patch(0x460D00,(void*)&cycle);patch(0x471EA0,(void*)&sound);patch(0x403350,(void*)&pathCheck);
 patch(0x4E67A0,(void*)&los);patch(0x4041C0,(void*)&height);patch(0x402F60,(void*)&nextWaypoint);patch(0x4E3940,(void*)&position);patch(0x4048F0,(void*)&addPoint);
 patch(0x403230,(void*)&sendSignal);patch(0x429C60,(void*)&hit);patch(0x42B270,(void*)&warn);patch(0x403B60,(void*)&nearTarget);patch(0x403FC0,(void*)&ring);
 patch(0x4583F0,(void*)&recover);patch(0x458360,(void*)&makeHandle);patch(0x4015B0,(void*)&diagnostic);
 patch(0x429F00,(void*)&caution);patch(0x42A060,(void*)&chase);patch(0x42AEA0,(void*)&attack);patch(0x42BFC0,(void*)&shoot);patch(0x42D2B0,(void*)&yank);
 patch(0x42D060,(void*)&whip);patch(0x42CA70,(void*)&fly);patch(0x42C750,(void*)&webThrow);patch(0x42E1D0,(void*)&grabbed);patch(0x42DD50,(void*)&fall);patch(0x42D9E0,(void*)&die);
 patch(0x402C00,(void*)&baddyCtor);patch(0x460560,(void*)&shadowOn);patch(0x460260,(void*)&attach);patch(0x404FD0,(void*)&parseScript);patch(0x460020,(void*)&initItem);patch(0x453C30,(void*)&hooks);
 mem<void*>(0x6A9038)=player;std::cout<<std::unitbuf;
 char mode;while(std::cin>>mode){std::memset(actor,0,sizeof actor);std::memset(player,0,sizeof player);events.clear();stopped=false;
  mem<void*>(0x682C3C)=nullptr;mem<void*>(0x682C40)=nullptr;mem<unsigned char>(0x682C18)=0;
  if(mode=='C'){int type,node,clock;std::cin>>type>>node>>clock;mem<int>(0x5FCCF4)=clock;alignas(4)unsigned char data[20]={};
   for(int i=0;i<3;i++)at<int>(data,i*4)=1000*(i+1);for(int i=0;i<3;i++)at<short>(data,12+i*2)=100*(i+1);
   reinterpret_cast<void*(__attribute__((thiscall))*)(void*,void*,int)>(0x428750)(actor,data,node);
   std::vector<long long>v={at<short>(actor,0x38),at<unsigned short>(actor,0xD0),at<int>(actor,0x230),at<int>(actor,0x1F4),at<unsigned short>(actor,0xDE),at<short>(actor,0x216),
    at<int>(actor,0xD8),at<int>(actor,0x31C),at<unsigned short>(actor,0xDC),at<unsigned>(actor,0x2A8),at<int>(actor,0x370),at<int>(actor,0x340),at<int>(actor,0x360),at<int>(actor,0x364),
    at<int>(actor,0x368),at<int>(actor,0x36C),at<int>(actor,0x374),at<short>(actor,0x1FC),at<int>(actor,0x37C),at<unsigned char>(actor,0x212),(long long)(at<unsigned char*>(actor,0x34C)?1:0)};
   events.push_back(v);
   reinterpret_cast<void(__attribute__((thiscall))*)(void*,int)>(0x4288D0)(actor,type);
   events.push_back({at<short>(actor,0x38),at<short>(actor,0x21E),at<unsigned>(actor,0x294),at<unsigned>(actor,0x298),at<unsigned short>(actor,0xDC),at<int>(actor,0x370)});
   eventsOut();std::cout<<'\n';continue;}
  if(mode=='S'){int index,value,combat;std::cin>>index>>value>>combat;at<unsigned>(actor,0x218)=combat;
   for(unsigned o=0x360;o<0x380;o+=4)at<int>(actor,o)=o;at<short>(actor,0x1FC)=7;
   reinterpret_cast<void(__attribute__((thiscall))*)(void*,int,int)>(0x4294E0)(actor,index,value);std::vector<long long>v;
   for(unsigned o=0x360;o<0x380;o+=4)v.push_back(at<int>(actor,o));v.push_back(at<short>(actor,0x1FC));v.push_back(at<unsigned>(actor,0x218));events.push_back(v);
   eventsOut();std::cout<<'\n';continue;}
  if(mode=='M'){int type,old294,old298,newMode,play;std::cin>>type>>old294>>old298>>newMode>>play;at<short>(actor,0x38)=type;at<unsigned>(actor,0x294)=old294;at<unsigned>(actor,0x298)=old298;
   int result=reinterpret_cast<int(__attribute__((thiscall))*)(void*,int,int)>(0x428EE0)(actor,newMode,play);
   events.push_back({result,at<unsigned>(actor,0x294),at<unsigned>(actor,0x298)});eventsOut();std::cout<<'\n';continue;}
  long long h[%HEADER%];for(auto&x:h)std::cin>>x;
  *(void**)actor=(void*)0x53B670;
  enum{%ENUM%};
  at<short>(actor,0x38)=h[type];at<int>(actor,0x31C)=h[state];at<int>(actor,0x320)=h[substate];at<unsigned>(actor,0x2A8)=h[behavior];at<unsigned>(actor,0x288)=h[completion];
  at<unsigned>(actor,0x218)=h[combat];at<unsigned short>(actor,0x46)=h[bodyFlags];at<int>(actor,0x324)=h[alert];at<int>(actor,0x328)=h[timer328];at<int>(actor,0x32C)=h[closeTicks];
  at<int>(actor,0x1A4)=h[suppression];at<int>(actor,0x318)=h[hitKind];at<int>(actor,0x35C)=h[poll];at<int>(actor,0x1F8)=h[counter];at<int>(actor,0x230)=h[pause];at<int>(actor,0x1F0)=h[pathIndex];
  at<unsigned char>(actor,0x21D)=h[phase];at<unsigned>(actor,0x2F0)=h[route];at<unsigned short>(actor,0xD0)=h[shadow];at<unsigned short>(actor,0x12A)=h[anim];at<short>(actor,0x128)=h[frame];
  at<unsigned char>(actor,0x142)=h[finished];at<unsigned short>(actor,4)=h[itemFlags];at<short>(actor,0xE2)=h[health];at<unsigned char>(actor,0x390)=h[ownership];at<unsigned char>(actor,0x391)=h[slotMask];
  static unsigned char other[0x400];mem<void*>(0x682C3C)=h[copGlobal]==1?(void*)actor:h[copGlobal]==2?(void*)other:nullptr;mem<void*>(0x682C40)=h[copList]==1?(void*)actor:h[copList]==2?(void*)other:nullptr;
  mem<unsigned char>(0x682C18)=h[sharedSlots];at<int>(actor,0x340)=h[snapshot];at<Vec>(actor,8)={int(h[x]),int(h[y]),int(h[z])};at<Vec>(actor,0x60)={int(h[vx]),int(h[vy]),int(h[vz])};
  at<Vec>(actor,0x6C)={301,-503,701};at<short>(actor,0x16)=h[yaw];at<short>(actor,0x8A)=-5;at<short>(actor,0x90)=2;at<Vec>(actor,0x1A8)={int(h[seenX]),int(h[seenY]),int(h[seenZ])};
  for(int i=1;i<6;i++)at<Vec>(actor,0x1A8+i*12)={100000+i*12288,-200000,300000+i*8192};
  at<unsigned>(actor,0x38C)=h[actionFlags];at<int>(actor,0x360)=h[far];at<int>(actor,0x364)=h[vertical];at<int>(actor,0x368)=h[cone];at<int>(actor,0x36C)=h[near];at<int>(actor,0x37C)=h[claim];
  at<int>(actor,0x208)=h[distanceClock];at<int>(actor,0x204)=h[distance];at<short>(actor,0x21E)=h[clearance];at<int>(actor,0x1F4)=7;at<void*>(actor,0x34C)=player;
  for(unsigned o:{0x78u,0x94u})for(int i=0;i<3;i++)at<unsigned char>(actor,o+i)=4;
  static const unsigned modes[2][4][2]={{{0x02010E0D,3},{0x1F1F0807,0x11F},{0x08080806,0x205},{0x05050807,0x305}},{{0x02010E0D,3},{0x1F1F0807,0x105},{0x1F1F0807,0x205},{0x05050807,0x305}}};
  at<unsigned>(actor,0x294)=modes[h[type]!=306][h[mode]][0];at<unsigned>(actor,0x298)=modes[h[type]!=306][h[mode]][1];
  at<void*>(actor,0x290)=h[messages]?(void*)actor:nullptr;
  at<Vec>(player,8)={int(h[playerX]),int(h[playerY]),int(h[playerZ])};at<short>(player,0xE2)=h[playerHealth];at<unsigned short>(player,0x46)=h[playerFlags];
  at<unsigned char>(player,0x57C)=h[unavailable];at<void*>(player,0xE48)=h[held]?(void*)other:nullptr;at<unsigned short>(player,0xEA8)=h[playerClearance];rngSeed=h[seed];
  int frames;std::cin>>frames;std::cout<<'[';
  for(int f=0;f<frames;f++){long long fr[%FRAME%];for(auto&x:fr)std::cin>>x;if(stopped)continue;events.clear();
   mem<int>(0x5FCCF4)=fr[0];at<int>(actor,0x80)=fr[1];if(fr[2]>=0)at<unsigned char>(actor,0x142)=fr[2];frameLos=fr[3];framePath=fr[4];frameAdd=fr[5];frameNext=fr[6];frameRoute=fr[7];frameGround=fr[8];frameRecover=fr[9];
   losCalls=pathCalls=addCalls=0;
   reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x42FA10)(actor);
   if(f)std::cout<<',';std::cout<<"{\"s\":";numbers(snap());std::cout<<",\"e\":";eventsOut();std::cout<<'}';}
  std::cout<<"]\n";}
}
'''


def header(**changes):
    h = dict.fromkeys(HEADER, 0)
    h.update(type=306, shadow=50, bodyFlags=0x216, poll=15, health=50, x=100000, y=-200000, z=300000, seenX=500000,
             seenY=-200000, seenZ=700000, playerX=100000 + 900 * 4096, playerY=-200000, playerZ=300000 + 400 * 4096,
             playerHealth=100, playerFlags=0x206, seed=0x1234, far=3500, vertical=400, cone=2047, near=100, claim=2000,
             clearance=100, playerClearance=50, behavior=1, snapshot=1000)
    h.update(changes)
    return h


def frame(**changes):
    f = dict(clock=1001, ticks=2, finished=-1, los=0, path=0, add=0, next=0, routeAfter=0, ground=-1, recover=0)
    f.update(changes)
    return f


def frames(rng, count, base_clock=1001, **fixed):
    out = []
    for i in range(count):
        f = frame(clock=base_clock + i * 2, ticks=rng.choice([1, 2, 3]), finished=rng.choice([-1, -1, 0, 1]),
                  los=rng.randrange(8), path=rng.randrange(8), add=rng.randrange(8), next=rng.randrange(2),
                  routeAfter=rng.choice([0, 1, 2, 3]), ground=rng.choice([-1, -150000, 0]), recover=rng.randrange(2))
        f.update(fixed)
        out.append(f)
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x42FA10)
    cases = []
    for t in [306, 320, 999]:
        for node in [4, 37, 0xFFFF]:
            cases.append(dict(mode='C', input=[t, node, 777]))
    for index in range(13):
        for value in [0, 1, -5, 70000]:
            cases.append(dict(mode='S', input=[index, value, rng.choice([0, 0x800, 0x4801])]))
    tables = {306: [(0x02010E0D, 3), (0x1F1F0807, 0x11F), (0x08080806, 0x205), (0x05050807, 0x305)],
              320: [(0x02010E0D, 3), (0x1F1F0807, 0x105), (0x1F1F0807, 0x205), (0x05050807, 0x305)]}
    for t in [306, 320, 0]:
        for old in range(5):
            for new in range(5):
                for play in [0, 1]:
                    w294, w298 = tables[306 if t == 306 else 320][old] if old < 4 else (0x11223344, 0x0477)
                    cases.append(dict(mode='M', input=[t, w294, w298, new, play]))
    # Initialization, guard and patrol from load.
    for t in [306, 320]:
        for start in range(8):
            fs = [frame(clock=c, ticks=2, ground=g, next=n, routeAfter=r) for c, g, n, r in
                  [(1001 + start, -1, 0, 0), (1016 + start * 16, -150000 + start, start & 1, start & 3),
                   (1018, 0, start & 1, 2), (1020, 0, 1, 0), (1022, 0, 0, 0), (1024, 0, 1, 1)]]
            cases.append(dict(mode='A', header=header(type=t, snapshot=1000 - start * 3, pathIndex=start // 4), frames=fs))
    for t in [306, 320]:
        for sub in range(5):
            for anim in [31, 12]:
                for pause in [0, 3]:
                    cases.append(dict(mode='A', header=header(type=t, state=1, substate=sub, anim=anim, pause=pause,
                                                              mode=rng.randrange(4)),
                                      frames=frames(rng, 8, los=0, path=1)))
    for t in [306, 320]:
        for state in [2, 24]:
            for path_index in [0, 1, 2]:
                for route in [0, 1, 2, 3]:
                    for alert in [0, 50]:
                        cases.append(dict(mode='A', header=header(type=t, state=state, pathIndex=path_index, route=route,
                                                                  alert=alert, mode=rng.randrange(4), phase=rng.randrange(256),
                                                                  copGlobal=rng.randrange(3), ownership=rng.choice([0, 2]),
                                                                  slotMask=rng.choice([0, 0x24]), sharedSlots=0xFF),
                                          frames=frames(rng, 8, los=0)))
    # Fight-state determination and switch logic.
    for i in range(400):
        state = rng.choice([1, 2, 23, 24, 25, 28, 10, 19, 21, 12, 13])
        h = header(type=rng.choice([306, 320]), state=state, substate=rng.choice([0, 0, 1]), alert=rng.choice([0, 0, 1, 40, 300]),
                   closeTicks=rng.choice([0, 0, 1, 2, 30]), suppression=rng.choice([0, 0, 0, 1, 5]),
                   poll=rng.choice([0, 3, 15, 31]), phase=rng.randrange(256),
                   behavior=rng.choice([1, 0x801, 0x2001, 0x41, 0x9, 0x2849, 0x10000001]),
                   combat=rng.choice([0, 0, 0x800, 0x4000, 0x4800]), health=rng.choice([50, 0, -3]),
                   playerX=100000 + rng.choice([20, 90, 150, 199, 500, 1200, 1800, 2500, 4000]) * 4096 * rng.choice([1, -1]),
                   playerZ=300000 + rng.choice([0, 40, 300, 1500]) * 4096,
                   playerY=-200000 + rng.choice([0, 100 * 4096, 300 * 4096, 500 * 4096]) * rng.choice([1, -1]),
                   playerHealth=rng.choice([100, 0]), playerFlags=rng.choice([0x206, 0x246]), unavailable=rng.choice([0, 0, 1]),
                   held=rng.choice([0, 0, 1]), copList=rng.choice([0, 0, 2]), yaw=rng.randrange(4096),
                   actionFlags=rng.choice([0, 4]), ownership=rng.choice([0, 1, 2]), far=rng.choice([3500, 1000]),
                   vertical=rng.choice([400, 0, 50]), cone=rng.choice([2047, 4096, 300]), near=rng.choice([100, 600]),
                   claim=rng.choice([2000, 200]), anim=rng.choice([0, 31, 14, 8, 10, 11, 22, 24]), frame=rng.randrange(30),
                   counter=rng.choice([0, 1, 3, 70, 130]), hitKind=rng.choice([0, 1]), messages=rng.choice([0, 0, 1]),
                   distanceClock=rng.choice([0, 1000, 999]), distance=rng.choice([0, 150, 5000]), playerClearance=rng.choice([0, 50, 400]),
                   timer328=rng.choice([0, 1, 9]), itemFlags=rng.choice([0, 4]), mode=rng.randrange(4), seed=rng.randrange(1 << 31),
                   shadow=rng.choice([0, 3, 45, 50]), route=rng.randrange(4))
        cases.append(dict(mode='A', header=h, frames=frames(rng, rng.choice([1, 4, 8]), base_clock=rng.choice([1000, 1001, 4096, 33]))))
    # Unported leaves stop at their call with the source pre-dispatch state.
    for state in [3, 4, 5, 9, 11, 14, 15, 16, 17, 18, 20, 22, 26, 27, 30]:
        for behavior in [1, 0x41]:
            cases.append(dict(mode='A', header=header(state=state, behavior=behavior), frames=[frame()]))
    rows = []
    for case in cases:
        if case['mode'] != 'A':
            rows.append(case['mode'] + ' ' + ' '.join(map(str, case['input'])))
        else:
            values = [case['header'][k] for k in HEADER] + [len(case['frames'])] + [f[k] for f in case['frames'] for k in FRAME]
            rows.append('A ' + ' '.join(map(str, values)))
    code = CPP.replace('%HEADER%', str(len(HEADER))).replace('%FRAME%', str(len(FRAME))).replace('%ENUM%', ','.join(HEADER))
    with tempfile.TemporaryDirectory(prefix='openspidey-cop-') as folder:
        cpp, binary = Path(folder)/'reference.cpp', Path(folder)/'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root/'src'),
                        '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(rows)+'\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/cop.json').write_text(json.dumps(fixtures, separators=(',', ':'))+'\n')
    print(f'Captured {len(cases)} actual PC CCop constructor, parameter, animation-mode and AI cases.')


if __name__ == '__main__':
    main()
