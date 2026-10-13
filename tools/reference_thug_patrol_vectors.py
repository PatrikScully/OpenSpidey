"""Compile extracted patrol and animation methods with instrumented external boundaries."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    thug=(root/'src/game/actors/thug.cpp').read_text()
    baddy=(root/'src/game/actors/baddy.cpp').read_text()
    code=r'''
#include <cstdint>
#include <iostream>
#include <vector>
using i32=int32_t;using u8=uint8_t;
std::vector<int>events;
int randomValue=0;int Rnd(int n){events.push_back(1000+n);return randomValue%n;}
void print_if_false(bool,const char*){}
struct CVector{int vx=0,vy=0,vz=0;};
struct SMoveToInfo{CVector field_0;int field_C,field_10,field_14;};
union Word{unsigned Int=0;unsigned char Bytes[4];};
int thugModes[4][2]={{0x0D0D0100,3},{0x14141C1B,0x114},{0x06061C1B,0x204},{0x14141C1B,0x314}};
int henchModes[4][2]={{0x06040504,0},{0x17171817,0x101},{0x17171817,0x201},{0x01010B0A,0x301}};
#define G_THUG_ANIM_MODES thugModes
#define G_HENCHMAN_ANIM_MODES henchModes
struct CThug;
CThug*G_GLOBAL_THUG=nullptr;unsigned G_ATTACK_FLAG_RELATED=0;
void Trig_GetPosition(CVector*p,int node){*p={409600,0,819200};}
struct CThug{
 int mType=0,dumbAssPad=0,mAnim=0,mFrame=0,mAnimFinished=0,field_230=0,field_310=0,field_1F0=0,field_1F4=0,field_330=0;
 unsigned mCBodyFlags=0,field_2A8=0,field_2F0=0,field_288=0,field_218=0,field_2AC=0;
 unsigned char field_3BC=0,field_3BD=0;
 struct{int bothFlags=0;}field_31C;Word field_294,field_298;CVector field_1A8[6];
 int runFinished=0,spit=0,next=0,nextFlags=0,fight=0,moves=0;
 SMoveToInfo lastMove{};
 void FollowWaypoints();int SetAnimMode(int,int);void RunAppropriateAnim();
 void Neutralize(){events.push_back(1);field_2A8&=0xB7FFFFFB;}
 void RunAnim(unsigned c,int from,int to){events.insert(events.end(),{2,int(c),from,to});mAnim=c;mFrame=from;mAnimFinished=runFinished;}
 void CycleAnim(unsigned c,int dir){events.insert(events.end(),{3,int(c),dir});mAnim=c;mAnimFinished=0;}
 int MonitorSpitPlease(){events.push_back(4);return spit;}
 int GetNextWaypoint(){events.push_back(5);if(!next)return 0;field_1F4=1;field_2F0=nextFlags;return 1;}
 int DetermineFightState(){events.push_back(6);if(fight){field_31C.bothFlags=6;dumbAssPad=0;}return fight;}
};
struct CAIProc_MoveTo{CAIProc_MoveTo(CThug*t,SMoveToInfo*p,int mask){t->lastMove=*p;t->moves++;t->field_288&=~unsigned(mask);t->field_288&=~0x10000u;t->field_2A8&=0xBFFFFFFF;}};
'''
    code+=controller_method(thug,'i32 CThug::SetAnimMode(')+'\n'
    code+=controller_method(baddy,'void CBaddy::RunAppropriateAnim(').replace('CBaddy::','CThug::')+'\n'
    code+=controller_method(thug,'void CThug::FollowWaypoints(')+'\n'
    code+=r'''
void animation(CThug&t,int result){std::cout<<'['<<result<<','<<t.field_294.Int<<','<<t.field_298.Int<<','<<t.mAnim<<','<<t.mFrame<<','<<t.mAnimFinished<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}
int main(){int operation;while(std::cin>>operation){events.clear();CThug t;
 if(operation<2){int mode,old,play;std::cin>>t.mType>>old>>mode>>play>>t.field_2AC>>t.mAnim>>t.mAnimFinished>>t.runFinished;
 t.field_294.Int=(t.mType==304?thugModes:henchModes)[old][0];t.field_298.Int=(t.mType==304?thugModes:henchModes)[old][1];
 int result=0;if(operation==0)result=t.SetAnimMode(mode,play);else t.RunAppropriateAnim();animation(t,result);
 }else{int owner,mode;std::cin>>t.mType>>t.dumbAssPad>>t.field_1F0>>t.field_330>>t.field_31C.bothFlags>>t.field_230>>t.mCBodyFlags
 >>t.field_2A8>>t.field_288>>t.field_218>>t.field_2F0>>t.field_2AC>>mode>>t.mAnim>>t.mAnimFinished>>t.runFinished
 >>randomValue>>t.spit>>t.next>>t.nextFlags>>t.fight>>owner>>G_ATTACK_FLAG_RELATED;
 int a,b;std::cin>>a>>b;t.field_3BC=a;t.field_3BD=b;
 G_GLOBAL_THUG=owner?&t:nullptr;for(int i=0;i<6;i++)t.field_1A8[i]={409600*i,0,-409600*i};
 t.field_294.Int=(t.mType==304?thugModes:henchModes)[mode][0];t.field_298.Int=(t.mType==304?thugModes:henchModes)[mode][1];
 t.FollowWaypoints();
 std::cout<<'['<<t.dumbAssPad<<','<<t.field_1F0<<','<<t.field_330<<','<<t.field_31C.bothFlags<<','<<t.field_230<<','<<t.mCBodyFlags
 <<','<<t.field_2A8<<','<<t.field_288<<','<<t.field_218<<','<<t.field_2F0<<','<<t.field_1F4<<','<<t.field_310
 <<','<<t.field_294.Int<<','<<t.field_298.Int<<','<<t.mAnim<<','<<t.mFrame<<','<<t.mAnimFinished
 <<','<<(G_GLOBAL_THUG==&t)<<','<<G_ATTACK_FLAG_RELATED<<','<<int(t.field_3BC)<<','<<int(t.field_3BD)<<','<<t.moves
 <<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
 }} }
'''
    cases=[]
    for kind in [304,312]:
        for old in range(4):
            for mode in range(4):
                for play in [0,1]:cases.append([0,kind,old,mode,play,0,10,1,0])
            for movement in [0,0x10000,0x20000,0x30000,0x40000,0x50000,0x60000,0x70000]:
                for anim in [-1,0,1,3,4,6,10,13,20,23,27,28]:
                    for done in [0,1,255]:cases.append([1,kind,old,0,0,movement,anim,done,0])
    animation_count=len(cases)
    rng=random.Random(0x4D5110)
    for kind in [304,312]:
        for substate in range(6):
            for path in [0,1,5]:
                for alert in [0,1]:
                    for state in [2,24]:
                        for done in [0,1]:
                            cases.append([2,kind,substate,path,alert,state,0,0x216,0xFFFFFFFF,0xFFFFFFFF,0xFFFF,3,0,0,10,done,0,
                                          1,1,1,2,0,1,0xFF,2,3])
    for _ in range(900):
        cases.append([2,rng.choice([304,312]),rng.randrange(6),rng.choice([0,1,5]),rng.choice([0,1,-1]),rng.choice([2,24,23]),
                      rng.choice([-2147483648,-1,0,1,50,2147483647]),rng.getrandbits(32),rng.getrandbits(32),rng.choice([0,1,0x10001]),
                      rng.getrandbits(32),rng.randrange(8),rng.choice([0,0x10000,0x20000,0x40000,0x70000]),rng.randrange(4),
                      rng.choice([-1,0,3,10,11,20,21,23,27]),rng.choice([0,1,255]),rng.randrange(2),rng.randrange(2),rng.randrange(2),
                      rng.randrange(2),rng.randrange(8),rng.randrange(2),rng.randrange(2),rng.randrange(256),rng.randrange(4),rng.randrange(256)])
    with tempfile.TemporaryDirectory(prefix='openspidey-thug-patrol-')as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        result=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c))for c in cases)+'\n').encode()).decode().splitlines()
    output=[dict(input=c,expected=json.loads(e))for c,e in zip(cases,result,strict=True)]
    for name,subset in [('thug-animation',output[:animation_count]),('thug-patrol',output[animation_count:])]:
        (root/f'modern/OpenSpidey.Tests/Fixtures/{name}.json').write_text(json.dumps(subset,separators=(',',':'))+'\n')
        print(f'Generated {len(subset)} {name} cases.')


if __name__=='__main__':main()
