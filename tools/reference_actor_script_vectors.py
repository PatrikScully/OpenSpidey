"""Compile recovered control-actor flow/variables against synthetic world callbacks."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/actors/baddy.cpp').read_text()
    harness = r'''
#include "core/fixed_vector.h"
#include "compatibility/export.h"
#include <iostream>
#include <vector>
#include <cstdlib>
struct CBaddy;
struct CPlayer { CVector mPos; };
CPlayer* G_MECHLIST_PLAYER = nullptr;
CBaddy* G_BADDY_LIST = nullptr;
int G_DIFFICULTY_LEVEL=1, G_GAME_FMV_PAD=0, G_TTIME=0, flash=0, death=0;
u8 skip=0;
u8* gSubmarinerDieRelated=&skip;
int* gCompassFlashTimer=&flash;
int suit=0, symbiote=0;
int* CurrentSuit=&suit;
int* gSymbioteRelated=&symbiote;
int gWhatIf=0;
void print_if_false(int condition, const char* text) { if(!condition) { std::cerr<<text; std::exit(2); } }
void DoAssert(int condition, const char* text) { print_if_false(condition,text); }
int Rnd(int n) { return n/2; }
int Utils_XZDist(const CVector*,const CVector*) { return 0; }
int Utils_LineOfSight(const CVector*,const CVector*,int,int) { return 1; }
u16 Spool_GetModel(u32, int) { return 0; }
u16* Trig_GetLinksPointer(int) { static u16 links[]={0};return links; }
void Trig_SendPulse(u16*) { death++; }
int gsub_4DE770() { return 257; }
int gsub_4C9180(CPlayer*,int,int) { return 0; }
int gsub_4E3940(int*,u16) { std::exit(2); }
void SFX_ModifyPos(int,const CVector*,int) {}
struct CBaddy {
    i16* field_24C=nullptr;
    i16* labelArr[8]={};
    u16 mType=203, mNode=42, mFlags=0, mInputFlags=0, mRMinor=128, mModel=0;
    u8 field_20C=1,field_234=0,field_211=0,field_210=0,field_20F=0;
    int field_2A8=0; int field_218=0,field_230=0,field_238=0,field_23C=0,mRegion=0,mAnimSpeed=0;
    i16 field_1FE=0,field_216=32,field_21E=0,field_220=0,mHealth=0;
    u16 field_1FC=0,field_214=0;
    i16 realRegisterArr[6]={},attributeArr[6]={};
    CVector mPos;
    CBaddy* mNextItem=nullptr;
    bool retired=false;
    virtual ~CBaddy() {}
    i16 GetVariable(u16); void SetVariable(u16); i16 GetScriptValue();
    virtual int ExecuteCommand(u16);
    void ParseScript(u16*); void SendDeathPulse();
    int Die(int) { retired=true;return 0; }
};
struct CSimby : CBaddy { void ExplosionReaction() {} };
struct CScriptOnlyBaddy : CBaddy {
    i16 field_32E=0,field_32C=-1;
    int field_328=0;
    void AI();
};
'''
    for signature in ['i16 CBaddy::GetScriptValue(void)', 'void CBaddy::SetVariable(u16 a2)',
                      'i16 CBaddy::GetVariable(u16 a2)', 'void CBaddy::SendDeathPulse(void)',
                      'static u16 CBaddy_ResolveOperand(CBaddy *self)', 'static u16 CBaddy_ReadOperand(CBaddy *self)',
                      'static void CBaddy_SkipToMatchingEndif(CBaddy *self)', 'void CBaddy::ParseScript(u16 *a2)',
                      'void CScriptOnlyBaddy::AI(void)']:
        harness += controller_method(source, signature) + '\n'
    execute = controller_method(source, 'int CBaddy::ExecuteCommand(u16 cmd)')
    first = execute[execute.index('\t\tcase 0x4101:'):execute.index('\t\tcase 0x4200:')]
    wait_signal = execute[execute.index('\t\tcase 0x4281:'):execute.index('\t\tcase 0x428E:')]
    wait_ticks = execute[execute.index('\t\tcase 0x42B6:'):execute.index('\t\tcase 0x42B8:')]
    stop_execution = execute[execute.index('\t\tcase 0x4240:'):execute.index('\t\tcase 0x4280:')]
    timed_motion = execute[execute.index('\t\tcase 0x4280:'):execute.index('\t\t\t// 0x405FED')] + 'std::exit(2); }\n'
    harness += 'int CBaddy::ExecuteCommand(u16 cmd) { switch(cmd) {\n' + first + wait_signal + wait_ticks + stop_execution + timed_motion
    harness += 'default: std::cerr << "Unexpected fixture command " << cmd; std::exit(2); } }\n'
    harness += r'''
void emit(const CScriptOnlyBaddy& a,const i16* begin) {
    std::cout << '[' << (a.field_24C-begin)*2+100 << ',' << int(a.field_20C) << ',' << a.retired << ','
      << a.field_230 << ',' << a.field_238 << ',' << int(a.field_210) << ',' << a.field_214 << ','
      << a.mInputFlags << ',' << int(a.field_211) << ',' << death << ',' << flash << ',' << a.mHealth;
    for(auto r:a.realRegisterArr) std::cout<<','<<r;
    std::cout << "," << a.field_2A8 << "]\n";
}
int main() {
    int count,frames,flags,initialFrames,initialPulses;
    while(std::cin>>count>>frames>>flags>>initialFrames>>initialPulses) {
      std::vector<i16> words(count);
      for(auto& word:words) { int n;std::cin>>n;word=n; }
      CScriptOnlyBaddy a;
      a.field_218=flags;a.field_238=initialFrames;a.field_210=initialPulses;
      skip=0;gWhatIf=0;symbiote=0;flash=0;death=0;
      a.ParseScript(reinterpret_cast<u16*>(words.data()));emit(a,words.data());
      for(int frame=0;frame<frames;frame++) {
        int ticks,signals,fast,whatif,sym;std::cin>>ticks>>signals>>fast>>whatif>>sym;
        skip=fast;gWhatIf=whatif;symbiote=sym;
        if(!a.retired) { a.field_214=0;a.mInputFlags|=signals;
          // mElapsedTicks is the source scheduler's two-tick field.
          a.mElapsedTicks=ticks;a.AI(); }
        emit(a,words.data());
      }
    }
}
'''
    # The source's frame delta belongs to CBody; our synthetic actor supplies the same scalar.
    harness = harness.replace('int field_2A8=0; int field_218=0,', 'int mElapsedTicks=2; int field_2A8=0; int field_218=0,')
    rng = random.Random(407840)
    programs = [[0x4100], [0x4107,0x2120,0,5,0x4100],
                [0x4281,0x2120,0,0x212C,0x4100],
                [0x42B7,0x42B6,100,0x2120,0,7,0x4100],
                [0x2100,32760,0x2120,0,0x2100,0x4110,0x2120,0,50,0x42B6,3,0x4111,0x2120,0,1,0x4100]]
    for wait in [0,1,2,3,7,100]:
        programs.append([0x2120,0,3,0x42B6,wait,0x4110,0x2120,0,2,0x4100])
        programs.append([0x2120,0,3,0x4280,wait,0x4110,0x2120,0,2,0x4100])
    for register in range(6):
        programs.append([0x4105,0x4104,0,0x4110,0x2120,256+register,1,0x42B6,2,0x4102,0,0x4100])
    for health in [0x8001,0,1,8,65535]:
        for op in [0x4112,0x4113,0x4114]:
            programs.append([0x2100,health,op,0x2100,0,0x4117,0x2120,0,9,0x4120,0x2120,1,7,0x4120,
                             0x4119,0x2120,2,4,0x4120,0x4100])
    for flags in [0,1,128,129]:
        programs.append([0x42B6,3,0x4115,flags,0x2120,0,1,0x4120,0x4116,flags,0x2120,1,2,0x4120,0x4100])
    cases=[]
    inputs=[]
    for program in programs:
      for variation in range(2):
        case={'words':program,'flags':rng.choice([0,1,128,129]),'waitFrames':variation*2,'pulses':rng.choice([0,254,255]),
              'frames':[[rng.randint(1,4),rng.randrange(4),int(frame>10),rng.randrange(2),rng.randrange(2)] for frame in range(32)]}
        cases.append(case)
        inputs.append(' '.join(map(str,[len(program),len(case['frames']),case['flags'],case['waitFrames'],case['pulses'],*program,
                                     *(n for frame in case['frames'] for n in frame)])))
    with tempfile.TemporaryDirectory(prefix='openspidey-actor-reference-') as temp:
        cpp=Path(temp)/'reference.cpp';exe=Path(temp)/'reference'
        cpp.write_text(harness)
        subprocess.run(['g++','-m32','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),'-o',str(exe)],check=True)
        output=subprocess.check_output([str(exe)],input='\n'.join(inputs).encode()).decode().splitlines()
    position=0
    for case in cases:
        count=len(case['frames'])+1
        case['expected']=[json.loads(line) for line in output[position:position+count]]
        position+=count
    assert position==len(output)
    destination=root/'modern/OpenSpidey.Tests/Fixtures/actor-scripts.json'
    destination.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} recovered control-actor sequences / {position} snapshots: {destination}')


if __name__=='__main__':
    main()
