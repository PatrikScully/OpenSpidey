"""Capture original PC player destructor, mission retry and constructor carried-state instructions."""
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
#include <array>
#include <iostream>
#include <string>
#include <vector>
int&word(unsigned address){return *reinterpret_cast<int*>(address);}
unsigned char&byte(unsigned address){return *reinterpret_cast<unsigned char*>(address);}
int&field(unsigned char*actor,unsigned offset){return *reinterpret_cast<int*>(actor+offset);}
std::vector<int>events,killSaved;unsigned char*currentActor;int levelId,killed;
void jump(unsigned address,void*target){byte(address)=0xE9;
 word(address+1)=reinterpret_cast<int>(target)-address-5;}
void readState(unsigned base,std::vector<int>&out,bool saved){
 if(saved)out={word(base+0x48),word(base+0x4C),word(base+0x50),byte(base+0x79),byte(base+0x7A)};
 else out={word(base),word(base+4),word(base+12),byte(base+8),byte(base+16)};}
void writeState(unsigned base,const std::array<int,5>&state,bool saved){
 if(saved){word(base+0x48)=state[0];word(base+0x4C)=state[1];word(base+0x50)=state[2];byte(base+0x79)=state[3];byte(base+0x7A)=state[4];}
 else{word(base)=state[0];word(base+4)=state[1];word(base+12)=state[2];byte(base+8)=state[3];byte(base+16)=state[4];}}
void kill(){killed++;readState(0x682858,killSaved,true);}
int getLevel(){return levelId;}
void*findAnim(const char*name,int required){assert(std::string(name)=="costarm"&&required==1);
 events.insert(events.end(),{1,field(currentActor,0x5D4),field(currentActor,0x5D8),field(currentActor,0x5EC),
 byte(0x6A9040),byte(0x6A9041),currentActor[0x57C],currentActor[0x5E9]});return reinterpret_cast<void*>(0x12345678);}
void swapTextures(int suit,int other){events.insert(events.end(),{2,suit,other,field(currentActor,0x5EC),
 field(currentActor,0x5D4),field(currentActor,0x5D8),byte(0x6A9040),byte(0x6A9041),currentActor[0x57C],currentActor[0x5E9]});}
// Enter the original constructor's two bounded blocks with its original registers/stack cleanup.
// Only the block exit is RET; all resource, armour, raw-byte and branch instructions remain original.
__attribute__((naked,noinline))void constructorResources(unsigned char*){asm volatile(
 "push %ebp;push %ebx;push %esi;push %edi;mov 20(%esp),%esi;xor %ebx,%ebx;mov $2,%edi;mov $100,%ebp;"
 "push $1f;sub $8,%esp;mov $0x4BA552,%eax;jmp *%eax;1:pop %edi;pop %esi;pop %ebx;pop %ebp;ret");}
__attribute__((naked,noinline))void constructorArmour(unsigned char*){asm volatile(
 "push %ebp;push %ebx;push %esi;push %edi;mov 20(%esp),%esi;xor %ebx,%ebx;mov $2,%edi;mov $100,%ebp;"
 "push $1f;sub $32,%esp;mov $0x4BA792,%eax;jmp *%eax;1:pop %edi;pop %esi;pop %ebx;pop %ebp;ret");}
void array(const std::vector<int>&values){std::cout<<'[';for(unsigned i=0;i<values.size();i++){if(i)std::cout<<',';std::cout<<values[i];}std::cout<<']';}
bool savedGuard(unsigned address){unsigned offset=address-0x682858;return !((offset>=0x48&&offset<0x54)||offset==0x79||offset==0x7A);}
bool backupGuard(unsigned address){unsigned offset=address-0x6A9058;return !((offset<8)||(offset>=12&&offset<16)||offset==8||offset==16);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Windows SEH registration is omitted. Destructor asset/base cleanup and engine scene kill are leaf boundaries.
 std::memset(reinterpret_cast<void*>(0x4BAA37),0x90,6);byte(0x4BAA37)=0x31;byte(0x4BAA38)=0xC0;
 std::memset(reinterpret_cast<void*>(0x4BAA3E),0x90,7);std::memset(reinterpret_cast<void*>(0x4BAD32),0x90,7);
 byte(0x4015B0)=0xC3;byte(0x48AA40)=0xC3;byte(0x460780)=0xC3;
 std::memset(reinterpret_cast<void*>(0x4E021D),0x90,6);byte(0x4E021D)=0x31;byte(0x4E021E)=0xC0;
 std::memset(reinterpret_cast<void*>(0x4E0224),0x90,7);std::memset(reinterpret_cast<void*>(0x4E2BA0),0x90,7);
 jump(0x4437E0,reinterpret_cast<void*>(&kill));jump(0x4DE770,reinterpret_cast<void*>(&getLevel));
 jump(0x4CAB50,reinterpret_cast<void*>(&findAnim));jump(0x4B8D80,reinterpret_cast<void*>(&swapTextures));
 byte(0x4BA643)=0xC3;byte(0x4BA881)=0xC3;
 int mode,status,same,hasPlayer,difficulty,suit,suppressed,globalArmour,processing,low;
 while(std::cin>>mode>>status>>same>>hasPlayer>>difficulty>>suit>>levelId>>suppressed>>globalArmour>>processing>>low){
  std::array<int,5>saved,previous,live;for(int&v:saved)std::cin>>v;for(int&v:previous)std::cin>>v;for(int&v:live)std::cin>>v;
  alignas(4)unsigned char actor[0x1000]={};currentActor=actor;
  std::memset(reinterpret_cast<void*>(0x682858),0xA5,0xA0);std::memset(reinterpret_cast<void*>(0x6A9058),0x3C,0x18);
  writeState(0x682858,saved,true);writeState(0x6A9058,previous,false);
  word(0x60CFA4)=status;word(0x54D474)=difficulty;word(0x5559DC)=suit;word(0x682940)=suppressed;
  byte(0x6A9040)=globalArmour;byte(0x6A9041)=processing;word(0x6B78F8)=low;
  word(0x6A9038)=0;word(0x6A9034)=1;word(0x6B55A0)=0;killed=0;killSaved.clear();events.clear();
  if(mode==0){field(actor,0x5D4)=live[0];field(actor,0x5D8)=live[1];field(actor,0x5EC)=live[2];
   byte(0x6A9040)=live[3];actor[0x57C]=live[4];word(0x6A9038)=reinterpret_cast<int>(actor);
   reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x4BAA30)(actor);}
  else if(mode==1){std::memset(reinterpret_cast<void*>(0x68285C),0,16);std::strcpy(reinterpret_cast<char*>(0x68285C),"L1A1_T");
   word(0x6A9038)=hasPlayer?0x1234:0;const char*next=same?"l1a1_t":"L1A2_T";
   std::vector<unsigned short>program(6,0);program[0]=181;std::memcpy(program.data()+1,next,7);program[5]=65535;
   reinterpret_cast<void(*)(unsigned short*,unsigned,int)>(0x4E0210)(program.data(),65535,0);}
  else{constructorResources(actor);constructorArmour(actor);}
  std::vector<int>outSaved,outPrevious;readState(0x682858,outSaved,true);readState(0x6A9058,outPrevious,false);
  bool guards=true;for(unsigned i=0;i<0xA0;i++)if(savedGuard(0x682858+i)&&!(mode==1&&i>=4&&i<20))guards&=byte(0x682858+i)==0xA5;
  for(unsigned i=0;i<0x18;i++)if(backupGuard(0x6A9058+i))guards&=byte(0x6A9058+i)==0x3C;
  std::cout<<"{\"saved\":";array(outSaved);std::cout<<",\"previous\":";array(outPrevious);
  std::cout<<",\"constructor\":";array({*reinterpret_cast<short*>(actor+0xE2),field(actor,0x5D4),field(actor,0x5D8),field(actor,0x5EC),actor[0x57C],actor[0x5E9],byte(0x6A9040),byte(0x6A9041)});
  std::cout<<",\"status\":"<<word(0x60CFA4)<<",\"killed\":"<<killed<<",\"guards\":"<<(guards?"true":"false")<<",\"killSaved\":";array(killSaved);
  std::cout<<",\"events\":";array(events);std::cout<<"}\n";
 }}
'''
    rng = random.Random(0x4BAA30)
    cases = []

    def add(mode, saved, previous, live, **changes):
        case = dict(mode=mode, status=0, same=1, hasPlayer=0, difficulty=2, suit=0,
                    levelId=0x101, suppressed=0, globalArmour=0, processing=0, low=0,
                    saved=saved, previous=previous, live=live)
        case.update(changes)
        cases.append(case)

    statuses = [-2147483648, -1, *range(12), 32767, 2147483647]
    for status in statuses:
        for old_flag in [0, 1, 2, 128, 255]:
            for live_flag in [0, 1, 2, 128, 255]:
                add(0, [101, -202, -2147483648, old_flag, 255-old_flag],
                    [-303, 404, 2147483647, 255-old_flag, old_flag],
                    [2147483647, -2147483648, -505, live_flag, 255-live_flag], status=status)
        for same in [0, 1]:
            for has_player in [0, 1]:
                add(1, [1, 2, 3, 170, 187], [2147483647, -2147483648, -8, 254, 128],
                    [10, 11, 12, 0, 0], status=status, same=same, hasPlayer=has_player)
    inventories = [(0, 0), (0, 1), (1, 0), (0, -1), (-4096, 20), (4096, 7), (2147483647, -2147483648)]
    for difficulty in range(4):
        for suit in [0, 5, 6, 9, 10]:
            for webbing, cartridges in inventories:
                for level in [0x101, 0x800, 0x8FF, 0x900, -1]:
                    for unlocked, armour_on, suppressed in [(0, 255, 0), (128, 2, 0), (255, 128, 1)]:
                        add(2, [webbing, cartridges, -12345, unlocked, armour_on], [17, 18, 19, 170, 187],
                            [0]*5, difficulty=difficulty, suit=suit, levelId=level, suppressed=suppressed)
    for global_armour in [0, 1, 128, 255]:
        for processing in [0, 1, 128, 255]:
            for low in [0, 1, -1]:
                for amount in [-2147483648, -1, 0, 1, 2147483647]:
                    add(2, [42, 17, amount, 255, 255], [0]*5, [0]*5,
                        suit=5, globalArmour=global_armour, processing=processing, low=low)
    for _ in range(80):
        state = lambda: [rng.randint(-2147483648, 2147483647) for _ in range(3)] + [rng.randrange(256), rng.randrange(256)]
        add(rng.randrange(3), state(), state(), state(), status=rng.choice(statuses), same=rng.randrange(2),
            hasPlayer=rng.randrange(2), difficulty=rng.randrange(4), suit=rng.choice([0, 5, 6, 9, 10]),
            levelId=rng.randint(-2147483648, 2147483647), suppressed=rng.choice([0, -1, 1]),
            globalArmour=rng.randrange(256), processing=rng.randrange(256), low=rng.choice([0, -1, 1]))
    fields = ['mode', 'status', 'same', 'hasPlayer', 'difficulty', 'suit', 'levelId', 'suppressed', 'globalArmour', 'processing', 'low']
    rows = [' '.join(map(str, [*[case[key] for key in fields], *case['saved'], *case['previous'], *case['live']])) for case in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-carried-save-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        output = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows)+'\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, output.decode().splitlines(), strict=True)]
    assert all(case['expected']['guards'] for case in fixtures), 'Original writes changed unrelated saved/backup bytes.'
    target = root / 'modern/OpenSpidey.Tests/Fixtures/player-carried-save.json'
    target.write_text(json.dumps(fixtures, separators=(',', ':'))+'\n')
    print(f'Captured {len(fixtures)} original PC carried-save cases: full destructor/181 branches and unchanged constructor resource/armour blocks.')


if __name__ == '__main__':
    main()
