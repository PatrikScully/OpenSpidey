"""Capture whole PC Scorpion Hit/SlideFromHit/ScorpPathCheck/TakeHit and genuine shared math/procedures."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


FIELDS = [
    'mode', 'difficulty', 'health', 'state', 'substate', 'behaviorFlags', 'completionFlags',
    'reactionContext', 'reactionFlags', 'field310', 'slideUpdates', 'hitFlashUpdates',
    'targetKind', 'targetUpdates', 'targetHandle', 'cachedJonah', 'lastFireQuip', 'quipSuppression',
    'clip', 'frame', 'waitUpdates', 'surfaceClearance', 'px', 'py', 'pz', 'vx', 'vy', 'vz',
    'ax', 'ay', 'az', 'lx', 'ly', 'lz', 'avx', 'avy', 'avz', 'aax', 'aay', 'aaz',
    'playerFlags', 'playerX', 'playerY', 'playerZ', 'hitFlags', 'hitPart', 'hitAttack', 'hitDamage',
    'hitX', 'hitY', 'hitZ', 'hitDistance', 'hitFrames', 'seed', 'multiplier', 'increment',
    'pathResult', 'contactX', 'contactY', 'contactZ', 'jonahAvailable', 'speechAccepted',
    'messageAccepted', 'handleAccepted', 'lookAtAccepted', 'elapsedTicks', 'slideDistance',
    'slideFrames', 'slideX', 'slideY', 'slideZ', 'mutation'
]


HARNESS = r'''
#include "original_executable.h"
#include <array>
#include <cstdlib>
#include <iostream>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
template<class T>T&mem(unsigned n){return *reinterpret_cast<T*>(n);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
void omit(unsigned a,int n){std::memset((void*)a,0x90,n);}
enum Field { GENERATED_FIELDS, FieldCount };
long long input[FieldCount];
alignas(4)unsigned char actor[0xC28],player[0xF20],other[0x400],jonah[0x400],cached[0x400],middle[0x400],hit[28];
std::array<std::array<unsigned char,64>,3> oldProcedures;
std::vector<void*> allocations;std::vector<std::vector<long long>> events;
unsigned char randomBytes[5];bool constructingLookAt=false;
int identity(void*p){return p==player?1:p==other?2:0;}
int jonahIdentity(void*p){return p==jonah?1:p==cached?2:0;}
std::vector<long long> snapshot(){
 std::vector<long long>v={at<short>(actor,0xE2),at<int>(actor,0x31C),at<int>(actor,0x320),at<unsigned>(actor,0x2A8),at<unsigned>(actor,0x288),
 at<int>(actor,0x318),at<unsigned>(actor,0x324),at<int>(actor,0x310),at<int>(actor,0x1F8),at<int>(actor,0x3EC),at<int>(actor,0xBF8),at<int>(actor,0xC10),
 identity(at<void*>(actor,0xBF0)),jonahIdentity(at<void*>(actor,0xBEC)),at<int>(actor,0xBE0),at<int>(actor,0xC20),
 at<unsigned short>(actor,0x12A),at<short>(actor,0x128),at<int>(actor,0x230),at<short>(actor,0x314)};
 for(unsigned offset:{8u,0x60u,0x6Cu,0x27Cu})for(int i=0;i<3;i++)v.push_back(at<int>(actor,offset+i*4));
 for(unsigned offset:{0x88u,0x8Eu})for(int i=0;i<3;i++)v.push_back(at<short>(actor,offset+i*2));
 v.insert(v.end(),{mem<int>(0x6B4C7C),mem<int>(0x6B4B90),mem<int>(0x6B4B94),at<unsigned>(player,0xE1C),at<int>(player,8),at<int>(player,12),at<int>(player,16),at<unsigned char>(hit,0),at<int>(hit,4),at<unsigned short>(hit,8)});
 std::vector<void*>procs;for(void*p=at<void*>(actor,0x28C);p;p=at<void*>(p,0x1C))procs.push_back(p);
 v.push_back(procs.size());for(void*p:procs)for(unsigned offset:{8u,0x10u,0x14u,0xCu,0x38u,0x3Cu})v.push_back(at<int>(p,offset));return v;
}
void event(int kind,std::initializer_list<long long>args={}){std::vector<long long>v={kind};v.insert(v.end(),args);auto s=snapshot();v.insert(v.end(),s.begin(),s.end());events.push_back(v);}
void mutate(int kind){
 if(input[mutation]!=kind)return;
 if(kind==1)at<short>(actor,0xE2)=451;
 if(kind==2){at<short>(actor,0xE2)=-1;at<int>(actor,0x31C)=15;}
 if(kind==3){at<int>(actor,0x31C)=15;at<int>(actor,0x320)=8;at<int>(actor,8)+=4096;at<unsigned>(actor,0x2A8)|=0x48000004;}
 if(kind==4){at<int>(actor,8)+=8192;at<int>(actor,16)-=4096;}
 if(kind==5){at<short>(actor,0xE2)=449;at<int>(player,8)+=4096;at<int>(player,16)-=8192;at<unsigned>(player,0xE1C)|=0x80;}
}
int next(int bound){std::memcpy((void*)0x4E5DA0,randomBytes,5);int result=reinterpret_cast<int(*)(int)>(0x4E5DA0)(bound);patch(0x4E5DA0,(void*)&next);event(0,{bound,result});return result;}
unsigned long long makeHandle(void*p){assert(p==player);if(!constructingLookAt){event(1);mutate(1);}return input[handleAccepted]?((unsigned long long)0x12345678<<32)|(unsigned)p:0;}
unsigned char speech(int bank,int slot,int*position,int priority){event(2,{bank,slot,position[0],position[1],position[2],priority});mutate(5);return input[speechAccepted];}
void __attribute__((thiscall)) attachSpeech(void*p,int bank,int slot){assert(p==player);event(3,{bank,slot});}
void sound(unsigned code,int*position,int pitch){event(4,{code,position[0],position[1],position[2],pitch});mutate(2);}
void __attribute__((thiscall)) animation(void*p,unsigned clip,int from,int to){assert(p==actor);event(5,{clip,from,to});at<unsigned short>(actor,0x12A)=clip;at<short>(actor,0x128)=from;mutate(3);}
void*allocate(unsigned n){assert(n==68||n==64);bool accepted=input[n==68?messageAccepted:lookAtAccepted]!=0;event(n==68?6:9,{n,accepted});if(!accepted)return nullptr;void*p=std::calloc(1,n);assert(p);allocations.push_back(p);return p;}
void*__attribute__((thiscall)) message(void*p,void*sender,void*recipient,int code,void*data){assert(sender==actor&&data==nullptr);event(7,{code,jonahIdentity(recipient)});return p;}
int __attribute__((thiscall)) path(void*p,int*from,int*to,int*out,int clearance){assert(p==actor);event(8,{from[0],from[1],from[2],to[0],to[1],to[2],clearance});for(int i=0;i<3;i++)out[i]=input[contactX+i];mutate(4);return input[pathResult];}
unsigned char lookAtBytes[5];
void*__attribute__((thiscall)) lookAt(void*p,void*baddy,void*target,void*point,int mask,int rate,int tolerance){
 assert(baddy==actor&&target==player&&point==nullptr&&mask==2&&rate==80&&tolerance==200);
 std::memcpy((void*)0x401180,lookAtBytes,5);constructingLookAt=true;
 void*r=reinterpret_cast<void*(__attribute__((thiscall)) *)(void*,void*,void*,void*,int,int,int)>(0x401180)(p,baddy,target,point,mask,rate,tolerance);
 constructingLookAt=false;patch(0x401180,(void*)&lookAt);return r;
}
void diagnostic(int condition,const char*text){if(std::strcmp(text,"Unknown substate!")==0){assert((condition&255)==0);event(10,{at<int>(actor,0x320)});}else{assert(std::strcmp(text,"This shouldn't be zero.  Remove state from table.")==0&&(condition&255)!=0);}}
void numbers(const std::vector<long long>&v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Only Windows SEH and external heap/world/audio/animation/message/handle transport are replaced.
 // Hit, SlideFromHit, ScorpPathCheck, TakeHit, CheckStateFlags, Rnd, difficulty, Neutralize,
 // RunTimer, LookAt construction/category replacement and fixed-vector math remain shipped PC code.
 for(unsigned a:{0x483A87u,0x4866E7u,0x401187u}){omit(a,6);*(unsigned short*)a=0xC031;}
 for(unsigned a:{0x483A8Eu,0x483AB9u,0x483E70u,0x4866EEu,0x48671Cu,0x48675Au,0x4867E7u,0x40118Eu,0x4012D5u})omit(a,7);
 std::memcpy(randomBytes,(void*)0x4E5DA0,5);patch(0x4E5DA0,(void*)&next);
 std::memcpy(lookAtBytes,(void*)0x401180,5);patch(0x401180,(void*)&lookAt);
 patch(0x458360,(void*)&makeHandle);patch(0x479D30,(void*)&speech);patch(0x4603D0,(void*)&attachSpeech);patch(0x471EA0,(void*)&sound);
 patch(0x460C10,(void*)&animation);patch(0x455390,(void*)&allocate);patch(0x458F10,(void*)&message);patch(0x403310,(void*)&path);patch(0x4015B0,(void*)&diagnostic);
 while(std::cin>>input[0]){for(int i=1;i<FieldCount;i++)std::cin>>input[i];assert(std::cin.good());events.clear();
  std::memset(actor,0,sizeof(actor));std::memset(player,0,sizeof(player));std::memset(other,0,sizeof(other));std::memset(jonah,0,sizeof(jonah));std::memset(cached,0,sizeof(cached));std::memset(middle,0,sizeof(middle));std::memset(hit,0,sizeof(hit));
  at<short>(actor,0xE2)=input[health];at<int>(actor,0x31C)=input[state];at<int>(actor,0x320)=input[substate];at<unsigned>(actor,0x2A8)=input[behaviorFlags];at<unsigned>(actor,0x288)=input[completionFlags];
  for(auto pair:std::initializer_list<std::pair<unsigned,int>>{{0x318,reactionContext},{0x324,reactionFlags},{0x310,field310},{0x1F8,slideUpdates},{0x3EC,hitFlashUpdates},{0xBF8,targetKind},{0xC10,targetUpdates},{0xBE0,lastFireQuip},{0xC20,quipSuppression},{0x230,waitUpdates}})at<int>(actor,pair.first)=input[pair.second];
  at<void*>(actor,0xBF0)=input[targetHandle]?other:nullptr;at<unsigned>(actor,0xBF4)=input[targetHandle]?0x87654321:0;at<void*>(actor,0xBEC)=input[cachedJonah]?cached:nullptr;
  at<unsigned short>(actor,0x12A)=input[clip];at<short>(actor,0x128)=input[frame];at<short>(actor,0x21E)=input[surfaceClearance];at<int>(actor,0x80)=input[elapsedTicks];
  for(auto pair:std::initializer_list<std::pair<unsigned,int>>{{8,px},{0x60,vx},{0x6C,ax},{0x27C,lx}})for(int i=0;i<3;i++)at<int>(actor,pair.first+i*4)=input[pair.second+i];
  for(auto pair:std::initializer_list<std::pair<unsigned,int>>{{0x88,avx},{0x8E,aax}})for(int i=0;i<3;i++)at<short>(actor,pair.first+i*2)=input[pair.second+i];
  at<unsigned>(player,0xE1C)=input[playerFlags];for(int i=0;i<3;i++)at<int>(player,8+i*4)=input[playerX+i];mem<void*>(0x6A9038)=player;
  at<unsigned char>(hit,0)=input[hitFlags];at<unsigned char>(hit,1)=input[hitPart];at<int>(hit,4)=input[hitAttack];at<unsigned short>(hit,8)=input[hitDamage];
  for(int i=0;i<3;i++)at<int>(hit,12+i*4)=input[hitX+i];at<short>(hit,24)=input[hitDistance];at<unsigned short>(hit,26)=input[hitFrames];
  mem<int>(0x54D474)=input[difficulty];mem<int>(0x6B4C7C)=input[seed];mem<int>(0x6B4B90)=input[multiplier];mem<int>(0x6B4B94)=input[increment];
  at<short>(middle,0x38)=304;at<short>(jonah,0x38)=at<short>(cached,0x38)=316;at<void*>(middle,0x20)=jonah;at<void*>(jonah,0x20)=cached;mem<void*>(0x56E990)=input[jonahAvailable]?middle:nullptr;
  for(auto&proc:oldProcedures)proc.fill(0);unsigned types[]={0x101,0x40200,0x20001};
  for(int i=0;i<3;i++){at<int>(oldProcedures[i].data(),8)=types[i];at<void*>(oldProcedures[i].data(),4)=actor;at<void*>(oldProcedures[i].data(),0x1C)=i<2?oldProcedures[i+1].data():nullptr;}
  at<void*>(actor,0x28C)=oldProcedures[0].data();int result=0;
  if(input[mode]==0)result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,void*)>(0x483A80)(actor,hit);
  else if(input[mode]==1){int direction[]={int(input[slideX]),int(input[slideY]),int(input[slideZ])};reinterpret_cast<int(__attribute__((thiscall)) *)(void*,int,int,int*)>(0x4838C0)(actor,input[slideDistance],input[slideFrames],direction);}
  else if(input[mode]==2)reinterpret_cast<int(__attribute__((thiscall)) *)(void*)>(0x4866E0)(actor);
  else{assert(input[mode]==3);int from[]={int(input[px]),int(input[py]),int(input[pz])},to[]={int(input[slideX]),int(input[slideY]),int(input[slideZ])},contact[3]={};result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,int*,int*,int*,int)>(0x483820)(actor,from,to,contact,55);}
  std::cout<<"{\"result\":"<<result<<",\"hitDamage\":"<<at<unsigned short>(hit,8)<<",\"snapshot\":";numbers(snapshot());std::cout<<",\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';numbers(events[i]);}std::cout<<"]}\n";
  for(void*p:allocations)std::free(p);allocations.clear();
 }
}
'''


def initial(**changes):
    values = dict.fromkeys(FIELDS, 0)
    values.update(health=900, state=2, substate=7, behaviorFlags=0xF2022005, completionFlags=0xFFFFFFFF,
                  reactionContext=99, field310=123, slideUpdates=87, hitFlashUpdates=5,
                  targetKind=3, targetUpdates=99, targetHandle=1, lastFireQuip=12,
                  clip=2, frame=15, waitUpdates=18, surfaceClearance=100,
                  px=-1735 << 12, py=-31 << 12, pz=443 << 12,
                  vx=12345, vy=-45678, vz=78901, ax=1, ay=2, az=3, lx=4, ly=5, lz=6,
                  avx=1, avy=2, avz=3, aax=4, aay=5, aaz=6,
                  playerX=-1480 << 12, playerY=-31 << 12, playerZ=839 << 12,
                  hitFlags=6, hitAttack=0, hitDamage=15, hitX=4096, hitY=7777, hitZ=-2048,
                  hitDistance=10, hitFrames=10, seed=1, multiplier=0x12B9B0A1, increment=0x0AA2FB3F,
                  pathResult=0, contactX=-1700 << 12, contactY=-31 << 12, contactZ=450 << 12,
                  jonahAvailable=1, speechAccepted=1, messageAccepted=1, handleAccepted=1, lookAtAccepted=1,
                  elapsedTicks=1, slideDistance=10, slideFrames=10, slideX=4096, slideZ=-2048)
    values.update(changes)
    return values


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    cases = []
    for difficulty in [-1, 0, 1, 2, 3, 5]:
        for health in [-32768, -1, 0, 1, 449, 450, 451, 900, 32767]:
            for attack in [0, 2, 6, 23]:
                cases.append(initial(difficulty=difficulty, health=health, hitAttack=attack))
    for state in [0, 2, 3, 9, 12, 15, 16, 17, 18, 19, 21, -1, -32768, 65551, 2147483647]:
        for hit_flags in [0, 2, 4, 6, 8, 16, 24, 30]:
            cases.append(initial(state=state, hitFlags=hit_flags, hitAttack=23))
    for flags in [0, 0x80, 0x800000, 0x800080, 0xFFFFFFFF]:
        for suppression in [-1, 0, 1]:
            for last in [0, 11, 12, 13, 14]:
                for seed in [0, 1, 7]:
                    cases.append(initial(playerFlags=flags, quipSuppression=suppression, lastFireQuip=last, seed=seed, hitAttack=23))
    for damage in [0, 1, 15, 256, 450, 900, 32767, 32768, 65535]:
        for attack in [0, 6, 23]:
            for health in [450, 900]:
                cases.append(initial(hitDamage=damage, hitAttack=attack, health=health))
    for distance in [-32768, -1, 0, 1, 128, 129, 32767]:
        for frames in [1, 2, 3, 10, 65535]:
            for path_result in [0, 1, 2, 4]:
                # Zero displacement with a blocked path causes the original signed division trap.
                if distance == 0 and path_result == 2:
                    continue
                cases.append(initial(hitFlags=30, hitAttack=6, hitDistance=distance, hitFrames=frames, pathResult=path_result))
    for mutation in range(6):
        for attack in [0, 2, 6, 23]:
            for accepted in [0, 1]:
                cases.append(initial(health=450, hitDamage=40, hitAttack=attack, mutation=mutation,
                                     speechAccepted=accepted, messageAccepted=accepted, handleAccepted=accepted))
    for difficulty in [-2147483648, -1, 0, 1, 2, 3, 2147483647]:
        for substate in [-1, 0, 1, 4, 8]:
            for accepted in [0, 1]:
                for clip in [2, 10, 65535]:
                    cases.append(initial(mode=2, difficulty=difficulty, substate=substate, lookAtAccepted=accepted, clip=clip))
    for remaining in [-2147483648, -1, 0, 1, 21, 40, 2147483647]:
        for elapsed in [-2147483648, -1, 0, 1, 2, 50, 2147483647]:
            cases.append(initial(mode=2, substate=4, waitUpdates=remaining, elapsedTicks=elapsed))
    for path_result in [0, 1, 2, 4]:
        for cached_jonah in [0, 1]:
            for available in [0, 1]:
                cases.append(initial(mode=3, cachedJonah=cached_jonah, jonahAvailable=available,
                                     pathResult=path_result, surfaceClearance=-32768, py=2147483647, mutation=4))
    for path_result in [1, 2, 4]:
        cases.append(initial(mode=1, pathResult=path_result, slideX=0, slideY=8192, slideZ=0, slideFrames=0))
    for direction in [(-2147483648, 1), (1, -2147483648), (-2147483648, -2147483648), (0, 0)]:
        cases.append(initial(mode=1, pathResult=2, slideDistance=1, slideFrames=3,
                             slideX=direction[0], slideZ=direction[1]))
    for updates in [-3, 0, 1, 2, 3, 4, 10]:
        cases.append(initial(mode=1, px=0, py=123, pz=0, pathResult=2, slideX=4096, slideY=7777, slideZ=2048,
                             contactX=4096 * updates, contactY=500000, contactZ=2048 * updates))
    for distance in [-128, -1, 0, 1, 128]:
        for frames in [-100, -1, 1, 2, 10]:
            cases.append(initial(mode=1, slideDistance=distance, slideFrames=frames, pathResult=0))
    rng = random.Random(0x483A80)
    for _ in range(420):
        mode = rng.choice([0, 1, 2, 3])
        distance = rng.choice([-32768, -128, -1, 1, 128, 129, 1000, 32767])
        values = initial(mode=mode, difficulty=rng.choice([-1, 0, 1, 2, 3]), health=rng.choice([-1, 0, 1, 450, 451, 900, 32767]),
                         state=rng.choice([0, 2, 3, 9, 12, 15, 16, 17, 18, 19, 21, -1, 65551]),
                         substate=rng.choice([0, 1, 4]), behaviorFlags=rng.randrange(2**32), completionFlags=rng.randrange(2**32),
                         reactionFlags=rng.randrange(2**32), hitFlags=rng.randrange(32), hitAttack=rng.choice([0, 2, 6, 23]),
                         hitDamage=rng.randrange(65536), hitDistance=distance, hitFrames=rng.randrange(1, 65536),
                         slideDistance=distance, slideFrames=rng.randrange(1, 65536), pathResult=rng.choice([0, 1, 2, 4]),
                         playerFlags=rng.choice([0, 0x80, 0x800000, 0x800080]), cachedJonah=rng.randrange(2),
                         jonahAvailable=rng.randrange(2), speechAccepted=rng.randrange(2), messageAccepted=rng.randrange(2),
                         handleAccepted=rng.randrange(2), lookAtAccepted=rng.randrange(2), mutation=rng.randrange(6),
                         seed=rng.randint(-2147483648, 2147483647), multiplier=rng.randint(-2147483648, 2147483647), increment=rng.randint(-2147483648, 2147483647))
        for name in ['px', 'py', 'pz', 'playerX', 'playerY', 'playerZ', 'contactX', 'contactY', 'contactZ', 'hitX', 'hitY', 'hitZ', 'slideX', 'slideY', 'slideZ']:
            values[name] = rng.randint(-100000000, 100000000)
        cases.append(values)
    with tempfile.TemporaryDirectory(prefix='openspidey-scorpion-damage-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(HARNESS.replace('GENERATED_FIELDS', ', '.join(FIELDS)))
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = [[case[name] for name in FIELDS] for case in cases]
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in rows) + '\n').encode()).decode().splitlines()
    fixture = [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/scorpion-damage.json').write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    counts = [sum(case['mode'] == mode for case in cases) for mode in range(4)]
    print(f'Captured {len(cases)} whole shipped PC damage methods: {counts[0]} Hit, {counts[1]} SlideFromHit, {counts[2]} TakeHit, {counts[3]} ScorpPathCheck. Genuine RNG/state-table/math/Neutralize/LookAt/RunTimer run unchanged; callback snapshots include shared fields, complete procedure order and by-reference hit damage.')


if __name__ == '__main__':
    main()
