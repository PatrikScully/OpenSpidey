"""Capture actual complete PC airborne smash and straight R1-zip selectors at genuine typed swing/world/FireWeb boundaries."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
std::vector<long long>events;unsigned face[4];int worldHit,available;unsigned char web[0x140];
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void vec(std::vector<long long>&e,CVector&p){e.insert(e.end(),{p.vx,p.vy,p.vz});}
void __attribute__((thiscall)) swingBack(void*){events.push_back(1);}
void* __attribute__((thiscall)) deleteSwing(void*p,int mode){assert(mode==1);events.push_back(2);return p;}
void __attribute__((thiscall)) torso(void*,CVector*p){events.push_back(3);vec(events,*p);}
void __attribute__((thiscall)) animation(void*,int clip,int first,int last){events.insert(events.end(),{4,clip,first,last});}
void __attribute__((thiscall)) trails(void*){events.push_back(5);}
void world(void*p,int mode){events.push_back(6);vec(events,at<CVector>(p,0));vec(events,at<CVector>(p,12));events.push_back(mode);at<void*>(p,0x68)=worldHit?reinterpret_cast<void*>(1):nullptr;if(worldHit){at<CVector>(p,0x6C)=at<CVector>(p,12)-CVector(111,222,333);at<short>(p,0x78)=-123;at<short>(p,0x7A)=at<short>(face,0);at<short>(p,0x7C)=-3456;at<void*>(p,0x80)=face;}}
unsigned char __attribute__((thiscall)) zipAvailable(void*,void*,int range){events.insert(events.end(),{7,range});return available;}
void*allocate(int size){assert(size==0x13C);events.push_back(8);memset(web,0,sizeof web);return web;}
void* __attribute__((thiscall)) constructor(void*p){events.push_back(9);return p;}
int __attribute__((thiscall)) fire(void*p,bool held,int cost,CVector*target,bool hit,short*normal){events.insert(events.end(),{10,held,cost});vec(events,*target);events.insert(events.end(),{hit,normal[0],normal[1],normal[2],at<unsigned char>(p,0x8F8),at<unsigned char>(p,0xE10)});return 0;}
extern "C" __attribute__((naked)) void zipEnd(){asm volatile("pop %edi;pop %esi;pop %ebp;pop %ebx;add $0xE0,%esp;ret");}
extern "C" __attribute__((naked)) unsigned char zipStep(void*){asm volatile("sub $0xE0,%esp;push %ebx;push %ebp;push %esi;push %edi;mov 0xF4(%esp),%esi;xor %ebx,%ebx;mov $0x4C0F03,%eax;jmp *%eax");}
void emitEvents(){std::cout<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<']';}
void emitVector(void*p,unsigned off){auto&v=at<CVector>(p,off);std::cout<<','<<v.vx<<','<<v.vy<<','<<v.vz;}
int main(int argc,char**argv){assert(argc==3);original_executable::loadOriginalImage(argv[1]);int family=atoi(argv[2]);patch(0x4F7550,(void*)&swingBack);patch(0x4C68A0,(void*)&torso);patch(0x460C10,(void*)&animation);patch(0x4C0D50,(void*)&trails);
if(family){patch(0x4549A0,(void*)&world);patch(0x4C30D0,(void*)&zipAvailable);patch(0x455390,(void*)&allocate);patch(0x4F5DA0,(void*)&constructor);patch(0x4C5DD0,(void*)&fire);patch(0x4C1262,(void*)&zipEnd);}
unsigned vtable[]={reinterpret_cast<unsigned>(&deleteSwing)};long long v[24];while(std::cin>>v[0]){int count=family?23:21;for(int i=1;i<count;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={},controls[0x200]={},body[0x100]={},camera[0x2A4]={},swing[0x190]={};at<void*>(player,0xE0C)=controls;at<void*>(reinterpret_cast<void*>(0x56F3B8),0)=camera;at<void*>(swing,0)=vtable;at<unsigned>(player,0xE1C)=0x400;player[0x54C]=77;player[0x54D]=88;at<int>(camera,0x12C)=2;events.clear();unsigned char result;
if(!family){at<CVector>(player,8)=CVector(v[0],v[1],v[2]);at<CVector>(body,8)=CVector(v[3],v[4],v[5]);at<void*>(player,0xDCC)=v[6]?body:nullptr;at<short>(body,0x38)=v[7];controls[0x121]=v[8];controls[0x131]=v[9];player[0x8EA]=v[10];at<void*>(player,0xE64)=v[11]?swing:nullptr;at<int>(player,0x8C4)=v[12];at<int>(player,0x8C8)=v[13];at<CVector>(player,0x8CC)=CVector(v[14],v[15],v[16]);at<int>(player,0x8DC)=v[17];player[0x8D8]=v[18];*reinterpret_cast<unsigned*>(0x6B4CA8)=v[19];at<unsigned>(player,0xE1C)=v[20];result=reinterpret_cast<unsigned char(__attribute__((thiscall)) *)(void*)>(0x4C1EB0)(player);std::cout<<'['<<unsigned(result)<<','<<at<unsigned>(player,0xE1C)<<','<<unsigned(player[0x54C])<<','<<unsigned(player[0x54D])<<','<<at<int>(camera,0x12C)<<','<<(at<void*>(player,0xE64)?1:0)<<','<<at<int>(player,0x8DC)<<','<<at<int>(player,0x8C4)<<','<<at<int>(player,0x8C8)<<','<<unsigned(player[0x8D8]);emitVector(player,0x8CC);std::cout<<','<<unsigned(controls[0x121])<<','<<unsigned(controls[0x131]);emitEvents();std::cout<<"]\n";}
else{at<CVector>(player,8)=CVector(v[0],v[1],v[2]);at<CVector>(player,0xC84)=CVector(v[3],v[4],v[5]);player[0x8EA]=v[6];at<void*>(player,0xE48)=v[7]?body:nullptr;player[0x550]=v[8];controls[0x60]=v[9];worldHit=v[10];face[3]=v[11];at<short>(face,0)=v[12];available=v[13];at<unsigned>(player,0xE1C)=v[14];player[0xAD4]=v[15];at<void*>(player,0xE64)=v[16]?swing:nullptr;player[0x5E8]=v[17];player[0x8ED]=v[18];at<int>(player,0xDF8)=v[19];player[0x8F8]=v[20];player[0xE10]=v[21];at<CVector>(player,0xDC0)=CVector(11,-12,13);at<CVector>(player,0xDA0)=CVector(21,-22,23);at<CVector>(player,0x558)=CVector(31,-32,33);result=zipStep(player);std::cout<<'['<<unsigned(result)<<','<<at<unsigned>(player,0xE1C)<<','<<unsigned(player[0x54C])<<','<<at<int>(camera,0x12C)<<','<<(at<void*>(player,0xE64)?1:0)<<','<<unsigned(player[0x8ED])<<','<<at<int>(player,0xDF8)<<','<<unsigned(player[0x8F8])<<','<<unsigned(player[0xE10])<<','<<(at<void*>(player,0xE6C)?1:0)<<','<<(at<void*>(player,0xE6C)?at<short>(web,0x102):-1)<<','<<(at<void*>(player,0xE6C)?at<int>(web,0xF8):-1);for(unsigned o:{0xDC0u,0xDA0u,0x558u})emitVector(player,o);emitEvents();std::cout<<"]\n";}
}}
'''
    rng = random.Random(0x4C1EB0)
    smash = [[12345, -23456, 34567, 12345 + x * 4096, -23456 + y * 4096, 34567 + z * 4096, target, kind, kick, punch, active, swing,
              1000, 900, 31, -32, 33, 77, 88, 0xFFFF1234, 0x400]
             for x, y, z in [(0, 128, 0), (400, 100, 0), (0, -128, 0), (384, 128, 0)] for target in [0, 1]
             for kind in [304, 329, 412] for kick, punch in [(0, 0), (1, 0), (0, 1), (1, 1)] for active in [0, 1] for swing in [0, 1]]
    smash += [[*[rng.randint(-10000000, 10000000) for _ in range(6)], rng.randrange(2), rng.choice([304, 329, 412]), rng.randrange(2), rng.randrange(2),
               rng.randrange(2), rng.randrange(2), *[rng.randint(-2147483648, 2147483647) for _ in range(6)], rng.randrange(256), rng.randrange(2**32), rng.choice([1, 2, 4, 0x400])] for _ in range(160)]
    straight = [[12345, -23456, 34567, 0, -4096, 0, active, held, latch, pressed, hit, face, normal, available, action, crawl, swing, 1, 77, 88, 3, 5, 0]
                for active, held, latch, pressed in [(0, 0, 0, 1), (1, 0, 0, 1), (0, 1, 0, 1), (0, 0, 1, 1), (0, 0, 0, 0)]
                for hit in [0, 1] for face, normal in [(0, -1234), (0x40000, -2601), (0x40000, -2600)]
                for available in [0, 1] for action in [1, 4, 0x400] for crawl in [0, 1] for swing in [0, 1]]
    with tempfile.TemporaryDirectory(prefix='openspidey-airborne-guards-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        output = []
        for family, name, cases in [(0, 'smash', smash), (1, 'straight_zip', straight)]:
            requests = ('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()
            lines = subprocess.check_output([str(binary), str(args.original_exe.resolve()), str(family)], input=requests).decode().splitlines()
            output += [dict(kind=name, input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/airborne-attack-selection.json').write_text(json.dumps(output, separators=(',', ':')) + '\n')
    print(f'Captured {len(smash)} whole PC smash selectors and {len(straight)} full R1-zip selectors, including body gate, sharedE64/E6C, actual input6/18/19 and animation/clock/effect order.')


if __name__ == '__main__': main()
