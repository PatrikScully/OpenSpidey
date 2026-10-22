"""Capture CVenom combat leaves (fight selection, jump, hit reactions, trap web, message loop) from SpideyPC.exe."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path

# Node table shared with VenomCombatTests: [type, subtype, x, y, z] in whole units.
NODES = [
    [0, 0, 0, 0, 0],
    [1002, 3, 500, 0, 100],
    [1002, 3, 1500, 0, -200],
    [1002, 1, 900, 0, 900],
    [1002, 3, -800, 0, 900],
    [1002, 3, 3500, 0, 0],
    [5, 0, 100, 0, 100],
    [1002, 3, 2200, 0, 2200],
    [1002, 3, 7, 0, 5],
    [1002, 3, 1200, 0, 1200],
    [1, 0, 1000, 0, 0],
    [1, 0, 0, 0, 0],
]
RANDOM = [7, 3, 11, 0, 5, 2, 9, 14, 1, 6, 13, 8, 4, 12, 10, 15, 3, 3, 0, 1, 2, 7, 7, 5,
          19, 22, 31, 17, 2, 2, 8, 9, 6, 0, 1, 1, 4, 4, 3, 2, 11, 12, 5, 5, 5, 9, 10, 0,
          65, 99, 12, 80, 33, 59, 61, 0, 258, 4095, 1024, 3000, 17, 250, 1, 2]

# name: (buffer, offset, size).  v = venom body, p = MechList player, h = SHitInfo, o = pose joints.
MEM = {
    'ai': ('v', 0x31C, 4), 'sub': ('v', 0x320, 4), 'f218': ('v', 0x218, 4), 'item': ('v', 0x04, 2),
    'body': ('v', 0x46, 2), 'minor': ('v', 0xDC, 2), 'health': ('v', 0xE2, -2), 'frame': ('v', 0x128, -2),
    'anim': ('v', 0x12A, 2), 'finished': ('v', 0x142, 1), 'ticks': ('v', 0x80, 4),
    'px': ('v', 0x08, 4), 'py': ('v', 0x0C, 4), 'pz': ('v', 0x10, 4),
    'ax': ('v', 0x14, -2), 'ay': ('v', 0x16, -2), 'az': ('v', 0x18, -2),
    'vx': ('v', 0x60, 4), 'vy': ('v', 0x64, 4), 'vz': ('v', 0x68, 4),
    'tx': ('v', 0x240, 4), 'ty': ('v', 0x244, 4), 'tz': ('v', 0x248, 4),
    'retAi': ('v', 0x226, -2), 'retSub': ('v', 0x228, -2), 'f39C': ('v', 0x39C, 4), 'f3E0': ('v', 0x3E0, 4),
    'f388': ('v', 0x388, 4), 'f368': ('v', 0x368, 4), 'f36C': ('v', 0x36C, 4), 'f370': ('v', 0x370, 4),
    'f374': ('v', 0x374, 4), 'slide': ('v', 0x378, 4), 'sx': ('v', 0x37C, 4), 'sy': ('v', 0x380, 4),
    'sz': ('v', 0x384, 4), 'f318': ('v', 0x318, 4), 'jumpIdx': ('v', 0x394, 4), 'jumpCount': ('v', 0x38C, 4),
    'jx': ('v', 0x3A0, 4), 'jy': ('v', 0x3A4, 4), 'jz': ('v', 0x3A8, 4), 'trapCtr': ('v', 0x1F8, 4),
    'stretch': ('v', 0x3FC, 4), 'f330': ('v', 0x330, 4), 'webT': ('v', 0x104, 4), 'webG': ('v', 0x10C, 4),
    'webObj': ('v', 0x428, 4), 'elec': ('v', 0x334, 4), 'f3E0b': ('v', 0x3E0, 4), 'pathPtr': ('v', 0x390, 4),
    'f33D': ('v', 0x33D, 1), 'f324': ('v', 0x324, 4),
    'pAction': ('p', 0xE1C, 4), 'pHealth': ('p', 0xE2, -2), 'p36C': ('p', 0x36C, 4), 'pCrawl': ('p', 0xAD4, 1),
    'p8E8': ('p', 0x8E8, 1), 'p8E9': ('p', 0x8E9, 1), 'qx': ('p', 0x08, 4), 'qy': ('p', 0x0C, 4), 'qz': ('p', 0x10, 4),
    'hFlags': ('h', 0, 1), 'hKind': ('h', 4, 4), 'hDmg': ('h', 8, 2), 'hdx': ('h', 12, 4), 'hdy': ('h', 16, 4),
    'hdz': ('h', 20, 4), 'hSlideD': ('h', 24, -2), 'hSlideF': ('h', 26, 2),
    'o198': ('o', 198, -2), 'o200': ('o', 200, -2), 'o202': ('o', 202, -2),
    'o210': ('o', 210, -2), 'o212': ('o', 212, -2), 'o214': ('o', 214, -2),
}
# Inputs that are not plain memory.
SPECIAL = ['mode', 'seed', 'level', 'difficulty', 'timer', 'losMask', 'groundMode', 'groundParam', 'recT', 'recG',
           'lineHit', 'lineDist', 'count', 'pathSeed', 'poseOn']
MEM_INPUTS = ['ai', 'sub', 'f218', 'item', 'body', 'minor', 'health', 'frame', 'anim', 'finished', 'ticks',
              'px', 'py', 'pz', 'ax', 'ay', 'az', 'vx', 'vy', 'vz', 'tx', 'ty', 'tz', 'retAi', 'retSub', 'f39C',
              'f3E0', 'f388', 'f368', 'f36C', 'f370', 'f374', 'slide', 'sx', 'sy', 'sz', 'f318', 'jumpIdx',
              'jumpCount', 'jx', 'jy', 'jz', 'trapCtr', 'stretch', 'f330', 'webT', 'webG', 'webObj', 'elec',
              'f33D', 'f324', 'pAction', 'pHealth', 'p36C', 'pCrawl', 'p8E8', 'p8E9', 'qx', 'qy', 'qz', 'hFlags',
              'hKind', 'hDmg', 'hdx', 'hdy', 'hdz', 'hSlideD', 'hSlideF']
INPUTS = SPECIAL + MEM_INPUTS
OUTPUTS = ['ret', 'used', 'ai', 'sub', 'f218', 'item', 'body', 'minor', 'health', 'anim', 'frame', 'px', 'py', 'pz',
           'ax', 'ay', 'az', 'vx', 'vy', 'vz', 'tx', 'ty', 'tz', 'retAi', 'retSub', 'f3E0', 'f388', 'f368', 'f36C',
           'f370', 'f374', 'slide', 'sx', 'sy', 'sz', 'f318', 'jumpIdx', 'jumpCount', 'jx', 'jy', 'jz', 'trapCtr',
           'stretch', 'f330', 'webT', 'webG', 'webObj', 'elec', 'f33D', 'f324', 'o198', 'o200', 'o202', 'o210',
           'o212', 'o214', 'qx', 'qy', 'qz', 'msg0', 'msg1', 'msg2', 'pathFreed']
MESSAGE_INPUTS = ['m0', 'm1', 'm2']
INPUTS += MESSAGE_INPUTS


def table_cpp(names, with_special):
    rows = []
    for name in names:
        if name in MEM:
            buf, off, size = MEM[name]
            rows.append(f'{{"{name}",\'{buf}\',{off},{size}}}')
    return ',\n'.join(rows)


CPP_TEMPLATE = r'''
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
std::vector<int> R;int seed,used,level,losMask,losCalls,groundMode,groundParam,recT,recG,lineHit,lineDist;
std::vector<std::string> events;
alignas(4) unsigned char venom[0x600],player[0x1000],hitInfo[0x40],pose[0x100],jumpBuf[0x400],fakeVtable[16],trapWeb[0x100],tugWeb[0x100],polyLine[0x100],elecBuf[0x100],fakeObj[0x40],procBuf[0x40],msgs[3][0x40];
int* I(void*b,int o){return (int*)((unsigned char*)b+o);} short* S(void*b,int o){return (short*)((unsigned char*)b+o);}
void L(const std::string&s){events.push_back(s);}
int* getPos(int*out,int n){out[0]=nodes[n].x<<12;out[1]=nodes[n].y<<12;out[2]=nodes[n].z<<12;return out;}
short* getLinks(int n){return nodes[n].links;}
int groundH(int*p,int,int,void*){
 if(groundMode==0)return -1;
 if(groundMode==1)return groundParam<<12;
 int x=p[0]>>12,z=p[2]>>12;
 if(((x+z)&7)==5)return -1;
 return ((x&15)<<12)-(groundParam<<12);}
int lineOfSight(int*,int*,int*,int){int bit=losCalls<32?losCalls:31;losCalls++;return (losMask>>bit)&1;}
int rnd(int n){int v=R[(seed+used++)%R.size()];return n>0?v%n:0;}
unsigned char xa(int bank,int track,int*,int pri){L("X"+std::to_string(bank)+":"+std::to_string(track)+":"+std::to_string(pri));return 1;}
int sfx(unsigned s,int*,int f){L("S"+std::to_string(s)+":"+std::to_string(f));return 1;}
void TC runAnim(void*self,int a,int b,int c){L("A"+std::to_string(a)+":"+std::to_string(b)+":"+std::to_string(c));*(unsigned short*)((unsigned char*)self+0x12A)=a;}
int levelId(){return level;}
void TC flick(void*self){}
void TC focus(void*,void*t){}
void compass(int*p){L("C"+std::to_string(p[0])+":"+std::to_string(p[2]));}
void pulse(int n){L("P"+std::to_string(n));}
void TC die(void*,int a){L("D"+std::to_string(a));}
void TC synth(void*){L("Y");}
void* bitNew(unsigned){memset(elecBuf,0,sizeof elecBuf);return elecBuf;}
void TC outlineOn(void*self){*I(self,0x12C)|=4;}
void TC nop1(void*){}
void TC nop2(void*,void*){}
void nopC(){}
void TC nopPose(void*self,void*){if(!*I(self,0x188))*I(self,0x188)=(int)pose;}
// Web handles (CBaddy 0x104 trap, 0x10C tug).  Mem_RecoverPointer clears a dead handle like the original.
void* recoverPtr(int*h){
 if(!*h)return nullptr;
 bool trap=(h==I(venom,0x104));
 bool ok=trap?recT>=0:recG!=0;
 if(!ok){*h=0;return nullptr;}
 return trap?(void*)trapWeb:(void*)tugWeb;}
void TC burst(void*web){L(web==trapWeb?"B0":"B1");if(web==trapWeb)*I(venom,0x104)=0;else *I(venom,0x10C)=0;}
void memDelete(void*){}
void* dcNew(unsigned,int,int,int,int){return jumpBuf;}
void* classNew(unsigned){return procBuf;}
void TC procCtor(void*,void*,int code){L("Z"+std::to_string(code));}
void TC deleteObj(void*self,int){L("V");}
void TC bitCtor(void*){}
long long makeHandle(void*){return 1;}
void TC cleanMsgs(void*,int,int){}
void TC hitPlayer(void*self,void*info){L("H"+std::to_string(*(unsigned char*)info)+":"+std::to_string(*I(info,4))+":"+std::to_string(*(unsigned short*)((unsigned char*)info+8)));}
void initLine(int*info){
 int dx=info[3]-info[0],dy=info[4]-info[1],dz=info[5]-info[2];
 short sdx=(short)(dx>>12),sdy=(short)(dy>>12),sdz=(short)(dz>>12);
 info[16]=0x7FFFFFFF;info[26]=0;
 int horiz2=sdz*sdz+sdx*sdx;
 if(horiz2!=0){int full2=horiz2+sdy*sdy;info[17]=(int)std::sqrt((double)full2);}
 else info[17]=sdy>=0?sdy:-sdy;}
void lineToItem(int*info,int){if(lineHit&&info[17]>0){info[26]=1;info[16]=lineDist;}}
struct Field{const char*name;char buf;int off;int size;};
Field fields[]={
@FIELDS@
};
unsigned char* bufOf(char b){return b=='v'?venom:b=='p'?player:b=='h'?hitInfo:pose;}
void setField(const char*name,long long v){
 for(auto&f:fields)if(!strcmp(f.name,name)){unsigned char*p=bufOf(f.buf)+f.off;int s=f.size<0?-f.size:f.size;
  if(s==1)*p=(unsigned char)v;else if(s==2)*(unsigned short*)p=(unsigned short)v;else *(int*)p=(int)v;return;}
 std::cerr<<"unknown field "<<name<<std::endl;exit(2);}
long long getField(const char*name){
 for(auto&f:fields)if(!strcmp(f.name,name)){unsigned char*p=bufOf(f.buf)+f.off;
  if(f.size==1)return *p;if(f.size==-1)return *(signed char*)p;if(f.size==2)return *(unsigned short*)p;if(f.size==-2)return *(short*)p;return *(int*)p;}
 std::cerr<<"unknown field "<<name<<std::endl;exit(2);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 patch(0x4015B0,(void*)nullsub);patch(0x4E3940,(void*)getPos);patch(0x4E3880,(void*)getLinks);patch(0x4E6840,(void*)groundH);
 patch(0x4E67A0,(void*)lineOfSight);patch(0x4E5DA0,(void*)rnd);patch(0x479D30,(void*)xa);patch(0x471EA0,(void*)sfx);
 patch(0x460C10,(void*)runAnim);patch(0x4DE770,(void*)levelId);patch(0x4D1870,(void*)flick);patch(0x4B9E30,(void*)focus);
 patch(0x463800,(void*)compass);patch(0x4DFC20,(void*)pulse);patch(0x404320,(void*)die);patch(0x4E9B00,(void*)synth);
 patch(0x4088A0,(void*)bitNew);patch(0x460880,(void*)outlineOn);patch(0x4EBDD0,(void*)nop1);patch(0x475F50,(void*)nopC);
 patch(0x460E80,(void*)nopPose);patch(0x4F1040,(void*)nop1);
 patch(0x4583F0,(void*)recoverPtr);patch(0x4F8600,(void*)burst);patch(0x458210,(void*)memDelete);patch(0x4581A0,(void*)dcNew);
 patch(0x455390,(void*)classNew);patch(0x401E60,(void*)procCtor);patch(0x408B10,(void*)bitCtor);patch(0x458360,(void*)makeHandle);
 patch(0x403A90,(void*)cleanMsgs);patch(0x4524C0,(void*)initLine);patch(0x4549A0,(void*)lineToItem);
 // Neutralize every fs:0 SEH link/unlink in the venom module (Linux fs:0 is the TLS self pointer).
 for(unsigned char*q=(unsigned char*)0x4E9000;q<(unsigned char*)0x4F1100;q++){
  if(q[0]==0x64&&q[1]==0xA1&&!*(int*)(q+2)){q[0]=0x31;q[1]=0xC0;memset(q+2,0x90,4);}
  else if(q[0]==0x64&&(q[1]==0x89||q[1]==0x8B)&&(q[2]&0xC7)==0x05&&!*(int*)(q+3)){memset(q,0x90,7);}
 }
 std::memset((void*)0x4EC040,0x90,6);*(unsigned short*)0x4EC040=0xC031;std::memset((void*)0x4EC04E,0x90,7);std::memset((void*)0x4ECB22,0x90,7);
 // Sine table at 0x610C48 (interleaved sin/cos shorts), needed by CalculateJumpPositionArray.
 for(int i=0;i<4096;i++){double a=i*0.001536096911877394;*(short*)(0x610C48+4*i)=(short)(std::sin(a)*4096.0);*(short*)(0x610C48+4*i+2)=(short)(std::cos(a)*4096.0);}
 int nn;std::cin>>nn;for(int i=0;i<nn;i++){Node n{};int t,s;std::cin>>t>>s>>n.x>>n.y>>n.z;n.w[0]=t;n.w[1]=s;nodes.push_back(n);}
 for(auto&n:nodes)nodePtrs.push_back(n.w);
 *(void**)0x6B466C=nodePtrs.data();*(int*)0x6B4670=nn;*(void**)0x6A9038=player;
 int nr;std::cin>>nr;R.resize(nr);for(int&v:R)std::cin>>v;
 *(void**)fakeVtable=(void*)deleteObj;
 while(true){
  long long in[@NINPUTS@];
  if(!(std::cin>>in[0]))break;
  for(int i=1;i<@NINPUTS@;i++)std::cin>>in[i];
  const char*names[]={@NAMES@};
  memset(venom,0,sizeof venom);memset(player,0,sizeof player);memset(hitInfo,0,sizeof hitInfo);memset(pose,0,sizeof pose);
  memset(jumpBuf,0,sizeof jumpBuf);memset(trapWeb,0,sizeof trapWeb);memset(tugWeb,0,sizeof tugWeb);memset(polyLine,0,sizeof polyLine);
  memset(msgs,0,sizeof msgs);
  events.clear();used=0;losCalls=0;*(int*)0x6B4E58=0;
  int mode=0;long long pathSeed=0,poseOn=0,count=48;
  long long msgCodes[3]={0,0,0};
  for(int i=0;i<@NINPUTS@;i++){const char*n=names[i];long long v=in[i];
   if(!strcmp(n,"mode"))mode=v;else if(!strcmp(n,"seed"))seed=v;else if(!strcmp(n,"level"))level=v;
   else if(!strcmp(n,"difficulty"))*(int*)0x54D474=v;else if(!strcmp(n,"timer"))*(int*)0x6B4CA8=v;
   else if(!strcmp(n,"losMask"))losMask=(int)v;else if(!strcmp(n,"groundMode"))groundMode=v;else if(!strcmp(n,"groundParam"))groundParam=v;
   else if(!strcmp(n,"recT"))recT=v;else if(!strcmp(n,"recG"))recG=v;else if(!strcmp(n,"lineHit"))lineHit=v;else if(!strcmp(n,"lineDist"))lineDist=v;
   else if(!strcmp(n,"count"))count=v;else if(!strcmp(n,"pathSeed"))pathSeed=v;else if(!strcmp(n,"poseOn"))poseOn=v;
   else if(!strcmp(n,"m0"))msgCodes[0]=v;else if(!strcmp(n,"m1"))msgCodes[1]=v;else if(!strcmp(n,"m2"))msgCodes[2]=v;
   else setField(n,v);}
  *S(venom,0x21E)=100;
  if(poseOn)*I(venom,0x188)=(int)pose;
  // Web handles: nonzero means held; recoverable objects carry the hook count behind +68/+60.
  *I(trapWeb,68)=(int)polyLine;*I(polyLine,60)=recT;
  if(*I(venom,0x428))*I(venom,0x428)=(int)fakeObj;
  *(void**)fakeObj=fakeVtable;
  if(*I(venom,0x334))*I(venom,0x334)=(int)fakeObj;
  if(*I(venom,0x38C)>0&&mode!=9){for(int i=0;i<*I(venom,0x38C);i++){I(jumpBuf,12*i)[0]=(int)(i*100+pathSeed);I(jumpBuf,12*i)[1]=i*7-50;I(jumpBuf,12*i)[2]=13*i;}*I(venom,0x390)=(int)jumpBuf;}
  *(void**)player=(void*)fakeVtable; // vtable pointer for slot 3 below
  void*vt[8]={0,0,0,(void*)hitPlayer,0,0,0,0};*(void**)player=vt;
  // Message list: head at 0x290, flags at +16, next at +8, code at +20.
  void*prev=nullptr;
  for(int i=2;i>=0;i--){if(msgCodes[i]==0)continue;unsigned char*m=msgs[i];*I(m,20)=(int)msgCodes[i];*I(m,8)=(int)prev;prev=m;}
  *I(venom,0x290)=(int)prev;
  long long ret=0;
  if(mode==0)reinterpret_cast<void(TC*)(void*)>(0x4EE460)(venom);
  else if(mode==1)reinterpret_cast<void(TC*)(void*)>(0x4ED410)(venom);
  else if(mode==2)reinterpret_cast<void(TC*)(void*)>(0x4EF5A0)(venom);
  else if(mode==3)reinterpret_cast<void(TC*)(void*)>(0x4ED210)(venom);
  else if(mode==4)reinterpret_cast<void(TC*)(void*)>(0x4ECF40)(venom);
  else if(mode==5)reinterpret_cast<void(TC*)(void*)>(0x4ED330)(venom);
  else if(mode==6)ret=reinterpret_cast<int(TC*)(void*,void*)>(0x4EB7F0)(venom,hitInfo);
  else if(mode==7)reinterpret_cast<void(TC*)(void*)>(0x4EC040)(venom);
  else if(mode==8)ret=reinterpret_cast<int(TC*)(void*)>(0x4ECC60)(venom);
  else if(mode==9)reinterpret_cast<int(TC*)(void*,void*,int)>(0x4ECD80)(venom,I(venom,0x3A0),(int)count);
  else if(mode==10)reinterpret_cast<int(TC*)(void*)>(0x4EBCA0)(venom);
  else if(mode==11)ret=(long long)reinterpret_cast<unsigned(TC*)(void*)>(0x4EF440)(venom);
  std::cout<<"{\"s\":[";
  const char*outs[]={@OUTS@};
  for(int i=0;i<@NOUTS@;i++){const char*n=outs[i];if(i)std::cout<<',';
   if(!strcmp(n,"ret"))std::cout<<(int)ret;else if(!strcmp(n,"used"))std::cout<<used;
   else if(!strcmp(n,"msg0"))std::cout<<(int)(msgs[0][16]);else if(!strcmp(n,"msg1"))std::cout<<(int)(msgs[1][16]);else if(!strcmp(n,"msg2"))std::cout<<(int)(msgs[2][16]);
   else if(!strcmp(n,"pathFreed"))std::cout<<(*I(venom,0x390)==0?1:0);
   else if(!strcmp(n,"webObj"))std::cout<<(*I(venom,0x428)!=0?1:0);
   else if(!strcmp(n,"elec"))std::cout<<(*I(venom,0x334)!=0?1:0);
   else if(!strcmp(n,"webT"))std::cout<<(*I(venom,0x104)!=0?1:0);
   else if(!strcmp(n,"webG"))std::cout<<(*I(venom,0x10C)!=0?1:0);
   else std::cout<<getField(n);}
  // Jump path as written by CalculateJumpPositionArray (count <= 48).
  std::cout<<"],\"path\":[";
  if(mode==9){int c=(int)count;for(int i=0;i<c*3&&i<144;i++){if(i)std::cout<<',';std::cout<<I(jumpBuf,0)[i];}}
  std::cout<<"],\"log\":[";
  for(size_t i=0;i<events.size();i++)std::cout<<(i?",":"")<<'"'<<events[i]<<'"';
  std::cout<<"]}"<<std::endl;}
}
'''


def build_cpp():
    names = ','.join(f'"{n}"' for n in INPUTS)
    outs = ','.join(f'"{n}"' for n in OUTPUTS)
    fields = table_cpp(MEM.keys(), False)
    return (CPP_TEMPLATE.replace('@FIELDS@', fields).replace('@NINPUTS@', str(len(INPUTS)))
            .replace('@NAMES@', names).replace('@OUTS@', outs).replace('@NOUTS@', str(len(OUTPUTS))))


def base(mode):
    c = dict.fromkeys(INPUTS, 0)
    c.update(mode=mode, level=0x604, ticks=2, difficulty=2, timer=5000, losMask=0xFFFFFFFF, groundMode=1, groundParam=0,
             minor=0xC0, health=600, count=48, pAction=1, pHealth=300, f33D=1, f39C=50, f3E0=4800,
             hDmg=20, hFlags=2, hKind=1, recT=-1, item=0x480, body=0x216, f218=0, ay=1000, seed=0)
    return c


def player_ring(rng, c, near):
    c['px'], c['py'], c['pz'] = rng.randrange(-300, 300) << 12, 0, rng.randrange(-300, 300) << 12
    d = rng.choice(near)
    ang = rng.random() * 6.2831853
    import math
    c['qx'] = c['px'] + (int(d * math.sin(ang)) << 12)
    c['qz'] = c['pz'] + (int(d * math.cos(ang)) << 12)
    c['qy'] = rng.choice([0, 0, 0, 40 << 12, -(30 << 12)])


def cases(rng):
    out = []
    # EnterFightMode: timer window, player state, distance bands, LOS/ground combos.
    for _ in range(260):
        c = base(0)
        c['ai'] = 32
        c['sub'] = rng.choice([0, 0, 0, 1])
        c['timer'] = 5000
        c['f3E0'] = rng.choice([4800, 4800, 4800, 3000, 4500, 5000])
        c['pAction'] = rng.choice([1, 1, 1, 0x800000, 0x20000000, 2])
        c['p36C'] = rng.choice([0, 0, 0, 1])
        c['pHealth'] = rng.choice([300, 300, 300, 0, -5])
        c['pCrawl'] = rng.choice([0, 0, 0, 1])
        c['p8E8'] = rng.choice([0, 0, 0, 1])
        c['p8E9'] = rng.choice([0, 0, 0, 1])
        player_ring(rng, c, [50, 200, 255, 256, 300, 700, 1020, 1030, 1500, 2040, 2100, 3000])
        c['f39C'] = rng.choice([20, 60, 70])
        c['losMask'] = rng.randrange(1 << 8)
        c['groundMode'] = rng.choice([0, 1, 2, 2])
        c['groundParam'] = rng.choice([0, 30, -30, 100])
        c['seed'] = rng.randrange(64)
        c['f218'] = rng.choice([0, 1, 3, 0x80])
        c['vx'], c['vy'], c['vz'] = rng.choice([(0, 0, 0), (5, 6, 7)])
        out.append(c)
    # Scan and jump arc.
    for _ in range(60):
        c = base(8)
        player_ring(rng, c, [300, 600, 1500, 2500, 4000])
        c['groundMode'] = rng.choice([1, 2, 2])
        c['groundParam'] = rng.choice([0, 30, -30])
        out.append(c)
    for count in (48, 48, 48, 10, 33, 2, 5):
        for _ in range(8):
            c = base(9)
            c['px'], c['py'], c['pz'] = rng.randrange(-300, 300) << 12, rng.randrange(-30, 30) << 12, rng.randrange(-300, 300) << 12
            c['jx'], c['jy'], c['jz'] = rng.randrange(-3000, 3000) << 12, rng.randrange(-60, 60) << 12, rng.randrange(-3000, 3000) << 12
            c['count'] = count
            out.append(c)
    for sub in (0, 1, 2, 3):
        for _ in range(26):
            c = base(1)
            c['ai'] = 16
            c['sub'] = sub
            c['anim'] = rng.choice([7, 7, 5, 12])
            c['frame'] = rng.choice([0, 3, 4, 14, 20, 40])
            c['finished'] = rng.choice([0, 1])
            c['f218'] = rng.choice([0, 2, 3])
            c['ticks'] = rng.choice([1, 2, 4, 9, 70])
            c['jumpCount'] = 48
            c['jumpIdx'] = rng.choice([0, 10, 30, 46, 47])
            c['pathSeed'] = rng.randrange(1000)
            c['retAi'], c['retSub'] = rng.choice([(32, 0), (1, 1), (64, 2)])
            c['body'] = rng.choice([0x216, 0x206])
            c['minor'] = rng.choice([0, 0xC0])
            c['px'], c['py'], c['pz'] = 5 << 12, 6 << 12, 7 << 12
            out.append(c)
    # GetWithinAttackRange and its point finder.
    for sub in (0, 1, 2):
        for _ in range(60):
            c = base(2)
            c['ai'] = 128
            c['sub'] = sub
            player_ring(rng, c, [100, 150, 191, 192, 500, 1000, 1280, 1281, 2000])
            c['tx'], c['ty'], c['tz'] = c['px'] + (rng.randrange(-100, 100) << 12), c['py'], c['pz'] + (rng.randrange(-100, 100) << 12)
            c['ticks'] = rng.choice([1, 2, 6, 20])
            c['losMask'] = rng.randrange(1 << 8)
            c['groundMode'] = rng.choice([0, 1, 2, 2])
            c['groundParam'] = rng.choice([0, 30, -30, 100])
            c['f218'] = rng.choice([0, 1])
            c['seed'] = rng.randrange(64)
            out.append(c)
    for _ in range(70):
        c = base(11)
        player_ring(rng, c, [100, 500, 1000])
        c['losMask'] = rng.randrange(1 << 8)
        c['groundMode'] = rng.choice([1, 2, 2])
        c['groundParam'] = rng.choice([0, 30, -30, 100])
        c['seed'] = rng.randrange(64)
        out.append(c)
    # CVenom::Hit: guards, damage windows, slide direction and kind.
    for _ in range(300):
        c = base(6)
        c['ai'] = rng.choice([1, 32, 64, 128, 512, 2048])
        c['sub'] = rng.choice([0, 1, 2])
        c['level'] = rng.choice([0x604, 0x604, 0x603, 0x502, 0x501])
        c['minor'] = rng.choice([0xC0, 0xC0, 0xC0, 0])
        c['health'] = rng.choice([600, 600, 100, 20, 5, -3])
        c['item'] = rng.choice([0x480, 0x480, 0x481])
        c['f218'] = rng.choice([0, 0, 0x4000, 0x80, 0x100, 0x3F, 0x180])
        c['anim'] = rng.choice([5, 17, 18, 34, 12])
        c['frame'] = rng.choice([0, 2, 3, 8, 9, 14, 15, 40])
        c['f368'] = rng.choice([0, 20, 70, 95, 99])
        c['f374'] = c['timer'] - rng.choice([0, 5, 19, 20, 21, 39, 40, 41, 59, 60, 61])
        c['difficulty'] = rng.choice([0, 1, 2, 3])
        c['stretch'] = rng.choice([0, 0, 7])
        c['webT'] = rng.choice([0, 1])
        c['recT'] = rng.choice([-1, 3])
        c['hFlags'] = rng.choice([0, 2, 8, 10, 26, 24, 18, 14])
        c['hKind'] = rng.choice([0, 1, 2, 3])
        c['hDmg'] = rng.choice([1, 5, 20, 80, 120, 700])
        c['hdx'], c['hdy'], c['hdz'] = rng.choice([(4096, 0, 0), (0, 0, 4096), (-4096, 0, 0), (0, 0, -4096), (3000, 100, -3000), (1, 2, 3)])
        c['hSlideD'] = rng.choice([0, 100, 129, 300, -50])
        c['hSlideF'] = rng.choice([4, 8, 16, 20])
        c['ay'] = rng.randrange(4096)
        c['px'], c['py'], c['pz'] = 10 << 12, 3 << 12, 20 << 12
        c['lineHit'] = rng.choice([0, 1, 1])
        c['lineDist'] = rng.choice([100, 256, 257, 300, 1000, 5000])
        out.append(c)
    for _ in range(40):
        c = base(10)
        c['px'], c['py'], c['pz'] = 10 << 12, 3 << 12, 20 << 12
        c['sx'], c['sy'], c['sz'] = rng.choice([(4096, 0, 0), (0, 4096, 4096), (-4000, 100, 50), (0, 0, 0)])
        c['slide'] = rng.choice([4, 8, 16, 20, 100])
        c['lineHit'] = rng.choice([0, 1, 1])
        c['lineDist'] = rng.choice([100, 256, 257, 300, 1000, 5000])
        out.append(c)
    # TakeHit.
    for sub in (0, 1, 2):
        for _ in range(40):
            c = base(3)
            c['ai'] = 2048
            c['sub'] = sub
            c['webObj'] = rng.choice([0, 1])
            c['f218'] = rng.choice([0, 8, 0x10, 0x20, 0x38, 0x18])
            c['slide'] = rng.choice([0, 1, 2, 5, 40, -3])
            c['ticks'] = rng.choice([1, 2, 4])
            c['sx'], c['sy'], c['sz'] = rng.choice([(100, 0, 200), (-4096, 5, 9)])
            c['finished'] = rng.choice([0, 1])
            c['f36C'] = rng.choice([0, 1, 4, 64])
            c['px'], c['py'], c['pz'] = 10 << 12, 3 << 12, 20 << 12
            out.append(c)
    # VenomDie.
    for sub in (0, 1, 2, 3):
        for _ in range(14):
            c = base(5)
            c['ai'] = 4096
            c['sub'] = sub
            c['level'] = rng.choice([0x604, 0x603, 0x502])
            c['finished'] = rng.choice([0, 1])
            c['f218'] = rng.choice([0, 0x1FF, 0xFFFF])
            c['body'] = rng.choice([0x216, 0x206])
            c['item'] = rng.choice([0x480, 0x4C1])
            out.append(c)
    # GetTrapped.
    for sub in (0, 1, 2, 3, 4):
        for _ in range(30):
            c = base(4)
            c['ai'] = 1024
            c['sub'] = sub
            c['anim'] = rng.choice([31, 32, 33, 12, 0x1F])
            c['finished'] = rng.choice([0, 1])
            c['trapCtr'] = rng.choice([0, 1, 2, 16, -1])
            c['retSub'] = rng.choice([0, 3, 5])
            c['webT'] = rng.choice([0, 1, 1])
            c['recT'] = rng.choice([-1, 0, 3, 5])
            c['stretch'] = rng.choice([0, 0, 9])
            c['elec'] = rng.choice([0, 1])
            c['item'] = rng.choice([0x480, 0x484])
            out.append(c)
    # CVenom::AI: message loop, state 0x2000 and 0x8000, dispatch into the new leaves, goo timer.
    for _ in range(220):
        c = base(7)
        c['ai'] = rng.choice([1024, 0x2000, 0x8000, 32, 16, 128, 2048, 4096, 1024, 0x2000, 32])
        c['sub'] = rng.choice([0, 0, 1, 2, 3])
        c['level'] = rng.choice([0x604, 0x603, 0x502])
        c['m0'], c['m1'], c['m2'] = rng.choice([(0, 0, 0), (5, 0, 0), (6, 0, 0), (14, 0, 0), (15, 0, 0), (5, 6, 0), (6, 14, 15), (7, 5, 0),
                                                (14, 5, 15), (5, 5, 0), (0, 0, 0)])
        c['webT'] = rng.choice([0, 1])
        c['webG'] = rng.choice([0, 1])
        c['recT'] = rng.choice([-1, 3])
        c['recG'] = rng.choice([0, 1])
        c['f218'] = rng.choice([0, 0x200, 0x8, 0x200, 0x208, 0x10])
        c['trapCtr'] = rng.choice([0, 9])
        c['finished'] = rng.choice([0, 1])
        c['anim'] = rng.choice([5, 8, 12, 31, 32, 33, 40])
        c['frame'] = rng.choice([2, 8, 20])
        c['f330'] = rng.choice([0, 0, 5, 40])
        c['elec'] = rng.choice([0, 1])
        c['poseOn'] = 1
        c['item'] = rng.choice([0x480, 0x480, 0x484])
        c['f324'] = rng.choice([0, 3])
        c['f3E0'] = rng.choice([4800, 4000])
        c['pAction'] = rng.choice([1, 0x800000])
        c['stretch'] = 0
        player_ring(rng, c, [50, 400, 700, 1500, 2500])
        c['losMask'] = rng.randrange(1 << 8)
        c['groundMode'] = rng.choice([0, 1, 2])
        c['groundParam'] = rng.choice([0, 30])
        c['seed'] = rng.randrange(64)
        c['ticks'] = rng.choice([1, 2, 3])
        c['jumpCount'] = 48
        c['pathSeed'] = rng.randrange(100)
        c['retAi'], c['retSub'] = rng.choice([(32, 0), (1, 1)])
        out.append(c)
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x313C0)
    rows = cases(rng)
    with tempfile.TemporaryDirectory(prefix='openspidey-venom-combat-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(build_cpp())
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-w',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), '-o', str(binary)], check=True)
        payload = f'{len(NODES)}\n' + '\n'.join(' '.join(map(str, n)) for n in NODES) + '\n'
        payload += f'{len(RANDOM)} ' + ' '.join(map(str, RANDOM)) + '\n'
        payload += '\n'.join(' '.join(str(c[k]) for k in INPUTS) for c in rows) + '\n'
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=payload.encode())
    lines = result.decode().splitlines()
    assert len(lines) == len(rows), (len(lines), len(rows))
    fixture = dict(nodes=NODES, random=RANDOM, inputs=INPUTS, outputs=OUTPUTS,
                   cases=[dict(input=[c[k] for k in INPUTS], **json.loads(line)) for c, line in zip(rows, lines)])
    path = root / 'modern/OpenSpidey.Tests/Fixtures/venom-combat.json'
    path.write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    counts = {}
    for c in rows:
        counts[c['mode']] = counts.get(c['mode'], 0) + 1
    print(f'Captured {len(rows)} CVenom combat cases by mode {counts}.')


if __name__ == '__main__':
    main()
