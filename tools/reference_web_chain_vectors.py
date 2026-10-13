"""Verify original CChain construction, floor and fixed-point Move against actual PC instructions."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
import shutil

from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/objects/chain.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <cmath>
#include <cassert>
#include <cstring>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
struct SChainData{CVector field_0,field_C,field_18;unsigned char field_24=0,pad[3]={};};
struct CChain{unsigned vtable=0;SChainData*field_4=nullptr;int field_8=0,field_C=0,field_10=0,field_14=0,field_18=0,field_1C=0,field_20=0,field_24=0;
 CChain(CVector*,int,int,int,int,int);void Move(CVector*);void SetGroundY(int);};
void*DCMem_New(unsigned size,int,int,void*,bool){return calloc(1,size);}
int CVector::LengthSquared(){int x=vx>>12,y=vy>>12,z=vz>>12;return x*x+y*y+z*z;}
int CVector::Length(){int squared=LengthSquared();return squared<=-32768?32768:squared<0?0:int(std::sqrt(squared));}
'''
    for signature in ['CChain::CChain(', 'void CChain::SetGroundY(', 'void CChain::Move(']:
        code += controller_method(source, signature) + '\n'
    code += r'''
void output(CChain&chain){std::cout<<'['<<chain.field_8<<','<<chain.field_C<<','<<chain.field_10<<','<<chain.field_14<<','<<chain.field_18<<','<<chain.field_1C<<','<<chain.field_20<<','<<chain.field_24<<",[";
 for(int i=0;i<chain.field_8;i++){if(i)std::cout<<',';auto&p=chain.field_4[i];std::cout<<'['<<p.field_0.vx<<','<<p.field_0.vy<<','<<p.field_0.vz<<','<<p.field_C.vx<<','<<p.field_C.vy<<','<<p.field_C.vz<<','<<p.field_18.vx<<','<<p.field_18.vy<<','<<p.field_18.vz<<','<<int(p.field_24)<<']';}std::cout<<"]]";}
#ifdef VALIDATE_ORIGINAL
void patch(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
#endif
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4581A0,reinterpret_cast<void*>(&DCMem_New));
// Omit only Windows constructor SEH registration/restoration.
for(auto entry:std::vector<std::pair<unsigned,int>>{{0x420407,6},{0x42040E,7},{0x4204DD,7},{0x4204F3,7}})
 std::memset(reinterpret_cast<void*>(entry.first),0x90,entry.second);
#endif
int count,length,gravity,friction,recoil,groundEnabled,ground;CVector origin,step,anchorDelta;
while(std::cin>>count>>length>>gravity>>friction>>recoil>>groundEnabled>>ground>>origin.vx>>origin.vy>>origin.vz>>step.vx>>step.vy>>step.vz>>anchorDelta.vx>>anchorDelta.vy>>anchorDelta.vz){
 CVector points[41];for(int i=0;i<count;i++)points[i]=origin+step*i;CChain source(points,count,length,gravity,friction,recoil);if(groundEnabled)source.SetGroundY(ground);
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0x28]={};auto&actual=*reinterpret_cast<CChain*>(raw);reinterpret_cast<void(__attribute__((thiscall)) *)(void*,CVector*,int,int,int,int,int)>(0x420400)(raw,points,count,length,gravity,friction,recoil);
if(groundEnabled)reinterpret_cast<void(__attribute__((thiscall)) *)(void*,int)>(0x420560)(raw,ground);
#endif
 std::cout<<'[';for(int frame=0;frame<=20;frame++){if(frame){CVector anchor=origin+anchorDelta*(frame%7);source.Move(&anchor);
#ifdef VALIDATE_ORIGINAL
reinterpret_cast<void(__attribute__((thiscall)) *)(void*,CVector*)>(0x420580)(raw,&anchor);
#endif
std::cout<<',';}
#ifdef VALIDATE_ORIGINAL
assert(source.field_8==actual.field_8&&source.field_C==actual.field_C&&source.field_10==actual.field_10&&source.field_14==actual.field_14&&source.field_18==actual.field_18&&source.field_1C==actual.field_1C&&source.field_20==actual.field_20&&source.field_24==actual.field_24);
assert(!std::memcmp(source.field_4,actual.field_4,count*sizeof(SChainData)));
#endif
output(source);}std::cout<<"]\n";free(source.field_4);
#ifdef VALIDATE_ORIGINAL
free(actual.field_4);
#endif
}}
'''
    rng = random.Random(0x420580)
    cases = []
    for count in [1, 2, 3, 8, 41]:
        for rest in [1, 8, 80]:
            for mode in [0, 1, 2]:
                cases.append([count, rest, 0 if mode == 0 else 24576, 2400, 2048, mode == 2, 400 << 12,
                              123456, -54321, 76543, (rest << 12) if mode else 0, 0, 0,
                              0 if mode == 0 else 20 << 12, 0, 0])
    for _ in range(64):
        cases.append([rng.choice([2, 3, 8, 16, 41]), rng.choice([1, 20, 80, 256]), rng.choice([0, 4096, 24576]),
                      rng.choice([0, 1024, 2400, 4096]), rng.choice([0, 1024, 2048, 4096]), rng.randrange(2),
                      rng.randint(-200000, 200000), *[rng.randint(-200000, 200000) for _ in range(3)],
                      *[rng.randint(-500000, 500000) for _ in range(3)], *[rng.randint(-40000, 40000) for _ in range(3)]])
    with tempfile.TemporaryDirectory(prefix='openspidey-web-chain-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        flags = ['-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', *flags,
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp),
                        str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        command = [str(binary)] + ([str(args.original_exe.resolve())] if args.original_exe else [])
        inputs = ('\n'.join(' '.join(str(int(v)) for v in case) for case in cases) + '\n').encode()
        try:
            lines = subprocess.check_output(command, input=inputs).decode().splitlines()
        except subprocess.CalledProcessError:
            shutil.copyfile(cpp, '/tmp/openspidey-web-chain-reference.cpp')
            shutil.copyfile(binary, '/tmp/openspidey-web-chain-reference')
            Path('/tmp/openspidey-web-chain-cases.txt').write_bytes(inputs)
            raise
    fixtures = [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/web-chain.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Generated {len(fixtures)} chain sequences / {len(fixtures) * 20} Move calls; actual PC verification={bool(args.original_exe)}.')


if __name__ == '__main__':
    main()
