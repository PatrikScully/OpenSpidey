"""Capture actual PC SetupLookaroundCamera through camera placement, with genuine matrix/line initialization and observed world queries."""
import argparse
import json
import math
from pathlib import Path
import random
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    code = r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
#include <cmath>
struct Quat{int x,y,z,w;};struct Matrix{short m[3][3];int t[3];};
std::vector<long long>events;int effectiveHeading,hitDistance,query;unsigned long long hits;
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
void emit(std::initializer_list<long long>v){events.insert(events.end(),v);}
void patch(unsigned a,void*target){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-a-5;}
short __attribute__((thiscall)) heading(void*){emit({2});return effectiveHeading;}
void screen(bool shown){emit({1,shown});}void dispose(void*){emit({5});}
void world(void*p,int mode){CVector from=at<CVector>(p,0),to=at<CVector>(p,0xC);bool hit=query<64&&((hits>>query)&1);query++;emit({4,from.vx,from.vy,from.vz,to.vx,to.vy,to.vz,at<int>(p,0x44),hit?1:0,mode,*reinterpret_cast<int*>(0x5FBEE4)});at<void*>(p,0x68)=hit?reinterpret_cast<void*>(1):nullptr;if(hit){at<CVector>(p,0x6C)=from+(to-from)/2;at<int>(p,0x40)=hitDistance;}}
void vec(std::vector<long long>&v,const CVector&p){v.insert(v.end(),{p.vx,p.vy,p.vz});}void quat(std::vector<long long>&v,const Quat&q){v.insert(v.end(),{q.x,q.y,q.z,q.w});}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);for(int i=0;i<4096;i++){reinterpret_cast<short*>(0x610C48)[2*i]=sin(i*0.001536096911877394)*4096.0;reinterpret_cast<short*>(0x610C48)[2*i+1]=cos(i*0.001536096911877394)*4096.0;}patch(0x4C6AA0,reinterpret_cast<void*>(&heading));patch(0x48AA40,reinterpret_cast<void*>(&screen));patch(0x458210,reinterpret_cast<void*>(&dispose));patch(0x4549A0,reinterpret_cast<void*>(&world));patch(0x4C41BD,reinterpret_cast<void*>(0x4C46EF));
long long v[40];while(std::cin>>v[0]){for(int i=1;i<40;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={},camera[0x2A4]={};Quat path[24],body{int(v[0]),int(v[1]),int(v[2]),int(v[3])},pathRotation{int(v[4]),int(v[5]),int(v[6]),int(v[7])};for(auto&q:path)q=pathRotation;short joints[24]={};at<CVector>(player,8)=CVector(v[8],v[9],v[10]);at<CVector>(player,0xD0C)=CVector(v[11],v[12],v[13]);at<CVector>(player,0xCB8)=CVector(v[14],v[15],v[16]);at<CVector>(player,0xCE8)=CVector(v[17],v[18],v[19]);at<CVector>(player,0xCF4)=CVector(v[20],v[21],v[22]);player[0x8EA]=v[23];at<int>(player,0xCB4)=v[24];at<int>(player,0xCE4)=v[25];at<int>(player,0x80)=v[26];*reinterpret_cast<short*>(0x6A8D44)=v[27];effectiveHeading=v[28];player[0x8E9]=v[29];*reinterpret_cast<int*>(0x6A818C)=v[30];*reinterpret_cast<int*>(0x6A7FFC)=v[31];*reinterpret_cast<int*>(0x6A82B4)=v[32];*reinterpret_cast<int*>(0x6A8D54)=v[33];joints[6]=v[34];joints[7]=v[35];joints[18]=v[36];joints[19]=v[37];hits=v[38];hitDistance=v[39];at<void*>(player,0x188)=joints;at<void*>(player,0xC90)=path;at<int>(player,0xDF8)=77;*reinterpret_cast<void**>(0x56F3B8)=camera;*reinterpret_cast<int*>(0x660F80)=13;*reinterpret_cast<int*>(0x60F76C)=13;*reinterpret_cast<int*>(0x5FBEE4)=0;
reinterpret_cast<void(*)(Quat*,Matrix*)>(0x47C7F0)(&body,reinterpret_cast<Matrix*>(player+0x164));events.clear();query=0;reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4C38A0)(player);
std::vector<long long>out{player[0x8EA],at<int>(player,0xCB4),at<int>(player,0xCE4),*reinterpret_cast<short*>(0x6A8D44),*reinterpret_cast<int*>(0x6A818C),*reinterpret_cast<int*>(0x6A7FFC),*reinterpret_cast<int*>(0x6A82B4),*reinterpret_cast<int*>(0x6A8D54),joints[6],joints[7],joints[18],joints[19],at<int>(player,0xDF8),*reinterpret_cast<int*>(0x660F80)};vec(out,at<CVector>(player,0xD00));vec(out,at<CVector>(player,0xCE8));vec(out,at<CVector>(camera,8));quat(out,at<Quat>(player,0xCD4));quat(out,at<Quat>(camera,0x1F4));out.push_back(at<void*>(player,0xC90)?1:0);
std::cout<<'[';for(unsigned i=0;i<out.size();i++){if(i)std::cout<<',';std::cout<<out[i];}std::cout<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    rng = random.Random(0x4C3F68)
    def quaternion():
        values = [rng.uniform(-1, 1) for _ in range(4)]; length = math.sqrt(sum(value * value for value in values))
        return [int(value * 4096 / length) for value in values]
    cases = []
    for entry, exit in [(0, 0), (24, 0), (2, 0), (0, 24), (0, 2)]:
        for hits in [0, 1, 2, 3, 4, 8, 0x7FFFFFFFFFFFFFFF]:
            for distance in [15, 16, 64]:
                cases.append([0, 0, 0, 4096, 0, 0, 0, 4096, 123456, -234567, 345678, 0, -524288, 0,
                              -123456, 234567, -345678, 100000, -200000, 300000, -100000, 200000, -300000,
                              1, entry, exit, 2, 4095, 0, 0, 170, 1025, 0, -1025, -32768, -3, 32767, 3, hits, distance])
    for _ in range(160):
        cases.append(quaternion() + quaternion() + [rng.randint(-1000000, 1000000) for _ in range(15)] +
                     [rng.randrange(2), rng.choice([0, 2, 6, 24]), rng.choice([0, 2, 6, 24]), 2,
                      rng.randrange(4096), rng.randrange(4096), rng.randrange(2)] + [rng.randint(-1025, 1025) for _ in range(4)] +
                     [rng.randint(-4095, 4095) for _ in range(4)] + [rng.choice([0, 1, 2, 4, 0x7FFFFFFFFFFFFFFF]), rng.choice([15, 16, 64])])
    with tempfile.TemporaryDirectory(prefix='openspidey-lookaround-setup-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        requests = '\n'.join(' '.join(map(str, case)) for case in cases) + '\n'
        try: lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=requests.encode()).decode().splitlines()
        except subprocess.CalledProcessError as error:
            shutil.copy2(cpp, '/tmp/openspidey-lookaround-setup-reference.cpp'); shutil.copy2(binary, '/tmp/openspidey-lookaround-setup-reference')
            Path('/tmp/openspidey-lookaround-setup-cases.txt').write_text(requests); print('Completed rows:', error.output.count(b'\n')); raise
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-lookaround-setup.json').write_text(json.dumps([dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} original PC camera-placement slices, including matrix/quaternion pipeline, anchor distance16 and full lateral clearance grid.')


if __name__ == '__main__': main()
