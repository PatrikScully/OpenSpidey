"""Capture actual PC steady lookaround target/action slice, preserving its matrix/ray/state logic with typed target and availability boundaries."""
import argparse
import json
from pathlib import Path
import random
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    # CBody's source mCBodyFlags is at 0x46; 0x40 is an unrelated body word.
    code = r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
struct Matrix{short m[3][3];int t[3];};struct Quat{int x,y,z,w;};struct Angles{short x,y,z,pad;};
std::vector<long long>events;alignas(4)unsigned char baddy[0x110],control[0x110];unsigned face[4];int sphereKind,worldHit,zipAllowed,swingAllowed,effectiveHeading;
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
void emit(std::initializer_list<long long>v){events.insert(events.end(),v);}void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void*sphere(CVector*from,CVector*to,CVector*out,void*,void*exclude,int scale){emit({1,from->vx,from->vy,from->vz,to->vx,to->vy,to->vz,exclude?1:0,scale});*out=*from+(*to-*from)/2;return sphereKind?baddy:nullptr;}
void world(void*p,int mode){CVector from=at<CVector>(p,0),to=at<CVector>(p,0xC);emit({2,from.vx,from.vy,from.vz,to.vx,to.vy,to.vz,mode,*reinterpret_cast<int*>(0x5FBEE4)});at<void*>(p,0x68)=worldHit?reinterpret_cast<void*>(1):nullptr;if(worldHit){at<CVector>(p,0x6C)=to-CVector(111,222,333);at<short>(p,0x78)=-123;at<short>(p,0x7A)=2345;at<short>(p,0x7C)=-3456;at<void*>(p,0x80)=face;at<int>(p,0x40)=777;}}
void*resolve(void*){emit({3});return control;}
unsigned char __attribute__((thiscall)) zip(void*,void*,int range){emit({4,range});return zipAllowed;}
unsigned char __attribute__((thiscall)) swing(void*p,void*){emit({5});if(swingAllowed)at<CVector>(p,0xDA0)=CVector(4096,-2048,1024);return swingAllowed;}
void __attribute__((thiscall)) animation(void*,int clip,int first,int last){emit({6,clip,first,last});}
short __attribute__((thiscall)) heading(void*){emit({7});return effectiveHeading;}
void __attribute__((thiscall)) torso(void*,short wanted,bool fast){emit({8,wanted,fast});}
void __attribute__((thiscall)) exitModeLeaf(void*p){emit({9});at<unsigned char>(p,0x8EA)=0;}
void screen(bool shown){emit({10,shown});}void target(CVector*p,unsigned short size,short angle){emit({11,p->vx,p->vy,p->vz,size,angle});}
extern "C" __attribute__((naked)) void step(void*,CVector*,Matrix*){asm volatile("sub $0x194,%esp;push %ebx;push %ebp;push %esi;push %edi;mov 0x1A8(%esp),%ebp;xor %ebx,%ebx;mov 0x1AC(%esp),%edx;mov (%edx),%eax;mov %eax,0x1C(%esp);mov 4(%edx),%eax;mov %eax,0x20(%esp);mov 8(%edx),%eax;mov %eax,0x24(%esp);mov 0x1B0(%esp),%esi;lea 0x164(%esp),%edi;mov $8,%ecx;rep movsl;mov $0x4C41D3,%eax;jmp *%eax");}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x452C30,(void*)&sphere);patch(0x4549A0,(void*)&world);patch(0x4D1490,(void*)&resolve);patch(0x4C30D0,(void*)&zip);patch(0x4C31D0,(void*)&swing);patch(0x460C10,(void*)&animation);patch(0x4C6AA0,(void*)&heading);patch(0x4C6970,(void*)&torso);patch(0x4C3810,(void*)&exitModeLeaf);patch(0x48AA40,(void*)&screen);patch(0x48AA50,(void*)&target);
long long v[24];while(std::cin>>v[0]){for(int i=1;i<24;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={};Quat body{int(v[0]),int(v[1]),int(v[2]),int(v[3])};Angles angles{short(v[4]),short(v[5]),0,0};Matrix raw;reinterpret_cast<void(*)(Quat*,Matrix*)>(0x47C7F0)(&body,reinterpret_cast<Matrix*>(player+0x164));reinterpret_cast<void(*)(Angles*,Matrix*)>(0x46E730)(&angles,&raw);CVector anchor(v[6],v[7],v[8]);at<CVector>(player,8)=CVector(v[9],v[10],v[11]);player[0xAD4]=v[12];at<unsigned>(player,0xE1C)=v[13];player[0x54F]=v[14];effectiveHeading=v[15];*reinterpret_cast<int*>(0x6A7FFC)=v[16];sphereKind=v[17];worldHit=v[18];face[3]=v[19];at<int>(control,0x100)=v[20];zipAllowed=v[21];swingAllowed=v[22];*reinterpret_cast<unsigned*>(0x6B4CA8)=v[23];at<CVector>(baddy,8)=CVector(100000,-200000,300000);at<short>(baddy,0x46)=sphereKind==1?0x10:0;at<CVector>(control,8)=CVector(-100000,200000,-300000);at<CVector>(player,0xDA0)=CVector(21,-22,23);at<CVector>(player,0xDC0)=CVector(11,-12,13);at<CVector>(player,0x558)=CVector(31,-32,33);at<CVector>(player,0xDAC)=CVector(41,-42,43);player[0x8EA]=1;player[0x8ED]=77;player[0xDE4]=88;at<unsigned>(player,0xDE8)=0xABCDEF;events.clear();step(player,&anchor,&raw);
std::cout<<'['<<at<unsigned>(player,0xE1C)<<','<<unsigned(player[0xAD4])<<','<<unsigned(player[0x8ED])<<','<<unsigned(player[0x8EA])<<','<<unsigned(player[0x54F])<<','<<unsigned(player[0xDE4])<<','<<at<unsigned>(player,0xDE8);for(unsigned o:{0xDA0u,0xDC0u,0x558u,0xDACu}){auto&pos=at<CVector>(player,o);std::cout<<','<<pos.vx<<','<<pos.vy<<','<<pos.vz;}void*aim=at<void*>(player,0xDCC);std::cout<<','<<(aim==baddy?1:aim==control?2:0)<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    cases = []
    for sphere in [0, 1, 2]:
        for hit in [0, 1]:
            for face in [0, 0x2000000]:
                for mode in [0, 1]:
                    for zip_allowed in [0, 1]:
                        for swing in [0, 1]:
                            for activate in [0, 1]:
                                for crawling in [0, 1]:
                                    cases.append([0, 0, 0, 4096, 170, 1025, 123456, -234567, 345678, 234567, -345678, 456789, crawling, 1, activate, 1357, 1025, sphere, hit, face, mode, zip_allowed, swing, 12345])
    rng = random.Random(0x4C41D3)
    for _ in range(160):
        cases.append([0, 0, 0, 4096, rng.randint(-1025, 1025), rng.randint(-2048, 2048), *[rng.randint(-1000000, 1000000) for _ in range(6)],
                      rng.randrange(2), rng.choice([1, 2, 4, 8, 0x8000]), rng.choice([0, 1, 255]), rng.randrange(4096), rng.randint(-4096, 4096),
                      rng.randrange(3), rng.randrange(2), rng.choice([0, 0x2000000]), rng.randrange(2), rng.randrange(2), rng.randrange(2), rng.randrange(0x100000000)])
    with tempfile.TemporaryDirectory(prefix='openspidey-lookaround-target-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        requests = '\n'.join(' '.join(map(str, case)) for case in cases) + '\n'
        try: lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=requests.encode()).decode().splitlines()
        except subprocess.CalledProcessError:
            shutil.copy2(binary, '/tmp/openspidey-lookaround-target-reference'); shutil.copy2(cpp, '/tmp/openspidey-lookaround-target-reference.cpp')
            Path('/tmp/openspidey-lookaround-target-cases.txt').write_text(requests); raise
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-lookaround-target.json').write_text(json.dumps([dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} actual PC steady target/action slices, including body/switch identity and complete zip/swing/reticle callback order.')


if __name__ == '__main__': main()
