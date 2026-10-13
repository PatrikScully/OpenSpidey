"""Execute original PC interior/exterior entry, controls,1000 recovery and80000 completion with genuine typed pose/camera boundaries."""
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
std::vector<long long>events;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void __attribute__((thiscall)) orient(void*,bool use,CVector*p){events.insert(events.end(),{1,use,p->vx,p->vy,p->vz});}
void __attribute__((thiscall)) run(void*p,int clip,int first,int last){events.insert(events.end(),{2,clip,first,last});at<unsigned short>(p,0x12A)=clip;at<short>(p,0x128)=0;}
void __attribute__((thiscall)) play(void*p,int clip,int first,int last){auto*words=*(int**)(0x6A82B8+clip*4);at<void*>(p,0x350)=words;if(words)for(auto*cursor=words;*cursor!=-1;cursor++)*cursor&=0xFFFF;run(p,clip,first,last);}
void __attribute__((thiscall)) pose(void*p,void*){events.insert(events.end(),{3,at<unsigned short>(p,0x12A),at<short>(p,0x128)});}
void hook(CVector*out,void*,int index){events.insert(events.end(),{4,index});*out=CVector(123456,-234567,345678);}
void __attribute__((thiscall)) tripod(void*,CVector*p,unsigned ticks){events.insert(events.end(),{5,p->vx,p->vy,p->vz,ticks});}
void __attribute__((thiscall)) stand(void*p){events.push_back(6);at<unsigned>(p,0xE1C)=1;}
void __attribute__((thiscall)) lock(void*p){events.push_back(7);at<int>(p,0xDF8)=0;}
void __attribute__((thiscall)) camX(void*,short value,unsigned short ticks){events.insert(events.end(),{8,0,value,ticks});}
void __attribute__((thiscall)) camY(void*,short value,unsigned short ticks){events.insert(events.end(),{8,1,value,ticks});}
void __attribute__((thiscall)) camZ(void*,short value,unsigned short ticks){events.insert(events.end(),{8,2,value,ticks});}
void __attribute__((thiscall)) camXZ(void*,unsigned short value,unsigned short ticks){events.insert(events.end(),{8,3,value,ticks});}
void __attribute__((thiscall)) camVD(void*,short value,unsigned short ticks){events.insert(events.end(),{8,4,value,ticks});}
void __attribute__((thiscall)) camAngle(void*,short angle,unsigned short ticks){events.insert(events.end(),{9,angle,ticks});}
extern "C" __attribute__((naked)) void leafEnd(){asm volatile("pop %edi;pop %esi;pop %ebp;pop %ebx;add $0x4B0,%esp;ret");}
extern "C" __attribute__((naked)) void leafStep(void*,int,int){asm volatile("sub $0x4B0,%esp;push %ebx;push %ebp;push %esi;push %edi;mov 0x4C4(%esp),%esi;mov 0x4C8(%esp),%edi;mov %edi,0x20(%esp);xor %ebx,%ebx;cmpl $0,0x4CC(%esp);jne 1f;mov $0x4B3DF9,%eax;jmp *%eax;1:mov $0x4B50F1,%eax;jmp *%eax");}
void emitVector(void*p,unsigned n){auto&v=at<CVector>(p,n);std::cout<<','<<v.vx<<','<<v.vy<<','<<v.vz;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4C4BB0,(void*)&orient);patch(0x4C9130,(void*)&play);patch(0x460C10,(void*)&run);patch(0x460E80,(void*)&pose);patch(0x453D60,(void*)&hook);patch(0x417710,(void*)&tripod);patch(0x4BE4B0,(void*)&stand);patch(0x4C6960,(void*)&lock);patch(0x417AE0,(void*)&camX);patch(0x417B60,(void*)&camY);patch(0x417BE0,(void*)&camZ);patch(0x4179F0,(void*)&camXZ);patch(0x417A70,(void*)&camVD);patch(0x4178E0,(void*)&camAngle);patch(0x4B6E8C,(void*)&leafEnd);
unsigned short* globals[]={reinterpret_cast<unsigned short*>(0x6A81DC),reinterpret_cast<unsigned short*>(0x6A81DE),reinterpret_cast<unsigned short*>(0x6A81E0),reinterpret_cast<unsigned short*>(0x6A81FA),reinterpret_cast<unsigned short*>(0x6A8274),reinterpret_cast<unsigned short*>(0x6A81E4),reinterpret_cast<unsigned short*>(0x6A81E2),reinterpret_cast<unsigned short*>(0x6A81E6),reinterpret_cast<unsigned short*>(0x6A81F8),reinterpret_cast<unsigned short*>(0x6A8C66),reinterpret_cast<unsigned short*>(0x6A8204),reinterpret_cast<unsigned short*>(0x6A8200),reinterpret_cast<unsigned short*>(0x6A8202),reinterpret_cast<unsigned short*>(0x6A825C),reinterpret_cast<unsigned short*>(0x6A8CAC)};
int values[]={-1234,2345,-3456,54321,-12345,1111,-2222,3333,62001,4444,-31000,32000,-32768,65535,32767};for(int i=0;i<15;i++)*globals[i]=values[i];
alignas(4)unsigned char animations[0x2000]={};*(void**)0x6B245C=animations;
long long v[31];while(std::cin>>v[0]){for(int i=1;i<31;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={},camera[0x300]={},face[0x20]={},item[0x20]={};int soundWords[]={65543,131081,-1};for(int i=0;i<300;i++){*(void**)(0x6A82B8+i*4)=soundWords;at<unsigned>(animations,8*i+8)=v[27];}*(void**)0x56F3B8=camera;at<unsigned>(camera,0x2A0)=v[26];at<CVector>(player,8)=CVector(12345,-23456,34567);at<CVector>(player,0x60)=CVector(11111,-22222,33333);at<CVector>(player,0xC6C)=CVector(4096,0,0);at<CVector>(player,0xC84)=CVector(11,-4096,13);at<CVector>(player,0xAC8)=CVector(1,2,3);at<unsigned>(player,0xE1C)=v[0]==3?0x1000:v[0]==4?0x80000:16;at<unsigned short>(player,0x12A)=50;at<short>(player,0x128)=12;player[0x142]=v[30];at<void*>(player,0xB74)=v[1]?item:nullptr;player[0xB08]=v[2];player[0xAD4]=v[3];player[0x8E8]=v[4];player[0x8E9]=v[5];player[0xAD5]=v[6];at<int>(player,0xB4C)=v[7];at<void*>(player,0xB8C)=face;at<unsigned>(face,0xC)=v[8];for(int i=0;i<3;i++){at<short>(player,0xB84+2*i)=v[9+i];at<short>(player,0xA8+2*i)=v[12+i];at<int>(player,0xC78+4*i)=v[15+i];}player[0xE2D]=v[18];for(int i=0;i<5;i++)player[0xAD8+i]=v[19+i];player[0xAD6]=v[24];player[0x8EC]=v[25];at<int>(player,0xC58)=v[29];at<int>(player,0xDF8)=99;player[0xAE5]=77;at<short>(player,0xEA6)=33;at<int>(player,0x540)=88;at<void*>(player,0x350)=(void*)1;events.clear();int result=-1;
if(v[0]==0)result=reinterpret_cast<int(__attribute__((thiscall))*)(void*)>(0x4BEB70)(player);else if(v[0]==1)result=reinterpret_cast<int(__attribute__((thiscall))*)(void*)>(0x4BF070)(player);else if(v[0]==2)reinterpret_cast<void(__attribute__((thiscall))*)(void*,bool)>(0x4BEA90)(player,v[30]);else leafStep(player,v[28],v[0]==4);
std::cout<<'['<<result<<','<<at<unsigned>(player,0xE1C)<<','<<at<unsigned short>(player,0x12A)<<','<<at<short>(player,0x128);emitVector(player,8);emitVector(player,0x60);std::cout<<','<<at<short>(player,0xA8)<<','<<at<short>(player,0xAA)<<','<<at<short>(player,0xAC);emitVector(player,0xAC8);std::cout<<','<<unsigned(player[0xAD4])<<','<<unsigned(player[0x8E8])<<','<<unsigned(player[0x8E9]);for(int i=0;i<5;i++)std::cout<<','<<unsigned(player[0xAD8+i]);std::cout<<','<<unsigned(player[0xAD6]);std::cout<<','<<at<int>(player,0xC58)<<','<<at<int>(player,0xDF8)<<','<<unsigned(player[0xAE5])<<','<<at<short>(player,0xEA6)<<','<<at<int>(player,0x540)<<','<<(at<void*>(player,0x350)==soundWords)<<','<<soundWords[0]<<','<<soundWords[1]<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
}}
'''
    rng = random.Random(0x4BEB70)
    normals = [(4096, 0, 0), (0, -4096, 0), (0, 4096, 0), (3500, -2601, -900), (3500, -2600, -900),
               (3500, 3400, -900), (3500, 3401, -900)]
    cases = []
    for family in range(5):
        for _ in range(400):
            cases.append([family, rng.choice([0, 1, 1, 1]), rng.randrange(2), rng.randrange(2), rng.randrange(2), rng.randrange(2), rng.randrange(2),
                          rng.choice([-1, 0, 50]), rng.choice([0, 0x1000000]), *rng.choice(normals), *rng.choice(normals),
                          *rng.choice([(4096, 0, 0), (-4096, 0, 0), (0, 0, 4096), (2147483647, -2147483648, 123456789)]),
                          rng.choice([-127, 0, 127]), *[rng.choice([0, 1, 77, 255]) for _ in range(5)], 6,
                          rng.choice([0, 1]), rng.choice([3, 4]), rng.choice([1, 25, 65535, 65537]),
                          rng.choice([65535, 87, 88, 60, 72, 78, 66, 32768]), rng.choice([-9, -1, 0, 1, 7, 8, 9, 100, 2147483647]), rng.randrange(2)])
    # Complete positive entries cover all source surface regions, side clip choices, camera presets and control signs.
    for family in [0, 1]:
        for normal in normals:
            for wall, ceiling in [(0, 0), (1, 0), (0, 1), (1, 1)]:
                for right in [(4096, 0, 0), (-4096, 0, 0), (0, 0, 4096)]:
                    for hard in [0, 1]:
                        cases.append([family, 1, family, 1, wall, ceiling, hard, 50, 0, *normal, -4096, 0, 0, *right,
                                      -127 if hard else 127, 1, 0, 77, 88, 99, 6, 0, 3, 25, 65535, 9, 1])
    with tempfile.TemporaryDirectory(prefix='openspidey-surface-entry-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root/'src'), '-I', str(root/'tests/runtime'), str(cpp), str(root/'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, case)) for case in cases)+'\n').encode()).decode().splitlines()
    fixture = [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/player-surface-entry.json').write_text(json.dumps(fixture, separators=(',', ':'))+'\n')
    print(f'Captured {len(cases)} complete original PC surface entry/control/1000/80000 cases with actual inline camera presets, signed frame counts, animation packet resets and posed hook/tripod order.')


if __name__ == '__main__':
    main()
