"""Compile recovered MonitorAttack and SmackSpidey with instrumented animated hook/combat boundaries."""
import json
from pathlib import Path
import random
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    ai = (root / "src/engine/world/ai.cpp").read_text()
    baddy = (root / "src/game/actors/baddy.cpp").read_text()
    maths = (root / "src/engine/rendering/ps2funcs.cpp").read_text()
    code = r'''
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <iostream>
#include <vector>
using i32=int32_t;using i16=int16_t;using u16=uint16_t;using u8=uint8_t;using u32=uint32_t;
struct CVector{int vx=0,vy=0,vz=0;
 CVector operator-(const CVector&o)const{return {vx-o.vx,vy-o.vy,vz-o.vz};}
 CVector operator>>(int n)const{return {vx>>n,vy>>n,vz>>n};}
 void operator+=(const CVector&o){vx+=o.vx;vy+=o.vy;vz+=o.vz;}};
using VECTOR=CVector;
struct CSVector{i16 vx=0,vy=0,vz=0;};struct SHook{CSVector Part;u16 Offset=0;};
struct SHitInfo{u8 field_0,field_1;int field_4;u16 field_8;CVector field_C;};
std::vector<int64_t> events;
void event(std::initializer_list<int64_t> e){events.insert(events.end(),e.begin(),e.end());}
int G_NUM_DOMES=0,collisionOrdinal=-1,collisionCalls=0,returnedPart=0;CVector delta;
struct Player{CVector mPos;i16 mHealth=100;
 void Hit(SHitInfo*h){event({5,h->field_0,h->field_1,h->field_4,h->field_8,h->field_C.vx,h->field_C.vy,h->field_C.vz});
 mHealth=i16(mHealth-h->field_8);}}player;
Player*G_MECHLIST_PLAYER=&player;
struct CBaddy{int mAnim=13,mFrame=0;u32 field_288=0;u16 field_1FC=10;CVector mPos;
 i32 SmackSpidey(i32,CVector*,i32,i32);
 void CreateCombatImpactEffect(CVector*p,int flags){event({3,p->vx,p->vy,p->vz,flags});}
 void Victorious(){event({6});}};
enum AIProcType{MONITOR_ATTACK=1281};
struct CAIProc{CBaddy*pBaddy=nullptr;int field_10=0,field_14=0;CAIProc*mNext=nullptr;
 virtual void Execute()=0;
 void AttachProc(AIProcType,CBaddy*b,int mask){pBaddy=b;field_14=mask;b->field_288&=~mask;}};
struct Next:CAIProc{void Execute(){event({7});}};
struct CAIProc_MonitorAttack:CAIProc{
 int field_20=0,field_24=0,field_28=0,field_2C=0,field_34=0;CVector*field_30=nullptr;
 CAIProc_MonitorAttack(CBaddy*,int,int,int,int);void Execute();};
void*DCMem_New(int size,int,int,int,int){return std::calloc(1,size);}
void M3dUtils_GetDynamicHookPosition(VECTOR*out,CBaddy*b,SHook*hook){
 *out={b->mPos.vx+delta.vx+int(hook->Offset)*4096,b->mPos.vy+delta.vy-int(hook->Offset)*2048,b->mPos.vz+delta.vz+int(hook->Offset)*1024};
 event({1,hook->Part.vx,hook->Part.vy,hook->Part.vz,hook->Offset,out->vx,out->vy,out->vz});}
int Web_CollideWithSuper(Player*,const CVector*from,const CVector*to,SHook*out,int scale){
 bool hit=collisionCalls++==collisionOrdinal;out->Offset=returnedPart;
 event({2,from->vx,from->vy,from->vz,to->vx,to->vy,to->vz,scale,hit,returnedPart});return hit;}
void SFX_PlayPos(int sound,CVector*pos,int volume){event({4,sound,pos->vx,pos->vy,pos->vz,volume});}
'''
    for source, signature in [(maths, "void VectorNormal("), (baddy, "i32 CBaddy::SmackSpidey("),
                              (ai, "CAIProc_MonitorAttack::CAIProc_MonitorAttack("),
                              (ai, "void CAIProc_MonitorAttack::Execute(")]:
        code += controller_method(source, signature) + "\n"
    code += r'''
void dump(CAIProc_MonitorAttack&p,CBaddy&b){
 std::cout<<"{\"state\":["<<p.field_10<<','<<u32(p.field_14)<<','<<p.field_34<<','<<b.field_288<<','<<player.mHealth<<"],\"events\":[";
 for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}
 std::cout<<"],\"points\":[";unsigned count=__builtin_popcount(u32(p.field_2C));
 for(unsigned i=0;i<count;i++){if(i)std::cout<<',';auto v=p.field_30[i];std::cout<<'['<<v.vx<<','<<v.vy<<','<<v.vz<<']';}
 std::cout<<"]}";}
int main(){int start,length;u32 mask,complete,actorFlags;
 while(std::cin>>start>>mask>>length>>complete>>actorFlags){
 CBaddy b;std::cin>>b.mPos.vx>>b.mPos.vy>>b.mPos.vz>>player.mPos.vx>>player.mPos.vy>>player.mPos.vz;
 b.field_288=actorFlags;CAIProc_MonitorAttack proc(&b,start,int(mask),length,int(complete));Next next;proc.mNext=&next;
 std::cout<<"{\"initial\":["<<proc.field_20<<','<<proc.field_24<<','<<proc.field_28<<','<<u32(proc.field_2C)<<','<<b.field_288<<"],\"steps\":[";
 for(int step=0;step<7;step++){
 int ticks;std::cin>>b.mAnim>>b.mFrame>>G_NUM_DOMES>>delta.vx>>delta.vy>>delta.vz>>collisionOrdinal>>returnedPart>>player.mHealth>>b.field_1FC>>ticks;
 events.clear();collisionCalls=0;proc.Execute();if(step)std::cout<<',';dump(proc,b);
 }
 std::cout<<"]}\n";std::free(proc.field_30);
 }
}
'''
    randomizer = random.Random(0x401DD0)
    masks = [0, 1, 136, 10, 4096, 23552, 0x80000000, 0x80000001, 0xFFFFFFFF]
    cases = []
    for index in range(300):
        start = randomizer.choice([0, 3, 11, -2, 2147483647])
        length = randomizer.choice([0, 1, 5, 10, 2147483647])
        mask = masks[index % len(masks)]
        completion = randomizer.choice([0, 0, 1, 0xF0F0, 0x12345678, 0xFFFFFFFF])
        position = [randomizer.randint(-2000000, 2000000) for _ in range(3)]
        player = position.copy() if index % 11 == 0 else [randomizer.randint(-2000000, 2000000) for _ in range(3)]
        steps = []
        frames = [start - 1, start, start + 1, start + 2, start + 3, start + length, start]
        frames = [((value + 2147483648) % 4294967296) - 2147483648 for value in frames]
        for step in range(7):
            steps.append([14 if step == 6 and index % 2 else 13, frames[step],
                          1 if step in [1, 2] and index % 5 == 0 else 0,
                          randomizer.randint(-1000000, 1000000), randomizer.randint(-1000000, 1000000), randomizer.randint(-1000000, 1000000),
                          randomizer.choice([-1, 0, 1, 2, 31]), randomizer.choice([0, 1, 17, 255, 65535]),
                          randomizer.choice([-1, 0, 5, 100, 32767]), randomizer.choice([0, 9, 10, 19, 20, 65535]),
                          randomizer.choice([-1, 0, 1, 2, 50])])
        cases.append(dict(start=start, mask=mask, length=length, completionMask=completion,
                          actorCompletionFlags=randomizer.getrandbits(32), position=position, player=player, steps=steps))
    requests = "".join(" ".join(map(str, [case["start"], case["mask"], case["length"], case["completionMask"],
                                        case["actorCompletionFlags"], *case["position"], *case["player"],
                                        *(value for step in case["steps"] for value in step)])) + "\n" for case in cases)
    with tempfile.TemporaryDirectory(prefix="openspidey-attack-monitor-reference-") as directory:
        path = Path(directory)
        (path / "reference.cpp").write_text(code)
        subprocess.run(["c++", "-std=c++17", "-O0", "-fwrapv", str(path / "reference.cpp"),
                        "-o", str(path / "reference")], check=True)
        outputs = subprocess.check_output([str(path / "reference")], input=requests.encode()).decode().splitlines()
    for case, output in zip(cases, outputs, strict=True):
        case["expected"] = json.loads(output)
    destination = root / "modern/OpenSpidey.Tests/Fixtures/actor-attack-monitor.json"
    destination.write_text(json.dumps(cases, separators=(",", ":")) + "\n")
    print(f"Generated {len(cases)} monitor sequences / {len(cases)*7} source updates at {destination}")


if __name__ == "__main__":
    main()
