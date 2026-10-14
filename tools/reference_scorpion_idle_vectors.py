"""Capture whole PC486980 Scorpion idle with its487E20/486800 movement helpers.

Only weak-handle recovery, DetermineTarget, visibility, the CBaddy path-world query,
CycleAnim and the diagnostic sink are replaced. Neutralize, waypoint selection, the
inline Jonah list walk, ScorpPathCheck height adjustment and all distances run unchanged.
"""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


FIELDS = ['substate', 'targetKind', 'currentPointer', 'currentValid', 'cachedJonah', 'hasJonah', 'counter', 'phase',
          'reaction', 'behavior', 'standingY', 'radius', 'px', 'py', 'pz', 'playerX', 'playerY', 'playerZ', 'crawling',
          'losPlayer', 'losTarget', 'jonahX', 'jonahY', 'jonahZ', 'jonahRadius', 'bodyX', 'bodyY', 'bodyZ']
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
enum Field{GENERATED_FIELDS,FieldCount};long long input[FieldCount];
struct Path{int result;Vector contact;};Path paths[PATH_COUNT];int pathCalls;
alignas(4)unsigned char actor[0xC28],player[0xF20],jonah[0x380],middle[0x400],body[0x400];
std::vector<std::vector<long long>>events;
int identity(void*p){if(p==player)return 1;if(p==jonah)return 2;if(p==body)return 3;return p?9:0;}
void*pointer(int id){if(id==1)return player;if(id==2)return jonah;if(id==3)return body;return nullptr;}
void append(std::vector<long long>&v,Vector p){v.insert(v.end(),{p.x,p.y,p.z});}
std::vector<long long>snapshot(){std::vector<long long>v={at<int>(actor,0x31C),at<int>(actor,0x320),at<int>(actor,0xBF8),identity(at<void*>(actor,0xBF0)),at<unsigned>(actor,0xBF4),
 identity(at<void*>(actor,0xBEC)),at<int>(actor,0x1F8),at<unsigned char>(actor,0x21D),at<unsigned>(actor,0x324),at<unsigned>(actor,0x2A8),at<int>(actor,0x1F0)};
 append(v,at<Vector>(actor,0x1B4));append(v,at<Vector>(actor,8));return v;}
void event(int kind,std::initializer_list<long long>args={}){std::vector<long long>v={kind};v.insert(v.end(),args);auto s=snapshot();v.insert(v.end(),s.begin(),s.end());events.push_back(v);}
void*recover(void*handle){assert(handle==actor+0xBF0);if(!input[currentValid]){at<void*>(handle,0)=nullptr;return nullptr;}return at<void*>(handle,0);}
void __attribute__((thiscall))determine(void*p){assert(p==actor);event(1);}
int visible(int*start,int*end,int flags,int argument){assert(start==(int*)(actor+8)&&flags==0);event(2,{argument,end[0],end[1],end[2]});return input[argument?losTarget:losPlayer]!=0;}
int __attribute__((thiscall))path(void*p,int*start,int*end,int*contact,int clearance){assert(p==actor&&contact&&pathCalls<PATH_COUNT);
 event(3,{start[0],start[1],start[2],end[0],end[1],end[2],clearance});*reinterpret_cast<Vector*>(contact)=paths[pathCalls].contact;return paths[pathCalls++].result;}
void __attribute__((thiscall))cycle(void*p,int clip,int direction){assert(p==actor);event(4,{clip,(signed char)direction});}
void diagnostic(int condition,const char*message){if((condition&255)!=0)return;int id=!strcmp(message,"Unknown substate.")?0:!strcmp(message,"Stuck goddammit.")?1:!strcmp(message,"Unknown method.")?2:!strcmp(message,"What the fuck?")?3:-1;assert(id>=0);event(5,{id});}
void numbers(const std::vector<long long>&v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 for(int i=0;i<4096;i++){mem<short>(0x610C48+4*i)=(short)(std::sin(i*0.001536096911877394)*4096);mem<short>(0x610C4A+4*i)=(short)(std::cos(i*0.001536096911877394)*4096);}
 patch(0x4583F0,(void*)&recover);patch(0x488140,(void*)&determine);patch(0x4E67A0,(void*)&visible);patch(0x403310,(void*)&path);patch(0x460D00,(void*)&cycle);patch(0x4015B0,(void*)&diagnostic);
 while(std::cin>>input[0]){for(int i=1;i<FieldCount;i++)std::cin>>input[i];for(auto&q:paths)std::cin>>q.result>>q.contact.x>>q.contact.y>>q.contact.z;
  for(void*p:std::initializer_list<void*>{actor,player,jonah,middle,body})std::memset(p,0,p==actor?sizeof(actor):p==player?sizeof(player):p==jonah?sizeof(jonah):p==middle?sizeof(middle):sizeof(body));
  events.clear();pathCalls=0;
  at<int>(actor,0x31C)=2;at<int>(actor,0x320)=input[substate];at<int>(actor,0xBF8)=input[targetKind];at<void*>(actor,0xBF0)=pointer(input[currentPointer]);at<unsigned>(actor,0xBF4)=777;
  at<void*>(actor,0xBEC)=input[cachedJonah]?jonah:nullptr;at<int>(actor,0x1F8)=input[counter];at<unsigned char>(actor,0x21D)=input[phase];at<unsigned>(actor,0x324)=input[reaction];
  at<unsigned>(actor,0x2A8)=input[behavior];at<int>(actor,0x29C)=input[standingY];at<unsigned short>(actor,0xDC)=input[radius];at<short>(actor,0x21E)=100;
  at<Vector>(actor,8)={int(input[px]),int(input[py]),int(input[pz])};at<Vector>(player,8)={int(input[playerX]),int(input[playerY]),int(input[playerZ])};at<unsigned char>(player,0xAD4)=input[crawling];
  at<short>(jonah,0x38)=316;at<Vector>(jonah,8)={int(input[jonahX]),int(input[jonahY]),int(input[jonahZ])};at<unsigned short>(jonah,0xDC)=input[jonahRadius];
  at<short>(middle,0x38)=304;at<void*>(middle,0x20)=jonah;at<Vector>(body,8)={int(input[bodyX]),int(input[bodyY]),int(input[bodyZ])};
  mem<void*>(0x56E990)=input[hasJonah]?middle:nullptr;mem<void*>(0x6A9038)=player;
  reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x486980)(actor);
  std::cout<<"{\"snapshot\":";numbers(snapshot());std::cout<<",\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';numbers(events[i]);}std::cout<<"]}\n";
 }
}
'''

S = 1 << 12
# Utils_CrapDist boundaries: largest + middle/2 + smallest/4 summed before the >> 12.
CRAP_OFFSETS = [(0, 0, 0), (549 * S, 0, 0), (550 * S, 0, 0), (-550 * S, 0, 0), (400 * S, 0, 300 * S), (400 * S, 0, 300 * S - 1),
                (400 * S, 0, 298 * S), (300 * S, 200 * S, 200 * S), (300 * S, 400 * S, 400 * S), (0, 550 * S, 0), (100 * S, 0, 500 * S),
                (500 * S, 0, 100 * S), (440 * S, 200 * S, 40 * S), (440 * S, 40 * S, 200 * S), (440 * S, 40 * S, 199 * S)]


def initial(**changes):
    values = dict.fromkeys(FIELDS, 0)
    values.update(targetKind=2, currentPointer=1, currentValid=1, hasJonah=1, behavior=0xFFFFFFFF, standingY=-29 * S, radius=175,
                  px=-1735 * S, py=-31 * S, pz=443 * S, playerX=-1000 * S, playerY=-31 * S, playerZ=900 * S, losTarget=1,
                  jonahX=-1480 * S, jonahY=-31 * S, jonahZ=839 * S, jonahRadius=120, bodyX=-1600 * S, bodyY=-31 * S, bodyZ=600 * S)
    values.update(changes)
    return values


def paths(*entries):
    data = [list(entry) for entry in entries] + [[1, 0, 0, 0]] * (PATHS - len(entries))
    return data[:PATHS]


def near(dx, dz):
    return [0, (-1735 + dx) * S, -31 * S, (443 + dz) * S]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    cases = []
    for kind in [0, 1, 2, 3]:
        for valid in [0, 1]:
            for pointer in [0, 1, 2, 3]:
                cases.append(dict(input=initial(targetKind=kind, currentValid=valid, currentPointer=pointer, substate=4), paths=paths()))
    for crawling in [0, 1]:
        for los in [0, 1]:
            for kind in [1, 2]:
                cases.append(dict(input=initial(crawling=crawling, losPlayer=los, targetKind=kind, substate=4), paths=paths()))
    for distance in [0, 300, 549, 550, 551, 189, 190, 191, 3000]:
        for axis in [0, 1]:
            for los in [0, 1]:
                for result in [0, 1, 2, 4]:
                    for contact in [0, 549, 550, 2000]:
                        target = dict(playerX=(-1735 + (distance if axis == 0 else 0)) * S, playerZ=(443 + (distance if axis else 0)) * S)
                        hit = [0, target['playerX'] + contact * S, -31 * S, target['playerZ']]
                        cases.append(dict(input=initial(substate=0, losTarget=los, **target), paths=paths([result, *hit[1:]])))
    for kind in [1, 2]:
        for counter in [0, 16, 17]:
            for phase in [0, 31, 32, 255]:
                for first in [0, 1, 2, 4]:
                    for second in [0, 1, 2]:
                        for contact in [0, 549, 550]:
                            cases.append(dict(input=initial(substate=1, targetKind=kind, currentPointer=3 if kind == 1 else 1, counter=counter, phase=phase),
                                              paths=paths([first, -1700 * S, -20 * S, 500 * S], [first, -1650 * S, -20 * S, 520 * S],
                                                          [second, -1000 * S + contact * S, -31 * S, 900 * S], [0, 0, 0, 0])))
    for counter in [-5, 39, 40, 41, 0x10000, 0x7FFFFFFF]:
        for first in [0, 1, 2]:
            for second in [0, 2, 4]:
                for dx, dy, dz in CRAP_OFFSETS:
                    cases.append(dict(input=initial(substate=2, counter=counter),
                                      paths=paths([first, -1600 * S, 5 * S, 300 * S], [second, -1000 * S + dx, -31 * S + dy, 900 * S - dz])))
    for counter in [0, 664, 665, 666, 0x7FFFFFFF]:
        for jonah in [(0, 0), (1, 0), (0, 1), (1, 1)]:
            for jonah_offset in [0, 294, 295, 2000]:
                for result in [0, 1, 2, 4]:
                    for contact in [100, 101, 500]:
                        values = initial(substate=3, counter=counter, cachedJonah=jonah[0], hasJonah=jonah[1],
                                         jonahX=(-1735 + 525 + jonah_offset) * S, jonahZ=443 * S)
                        cases.append(dict(input=values, paths=paths([result, *near(contact, 0)[1:]])))
    for substate in [-1, 4, 5, 0x7FFFFFFF]:
        cases.append(dict(input=initial(substate=substate), paths=paths()))
    rng = random.Random(0x486980)
    for _ in range(400):
        values = initial(substate=rng.choice([-1, 0, 1, 2, 3, 3, 4]), targetKind=rng.choice([0, 1, 2, 2, 3]),
                         currentPointer=rng.choice([0, 1, 2, 3]), currentValid=rng.choice([0, 1, 1, 1]), cachedJonah=rng.randrange(2),
                         hasJonah=rng.randrange(2), counter=rng.choice([rng.randint(-100, 800), rng.randint(-2**31, 2**31 - 1)]),
                         phase=rng.randrange(256), reaction=rng.randrange(2**32), behavior=rng.randrange(2**32),
                         standingY=rng.randint(-2**31, 2**31 - 1), radius=rng.randrange(65536), crawling=rng.randrange(2),
                         losPlayer=rng.randrange(2), losTarget=rng.randrange(2), jonahRadius=rng.randrange(65536))
        for name in ['px', 'py', 'pz', 'playerX', 'playerY', 'playerZ', 'jonahX', 'jonahY', 'jonahZ', 'bodyX', 'bodyY', 'bodyZ']:
            values[name] = rng.choice([rng.randint(-3000 * S, 3000 * S), rng.randint(-2**31, 2**31 - 1)])
        cases.append(dict(input=values, paths=[[rng.choice([0, 1, 2, 2, 4])] + [rng.choice([rng.randint(-3000 * S, 3000 * S), rng.randint(-2**31, 2**31 - 1)])
                                                                                 for _ in range(3)] for _ in range(PATHS)]))
    rows = [[case['input'][name] for name in FIELDS] + [value for entry in case['paths'] for value in entry] for case in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-scorpion-idle-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(HARNESS.replace('GENERATED_FIELDS', ', '.join(FIELDS)).replace('PATH_COUNT', str(PATHS)))
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                        input=('\n'.join(' '.join(map(str, row)) for row in rows) + '\n').encode()).decode().splitlines()
    for case, line in zip(cases, lines, strict=True):
        case['expected'] = json.loads(line)
    (root / 'modern/OpenSpidey.Tests/Fixtures/scorpion-idle.json').write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole PC idle calls with native Neutralize, waypoint, circle, Jonah walk and distance math.')


if __name__ == '__main__':
    main()
