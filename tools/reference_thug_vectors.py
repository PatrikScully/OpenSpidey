"""Compile recovered CThug methods against instrumented animation/world boundaries.

Synthetic fixtures include constructor, type/parameter changes and Guard dispatch.
The source methods are extracted verbatim; no Windows game or original assets run.
"""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/actors/thug.cpp').read_text()
    code = r'''
#include <cstdint>
#include <iostream>
#include <vector>
using i32 = int32_t; using i16 = int16_t; using u16 = uint16_t;
using u32 = uint32_t; using u8 = uint8_t;
int level=0,randomValue=0; std::vector<int> events;
int Rnd(int bound){events.push_back(1000+bound);return randomValue%bound;}
int Trig_GetLevelID(){return level;}
int G_ATTACK_RELATED=123;void* G_BADDY_LIST=nullptr;
void print_if_false(bool,const char*){events.push_back(-999);}
int gThugTypeRelatedFirstFirst[2]={0x0D0D0100,3};
int gThugTypeRelatedSecondFirst[2]={0x06040504,0};
u8 gThugTypeRelatedFirstThird[1]={0},gThugTypeRelatedSecondThird[1]={1};
struct CBody{};
union Word { unsigned Int=0; unsigned char Bytes[4]; };
struct CThug {
 int mType=0,dumbAssPad=0,mAnim=0,mFrame=0,mAnimFinished=0;
 unsigned mCBodyFlags=0,field_218=0,field_2A8=0;
 int field_1F8=0,field_230=0,field_374=0,field_370=0,field_378=0,field_37C=0,
 field_380=0,field_384=0,field_388=0,field_38C=0,field_1FC=0,field_394=0,
 field_21E=0,field_3B8=0,mShadowScale=0,field_3B0=0,field_1F4=0,mNode=0,
 field_216=0,mRMinor=0,mPushVal=0,field_212=0;
 struct {int bothFlags=0;}field_31C;
 Word field_294,field_298;int model=0,hooks=-1;
 int runFinish=0,spitResult=0;
 CThug()=default;CThug(i16*,i32);
 void Guard(); void CycleOrContinueAnim(int,int,int,int);
 void SetParamByIndex(int,int);void SetThugType(int);
 void Neutralize(){events.push_back(1);}
 void RunAnim(unsigned clip,int from,int to){events.push_back(2);events.push_back(clip);events.push_back(from);events.push_back(to);mAnim=clip;mFrame=from;mAnimFinished=runFinish;}
 void CycleAnim(unsigned clip,int direction){events.push_back(3);events.push_back(clip);events.push_back(direction);mAnim=clip;mAnimFinished=0;}
 int MonitorSpitPlease(){events.push_back(4);return spitResult;}
 void InitItem(const char*name){model=name[0]=='t'?1:name[5]=='n'?2:3;}
 i16* SquirtPos(i16*p){return p;}i16*SquirtAngles(i16*p){return p;}
 void ShadowOn(){}void AttachTo(CBody**){}void ParseScript(u16*){}
};
void M3dUtils_ReadHooksPacket(CThug*t,u8*data){t->hooks=*data;}
'''
    for signature in ['void CThug::CycleOrContinueAnim(', 'void CThug::Guard(',
                      'void CThug::SetParamByIndex(', 'void CThug::SetThugType(', 'CThug::CThug(']:
        code += controller_method(source, signature) + '\n'
    code += r'''
void dump(CThug&t){std::cout<<'['<<t.dumbAssPad<<','<<t.field_1F8<<','<<t.field_230<<','<<t.mCBodyFlags<<','<<t.mAnim<<','<<t.mFrame<<','<<t.mAnimFinished<<",[";
 for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";events.clear();}
int main(){int mode;while(std::cin>>mode){events.clear();if(mode==0){CThug t;int idle;
 std::cin>>t.mType>>t.dumbAssPad>>t.field_1F8>>t.field_230>>t.mCBodyFlags>>t.mAnim>>t.mFrame>>t.runFinish>>idle;
 t.field_298.Int=idle;std::cout<<'[';
 for(int i=0;i<5;i++){std::cin>>t.mAnimFinished>>randomValue>>t.spitResult;if(i)std::cout<<',';t.Guard();dump(t);}std::cout<<"]\n";
 }else{int initialType,node,type,count;std::cin>>initialType>>node>>level>>type>>count;
 i16 data[1]={0};CThug t(data,node); // actual base construction begins at type zero
 t.SetThugType(type);for(int i=0;i<count;i++){int index,value;std::cin>>index>>value;t.SetParamByIndex(index,value);}
 std::cout<<'['<<t.mType<<','<<t.model<<','<<t.hooks<<','<<t.field_21E<<','<<t.field_294.Int<<','<<t.field_298.Int
 <<','<<t.field_374<<','<<t.field_370<<','<<t.field_378<<','<<t.field_37C<<','<<t.field_380<<','<<t.field_384
 <<','<<t.field_388<<','<<t.field_38C<<','<<t.field_1FC<<','<<t.field_394<<','<<t.field_218<<','<<t.mRMinor
 <<','<<t.mPushVal<<','<<t.field_3B8<<','<<t.field_3B0<<','<<t.field_1F4<<','<<t.mNode<<','<<t.field_216
 <<','<<t.field_2A8<<','<<t.field_31C.bothFlags<<','<<t.field_212<<','<<t.mShadowScale<<"]\n";
 }} }
'''
    rng = random.Random(0x4D4F30)
    cases = []
    for thug_type in [304, 312]:
        for substate in range(7):
            for counter in [-2147483648, -1, 0, 1, 2, 2147483647]:
                for animation in [-1, 3, 10, 11, 12]:
                    cases.append([0, thug_type, substate, counter, counter, 0xAB00, animation, 30, 1, 3 if thug_type==304 else 0,
                                  *[v for _ in range(5) for v in (1, 29, 1)]])
    for _ in range(350):
        thug_type = rng.choice([304, 312])
        cases.append([0, thug_type, rng.randrange(7), rng.randint(-3,12), rng.randint(-3,3), rng.getrandbits(32),
                      rng.choice([-1,0,3,10,11,21]),rng.randrange(40),rng.randrange(2),3 if thug_type==304 else 0,
                      *[v for _ in range(5) for v in (rng.choice([0,1,2,255]),rng.randrange(30),rng.randrange(2))]])
    for thug_type in [304, 312]:
        for level in [0, 513, 514]:
            cases.append([1,0,27,level,thug_type,0])
            for index in range(1,13):
                for value in [-2147483648,-1,0,1,2147483647]:
                    cases.append([1,0,27,level,thug_type,1,index,value])
            cases.append([1,0,27,level,thug_type,4,11,1,12,0,11,0,12,-1])
    with tempfile.TemporaryDirectory(prefix='openspidey-thug-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    fixture=[dict(input=c,expected=json.loads(e)) for c,e in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-behavior.json').write_text(json.dumps(fixture,separators=(',',':'))+'\n')
    print(f'Generated {len(fixture)} thug behavior cases from recovered C++.')


if __name__ == '__main__':
    main()
