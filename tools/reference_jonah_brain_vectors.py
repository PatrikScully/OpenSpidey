"""Capture whole PC Jonah Init/Plead/Fret/AI; replace only explicit host transports.

Original timers, neutralization, state flags, speech selection, animation-mode decisions,
message filtering and dispatch remain executable. Animation calls are observable host
transports; movement/hide/hit/death/collision/shadow leaves are required opaque callbacks.
This fixture does not claim complete boss AI or native actor integration.
"""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


HARNESS = r'''
#include "original_executable.h"
#include <iostream>
#include <cmath>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
template<class T>T&mem(unsigned a){return *reinterpret_cast<T*>(a);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
void omit(unsigned a,int n){std::memset((void*)a,0x90,n);}
alignas(4)unsigned char actor[0x380],scorp[0xC28],player[0xEFC],procedure[64],messages[3][68];
void*procedureVtable[2];void*messageVtable[1];
unsigned char neutralBytes[5],cleanupBytes[5],flagsBytes[5];
std::vector<std::vector<long long>>events;int hasScorp,valid,handlePointer,target,victorious,contact,allocation,randomValue,ground,path,blocks,addFirst,addSecond,addCalls;
int procedureState,procedureSub,procedureRetire;unsigned clockValue;int messageCount;
std::vector<long long>snapshot(){std::vector<long long>v;
 for(unsigned o:{0x31Cu,0x320u,0x1F8u,0x328u,0x32Cu,0x334u,0x338u,0x358u})v.push_back(at<int>(actor,o));
 for(unsigned o:{0x218u,0x2A8u,0x294u,0x298u,0x378u})v.push_back(at<unsigned>(actor,o));
 for(unsigned o:{0x37Cu,0x29Cu,0x2A0u,8u,12u,16u,0x34Cu,0x350u,0x354u})v.push_back(at<int>(actor,o));
 v.push_back(at<unsigned short>(actor,0x12A));v.push_back(at<short>(actor,0x128));v.push_back(at<unsigned char>(actor,0x142));
 v.push_back(at<unsigned short>(actor,0x44));v.push_back(at<short>(actor,0x314));v.push_back(at<unsigned>(actor,0x288));
 for(unsigned o:{0x60u,0x64u,0x68u,0x6Cu,0x70u,0x74u,0x27Cu,0x280u,0x284u})v.push_back(at<int>(actor,o));
 for(unsigned o:{0x88u,0x8Au,0x8Cu,0x8Eu,0x90u,0x92u})v.push_back(at<short>(actor,o));
 v.push_back(at<void*>(actor,0x35C)==scorp?1:0);v.push_back(at<unsigned>(actor,0x360));
 for(unsigned i=0;i<3;i++)v.push_back(at<int>(messages[i],16));
 int count=0;for(void*m=at<void*>(actor,0x290);m;m=at<void*>(m,8))count++;v.push_back(count);
 v.push_back(at<void*>(actor,0x28C)?1:0);v.push_back(at<int>(procedure,16));return v;}
void event(int kind,std::initializer_list<long long>args={}){std::vector<long long>v={kind};v.insert(v.end(),args);auto s=snapshot();v.insert(v.end(),s.begin(),s.end());events.push_back(v);}
void*recover(void*handle){event(1);return valid&&at<void*>(handle,0)==scorp?scorp:nullptr;}
unsigned long long makeHandle(void*p){assert(p==scorp);event(2);valid=1;return (static_cast<unsigned long long>(321)<<32)|(unsigned)p;}
void module(const char*name,int slot,unsigned*arguments,void**result){assert(!strcmp(name,"scorpion")&&slot==1&&*arguments==(unsigned)scorp);event(3);*result=target?actor:nullptr;}
int __attribute__((thiscall)) winner(void*){event(4);return victorious;}
void*allocate(unsigned size){assert(size==64||size==68);return allocation?procedure:nullptr;}
void* __attribute__((thiscall))look(void*p,void*self,void*body,void*point,int mask,int rate,int tolerance){assert(p==procedure&&self==actor&&body==scorp&&point==nullptr);event(5,{mask,rate,tolerance});return p;}
int rnd(int maximum){event(6,{maximum});return randomValue%maximum;}
void __attribute__((thiscall))run(void*,unsigned short clip,int start,int end){event(7,{clip,start,end});at<unsigned short>(actor,0x12A)=clip;at<short>(actor,0x128)=start;at<unsigned char>(actor,0x142)=0;}
void __attribute__((thiscall))cycle(void*,unsigned short clip,signed char direction){event(8,{clip,direction});at<unsigned short>(actor,0x12A)=clip;at<short>(actor,0x128)=0;at<unsigned char>(actor,0x142)=0;}
int voice(int bank,int index,void*position,int flags){assert(position==actor+8);event(9,{bank,index,flags});return 0;}
int sound(unsigned id,void*position,int pitch){assert(position==actor+8);event(10,{id,pitch});return 0;}
int groundHeight(void*position,int above,int below,int flags){assert(position==actor+8);event(11,{above,below,flags});return ground;}
void waypoint(int*position,int node){event(12,{node});position[0]=1000001;position[1]=-2000002;position[2]=3000003;}
int __attribute__((thiscall))trace(void*,int*start,int*end,int*hit,int clearance){event(13,{start[0],start[1],start[2],end[0],end[1],end[2],clearance});
 hit[0]=start[0]+(path==2?600000:100000);hit[1]=start[1];hit[2]=start[2];return path;}
void*objectCollision(int*start,int*end,void*body,void*ignore){assert(body==scorp&&ignore==actor);event(14,{start[0],start[1],start[2],end[0],end[1],end[2]});return blocks?scorp:nullptr;}
int __attribute__((thiscall))addPath(void*,int*position,int flags){event(15,{position[0],position[1],position[2],flags});return addCalls++?addSecond:addFirst;}
void __attribute__((thiscall))doPhysics(void*,int limitElapsed){event(16,{limitElapsed});}
void transform(void*){event(17);}
void __attribute__((thiscall))movementHeight(void*,int flags,int clearance,int distance){event(18,{flags,clearance,distance});}
void __attribute__((thiscall))follow(void*){event(19);}
void __attribute__((thiscall))hide(void*){event(20);}
void __attribute__((thiscall))takeHit(void*){event(21);}
void __attribute__((thiscall))die(void*){event(22);}
void __attribute__((thiscall))goAround(void*,void*body){assert(body==scorp);event(23);}
void __attribute__((thiscall))shadow(void*){event(24);}
void* __attribute__((thiscall))sendMessage(void*p,void*from,void*to,int code,unsigned*data){assert(p==procedure&&from==actor&&to==scorp&&code==9&&data[1]==0&&data[2]==2);
 int*position=(int*)data[5],*target=(int*)data[6];event(25,{code,data[2],position[0],position[1],position[2],target[0],target[1],target[2]});return p;}
void diagnostic(int ok,const char*){if(!ok)event(26);}
void __attribute__((thiscall))execute(void*){event(27);at<int>(actor,0x31C)=procedureState;at<int>(actor,0x320)=procedureSub;at<int>(actor,16)-=23;
 if(procedureRetire)at<int>(procedure,16)|=1;}
void __attribute__((thiscall))deleteProcedure(void*,int){at<void*>(actor,0x28C)=nullptr;}
void __attribute__((thiscall))deleteMessage(void*p,int){void*previous=nullptr;for(void*m=at<void*>(actor,0x290);m;m=at<void*>(m,8)){
 if(m==p){if(previous)at<void*>(previous,8)=at<void*>(m,8);else at<void*>(actor,0x290)=at<void*>(m,8);return;}previous=m;}assert(false);}
void __attribute__((thiscall))neutral(void*p){std::memcpy((void*)0x4040E0,neutralBytes,5);reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x4040E0)(p);patch(0x4040E0,(void*)&neutral);}
void numbers(const std::vector<long long>&v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Omit only Windows FS exception-registration transports, preserving each original stack layout.
 for(unsigned a:{0x444CF0u,0x445B80u}){omit(a,6);mem<unsigned short>(a)=0xC031;}
 for(unsigned a:{0x444CFEu,0x444DBAu,0x444F43u,0x444F6Du,0x444FA0u,0x444FCAu,0x445176u,0x4451ABu,0x4451FDu,0x4452E1u,0x445303u,0x445B8Eu,0x4460E7u})omit(a,7);
 patch(0x4583F0,(void*)&recover);patch(0x458360,(void*)&makeHandle);patch(0x47D470,(void*)&module);patch(0x455390,(void*)&allocate);
 patch(0x401180,(void*)&look);patch(0x4E5DA0,(void*)&rnd);patch(0x460C10,(void*)&run);patch(0x460D00,(void*)&cycle);
 patch(0x479D30,(void*)&voice);patch(0x471EA0,(void*)&sound);patch(0x4E6840,(void*)&groundHeight);patch(0x4E3940,(void*)&waypoint);
 patch(0x403310,(void*)&trace);patch(0x4E6470,(void*)&objectCollision);patch(0x4048F0,(void*)&addPath);patch(0x404C50,(void*)&doPhysics);
 patch(0x475F50,(void*)&transform);patch(0x4041C0,(void*)&movementHeight);patch(0x445330,(void*)&follow);patch(0x446130,(void*)&hide);
 patch(0x4457B0,(void*)&takeHit);patch(0x445950,(void*)&die);patch(0x446770,(void*)&goAround);patch(0x446980,(void*)&shadow);
 patch(0x458F10,(void*)&sendMessage);patch(0x4015B0,(void*)&diagnostic);std::memcpy(neutralBytes,(void*)0x4040E0,5);patch(0x4040E0,(void*)&neutral);
 // The PC fills its heading table before gameplay; reproduce that initialization from the existing source table.
 for(int i=0;i<4096;i++){mem<short>(0x610C48+i*4)=(short)(std::cos(i*0.001536096911877394)*4096.0);mem<short>(0x610C4A+i*4)=(short)(std::sin(i*0.001536096911877394)*4096.0);}
 void*scorpionVtable[19]={};scorpionVtable[18]=(void*)&winner;procedureVtable[0]=(void*)&deleteProcedure;procedureVtable[1]=(void*)&execute;messageVtable[0]=(void*)&deleteMessage;
 int method,state,sub,wait,suppress,panic,last,regular,clip,frame,finished,collision,mode,elapsed,clearance,hasProcedure,synth;unsigned signals,behavior,timestamp;
 while(std::cin>>method>>state>>sub>>wait>>suppress>>panic>>last>>signals>>behavior>>timestamp>>regular>>clip>>frame>>finished>>collision>>mode>>elapsed>>clockValue
 >>hasScorp>>valid>>handlePointer>>target>>victorious>>contact>>allocation>>randomValue>>ground>>path>>blocks>>addFirst>>addSecond>>hasProcedure>>procedureState>>procedureSub>>procedureRetire>>synth>>clearance){
  std::memset(actor,0,sizeof actor);std::memset(scorp,0,sizeof scorp);std::memset(player,0,sizeof player);std::memset(procedure,0,sizeof procedure);std::memset(messages,0,sizeof messages);events.clear();addCalls=0;
  at<int>(actor,0x31C)=state;at<int>(actor,0x320)=sub;at<int>(actor,0x1F8)=wait;at<int>(actor,0x328)=suppress;at<int>(actor,0x32C)=panic;
  at<int>(actor,0x334)=17;at<int>(actor,0x338)=-19;at<int>(actor,0x358)=last;at<unsigned>(actor,0x218)=signals;at<unsigned>(actor,0x2A8)=behavior;
  at<unsigned>(actor,0x294)=0x02020202;at<unsigned>(actor,0x298)=2|(mode<<8);at<unsigned>(actor,0x378)=timestamp;at<int>(actor,0x37C)=regular;
  at<int>(actor,0x29C)=111;at<int>(actor,0x2A0)=222;at<int>(actor,8)=100001;at<int>(actor,12)=-200002;at<int>(actor,16)=300003;
  at<int>(actor,0x34C)=333;at<int>(actor,0x350)=444;at<int>(actor,0x354)=555;
  at<unsigned short>(actor,0x12A)=clip;at<short>(actor,0x128)=frame;at<unsigned char>(actor,0x142)=finished;at<unsigned short>(actor,0x44)=collision;
  at<unsigned>(actor,0x288)=0xABCDEF09;at<short>(actor,0x21E)=clearance;at<int>(actor,0x1F4)=15;at<int>(actor,0x80)=elapsed;
  for(unsigned o:{0x60u,0x64u,0x68u,0x6Cu,0x70u,0x74u,0x27Cu,0x280u,0x284u})at<int>(actor,o)=o*101;
  for(unsigned o:{0x88u,0x8Au,0x8Cu,0x8Eu,0x90u,0x92u})at<short>(actor,o)=o-140;
  at<void*>(actor,0x35C)=handlePointer?scorp:nullptr;at<unsigned>(actor,0x360)=valid?321:999;
  at<void*>(scorp,0)=scorpionVtable;at<short>(scorp,0x38)=310;at<unsigned char>(scorp,0xBDC)=contact;
  mem<void*>(0x56E990)=hasScorp?scorp:nullptr;mem<unsigned>(0x6B4CA0)=clockValue;mem<void*>(0x6A9038)=player;at<unsigned char>(player,0x1AC)=synth;
  if(hasProcedure){at<void*>(procedure,0)=procedureVtable;at<unsigned>(procedure,8)=0x20000;at<void*>(actor,0x28C)=procedure;}
  if(method==3){for(int i=2;i>=0;i--){at<void*>(messages[i],0)=messageVtable;at<void*>(messages[i],8)=at<void*>(actor,0x290);at<int>(messages[i],16)=i*4;at<int>(messages[i],20)=i==1?8:20+i;at<void*>(actor,0x290)=messages[i];}}
  auto fn=method==0?0x446340:method==1?0x444CF0:method==2?0x445B80:0x446470;
  reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(fn)(actor);
  std::cout<<"{\"state\":";numbers(snapshot());std::cout<<",\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';numbers(events[i]);}std::cout<<"]}\n";
 }
}
'''


FIELDS = ['method', 'state', 'substate', 'wait', 'suppression', 'panic', 'lastSound', 'signals', 'behavior',
          'pleaWord', 'regularVoice', 'clip', 'frame', 'finished', 'collision', 'mode', 'elapsed', 'clock',
          'hasScorpion', 'validHandle', 'handlePointer', 'isTarget', 'victorious', 'contact', 'allocation', 'random', 'ground',
          'trace', 'blocked', 'addFirst', 'addSecond', 'hasProcedure', 'procedureState', 'procedureSubstate',
          'procedureRetire', 'synthesizing', 'clearance']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x444CF0)
    cases = []
    for method, state, substates in [(0, 0, [0, 1, 2, 3]), (1, 3, [0, 1, 2, 5, 6, 3]),
                                     (2, 4, [0, 1, 2, 5, 7, 10, 11, 3]),
                                     (3, None, [0, 1, 2])]:
        for _ in range(360):
            initial_state = state if state is not None else rng.choice(range(10))
            case = dict(zip(FIELDS, [method, initial_state, rng.choice(substates), rng.choice([-2, 0, 1, 2, 9, 10, 11, 120, 550]),
                        rng.choice([0, 1, 2, 700]), rng.choice([0, 1, 2, 120]), rng.choice([0, 173, 174, 175, 176]),
                        rng.choice([0, 0x40, 0x100, 0x200, 0x400, 0x800, 0x1000, 0x2000, 0x3202, 0xFFFFFFFF]),
                        rng.choice([0, 4, 0xFFFFFFFF]), rng.choice([0, 7, 8, 9, 10, 300, 400, 0xFFFFFF00]),
                        rng.choice([0, 4, 5, 6]), rng.choice([2, 6, 8, 10, 20, 25]), rng.choice([-32768, -1, 0, 4, 14, 32767]),
                        rng.randrange(2), rng.choice([0, 1, 2, 0xFFFF]), rng.choice([0, 1, 2]), rng.choice([0, 1, 2, 6]),
                        rng.choice([0, 7, 8, 10, 300, 301, 307, 400, 701, 0xFFFFFFFE]),
                        rng.randrange(2), rng.randrange(2), rng.randrange(2), rng.randrange(2), rng.randrange(2), rng.randrange(2), rng.randrange(2),
                        rng.randrange(4096), rng.choice([-1, 0, 1000000, -1000000]), rng.choice([0, 1, 2]), rng.randrange(2),
                        rng.randrange(2), rng.randrange(2), 0 if method != 3 else rng.randrange(2), rng.choice(range(10)),
                        rng.choice([0, 1, 2, 5, 6, 7, 10, 11]), rng.randrange(2), rng.randrange(2), rng.choice([0, 100, -100])], strict=True))
            cases.append(case)
    # Explicit complete dispatch and message8 guard boundaries; every callback stage occurs in the capture.
    base = cases[0].copy()
    for state in range(10):
        for substate in [0, 1]:
            for contact in [0, 1]:
                c = base | dict(method=3, state=state, substate=substate, hasScorpion=1, validHandle=1, handlePointer=1,
                                isTarget=1, contact=contact, hasProcedure=0, synthesizing=1, mode=1)
                cases.append(c)
    with tempfile.TemporaryDirectory(prefix='openspidey-jonah-brain-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(HARNESS)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = [' '.join(str(c[field]) for field in FIELDS) for c in cases]
        output = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(rows) + '\n').encode()).decode().splitlines()
    for case, line in zip(cases, output, strict=True):
        case['expected'] = json.loads(line)
    (root / 'modern/OpenSpidey.Tests/Fixtures/jonah-brain.json').write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole PC Jonah decision/frame cases with source host transport ordering.')


if __name__ == '__main__':
    main()
