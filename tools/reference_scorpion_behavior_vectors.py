"""Capture whole PC Scorpion GetTugged486C00, WindUp484A90, ReactToJonah487CD0, AvoidObstacle484C60, Die487B80,
PursueTarget485120 and FollowWaypoints484240 with their483E80/4840D0/484800/484EF0 charge, wall and jump helpers.

Only external transport is replaced: CClass_new and the CAIProc constructors, animation, Rnd, speech,
sounds, weak-handle recovery, the web burst, CBaddy RunAppropriateAnim/PathCheck, Trig_GetPosition,
the pulse link list, player input lock, camera setters, DetermineTarget, DoPhysics, LineToItem, Mem_MakeHandle,
script/waypoint/link/trigger-node/chunk services and the diagnostic sink. Neutralize, ScorpPathCheck,
GetChaseWaypoint487E20, MoveAnywhere486800, the Jonah list walk, VectorNormal, CalcAim, CVector::Length,
InitLineInfo, the death-pulse gate4048B0 and all CVector math run unchanged.
"""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


FIELDS = ['method', 'state', 'substate', 'counter', 'behavior', 'completion', 'reaction', 'signals', 'field310', 'bodyFlags',
          'pulseSent', 'sourceNode', 'pathCount', 'finished', 'frame', 'idleByte', 'tugPointer', 'tugValidation', 'tugValid',
          'lastWindUp', 'quipSuppression', 'rand0', 'rand1', 'speechAccepted', 'allocAccepted', 'radius', 'standingY', 'phase',
          'px', 'py', 'pz', 'vx', 'vy', 'vz', 'ax', 'ay', 'az', 'lx', 'ly', 'lz', 'avx', 'avy', 'avz', 'aax', 'aay', 'aaz',
          'waypoint', 'wpX', 'wpY', 'wpZ', 'envX', 'envY', 'envZ', 'obsX', 'obsY', 'obsZ', 'obwX', 'obwY', 'obwZ',
          'axisX', 'axisY', 'axisZ', 'sideX', 'sideY', 'sideZ', 'hasCamera', 'cameraAngle', 'elapsed',
          'cachedJonah', 'hasJonah', 'jonahX', 'jonahY', 'jonahZ', 'jonahRadius',
          'targetKind', 'determineKind', 'target', 'targetSignals', 'crawling', 'losPlayer', 'playerX', 'playerY', 'playerZ',
          'bodyX', 'bodyY', 'bodyZ', 'bodyType', 'attackClock', 'baddyCheck', 'angleY', 'movementByte', 'playerTargetUpdates',
          'clearance', 'chargeIndex', 'chargeX', 'chargeY', 'chargeZ', 'startX', 'startY', 'startZ', 'chargeAttempts', 'checksum',
          'physicsX', 'physicsZ', 'lineItem', 'lineFace', 'linkCount', 'linkNode', 'nextWaypoint', 'breakWall', 'squirtOffset',
          'word0', 'word1', 'word2', 'word3', 'pt1X', 'pt1Y', 'pt1Z', 'pt2X', 'pt2Y', 'pt2Z', 'pt3X', 'pt3Y', 'pt3Z']
PATHS = 4

HARNESS = r'''
#include "original_executable.h"
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <vector>
struct Vector{int x,y,z;};
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
template<class T>T&mem(unsigned n){return *reinterpret_cast<T*>(n);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
// Linux has no Windows SEH chain: mov eax,fs:[0] becomes xor eax,eax; stores to fs:[0] become nops.
void stripSeh(unsigned from,unsigned to){for(auto*p=(unsigned char*)from;p<(unsigned char*)to;p++){
 if(!std::memcmp(p,"\x64\xA1\0\0\0\0",6)){std::memcpy(p,"\x31\xC0\x90\x90\x90\x90",6);p+=5;}
 else if(p[0]==0x64&&p[1]==0x89&&(p[2]==0x25||p[2]==0x0D)&&!std::memcmp(p+3,"\0\0\0\0",4)){std::memset(p,0x90,7);p+=6;}}}
enum Field{GENERATED_FIELDS,FieldCount};long long input[FieldCount];
struct Path{int result;Vector contact;};Path paths[PATH_COUNT];int pathCalls,randomCalls;
alignas(4)unsigned char actor[0xC28],player[0xF20],jonah[0x380],middle[0x400],web[0x100],camera[0x300],block[0x100],links[16],body[0x400],nodeData[2][8],squirt[8];void*trigNodes[512];
std::vector<std::vector<long long>>events;
int identity(void*p){if(p==player)return 1;if(p==jonah)return 2;if(p==body)return 3;if(p==web)return 4;if(p==actor)return 5;return p?9:0;}
void*pointer(int id){if(id==1)return player;if(id==2)return jonah;if(id==3)return body;return nullptr;}
void append(std::vector<long long>&v,Vector p){v.insert(v.end(),{p.x,p.y,p.z});}
std::vector<long long>snapshot(){std::vector<long long>v={at<int>(actor,0x31C),at<int>(actor,0x320),at<int>(actor,0x1F8),at<unsigned>(actor,0x2A8),at<unsigned>(actor,0x288),
 at<unsigned>(actor,0x324),at<unsigned>(actor,0x218),at<int>(actor,0x310),at<unsigned short>(actor,0x46),at<unsigned char>(actor,0x211),at<int>(actor,0x1F0),
 identity(at<void*>(actor,0x10C)),at<unsigned>(actor,0x110),at<int>(actor,0xBE4),identity(at<void*>(actor,0xBEC)),at<unsigned char>(actor,0x21D),
 at<unsigned char>(camera,0x100),identity(at<void*>(camera,0xFC)),at<unsigned char>(camera,0x140),identity(at<void*>(camera,0x13C)),
 at<int>(actor,0x384),at<int>(actor,0x3A8),at<unsigned>(actor,0xBFC),at<int>(actor,0xBF8),identity(at<void*>(actor,0xBF0)),at<unsigned>(actor,0xBF4),at<int>(actor,0xC10),mem<int>(0x5FBEE0),at<int>(actor,0x1F4)};
 for(unsigned o:{0x1B4u,8u,0x60u,0x6Cu,0x27Cu,0x354u,0x360u,0x36Cu,0x378u,0x2FCu,0xC00u,0x1C0u,0x1CCu})append(v,at<Vector>(actor,o));
 for(unsigned o:{0x88u,0x8Eu})for(int i=0;i<3;i++)v.push_back(at<short>(actor,o+i*2));return v;}
void event(int kind,std::initializer_list<long long>args={}){std::vector<long long>v={kind};v.insert(v.end(),args);auto s=snapshot();v.insert(v.end(),s.begin(),s.end());events.push_back(v);}
int rnd(int bound){assert(randomCalls<2);int r=input[rand0+randomCalls++];assert(r>=0&&r<bound);event(1,{bound,r});return r;}
void*allocate(unsigned n){if(!input[allocAccepted]){event(2,{n});return nullptr;}std::memset(block,0,sizeof(block));return block;}
void*__attribute__((thiscall))lookAt(void*p,void*baddy,void*body,int*point,int mask,int rate,int tolerance){assert(p==block&&baddy==actor);
 event(3,{identity(body),point?1:0,point?point[0]:0,point?point[1]:0,point?point[2]:0,mask,rate,tolerance});return p;}
void*__attribute__((thiscall))stateSwitch(void*p,void*baddy,int code){assert(p==block&&baddy==actor);event(4,{code});return p;}
void __attribute__((thiscall))run(void*p,int clip,int from,int to){assert(p==actor);event(5,{clip,from,to});}
void __attribute__((thiscall))cycle(void*p,int clip,int direction){assert(p==actor);event(6,{clip,(signed char)direction});}
void sound(unsigned code,int*position,int pitch){assert(position==(int*)(actor+8));event(7,{code,pitch});}
unsigned char speech(int bank,int slot,int*position,int priority){assert(position==(int*)(actor+8));event(8,{bank,slot,priority});return input[speechAccepted]!=0;}
void __attribute__((thiscall))attachSpeech(void*p,int bank,int slot){assert(p==actor);event(9,{bank,slot});}
void __attribute__((thiscall))stopSpeech(void*p){assert(p==actor);event(10);}
void*recover(void*handle){assert(handle==actor+0x10C);event(11);if(!input[tugValid]){at<void*>(handle,0)=nullptr;return nullptr;}return at<void*>(handle,0);}
void __attribute__((thiscall))burst(void*p){event(12,{identity(p)});}
void __attribute__((thiscall))appropriate(void*p){assert(p==actor);event(13);}
int*position(int*out,int node){event(14,{node});out[0]=input[wpX];out[1]=input[wpY];out[2]=input[wpZ];return out;}
int __attribute__((thiscall))path(void*p,int*start,int*end,int*contact,int clearance){assert(p==actor&&pathCalls<PATH_COUNT);
 event(15,{start[0],start[1],start[2],end[0],end[1],end[2],clearance});if(contact)*reinterpret_cast<Vector*>(contact)=paths[pathCalls].contact;return paths[pathCalls++].result;}
void __attribute__((thiscall))ignore(void*p,int ticks){assert(p==player);event(16,{ticks});}
void*linksFor(int node){event(17,{node});return links;}
void pulse(void*p){assert(p==links);}
int __attribute__((thiscall))setMode(void*p,int mode){assert(p==camera);event(18,{mode});return 1;}
void __attribute__((thiscall))angle(void*p,int a,int t){assert(p==camera);event(19,{(short)a,(unsigned short)t});}
void __attribute__((thiscall))xOffset(void*p,int a,int t){assert(p==camera);event(20,{(short)a,(unsigned short)t});}
void __attribute__((thiscall))yOffset(void*p,int a,int t){assert(p==camera);event(21,{(short)a,(unsigned short)t});}
void __attribute__((thiscall))zOffset(void*p,int a,int t){assert(p==camera);event(22,{(short)a,(unsigned short)t});}
void __attribute__((thiscall))xzDistance(void*p,int a,int t){assert(p==camera);event(23,{(unsigned short)a,(unsigned short)t});}
void __attribute__((thiscall))yDistance(void*p,int a,int t){assert(p==camera);event(24,{(short)a,(unsigned short)t});}
void __attribute__((thiscall))determine(void*p){assert(p==actor);event(26);if(input[determineKind]>=0)at<int>(actor,0xBF8)=input[determineKind];}
int visible(int*start,int*end,int flags,int argument){assert(start==(int*)(actor+8)&&flags==0&&argument==0);event(27,{end[0],end[1],end[2]});return input[losPlayer]!=0;}
void __attribute__((thiscall))physics(void*p,int limit){assert(p==actor);event(28,{limit});at<int>(actor,8)+=input[physicsX];at<int>(actor,16)+=input[physicsZ];}
void lineToItem(int*line,int mode){event(29,{line[0],line[1],line[2],line[3],line[4],line[5],line[6],line[7],line[8],line[9],line[10],line[11],mode});
 at<void*>(line,0x68)=pointer(input[lineItem]);at<void*>(line,0x80)=input[lineFace]?(void*)squirt:nullptr;}
unsigned long long makeHandle(void*p){event(30,{identity(p)});return ((unsigned long long)0x9999<<32)|(unsigned)p;}
void*__attribute__((thiscall))rotY(void*p,void*baddy,int angle,int rate,int mask){assert(p==block&&baddy==actor);event(31,{angle,rate,mask});return p;}
void*__attribute__((thiscall))accZ(void*p,void*baddy,int a,int b,int c){assert(p==block&&baddy==actor);event(32,{a,b,c});return p;}
void*__attribute__((thiscall))moveTo(void*p,void*baddy,int*info,int mask){assert(p==block&&baddy==actor);event(33,{info[0],info[1],info[2],info[3],info[4],info[5],mask});return p;}
void __attribute__((thiscall))script(void*p){assert(p==actor);event(34);}
int __attribute__((thiscall))followingWaypoint(void*p){assert(p==actor);event(35);return input[nextWaypoint];}
int readLinks(int node,int*out,int maximum){event(36,{node,maximum});if(input[linkCount])out[0]=input[linkNode];return input[linkCount];}
unsigned short*__attribute__((thiscall))squirtAngles(void*p,unsigned short*data){assert(p==actor);event(37);return (unsigned short*)(squirt+input[squirtOffset]);}
void chunk(unsigned checksum){event(38,{checksum});}
void diagnostic(int condition,const char*message){if((condition&255)!=0)return;
 int id=!strcmp(message,"Unknown substate.")?0:!strcmp(message,"Unknown substate!")?1:!strcmp(message,"Stuck again. Emergency fix required.")?2:!strcmp(message,"Unknown method.")?3:
 !strcmp(message,"Not detecting baddy polys!")?4:!strcmp(message,"No checksum in ID_BREAK_WALL type scriptpoint!")?5:!strcmp(message,"No link through wall to next room!")?6:
 !strcmp(message,"No link through wall into next room!")?7:!strcmp(message,"Don't collide with yourself idiot.")?8:!strcmp(message,"Don't collide with manipobs please.")?9:-1;assert(id>=0);event(25,{id});}
void numbers(const std::vector<long long>&v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 for(int i=0;i<4096;i++){mem<short>(0x610C48+4*i)=(short)(std::sin(i*0.001536096911877394)*4096);mem<short>(0x610C4A+4*i)=(short)(std::cos(i*0.001536096911877394)*4096);}
 patch(0x4E5DA0,(void*)&rnd);patch(0x455390,(void*)&allocate);patch(0x401180,(void*)&lookAt);patch(0x401E60,(void*)&stateSwitch);patch(0x460C10,(void*)&run);
 patch(0x460D00,(void*)&cycle);patch(0x471EA0,(void*)&sound);patch(0x479D30,(void*)&speech);patch(0x4603D0,(void*)&attachSpeech);patch(0x460400,(void*)&stopSpeech);
 patch(0x4583F0,(void*)&recover);patch(0x4F8600,(void*)&burst);patch(0x404B60,(void*)&appropriate);patch(0x4E3940,(void*)&position);patch(0x403310,(void*)&path);
 patch(0x4C30A0,(void*)&ignore);patch(0x4E3880,(void*)&linksFor);patch(0x4DFD30,(void*)&pulse);patch(0x4167F0,(void*)&setMode);patch(0x4178E0,(void*)&angle);
 patch(0x417AE0,(void*)&xOffset);patch(0x417B60,(void*)&yOffset);patch(0x417BE0,(void*)&zOffset);patch(0x4179F0,(void*)&xzDistance);patch(0x417A70,(void*)&yDistance);
 patch(0x4015B0,(void*)&diagnostic);stripSeh(0x483800,0x489000);
 patch(0x488140,(void*)&determine);patch(0x4E67A0,(void*)&visible);patch(0x404C50,(void*)&physics);patch(0x4549A0,(void*)&lineToItem);patch(0x458360,(void*)&makeHandle);
 patch(0x401060,(void*)&rotY);patch(0x4016B0,(void*)&accZ);patch(0x4018D0,(void*)&moveTo);patch(0x402E10,(void*)&script);patch(0x402F60,(void*)&followingWaypoint);
 patch(0x4DFE70,(void*)&readLinks);patch(0x4603A0,(void*)&squirtAngles);patch(0x4273D0,(void*)&chunk);mem<void**>(0x6B466C)=trigNodes;
 unsigned methods[]={0x486C00,0x484A90,0x487CD0,0x484C60,0x487B80,0x485120,0x484240};
 while(std::cin>>input[0]){for(int i=1;i<FieldCount;i++)std::cin>>input[i];for(auto&q:paths)std::cin>>q.result>>q.contact.x>>q.contact.y>>q.contact.z;
  for(void*p:std::initializer_list<void*>{actor,player,jonah,middle,web,camera,body})std::memset(p,0,p==actor?sizeof(actor):p==player?sizeof(player):p==jonah?sizeof(jonah):p==middle?sizeof(middle):p==web?sizeof(web):p==camera?sizeof(camera):sizeof(body));
  events.clear();pathCalls=0;randomCalls=0;
  at<int>(actor,0x31C)=input[state];at<int>(actor,0x320)=input[substate];at<int>(actor,0x1F8)=input[counter];at<unsigned>(actor,0x2A8)=input[behavior];
  at<unsigned>(actor,0x288)=input[completion];at<unsigned>(actor,0x324)=input[reaction];at<unsigned>(actor,0x218)=input[signals];at<int>(actor,0x310)=input[field310];
  at<unsigned short>(actor,0x46)=input[bodyFlags];at<unsigned char>(actor,0x211)=input[pulseSent];at<unsigned short>(actor,0xDE)=input[sourceNode];at<int>(actor,0x1F0)=input[pathCount];
  at<unsigned char>(actor,0x142)=input[finished];at<short>(actor,0x128)=input[frame];at<unsigned char>(actor,0x298)=input[idleByte];
  at<void*>(actor,0x10C)=input[tugPointer]?web:nullptr;at<unsigned>(actor,0x110)=input[tugValidation];at<int>(actor,0xBE4)=input[lastWindUp];at<int>(actor,0xC20)=input[quipSuppression];
  at<unsigned short>(actor,0xDC)=input[radius];at<int>(actor,0x29C)=input[standingY];at<unsigned char>(actor,0x21D)=input[phase];at<short>(actor,0x21E)=100;
  at<Vector>(actor,8)={int(input[px]),int(input[py]),int(input[pz])};at<Vector>(actor,0x60)={int(input[vx]),int(input[vy]),int(input[vz])};
  at<Vector>(actor,0x6C)={int(input[ax]),int(input[ay]),int(input[az])};at<Vector>(actor,0x27C)={int(input[lx]),int(input[ly]),int(input[lz])};
  for(int i=0;i<3;i++){at<short>(actor,0x88+i*2)=input[avx+i];at<short>(actor,i*2+0x8E)=input[aax+i];}
  at<int>(actor,0x1F4)=input[waypoint];at<Vector>(actor,0xC00)={int(input[envX]),int(input[envY]),int(input[envZ])};
  at<Vector>(actor,0x33C)={int(input[obsX]),int(input[obsY]),int(input[obsZ])};at<Vector>(actor,0x348)={int(input[obwX]),int(input[obwY]),int(input[obwZ])};
  at<Vector>(actor,0x354)={int(input[axisX]),int(input[axisY]),int(input[axisZ])};at<Vector>(actor,0x360)={int(input[sideX]),int(input[sideY]),int(input[sideZ])};
  at<int>(actor,0x80)=input[elapsed];at<short>(camera,0x236)=input[cameraAngle];mem<void*>(0x56F3B8)=input[hasCamera]?camera:nullptr;
  at<void*>(actor,0xBEC)=input[cachedJonah]?jonah:nullptr;at<short>(jonah,0x38)=316;at<Vector>(jonah,8)={int(input[jonahX]),int(input[jonahY]),int(input[jonahZ])};
  at<unsigned short>(jonah,0xDC)=input[jonahRadius];at<short>(middle,0x38)=304;at<void*>(middle,0x20)=jonah;mem<void*>(0x56E990)=input[hasJonah]?middle:nullptr;mem<void*>(0x6A9038)=player;
  at<int>(actor,0xBF8)=input[targetKind];at<unsigned>(pointer(input[target]?input[target]:3),0x218)=input[targetSignals];
  at<unsigned char>(player,0xAD4)=input[crawling];at<Vector>(player,8)={int(input[playerX]),int(input[playerY]),int(input[playerZ])};
  at<Vector>(body,8)={int(input[bodyX]),int(input[bodyY]),int(input[bodyZ])};at<short>(body,0x38)=input[bodyType];at<short>(player,0x38)=input[bodyType]==401?0:input[bodyType];
  mem<int>(0x5FCCF4)=input[attackClock];mem<int>(0x5FBEE0)=input[baddyCheck];at<short>(actor,0x16)=input[angleY];at<unsigned char>(actor,0x295)=input[movementByte];
  at<int>(actor,0xC10)=input[playerTargetUpdates];at<short>(actor,0x21E)=input[clearance];at<int>(actor,0x384)=input[chargeIndex];
  at<Vector>(actor,0x36C)={int(input[chargeX]),int(input[chargeY]),int(input[chargeZ])};at<Vector>(actor,0x378)={int(input[startX]),int(input[startY]),int(input[startZ])};
  at<int>(actor,0x3A8)=input[chargeAttempts];at<unsigned>(actor,0xBFC)=input[checksum];
  for(int i=0;i<3;i++)at<Vector>(actor,0x1B4+12*i)={int(input[pt1X+3*i]),int(input[pt1Y+3*i]),int(input[pt1Z+3*i])};
  unsigned short kinds[2][2]={{1,0},{1002,5}};for(int i=0;i<2;i++){std::memset(nodeData[i],0,8);std::memcpy(nodeData[i],kinds[i],4);}
  for(int i=0;i<512;i++)trigNodes[i]=nodeData[0];trigNodes[input[waypoint]&511]=nodeData[input[breakWall]?1:0];
  for(int i=0;i<4;i++)at<unsigned short>(squirt,2*i)=input[word0+i];
  if(input[method]==5)reinterpret_cast<void(__attribute__((thiscall))*)(void*,void*)>(methods[5])(actor,pointer(input[target]));
  else reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(methods[input[method]])(actor);
  std::cout<<"{\"snapshot\":";numbers(snapshot());std::cout<<",\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';numbers(events[i]);}std::cout<<"]}\n";
 }
}
'''

S = 1 << 12
I32 = (-2**31, 2**31 - 1)


def initial(**changes):
    values = dict.fromkeys(FIELDS, 0)
    values.update(state=12, behavior=0xF2022005, completion=0xFFFFFFFF, reaction=0x55, signals=0x1100, field310=77, bodyFlags=0x216,
                  sourceNode=40, finished=1, frame=0, tugPointer=1, tugValidation=0x1234, tugValid=1, lastWindUp=11, speechAccepted=1,
                  allocAccepted=1, radius=175, standingY=-29 * S, phase=3, px=-1735 * S, py=-31 * S, pz=443 * S, vx=5, vy=6, vz=7,
                  ax=8, ay=9, az=10, lx=11, ly=12, lz=13, avx=14, avy=15, avz=16, aax=17, aay=18, aaz=19, waypoint=12,
                  wpX=-1200 * S, wpY=-40 * S, wpZ=700 * S, envX=-1500 * S, envY=-20 * S, envZ=900 * S,
                  obsX=-1700 * S, obsY=-31 * S, obsZ=500 * S, obwX=-1735 * S, obwY=-31 * S, obwZ=443 * S, hasCamera=1, cameraAngle=1000,
                  elapsed=2, cachedJonah=0, hasJonah=1, jonahX=-1480 * S, jonahY=-31 * S, jonahZ=839 * S, jonahRadius=120,
                  targetKind=2, determineKind=-1, target=1, playerX=-1000 * S, playerY=-31 * S, playerZ=900 * S, bodyX=-1600 * S,
                  bodyY=-31 * S, bodyZ=600 * S, bodyType=401, angleY=300, movementByte=2, playerTargetUpdates=0, clearance=100,
                  chargeX=-1300 * S, chargeY=-25 * S, chargeZ=650 * S, startX=-1735 * S, startY=-31 * S, startZ=443 * S,
                  checksum=0xABCD, physicsX=40 * S, physicsZ=-25 * S, linkNode=77, word0=8504, word1=0x1111, word2=0x2222, word3=0x3333,
                  pt1X=-1500 * S, pt1Y=-10 * S, pt1Z=600 * S, pt2X=-1400 * S, pt2Y=-12 * S, pt2Z=500 * S, pt3X=-1200 * S, pt3Y=-14 * S, pt3Z=200 * S)
    values.update(changes)
    return values


def paths(*entries):
    data = [list(entry) for entry in entries] + [[1, 0, 0, 0]] * (PATHS - len(entries))
    return data[:PATHS]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    cases = []
    add = lambda values, entries=(): cases.append(dict(input=values, paths=paths(*entries)))
    # GetTugged
    for substate in [-1, 0, 1, 2, 3, 4]:
        for behavior in [0, 8]:
            for counter in [0, 10, 11, 12]:
                for frame in [9, 10, -5]:
                    for tug in [(0, 1), (1, 0), (1, 1)]:
                        for finished in [0, 1]:
                            add(initial(method=0, substate=substate, behavior=behavior, counter=counter, frame=frame,
                                        tugPointer=tug[0], tugValid=tug[1], finished=finished, allocAccepted=counter & 1))
    # WindUp
    for state in [16, 17]:
        for substate in [-1, 0, 1, 2, 3, 4]:
            for completion in [0, 2, 0xFFFFFFFF]:
                for alloc in [0, 1]:
                    add(initial(method=1, state=state, substate=substate, completion=completion, allocAccepted=alloc))
    for r0 in range(4):
        for r1 in range(3):
            for last in [10, 11, 12, 13, 0]:
                for quip in [0, 5]:
                    for speech in [0, 1]:
                        add(initial(method=1, state=16, substate=2, rand0=r0, rand1=r1, lastWindUp=last, quipSuppression=quip, speechAccepted=speech))
    # ReactToJonah
    for result in [0, 1, 2, 4]:
        for contact in [0, 199, 200, 1000]:
            for idle in [0, 2]:
                add(initial(method=2, state=14, substate=0, idleByte=idle),
                    [[result, -1200 * S + contact * S, -29 * S, 700 * S]])
    for counter in [0, 63, 64, 65, 0x7FFFFFFF]:
        for first in [0, 1, 2]:
            for second in [0, 2, 4]:
                for contact in [0, 549, 550]:
                    for jonah in [(0, 0), (1, 1), (0, 1)]:
                        add(initial(method=2, state=14, substate=1, counter=counter, cachedJonah=jonah[0], hasJonah=jonah[1]),
                            [[first, -1600 * S, 5 * S, 300 * S], [second, -1200 * S + contact * S, -31 * S, 700 * S]])
    for substate in [-1, 2, 0x7FFFFFFF]:
        add(initial(method=2, state=14, substate=substate))
    # AvoidObstacle
    for substate in [0, 1]:
        for counter in [0, 1, 7, 22, 23, 24, 40]:
            for result in [0, 1, 2]:
                for finished in [0, 1]:
                    for obstacle in [(-1700, 500), (-1735, 443), (-1000, 443), (-1735, 1200), (0, 0)]:
                        add(initial(method=3, state=20, substate=substate, counter=counter, finished=finished, obsX=obstacle[0] * S, obsZ=obstacle[1] * S,
                                    axisX=4096 * 100, axisZ=-300, sideX=1234, sideY=5, sideZ=-4321, radius=175 + counter),
                            [[result, -1650 * S, -20 * S, 520 * S]])
    for counter in [0, 664, 665, 666, 0x7FFFFFFF]:
        for result in [0, 1, 2]:
            for contact in [100, 101, 500]:
                add(initial(method=3, state=20, substate=2, counter=counter, cachedJonah=counter & 1),
                    [[result, (-1735 + contact) * S, -31 * S, 443 * S]])
    for substate in [-1, 3]:
        add(initial(method=3, state=20, substate=substate))
    # Die
    for substate in [-1, 0, 1, 2, 3]:
        for camera in [0, 1]:
            for finished in [0, 1]:
                for behavior in [0, 0x4000]:
                    for pulse in [0, 1]:
                        for node in [40, 0xFFFF, 0]:
                            for angle in [1000, -32768, 32767]:
                                add(initial(method=4, state=21, substate=substate, hasCamera=camera, finished=finished, behavior=behavior,
                                            pulseSent=pulse, sourceNode=node, cameraAngle=angle, elapsed=[0, 2, 0x10001][(substate + 3) % 3]))
    # PursueTarget
    for kind, target in [(1, 2), (2, 1), (3, 3)]:
        for signals in [0, 0x800]:
            for determine in [-1, 1, 2]:
                for crawling, los in [(0, 1), (1, 0), (1, 1)]:
                    for substate in [0, 666]:
                        add(initial(method=5, state=3, substate=substate, targetKind=kind, target=target, targetSignals=signals,
                                    determineKind=determine, crawling=crawling, losPlayer=los))
    for substate in [-1, 4, 665, 669, 0x7FFFFFFF]:
        add(initial(method=5, state=3, substate=substate))
    for completion in [0, 2, 0xFFFFFFFF]:
        for distance in [0, 0x225, 0x226, 3000]:
            for alloc in [0, 1]:
                add(initial(method=5, state=3, substate=1, completion=completion, allocAccepted=alloc, movementByte=distance & 7,
                            playerX=-1735 * S + distance * S, playerZ=443 * S, physicsX=0, physicsZ=0))
    for clock in [0, 1, 4]:
        for jump in [1099, 1100, 1500, 2549, 2550, 2551, 2600, 4000]:
            for path in [0, 1, 2]:
                for contact in [0, 549, 550]:
                    for line in [(0, 0), (3, 0), (1, 0), (0, 1), (2, 1)]:
                        for behavior in [0, 0x2000]:
                            add(initial(method=5, state=3, substate=2, attackClock=clock, behavior=behavior, lineItem=line[0], lineFace=line[1],
                                        playerX=-1735 * S + jump * S, playerZ=443 * S, targetKind=2 if clock != 4 else 3,
                                        target=1 if clock != 4 else 3, bodyX=-1735 * S + jump * S, bodyZ=443 * S,
                                        playerTargetUpdates=jump & 1, baddyCheck=jump & 2),
                                [[path, (-1735 + jump + contact) * S, -31 * S, 443 * S]])
    for near in [100, 549, 550, 551]:
        for target in [1, 3]:
            for updates in [0, 5]:
                add(initial(method=5, state=3, substate=2, attackClock=1, target=target, targetKind=2 if target == 1 else 3,
                            playerX=-1735 * S + near * S, playerZ=443 * S, bodyX=-1735 * S + near * S, bodyZ=443 * S,
                            physicsX=0, physicsZ=0, playerTargetUpdates=updates))
    for completion in [0, 8]:
        for line in [(0, 0), (3, 0), (1, 0), (0, 1)]:
            for physics in [(0, 0), (40 * S, -25 * S), (-3 * S, 900 * S)]:
                for radius in [175, 0, 65535]:
                    add(initial(method=5, state=3, substate=3, completion=completion, lineItem=line[0], lineFace=line[1],
                                physicsX=physics[0], physicsZ=physics[1], radius=radius, clearance=[100, 9, -5][radius % 3]))
    for index in range(3):
        for substate in [666, 667, 668]:
            for frame in [-1, 3, 4, 5, 10, 11, 12, 27, 28, 29]:
                for completion in [0, 2]:
                    for finished in [0, 1]:
                        for method in [5, 6]:
                            add(initial(method=method, state=3 if method == 5 else 1, substate=substate, chargeIndex=index, frame=frame,
                                        completion=completion, finished=finished, signals=[0, 0x80, 0x100, 0x180][frame & 3]))
    # FollowWaypoints
    for count in [0, 1, 2, 3]:
        for attempts in [0, 1, 2, 5]:
            for charge in [(0, 0), (100, 0), (150, 0), (151, 0), (300, 300), (399, 0), (400, 0), (401, 0), (0, 500), (499, 0), (500, 0),
                           (501, 0), (0, -1999), (2000, 0), (2001, 0), (-2400, 1700), (-120, -380), (90, 400)]:
                for angle in [0, 1024, 2047, 3000]:
                    point = (-1735 + charge[0]) * S, -27 * S, (443 + charge[1]) * S
                    add(initial(method=6, state=1, substate=0, pathCount=count, chargeAttempts=attempts, angleY=angle, allocAccepted=attempts & 1,
                                wpX=point[0], wpY=point[1], wpZ=point[2], **{f'pt{count}X': point[0], f'pt{count}Y': point[1], f'pt{count}Z': point[2]}
                                if count else {}))
    for kind in [1, 2]:
        for signals in [0, 0x80]:
            for state in [1, 15]:
                for completion in [0, 1]:
                    add(initial(method=6, state=state, substate=1, targetKind=kind, signals=signals, completion=completion, pathCount=0))
    for substate in [1, 2]:
        for state in [1, 15, 16]:
            for count in [0, 1, 2]:
                for wall in [0, 1]:
                    for offset in [0, 2]:
                        for word in [8504, 8505]:
                            for following in [0, 1]:
                                for signals in [0, 0x80, 0x98]:
                                    add(initial(method=6, state=state, substate=substate, completion=1, pathCount=count, breakWall=wall,
                                                squirtOffset=offset, word0=word, word1=word, nextWaypoint=following, signals=signals))
    for links in [0, 1]:
        for alloc in [0, 1]:
            add(initial(method=6, state=15, substate=5, linkCount=links, allocAccepted=alloc))
    for completion in [0, 2]:
        for following in [0, 1]:
            add(initial(method=6, state=15, substate=6, completion=completion, nextWaypoint=following))
    for frame in [27, 28, 100]:
        add(initial(method=6, state=15, substate=7, frame=frame))
    for finished in [0, 1]:
        for signals in [0, 0xFFFFFFFF, 0x4]:
            add(initial(method=6, state=15, substate=8, finished=finished, signals=signals))
    for substate in [-1, 3, 4, 9, 665, 669]:
        add(initial(method=6, state=15, substate=substate))
    rng = random.Random(0x488590)
    for _ in range(1400):
        method = rng.randrange(7)
        values = initial(method=method, state=[12, rng.choice([16, 17]), 14, 20, 21, 3, rng.choice([1, 15])][method],
                         substate=rng.choice([-1, 0, 1, 2, 3, 4] + ([5, 6, 7, 8, 666, 667, 668] if method >= 5 else [])),
                         counter=rng.choice([rng.randint(-100, 800), rng.randint(*I32)]), behavior=rng.randrange(2**32),
                         completion=rng.randrange(2**32), reaction=rng.randrange(2**32), signals=rng.randrange(2**32),
                         bodyFlags=rng.randrange(65536), pulseSent=rng.randrange(2), sourceNode=rng.choice([0xFFFF, rng.randrange(65536)]),
                         pathCount=rng.randint(*I32), finished=rng.randrange(2), frame=rng.randint(-32768, 32767), idleByte=rng.randrange(256),
                         tugPointer=rng.randrange(2), tugValid=rng.randrange(2), tugValidation=rng.randrange(2**32), lastWindUp=rng.randint(9, 14),
                         quipSuppression=rng.choice([0, 0, 7]), rand0=rng.randrange(3), rand1=rng.randrange(3), speechAccepted=rng.randrange(2),
                         allocAccepted=rng.randrange(2), radius=rng.randrange(65536), standingY=rng.randint(*I32), phase=rng.randrange(256),
                         waypoint=rng.randrange(400), hasCamera=rng.randrange(2), cameraAngle=rng.randint(-32768, 32767), elapsed=rng.randint(*I32),
                         cachedJonah=rng.randrange(2), hasJonah=rng.randrange(2), jonahRadius=rng.randrange(65536),
                         targetKind=rng.choice([0, 1, 2, 3]), determineKind=rng.choice([-1, 1, 2, 3]), target=rng.choice([1, 2, 3]),
                         targetSignals=rng.randrange(2**32), crawling=rng.randrange(2), losPlayer=rng.randrange(2), bodyType=rng.choice([401, 316, 0]),
                         attackClock=rng.randrange(8), baddyCheck=rng.randrange(2), angleY=rng.randint(-32768, 32767), movementByte=rng.randrange(256),
                         playerTargetUpdates=rng.choice([0, 9]), clearance=rng.randint(-32768, 32767), chargeIndex=rng.randrange(3),
                         chargeAttempts=rng.randint(-2, 3), checksum=rng.randrange(2**32), lineItem=rng.choice([0, 0, 1, 2, 3]),
                         lineFace=rng.randrange(2), linkCount=rng.randrange(2), linkNode=rng.randrange(512), nextWaypoint=rng.randrange(2),
                         breakWall=rng.randrange(2), squirtOffset=rng.choice([0, 2]), word0=rng.choice([8504, rng.randrange(65536)]),
                         word1=rng.randrange(65536), word2=rng.randrange(65536), word3=rng.randrange(65536))
        if method == 6: values['pathCount'] = rng.randrange(4)
        for name in ['px', 'py', 'pz', 'wpX', 'wpY', 'wpZ', 'envX', 'envY', 'envZ', 'obsX', 'obsY', 'obsZ', 'obwX', 'obwY', 'obwZ',
                     'axisX', 'axisY', 'axisZ', 'sideX', 'sideY', 'sideZ', 'jonahX', 'jonahY', 'jonahZ', 'vx', 'vy', 'vz', 'ax', 'ay', 'az',
                     'lx', 'ly', 'lz', 'playerX', 'playerY', 'playerZ', 'bodyX', 'bodyY', 'bodyZ', 'chargeX', 'chargeY', 'chargeZ',
                     'startX', 'startY', 'startZ', 'physicsX', 'physicsZ', 'pt1X', 'pt1Y', 'pt1Z', 'pt2X', 'pt2Y', 'pt2Z', 'pt3X', 'pt3Y', 'pt3Z']:
            values[name] = rng.choice([rng.randint(-3000 * S, 3000 * S), rng.randint(*I32)])
        for name in ['avx', 'avy', 'avz', 'aax', 'aay', 'aaz']:
            values[name] = rng.randint(-32768, 32767)
        cases.append(dict(input=values, paths=[[rng.choice([0, 1, 2, 2, 4])] + [rng.choice([rng.randint(-3000 * S, 3000 * S), rng.randint(*I32)])
                                                                                 for _ in range(3)] for _ in range(PATHS)]))
    rows = [[case['input'][name] for name in FIELDS] + [value for entry in case['paths'] for value in entry] for case in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-scorpion-behavior-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(HARNESS.replace('GENERATED_FIELDS', ', '.join(FIELDS)).replace('PATH_COUNT', str(PATHS)))
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                        input=('\n'.join(' '.join(map(str, row)) for row in rows) + '\n').encode()).decode().splitlines()
    for case, line in zip(cases, lines, strict=True):
        case['expected'] = json.loads(line)
    (root / 'modern/OpenSpidey.Tests/Fixtures/scorpion-behavior.json').write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole PC GetTugged/WindUp/ReactToJonah/AvoidObstacle/Die/PursueTarget/FollowWaypoints calls.')


if __name__ == '__main__':
    main()
