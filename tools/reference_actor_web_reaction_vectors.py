"""Verify shared baddy TrapWeb/TugWeb leaves against original PC instructions."""
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
    source = (root / 'src/game/actors/baddy.cpp').read_text()
    code = r'''
#include <iostream>
#include <vector>
#include <cassert>
#include <cstring>
#include <cstddef>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
using i32=int;
struct SHandle{void*pointer;unsigned validation;};
struct CBaddy{unsigned field_2A8;short mHealth;SHandle field_10C;int TrapWeb();int TugWeb();};
std::vector<int> events;bool allocated,yankContact;
alignas(4) unsigned char messageStorage[68];
void* allocateMessage(unsigned){events.insert(events.end(),{1,68});return allocated?messageStorage:nullptr;}
struct CMessage{
 static void*operator new(std::size_t size) noexcept{return allocateMessage(size);}
 CMessage(CBaddy*sender,CBaddy*owner,int code,void*data){events.insert(events.end(),{2,sender?1:0,owner?1:0,code,data?1:0});}
};
struct CTrapWebEffect{void Burst(){events.push_back(4);}}contact;
void*Mem_RecoverPointer(SHandle*){events.insert(events.end(),{3,yankContact?1:0});return yankContact?&contact:nullptr;}
'''
    for signature in ['int CBaddy::TrapWeb(', 'int CBaddy::TugWeb(']:
        code += controller_method(source, signature) + '\n'
    code += r'''
#ifdef VALIDATE_ORIGINAL
void patch(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;
 *reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
void __attribute__((thiscall)) constructMessage(void*,void*sender,void*owner,int code,void*data){
 events.insert(events.end(),{2,sender?1:0,owner?1:0,code,data?1:0});}
void __attribute__((thiscall)) burstContact(void*){events.push_back(4);}
#endif
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
 assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 patch(0x455390,reinterpret_cast<void*>(&allocateMessage));
 patch(0x458F10,reinterpret_cast<void*>(&constructMessage));
 patch(0x4583F0,reinterpret_cast<void*>(&Mem_RecoverPointer));
 patch(0x4F8600,reinterpret_cast<void*>(&burstContact));
 // Omit only Windows SEH registration/restoration; acceptance and message/burst instructions remain original.
 for(auto entry:std::vector<std::pair<unsigned,int>>{{0x403E77,6},{0x403E7E,7},{0x403ED1,7},{0x403EE3,7},
   {0x403EF7,6},{0x403EFE,7},{0x403F50,7},{0x403F7C,7}})
  std::memset(reinterpret_cast<void*>(entry.first),0x90,entry.second);
#endif
 int method,health;unsigned flags;int allocation,contactPresent;
 while(std::cin>>method>>flags>>health>>allocation>>contactPresent){
  allocated=allocation;yankContact=contactPresent;CBaddy actor{};actor.field_2A8=flags;actor.mHealth=health;
  events.clear();int expected=method?actor.TugWeb():actor.TrapWeb();auto expectedEvents=events;
#ifdef VALIDATE_ORIGINAL
  alignas(4) unsigned char raw[0x324]={};*reinterpret_cast<unsigned*>(raw+0x2A8)=flags;
  *reinterpret_cast<short*>(raw+0xE2)=health;events.clear();
  unsigned address=method?0x403EF0:0x403E70;
  int actual=reinterpret_cast<int(__attribute__((thiscall)) *)(void*)>(address)(raw);
  assert(actual==expected&&events==expectedEvents);
#endif
  std::cout<<'['<<expected<<",[";for(unsigned i=0;i<expectedEvents.size();i++){if(i)std::cout<<',';std::cout<<expectedEvents[i];}
  std::cout<<"]]\n";
 }
}
'''
    rng = random.Random(0x403E70)
    cases = []
    for method in [0, 1]:
        for flags in [0, 0x200, 0x10000, 0x10200, 0x7FFFFFFF, -1]:
            for health in [-32768, -1, 0, 1, 32767]:
                for allocated in [0, 1]:
                    for contact in [0, 1]:
                        cases.append([method, flags, health, allocated, contact])
    for _ in range(256):
        cases.append([rng.randrange(2), rng.randint(-(1 << 31), (1 << 31) - 1),
                      rng.randint(-32768, 32767), rng.randrange(2), rng.randrange(2)])
    with tempfile.TemporaryDirectory(prefix='openspidey-actor-web-reactions-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        flags = ['-m32', '-fno-pie', '-no-pie', '-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-fcheck-new', *flags,
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        command = [str(binary)] + ([str(args.original_exe.resolve())] if args.original_exe else [])
        result = subprocess.check_output(command, input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode())
        lines = result.decode().splitlines()
    fixtures = [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/actor-web-reactions.json').write_text(
        json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Generated {len(fixtures)} baddy web reaction cases; ' +
          ('actual PC instructions match recovered source.' if args.original_exe else 'compiled recovered source.'))


if __name__ == '__main__':
    main()
