"""Compiled recovered thug fight, visibility, sight cone and distance-cache fixtures."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def recovered_harness(root):
    thug=(root/'src/game/actors/thug.cpp').read_text()
    baddy=(root/'src/game/actors/baddy.cpp').read_text()
    utils=(root/'src/core/utils.cpp').read_text()
    distance=(root/'src/core/distance.h').read_text()
    code=r'''
#include <cmath>
#include <cstdint>
#include <iostream>
#include <utility>
#include <vector>
using i32=int32_t;using u32=uint32_t;using i16=int16_t;
struct CVector{int vx=0,vy=0,vz=0;};struct CSVector{i16 vx=0,vy=0,vz=0;};
int my_abs(int x){return x<0?-x:x;}
int catan(int v){return int(std::atan(v/4096.0)*651.0006103515625);}
int M3dMaths_SquareRoot0(int v){return v<=-32768?32768:v<0?0:int(std::sqrt(double(v)));}
std::vector<int>events;int los=0,pathTarget=0,pathLast=0,addFirst=0,addSecond=0,adds=0;
int Utils_LineOfSight(CVector*,CVector*,CVector*,int){events.push_back(1);return los;}
struct CBaddy;using CBody=CBaddy;
CBaddy*G_MECHLIST_PLAYER=nullptr,*G_THUG_LIST=nullptr,*G_GLOBAL_THUG=nullptr;
unsigned G_ATTACK_FLAG_RELATED=0;int G_ATTACK_RELATED=0;
namespace spidey{namespace math{
'''
    code+='enum{PositionFractionBits=12};\n'+controller_method(distance,'inline u32 approximateDistance(')+'\n'
    code+=controller_method(distance,'inline u32 approximateHorizontalDistance(')+'\n}}\n'
    code+=controller_method(utils,'int Utils_CalcAim(')+'\n'
    code+=r'''
struct CBaddy{
 int mType=0,dumbAssPad=0,mHealth=0,field_57C=0;
 void*mHeldObject=nullptr;CVector mPos,field_1A8[6];CSVector mAngles;
 int field_330=0,field_36C=0,field_348=0,field_1A4=0,field_32C=0,field_208=0,field_204=0,field_1F0=0;
 int field_370=3500,field_374=400,field_378=2047,field_37C=100,field_384=1500;
 unsigned field_2A8=0,mCBodyFlags=0;unsigned char field_3BC=0,field_3BD=0;
 struct{int bothFlags=0;}field_31C;
 int DistanceToPlayer(int);int CheckSightCone(int,int,int,int,CBody*);int PlayerIsVisible();int DetermineFightState();
 bool IsDead(){return (mCBodyFlags&0x40)!=0;}
 int PathCheck(CVector*,CVector*p,CVector*,int c){events.insert(events.end(),{2,p->vx,p->vy,p->vz,c});return p==&field_1A8[0]?pathLast:pathTarget;}
 int AddPointToPath(CVector*p,int a){int result=adds++==0?addFirst:addSecond;events.insert(events.end(),{3,p->vx,p->vy,p->vz,a,result});if(result)field_1F0++;return result;}
 void Neutralize(){events.push_back(4);field_2A8&=0xB7FFFFFB;}
 void Baddy_SendSignal(){events.push_back(5);}
};
'''
    for source,signature in [(baddy,'i32 CBaddy::DistanceToPlayer('),(baddy,'i32 CBaddy::PlayerIsVisible('),
                            (baddy,'i32 CBaddy::CheckSightCone('),(thug,'i32 CThug::DetermineFightState(')]:
        code+=controller_method(source,signature).replace('CThug::','CBaddy::')+'\n'
    return code


def main():
    root=Path(__file__).resolve().parent.parent
    code=recovered_harness(root)
    code+=r'''
int main(){int op;while(std::cin>>op){events.clear();adds=0;CBaddy t,p,owner;G_MECHLIST_PLAYER=&p;int holds,fightOwner,globalOwner,a,b,yaw;
 std::cin>>t.mType>>t.mHealth>>p.mHealth>>p.mCBodyFlags>>p.field_57C>>holds>>fightOwner>>globalOwner>>G_ATTACK_FLAG_RELATED>>a>>b
 >>t.field_31C.bothFlags>>t.dumbAssPad>>t.field_330>>t.field_1A4>>t.field_32C>>t.field_36C>>t.field_348>>t.field_208>>t.field_204>>G_ATTACK_RELATED
 >>t.field_2A8>>t.mCBodyFlags>>t.field_374>>t.field_370>>t.field_378>>t.field_37C>>t.field_384
 >>t.mPos.vx>>t.mPos.vy>>t.mPos.vz>>yaw>>p.mPos.vx>>p.mPos.vy>>p.mPos.vz>>t.field_1A8[0].vx>>t.field_1A8[0].vy>>t.field_1A8[0].vz
 >>los>>pathTarget>>pathLast>>addFirst>>addSecond;
 t.mAngles.vy=yaw;t.field_3BC=a;t.field_3BD=b;p.mHeldObject=holds?&p:nullptr;G_THUG_LIST=fightOwner?&owner:nullptr;G_GLOBAL_THUG=globalOwner?&t:nullptr;
 int result=0;if(op==0)result=t.DetermineFightState();else if(op==1)result=t.PlayerIsVisible();else if(op==2)result=t.CheckSightCone(t.field_378,t.field_374,t.field_370,t.field_37C,&p);else result=t.DistanceToPlayer(op-1);
 std::cout<<'['<<result<<','<<t.field_31C.bothFlags<<','<<t.dumbAssPad<<','<<t.field_330<<','<<t.field_36C<<','<<t.field_348<<','<<t.field_208<<','<<t.field_204
 <<','<<t.field_2A8<<','<<t.mCBodyFlags<<','<<t.field_1A8[0].vx<<','<<t.field_1A8[0].vy<<','<<t.field_1A8[0].vz<<','<<t.field_1F0
 <<','<<(G_THUG_LIST!=nullptr)<<','<<(G_GLOBAL_THUG==&t)<<','<<G_ATTACK_FLAG_RELATED<<','<<int(t.field_3BC)<<','<<int(t.field_3BD)<<",[";
 for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
 }}
'''
    def case(**values):
        base=dict(op=0,kind=304,health=90,ph=100,pbody=0,unavailable=0,held=0,fightowner=0,globalowner=1,mask=255,aflags=2,omask=3,
                  state=1,sub=7,alert=600,suppress=0,ranged=0,poll=99,close=0,cacheclock=0,cached=1234,clock=20,
                  behavior=0,body=0x206,height=400,range=3500,cone=2047,near=100,shotrange=1500,
                  x=0,y=0,z=0,yaw=2048,px=0,py=0,pz=4096000,lx=40960,ly=0,lz=-81920,
                  los=1,path=2,lastpath=0,add1=1,add2=1)
        base.update(values);return list(base.values())
    cases=[]
    for kind in [304,312]:
        for dist in [0,99,100,299,300,500,501,999,1000,1001,1499,1500,1999,2000,2001,3499,3500]:
            for alert in [0,600]:
                for los in [0,1]:
                    for path in [0,1,2,-1,4]:
                        cases.append(case(kind=kind,pz=dist<<12,alert=alert,los=los,path=path))
    for state in [1,2,3,4,9,11,23,24]:
        for behavior in [0,0x800,0xffffffff]:
            for add1,add2,lastpath in [(0,1,0),(1,0,0),(1,1,0),(1,1,2)]:
                cases.append(case(state=state,behavior=behavior,los=0,add1=add1,add2=add2,lastpath=lastpath))
    rng=random.Random(0x4D9A40)
    for _ in range(1500):
        cases.append(case(op=rng.choice([0,0,0,1,2,3,5]),kind=rng.choice([304,312]),health=rng.choice([-1,0,90]),ph=rng.choice([0,100]),
                          pbody=rng.choice([0,0x40,0xffffffff]),unavailable=rng.choice([0,0,1]),held=rng.randrange(2),fightowner=rng.randrange(2),
                          globalowner=rng.randrange(2),mask=rng.randrange(256),aflags=rng.randrange(4),omask=rng.randrange(256),
                          state=rng.choice([1,2,3,4,9,11,23,24]),sub=rng.randrange(8),alert=rng.choice([0,0,1,-1,600]),
                          suppress=rng.choice([0,0,1,-1]),ranged=rng.choice([0,1]),close=rng.choice([0,0,1]),cacheclock=rng.choice([0,17,18,19,20,21]),
                          cached=rng.choice([0,299,500,501,1499,1500,2000,3500]),behavior=rng.getrandbits(32),body=rng.getrandbits(32),
                          height=rng.choice([-1,0,100,400]),range=rng.choice([-1,0,1000,3500]),cone=rng.choice([-1,0,512,2047,4096]),
                          near=rng.choice([-1,0,100]),shotrange=rng.choice([500,1000,1500]),x=rng.randint(-2000000,2000000),y=rng.randint(-2000000,2000000),
                          z=rng.randint(-2000000,2000000),yaw=rng.randint(-4096,8192),px=rng.randint(-8000000,8000000),py=rng.randint(-2000000,2000000),
                          pz=rng.randint(-8000000,8000000),los=rng.randrange(2),path=rng.choice([-1,0,1,2,4]),lastpath=rng.choice([0,2]),add1=rng.randrange(2),add2=rng.randrange(2)))
    for op in [2,3,5]:
        for clock in [-2147483648,-1,0,1,17,18,19,20,21,2147483647]:
            for cacheclock in [-2147483648,-1,0,1,18,20,2147483647]:cases.append(case(op=op,clock=clock,cacheclock=cacheclock))
    with tempfile.TemporaryDirectory(prefix='openspidey-thug-fight-')as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c))for c in cases)+'\n').encode()).decode().splitlines()
    output=[dict(input=c,expected=json.loads(e))for c,e in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-fight.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Generated {len(output)} thug perception/fight cases.')


if __name__=='__main__':main()
