"""Capture the CRhino::AI (0x4819C0) state leaves, Hit and DetermineFightState from SpideyPC.exe with patched callees."""
import argparse
import json
import subprocess
import tempfile
from pathlib import Path

# (name, offset or absolute address, size, signed). Shared with RhinoAiTests.cs by index.
FIELDS = [
    ('State', 0x31C, 4, 1), ('Substate', 0x320, 4, 1), ('Field310', 0x310, 4, 1), ('Field324', 0x324, 4, 1),
    ('Field328', 0x328, 4, 1), ('Field330', 0x330, 4, 1), ('Field334', 0x334, 4, 1), ('Field338', 0x338, 4, 1),
    ('Field344', 0x344, 4, 1), ('Field348', 0x348, 4, 1), ('Field34C', 0x34C, 4, 1), ('Field350', 0x350, 4, 1),
    ('Field354', 0x354, 4, 1), ('Field358', 0x358, 4, 1), ('Field35C', 0x35C, 4, 1), ('Field360', 0x360, 4, 1),
    ('Field388', 0x388, 4, 1), ('Field3D0', 0x3D0, 4, 1), ('Field420', 0x420, 4, 1), ('Field1F0', 0x1F0, 4, 1),
    ('Field1F8', 0x1F8, 4, 1), ('Field230', 0x230, 4, 1), ('CombatFlags', 0x218, 4, 0), ('Completion', 0x288, 4, 0),
    ('Behavior', 0x2A8, 4, 0), ('BodyFlags', 0x46, 2, 0), ('Health', 0xE2, 2, 1), ('LastHitKind', 0x318, 4, 1),
    ('VelX', 0x60, 4, 1), ('VelY', 0x64, 4, 1), ('VelZ', 0x68, 4, 1), ('AngleY', 0x16, 2, 1),
    ('PosX', 0x08, 4, 1), ('PosY', 0x0C, 4, 1), ('PosZ', 0x10, 4, 1), ('Path1X', 0x1B4, 4, 1),
    ('Path1Y', 0x1B8, 4, 1), ('Path1Z', 0x1BC, 4, 1), ('Field21D', 0x21D, 1, 0), ('CachedDistance', 0x204, 4, 1),
    ('CachedClock', 0x208, 4, 1), ('MovementFlags', 0x2AC, 4, 0), ('MovementAnims', 0x294, 4, 0),
    ('IdleAnims', 0x298, 4, 0), ('RouteFlags', 0x2F0, 4, 0), ('PlayerDistance', 0xE4, 4, 0),
    ('Animation', 0x12A, 2, 0), ('Frame', 0x128, 2, 1), ('Finished', 0x142, 1, 0), ('Elapsed', 0x80, 4, 1),
    ('LastFootstep', 0x682C4C, 4, 1), ('TrapWeb', 0x104, 4, 0),
]
INDEX = {name: index for index, (name, *_rest) in enumerate(FIELDS)}
# (address, has argument)
FUNCTIONS = [
    (0x47EE70, 'hit'), (0x481300, 1), (0x4823C0, 'los'), (0x480820, 0), (0x47ECA0, 0), (0x480090, 0),
    (0x480F50, 0), (0x47E680, 0), (0x47E420, 0), (0x4810C0, 0), (0x480A40, 0), (0x480170, 0),
    (0x47F490, 1), (0x47F320, 0), (0x480DA0, 0), (0x481550, 0),
]

CPP = r'''
#include "original_executable.h"
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <deque>
#include <iostream>
#include <map>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
std::map<unsigned,std::vector<unsigned char>>saved;
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;if(!saved.count(a))saved[a]=std::vector<unsigned char>(p,p+5);
 p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
// Linux i386 has no Windows TIB at fs:0; neutralize the SEH frame links of a function body.
void omitUnwind(unsigned start,unsigned end){
 for(unsigned a=start;a+7<=end;a++){auto*p=(unsigned char*)a;
  if(p[0]!=0x64)continue;
  if(p[1]==0xA1&&!*reinterpret_cast<int*>(p+2)){p[0]=0x31;p[1]=0xC0;std::memset(p+2,0x90,4);a+=5;continue;}
  if((p[1]==0x89||p[1]==0x8B)&&(p[2]&0xC7)==0x05&&!*reinterpret_cast<int*>(p+3)){
   int reg=(p[2]>>3)&7;
   if(p[1]==0x8B){p[0]=0x31;p[1]=(unsigned char)(0xC0|reg<<3|reg);std::memset(p+2,0x90,5);}
   else std::memset(p,0x90,7);
   a+=6;}}}
alignas(4) unsigned char actor[0x500],player[0xF20],proc[0x100],item[0x100];
void*playerVtable[8];
std::vector<std::vector<int>>events;
std::deque<int>script;
int seed,caseIndex,underflow;
int pop(){if(script.empty()){underflow++;return 0;}int v=script.front();script.pop_front();return v;}
void anim(int clip,int frame){at<unsigned short>(actor,0x12A)=(unsigned short)clip;at<short>(actor,0x128)=(short)frame;actor[0x142]=0;}
int rnd(int bound){events.push_back({1,bound,seed});int v=bound<=0?0:seed%bound;seed++;return v;}
void*alloc(unsigned){std::memset(proc,0,sizeof proc);return proc;}
void __attribute__((thiscall)) neutralize(void*){events.push_back({3});}
void __attribute__((thiscall)) lookAt(void*,void*,void*body,void*,int mask,int rate,int tol){events.push_back({4,mask,rate,tol,body==player?1:0});}
void __attribute__((thiscall)) lookYaw(void*,void*,int yaw,int mask,int rate,int tol){events.push_back({24,(short)yaw,mask,rate,tol});}
void __attribute__((thiscall)) moveTo(void*,void*,int*info,int mask){events.push_back({23,info[0],info[1],info[2],info[3],info[4],info[5],mask});}
void __attribute__((thiscall)) accZ(void*,void*,int acc,int target,int mask){events.push_back({12,acc,target,mask});}
void __attribute__((thiscall)) monitor(void*,void*,int start,int mask,int frames,int completion){events.push_back({11,start,mask,frames,completion});}
void __attribute__((thiscall)) stateSwitch(void*,void*,int code){events.push_back({13,code});}
void __attribute__((thiscall)) runAnim(void*,int clip,int frame,int){events.push_back({5,clip,frame});anim(clip,frame);}
void __attribute__((thiscall)) cycleAnim(void*,int clip,int){events.push_back({9,clip});at<unsigned short>(actor,0x12A)=(unsigned short)clip;actor[0x142]=0;}
int sfx(unsigned id,void*,int){events.push_back({6,(int)id});return 5;}
void sfxStop(int handle){events.push_back({22,handle});}
void __attribute__((thiscall)) physics(void*,int mode){events.push_back({7,mode});}
void __attribute__((thiscall)) xa(void*,int a,int b,int c){events.push_back({10,a,b,c});}
int __attribute__((thiscall)) pathCheck(void*,int*,int*to,int*out,int clearance){
 events.push_back({16,to[0],to[1],to[2],clearance,out?1:0});int code=pop();if(out){out[0]=pop();out[1]=pop();out[2]=pop();}return code;}
void lineToItem(unsigned char*info,int mode){
 events.push_back({17,at<int>(info,0),at<int>(info,4),at<int>(info,8),at<int>(info,12),at<int>(info,16),at<int>(info,20),mode});
 if(pop()){at<void*>(info,0x68)=item;at<int>(info,0x6C)=pop();at<int>(info,0x70)=pop();at<int>(info,0x74)=pop();}else at<void*>(info,0x68)=nullptr;}
unsigned checksum(void*){return (unsigned)pop();}
void __attribute__((thiscall)) sendSignal(void*){events.push_back({15});}
int __attribute__((thiscall)) playerHit(void*,unsigned char*h){
 events.push_back({14,h[0],at<int>(h,4),at<unsigned short>(h,8),at<int>(h,12),at<int>(h,16),at<int>(h,20)});return pop();}
void __attribute__((thiscall)) shake(void*,void*,int){events.push_back({18});}
void stomp(void*){events.push_back({19});}
void electrify(void*){events.push_back({20});}
void __attribute__((thiscall)) knock(void*){events.push_back({28});}
void __attribute__((thiscall)) ignoreInput(void*,int ticks){events.push_back({29,ticks});}
void __attribute__((thiscall)) stopXa(void*){events.push_back({30});}
void __attribute__((thiscall)) die(void*,int){events.push_back({31});}
int __attribute__((thiscall)) nextWaypoint(void*){events.push_back({25});return pop();}
void*recover(void*){events.push_back({32});return nullptr;}
unsigned short actuatorTime(int,int){events.push_back({33});return 0;}
void actuatorOn(int,int,int,int){events.push_back({34});}
void diagnostic(int,const char*){}
void numbers(const std::vector<int>&v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
struct Field{unsigned offset,size,sign;};
unsigned char*address(const Field&f){return f.offset>=0x400000?(unsigned char*)f.offset:actor+f.offset;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 for(int i=0;i<4096;i++){double a=i*0.001536096911877394;
  reinterpret_cast<short*>(0x610C48)[2*i]=(short)(std::sin(a)*4096);reinterpret_cast<short*>(0x610C48)[2*i+1]=(short)(std::cos(a)*4096);}
@UNWIND@
 patch(0x4E5DA0,(void*)&rnd);patch(0x455390,(void*)&alloc);patch(0x4040E0,(void*)&neutralize);
 patch(0x401180,(void*)&lookAt);patch(0x4012F0,(void*)&lookYaw);patch(0x4018D0,(void*)&moveTo);patch(0x4016B0,(void*)&accZ);
 patch(0x401C40,(void*)&monitor);patch(0x401E60,(void*)&stateSwitch);patch(0x460C10,(void*)&runAnim);patch(0x460D00,(void*)&cycleAnim);
 patch(0x471EA0,(void*)&sfx);patch(0x4723D0,(void*)&sfxStop);patch(0x404C50,(void*)&physics);patch(0x47E0A0,(void*)&xa);
 patch(0x403310,(void*)&pathCheck);patch(0x4549A0,(void*)&lineToItem);patch(0x4C9430,(void*)&checksum);patch(0x403230,(void*)&sendSignal);
 patch(0x416880,(void*)&shake);patch(0x43A400,(void*)&stomp);patch(0x438E20,(void*)&electrify);patch(0x4BB710,(void*)&knock);
 patch(0x4C30A0,(void*)&ignoreInput);patch(0x460400,(void*)&stopXa);patch(0x404320,(void*)&die);patch(0x402F60,(void*)&nextWaypoint);
 patch(0x4583F0,(void*)&recover);patch(0x4794A0,(void*)&actuatorTime);patch(0x4794D0,(void*)&actuatorOn);patch(0x4015B0,(void*)&diagnostic);
 playerVtable[3]=(void*)&playerHit;
 const Field fields[]={@FIELDS@};const unsigned count=sizeof fields/sizeof fields[0];
 const unsigned functions[]={@FUNCTIONS@};
 int fn;
 for(caseIndex=0;std::cin>>fn;caseIndex++){
  int arg,hit[8],clock,difficulty,px,py,pz,crawl,wall,ceiling,action,suppress,n;
  std::cin>>arg;for(int&h:hit)std::cin>>h;
  std::cin>>clock>>difficulty>>px>>py>>pz>>crawl>>wall>>ceiling>>action>>suppress;
  std::memset(actor,0,sizeof actor);std::memset(player,0,sizeof player);events.clear();script.clear();underflow=0;
  for(unsigned i=0;i<count;i++){int v;std::cin>>v;unsigned char*p=address(fields[i]);
   if(fields[i].size==1)*p=(unsigned char)v;else if(fields[i].size==2)*reinterpret_cast<short*>(p)=(short)v;else *reinterpret_cast<int*>(p)=v;}
  std::cin>>n;for(int i=0;i<n;i++){int v;std::cin>>v;script.push_back(v);}
  std::cin>>seed;
  *reinterpret_cast<void**>(0x6A9038)=player;at<void**>(player,0)=playerVtable;
  *reinterpret_cast<void**>(0x56F3B8)=nullptr;*reinterpret_cast<void**>(0x60DAAC)=nullptr;
  *reinterpret_cast<int*>(0x5FCCF4)=clock;*reinterpret_cast<int*>(0x54D474)=difficulty;*reinterpret_cast<unsigned char*>(0x6828D3)=0;
  at<int>(player,8)=px;at<int>(player,12)=py;at<int>(player,16)=pz;player[0xAD4]=(unsigned char)crawl;
  player[0x8E8]=(unsigned char)wall;player[0x8E9]=(unsigned char)ceiling;at<int>(player,0xE1C)=action;at<int>(player,0xE18)=suppress;
  unsigned target=functions[fn];int result=0;
  if(fn==0){alignas(4) unsigned char info[0x20]={};info[0]=(unsigned char)hit[0];at<int>(info,4)=hit[1];at<unsigned short>(info,8)=(unsigned short)hit[2];
   at<int>(info,12)=hit[3];at<int>(info,16)=hit[4];at<int>(info,20)=hit[5];at<short>(info,24)=(short)hit[6];at<unsigned short>(info,26)=(unsigned short)hit[7];
   result=reinterpret_cast<int(__attribute__((thiscall))*)(void*,void*)>(target)(actor,info);}
  else if(fn==2)result=reinterpret_cast<unsigned char(__attribute__((thiscall))*)(void*,void*,int)>(target)(actor,player+8,arg);
  else if(fn==1)result=reinterpret_cast<int(__attribute__((thiscall))*)(void*,int)>(target)(actor,arg);
  else if(fn==12)reinterpret_cast<void(__attribute__((thiscall))*)(void*,int)>(target)(actor,arg);
  else reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(target)(actor);
  std::cout<<"{\"fields\":[";
  for(unsigned i=0;i<count;i++){if(i)std::cout<<',';unsigned char*p=address(fields[i]);
   long long v=fields[i].size==1?(long long)*p:fields[i].size==2?(fields[i].sign?(long long)*reinterpret_cast<short*>(p):(long long)*reinterpret_cast<unsigned short*>(p))
    :(fields[i].sign?(long long)*reinterpret_cast<int*>(p):(long long)*reinterpret_cast<unsigned*>(p));std::cout<<v;}
  std::cout<<"],\"ret\":"<<result<<",\"unused\":"<<script.size()<<",\"underflow\":"<<underflow<<",\"events\":[";
  for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';numbers(events[i]);}
  std::cout<<"]}\n";
 }
}
'''

LEAVES = [0x47EE70, 0x481300, 0x4823C0, 0x480820, 0x47ECA0, 0x480090, 0x480F50, 0x47E680, 0x47E420, 0x4810C0,
          0x480A40, 0x480170, 0x47F490, 0x47F320, 0x480DA0, 0x481550, 0x47F190, 0x47E8A0, 0x403B60, 0x404170, 0x404B60]


def case(fn, arg=0, hit=(0,) * 8, clock=0x100, difficulty=1, player=(0, 0, 0), crawl=0, wall=0, ceiling=0,
         action=0, suppress=0, script=(), seed=0, **fields):
    values = [0] * len(FIELDS)
    values[INDEX['BodyFlags']] = 0x216
    values[INDEX['Health']] = 1400
    values[INDEX['MovementAnims']] = 0x201
    values[INDEX['Elapsed']] = 2
    for name, value in fields.items():
        values[INDEX[name]] = value
    return dict(fn=fn, arg=arg, hit=list(hit), globals=[clock, difficulty, *player, crawl, wall, ceiling, action, suppress],
                fields=values, script=list(script), seed=seed)


def cases():
    far = (3000 << 12, 0, 0)
    near = (150 << 12, 0, 0)
    mid = (400 << 12, 0, 0)
    result = []
    add = result.append
    # Hit (0x47EE70)
    for flags, kind, damage, state, sub, f230, slide, script in [
        (6, 1, 50, 2, 0, 0, (0, 0), ()), (6, 2, 50, 2, 0, 0, (0, 0), (0, 0, 0, 0)), (6, 23, 80, 8, 3, 0, (0, 0), (2, 2048, 0, 0)),
        (6, 6, 80, 6, 1, 0, (0, 0), (1, 0, 0, 0)), (4, 0, 1500, 2, 0, 0, (0, 0), ()), (0x1E, 3, 40, 2, 0, 0, (60, 9), (0, 0, 0, 0)),
        (0x1E, 3, 40, 2, 0, 0, (300, 20), (2, 409600, 0, 102400)), (6, 3, 40, 10, 2, 30, (0, 0), ()),
        (6, 3, 40, 10, 2, 10, (0, 0), ()), (6, 3, 40, 12, 1, 0, (0, 0), (1,)), (6, 3, 40, 14, 0, 0, (0, 0), ()),
        (2, 3, 40, 2, 0, 0, (0, 0), ()), (6, 2, 20, 1, 0, 0, (0, 0), (2, 81920, 0, 40960)),
    ]:
        add(case(0, hit=(flags, kind, damage, 4096, 0, 0, *slide), State=state, Substate=sub, Field230=f230,
                 player=mid, PosX=20 << 12, PosZ=30 << 12, script=script))
    add(case(0, hit=(6, 1, 10, 0, 0, 0, 0, 0), Health=0))
    # DetermineFightState (0x481300)
    for arg, state, player, crawl, action, distance, clock, f358, combat, script in [
        (0, 2, mid, 0, 0, 100, 0x100, 0, 0, (0,)), (0, 2, mid, 0, 0, 300, 0x100, 0, 0x10, (0, 0)),
        (0, 1, mid, 0, 0, 600, 0x100, 0, 0, (0, 0)), (1, 2, mid, 0, 0, 600, 0x100, 0, 0, (0, 0)),
        (0, 2, mid, 0, 0x80, 300, 0x100, 0, 0, (0, 0)), (0, 2, mid, 0, 0x800000, 100, 0x100, 0, 0, (0, 0)),
        (0, 2, mid, 0, 0x800000, 300, 0x100, 0, 0, (0, 0)), (0, 2, mid, 1, 0, 300, 0x100, 0xC0, 0, (0, 0)),
        (0, 5, mid, 0, 0, 7000, 0x100, 0, 0, (0, 0)), (0, 5, mid, 0, 0, 300, 0x100, 0, 0, (0, 0)),
        (0, 2, mid, 0, 0, 300, 0x100, 0, 0, (1, 409600, 0, 0, 0x1234, 0, 0)),
        (0, 2, mid, 0, 0, 300, 0x100, 0, 0, (1, 409600, 0, 0, 1581659904, 0, 0, 0)),
    ]:
        add(case(1, arg=arg, State=state, player=player, crawl=crawl, action=action, PlayerDistance=distance,
                 clock=clock, Field358=f358, CombatFlags=combat, script=script))
    add(case(1, State=2, player=(400 << 12, 500 << 12, 0)))
    add(case(1, State=2, player=(400 << 12, 500 << 12, 0), Field324=17))
    add(case(1, State=2, player=mid, Field1F0=1, script=(1, 204800, 0, 0, 0x1234)))
    add(case(1, State=2, player=(3000 << 12, 0, 4000 << 12), Field21D=3, script=(1, 204800, 0, 0, 0x1234, 0)))
    add(case(1, State=2, player=(3000 << 12, 0, 4000 << 12), Field21D=7, script=(1, 204800, 0, 0, 0x1234, 1)))
    add(case(1, State=2, player=(100 << 12, 0, 50 << 12), script=(1, 204800, 0, 0, 0x1234)))
    # LineOfSightCheck (0x4823C0)
    chunk0 = 1581659904  # gRhinoWallChunk0[0] at 0x55AD18: probes pass through it.
    for arg, script in [(1, (0, 0)), (1, (1, 100 << 12, 0, 0, 0x1234)), (0, (1, 100 << 12, 0, 0, chunk0)),
                        (1, (0, 1, 50 << 12, 0, 0, 0x99)), (1, (1, 50 << 12, 0, 8 << 12, chunk0, 0, 0)),
                        (1, (0, 1, 50 << 12, 0, 0, chunk0, 1, 90 << 12, 0, 0, chunk0, 0))]:
        add(case(2, arg=arg, player=(300 << 12, 0, 200 << 12), PosX=10 << 12, script=script))
    for fn, state, sub, extra in [
        (3, 14, 0, {}), (3, 14, 1, dict(Field230=10)), (3, 14, 1, dict(Field230=1)),
        (4, 15, 0, {}), (4, 15, 1, dict(Field1F8=3)), (4, 15, 2, dict(Field230=5)),
        (5, 3, 0, {}), (5, 3, 1, dict(Field230=10, Finished=1)), (5, 3, 1, dict(Field230=1, Finished=1)),
        (5, 3, 1, dict(Field230=1)),
        (6, 9, 0, {}), (6, 9, 1, dict(Finished=1)), (6, 9, 1, dict(Finished=1, Health=0)),
        (6, 9, 2, dict(Finished=1, Animation=18, AngleY=100)), (6, 9, 2, dict(Finished=1, Animation=21)),
        (7, 10, 0, {}), (7, 10, 0, dict(Completion=1)), (7, 10, 2, dict(Field230=200, Finished=1)),
        (7, 10, 2, dict(Field230=1, Finished=1, CombatFlags=8)), (7, 11, 3, dict(Finished=1)),
        (8, 12, 0, {}), (8, 12, 1, dict(Field34C=100, Field420=3413, Field354=900)),
        (8, 12, 1, dict(Field34C=1, Field420=3413, Field354=900, Finished=1, Field338=5, Frame=7)),
        (8, 12, 1, dict(Field34C=1, Field420=3413, Field354=100, Finished=1)),
        (9, 13, 0, {}), (9, 13, 1, dict(Frame=10)), (9, 13, 1, dict(Frame=17)), (9, 20, 1, dict(Frame=17)),
        (9, 13, 2, dict(Finished=1)), (9, 20, 2, dict(Finished=1)), (9, 13, 3, dict(Finished=1)),
        (10, 16, 0, {}), (10, 16, 1, dict(Field1F8=3, Field350=4, Finished=1)), (10, 16, 1, dict(Field1F8=1)),
        (10, 16, 2, dict(Field350=1, Animation=0x1B, Frame=6)), (10, 16, 3, dict(Finished=1)),
        (10, 17, 4, dict(Frame=5)), (10, 17, 4, dict(Frame=12)), (10, 17, 4, dict(Frame=12, TrapWeb=1)), (10, 17, 5, dict(Finished=1)),
        (11, 6, 0, {}), (11, 6, 1, dict(Field330=10)), (11, 6, 1, dict(Field330=1)),
        (11, 6, 1, dict(Field330=1, Field360=1)), (11, 6, 1, dict(Field330=40, Completion=2, Field360=2)),
        (11, 6, 2, dict(Finished=1)), (11, 6, 3, dict(Finished=1)),
        (12, 5, 0, {}), (12, 5, 1, {}), (12, 5, 1, dict(Completion=2)), (12, 4, 3, dict(Finished=1, Animation=3)),
        (12, 5, 4, dict(Finished=1, Field330=70)),
        (13, 1, 0, {}), (13, 1, 0, dict(Field1F0=1, Path1X=100 << 12, Path1Z=50 << 12, MovementFlags=0x40000)),
        (13, 1, 1, dict(Completion=1, Field1F0=1, RouteFlags=2, MovementFlags=0x10000)),
        (13, 1, 1, dict(Completion=1, Field1F0=1, MovementFlags=0x20000)),
        (14, 21, 0, {}), (14, 21, 1, dict(Finished=1)), (14, 21, 4, dict(Field1F8=59)), (14, 21, 4, dict(Field1F8=30)),
        (14, 21, 3, {}),
    ]:
        add(case(fn, State=state, Substate=sub, player=mid, **extra))
    # Script-driven leaves.
    add(case(3, State=14, Substate=1, Field230=1, player=far, script=(0, 0)))
    add(case(3, State=14, Substate=1, Field230=1, player=near, PlayerDistance=150, script=(0, 0)))
    add(case(4, State=15, Substate=2, Field230=1, player=far, PlayerDistance=3000, script=(0, 0)))
    add(case(4, State=15, Substate=2, Field230=1, player=near, PlayerDistance=150, CachedClock=0x100, CachedDistance=9,
             script=(0, 0)))
    add(case(9, State=13, Substate=1, Frame=17, crawl=1, wall=1, script=(1,)))
    add(case(9, State=13, Substate=1, Frame=17, crawl=1, ceiling=1, script=(0,)))
    add(case(10, State=17, Substate=5, Finished=1, player=far, PlayerDistance=600, script=(0, 0)))
    add(case(10, State=17, Substate=5, Finished=1, player=mid, PlayerDistance=300, script=(0, 0)))
    add(case(11, State=6, Substate=2, Completion=0x10, player=near, script=(1,)))
    add(case(11, State=6, Substate=2, Completion=0x10, Finished=1, player=near, script=(0,)))
    add(case(12, arg=1, State=5, Substate=2, Animation=1, Finished=1, player=near, script=(0, 0)))
    add(case(12, arg=2, State=4, Substate=2, player=far, script=(0, 0)))
    add(case(12, arg=1, State=5, Substate=2, player=far, script=(1, 2000 << 12, 0, 0, 0)))
    for player, crawl, action in [(near, 0, 0), (near, 1, 0), (near, 0, 0x800000), (mid, 0, 0), (far, 0, 0)]:
        add(case(12, arg=2, State=4, Substate=3, Completion=8, player=player, crawl=crawl, action=action))
    add(case(13, State=1, Substate=1, Completion=1, script=(1,)))
    add(case(13, State=1, Substate=1, Completion=1, RouteFlags=2, script=(1,)))
    add(case(13, State=1, Substate=1, Completion=1, script=(0,)))
    # PlaySounds (0x481550)
    for animation, frame, f388, last in [(12, 3, 0, 0), (12, 14, 1, 0x804C), (12, 22, 3, 0x804D), (12, 31, 7, 0x804E),
                                         (12, 40, 15, 0), (5, 0, 0, 0x804C), (5, 9, 1, 0), (2, 0, 0, 0), (2, 16, 1, 0),
                                         (18, 21, 0, 0), (18, 10, 0, 0), (7, 30, 0, 0)]:
        for seed in (0, 1):
            add(case(15, Animation=animation, Frame=frame, Field388=f388, LastFootstep=last, seed=seed))
    for difficulty in range(4):
        add(case(8, State=12, Substate=0, difficulty=difficulty, seed=difficulty))
        add(case(8, State=12, Substate=1, Field34C=50, Field420=3413, Field354=900, difficulty=difficulty))
        add(case(7, State=10, Substate=1, Completion=1, difficulty=difficulty, suppress=difficulty & 1))
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    names = Path.home() / 'Documents/spidey-work/idbs/spideypc_names.txt'
    labels = ('def_', 'jpt_', 'loc_', 'locret_', 'byte_', 'word_', 'dword_', 'off_', 'unk_', 'stru_', 'asc_')
    starts = sorted(int(address, 16) for address, name in
                    (line.split(': ', 1) for line in names.read_text().splitlines() if line[:1].isdigit())
                    if int(address, 16) < 0x530000 and not name.startswith(labels))
    unwind = []
    for start in LEAVES:
        end = next(value for value in starts if value > start)
        unwind.append(f' omitUnwind(0x{start:X},0x{end:X});')
    source = (CPP.replace('@UNWIND@', '\n'.join(unwind))
              .replace('@FIELDS@', ','.join(f'{{0x{offset:X},{size},{signed}}}' for _, offset, size, signed in FIELDS))
              .replace('@FUNCTIONS@', ','.join(f'0x{address:X}' for address, _ in FUNCTIONS)))
    corpus = cases()
    rows = [' '.join(map(str, [c['fn'], c['arg'], *c['hit'], *c['globals'], *c['fields'], len(c['script']), *c['script'],
                               c['seed']])) for c in corpus]
    with tempfile.TemporaryDirectory(prefix='openspidey-rhino-ai-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(source)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), '-o', str(binary)], check=True)
        output = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(rows) + '\n').encode())
    fixture = [dict(input=c, expected=json.loads(line)) for c, line in zip(corpus, output.decode().splitlines(), strict=True)]
    out = root / 'modern/OpenSpidey.Tests/Fixtures/rhino-ai.json'
    out.write_text(json.dumps(dict(fields=[name for name, *_ in FIELDS], cases=fixture), separators=(',', ':')) + '\n')
    print(f'Captured {len(fixture)} original CRhino AI leaf cases.')


if __name__ == '__main__':
    main()
