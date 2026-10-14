"""Capture whole488140/487FC0/487880 against PC, actual Jonah module1 and source look-at/math.

Only original heap, weak-handle registry, module transport, visibility and path-world
queries are replaced. Native module decision444390 and fixed-distance/vector arithmetic
run unchanged. Local Jonah hiding-handle copies and current-handle aliasing are observed.
"""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


FIELDS = ['method', 'state', 'substate', 'playerTicks', 'environmentTicks', 'targetKind', 'currentPointer', 'currentValidation',
          'currentValid', 'hasJonah', 'cachedJonah', 'jonahState', 'hidingPointer', 'hidingValidation', 'hidingValid',
          'signals', 'px', 'py', 'pz', 'aimX', 'aimY', 'aimZ', 'completion', 'behavior', 'slideTicks', 'elapsed',
          'seed', 'multiplier', 'increment', 'lookAtAccepted', 'makeAccepted', 'mutation']

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
alignas(4)unsigned char actor[0xC28],player[0xF20],jonah[0x380],middle[0x400],bodies[8][0x400];
struct Query{int type,visible,path;Vector position,contact;};std::array<Query,8>query;int count;
std::vector<void*>allocations;std::vector<std::vector<long long>>events;unsigned char randomBytes[5],lookAtBytes[5];bool constructing=false;
int identity(void*p){if(p==player)return 1;if(p==jonah)return 2;for(int i=0;i<count;i++)if(p==bodies[i])return i+3;return 0;}
void*body(int id){if(id==1)return player;if(id==2)return jonah;if(id>=3&&id<count+3)return bodies[id-3];return nullptr;}
void append(std::vector<long long>&v,Vector p){v.insert(v.end(),{p.x,p.y,p.z});}
std::vector<long long>snapshot(){std::vector<long long>v={at<int>(actor,0x31C),at<int>(actor,0x320),at<int>(actor,0xC10),at<int>(actor,0xC14),at<int>(actor,0xBF8),identity(at<void*>(actor,0xBF0)),at<unsigned>(actor,0xBF4),
 identity(at<void*>(actor,0xBEC)),at<unsigned>(actor,0x218),at<unsigned>(actor,0x288),at<unsigned>(actor,0x2A8),at<int>(actor,0x1F8),at<short>(actor,0x314),
 identity(at<void*>(jonah,0x364)),at<unsigned>(jonah,0x368),at<int>(jonah,0x31C),mem<void*>(0x56E990)?1:0,mem<int>(0x6B4C7C),mem<int>(0x6B4B90),mem<int>(0x6B4B94)};
 append(v,at<Vector>(actor,8));append(v,at<Vector>(actor,0xC00));for(int i=0;i<count;i++)append(v,at<Vector>(bodies[i],8));
 std::vector<void*>procs;for(void*p=at<void*>(actor,0x28C);p;p=at<void*>(p,0x1C))procs.push_back(p);v.push_back(procs.size());for(void*p:procs)for(unsigned o:{8u,0x10u,0x14u,0xCu,0x38u,0x3Cu})v.push_back(at<int>(p,o));return v;}
void event(int kind,std::initializer_list<long long>args={}){std::vector<long long>v={kind};v.insert(v.end(),args);auto s=snapshot();v.insert(v.end(),s.begin(),s.end());events.push_back(v);}
int next(int bound){std::memcpy((void*)0x4E5DA0,randomBytes,5);int result=reinterpret_cast<int(*)(int)>(0x4E5DA0)(bound);patch(0x4E5DA0,(void*)&next);event(0,{bound,result});return result;}
void*recover(void*handle){int role=handle==actor+0xBF0?0:1;event(1,{role,identity(at<void*>(handle,0)),at<unsigned>(handle,4)});bool valid=input[role==0?currentValid:hidingValid]!=0;if(!valid)at<void*>(handle,0)=nullptr;
 event(2,{role,identity(at<void*>(handle,0)),at<unsigned>(handle,4)});return valid?at<void*>(handle,0):nullptr;}
unsigned long long make(void*p){int id=identity(p);assert(id>0);if(!constructing){event(3,{id});if(input[mutation]==1){for(void*target:std::initializer_list<void*>{p,bodies[0]}){auto&pos=at<Vector>(target,8);pos.x+=4096;pos.y-=8192;pos.z+=12288;}}}
 return input[makeAccepted]?((unsigned long long)(100+id)<<32)|(unsigned)p:0;}
void module(const char*name,int slot,unsigned*stack,unsigned*result){assert(!strcmp(name,"jonah")&&slot==1&&*stack==(unsigned)jonah);event(4);reinterpret_cast<void(*)(unsigned*,unsigned*)>(0x444390)(stack,result);
 if(input[mutation]==2){at<void*>(actor,0xBEC)=nullptr;mem<void*>(0x56E990)=nullptr;}else if(input[mutation]==3)at<int>(actor,0xC10)=900;}
void*allocate(unsigned bytes){assert(bytes==64);event(5,{bytes,input[lookAtAccepted]});if(!input[lookAtAccepted])return nullptr;void*p=std::calloc(1,bytes);assert(p);allocations.push_back(p);return p;}
void* __attribute__((thiscall))lookAt(void*p,void*self,void*target,void*point,int mask,int rate,int tolerance){assert(self==actor&&point==nullptr&&target!=nullptr);event(6,{identity(target),mask,rate,tolerance});
 std::memcpy((void*)0x401180,lookAtBytes,5);constructing=true;void*r=reinterpret_cast<void*(__attribute__((thiscall))*)(void*,void*,void*,void*,int,int,int)>(0x401180)(p,self,target,point,mask,rate,tolerance);
 constructing=false;patch(0x401180,(void*)&lookAt);return r;}
int bodyIndex(const int*position){for(int i=0;i<count;i++)if(position==(int*)(bodies[i]+8))return i;assert(false);return 0;}
int visible(int*start,int*end,int flags,int argument){assert(start==(int*)(actor+8)&&flags==0&&argument==1);int index=bodyIndex(end);event(7,{index+3});return query[index].visible;}
int __attribute__((thiscall))path(void*p,int*start,int*end,int*contact,int clearance){assert(p==actor&&clearance==20);int index=-1;for(int i=0;i<count;i++){Vector pos=at<Vector>(bodies[i],8);if(end[0]==pos.x&&end[2]==pos.z){index=i;break;}}assert(index>=0);
 event(8,{index+3,start[0],start[1],start[2],end[0],end[1],end[2],clearance});*reinterpret_cast<Vector*>(contact)=query[index].contact;return query[index].path;}
void diagnostic(int condition,const char*message){if((condition&255)==0){assert(!strcmp(message,"Unknown substate!"));event(9);}}
void numbers(const std::vector<long long>&v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);for(unsigned a:{0x487FC7u,0x401187u}){omit(a,6);mem<unsigned short>(a)=0xC031;}
 for(unsigned a:{0x487FCEu,0x488004u,0x488047u,0x488082u,0x48812Cu,0x40118Eu,0x4012D5u})omit(a,7);
 std::memcpy(randomBytes,(void*)0x4E5DA0,5);patch(0x4E5DA0,(void*)&next);std::memcpy(lookAtBytes,(void*)0x401180,5);patch(0x401180,(void*)&lookAt);
 patch(0x4583F0,(void*)&recover);patch(0x458360,(void*)&make);patch(0x47D470,(void*)&module);patch(0x455390,(void*)&allocate);patch(0x4E67A0,(void*)&visible);patch(0x403310,(void*)&path);patch(0x4015B0,(void*)&diagnostic);
 while(std::cin>>input[0]){for(int i=1;i<FieldCount;i++)std::cin>>input[i];std::cin>>count;assert(count>=1&&count<=8);
  for(void*p:std::initializer_list<void*>{actor,player,jonah,middle})std::memset(p,0,p==actor?sizeof(actor):p==player?sizeof(player):p==jonah?sizeof(jonah):sizeof(middle));std::memset(bodies,0,sizeof(bodies));events.clear();
  at<int>(actor,0x31C)=input[state];at<int>(actor,0x320)=input[substate];at<int>(actor,0xC10)=input[playerTicks];at<int>(actor,0xC14)=input[environmentTicks];at<int>(actor,0xBF8)=input[targetKind];at<void*>(actor,0xBF0)=body(input[currentPointer]);at<unsigned>(actor,0xBF4)=input[currentValidation];at<void*>(actor,0xBEC)=input[cachedJonah]?jonah:nullptr;
  at<unsigned>(actor,0x218)=input[signals];at<unsigned>(actor,0x288)=input[completion];at<unsigned>(actor,0x2A8)=input[behavior];at<int>(actor,0x1F8)=input[slideTicks];at<int>(actor,0x80)=input[elapsed];at<short>(actor,0x21E)=100;
  at<Vector>(actor,8)={int(input[px]),int(input[py]),int(input[pz])};at<Vector>(actor,0xC00)={int(input[aimX]),int(input[aimY]),int(input[aimZ])};
  at<short>(jonah,0x38)=316;at<int>(jonah,0x31C)=input[jonahState];at<void*>(jonah,0x364)=input[hidingPointer]?bodies[0]:nullptr;at<unsigned>(jonah,0x368)=input[hidingValidation];at<short>(middle,0x38)=304;at<void*>(middle,0x20)=jonah;
  mem<void*>(0x56E990)=input[hasJonah]?middle:nullptr;mem<void*>(0x6A9038)=player;mem<int>(0x6B4C7C)=input[seed];mem<int>(0x6B4B90)=input[multiplier];mem<int>(0x6B4B94)=input[increment];
  for(int i=0;i<count;i++){auto&q=query[i];std::cin>>q.type>>q.position.x>>q.position.y>>q.position.z>>q.visible>>q.path>>q.contact.x>>q.contact.y>>q.contact.z;at<short>(bodies[i],0x38)=q.type;at<Vector>(bodies[i],8)=q.position;at<void*>(bodies[i],0x20)=i<count-1?bodies[i+1]:nullptr;}
  mem<void*>(0x60DAAC)=bodies[0];int result=0;if(input[method]==0)reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x488140)(actor);else if(input[method]==1)reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x487FC0)(actor);else result=reinterpret_cast<int(__attribute__((thiscall))*)(void*)>(0x487880)(actor);
  std::cout<<"{\"result\":"<<result<<",\"snapshot\":";numbers(snapshot());std::cout<<",\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';numbers(events[i]);}std::cout<<"]}\n";for(void*p:allocations)std::free(p);allocations.clear();
 }
}
'''


def initial(**changes):
    values = dict.fromkeys(FIELDS, 0)
    values.update(state=2, substate=4, targetKind=1, currentPointer=3, currentValidation=999, currentValid=1,
                  hasJonah=1, jonahState=5, hidingPointer=1, hidingValidation=777, hidingValid=1,
                  completion=0xFFFFFFFF, behavior=0xFFFFFFFF, slideTicks=600, elapsed=2,
                  px=-1735 << 12, py=-31 << 12, pz=443 << 12, aimX=123, aimY=456, aimZ=789,
                  seed=1, multiplier=0x12B9B0A1, increment=0x0AA2FB3F, lookAtAccepted=1, makeAccepted=1)
    values.update(changes)
    return values


def bodies():
    return [dict(type=401 if index != 2 else 404, position=[(-1300 + index * 200) << 12, -31 << 12, (500 + index * 100) << 12],
                 visible=1, path=0, contact=[(-1300 + index * 200) << 12, -31 << 12, (500 + index * 100) << 12]) for index in range(8)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    cases = []
    for state in [0, 2, 3, 13, 16]:
        for player_ticks in [-1, 0, 1, 179, 180, 600]:
            for jonah_state in [0, 5, 8]:
                for current_valid in [0, 1]:
                    cases.append(dict(input=initial(state=state, playerTicks=player_ticks, jonahState=jonah_state, currentValid=current_valid), bodies=bodies()))
    for method in [0, 1, 2]:
        for has_jonah in [0, 1]:
            for cached in [0, 1]:
                for hiding in [0, 1]:
                    for valid in [0, 1]:
                        cases.append(dict(input=initial(method=method, hasJonah=has_jonah, cachedJonah=cached, hidingPointer=hiding, hidingValid=valid), bodies=bodies()))
    for substate in [-1, 0, 1, 2, 4]:
        for completed in [0, 2, 0xFFFFFFFF]:
            for remaining in [-1, 0, 1, 2, 599, 600]:
                cases.append(dict(input=initial(method=1, substate=substate, completion=completed, slideTicks=remaining), bodies=bodies()))
    for signals in [0, 0x10, 0x20, 0x30, 0xFFFFFFFF]:
        for path in [0, 1, 2, 4]:
            for height in [0, 549 << 12, 550 << 12, 551 << 12]:
                data = bodies()
                for entry in data:
                    entry['path'] = path
                    entry['contact'] = [entry['position'][0], entry['position'][1] + height, entry['position'][2]]
                cases.append(dict(input=initial(method=2, signals=signals), bodies=data))
    for mutation in [1, 2, 3]:
        for method in [0, 1, 2]:
            cases.append(dict(input=initial(method=method, mutation=mutation, substate=0, jonahState=0), bodies=bodies()))
    for accepted in [0, 1]:
        cases.append(dict(input=initial(method=1, substate=0, lookAtAccepted=accepted), bodies=bodies()))
        # A null body target with accepted LookAt heap storage is an undefined original null-point dereference.
        cases.append(dict(input=initial(method=1, substate=0, lookAtAccepted=0, makeAccepted=accepted), bodies=bodies()))
    rng = random.Random(0x488140)
    for _ in range(200):
        method = rng.randrange(3)
        values = initial(method=method, state=rng.choice([0, 2, 3, 9, 13, 16]), substate=rng.choice([0, 1, 2, 4]),
                         playerTicks=rng.choice([-1, 0, 1, 200]), environmentTicks=rng.randrange(2), targetKind=rng.randrange(4),
                         currentPointer=rng.choice([0, 1, 2, 3]), currentValidation=rng.randrange(2**32), currentValid=rng.randrange(2),
                         hasJonah=rng.randrange(2), cachedJonah=rng.randrange(2), jonahState=rng.choice([0, 1, 2, 5, 8]),
                         hidingPointer=rng.randrange(2), hidingValidation=rng.randrange(2**32), hidingValid=rng.randrange(2),
                         signals=rng.randrange(2**32), completion=rng.randrange(2**32), mutation=rng.randrange(4),
                         seed=rng.randint(-2147483648, 2147483647), lookAtAccepted=rng.randrange(2), slideTicks=rng.randint(-10, 610))
        data = bodies()[:rng.randrange(1, 9)]
        for entry in data:
            entry['type'] = rng.choice([401, 401, 404])
            entry['position'] = [rng.randint(-20000000, 20000000) for _ in range(3)]
            entry['visible'] = rng.randrange(2)
            entry['path'] = rng.choice([0, 1, 2, 4])
            entry['contact'] = [rng.randint(-20000000, 20000000) for _ in range(3)]
        cases.append(dict(input=values, bodies=data))
    rows = []
    for case in cases:
        row = [case['input'][name] for name in FIELDS] + [len(case['bodies'])]
        for entry in case['bodies']:
            row += [entry['type'], *entry['position'], entry['visible'], entry['path'], *entry['contact']]
        rows.append(row)
    with tempfile.TemporaryDirectory(prefix='openspidey-scorpion-target-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(HARNESS.replace('GENERATED_FIELDS', ', '.join(FIELDS)))
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in rows) + '\n').encode()).decode().splitlines()
    for case, line in zip(cases, lines, strict=True):
        case['expected'] = json.loads(line)
    (root / 'modern/OpenSpidey.Tests/Fixtures/scorpion-targets.json').write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole PC target/intro/environment calls with genuine Jonah module1, two local hiding-handle recoveries, original LookAt constructor,3D distance and post-handle average; no production actor stand-ins.')


if __name__ == '__main__':
    main()
