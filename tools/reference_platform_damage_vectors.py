"""Compare recovered CPlatform::Hit against original PC instructions and emit bounded fixtures."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    method = controller_method((root / 'src/game/objects/platform.cpp').read_text(),
                               'i32 CPlatform::Hit(')
    code = r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <cstring>
#include <cassert>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
struct SHitInfo { unsigned short padding[4]; unsigned short field_8; unsigned short pad; CVector field_C; };
struct CPlatform { unsigned char field_20F; short attributeArr[6]; short mHealth;
 unsigned short mFlags; CVector mPos; void Die(int); int Hit(SHitInfo*); };
std::vector<int> events; int randomValue;
void CPlatform::Die(int value){events.insert(events.end(), {1,value,mHealth,mFlags});mFlags|=1;}
int Rnd(int range){events.insert(events.end(),{2,range});return randomValue;}
int SFX_PlayPos(int sound,CVector*,int pitch){events.insert(events.end(),{3,sound,pitch});return 0;}
int Shatter_Item(CPlatform* actor,int seen,int processed){events.insert(events.end(),{4,seen,processed,actor->mFlags});return 1;}
'''
    code += method + '\n'
    code += r'''
#ifdef VALIDATE_ORIGINAL
void patch(unsigned address,void* target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;
 *reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
void __attribute__((thiscall)) pcDie(unsigned char* actor,int value){events.insert(events.end(),
 {1,value,*reinterpret_cast<short*>(actor+0xE2),*reinterpret_cast<unsigned short*>(actor+4)});
 *reinterpret_cast<unsigned short*>(actor+4)|=1;*reinterpret_cast<unsigned short*>(actor+0x46)|=0x40;}
int pcShatter(unsigned char* actor,int seen,int processed){events.insert(events.end(),
 {4,seen,processed,*reinterpret_cast<unsigned short*>(actor+4)});return 1;}
#endif
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
 assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 patch(0x404320,reinterpret_cast<void*>(&pcDie));patch(0x4E5DA0,reinterpret_cast<void*>(&Rnd));
 patch(0x471EA0,reinterpret_cast<void*>(&SFX_PlayPos));patch(0x48CFF0,reinterpret_cast<void*>(&pcShatter));
#endif
 int health,attribute,counter,flags,damage;
 while(std::cin>>health>>attribute>>counter>>flags>>damage>>randomValue){
 CPlatform p{};p.mHealth=health;p.attributeArr[1]=attribute;p.field_20F=counter;p.mFlags=flags;
 SHitInfo hit{};hit.field_8=damage;events.clear();int result=p.Hit(&hit);auto expectedEvents=events;
#ifdef VALIDATE_ORIGINAL
 alignas(4) unsigned char raw[0x35C]={};*reinterpret_cast<short*>(raw+0xE2)=health;
 *reinterpret_cast<short*>(raw+0x272)=attribute;raw[0x20F]=counter;*reinterpret_cast<unsigned short*>(raw+4)=flags;
 events.clear();int actual=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,SHitInfo*)>(0x469480)(raw,&hit);
 assert(actual==result&&raw[0x20F]==p.field_20F&&*reinterpret_cast<short*>(raw+0xE2)==p.mHealth&&
 *reinterpret_cast<unsigned short*>(raw+4)==p.mFlags&&events==expectedEvents);
#endif
 std::cout<<'['<<result<<','<<int(p.field_20F)<<','<<p.mHealth<<','<<p.mFlags<<",[";
 for(unsigned i=0;i<expectedEvents.size();i++){if(i)std::cout<<',';std::cout<<expectedEvents[i];}std::cout<<"]]\n";
 }
}
'''
    rng = random.Random(0x469480)
    cases = []
    for health in [-32768, -1, 0, 1, 32767]:
        for attribute in [0, 1, -1]:
            for damage in [0, 1, 32767, 32768, 65535]:
                cases.append([health, attribute, 255, 0x111, damage, rng.randrange(2)])
    for _ in range(160):
        cases.append([rng.randint(-32768, 32767), rng.randint(-32768, 32767),
                      rng.randrange(256), rng.randrange(65536), rng.randrange(65536), rng.randrange(2)])
    with tempfile.TemporaryDirectory(prefix='openspidey-platform-damage-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        flags = ['-m32', '-fno-pie', '-no-pie', '-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', *flags, '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        command = [str(binary)] + ([str(args.original_exe.resolve())] if args.original_exe else [])
        results = subprocess.check_output(command, input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    fixtures = [dict(input=case, expected=json.loads(result)) for case, result in zip(cases,results,strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/platform-damage.json').write_text(json.dumps(fixtures,separators=(',',':'))+'\n')
    print(f'Generated {len(fixtures)} platform damage cases; ' +
          ('actual PC Hit matches recovered source.' if args.original_exe else 'compiled recovered source.'))


if __name__ == '__main__':
    main()
