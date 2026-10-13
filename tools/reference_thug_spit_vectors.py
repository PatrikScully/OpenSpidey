"""Compile CThug::MonitorSpitPlease with instrumented sound, hooks, random and particles."""
import json
from pathlib import Path
import random
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    thug = (root / "src/game/actors/thug.cpp").read_text()
    utils = (root / "src/core/utils.cpp").read_text()
    distance = (root / "src/core/distance.h").read_text()
    code = r'''
#include <algorithm>
#include <cmath>
#include <cstdint>
#include <iostream>
#include <vector>
using i32=int32_t;using i16=int16_t;using u16=uint16_t;using u32=uint32_t;
struct CVector{int vx=0,vy=0,vz=0;void operator>>=(int n){vx>>=n;vy>>=n;vz>>=n;}};
using VECTOR=CVector;
struct CSVector{i16 vx=0,vy=0,vz=0;};
struct SHook{CSVector Part;u16 Offset;};
struct SinCos{i16 sin,cos;};SinCos G_RCOSSIN_TBL[4096];
struct CThug{int mFrame=0;u16 mFlags=0;CVector mPos;CSVector mAngles;i32 MonitorSpitPlease();};
CThug player;CThug* G_MECHLIST_PLAYER=&player;
std::vector<int> events,randomValues;unsigned randomIndex=0;CVector mouth;
void event(std::initializer_list<int> data){events.insert(events.end(),data.begin(),data.end());}
int Rnd(int bound){int value=randomValues.at(randomIndex++);event({3,bound,value});return value;}
void SFX_PlayPos(int sound,CVector* pos,int volume){event({1,sound,pos->vx,pos->vy,pos->vz,volume});}
void M3dUtils_GetDynamicHookPosition(VECTOR*out,CThug*,SHook*hook){
 event({2,hook->Part.vx,hook->Part.vy,hook->Part.vz,hook->Offset});*out=mouth;}
struct CGLineParticle{
 CVector position,velocity;u16 lifetime;int acceleration;u32 mCodeBGR0=0,endRgb=0;
 CGLineParticle(CVector&p,CVector&v,u16 life,int accel);
 void SetRGB0(int r,int g,int b){mCodeBGR0=(mCodeBGR0&0xff000000)|(r|(g<<8)|(b<<16));}
 void SetRGB1(int r,int g,int b){endRgb=r|(g<<8)|(b<<16);}
};
std::vector<CGLineParticle*> particles;
CGLineParticle::CGLineParticle(CVector&p,CVector&v,u16 life,int accel):position(p),velocity(v),lifetime(life),acceleration(accel<<12){
 particles.push_back(this);event({4,p.vx,p.vy,p.vz,v.vx,v.vy,v.vz,life,acceleration});}
namespace spidey{namespace math{enum{PositionFractionBits=12};
'''
    code += controller_method(distance, "inline u32 approximateDistance(") + "\n}}\n"
    code += controller_method(utils, "void Utils_GetVecFromMagDir(") + "\n"
    code += controller_method(thug, "i32 CThug::MonitorSpitPlease(") + "\n"
    code += r'''
int main(){for(int i=0;i<4096;i++){double angle=i*0.001536096911877394;
 G_RCOSSIN_TBL[i].sin=std::sin(angle)*4096.0;G_RCOSSIN_TBL[i].cos=std::cos(angle)*4096.0;}
 CThug actor;unsigned flags;
 while(std::cin>>actor.mFrame>>flags>>actor.mPos.vx>>actor.mPos.vy>>actor.mPos.vz
 >>player.mPos.vx>>player.mPos.vy>>player.mPos.vz>>actor.mAngles.vx>>actor.mAngles.vy>>actor.mAngles.vz
 >>mouth.vx>>mouth.vy>>mouth.vz){
 actor.mFlags=flags;randomValues.resize(12);for(int& value:randomValues)std::cin>>value;
 events.clear();particles.clear();randomIndex=0;int result=actor.MonitorSpitPlease();
 std::cout<<"{\"result\":"<<result<<",\"events\":[";
 for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}
 std::cout<<"],\"particles\":[";
 for(unsigned i=0;i<particles.size();i++){if(i)std::cout<<',';auto*p=particles[i];
 std::cout<<'['<<p->position.vx<<','<<p->position.vy<<','<<p->position.vz<<','
 <<p->velocity.vx<<','<<p->velocity.vy<<','<<p->velocity.vz<<','<<p->lifetime<<','<<p->acceleration<<','
 <<(p->mCodeBGR0&0xffffff)<<','<<p->endRgb<<','<<(p->mCodeBGR0&0xff000000)<<']';delete p;}
 std::cout<<"]}\n";
 }
}
'''
    randomizer = random.Random(0x4D4F30)
    cases = []
    for frame in [-32768, 29, 30, 31, 32767]:
        for flags in [0, 0x80, 0x8000, 0xFFFF]:
            for distance_units in [3999, 4000, 4001]:
                cases.append(dict(frame=frame, flags=flags, position=[0, 0, 0],
                                  player=[distance_units * 4096, 0, 0], angles=[0, 0, 0],
                                  mouth=[123456, -654321, 98765], random=[0, 1, 2, 2, 1, 0, 0, 2, 1, 1, 2, 0]))
    for yaw in [-32768, -1, 0, 1, 1024, 2048, 3072, 4095, 32767]:
        for pitch in [-32768, -1024, 0, 1, 1024, 2048, 32767]:
            cases.append(dict(frame=30, flags=0x8000, position=[123456, -234567, 345678],
                              player=[123456, -234567, 345678], angles=[pitch, yaw, -32768],
                              mouth=[-87654, 43210, -12345], random=[randomizer.randrange(3) for _ in range(12)]))
    for index in range(277):
        position = [randomizer.randint(-2147483648, 2147483647) for _ in range(3)]
        if index % 3 == 0:
            delta = [randomizer.randint(-12000 * 4096, 12000 * 4096) for _ in range(3)]
        else:
            delta = [randomizer.randint(-2000 * 4096, 2000 * 4096) for _ in range(3)]
        player = [((value + change + 2147483648) % 4294967296) - 2147483648 for value, change in zip(position, delta)]
        cases.append(dict(frame=randomizer.choice([29, 30, 31]), flags=randomizer.choice([0, 0x80, 0x8000, 0x8080]),
                          position=position, player=player, angles=[randomizer.randint(-32768, 32767) for _ in range(3)],
                          mouth=[randomizer.randint(-2147483648, 2147483647) for _ in range(3)],
                          random=[randomizer.randrange(3) for _ in range(12)]))
    requests = "".join(" ".join(map(str, [case["frame"], case["flags"], *case["position"], *case["player"],
                                        *case["angles"], *case["mouth"], *case["random"]])) + "\n" for case in cases)
    with tempfile.TemporaryDirectory(prefix="openspidey-spit-reference-") as directory:
        path = Path(directory)
        (path / "reference.cpp").write_text(code)
        subprocess.run(["c++", "-std=c++17", "-O0", "-fwrapv", str(path / "reference.cpp"),
                        "-o", str(path / "reference")], check=True)
        output = subprocess.check_output([str(path / "reference")], input=requests.encode()).decode().splitlines()
    for case, expected in zip(cases, output, strict=True):
        case["expected"] = json.loads(expected)
    destination = root / "modern/OpenSpidey.Tests/Fixtures/thug-spit.json"
    destination.write_text(json.dumps(cases, separators=(",", ":")) + "\n")
    print(f"Generated {len(cases)} recovered-source spit cases at {destination}")


if __name__ == "__main__":
    main()
