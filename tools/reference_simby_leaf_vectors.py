"""Capture multi-frame CSimby AI leaves against SpideyPC.exe: WalkToTarget, SlideToAttack, AttackPlayer (with
DoStretch), Shoot, TakeHit, GetLaunched, Trapped, DieWhileFalling, DieSimby, DieFireyDeath, SlinkToTarget,
ClimbToTarget and DropDown.

Each case runs one original leaf for several frames on a zeroed CSimby image. RunTimer, RunAppropriateAnim,
DistanceToPlayer, RegisterTargetPos, Utils_CalcAim and the vector/GTE math are the real code. Every other callee
(animation, sound, physics, ground/line queries, AI procedure constructors, sym_base/ribbon/slime/shot/fire
objects, kills, XA) is a recorded transport boundary answering from scripted queues. Objects created by a leaf are
dumped after the last frame so their fields (sym_base scale/follow, ribbon points/widths, slime flags) are checked.
"""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path

LEAVES = {
    0: ('WalkToTarget', 0x4AA4D0), 1: ('SlideToAttack', 0x4AA770), 2: ('AttackPlayer', 0x4AD520),
    3: ('Shoot', 0x4ACFB0), 4: ('TakeHit', 0x4ADB00), 5: ('GetLaunched', 0x4A7BD0), 6: ('Trapped', 0x4A87B0),
    7: ('DieWhileFalling', 0x4ADD50), 8: ('DieSimby', 0x4ADEA0), 9: ('DieFireyDeath', 0x4ADC30),
    10: ('SlinkToTarget', 0x4AABF0), 11: ('ClimbToTarget', 0x4AB570), 12: ('DropDown', 0x4AC180),
    13: ('GettingGrabbed', 0x4A7FA0), 14: ('AI', 0x4AE3D0), 15: ('Hit', 0x4A8AB0),
}

# (offset, width) fields written from the case header and dumped after the last frame.
FIELDS = [
    (0x8, 4), (0xC, 4), (0x10, 4), (0x14, 2), (0x16, 2), (0x18, 2), (0x60, 4), (0x64, 4), (0x68, 4),
    (0x4, 2), (0x28, 2), (0x46, 2), (0xDC, 2), (0xE2, 2), (0x1F8, 4), (0x218, 4), (0x21E, 2),
    (0x230, 4), (0x288, 4), (0x2A8, 4), (0x2AC, 4), (0x294, 4), (0x298, 4), (0x29C, 4), (0x2A0, 4), (0x31C, 4),
    (0x320, 4), (0x310, 4), (0x324, 4), (0x334, 4), (0x348, 4), (0x350, 4), (0x354, 4), (0x358, 4),
    (0x35C, 4), (0x360, 4), (0x364, 4), (0x368, 4), (0x36C, 4), (0x370, 4), (0x380, 4), (0x384, 4), (0x388, 4),
    (0x398, 4), (0x3A0, 4), (0x3A4, 4), (0x3A8, 4), (0x3AC, 4), (0x3B0, 4), (0x3D4, 4), (0x3DC, 4), (0x3E0, 4),
    (0x3E4, 4), (0x3EC, 4), (0x3F0, 4), (0x3F8, 4), (0x3FC, 4), (0x400, 4), (0x410, 4),
] + [(0x418 + 4 * i, 4) for i in range(10)] + [(0x444, 4), (0x448, 4), (0x8A, 2), (0x90, 2), (0x1F0, 4),
    (0x38C, 4), (0x390, 4), (0x394, 4), (0x344, 4), (0x3F4, 4), (0x34C, 4), (0x3C4, 4), (0x3C8, 4), (0x3B4, 4),
    (0x24, 4), (0x194, 4), (0x3B8, 4), (0x3BC, 4), (0x3C0, 4), (0x30C, 4)]

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
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
void stripSeh(unsigned from,unsigned to){for(unsigned char*c=(unsigned char*)from;c<(unsigned char*)to;c++){
 if(c[0]==0x64&&c[1]==0xA1&&!*(unsigned*)(c+2)){c[0]=0x31;c[1]=0xC0;std::memset(c+2,0x90,4);}
 else if(c[0]==0x64&&c[1]==0x89&&(c[2]==0x25||c[2]==0x0D||c[2]==0x15)&&!*(unsigned*)(c+3))std::memset(c,0x90,7);}}
alignas(4)unsigned char actor[0x480],player[0xF20],support[0x100],web[0x500],webInner[0x100],joints[16*12];
alignas(4)unsigned char initialBase[820],initialClimbBase[820],initialRibbon[0x60],initialSlime[0x100],ribbonPoints[8][10*28];
unsigned short frameCounts[64*4+8];unsigned animTable[17];unsigned textureTable[8];
std::vector<std::vector<long long>>events;
std::vector<int>rndQ,groundQ,losQ,lineQ,hookQ,xaQ;unsigned rndI,groundI,losI,lineI,hookI,xaI;
std::vector<unsigned char*>bases,ribbons,slimes;int canTalk,webGround,grabResult;
void ev(std::initializer_list<long long>v){events.emplace_back(v);}
int rnd(int bound){int r=rndQ[rndI++%rndQ.size()];r=bound>0?(unsigned)r%(unsigned)bound:0;ev({8,bound,r});return r;}
int ground(Vec*p,int above,int below,void**sup){unsigned k=(groundI++%(groundQ.size()/2))*2;
 ev({2,p->x,p->y,p->z,above,below,sup!=nullptr});if(sup)*sup=groundQ[k+1]?(void*)support:nullptr;return groundQ[k];}
int los(Vec*a,Vec*b,Vec*out,int flags){unsigned k=(losI++%(losQ.size()/4))*4;
 ev({3,a->x,a->y,a->z,b->x,b->y,b->z,flags});if(!losQ[k]&&out)*out={losQ[k+1],losQ[k+2],losQ[k+3]};return losQ[k];}
void initLine(int*){}
void lineToItem(int*info,int mode){unsigned k=(lineI++%(lineQ.size()/2))*2;
 ev({5,info[0],info[1],info[2],info[3],info[4],info[5],mode});info[26]=lineQ[k];info[28]=lineQ[k+1];}
std::vector<int>pathQ;unsigned pathI;
int __attribute__((thiscall)) pathCheck(void*,Vec*a,Vec*b,Vec*out,int range){unsigned k=(pathI++%(pathQ.size()/4))*4;
 ev({1,a->x,a->y,a->z,b->x,b->y,b->z,out!=nullptr,range});if(out)*out={pathQ[k+1],pathQ[k+2],pathQ[k+3]};return pathQ[k];}
void*__attribute__((thiscall)) messageCtor(void*m,void*sender,void*receiver,int code,void*data){ev({80,code,sender==(void*)actor,receiver==(void*)actor});return m;}
alignas(4)unsigned char hitInfo[0x20];int hitResult;
void*objectCollision(Vec*,Vec*,void*,void*){ev({4});return nullptr;}
void __attribute__((thiscall)) neutralize(void*){ev({6});}
void __attribute__((thiscall)) runAnim(void*p,int id,int from,int to){at<unsigned short>(p,0x12A)=id;at<unsigned char>(p,0x142)=0;ev({7,id,from,to});}
void __attribute__((thiscall)) cycleAnim(void*p,int id,int dir){at<unsigned short>(p,0x12A)=id;ev({9,id,(signed char)dir});}
int sfx(unsigned id,Vec*p,int pitch){ev({11,id&0xFFFF,p==&at<Vec>(actor,8),pitch});return 1;}
int __attribute__((thiscall)) setHeight(void*,int initial,int above,int below){ev({14,initial,above,below});return 1;}
void __attribute__((thiscall)) doPhysics(void*p,int mode){ev({15,mode});
 if(mode){int e=at<int>(p,0x80),b=at<int>(p,0x1F8);int used=b>e?e:b;if(!used)return;at<int>(p,0x1F8)=b-used;}}
void*classNew(unsigned n){return std::calloc(1,n+16);}
void __attribute__((thiscall)) moveTo(void*,void*,int*info,int mask){ev({20,info[0],info[1],info[2],info[3],info[4],info[5],mask});}
void __attribute__((thiscall)) lookAtBody(void*,void*,void*body,Vec*v,int mask,int rate,int tol){
 ev({21,body==(void*)player?1:body?2:0,v?v->x:0,v?v->y:0,v?v->z:0,mask,rate,tol});}
void __attribute__((thiscall)) lookAtYaw(void*,void*,int yaw,int mask,int rate,int tol){ev({22,yaw,mask,rate,tol});}
void __attribute__((thiscall)) monitor(void*,void*,int start,int hooks,int frames,int mask){ev({23,start,hooks,frames,mask});}
void __attribute__((thiscall)) stateSwitch(void*,void*,int message){ev({24,message});}
alignas(4)unsigned char slimeQuads[3][0x40];void*slimeVtableEarly[4];
void*__attribute__((thiscall)) slimeCtor(void*obj,Vec*p,short*angles,int flags){ev({25,p->x,p->y,p->z,flags});slimes.push_back((unsigned char*)obj);
 *(void**)obj=slimeVtableEarly;for(int q=0;q<3;q++)at<void*>(obj,184+4*q)=slimeQuads[q];return obj;}
void*bitNew(unsigned n){return std::calloc(1,n+16);}
void*__attribute__((thiscall)) fireDeath(void*obj,void*){ev({29});return obj;}
long long ribbonIndex(unsigned char*o){if(o==initialRibbon)return -1;for(unsigned i=0;i<ribbons.size();i++)if(ribbons[i]==o)return i;return -2;}
void __attribute__((thiscall)) ribbonDelete(void*obj,int){ev({37,ribbonIndex((unsigned char*)obj)});}
void*ribbonVtable[4]={(void*)&ribbonDelete,0,0,0};
void*__attribute__((thiscall)) ribbonCtor(unsigned char*obj,int faces){ev({30,faces});*(void**)obj=ribbonVtable;
 at<unsigned char*>(obj,68)=ribbonPoints[ribbons.size()];ribbons.push_back(obj);return obj;}
void __attribute__((thiscall)) ribbonTexture(void*,void*t){ev({31,t==(void*)textureTable[5]});}
void __attribute__((thiscall)) ribbonOpaque(void*){ev({32});}
void __attribute__((thiscall)) ribbonRgb(void*,int r,int g,int b){ev({33,r&0xFF,g&0xFF,b&0xFF});}
void __attribute__((thiscall)) ribbonWidth(void*,unsigned short w){ev({34,w});}
void __attribute__((thiscall)) ribbonWidthi(void*o,int i,unsigned short w){ev({35,ribbonIndex((unsigned char*)o),i,w});}
void*__attribute__((thiscall)) baddyCtor(void*o){return o;}
void __attribute__((thiscall)) attachTo(void*,void*){}
void __attribute__((thiscall)) initItem(unsigned char*o,const char*name){bases.push_back(o);ev({40,!std::strcmp(name,"sym_base")});}
unsigned long long makeHandle(void*p){return (unsigned)p;}
long long baseIndex(unsigned char*o){if(o==initialBase)return -1;if(o==initialClimbBase)return -2;for(unsigned i=0;i<bases.size();i++)if(bases[i]==o)return i;return -3;}
void*__attribute__((thiscall)) baseDelete(unsigned char*o,int){ev({42,baseIndex(o)});return o;}
void*__attribute__((thiscall)) shotCtor(void*o,Vec*p){ev({43,p->x,p->y,p->z});return o;}
void hook(Vec*out,void*,short*h){unsigned k=(hookI++%(hookQ.size()/3))*3;ev({44,h[0],h[1],h[2],h[3]});*out={hookQ[k],hookQ[k+1],hookQ[k+2]};}
void __attribute__((thiscall)) applyPose(void*,void*){std::vector<long long>e{45};for(int i=0;i<16*6;i++)e.push_back(((short*)joints)[i]);events.push_back(e);}
void __attribute__((thiscall)) notifyKill(void*,unsigned short type){ev({46,type});}
void __attribute__((thiscall)) die(void*,int a){ev({47,a});}
void splattyDeath(void*){ev({48});}
unsigned char __attribute__((thiscall)) canITalk(void*){ev({49,canTalk});return canTalk;}
unsigned char xaPlay(int bank,int slot,Vec*p,int vol){int r=xaQ[xaI++%xaQ.size()];ev({50,bank,slot,p==&at<Vec>(player,8),vol,r});return r;}
void __attribute__((thiscall)) attachXa(void*b,int bank,int slot){ev({51,b==(void*)player,bank,slot});}
void*recover(void*h){ev({52});return *(void**)h;}
int webGroundY(Vec*p){ev({53,p->x,p->y,p->z});return webGround;}
void*__attribute__((thiscall)) dropCtor(void*o,Vec*p,Vec*v,int g,int f){ev({54,p->x,p->y,p->z,v==(Vec*)0x60D9D0,g,f});return o;}
unsigned char __attribute__((thiscall)) grabUpdate(void*,Vec*p,short*yaw){int r=grabResult;ev({63,p->x,p->y,p->z,*yaw,r});if(r){p->x+=4096;*yaw+=16;}return r;}
void __attribute__((thiscall)) impact(void*,Vec*p,int f){ev({61,p->x,p->y,p->z,f});}
void __attribute__((thiscall)) singleAnim(void*,int a,int b,int c){ev({62,a,b,c});}
int __attribute__((thiscall)) playerHit(void*,unsigned char*info){ev({60,info[0],*(unsigned short*)(info+8)});return 1;}
void*playerVtable[8]={0,0,0,(void*)&playerHit,0,0,0,0};
void __attribute__((thiscall)) cleanUpMessages(void*,int a,int b){ev({27,a,b});}
void __attribute__((thiscall)) burst(void*){ev({28});}
int bumpResult;
int __attribute__((thiscall)) bumped(void*,int radius){ev({29,radius,bumpResult});return bumpResult;}
void __attribute__((thiscall)) slimeSetPos(void*,Vec*p,short*a,int g){ev({70,p->x,p->y,p->z,g});}
void __attribute__((thiscall)) setTint(void*,unsigned char r,unsigned char g,unsigned char b){ev({71,r,g,b});}
void buildTransform(void*){ev({72});}
int __attribute__((thiscall)) nextWaypoint(void*){ev({16});return 0;}
void __attribute__((thiscall)) slimeDelete(void*,int){ev({26});}
void*slimeVtable[4]={(void*)&slimeDelete,0,0,0};
alignas(4)unsigned char quads[3][0x40];alignas(4)unsigned char messages[3][0x50];
void diagnostic(){}
std::vector<int>readInts(int n){std::vector<int>v(n);for(int&x:v)std::cin>>x;return v;}
void dumpBase(std::vector<long long>&s,unsigned char*b){s.push_back(at<int>(b,8));s.push_back(at<int>(b,12));s.push_back(at<int>(b,16));
 s.push_back(at<short>(b,20));s.push_back(at<short>(b,22));s.push_back(at<short>(b,24));s.push_back(at<int>(b,804));s.push_back(at<int>(b,808));
 s.push_back(at<short>(b,40));s.push_back(at<short>(b,42));s.push_back(at<short>(b,44));}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 for(int i=0;i<4096;i++){reinterpret_cast<short*>(0x610C48)[2*i]=sin(i*0.001536096911877394)*4096;reinterpret_cast<short*>(0x610C48)[2*i+1]=cos(i*0.001536096911877394)*4096;}
 stripSeh(0x4A7000,0x4AF500);
 patch(0x4E5DA0,(void*)&rnd);patch(0x4E6840,(void*)&ground);patch(0x4E67A0,(void*)&los);patch(0x4524C0,(void*)&initLine);
 patch(0x4549A0,(void*)&lineToItem);patch(0x403310,(void*)&pathCheck);patch(0x4E6470,(void*)&objectCollision);
 patch(0x4040E0,(void*)&neutralize);patch(0x460C10,(void*)&runAnim);patch(0x460D00,(void*)&cycleAnim);patch(0x471EA0,(void*)&sfx);
 patch(0x4041C0,(void*)&setHeight);patch(0x404C50,(void*)&doPhysics);patch(0x455390,(void*)&classNew);patch(0x4018D0,(void*)&moveTo);
 patch(0x401180,(void*)&lookAtBody);patch(0x4012F0,(void*)&lookAtYaw);patch(0x401C40,(void*)&monitor);patch(0x401E60,(void*)&stateSwitch);
 patch(0x4088A0,(void*)&bitNew);patch(0x4A46D0,(void*)&slimeCtor);patch(0x4A3640,(void*)&fireDeath);patch(0x40A460,(void*)&ribbonCtor);
 patch(0x40A6D0,(void*)&ribbonTexture);patch(0x40A6B0,(void*)&ribbonOpaque);patch(0x40A920,(void*)&ribbonRgb);patch(0x40A970,(void*)&ribbonWidth);
 patch(0x40A9B0,(void*)&ribbonWidthi);patch(0x402C00,(void*)&baddyCtor);patch(0x460260,(void*)&attachTo);patch(0x460020,(void*)&initItem);
 patch(0x458360,(void*)&makeHandle);patch(0x4ACE80,(void*)&baseDelete);patch(0x4A5FD0,(void*)&shotCtor);patch(0x453EE0,(void*)&hook);
 patch(0x460E80,(void*)&applyPose);patch(0x4BBC60,(void*)&notifyKill);patch(0x404320,(void*)&die);patch(0x4A7050,(void*)&splattyDeath);
 patch(0x4B9730,(void*)&canITalk);patch(0x479D30,(void*)&xaPlay);patch(0x4603D0,(void*)&attachXa);patch(0x4583F0,(void*)&recover);
 patch(0x4F54A0,(void*)&webGroundY);patch(0x4A57E0,(void*)&dropCtor);patch(0x4015B0,(void*)&diagnostic);
 patch(0x4BB810,(void*)&grabUpdate);patch(0x458F10,(void*)&messageCtor);slimeVtableEarly[0]=slimeVtable[0];
 patch(0x403A90,(void*)&cleanUpMessages);patch(0x4F8600,(void*)&burst);patch(0x404470,(void*)&bumped);patch(0x4A4ED0,(void*)&slimeSetPos);
 patch(0x4093C0,(void*)&setTint);patch(0x475F50,(void*)&buildTransform);patch(0x402F60,(void*)&nextWaypoint);patch(0x4C6E10,(void*)&impact);patch(0x4C9130,(void*)&singleAnim);
 const unsigned fields[][2]={FIELD_LIST};const int fieldCount=sizeof fields/sizeof fields[0];
 int fn;while(std::cin>>fn){
  std::memset(actor,0,sizeof actor);std::memset(player,0,sizeof player);std::memset(support,0,sizeof support);std::memset(web,0,sizeof web);
  std::memset(initialBase,0,sizeof initialBase);std::memset(initialClimbBase,0,sizeof initialClimbBase);std::memset(initialSlime,0,sizeof initialSlime);
  std::memset(ribbonPoints,0,sizeof ribbonPoints);std::memset(initialRibbon,0,sizeof initialRibbon);
  events.clear();bases.clear();ribbons.clear();slimes.clear();rndI=groundI=losI=lineI=hookI=xaI=0;
  auto v=readInts(fieldCount);
  for(int i=0;i<fieldCount;i++){unsigned o=fields[i][0];if(fields[i][1]==2)at<short>(actor,o)=v[i];else at<int>(actor,o)=v[i];}
  at<short>(actor,0x38)=324;at<short>(actor,0x2A)=at<short>(actor,0x28);at<short>(actor,0x2C)=at<short>(actor,0x28);
  auto j=readInts(16*6);for(int i=0;i<16*6;i++)((short*)joints)[i]=j[i];at<void*>(actor,0x188)=joints;
  auto x=readInts(66);for(int i=0;i<9;i++)at<short>(actor,0x164+2*i)=x[24+i];
  at<Vec>(player,8)={x[0],x[1],x[2]};at<short>(player,0xE2)=x[3];at<unsigned char>(player,0x57C)=x[4];at<unsigned short>(player,0xDC)=x[5];
  at<unsigned>(actor,0xE4)=x[6];mem<int>(0x5FCCF4)=x[7];mem<int>(0x682C60)=x[8];mem<unsigned>(0x682C68)=x[9];mem<int>(0x682C64)=x[10];
  at<int>(support,0x64)=x[11];canTalk=x[12];webGround=x[13];
  *(void**)player=playerVtable;at<unsigned short>(player,0x12A)=x[33];at<int>(player,0xE1C)=x[34];at<short>(player,0x16)=x[35];grabResult=x[36];
  at<unsigned>(player,0x554)=0x1111;bumpResult=x[37];
  {void*next=nullptr;std::memset(messages,0,sizeof messages);for(int m=x[38]-1;m>=0;m--){unsigned char*g=messages[m];
    at<void*>(g,8)=next;at<int>(g,20)=x[39+m*6];at<int>(g,40)=x[40+m*6];at<int>(g,44)=x[41+m*6];at<int>(g,48)=x[42+m*6];
    at<int>(g,64)=x[43+m*6];at<int>(g,16)=x[44+m*6];next=g;}at<void*>(actor,0x290)=next;}
  if(x[14]){at<unsigned char>(web,1048)=x[15]!=0;at<void*>(web,68)=webInner;at<int>(webInner,60)=x[16];at<void*>(actor,0x104)=web;}
  *(void**)initialBase=(void*)0x53C100;*(void**)initialClimbBase=(void*)0x53C100;
  if(x[17])at<void*>(actor,0x338)=initialBase;if(x[18])at<void*>(actor,0x33C)=initialClimbBase;if(x[19]){*(void**)initialRibbon=ribbonVtable;at<void*>(initialRibbon,68)=ribbonPoints[7];at<void*>(actor,0x340)=initialRibbon;}
  if(x[20]){*(void**)initialSlime=slimeVtable;for(int q=0;q<3;q++)at<void*>(initialSlime,184+4*q)=quads[q];at<void*>(actor,0x3D8)=initialSlime;}
  at<unsigned char>(actor,0x1F)=0;for(int i=0;i<64;i++)frameCounts[4*i+4]=x[21]+i;mem<unsigned>(0x6B245C)=(unsigned)frameCounts;
  textureTable[5]=0x1234;mem<unsigned>(0x56EAC4)=(unsigned)textureTable;mem<void*>(0x6A9038)=player;
  for(int i=0;i<8;i++)mem<Vec>(0x6A7B10+i*12)={x[22]+i*409600,0,x[23]};
  int frames;std::cin>>frames;auto f=readInts(frames*4);
  int n;std::cin>>n;rndQ=readInts(n);std::cin>>n;groundQ=readInts(n*2);std::cin>>n;losQ=readInts(n*4);std::cin>>n;lineQ=readInts(n*2);
  std::cin>>n;hookQ=readInts(n*3);std::cin>>n;xaQ=readInts(n);std::cin>>n;pathQ=readInts(n*4);pathI=0;
  std::memset(hitInfo,0,sizeof hitInfo);hitInfo[0]=x[57];hitInfo[1]=x[58];at<int>(hitInfo,4)=x[59];at<unsigned short>(hitInfo,8)=x[60];
  at<Vec>(hitInfo,12)={x[61],x[62],x[63]};at<short>(hitInfo,24)=x[64];at<unsigned short>(hitInfo,26)=x[65];hitResult=-7;
  for(int fr=0;fr<frames;fr++){at<int>(actor,0x80)=f[fr*4];at<short>(actor,0x128)=f[fr*4+1];at<unsigned char>(actor,0x142)=f[fr*4+2];
   at<unsigned>(actor,0x288)|=f[fr*4+3];ev({90,fr});
   if(fn==15){unsigned char fresh[0x20];std::memcpy(fresh,hitInfo,sizeof fresh);
    hitResult=reinterpret_cast<int(__attribute__((thiscall))*)(void*,void*)>(LEAF)(actor,fresh);}
   else reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(LEAF)(actor);}
  std::vector<long long>s;
  for(int i=0;i<fieldCount;i++){unsigned o=fields[i][0];s.push_back(fields[i][1]==2?at<short>(actor,o):at<int>(actor,o));}
  for(int i=0;i<16*6;i++)s.push_back(((short*)joints)[i]);
  s.push_back(at<unsigned short>(actor,0x12A));s.push_back(mem<int>(0x682C60));s.push_back(mem<unsigned>(0x682C68));s.push_back(mem<int>(0x682C64));
  s.push_back(at<unsigned char*>(actor,0x338)==nullptr?0:baseIndex(at<unsigned char*>(actor,0x338))+10);
  s.push_back(at<unsigned char*>(actor,0x33C)==nullptr?0:baseIndex(at<unsigned char*>(actor,0x33C))+10);
  s.push_back(at<unsigned char*>(actor,0x340)==nullptr?0:ribbonIndex(at<unsigned char*>(actor,0x340))+10);
  s.push_back(at<void*>(actor,0x3D8)!=nullptr);
  s.push_back(x[20]?(at<int>(initialSlime,164)==-32&&at<int>(initialSlime,156)==1&&at<unsigned char>(initialSlime,58)==0):0);
  for(unsigned char*b:bases)dumpBase(s,b);dumpBase(s,initialBase);dumpBase(s,initialClimbBase);
  for(unsigned r=0;r<ribbons.size();r++)for(int i=0;i<10;i++){s.push_back(at<int>(ribbonPoints[r],i*28));s.push_back(at<int>(ribbonPoints[r],i*28+4));s.push_back(at<int>(ribbonPoints[r],i*28+8));}
  for(int i=0;i<10;i++){s.push_back(at<int>(ribbonPoints[7],i*28));s.push_back(at<int>(ribbonPoints[7],i*28+4));s.push_back(at<int>(ribbonPoints[7],i*28+8));}
  for(unsigned char*o:slimes){s.push_back(o[58]);s.push_back(at<int>(o,164));}
  {unsigned ai=at<unsigned>(player,0x554);s.push_back(ai==0x1111?0:ai==0x4A8720?1:ai==0x4A8680?2:ai==0?3:9);
   unsigned saved=at<unsigned>(actor,0x45C);s.push_back(saved==0?0:saved==0x1111?1:saved==0x4A8720?2:9);
   for(int i=0;i<3;i++)s.push_back(at<int>(player,8+4*i));for(int i=0;i<3;i++)s.push_back(at<int>(player,0x60+4*i));s.push_back(at<int>(player,0xE1C));}
  for(int m=0;m<x[38];m++){s.push_back(at<int>(messages[m],16));s.push_back(at<int>(messages[m],64));}
  s.push_back(at<unsigned short>(actor,0x314));s.push_back(at<void*>(actor,0x2A4)!=nullptr);
  s.push_back(fn==15?hitResult:0);s.push_back(at<int>(actor,0x318));
  std::cout<<"{\"state\":[";for(unsigned i=0;i<s.size();i++)std::cout<<(i?",":"")<<s[i];std::cout<<"],\"events\":[";
  for(unsigned i=0;i<events.size();i++){std::cout<<(i?",":"")<<'[';for(unsigned k=0;k<events[i].size();k++)std::cout<<(k?",":"")<<events[i][k];std::cout<<']';}
  std::cout<<"]}\n";
 }}
'''

U = 4096


def vec(rng, spread=1500):
    return [rng.randint(-spread, spread) * U + rng.randint(-2048, 2048) for _ in range(3)]


def make_case(rng, fn):
    pos = vec(rng)
    player = [pos[0] + rng.randint(-600, 600) * U, pos[1] + rng.choice([0, 0, 0, rng.randint(-200, 200)]) * U,
              pos[2] + rng.randint(-600, 600) * U]
    target = [pos[0] + rng.randint(-1500, 1500) * U, pos[1] + rng.choice([0, rng.randint(-1200, 100)]) * U,
              pos[2] + rng.randint(-1500, 1500) * U]
    climb = [pos[0], pos[1] - rng.randint(700, 2000) * U, pos[2]]
    stage = {0: 5, 1: 21, 2: 17, 3: 18, 4: 10, 5: 11, 6: rng.choice([12, 13]), 7: 19, 8: 22, 9: 23,
             10: rng.choice([6, 16, 24]), 11: 7, 12: rng.choice([8, 20]), 13: 14,
             14: rng.choice([4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25]),
             15: rng.choice([4, 4, 5, 9, 10, 11, 17, 22, 2])}[fn]
    sub = 0 if rng.random() < 0.7 else rng.choice({0: [1, 2], 1: [1], 2: [1, 2, 3, 4, 5, 10], 3: [1, 2, 3], 4: [1],
                                                    5: [1, 2], 6: [1, 2, 3, 4, 5, 6], 7: [1], 8: [1], 9: [1],
                                                    10: [1, 2, 3, 4, 5], 11: [1, 2, 3, 4, 5],
                                                    12: [1, 2, 3, 5, 6, 15], 13: [1, 2, 3, 4, 10, 11, 12, 13], 14: [1],
                                                    15: [1]}[fn])
    behavior = rng.choice([0, 0, 0x10, 0x20, 0x40, 0x80, 0x1000, 0x2000]) | rng.choice([0, 0, 0x100, 0x200, 0x400, 0x4000])
    if fn == 2:
        # DoStretch reads an uninitialized hook offset unless exactly one arm bit matches the substate.
        behavior = (behavior & ~0x700) | rng.choice([0, {2: 0x100, 3: 0x200, 5: 0x400}.get(sub, 0)])
    setup_high = rng.choice([261, 261, 261, 0x105, 0x10105, 0])
    values = {
        0x8: pos[0], 0xC: pos[1], 0x10: pos[2], 0x14: rng.randrange(4096), 0x16: rng.randrange(4096), 0x18: 0,
        0x60: rng.randint(-40000, 40000), 0x64: rng.randint(-40000, 40000), 0x68: rng.randint(-40000, 40000),
        0x4: rng.choice([0, 2, 6, 0x202, 0x402] + ([0x8002, 0x8402] if fn == 14 else [])), 0x28: rng.choice([4096, 4096, 3000, 300, 100]), 0x46: rng.choice([0x216, 0x206]),
        0xDC: rng.choice([140, 140, 100]), 0xE2: rng.choice([800, 0, 100]),
        0x1F8: rng.choice([0, 0, 3, 10, 25, rng.randint(0, 80)]), 0x218: behavior, 0x21E: rng.choice([100, 100, 60, 0]),
        0x230: rng.choice([0, 1, 10, 20, rng.randint(0, 1300000)]), 0x288: rng.choice([0, 1, 2, 3]),
        0x2A8: rng.choice([0x201, 0x10201]), 0x2AC: rng.choice([0, 0x10000, 0x20000, 0x40000]),
        0x294: rng.choice([84215815, 513, 0x01020304]), 0x298: setup_high, 0x29C: pos[1], 0x2A0: pos[1] + 100 * U,
        0x31C: stage, 0x320: sub, 0x310: 0, 0x324: rng.randint(0, 900), 0x334: rng.choice([0, 30, 15, 1]),
        0x348: rng.choice([0, 100, 3000]), 0x350: pos[0], 0x354: pos[1] + rng.choice([0, 4096, 9000]), 0x358: pos[2],
        0x35C: target[0], 0x360: target[1], 0x364: target[2], 0x368: rng.randint(-4096, 4096), 0x36C: 0,
        0x370: rng.randint(-4096, 4096), 0x380: climb[0], 0x384: climb[1], 0x388: climb[2], 0x398: rng.randrange(4096),
        0x3A0: rng.choice([0, 1, 0x10]), 0x3A4: climb[1] + rng.randint(-50, 50) * U, 0x3A8: rng.choice([280, 900, 0]),
        0x3AC: rng.choice([0, 1, 3]), 0x3B0: rng.choice([0, 455, 204]), 0x3D4: rng.choice([0, 100, 256]),
        0x3DC: 0, 0x3E0: 0, 0x3E4: 0, 0x3EC: 0, 0x3F0: rng.choice([0, 0, 1, 2]),
        0x3F8: 0, 0x3FC: 0, 0x400: 0, 0x410: rng.choice([0, 1500, rng.randint(1, 3000)]),
        0x444: rng.choice([0, 100, 900]), 0x448: rng.choice([0, 3000]),
        0x8A: rng.randint(-50, 50), 0x90: rng.randint(-50, 50), 0x1F0: rng.randint(0, 3),
        0x38C: 0, 0x390: 0, 0x394: 0, 0x344: rng.choice([0x801, 0x801, 0x1104, 0x702]), 0x3F4: rng.choice([0, 0, 1, 30]),
        0x34C: rng.choice([0, 1, 50]), 0x3C4: rng.randint(0, 25), 0x3C8: 0, 0x3B4: rng.choice([0, 1]),
        0x24: rng.randrange(1 << 24), 0x194: rng.choice([0, 0x8000, 7]), 0x3B8: 0xDAC, 0x3BC: rng.choice([0x190, 0x190, 0]),
        0x3C0: 0x555, 0x30C: 0x64,
    }
    for i in range(10):
        values[0x418 + 4 * i] = rng.choice([0, 0, 585, 900, rng.randint(0, 900)]) if sub else 0
    header = [values[o] for o, _ in FIELDS]
    joints = [rng.randint(-600, 600) if rng.random() < 0.3 else 0 for _ in range(16 * 6)]
    extra = [*player, rng.choice([100, 100, 0]), rng.choice([0, 0, 1]), rng.choice([140, 70]),
             rng.choice([50, 200, 300, 3000]), rng.randint(100, 10000), rng.choice([0, 1, 7]), rng.randrange(256),
             rng.choice([0, 1]), rng.randint(-30, 30), rng.choice([0, 1]), pos[1] + rng.randint(0, 400) * U,
             int(rng.random() < 0.8), int(rng.random() < 0.7), rng.choice([0, 41, 39]),
             int(rng.random() < 0.3), int(rng.random() < 0.15), int(rng.random() < 0.15), int(rng.random() < 0.5),
             rng.randint(5, 40), pos[0] + rng.choice([0, 600000]), pos[2],
             *[rng.randint(-4096, 4096) for _ in range(9)],
             rng.choice([0, 124, 124]), rng.choice([0, 0x8000000]), rng.randrange(4096), rng.choice([0, 1, 1]),
             rng.choice([0, 1])]
    # Message 17 drops the landing sym_base that DropDown substate 2 dereferences unconditionally.
    codes = [5, 10, 14, 20, 20, 3] + ([] if stage in (8, 20) else [17])
    messages = [rng.choice(codes) for _ in range(rng.choice([0, 0, 1, 2, 3]))] if fn == 14 else []
    extra += [len(messages)]
    for m in range(3):
        if m < len(messages):
            extra += [messages[m], *vec(rng), rng.choice([0, 3, 7, 8]), rng.choice([0, 0, 1])]
        else:
            extra += [0, 0, 0, 0, 0, 0]
    extra += [rng.choice([0, 2, 4, 6, 0xE, 0x1E, 0x16, 0x0C]), 0, rng.choice([0, 2, 3, 6, 7, 22, 23, 1]),
              rng.choice([10, 50, 400, 900, 2000]), *[rng.randint(-4096, 4096) for _ in range(3)],
              rng.choice([0, 10, 100, 300, -50]), rng.choice([1, 5, 15, 30])]
    # The original dereferences these objects without a null check in these substates.
    if stage == 7 and sub in (2, 3):
        extra[19] = 1
    if stage in (8, 20) and sub == 2:
        extra[17] = 1
    frames = []
    for _ in range(rng.randint(3, 14)):
        frames += [rng.choice([1, 1, 2, 3]), rng.choice([0, 5, 10, 14, 28, 40]), int(rng.random() < 0.35),
                   rng.choice([0, 0, 0, 1, 2, 3])]
    base_y = target[1] + rng.randint(-50, 50) * U
    grounds = []
    for _ in range(rng.randint(1, 5)):
        y = -1 if rng.random() < 0.1 else rng.choice([base_y, pos[1] + rng.randint(0, 400) * U])
        grounds += [y, int(rng.random() < 0.2)]
    loses = []
    for _ in range(rng.randint(1, 3)):
        loses += [int(rng.random() < 0.7), *vec(rng)]
    lines = []
    for _ in range(rng.randint(1, 2)):
        lines += [int(rng.random() < 0.6), climb[1] + rng.randint(-60, 60) * U]
    hooks = []
    for _ in range(2):
        hooks += [pos[0] + rng.randint(-60, 60) * U, pos[1] - rng.randint(0, 200) * U, pos[2] + rng.randint(-60, 60) * U]
    rnds = [rng.randrange(1 << 15) for _ in range(rng.randint(1, 6))]
    xas = [rng.choice([0, 1])]
    paths = []
    for _ in range(rng.randint(1, 3)):
        paths += [rng.choice([0, 0, 1, 2, 2, 4]), *[p + rng.randint(-200, 200) * U for p in pos]]
    tail = [len(frames) // 4, *frames, len(rnds), *rnds, len(grounds) // 2, *grounds, len(loses) // 4, *loses,
            len(lines) // 2, *lines, len(hooks) // 3, *hooks, len(xas), *xas, len(paths) // 4, *paths]
    return dict(fn=fn, header=header, joints=joints, extra=extra, tail=tail)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    parser.add_argument('--cases', type=int, default=120)
    parser.add_argument('--work-dir', type=Path, help='keep the generated harnesses and inputs here')
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x51B1EAF)
    field_list = ','.join(f'{{{o:#x},{w}}}' for o, w in FIELDS)
    by_leaf = {fn: [make_case(rng, fn) for _ in range(args.cases)] for fn in LEAVES}
    fixtures = []
    with tempfile.TemporaryDirectory(prefix='openspidey-simby-leaf-') as scratch:
        folder = str(args.work_dir or scratch)
        for fn, (name, address) in LEAVES.items():
            cpp, binary = Path(folder) / f'leaf{fn}.cpp', Path(folder) / f'leaf{fn}'
            cpp.write_text(CPP.replace('FIELD_LIST', field_list).replace('LEAF', hex(address)))
            subprocess.run(['g++', '-m32', '-std=c++17', '-O0', '-fno-pie', '-no-pie', '-fwrapv',
                            '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                            str(cpp), '-o', str(binary)], check=True)
            cases = by_leaf[fn]
            stdin = '\n'.join(' '.join(str(v) for v in [c['fn'], *c['header'], *c['joints'], *c['extra'], *c['tail']])
                              for c in cases) + '\n'
            (Path(folder) / f'leaf{fn}.in').write_text(stdin)
            result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=stdin.encode())
            lines = result.decode().splitlines()
            assert len(lines) == len(cases), (name, len(lines), len(cases))
            for case, line in zip(cases, lines, strict=True):
                output = json.loads(line)
                fixtures.append(dict(fn=fn, header=case['header'], joints=case['joints'], extra=case['extra'],
                                     tail=case['tail'], state=output['state'], events=output['events']))
    out = root / 'modern/OpenSpidey.Tests/Fixtures/simby-leaves.json'
    out.write_text(json.dumps(dict(fields=[[o, w] for o, w in FIELDS], cases=fixtures), separators=(',', ':')) + '\n')
    changed = sum(1 for c in fixtures if c['state'][FIELDS.index((0x31C, 4))] != c['header'][FIELDS.index((0x31C, 4))])
    print(f'Captured {len(fixtures)} Simby leaf cases over {len(LEAVES)} leaves; {changed} left their stage.')


if __name__ == '__main__':
    main()
