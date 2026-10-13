"""Execute original PC campaign lookup, status3 completion block and full PShell unlock policy."""
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
#include "original_executable.h"
#include <iostream>
#include <string>
#include <vector>
int&word(unsigned address){return *reinterpret_cast<int*>(address);}
unsigned char&byte(unsigned address){return *reinterpret_cast<unsigned char*>(address);}
std::vector<int>events;int newUnlock;
void jump(unsigned address,void*target){byte(address)=0xE9;word(address+1)=reinterpret_cast<int>(target)-address-5;}
void cleanup(int type){events.insert(events.end(),{1,type});}
void fade(){events.push_back(2);word(0x6B4C9C)=0;}
void save(){events.push_back(4);}
void unlock(){events.push_back(3);reinterpret_cast<void(*)()>(0x47BED0)();}
// Read the actual computed source EBX before its original callee-save restoration.
__attribute__((naked))void unlockExit(){asm volatile("mov %ebx,newUnlock;pop %ebx;ret");}
// The original status3 block has EBX=0, EBP=-1, EDI=1 and final-menu flag at stack+14.
// The two block exits alone become RET; all completion/lookup/unlock/save-gate arithmetic is original.
__attribute__((naked,noinline))int complete(){asm volatile(
 "push %ebp;push %ebx;push %esi;push %edi;sub $64,%esp;xor %ebx,%ebx;mov $-1,%ebp;mov $1,%edi;"
 "movl $0,16(%esp);push $1f;mov $0x456045,%eax;jmp *%eax;1:mov 16(%esp),%eax;add $64,%esp;"
 "pop %edi;pop %esi;pop %ebx;pop %ebp;ret");}
void array(const std::vector<int>&values){std::cout<<'[';for(unsigned i=0;i<values.size();i++){if(i)std::cout<<',';std::cout<<values[i];}std::cout<<']';}
void bytes(const char*value){std::vector<int>out;while(*value)out.push_back((unsigned char)*value++);array(out);}
std::string decode(const std::string&hex){if(hex=="-")return "";std::string out;for(unsigned i=0;i<hex.size();i+=2)out.push_back(std::stoi(hex.substr(i,2),nullptr,16));return out;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // External cleanup/fade and the interactive save confirmation are recorded leaf boundaries.
 jump(0x443AD0,reinterpret_cast<void*>(&cleanup));jump(0x48A820,reinterpret_cast<void*>(&fade));
 jump(0x47C010,reinterpret_cast<void*>(&save));jump(0x47C00B,reinterpret_cast<void*>(&unlockExit));
 // Preserve the complete original unlock body through a recording wrapper at its two completion callsites.
 for(unsigned address:{0x4560ABu,0x4561E4u})word(address+1)=reinterpret_cast<int>(&unlock)-address-5;
 byte(0x4560EF)=0xC3;byte(0x4561ED)=0xC3;
 std::cout<<"[";for(int i=0;i<34;i++){if(i)std::cout<<',';unsigned entry=0x54A518+i*20;
  std::cout<<"{\"index\":"<<i<<",\"titleBytes\":";bytes(reinterpret_cast<char*>(word(entry)));
  std::cout<<",\"missionBytes\":";bytes(reinterpret_cast<char*>(word(entry+4)));
  std::cout<<",\"flags\":"<<(int)byte(entry+8)<<",\"comicCovers\":"<<(unsigned)word(entry+12)
   <<",\"characters\":"<<(unsigned)word(entry+16)<<'}';}std::cout<<"]\n";
 int mode,difficulty,story,popup;unsigned render,costumes,characters,comics;std::string nameHex;
 while(std::cin>>mode>>nameHex>>difficulty>>render>>story>>popup>>costumes>>characters>>comics){
  std::string name=decode(nameHex);char query[256]={};std::memcpy(query,name.c_str(),name.size()+1);
  std::memset(reinterpret_cast<void*>(0x682858),0xA5,0xBC);std::memset(reinterpret_cast<void*>(0x68285C),0,16);
  std::memcpy(reinterpret_cast<void*>(0x68285C),query,name.size()+1);
  byte(0x6828AD)=story;byte(0x6828D4)=popup;word(0x6828D8)=costumes;word(0x6828DC)=characters;word(0x6828E4)=comics;
  std::vector<int>counts(34);for(int&i:counts){std::cin>>i;}for(int i=0;i<34;i++)byte(0x6828AE + i)=counts[i];
  word(0x54D474)=difficulty;word(0x2E0988C)=render;word(0x6B4C9C)=0;events.clear();newUnlock=-1;
  int lookup=reinterpret_cast<int(*)(char*)>(0x441E60)(query);
  unsigned found=reinterpret_cast<unsigned(*)(char*)>(0x441E00)(query);
  assert((found==0&&lookup==-1)||(found==0x54A518+lookup*20));
  int final=0;if(mode==0)final=complete();else if(mode==1)reinterpret_cast<void(*)()>(0x47BED0)();
  bool guard=true;for(unsigned i=0;i<0xBC;i++){
   if((i>=4&&i<20)||i==0x55||(i>=0x56&&i<0x78)||i==0x7C||(i>=0x80&&i<0x88)||(i>=0x8C&&i<0x90))continue;
   guard&=byte(0x682858+i)==0xA5;}
  std::cout<<"{\"lookup\":"<<lookup<<",\"final\":"<<final<<",\"story\":"<<(int)byte(0x6828AD)
   <<",\"popup\":"<<(int)byte(0x6828D4)<<",\"costumes\":"<<(unsigned)word(0x6828D8)
   <<",\"characters\":"<<(unsigned)word(0x6828DC)<<",\"comics\":"<<(unsigned)word(0x6828E4)
   <<",\"newUnlock\":"<<newUnlock<<",\"guard\":"<<(guard?"true":"false")<<",\"counts\":";
  counts.clear();for(int i=0;i<34;i++)counts.push_back(byte(0x6828AE + i));array(counts);
  std::cout<<",\"events\":";array(events);std::cout<<"}\n";
 }}
'''
    rng = random.Random(0x456045)
    cases = []

    def add(mode, name, counts, **values):
        case = dict(mode=mode, name=name, difficulty=2, render=0, story=170, popup=187,
                    costumes=0x80000100, characters=0x40000000, comics=0, counts=counts)
        case.update(values)
        cases.append(case)

    names = [f'l{chapter}a{area}_t' for chapter, areas in [(1, 4), (2, 2), (3, 5), (4, 1), (5, 7), (6, 4), (7, 5), (8, 6)] for area in range(1, areas+1)]
    for name in [*names, 'l8a7_t', 'L8A7_T', 'l1a2a_t', 'L1A3_T', 'x1z3', 'unknown', '', 'a', 'abc', 'l9a9_t']:
        for difficulty in [0, 1, 2, 3, -1]:
            for render in [0, 0x80, 0x80000000]:
                add(0, name, [0]*34, difficulty=difficulty, render=render)
        add(2, name, [0]*34)
    for value in [0, 1, 2, 254, 255]:
        for name in ['l1a1_t', 'l1a2_t', 'l1a3_t', 'l4a1_t', 'l6a4_t', 'l8a6_t', 'l8a7_t', 'unknown']:
            for difficulty in [0, 2, 3]:
                add(0, name, [value]*34, difficulty=difficulty, story=value)
    for mode in [0, 1]:
        for value in [1, 2, 255]:
            for flags in [0, 4, 8, 0x10, 4|8, 4|8|0x10, 0x80000000, 0xFFFFFFFF]:
                for difficulty in [0, 2, 3]:
                    add(mode, 'l8a7_t', [value]*34, costumes=flags, difficulty=difficulty, comics=0xFFFFFFFF)
        for absent in range(34):
            counts = [2]*34
            counts[absent] = 0
            add(mode, 'l1a2_t', counts, difficulty=3)
    for _ in range(180):
        add(rng.randrange(2), rng.choice(names+['l8a7_t','l1a2a_t','x1z3','unknown']),
            [rng.choice([0,1,2,254,255]) for _ in range(34)], difficulty=rng.choice([-1,0,1,2,3,4]),
            render=rng.getrandbits(32), story=rng.randrange(256), popup=rng.randrange(256),
            costumes=rng.getrandbits(32), characters=rng.getrandbits(32), comics=rng.choice([0,0xFFFFFFFF,rng.getrandbits(32)]))
    fields = ['mode', 'difficulty', 'render', 'story', 'popup', 'costumes', 'characters', 'comics']
    rows = [' '.join(map(str, [case['mode'], case['name'].encode('latin1').hex() or '-',
                             *[case[key] for key in fields[1:]], *case['counts']])) for case in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-campaign-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie',
                        '-I', str(root/'src'), '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        output = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows)+'\n').encode())
    lines = output.decode().splitlines()
    levels = json.loads(lines[0])
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, lines[1:], strict=True)]
    assert all(case['expected']['guard'] for case in fixtures), 'Original campaign policy changed unrelated saved bytes.'
    (root/'modern/OpenSpidey.Tests/Fixtures/campaign-completion.json').write_text(json.dumps(dict(levels=levels,cases=fixtures), separators=(',',':'))+'\n')
    print(f'Captured original 34-level metadata and {len(fixtures)} actual PC lookup/completion/unlock executions.')


if __name__ == '__main__':
    main()
