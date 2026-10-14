"""Capture whole shipped488590 outer Scorpion AI with genuine shared physics, timers, queues, procedures and tail math.

Camera mode, posed hooks, asset creation, footprint, audio, and the explicitly unported
behavior bodies are transport boundaries. Their ordered calls are captured, never
registered as production stand-ins. This fixture proves outer ordering and the inline
startup/slide/message decisions, not completion of Scorpion's remaining behaviors.
"""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_scorpion_damage_vectors import FIELDS as DAMAGE_FIELDS, initial as damage_initial


EXTRA_FIELDS = [
    'signals', 'standingY', 'groundY', 'environmentTimer', 'gloatTimer', 'timerC1C', 'glowEnabled',
    'savedHorizontal', 'savedVertical', 'distancesSaved', 'lastFootstep', 'cameraEnabled',
    'geometryRegion', 'glowRegion', 'cameraMode', 'cameraFlag', 'cameraSpin', 'cameraRadius',
    'cameraHorizontal', 'cameraVertical', 'hiddenPattern', 'heldItem', 'crawling', 'playerLock',
    'playerAa', 'synthesizedInput', 'musicPhase', 'musicClock', 'trackEnding', 'globalBoss',
    'trapHandle', 'tugHandle', 'trapValid', 'tugValid', 'targetValid', 'hasProcedure',
    'procedureState', 'procedureSubstate', 'procedureRetire', 'procedureType', 'impactAccepted',
    'groundResult', 'brainMutation', 'radius', 'jonahRadius', 'jonahX', 'jonahY', 'jonahZ', 'cameraCollision'
]
FIELDS = DAMAGE_FIELDS + EXTRA_FIELDS

HARNESS = r'''
#include "original_executable.h"
#include <array>
#include <cstdlib>
#include <iostream>
struct Vector{int x,y,z;};
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
template<class T>T&mem(unsigned n){return *reinterpret_cast<T*>(n);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
void omit(unsigned a,int n){std::memset((void*)a,0x90,n);}
enum Field{GENERATED_FIELDS,FieldCount};long long input[FieldCount];
alignas(4)unsigned char actor[0xC28],player[0xF20],camera[0x300],jonah[0x400],cached[0x400],other[0x400],webs[2][0x200],items[8][0x50],procedure[64],messages[4][68];
void*procedureVtable[2];void*messageVtable[1];int messageCount;std::vector<void*> allocations;
std::vector<std::vector<long long>>events;unsigned char randomBytes[5],physicsBytes[5],tailBytes[5];
int targetId(void*p){return p==player?1:p==other?2:0;}int jonahId(void*p){return p==jonah?1:p==cached?2:0;}
int webId(void*p){return p==webs[0]?1:p==webs[1]?2:0;}int itemId(void*p){for(int i=0;i<8;i++)if(p==items[i])return i+1;return 0;}
void appendVector(std::vector<long long>&v,Vector p){v.insert(v.end(),{p.x,p.y,p.z});}
std::vector<long long>snapshot(){std::vector<long long>v={at<int>(actor,0x31C),at<int>(actor,0x320),at<unsigned>(actor,0x2A8),at<unsigned>(actor,0x288),at<unsigned>(actor,0x324),at<unsigned>(actor,0x218),
 at<int>(actor,0x3EC),at<int>(actor,0xC10),at<int>(actor,0xC14),at<int>(actor,0xC18),at<int>(actor,0xC1C),at<int>(actor,0xC20),at<int>(actor,0x29C),at<int>(actor,0x2A0),at<short>(actor,0x314),
 at<unsigned char>(actor,0x480),at<unsigned short>(actor,0x328),at<unsigned short>(actor,0x32A),at<int>(actor,0x32C),at<int>(actor,0x3AC),at<unsigned char>(actor,0xC0C),
 at<unsigned char>(actor,0x417),at<unsigned char>(actor,0x45F),at<int>(actor,0x1F8),at<int>(actor,0x1F4),at<unsigned short>(actor,0x12A),at<short>(actor,0x128),at<int>(actor,0x230),
 at<int>(actor,0xBF8),targetId(at<void*>(actor,0xBF0)),jonahId(at<void*>(actor,0xBEC)),at<unsigned char>(actor,0xBDC),webId(at<void*>(actor,0x104)),at<unsigned>(actor,0x108),webId(at<void*>(actor,0x10C)),at<unsigned>(actor,0x110)};
 for(unsigned o:{8u,0x60u,0x6Cu,0x27Cu,0x2FCu,0x33Cu,0x348u})appendVector(v,at<Vector>(actor,o));
 for(unsigned o:{0x14u,0x88u,0x8Eu})for(int i=0;i<3;i++)v.push_back(at<short>(actor,o+i*2));
 v.insert(v.end(),{at<unsigned short>(actor,0xE0),at<unsigned>(actor,0x2AC),mem<int>(0x6B4C7C),mem<int>(0x6B4B90),mem<int>(0x6B4B94),mem<int>(0x60D880),mem<int>(0x60D86C),mem<unsigned char>(0x60D874),
 at<unsigned>(camera,0x2A0),at<unsigned char>(camera,0xF9),at<unsigned char>(camera,0x180),at<int>(camera,0x2A4),at<int>(camera,0x2A8),mem<int>(0x548860),mem<int>(0x548864),mem<void*>(0x56E998)==actor?1:0});
 for(int i=0;i<8;i++)v.insert(v.end(),{itemId(at<void*>(camera,0x184+i*4)),at<unsigned short>(items[i],4)});
 for(int i=0;i<4;i++)v.push_back(at<int>(messages[i],16));unsigned alive=0;for(void*p=at<void*>(actor,0x290);p;p=at<void*>(p,8))for(int i=0;i<4;i++)if(p==messages[i])alive|=1u<<i;v.push_back(alive);
 std::vector<void*>procs;for(void*p=at<void*>(actor,0x28C);p;p=at<void*>(p,0x1C))procs.push_back(p);v.push_back(procs.size());for(void*p:procs)for(unsigned o:{8u,0x10u,0x14u,0xCu,0x38u,0x3Cu})v.push_back(at<int>(p,o));
 for(unsigned i=0;i<6;i++)appendVector(v,mem<Vector>(0x6A73F8+i*12));return v;}
void event(int kind,std::initializer_list<long long>args={}){std::vector<long long>v={kind};v.insert(v.end(),args);auto s=snapshot();v.insert(v.end(),s.begin(),s.end());events.push_back(v);}
int next(int bound){std::memcpy((void*)0x4E5DA0,randomBytes,5);int result=reinterpret_cast<int(*)(int)>(0x4E5DA0)(bound);patch(0x4E5DA0,(void*)&next);event(0,{bound,result});return result;}
void __attribute__((thiscall)) setMode(void*p,unsigned value){assert(p==camera);event(1,{value});at<unsigned>(camera,0x2A0)=value;}
void __attribute__((thiscall)) behind(void*p,int argument){assert(p==player);event(2,{argument});}
void __attribute__((thiscall)) tailGeometry(void*p){assert(p==actor);event(3);at<unsigned char>(actor,0x417)=7;}
void __attribute__((thiscall)) tailGlow(void*p){assert(p==actor);event(4);at<unsigned char>(actor,0x45F)=8;}
void __attribute__((thiscall)) physics(void*p,int limit){assert(p==actor);event(5,{limit});std::memcpy((void*)0x404C50,physicsBytes,5);reinterpret_cast<void(__attribute__((thiscall))*)(void*,int)>(0x404C50)(p,limit);patch(0x404C50,(void*)&physics);}
void transform(void*p){assert(p==actor);event(6);if(input[brainMutation]==1){at<unsigned>(actor,0x218)|=0x4C;at<int>(actor,0xBF8)=1;}}
void __attribute__((thiscall)) execute(void*p){assert(p==procedure);event(7);at<int>(actor,0x31C)=input[procedureState];at<int>(actor,0x320)=input[procedureSubstate];at<int>(actor,16)-=23;if(input[procedureRetire])at<int>(procedure,16)|=1;}
void __attribute__((thiscall)) deleteProcedure(void*p,int){assert(p==procedure);at<void*>(actor,0x28C)=at<void*>(p,0x1C);}
void __attribute__((thiscall)) deleteMessage(void*p,int){void*previous=nullptr;for(void*m=at<void*>(actor,0x290);m;m=at<void*>(m,8)){if(m==p){if(previous)at<void*>(previous,8)=at<void*>(m,8);else at<void*>(actor,0x290)=at<void*>(m,8);return;}previous=m;}assert(false);}
void sound(unsigned value,int*position,int pitch){assert(position==(int*)(actor+8));event(8,{value,pitch});if(input[brainMutation]==2){at<int>(actor,0x31C)=20;at<int>(actor,0x320)=0;at<unsigned>(actor,0x218)|=0x801;}}
void*recover(void*handle){int role=handle==actor+0xBF0?0:handle==actor+0x104?1:handle==actor+0x10C?2:-1;assert(role>=0);event(9,{role});bool valid=input[role==0?targetValid:role==1?trapValid:tugValid]!=0;if(!valid)at<void*>(handle,0)=nullptr;return valid?at<void*>(handle,0):nullptr;}
void __attribute__((thiscall))burst(void*p){event(10,{webId(p)});if(input[brainMutation]==3){at<unsigned>(actor,0x218)|=0x801;at<Vector>(messages[0],0x34)={777,888,999};}}
void __attribute__((thiscall))run(void*p,unsigned clip,int from,int to){assert(p==actor);event(11,{clip,from,to});at<unsigned short>(actor,0x12A)=clip;at<short>(actor,0x128)=from;if(input[brainMutation]==4){at<int>(actor,0x320)=4;at<Vector>(actor,8).x+=4096;}}
void __attribute__((thiscall))cycle(void*p,unsigned clip,signed char direction){assert(p==actor);event(12,{clip,direction});at<unsigned short>(actor,0x12A)=clip;at<short>(actor,0x128)=0;}
int ground(void*position,int above,int below,int flags){assert(position==actor+8);event(13,{above,below,flags});if(input[brainMutation]==5)at<int>(actor,0x320)=5;return input[groundResult];}
void behavior(unsigned entry,void*target=nullptr){event(14,{entry,targetId(target)});if(input[brainMutation]==6){at<unsigned>(actor,0x218)|=0x801;at<int>(actor,0xBF8)=2;at<unsigned>(actor,0x324)|=4;}}
#define BEHAVIOR(name,entry) void __attribute__((thiscall))name(void*p){assert(p==actor);behavior(entry);}
BEHAVIOR(follow,0x484240) BEHAVIOR(idle,0x486980) BEHAVIOR(laser,0x487230) BEHAVIOR(trapped,0x486DA0) BEHAVIOR(tugged,0x486C00)
BEHAVIOR(intro,0x487FC0) BEHAVIOR(jonahReaction,0x487CD0) BEHAVIOR(windup,0x484A90) BEHAVIOR(gloat,0x487740) BEHAVIOR(avoid,0x484C60) BEHAVIOR(die,0x487B80)
void __attribute__((thiscall))pursue(void*p,void*target){assert(p==actor);behavior(0x485120,target);}void __attribute__((thiscall))environment(void*p,void*target){assert(p==actor);behavior(0x485D90,target);}
void __attribute__((thiscall))determine(void*p){assert(p==actor);event(15);at<int>(actor,0x31C)=3;at<int>(actor,0x320)=42;at<int>(actor,0xBF8)=2;}
void __attribute__((thiscall))footprint(void*p){assert(p==actor);event(16);if(input[brainMutation]==7)at<unsigned>(actor,0x218)|=0x801;}
void __attribute__((thiscall))tail(void*p){assert(p==actor);event(17);std::memcpy((void*)0x488370,tailBytes,5);reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x488370)(p);patch(0x488370,(void*)&tail);}
void dynamicHook(Vector*out,void*p,const short*hook){assert(p==actor&&hook[0]==0&&hook[1]==0&&hook[2]==0&&hook[3]>=17&&hook[3]<=23);event(18,{hook[3]});Vector pos=at<Vector>(actor,8);*out={pos.x+hook[3]*12345,pos.y+at<short>(actor,0x128)*200,pos.z-hook[3]*7777};}
void*allocate(unsigned n){assert(n==64||n==76);bool accepted=input[n==64?lookAtAccepted:impactAccepted]!=0;event(n==64?19:20,{n,accepted});if(n==76&&input[brainMutation]==8)at<Vector>(actor,0x5C4)={1234567,-2345678,3456789};if(!accepted)return nullptr;void*p=std::calloc(1,n);assert(p);allocations.push_back(p);return p;}
unsigned long long makeHandle(void*p){assert(p==player);return input[handleAccepted]?((unsigned long long)321<<32)|(unsigned)p:0;}
void* __attribute__((thiscall))impact(void*p,int*position){assert(position==(int*)(actor+0x5C4));event(21,{position[0],position[1],position[2]});return p;}
void diagnostic(int condition,const char*message){if((condition&255)==0){int code=!strcmp(message,"Unknown state!")?0:!strcmp(message,"Unknown substate!")?1:!strcmp(message,"Unknown sub-state!")?2:-1;assert(code>=0);event(22,{code});}}
void numbers(const std::vector<long long>&v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
std::vector<long long>tailState(){std::vector<long long>v;for(unsigned o:{0x48Cu,0x4BCu,0x5D0u})for(unsigned i=0;i<(o==0x48C?4:o==0x4BC?23:128);i++)appendVector(v,at<Vector>(actor,o+i*12));return v;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 for(unsigned a:{0x488597u,0x4866E7u,0x401187u}){omit(a,6);mem<unsigned short>(a)=0xC031;}
 for(unsigned a:{0x48859Eu,0x488F79u,0x4866EEu,0x48671Cu,0x48675Au,0x4867E7u,0x40118Eu,0x4012D5u})omit(a,7);
 std::memcpy(randomBytes,(void*)0x4E5DA0,5);patch(0x4E5DA0,(void*)&next);std::memcpy(physicsBytes,(void*)0x404C50,5);patch(0x404C50,(void*)&physics);std::memcpy(tailBytes,(void*)0x488370,5);patch(0x488370,(void*)&tail);
 patch(0x4167F0,(void*)&setMode);patch(0x4BF820,(void*)&behind);patch(0x489050,(void*)&tailGeometry);patch(0x489370,(void*)&tailGlow);patch(0x475F50,(void*)&transform);patch(0x471EA0,(void*)&sound);
 patch(0x4583F0,(void*)&recover);patch(0x4F8600,(void*)&burst);patch(0x460C10,(void*)&run);patch(0x460D00,(void*)&cycle);patch(0x4E6840,(void*)&ground);patch(0x488140,(void*)&determine);
 patch(0x484240,(void*)&follow);patch(0x486980,(void*)&idle);patch(0x485120,(void*)&pursue);patch(0x485D90,(void*)&environment);patch(0x487230,(void*)&laser);patch(0x486DA0,(void*)&trapped);
 patch(0x486C00,(void*)&tugged);patch(0x487FC0,(void*)&intro);patch(0x487CD0,(void*)&jonahReaction);patch(0x484A90,(void*)&windup);patch(0x487740,(void*)&gloat);patch(0x484C60,(void*)&avoid);patch(0x487B80,(void*)&die);
 patch(0x489F60,(void*)&footprint);patch(0x453EE0,(void*)&dynamicHook);patch(0x4088A0,(void*)&allocate);patch(0x455390,(void*)&allocate);patch(0x458360,(void*)&makeHandle);patch(0x482F20,(void*)&impact);patch(0x4015B0,(void*)&diagnostic);
 procedureVtable[0]=(void*)&deleteProcedure;procedureVtable[1]=(void*)&execute;messageVtable[0]=(void*)&deleteMessage;
 // Genuine GTE physics uses the source table initialized before gameplay.
 for(int i=0;i<4096;i++){mem<short>(0x610C48+i*4)=short(__builtin_cos(i*0.001536096911877394)*4096.0);mem<short>(0x610C4A+i*4)=short(__builtin_sin(i*0.001536096911877394)*4096.0);}
 while(std::cin>>input[0]){for(int i=1;i<FieldCount;i++)std::cin>>input[i];std::cin>>messageCount;assert(messageCount>=0&&messageCount<=4);
  for(void*p:std::initializer_list<void*>{actor,player,camera,jonah,cached,other})std::memset(p,0,p==actor?sizeof(actor):p==player?sizeof(player):p==camera?sizeof(camera):sizeof(jonah));
  std::memset(webs,0,sizeof(webs));std::memset(items,0,sizeof(items));std::memset(procedure,0,sizeof(procedure));std::memset(messages,0,sizeof(messages));events.clear();
  at<short>(actor,0xE2)=input[health];at<int>(actor,0x31C)=input[state];at<int>(actor,0x320)=input[substate];at<unsigned>(actor,0x2A8)=input[behaviorFlags];at<unsigned>(actor,0x288)=input[completionFlags];at<unsigned>(actor,0x324)=input[reactionFlags];at<unsigned>(actor,0x218)=input[signals];
  for(auto pair:std::initializer_list<std::pair<unsigned,int>>{{0x310,field310},{0x1F8,slideUpdates},{0x3EC,hitFlashUpdates},{0xBF8,targetKind},{0xC10,targetUpdates},{0xC20,quipSuppression},{0x230,waitUpdates},
   {0x29C,standingY},{0x2A0,groundY},{0xC14,environmentTimer},{0xC18,gloatTimer},{0xC1C,timerC1C},{0x32C,distancesSaved},{0x3AC,lastFootstep}})at<int>(actor,pair.first)=input[pair.second];
  at<unsigned char>(actor,0x480)=input[glowEnabled];at<unsigned short>(actor,0x328)=input[savedHorizontal];at<unsigned short>(actor,0x32A)=input[savedVertical];at<unsigned char>(actor,0xC0C)=input[cameraEnabled];at<unsigned char>(actor,0x417)=input[geometryRegion];at<unsigned char>(actor,0x45F)=input[glowRegion];
  at<void*>(actor,0xBF0)=input[targetHandle]?other:nullptr;at<void*>(actor,0xBEC)=input[cachedJonah]?cached:nullptr;at<unsigned short>(actor,0x12A)=input[clip];at<short>(actor,0x128)=input[frame];at<short>(actor,0x21E)=input[surfaceClearance];at<unsigned short>(actor,0xDC)=input[radius];at<int>(actor,0x80)=input[elapsedTicks];
  for(auto pair:std::initializer_list<std::pair<unsigned,int>>{{8,px},{0x60,vx},{0x6C,ax},{0x27C,lx}})for(int i=0;i<3;i++)at<int>(actor,pair.first+i*4)=input[pair.second+i];
  for(auto pair:std::initializer_list<std::pair<unsigned,int>>{{0x88,avx},{0x8E,aax}})for(int i=0;i<3;i++)at<short>(actor,pair.first+i*2)=input[pair.second+i];
  for(unsigned o:{0x78u,0x94u})for(int i=0;i<3;i++)at<unsigned char>(actor,o+i)=1;
  at<void*>(actor,0x104)=input[trapHandle]?webs[0]:nullptr;at<unsigned>(actor,0x108)=901;at<void*>(actor,0x10C)=input[tugHandle]?webs[1]:nullptr;at<unsigned>(actor,0x110)=902;
  for(int i=0;i<4;i++)at<Vector>(actor,0x48C+i*12)={100+i,200+i,300+i};for(int i=0;i<23;i++)at<Vector>(actor,0x4BC+i*12)={10000+i,-20000-i,30000+i};for(int i=0;i<128;i++)at<Vector>(actor,0x5D0+i*12)={40000+i,-50000-i,60000+i};
  for(int i=0;i<6;i++)mem<Vector>(0x6A73F8+i*12)={70000+i,-80000-i,90000+i};
  mem<void*>(0x6A9038)=player;at<unsigned char>(player,0xAD4)=input[crawling];at<int>(player,0xE18)=input[playerLock];at<short>(player,0xAA)=input[playerAa];at<unsigned char>(player,0x1AC)=input[synthesizedInput];at<void*>(player,0xE48)=input[heldItem]?items[input[heldItem]-1]:nullptr;
  mem<void*>(0x56F3B8)=camera;at<unsigned>(camera,0x2A0)=input[cameraMode];at<unsigned char>(camera,0xF9)=input[cameraFlag];at<unsigned char>(camera,0x180)=input[cameraCollision];at<int>(camera,0x2A4)=input[cameraSpin];at<int>(camera,0x2A8)=input[cameraRadius];mem<int>(0x548860)=input[cameraHorizontal];mem<int>(0x548864)=input[cameraVertical];mem<void*>(0x56E998)=input[globalBoss]?actor:nullptr;
  for(int i=0;i<8;i++){at<void*>(camera,0x184+i*4)=input[hiddenPattern]&(1<<i)?items[i]:nullptr;at<unsigned short>(items[i],4)=0xF800+i;}
  mem<int>(0x54D474)=input[difficulty];mem<int>(0x60D880)=input[musicPhase];mem<int>(0x60D86C)=input[musicClock];mem<unsigned char>(0x60D874)=input[trackEnding];mem<int>(0x6B4C7C)=input[seed];mem<int>(0x6B4B90)=input[multiplier];mem<int>(0x6B4B94)=input[increment];
  at<short>(jonah,0x38)=at<short>(cached,0x38)=316;at<unsigned short>(jonah,0xDC)=at<unsigned short>(cached,0xDC)=input[jonahRadius];at<Vector>(jonah,8)=at<Vector>(cached,8)={int(input[jonahX]),int(input[jonahY]),int(input[jonahZ])};mem<void*>(0x56E990)=input[jonahAvailable]?jonah:nullptr;
  if(input[hasProcedure]){at<void*>(procedure,0)=procedureVtable;at<int>(procedure,8)=input[procedureType];at<void*>(procedure,4)=actor;at<void*>(actor,0x28C)=procedure;}
  for(int i=0;i<messageCount;i++){int code,flags,param;Vector v0,v1;std::cin>>code>>flags>>param>>v0.x>>v0.y>>v0.z>>v1.x>>v1.y>>v1.z;at<void*>(messages[i],0)=messageVtable;at<int>(messages[i],20)=code;at<int>(messages[i],16)=flags;at<int>(messages[i],32)=param;at<Vector>(messages[i],40)=v0;at<Vector>(messages[i],52)=v1;at<void*>(messages[i],8)=at<void*>(actor,0x290);at<void*>(actor,0x290)=messages[i];}
  int result=reinterpret_cast<int(__attribute__((thiscall))*)(void*)>(0x488590)(actor);
  std::cout<<"{\"result\":"<<result<<",\"snapshot\":";numbers(snapshot());std::cout<<",\"tail\":";numbers(tailState());std::cout<<",\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';numbers(events[i]);}std::cout<<"]}\n";for(void*p:allocations)std::free(p);allocations.clear();
 }
}
'''


def initial(**changes):
    values = damage_initial()
    values.update(dict.fromkeys(EXTRA_FIELDS, 0))
    values.update(standingY=-31 << 12, groundY=69 << 12, environmentTimer=10, gloatTimer=20, timerC1C=30,
                  savedHorizontal=700, savedVertical=65535-127, lastFootstep=161, geometryRegion=255, glowRegion=255,
                  cameraMode=3, cameraHorizontal=512, cameraVertical=-128, cameraSpin=50, cameraRadius=100, hiddenPattern=255,
                  musicClock=100, trapHandle=1, tugHandle=1, trapValid=1, tugValid=1, targetValid=1,
                  procedureState=2, procedureSubstate=0, procedureType=0x20000, impactAccepted=1,
                  groundResult=69 << 12, radius=175, jonahRadius=100, jonahX=-1480 << 12, jonahY=-31 << 12, jonahZ=839 << 12, cameraCollision=1)
    values.update(changes)
    return values


def message(code, flags=0, parameter=123, vector0=None, vector1=None):
    return dict(code=code, flags=flags, parameter=parameter, vector0=vector0 or [100001, -200002, 300003],
                vector1=vector1 or [-400004, 500005, -600006])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    cases = []
    for state in range(23):
        for substate in [0, 1, 4, 666]:
            for signals in [0, 4, 8, 0x40, 0x80D]:
                cases.append(dict(input=initial(state=state, substate=substate, signals=signals), messages=[]))
    for crawling in [0, 1]:
        for camera_enabled in [0, 1]:
            for lock in [0, 1, -1]:
                for saved in [0, 1]:
                    for normal_y in [3400, 3401]:
                        cases.append(dict(input=initial(crawling=crawling, cameraEnabled=camera_enabled, playerLock=lock,
                                                       distancesSaved=saved, playerAa=normal_y, heldItem=2, hiddenPattern=0b1111011), messages=[]))
    for phase in [0, 1, 2, 3, -1]:
        for target in [0, 1, 2, 3]:
            for ending in [0, 1]:
                cases.append(dict(input=initial(state=2, musicPhase=phase, targetKind=target, trackEnding=ending), messages=[]))
    for code in [0, 5, 6, 8, 9, 14, 15, 99]:
        for state in [1, 2, 3, 10, 12, 16, 17, 20, 21]:
            for flags in [0, 2]:
                cases.append(dict(input=initial(state=state, reactionFlags=flags), messages=[message(code)]))
    for mutation in range(9):
        for state in [0, 2, 3, 9, 13, 19]:
            cases.append(dict(input=initial(state=state, substate=0, brainMutation=mutation, frame=2, signals=0x801),
                              messages=[message(9), message(14)]))
    for accepted in [0, 1]:
        for valid in [0, 1]:
            for marker in [0, 1]:
                cases.append(dict(input=initial(state=3, targetValid=valid, targetHandle=marker, lookAtAccepted=accepted), messages=[]))
                cases.append(dict(input=initial(state=9, substate=0, lookAtAccepted=accepted, handleAccepted=valid), messages=[]))
    rng = random.Random(0x488590)
    for _ in range(180):
        values = initial(state=rng.randrange(23), substate=rng.choice([0, 1, 4, 665, 666]), elapsedTicks=rng.randrange(7),
                         signals=rng.randrange(0x2000), behaviorFlags=rng.randrange(2**32), reactionFlags=rng.randrange(8),
                         targetKind=rng.randrange(4), targetValid=rng.randrange(2), hasProcedure=rng.randrange(2),
                         procedureType=rng.choice([0x20000, 0x101]), procedureState=rng.randrange(23),
                         procedureSubstate=rng.choice([0, 1, 4]), procedureRetire=rng.randrange(2),
                         cameraMode=rng.choice([3, 7, 16]), cameraEnabled=rng.randrange(2), crawling=rng.randrange(2), cameraCollision=rng.randrange(2),
                         playerLock=rng.randrange(2), heldItem=rng.randrange(9), hiddenPattern=rng.randrange(256),
                         cameraHorizontal=rng.choice([-32768, -1, 0, 512, 65535, 2147483647]),
                         cameraVertical=rng.choice([-32768, -1, 0, 32767, 65535]),
                         musicPhase=rng.choice([-1, 0, 1, 2, 3]), trackEnding=rng.randrange(2), brainMutation=rng.randrange(9),
                         cachedJonah=rng.randrange(2), jonahAvailable=rng.randrange(2), trapValid=rng.randrange(2), tugValid=rng.randrange(2),
                         synthesizedInput=rng.randrange(2), impactAccepted=rng.randrange(2), lookAtAccepted=rng.randrange(2),
                         frame=rng.choice([0, 2, 17, 31]), clip=rng.choice([0, 2, 10]), slideUpdates=rng.choice([0, 1, 2, 40]),
                         groundResult=rng.choice([-1, 0, 300000]), seed=rng.randint(-2147483648, 2147483647))
        cases.append(dict(input=values, messages=[message(rng.choice([0, 5, 6, 8, 9, 14, 15, 99]), parameter=rng.randint(-2147483648, 2147483647),
                            vector0=[rng.randint(-2147483648, 2147483647) for _ in range(3)],
                            vector1=[rng.randint(-2147483648, 2147483647) for _ in range(3)]) for _ in range(rng.randrange(5))]))
    rows = []
    for case in cases:
        row = [case['input'][name] for name in FIELDS] + [len(case['messages'])]
        for m in case['messages']:
            row += [m['code'], m['flags'], m['parameter'], *m['vector0'], *m['vector1']]
        rows.append(row)
    with tempfile.TemporaryDirectory(prefix='openspidey-scorpion-brain-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(HARNESS.replace('GENERATED_FIELDS', ', '.join(FIELDS)))
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in rows) + '\n').encode()).decode().splitlines()
    for case, line in zip(cases, lines, strict=True):
        case['expected'] = json.loads(line)
    (root / 'modern/OpenSpidey.Tests/Fixtures/scorpion-brain.json').write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole PC488590 outer AI visits with genuine404C50 physics,402BE0 timers,403160 state cache,4040E0 neutralization, source message/procedure cleanup,4866E0 recovery and488370/489660 tail math. Camera/asset/audio/footprint and13 unported behavior bodies remain explicitly scoped host transports.')


if __name__ == '__main__':
    main()
