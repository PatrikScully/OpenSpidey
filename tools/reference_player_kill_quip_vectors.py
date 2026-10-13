"""Compile CPlayer::NotifyKill and SetFirstContactDetails; compare original PC tables/selection/history/callback order."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent;source=(root/'src/game/player/spidey.cpp').read_text()
    code=r'''
#include <iostream>
#include <vector>
#include <cstdint>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
using u8=uint8_t;using u16=uint16_t;using u32=uint32_t;using i32=int32_t;
int G_TIMER_RELATED;unsigned randomState;std::vector<int>events;
int Rnd(int n){events.insert(events.end(),{1,n});randomState=randomState*1664525u+1013904223u;return n?randomState%unsigned(n):0;}
void Redbook_XAPlay(int bank,int slot,int priority){events.insert(events.end(),{2,bank,slot,priority});}
struct CBaddy{u32 mCBodyFlags=0;short mHealth=0;u32 field_2A8=0;CBaddy*mNextItem=nullptr;};CBaddy baddies[4],*G_BADDY_LIST=baddies;
struct CPlayer{u8 field_354=0;i32 field_358=0;u32 field_35C=0;short mHealth=100;void NotifyKill(u16);void SetFirstContactDetails();};
u8 earlySpecial[]={20,11,20,12},early324[]={46,8,46,9,46,10,46,11},earlyOther[]={60,0,60,1,60,2,60,3,60,4,60,5,60,6,60,7,60,8,60,9,60,10,60,11,60,12,60,13,60,14,60,15};
u8 lateSpecial[]={20,11,20,12},late324[]={46,13,46,14},lateOther[]={62,0,62,1,62,2,62,3,62,4,62,5,62,6,62,7,62,8,62,9};
u8*gKillTauntTableEarlySpecial=earlySpecial,*gKillTauntTableEarly144=early324,*gKillTauntTableEarlyOther=earlyOther,*gKillTauntTableLateSpecial=lateSpecial,*gKillTauntTableLate144=late324,*gKillTauntTableLateOther=lateOther;
int history[5]={},lastBank=77,lastVariant=0,callCount=0;
int*gKillTauntLastBank=&lastBank;int*gKillTauntHistory1=history,*gKillTauntHistory2=history+1,*gKillTauntHistory3=history+2,*gKillTauntHistory4=history+3,*gKillTauntHistory5=history+4,*gKillTauntLastVariant=&lastVariant,*gKillNotifyCallCount=&callCount;
'''
    code+=controller_method(source,'void CPlayer::NotifyKill(').replace('*gKillTauntLastVariant = variantIndex;', '*gKillTauntLastBank = groupIndex;\n\t\t\t*gKillTauntLastVariant = variantIndex;')+'\n'+controller_method(source,'void CPlayer::SetFirstContactDetails(')+'\n'
    code+=r'''
#ifdef VALIDATE_ORIGINAL
void patchJump(unsigned address,void*function){auto p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
#endif
void emit(CPlayer&p){std::cout<<'['<<int(p.field_354)<<','<<p.field_358<<','<<p.field_35C<<','<<lastBank<<','<<lastVariant<<','<<callCount<<",[";for(int i=0;i<5;i++){if(i)std::cout<<',';std::cout<<history[i];}std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patchJump(0x4E5DA0,reinterpret_cast<void*>(&Rnd));patchJump(0x479EE0,reinterpret_cast<void*>(&Redbook_XAPlay));
assert(!std::memcmp(reinterpret_cast<void*>(0x5564D0),earlySpecial,4)&&!std::memcmp(reinterpret_cast<void*>(0x5564D8),early324,8)&&!std::memcmp(reinterpret_cast<void*>(0x55649C),earlyOther,32));
assert(!std::memcmp(reinterpret_cast<void*>(0x5564D4),lateSpecial,4)&&!std::memcmp(reinterpret_cast<void*>(0x5564E0),late324,4)&&!std::memcmp(reinterpret_cast<void*>(0x5564BC),lateOther,20));
#endif
int mode;while(std::cin>>mode){CPlayer player;int active,type;unsigned clock,encounterClock;std::cin>>active>>player.field_358>>encounterClock>>player.mHealth>>clock>>type>>randomState>>lastVariant>>callCount;
lastBank=77;player.field_354=active;player.field_35C=encounterClock;G_TIMER_RELATED=clock;for(int i=0;i<5;i++)std::cin>>history[i];
for(int i=0;i<4;i++){std::cin>>baddies[i].mCBodyFlags>>baddies[i].mHealth>>baddies[i].field_2A8;baddies[i].mNextItem=i<3?baddies+i+1:nullptr;}
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char rawPlayer[0x800]={},rawBaddies[4][0x300]={};rawPlayer[0x354]=active;*reinterpret_cast<int*>(rawPlayer+0x358)=player.field_358;*reinterpret_cast<unsigned*>(rawPlayer+0x35C)=encounterClock;*reinterpret_cast<short*>(rawPlayer+0xE2)=player.mHealth;
for(int i=0;i<4;i++){*reinterpret_cast<unsigned*>(rawBaddies[i]+0x46)=baddies[i].mCBodyFlags;*reinterpret_cast<short*>(rawBaddies[i]+0xE2)=baddies[i].mHealth;*reinterpret_cast<unsigned*>(rawBaddies[i]+0x2A8)=baddies[i].field_2A8;*reinterpret_cast<void**>(rawBaddies[i]+0x20)=i<3?rawBaddies[i+1]:nullptr;}
*reinterpret_cast<void**>(0x56E990)=rawBaddies;std::memcpy(reinterpret_cast<void*>(0x6A7FE8),history,20);*reinterpret_cast<int*>(0x6A906C)=lastBank;*reinterpret_cast<int*>(0x6A9070)=lastVariant;*reinterpret_cast<int*>(0x60CFBC)=callCount;
#endif
std::cout<<'[';for(int invocation=0;invocation<8;invocation++){events.clear();unsigned before=randomState;G_TIMER_RELATED=clock;
if(mode==0)player.NotifyKill(type);else{player.SetFirstContactDetails();if(invocation==3)player.field_354=0;}
auto sourceEvents=events;
#ifdef VALIDATE_ORIGINAL
*reinterpret_cast<unsigned*>(0x6B4CA8)=clock;events.clear();randomState=before;
if(mode==0)reinterpret_cast<void(__attribute__((thiscall)) *)(void*,u16)>(0x4BBC60)(rawPlayer,type);
else{reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4C70F0)(rawPlayer);if(invocation==3)rawPlayer[0x354]=0;}
assert(events==sourceEvents&&rawPlayer[0x354]==player.field_354&&*reinterpret_cast<int*>(rawPlayer+0x358)==player.field_358&&*reinterpret_cast<unsigned*>(rawPlayer+0x35C)==player.field_35C);
assert(*reinterpret_cast<int*>(0x6A906C)==lastBank&&!std::memcmp(reinterpret_cast<void*>(0x6A7FE8),history,20)&&*reinterpret_cast<int*>(0x6A9070)==lastVariant&&*reinterpret_cast<int*>(0x60CFBC)==callCount);
#endif
if(invocation)std::cout<<',';events=sourceEvents;emit(player);clock+=17;player.mHealth--;
#ifdef VALIDATE_ORIGINAL
*reinterpret_cast<short*>(rawPlayer+0xE2)=player.mHealth;
#endif
}std::cout<<"]\n";}}
'''
    rng=random.Random(0x4BBC60);cases=[]
    for elapsed in [-2147483648,-1,0,239,240,1200,1201,2147483647]:
        for lost in [-1,0,9,10,50,51]:
            for kind in [304,306,312,320,324]:cases.append([0,1,100,0,100-lost,elapsed&0xffffffff,kind,rng.randrange(2**32),999,0,0,0,0,0,0,*([0,0,0]*4)])
    for _ in range(256):
        active=rng.choice([0,1,255]);health=rng.randint(-32768,32767);initial=rng.randint(-2147483648,2147483647);clock=rng.randrange(2**32)
        case=[rng.choice([0,0,0,1]),active,initial,rng.randrange(2**32),health,clock,rng.choice([304,306,312,320,324,65535]),rng.randrange(2**32),rng.randrange(16),rng.choice([0,2147483647,-2147483648])]
        case.extend(rng.sample([0,*range(960,976),*range(992,1002)],5))
        for i in range(4):case.extend([rng.choice([0,0x200,0x216,0xffffffff]),rng.choice([-1,0,1,90]),rng.choice([0,0x20,0xffffffff])])
        cases.append(case)
    # Forced repeat history for ordinary banks versus deliberate special-bank repetition.
    for kind in [304,306,324]:cases.append([0,1,100,0,100,0,kind,0,999,2147483647,960,961,962,963,964,*([0,0,0]*4)])
    with tempfile.TemporaryDirectory(prefix='openspidey-kill-quips-reference-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'tests/runtime'),'-I',str(root/'src'),str(cpp),'-o',str(binary)],check=True)
        cmd=[str(binary)]
        if args.original_exe:cmd.append(str(args.original_exe.resolve()))
        lines=subprocess.check_output(cmd,input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-kill-quips.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated{len(cases)} kill quip sequences; original PC NotifyKill/table checks when requested.')

if __name__=='__main__':main()
