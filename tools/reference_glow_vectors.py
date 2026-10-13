"""Compile CGlow construction/setters, optionally checking original PC machine code."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original-exe", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    source = (root / "src/engine/world/bit.cpp").read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <cassert>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <vector>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
void print_if_false(bool condition,const char*){assert(condition);}
void DoAssert(bool condition,const char*){assert(condition);}
void* DCMem_New(size_t count,int,int,int,int){return std::calloc(1,count);}
struct SSection{u32 Radius=0,PadBGR=0;};
struct SFringeQuad{u32 Width=0,CodeBGR=0;};
struct CGlow;
using CBit=CGlow;
CBit* G_GLOW_LIST=nullptr;
struct CGlow{
 CVector mPos;SSection*mpSections=nullptr;SFringeQuad*mpFringes=nullptr;
 u32 mNumSections=0,mNumFringes=0,mCentreCodeBGR=0,mMask=0;
 i16 mStepAngle=0;u8 mSkipTriangles=0;u16 mAngle=0;
 CGlow(u32,u32);CGlow(CVector*,i32,i32,u8,u8,u8,u8,u8,u8);
 ~CGlow(){std::free(mpSections);}void AttachTo(void*){}
 void SetRadius(int);void SetCentreRGB(u8,u8,u8);void SetRGB(u8,u8,u8);
 void SetFringeWidth(u32,u32);void SetFringeRGB(u32,u8,u8,u8);
};
'''
    for signature in ["CGlow::CGlow(u32 NumPoints", "CGlow::CGlow(\n", "void CGlow::SetRadius(",
                      "void CGlow::SetCentreRGB(", "void CGlow::SetRGB(", "void CGlow::SetFringeWidth(", "void CGlow::SetFringeRGB("]:
        code += controller_method(source, signature) + "\n"
    code += r'''
#ifdef VALIDATE_ORIGINAL
std::vector<void*> allocations;
void* allocate(int bytes,int,int,int,int){void*p=std::calloc(1,bytes);allocations.push_back(p);return p;}
void __attribute__((thiscall)) attach(void*,void*){}
void patch(unsigned address,void*function){auto p=reinterpret_cast<u8*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
void omitWindowsUnwind(unsigned read,unsigned write){
 // Keep the stack layout intact; only Windows FS-based exception registration
 // is replaced. All constructor arithmetic, branches and field writes run.
 auto p=reinterpret_cast<u8*>(read);assert(p[0]==0x64&&p[1]==0xA1);
 p[0]=0x31;p[1]=0xC0;std::memset(p+2,0x90,4);
 p=reinterpret_cast<u8*>(write);assert(p[0]==0x64&&p[1]==0x89);std::memset(p,0x90,7);
}
void verify(const CGlow&state,u8*raw){
 assert(!std::memcmp(raw+0x10,&state.mPos,12));
 assert(*reinterpret_cast<u32*>(raw+0x44)==state.mNumSections&&*reinterpret_cast<u32*>(raw+0x48)==state.mNumFringes);
 assert(*reinterpret_cast<u32*>(raw+0x4C)==state.mCentreCodeBGR&&*reinterpret_cast<i16*>(raw+0x50)==state.mStepAngle);
 assert(raw[0x52]==state.mSkipTriangles&&*reinterpret_cast<u16*>(raw+0x54)==state.mAngle&&*reinterpret_cast<u32*>(raw+0x58)==state.mMask);
 assert(!std::memcmp(*reinterpret_cast<void**>(raw+0x3C),state.mpSections,state.mNumSections*8));
 auto actual=*reinterpret_cast<SFringeQuad**>(raw+0x40);
 for(u32 i=0;i<state.mNumSections*state.mNumFringes;i++){
  if(std::memcmp(actual+i,state.mpFringes+i,8))std::cerr<<"Fringe mismatch sections="<<state.mNumSections<<" rows="<<state.mNumFringes<<" index="<<i
   <<" width="<<actual[i].Width<<'/'<<state.mpFringes[i].Width<<" color="<<actual[i].CodeBGR<<'/'<<state.mpFringes[i].CodeBGR<<'\n';
  assert(!std::memcmp(actual+i,state.mpFringes+i,8));
 }
}
#endif
void emit(const CGlow&state){
 std::cout<<'['<<state.mPos.vx<<','<<state.mPos.vy<<','<<state.mPos.vz<<','<<state.mNumSections<<','<<state.mNumFringes
 <<','<<state.mCentreCodeBGR<<','<<state.mStepAngle<<','<<int(state.mSkipTriangles)<<','<<state.mAngle<<','<<state.mMask;
 for(u32 i=0;i<state.mNumSections;i++)std::cout<<','<<state.mpSections[i].Radius<<','<<state.mpSections[i].PadBGR;
 for(u32 i=0;i<state.mNumSections*state.mNumFringes;i++)std::cout<<','<<state.mpFringes[i].Width<<','<<state.mpFringes[i].CodeBGR;
 std::cout<<']';
}
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
 assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 patch(0x4581A0,reinterpret_cast<void*>(&allocate));patch(0x4088E0,reinterpret_cast<void*>(&attach));
 omitWindowsUnwind(0x40C217,0x40C21E);omitWindowsUnwind(0x40C357,0x40C35E);
 for(unsigned address:{0x40C31Du,0x40C4C7u,0x40C4E3u})std::memset(reinterpret_cast<void*>(address),0x90,7);
#endif
 long long a[10];int count;
 while(std::cin>>a[0]){
  for(int i=1;i<10;i++)std::cin>>a[i];std::cin>>count;CVector position{int(a[3]),int(a[4]),int(a[5])};
  auto state=a[0]==0?new CGlow(a[1],a[2]):new CGlow(&position,a[6],a[7],a[8],a[8]>>8,a[8]>>16,a[9],a[9]>>8,a[9]>>16);
#ifdef VALIDATE_ORIGINAL
  u8 raw[0x5C]={};
  *reinterpret_cast<void**>(0x56E9E4)=nullptr;
  if(a[0]==0)reinterpret_cast<void(__attribute__((thiscall)) *)(void*,u32,u32)>(0x40C210)(raw,a[1],a[2]);
  else reinterpret_cast<void(__attribute__((thiscall)) *)(void*,CVector*,i32,i32,u8,u8,u8,u8,u8,u8)>(0x40C350)
   (raw,&position,a[6],a[7],a[8],a[8]>>8,a[8]>>16,a[9],a[9]>>8,a[9]>>16);
  verify(*state,raw);
#endif
  std::cout<<'[';emit(*state);
  for(int i=0;i<count;i++){
   long long op[5];for(int j=0;j<5;j++)std::cin>>op[j];
   switch(op[0]){
    case 0:state->SetRadius(op[1]);break;
    case 1:state->SetCentreRGB(op[1],op[2],op[3]);break;
    case 2:state->SetRGB(op[1],op[2],op[3]);break;
    case 3:state->SetFringeWidth(op[1],op[2]);break;
    case 4:state->SetFringeRGB(op[1],op[2],op[3],op[4]);break;
   }
#ifdef VALIDATE_ORIGINAL
   switch(op[0]){
    case 0:reinterpret_cast<void(__attribute__((thiscall)) *)(void*,i32)>(0x40C580)(raw,op[1]);break;
    case 1:reinterpret_cast<void(__attribute__((thiscall)) *)(void*,u8,u8,u8)>(0x40C600)(raw,op[1],op[2],op[3]);break;
    case 2:reinterpret_cast<void(__attribute__((thiscall)) *)(void*,u8,u8,u8)>(0x40C630)(raw,op[1],op[2],op[3]);break;
    case 3:reinterpret_cast<void(__attribute__((thiscall)) *)(void*,u32,u32)>(0x40C5B0)(raw,op[1],op[2]);break;
    case 4:reinterpret_cast<void(__attribute__((thiscall)) *)(void*,u32,u8,u8,u8)>(0x40C670)(raw,op[1],op[2],op[3],op[4]);break;
   }
   verify(*state,raw);
#endif
   std::cout<<',';emit(*state);
  }
  std::cout<<"]\n";delete state;
#ifdef VALIDATE_ORIGINAL
  for(void*p:allocations)std::free(p);allocations.clear();
#endif
 }
}
'''
    rng = random.Random(0x40C210)
    cases = []
    for mode in [0, 1]:
        for points in [1, 2, 3, 4, 5, 8, 16]:
            for fringes in [0, 1, 2, 3, 4]:
                constructor = [mode, points, fringes, *[rng.randint(-2147483648, 2147483647) for _ in range(3)],
                               rng.choice([-2147483648, -1, 0, 32, 2147483647]), rng.choice([-1, 0, 16, 2147483647]),
                               rng.randrange(1 << 32), rng.randrange(1 << 32)]
                actual_points, actual_fringes = (points, fringes) if mode == 0 else (4, 1)
                actions = []
                for _ in range(32):
                    op = rng.randrange(5 if actual_fringes else 3)
                    if op == 0:
                        actions.append([op, rng.choice([-2147483648, -1, 0, 32768, 2147483647]), 0, 0, 0])
                    elif op in [1, 2]:
                        actions.append([op, *[rng.randrange(256) for _ in range(3)], 0])
                    else:
                        fringe = rng.randrange(actual_fringes)
                        if op == 3:
                            actions.append([op, fringe, rng.choice([0, 1, 65535, 2147483648, 4294967295]), 0, 0])
                        else:
                            actions.append([op, fringe, *[rng.randrange(256) for _ in range(3)]])
                cases.append(dict(constructor=constructor, actions=actions))
    # Actual grenade row0/row1 writes verify the independent fringe colors.
    cases.append(dict(constructor=[0, 4, 2, 0, 0, 0, 0, 0, 0, 0], actions=[
        [3, 0, 16, 0, 0], [3, 1, 16, 0, 0], [1, 0, 0, 0, 0], [2, 0, 0, 0, 0],
        [4, 0, 100, 50, 0], [4, 1, 0, 0, 0], [0, 0, 0, 0, 0]]))
    requests = "".join(" ".join(map(str, case["constructor"] + [len(case["actions"])] +
                                    [value for action in case["actions"] for value in action])) + "\n" for case in cases)
    with tempfile.TemporaryDirectory(prefix="openspidey-glow-reference-") as folder:
        path = Path(folder)
        (path / "reference.cpp").write_text(code)
        flags = ["-m32", "-fno-pie", "-no-pie", "-DVALIDATE_ORIGINAL"] if args.original_exe else []
        subprocess.run(["g++", "-std=c++17", "-O0", "-fwrapv", *flags, "-I", str(root / "src"),
                        "-I", str(root / "tests/runtime"), str(path / "reference.cpp"),
                        str(root / "src/core/fixed_vector.cpp"), "-o", str(path / "reference")], check=True)
        command = [str(path / "reference")]
        if args.original_exe:
            command.append(str(args.original_exe.resolve()))
        lines = subprocess.check_output(command, input=requests.encode()).decode().splitlines()
    for case, line in zip(cases, lines, strict=True):
        case["expected"] = json.loads(line)
    (root / "modern/OpenSpidey.Tests/Fixtures/glow.json").write_text(json.dumps(cases, separators=(",", ":")) + "\n")
    print(f"Generated {len(cases)} CGlow construction/setter sequences.")
    if args.original_exe:
        print("Both constructors and every setter match original PC machine code, including the corrected fringe RGB stride.")


if __name__ == "__main__":
    main()
