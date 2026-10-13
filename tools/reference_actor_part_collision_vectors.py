"""Generate posed-part collision fixtures from recovered source, optionally checking original PC machine code."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original-exe", type=Path, help="Privately installed executable for Linux/i386 validation")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    source = (root / "src/game/player/web.cpp").read_text()
    maths = (root / "src/engine/rendering/ps2funcs.cpp").read_text()
    code = r'''
#include <algorithm>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <iostream>
#include <vector>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
using i32=int32_t;using i16=int16_t;using u32=uint32_t;using u16=uint16_t;using u8=uint8_t;
struct CVector{int vx=0,vy=0,vz=0;};
struct VECTOR{int vx=0,vy=0,vz=0,pad=0;};
struct SVECTOR{i16 vx=0,vy=0,vz=0,pad=0;};
struct MATRIX{i16 m[3][3];int t[3];};
struct SMatrix{i16 m[3][3];i16 t[3];};
struct SHook{struct{i16 vx=0,vy=0,vz=0;}Part;i16 Offset=0;};
struct CSuper{CVector mPos;MATRIX mTransform;u8 mRegion=0;u32 field_194=0;};
i16 G_ROT_MATRIX[3][3];int G_VERTEX_REGISTER[4];VECTOR G_OP12_RESULT,G_GENERAL_LONG_VECTOR;
enum GTREGType{GT_ZERO,GT_ONE};
void print_if_false(bool condition,const char*){if(!condition)std::abort();}
void MTC2(i32 value,GTREGType slot){if(slot==GT_ZERO){G_VERTEX_REGISTER[0]=i16(value);G_VERTEX_REGISTER[1]=value>>16;}
 else G_VERTEX_REGISTER[2]=i16(value);}
void gte_ldlvl(VECTOR*p){G_OP12_RESULT.vx=p->vx;G_OP12_RESULT.vy=p->vy;G_OP12_RESULT.vz=p->vz;}
void gte_stlvnl(VECTOR*p){p->vx=G_GENERAL_LONG_VECTOR.vx;p->vy=G_GENERAL_LONG_VECTOR.vy;p->vz=G_GENERAL_LONG_VECTOR.vz;}
i32**CItemRelatedList[17];i32**gWebHookCountTable[17];SMatrix*poses;
SMatrix*Decomp_GetAnimTransform(CSuper*){return poses;}
'''
    for signature in ["void M3dMaths_TransposeMatrix1(", "void gte_mvmva(", "void gte_stsv(",
                      "void gte_sqr0(", "i32 M3dMaths_SquareRoot0("]:
        code += controller_method(maths, signature) + "\n"
    code += controller_method(source, "i32 BoundingBoxCollisionCheck(") + "\n"
    code += controller_method(source, "i32 Web_CollideWithSuper(") + "\n"
    code += r'''
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
 assert(argc==2);original_executable::loadOriginalImage(argv[1]);
#endif
 int mode;
 while(std::cin>>mode){if(mode==0){
  SVECTOR upper,lower,start,end;
  std::cin>>upper.vx>>lower.vx>>upper.vy>>lower.vy>>upper.vz>>lower.vz>>start.vx>>start.vy>>start.vz>>end.vx>>end.vy>>end.vz;
  SVECTOR originalEnd=end;int hit=BoundingBoxCollisionCheck(&upper,&lower,&start,&end);
#ifdef VALIDATE_ORIGINAL
  int originalHit=reinterpret_cast<int(*)(SVECTOR*,SVECTOR*,SVECTOR*,SVECTOR*)>(0x4F7680)(&upper,&lower,&start,&originalEnd);
  assert(originalHit==hit&&originalEnd.vx==end.vx&&originalEnd.vy==end.vy&&originalEnd.vz==end.vz);
#endif
  std::cout<<'['<<hit<<','<<end.vx<<','<<end.vy<<','<<end.vz<<"]\n";
 }else{
  CSuper actor;CVector from,to;int count,scale;
  std::cin>>actor.mPos.vx>>actor.mPos.vy>>actor.mPos.vz;
  for(auto&row:actor.mTransform.m)for(auto&cell:row)std::cin>>cell;
  std::cin>>actor.field_194>>from.vx>>from.vy>>from.vz>>to.vx>>to.vy>>to.vz>>scale>>count;
  std::vector<SMatrix>poseStorage(count);poses=poseStorage.data();
  std::vector<std::vector<i16>>records(count,std::vector<i16>(14));std::vector<i32*>modelPointers(count);
  for(int i=0;i<count;i++){
   for(auto&row:poses[i].m)for(auto&cell:row)std::cin>>cell;
   for(auto&cell:poses[i].t)std::cin>>cell;
   for(int box=0;box<6;box++)std::cin>>records[i][6+box];
   modelPointers[i]=reinterpret_cast<i32*>(records[i].data());
  }
  int psxHeader[3]={0,0,count};CItemRelatedList[0]=modelPointers.data();gWebHookCountTable[0]=reinterpret_cast<i32**>(psxHeader);
  SHook hook;int hit=Web_CollideWithSuper(&actor,&from,&to,&hook,scale);
#ifdef VALIDATE_ORIGINAL
  std::vector<u32>originalModelTable(count+1);originalModelTable[0]=count;
  for(int i=0;i<count;i++)originalModelTable[i+1]=reinterpret_cast<u32>(modelPointers[i]);
  std::vector<u8>animation(20+count*sizeof(SMatrix));u32*header=reinterpret_cast<u32*>(animation.data());
  header[0]=42;header[1]=animation.size()-8;header[2]=1;header[3]=12;header[4]=1;
  std::memcpy(animation.data()+20,poses,count*sizeof(SMatrix));
  *reinterpret_cast<u32*>(0x6B2454)=reinterpret_cast<u32>(originalModelTable.data()+1);
  *reinterpret_cast<u32*>(0x6B2458)=reinterpret_cast<u32>(psxHeader);
  *reinterpret_cast<u32*>(0x6B245C)=reinterpret_cast<u32>(animation.data()+8);
  alignas(4)u8 originalActor[0x200]={};
  std::memcpy(originalActor+8,&actor.mPos,sizeof(CVector));
  std::memcpy(originalActor+0x164,&actor.mTransform,sizeof(MATRIX));
  std::memcpy(originalActor+0x194,&actor.field_194,4);
  SHook originalHook;int originalHit=reinterpret_cast<int(*)(void*,CVector*,CVector*,SHook*,int)>(0x4F7AE0)(originalActor,&from,&to,&originalHook,scale);
  if(originalHit!=hit||originalHook.Offset!=hook.Offset||originalHook.Part.vx!=hook.Part.vx||originalHook.Part.vy!=hook.Part.vy||originalHook.Part.vz!=hook.Part.vz){
   std::cerr<<"Original posed sweep differs: recovered="<<hit<<','<<hook.Offset<<','<<hook.Part.vx<<','<<hook.Part.vy<<','<<hook.Part.vz
   <<" original="<<originalHit<<','<<originalHook.Offset<<','<<originalHook.Part.vx<<','<<originalHook.Part.vy<<','<<originalHook.Part.vz<<'\n';return 2;}
#endif
  std::cout<<'['<<hit<<','<<hook.Part.vx<<','<<hook.Part.vy<<','<<hook.Part.vz<<','<<u16(hook.Offset)<<"]\n";
 }}
}
'''
    identity = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096]
    yaw = [0, 0, 4096, 0, 4096, 0, -4096, 0, 0]
    randomizer = random.Random(0x4F7AE0)
    box_cases = []
    for axis in range(3):
        for sign in [-1, 1]:
            for start_amount, end_amount in [(20, -20), (20, 0), (0, 0), (10, 0), (20, 10), (20, 20)]:
                start = [0, 0, 0]; end = [0, 0, 0]
                start[axis] = sign * start_amount; end[axis] = sign * end_amount
                box_cases.append(dict(box=[10, -10, 10, -10, 10, -10], start=start, end=end))
    for _ in range(364):
        upper = [randomizer.randint(1, 32767) for _ in range(3)]
        lower = [randomizer.randint(-32768, -1) for _ in range(3)]
        box_cases.append(dict(box=[value for pair in zip(upper, lower) for value in pair],
                              start=[randomizer.randint(-32768, 32767) for _ in range(3)],
                              end=[randomizer.randint(-32768, 32767) for _ in range(3)]))
    posed_cases = []
    for index in range(400):
        count = randomizer.choice([1, 2, 3, 4, 18])
        position = [randomizer.randint(-10000000, 10000000) for _ in range(3)]
        rotation = randomizer.choice([identity, yaw])
        scale = randomizer.choice([0, 2048, 4096, 8192, 0x7FFFFFFF, -4096])
        hidden = randomizer.getrandbits(32) if index % 4 == 0 else 0
        parts = []
        for part in range(count):
            matrix = randomizer.choice([identity, yaw, [randomizer.randint(-4096, 4096) for _ in range(9)]])
            pivot = [randomizer.randint(-2048, 2048) for _ in range(3)]
            box = [50, -50, 50, -50, 50, -50] if index % 3 == 0 else [20, -10, 40, -30, 15, -5]
            parts.append(dict(pose=matrix+pivot, box=box))
        start = [position[0] + 200 * 4096, position[1], position[2]]
        end = [position[0] - 200 * 4096, position[1], position[2]]
        if index % 3 == 1:
            start = [position[i] + randomizer.randint(-40000, 40000) * 4096 for i in range(3)]
            end = [position[i] + randomizer.randint(-40000, 40000) * 4096 for i in range(3)]
        posed_cases.append(dict(position=position, rotation=rotation, hidden=hidden, scale=scale,
                                start=start, end=end, parts=parts))
    requests = "".join(" ".join(map(str, [0, *case["box"], *case["start"], *case["end"]])) + "\n" for case in box_cases)
    requests += "".join(" ".join(map(str, [1, *case["position"], *case["rotation"], case["hidden"],
                                         *case["start"], *case["end"], case["scale"], len(case["parts"]),
                                         *(value for part in case["parts"] for value in part["pose"]+part["box"])])) + "\n"
                        for case in posed_cases)
    with tempfile.TemporaryDirectory(prefix="openspidey-part-collision-reference-") as directory:
        path = Path(directory);(path/"reference.cpp").write_text(code)
        flags = ["-m32", "-fno-pie", "-no-pie", "-DVALIDATE_ORIGINAL"] if args.original_exe else []
        subprocess.run(["c++", "-std=c++17", "-O0", "-fwrapv", *flags, "-I", str(root/"src"),
                        "-I", str(root/"tests/runtime"), str(path/"reference.cpp"), "-o", str(path/"reference")], check=True)
        command = [str(path/"reference")]
        if args.original_exe:command.append(str(args.original_exe.resolve()))
        outputs = subprocess.check_output(command, input=requests.encode()).decode().splitlines()
    for case, output in zip(box_cases+posed_cases, outputs, strict=True):case["expected"]=json.loads(output)
    destination = root/"modern/OpenSpidey.Tests/Fixtures/actor-part-collision.json"
    destination.write_text(json.dumps(dict(boxes=box_cases, posed=posed_cases),separators=(",", ":"))+"\n")
    print(f"Generated {len(box_cases)} box clips and {len(posed_cases)} posed-part sweeps at {destination}")
    if args.original_exe:print("All clips and posed sweeps also match original PC 0x4F7680 and 0x4F7AE0 machine code.")


if __name__ == "__main__":main()
