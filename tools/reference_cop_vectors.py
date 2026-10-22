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
          'distance', 'clearance', 'workCounter', 'slotCursor', 'aimY', 'otherState',
          'joints', 'jointPitch', 'landingY', 'slidePitch', 'slideYaw', 'deathSound', 'pulseSent', 'throwPoints', 'yankOsc', 'web', 'playerCombo', 'workDelay', 'playerAnim', 'playerGrabMode', 'playerYaw']
FRAME = ['clock', 'ticks', 'finished', 'los', 'path', 'add', 'next', 'routeAfter', 'ground', 'recover', 'laser', 'pathCode', 'struck', 'strands', 'grab']

CPP = r'''
#include "original_executable.h"
#include <cstdlib>
#include <cstring>
#include <csetjmp>
#include <csignal>
#include <ucontext.h>
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
int framePathCode,frameStruck,frameGrab;alignas(4)unsigned char struckBody[0x400];short jointWords[32];Vec whipPoints[8];alignas(4)unsigned char trigBuffer[64];
int frameLos,framePath,frameAdd,frameNext,frameRoute,frameGround,frameRecover,frameLaser,losCalls,pathCalls,addCalls,laserCalls;
void event(std::initializer_list<long long>v){events.emplace_back(v);}
void*alloc(unsigned n,int,int,int,int){return std::calloc(1,n);}void release(void*p){std::free(p);}
int rnd(int bound){rngSeed=rngSeed*1103515245u+12345u;int r=int((rngSeed>>16)&0x7FFF)%bound;event({8,bound,r});return r;}
void build(void*p){event({1});}
void __attribute__((thiscall)) messages(void*){event({2});}
int ground(Vec*p,int a,int b,int f){event({3,p->x,p->y,p->z,a,b,f});return frameGround;}
void __attribute__((thiscall)) run(void*p,int id,int from,int to){at<unsigned short>(p,0x12A)=id;at<short>(p,0x128)=from;at<unsigned char>(p,0x142)=0;event({9,id,from,to});}
void __attribute__((thiscall)) cycle(void*p,int id,int direction){at<unsigned short>(p,0x12A)=id;at<unsigned char>(p,0x142)=0;event({10,id,(signed char)direction});}
void sound(unsigned id,Vec*p,int pitch){event({11,id&0xFFFF,p->x,p->y,p->z,pitch});}
Vec pathHit;bool hitMode;int closestType,closestAlert;unsigned char hitOriginal[5];
int __attribute__((stdcall)) pathCheck(Vec*a,Vec*b,void*out,int clearance){int r=((framePath>>pathCalls++)&1)?framePathCode:0;event({12,a->x,a->y,a->z,b->x,b->y,b->z,out?1:0,clearance,mem<int>(0x5FBEE0),r});if(out)*(Vec*)out=pathHit;return r;}
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
alignas(4)unsigned char otherStorage[0x400];void*otherCop(){return otherStorage;}
void stop(std::vector<long long>v){auto s=snap();v.insert(v.end(),s.begin(),s.end());events.push_back(v);stopped=true;}
void diagnostic(int condition,const char*message){if(condition&0xFF)return;int code=!std::strncmp(message,"Unknown",7)?1:!std::strncmp(message,"Shouldn",7)?2:0;
 if(code&&!stopped)stop({99,code});}
sigjmp_buf frameJump;void segv(int,siginfo_t*,void*context){unsigned eip=((ucontext_t*)context)->uc_mcontext.gregs[REG_EIP];if(!stopped)stop({eip==0x42D4E5u?97ll:96ll,eip});siglongjmp(frameJump,1);}
void unported(unsigned address){if(!stopped)stop({98,address});}
#define UNPORTED(name,address) void __attribute__((thiscall)) name(void*){unported(address);}
#define UNPORTED1(name,address) void __attribute__((thiscall)) name(void*,int){unported(address);}
void __attribute__((thiscall)) notifyKill(void*,int type){event({40,type&0xFFFF});}
void*__attribute__((thiscall)) struck(void*,int a,int b){event({41,a,b,frameStruck});if(!frameStruck)return nullptr;at<short>(struckBody,0x38)=frameStruck;return struckBody;}
void __attribute__((stdcall)) elastic(void*,void*body,Vec*v,int t){event({42,at<short>(body,0x38),v->x,v->y,v->z,t});}
alignas(4)unsigned char webObject[0x80],webInner[0x80];
void __attribute__((thiscall)) burst(void*){event({48});}
int __attribute__((thiscall)) grabUpdate(void*,Vec*p,short*yaw){event({49,p->x,p->y,p->z,*yaw,frameGrab});if(frameGrab){p->x+=3;p->z-=3;*yaw+=5;}return frameGrab;}
void sfxPlay(unsigned id,int volume,int pan){event({43,id&0xFFFF,volume,pan});}
void __attribute__((thiscall)) killShadow(void*p){event({44});at<unsigned short>(p,0x46)&=~8;}
void*trigLinks(int){event({45});return trigBuffer;}
void trigPulse(void*){}
void*otherCop();
void*__attribute__((thiscall)) getClosest(void*,int type,int){event({47,type});return type==closestType?otherCop():nullptr;}
void __attribute__((thiscall)) applyPose(void*,void*pose){event({46,pose==(void*)0x549264?1:pose==(void*)0x549344?2:0});}
void __attribute__((thiscall)) laserDelete(void*,int){event({25});}
void*laserVtable[1]={(void*)&laserDelete};void*fakeLaser[1]={(void*)laserVtable};
int __attribute__((thiscall)) laser(void*p,int check,void*target){int r=(frameLaser>>laserCalls++)&1;event({24,check});if(r)at<void*>(p,0x380)=fakeLaser;return r;}
void __attribute__((thiscall)) playerHit(void*,unsigned char*info){event({26,info[0],*(short*)(info+8)});}
void*playerVtable[8]={0,0,0,(void*)&playerHit,0,0,0,0};
void*__attribute__((thiscall)) message(void*m,int,void*,int code,int){event({28,code});return m;}
void*__attribute__((thiscall)) monitor(void*m,void*,int start,int mask,int count,int completion){event({29,start,mask,count,completion});return m;}
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
 v.push_back(at<unsigned short>(actor,4));v.push_back(at<unsigned>(actor,0x24));v.push_back(at<int>(actor,0x30));v.push_back(at<unsigned char>(actor,0x211));
 v.push_back(at<int>(actor,0x308));vec(v,0x2FC);v.push_back(at<int>(actor,0x338));v.push_back(jointWords[6]);v.push_back(at<int>(actor,0x33C));v.push_back(at<int>(actor,0x358));v.push_back(at<int>(player,0x52C));v.push_back(at<int>(player,0x534));
 v.push_back(at<int>(actor,0x354));v.push_back(at<unsigned char>(actor,0x392));v.push_back(at<int>(actor,0x388));v.push_back(at<void*>(actor,0x380)?1:0);
 std::vector<void*>procs;for(void*p=at<void*>(actor,0x28C);p;p=at<void*>(p,0x1C))procs.push_back(p);v.push_back(procs.size());
 for(void*p:procs){v.push_back(at<int>(p,8));v.push_back(at<int>(p,0x10));v.push_back(at<int>(p,0x14));}
 return v;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 stripSeh(0x401000,0x402000);stripSeh(0x428750,0x42FC00);
 for(int i=0;i<4096;i++){mem<short>(0x610C48+i*4)=short(__builtin_sin(i*0.001536096911877394)*4096.0);mem<short>(0x610C4A+i*4)=short(__builtin_cos(i*0.001536096911877394)*4096.0);}
 patch(0x4581A0,(void*)&alloc);patch(0x458210,(void*)&release);patch(0x4E5DA0,(void*)&rnd);patch(0x475F50,(void*)&build);patch(0x42F470,(void*)&messages);
 patch(0x4E6840,(void*)&ground);patch(0x460C10,(void*)&run);patch(0x460D00,(void*)&cycle);patch(0x471EA0,(void*)&sound);patch(0x403350,(void*)&pathCheck);
 patch(0x4E67A0,(void*)&los);patch(0x4041C0,(void*)&height);patch(0x402F60,(void*)&nextWaypoint);patch(0x4E3940,(void*)&position);patch(0x4048F0,(void*)&addPoint);
 patch(0x403230,(void*)&sendSignal);std::memcpy(hitOriginal,(void*)0x429C60,5);patch(0x429C60,(void*)&hit);patch(0x42B270,(void*)&warn);patch(0x403B60,(void*)&nearTarget);patch(0x403FC0,(void*)&ring);
 patch(0x4583F0,(void*)&recover);patch(0x458360,(void*)&makeHandle);patch(0x4015B0,(void*)&diagnostic);
 patch(0x42BB50,(void*)&laser);patch(0x458F10,(void*)&message);patch(0x401C40,(void*)&monitor);
 
 patch(0x4BBC60,(void*)&notifyKill);patch(0x404AE0,(void*)&struck);patch(0x404700,(void*)&elastic);patch(0x471C50,(void*)&sfxPlay);
 patch(0x460570,(void*)&killShadow);patch(0x4E3880,(void*)&trigLinks);patch(0x4DFD30,(void*)&trigPulse);patch(0x460E80,(void*)&applyPose);patch(0x403AD0,(void*)&getClosest);patch(0x4F8600,(void*)&burst);patch(0x4BB810,(void*)&grabUpdate);
 patch(0x402C00,(void*)&baddyCtor);patch(0x460560,(void*)&shadowOn);patch(0x460260,(void*)&attach);patch(0x404FD0,(void*)&parseScript);patch(0x460020,(void*)&initItem);patch(0x453C30,(void*)&hooks);
 mem<void*>(0x6A9038)=player;std::cout<<std::unitbuf;struct sigaction fault={};fault.sa_sigaction=segv;fault.sa_flags=SA_SIGINFO|SA_NODEFER;sigaction(SIGSEGV,&fault,nullptr);
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
  long long h[%HEADER%];for(auto&x:h)std::cin>>x;long long hitIn[14]={};hitMode=mode=='H';if(hitMode)for(auto&x:hitIn)std::cin>>x;
  *(void**)actor=(void*)0x53B670;
  enum{%ENUM%};
  at<short>(actor,0x38)=h[type];at<int>(actor,0x31C)=h[state];at<int>(actor,0x320)=h[substate];at<unsigned>(actor,0x2A8)=h[behavior];at<unsigned>(actor,0x288)=h[completion];
  at<unsigned>(actor,0x218)=h[combat];at<unsigned short>(actor,0x46)=h[bodyFlags];at<int>(actor,0x324)=h[alert];at<int>(actor,0x328)=h[timer328];at<int>(actor,0x32C)=h[closeTicks];
  at<int>(actor,0x1A4)=h[suppression];at<int>(actor,0x318)=h[hitKind];at<int>(actor,0x35C)=h[poll];at<int>(actor,0x1F8)=h[counter];at<int>(actor,0x230)=h[pause];at<int>(actor,0x1F0)=h[pathIndex];
  at<unsigned char>(actor,0x21D)=h[phase];at<unsigned>(actor,0x2F0)=h[route];at<unsigned short>(actor,0xD0)=h[shadow];at<unsigned short>(actor,0x12A)=h[anim];at<short>(actor,0x128)=h[frame];
  at<unsigned char>(actor,0x142)=h[finished];at<unsigned short>(actor,4)=h[itemFlags];at<short>(actor,0xE2)=h[health];at<unsigned char>(actor,0x390)=h[ownership];at<unsigned char>(actor,0x391)=h[slotMask];
  unsigned char*other=(unsigned char*)otherCop();std::memset(other,0,0x400);at<int>(other,0x31C)=h[otherState];mem<void*>(0x682C3C)=h[copGlobal]==1?(void*)actor:h[copGlobal]==2?(void*)other:nullptr;mem<void*>(0x682C40)=h[copList]==1?(void*)actor:h[copList]==2?(void*)other:nullptr;
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
  at<unsigned char>(player,0x57C)=h[unavailable];at<void*>(player,0xE48)=h[held]?(void*)other:nullptr;at<unsigned short>(player,0xEA8)=h[playerClearance];rngSeed=h[seed];*(void**)player=playerVtable;
  at<int>(actor,0x354)=h[workCounter];std::memset(jointWords,0,sizeof jointWords);jointWords[6]=h[jointPitch];at<void*>(actor,0x188)=h[joints]?(void*)jointWords:nullptr;
  at<int>(actor,0x308)=h[landingY];at<int>(actor,0x330)=h[slidePitch];at<int>(actor,0x334)=h[slideYaw];at<int>(actor,0x338)=h[deathSound];at<unsigned char>(actor,0x211)=h[pulseSent];at<int>(actor,0x33C)=h[yankOsc];at<int>(actor,0x358)=h[workDelay];at<void*>(webObject,68)=webInner;at<void*>(actor,0x104)=h[web]?(void*)webObject:nullptr;at<int>(player,0x528)=h[playerCombo];at<unsigned short>(player,0x12A)=h[playerAnim];at<int>(player,0xE1C)=h[playerGrabMode];at<short>(player,0x16)=h[playerYaw];
  for(int i=0;i<8;i++)whipPoints[i]={100000+i*20480,-200000+i*4096,300000-i*12288};at<void*>(actor,0x384)=h[throwPoints]?(void*)whipPoints:nullptr;at<unsigned char>(actor,0x392)=h[slotCursor];at<int>(actor,0x388)=h[aimY];
  int frames;std::cin>>frames;std::cout<<'[';
  for(int f=0;f<frames;f++){long long fr[%FRAME%];for(auto&x:fr)std::cin>>x;if(stopped)continue;events.clear();
   mem<int>(0x5FCCF4)=fr[0];at<int>(actor,0x80)=fr[1];if(fr[2]>=0)at<unsigned char>(actor,0x142)=fr[2];frameLos=fr[3];framePath=fr[4];frameAdd=fr[5];frameNext=fr[6];frameRoute=fr[7];frameGround=fr[8];frameRecover=fr[9];frameLaser=fr[10];framePathCode=fr[11];frameStruck=fr[12];at<int>(webInner,60)=fr[13];frameGrab=fr[14];
   losCalls=pathCalls=addCalls=laserCalls=0;
   long long result=0;
   if(hitMode){alignas(4)unsigned char info[32]={};info[0]=hitIn[0];at<int>(info,4)=hitIn[1];at<short>(info,8)=hitIn[2];at<Vec>(info,12)={int(hitIn[3]),int(hitIn[4]),int(hitIn[5])};
    at<short>(info,24)=hitIn[6];at<unsigned short>(info,26)=hitIn[7];closestType=hitIn[8];closestAlert=hitIn[9];at<int>(otherCop(),0x324)=closestAlert;at<unsigned short>(player,0x12A)=hitIn[10];
    pathHit={int(hitIn[11]),int(hitIn[12]),int(hitIn[13])};std::memcpy((void*)0x429C60,hitOriginal,5);
    result=reinterpret_cast<int(__attribute__((thiscall))*)(void*,void*)>(0x429C60)(actor,info);patch(0x429C60,(void*)&hit);}
   else if(!sigsetjmp(frameJump,1))reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x42FA10)(actor);
   if(f)std::cout<<',';std::cout<<"{\"r\":"<<result<<",\"s\":";numbers(snap());std::cout<<",\"e\":";eventsOut();std::cout<<'}';}
  std::cout<<"]\n";}
}
'''


def header(**changes):
    h = dict.fromkeys(HEADER, 0)
    h.update(type=306, shadow=50, bodyFlags=0x216, poll=15, health=50, x=100000, y=-200000, z=300000, seenX=500000,
             seenY=-200000, seenZ=700000, playerX=100000 + 900 * 4096, playerY=-200000, playerZ=300000 + 400 * 4096,
             playerHealth=100, playerFlags=0x206, seed=0x1234, far=3500, vertical=400, cone=2047, near=100, claim=2000,
             clearance=100, playerClearance=50, behavior=1, snapshot=1000, throwPoints=1)
    h.update(changes)
    return h


def frame(**changes):
    f = dict(clock=1001, ticks=2, finished=-1, los=0, path=0, add=0, next=0, routeAfter=0, ground=-1, recover=0, laser=0, pathCode=1, struck=0, strands=0, grab=0)
    f.update(changes)
    return f


def frames(rng, count, base_clock=1001, **fixed):
    out = []
    for i in range(count):
        f = frame(clock=base_clock + i * 2, ticks=rng.choice([1, 2, 3]), finished=rng.choice([-1, -1, 0, 1]),
                  los=rng.randrange(8), path=rng.randrange(8), add=rng.randrange(8), next=rng.randrange(2),
                  routeAfter=rng.choice([0, 1, 2, 3]), ground=rng.choice([-1, -150000, 0]), recover=rng.randrange(2), laser=rng.randrange(4))
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
    # Caution, chase, melee and shooting with the ported procedures, attack slots, messages and shot boundaries.
    substates = {3: [0, 0, 1], 4: [0, 1, 2, 3, 3, 3, 4, 5, 20, 21, 22, 22, 6], 5: [0, 1, 2, 2, 3, 4, 5, 6, 6, 7],
                 9: [0, 1, 1, 2, 2, 3, 4, 4, 4, 5, 6, 7]}
    for i in range(700):
        state = rng.choice([3, 4, 4, 5, 5, 9, 9])
        far = rng.choice([20, 90, 104, 110, 150, 161, 199, 250, 299, 320, 500, 700, 900, 1200, 1800, 2500])
        h = header(type=rng.choice([306, 320]), state=state, substate=rng.choice(substates[state]),
                   alert=rng.choice([0, 300]), behavior=rng.choice([1, 0x2001, 0x41, 0x801]),
                   combat=rng.choice([0, 0x1000, 0x800]), health=50, phase=rng.randrange(256),
                   playerX=100000 + far * 4096 * rng.choice([1, -1]), playerZ=300000 + rng.choice([0, 30, 200]) * 4096 * rng.choice([1, -1]),
                   playerY=-200000 + rng.choice([0, 0, 20 * 4096, 60 * 4096, 120 * 4096]) * rng.choice([1, -1]),
                   playerHealth=rng.choice([100, 100, 100, 0, 3]), unavailable=rng.choice([0, 0, 0, 1]),
                   held=rng.choice([0, 0, 0, 1]), copList=rng.choice([0, 0, 0, 1, 2]), copGlobal=rng.choice([0, 1, 2, 2]),
                   ownership=rng.choice([0, 1, 1, 2]), slotMask=rng.choice([0, 1, 4, 0x10, 0x80]), sharedSlots=rng.choice([0, 0x0F, 0xFF, 0x55]),
                   otherState=rng.choice([0, 0, 4, 5]), yaw=rng.randrange(4096), counter=rng.choice([0, 1, 3, 6, 7, 30, 120]),
                   pause=rng.choice([0, 1, 2, 7, 8, 15]), workCounter=rng.choice([0, 4, 5, 6, 7]), slotCursor=rng.randrange(256),
                   aimY=rng.choice([0, -200000]), pathIndex=rng.choice([0, 1, 2, 3]), anim=rng.choice([0, 3, 11, 29, 30, 31, 13, 14]),
                   frame=rng.randrange(30), finished=rng.choice([0, 1]), mode=rng.randrange(4), seed=rng.randrange(1 << 31),
                   distanceClock=rng.choice([0, 0, 1000]), distance=rng.choice([0, 150, 5000]), hitKind=rng.choice([0, 1]),
                   itemFlags=rng.choice([0, 4]), poll=rng.choice([0, 3, 15, 31]), suppression=rng.choice([0, 0, 5]),
                   far=rng.choice([3500, 1000]), messages=rng.choice([0, 1]))
        cases.append(dict(mode='A', header=h, frames=frames(rng, rng.choice([1, 4, 8, 16]), base_clock=rng.choice([1000, 1001, 4096, 33]))))
    # Flight, fall and death leaves (0x42CA70, 0x42DD50, 0x42D9E0) with the CBaddy Die/ShouldFall/CheckFallBack helpers.
    def injury_frames(count, **fixed):
        out = frames(rng, count, base_clock=rng.choice([1000, 1001, 33]), **fixed)
        for f in out:
            f.update(pathCode=rng.choice([1, 2]), struck=rng.choice([0, 0, 0, 306, 320, 50, 304]),
                     ground=rng.choice([-1, 209600, 809600, 0, -150000, 209600, 0]))
            f.update(fixed)
        return out
    for i in range(260):
        state = rng.choice([17, 17, 22, 26, 27, 16])
        subs = {17: [0, 0, 1, 1, 1, 2, 3, 3, 5, 10, 10, 4], 22: [0, 0, 1, 2, 2, 3, 4, 4, 5, 6, 7],
                26: [0, 0, 1, 2, 3, 5, 6, 7, 8, 8, 4], 27: [0, 0, 1, 2, 7, 8],
                16: [0, 0, 0, 1, 1, 2, 3, 4, 4, 5]}[state]
        h = header(type=rng.choice([306, 320]), state=state, substate=rng.choice(subs), health=rng.choice([50, 0, 30, -2]),
                   combat=rng.choice([0, 0, 0x4000, 0x2000, 0x400, 0x2400, 2, 0x2002, 0x80, 0x82]), hitKind=rng.choice([0, 1, 2, 6, 23, 25, 3]),
                   vx=rng.choice([0, 4096, -90000, 300000]), vz=rng.choice([0, -4096, 120000, -400000]), vy=rng.choice([0, 2000000, -50000]),
                   counter=rng.choice([0, 0, 1, 2, 3, 5, 9, 30, 40, 41, 65, 120]), anim=rng.choice([17, 20, 22, 23, 24, 5, 36, 37, 38]),
                   frame=rng.choice([0, 10, 21, 22, 30]), finished=rng.choice([0, 1]), completion=rng.choice([0, 0, 4]),
                   behavior=rng.choice([1, 0x11, 0x2011, 0x401, 0x4401, 0x4001, 0x2001]), bodyFlags=rng.choice([0x216, 0x216, 0x206, 0x256, 0x21E]),
                   ownership=rng.choice([0, 1, 2]), slotMask=rng.choice([0, 4, 0x10]), copGlobal=rng.choice([0, 1, 2]), sharedSlots=rng.choice([0, 0xFF, 0x14]),
                   joints=rng.choice([0, 1]), jointPitch=rng.choice([0, 90, 850, 900, 100, 45]), landingY=rng.choice([0, 900000, -200000, 1000000]),
                   slidePitch=rng.choice([0, 7, -30]), slideYaw=rng.choice([0, 11, 200]), deathSound=rng.choice([0, 1, 2, 50]),
                   pulseSent=rng.choice([0, 0, 1]), throwPoints=1, timer328=rng.choice([0, 0, 5]), itemFlags=rng.choice([0, 4, 0x800]),
                   yaw=rng.randrange(4096), seed=rng.randrange(1 << 31), mode=rng.randrange(4), alert=rng.choice([0, 300]))
        if state == 16:
            h['counter'] = rng.randrange(8)  # mpThrowPoints holds eight points; the drag starts at index 0.
        cases.append(dict(mode='A', header=h, frames=injury_frames(rng.choice([1, 4, 8, 16, 24]))))
    # GetYankedBySpidey (0x42D2B0); a spent counter in substate 1 reads a null body's type and faults (stop 97).
    for i in range(220):
        h = header(type=rng.choice([306, 320]), state=rng.choice([14, 14, 15]), substate=rng.choice([0, 0, 1, 1, 2, 3, 9, 10, 10, 11, 5]),
                   health=rng.choice([50, 50, 0, -3]), combat=rng.choice([0, 0x100, 0x20, 0x120, 0x400, 0x500]),
                   counter=rng.choice([1, 2, 5, 30, 79, 79, 0]), anim=rng.choice([18, 19, 22, 23]), finished=rng.choice([0, 1]),
                   behavior=rng.choice([1, 0x11, 0x2011]), bodyFlags=rng.choice([0x216, 0x206]), yankOsc=rng.choice([0, 0, 5, 39, 40]),
                   vx=rng.choice([0, 90000, -200000]), vz=rng.choice([0, 120000, -50000]), ownership=rng.choice([0, 1, 2]),
                   slotMask=rng.choice([0, 4]), copGlobal=rng.choice([0, 1]), sharedSlots=rng.choice([0, 0xFF]), pulseSent=rng.choice([0, 1]),
                   yaw=rng.randrange(4096), seed=rng.randrange(1 << 31), mode=rng.randrange(4))
        cases.append(dict(mode='A', header=h, frames=injury_frames(rng.choice([1, 4, 8, 16, 40]))))
    # Trap-web struggle, state 18 (0x42C750).
    for i in range(120):
        h = header(type=rng.choice([306, 320]), state=18, substate=rng.choice([0, 0, 1, 2, 2, 3, 4]), finished=rng.choice([0, 1]),
                   counter=rng.choice([0, 1, 5]), workCounter=rng.choice([0, 10, 79]), workDelay=rng.choice([0, 1, 3, 20]),
                   web=rng.choice([0, 1, 1, 1]), playerCombo=rng.choice([0, 7, 300]), health=rng.choice([50, 10]),
                   ownership=rng.choice([0, 1, 2]), slotMask=rng.choice([0, 4]), copGlobal=rng.choice([0, 1]), pulseSent=rng.choice([0, 1]),
                   seed=rng.randrange(1 << 31), mode=rng.randrange(4))
        strands = 0
        fs = injury_frames(rng.choice([1, 6, 12, 30]))
        for f in fs:
            strands = min(90, strands + rng.choice([0, 0, 3, 10, 25]))
            f['strands'] = strands
        cases.append(dict(mode='A', header=h, frames=fs))
    # GettingGrabbed (0x42E1D0), state 20 with behavior bit 0x40.
    for i in range(140):
        h = header(type=rng.choice([306, 320]), state=20, substate=rng.choice([0, 1, 1, 1, 2, 2, 3]), finished=rng.choice([0, 1]),
                   behavior=rng.choice([0x41, 0x41, 0x2041, 1]), anim=rng.choice([25, 26, 27, 3, 28]), pathIndex=rng.choice([1, 2, 3, 4, 5]),
                   x=rng.choice([100000 + 12288, 100000 + 2 * 12288 + 10, 100000, 300000]), z=rng.choice([300000 + 8192, 300000 + 16384 + 5, 0]),
                   playerAnim=rng.choice([0, 124, 124, 50]), playerGrabMode=rng.choice([0, 0x8000000]), playerYaw=rng.randrange(4096),
                   ownership=rng.choice([0, 1, 2]), slotMask=rng.choice([0, 4, 0x20]), copGlobal=rng.choice([0, 1, 2, 2]), sharedSlots=rng.choice([0, 0xFF, 0x24]),
                   yaw=rng.randrange(4096), seed=rng.randrange(1 << 31), mode=rng.randrange(4))
        fs = injury_frames(rng.choice([1, 4, 8, 16]))
        for f in fs:
            f['grab'] = rng.choice([1, 1, 1, 0])
        cases.append(dict(mode='A', header=h, frames=fs))
    # CCop::Hit (0x429C60) and its slide setup (0x4297D0).
    for i in range(500):
        h = header(type=rng.choice([306, 320]), state=rng.choice([1, 2, 4, 5, 9, 12, 13, 17, 19, 20, 20, 22, 26, 26, 28]),
                   substate=rng.choice([0, 1, 8, 8, 3]), health=rng.choice([50, 10, 60, 0, -3]),
                   actionFlags=rng.choice([0, 0, 1]), behavior=rng.choice([1, 0x11, 0x2001, 0x10000011]),
                   x=rng.choice([100000, 0, -400000]), z=rng.choice([300000, 0, 700000]), yaw=rng.randrange(4096),
                   clearance=rng.choice([100, 20, 0]), pulseSent=rng.choice([0, 0, 1]), seed=rng.randrange(1 << 31),
                   combat=rng.choice([0, 0x10000, 0x2000]), counter=rng.choice([0, 3]), ownership=rng.choice([0, 1, 2]),
                   copGlobal=rng.choice([0, 1]), mode=rng.randrange(4))
        flags = rng.choice([4, 6, 0x0C, 0x0E, 0x14, 0x16, 0x1C, 2, 0, 0x10, 0x08])
        frames_ = rng.choice([1, 3, 6, 10, 16, 30])
        # Both horizontal components stay nonzero: a blocked slide divides by the dominant velocity axis.
        direction = rng.choice([(4000, 0, -900), (-1200, 0, 3900), (2896, 100, 2896), (-3000, 0, 2700)])
        hit = [flags, rng.choice([0, 1, 3, 29, 23, 6]), rng.choice([0, 10, 50, 70, 200]), *direction,
               rng.choice([20, 100, 400, 1500]), frames_, rng.choice([0, 306, 320]), rng.choice([0, 0, 300]),
               rng.choice([0, 0, 106, 113, 284, 5]), rng.choice([100000, 900000, -200000]), -200000 + 80 * 4096,
               rng.choice([300000, -600000, 500000])]
        fs = [frame(clock=1001, ticks=2, path=rng.randrange(4), pathCode=rng.choice([1, 2, 2, 4]), add=rng.randrange(4))]
        cases.append(dict(mode='H', header=h, hit=hit, frames=fs))
    # Unported leaves stop at their call with the source pre-dispatch state.
    for state in [11, 20, 30]:
        for behavior in [1, 0x41]:
            cases.append(dict(mode='A', header=header(state=state, behavior=behavior), frames=[frame()]))
    rows = []
    for case in cases:
        if case['mode'] == 'H':
            values = [case['header'][k] for k in HEADER] + case['hit'] + [len(case['frames'])] + [f[k] for f in case['frames'] for k in FRAME]
            rows.append('H ' + ' '.join(map(str, values)))
        elif case['mode'] != 'A':
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
