"""Compare every stand-animation branch with the original PC method at 0x4BE4B0."""
import argparse
import json
from pathlib import Path
import random
import shutil
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original-exe", type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "core/types.h"
#include "original_executable.h"
#include <iostream>
struct Object { int field_10C; };
int words[300][2],*G_SPIDEY_SFX_ENTRY[300];
int selected=-1,first=0,last=-1,resets=0;
void resetWords(int clip){resets++;for(int*p=G_SPIDEY_SFX_ENTRY[clip];*p!=-1;p++)*p&=0xFFFF;}
struct CPlayer {
 u16 mAnim; i16 mFrame; signed char mAnimDir; u8 mIsCrawling;
 Object*mHeldObject; int*field_350; unsigned mActionState;
 void SwitchToStandMode();
 void ResetSFXArrayEntry(int clip){resetWords(clip);}
 void RunAnim(int clip,int from,int to){selected=clip;first=from;last=to;}
};
'''
    code += controller_method((root / "src/game/player/spidey.cpp").read_text(), "void CPlayer::SwitchToStandMode(")
    code += r'''
void __attribute__((thiscall)) originalRun(void*,int clip,int from,int to){selected=clip;first=from;last=to;}
void __attribute__((thiscall)) originalReset(void*,int clip){resetWords(clip);}
void patch(unsigned address,void*function){auto p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
int main(int argc,char**argv){
 assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 patch(0x460C10,(void*)&originalRun);patch(0x4C9100,(void*)&originalReset);
 int clip,frame,direction,crawl,held,present;
 while(std::cin>>clip>>frame>>direction>>crawl>>held>>present){
  for(int i=0;i<300;i++){words[i][0]=int(0xABCD0123);words[i][1]=-1;G_SPIDEY_SFX_ENTRY[i]=present?words[i]:nullptr;}
  Object object{held}; CPlayer p{(u16)clip,(i16)frame,(signed char)direction,(u8)crawl,held==-1?nullptr:&object,words[299],0x12345678};
  selected=-1;first=0;last=-1;resets=0;p.SwitchToStandMode();
  int expectedSelected=selected,expectedFirst=first,expectedLast=last,expectedResets=resets;
  int expectedSound=p.field_350?(int)(p.field_350-&words[0][0])/2:-1;
  int expectedWord=p.field_350?*p.field_350:0;
  for(auto&entry:words)entry[0]=int(0xABCD0123);
  alignas(4)unsigned char raw[0xED0]={},rawObject[0x110]={};
  *reinterpret_cast<i16*>(raw+0x128)=frame;*reinterpret_cast<u16*>(raw+0x12A)=clip;
  raw[0x141]=direction;raw[0xAD4]=crawl;
  *reinterpret_cast<int*>(rawObject+0x10C)=held;
  *reinterpret_cast<void**>(raw+0xE48)=held==-1?nullptr:rawObject;
  *reinterpret_cast<void**>(raw+0x350)=words[299];
  *reinterpret_cast<unsigned*>(raw+0xE1C)=0x12345678;
  for(int i=0;i<300;i++)reinterpret_cast<int**>(0x6A82B8)[i]=G_SPIDEY_SFX_ENTRY[i];
  selected=-1;first=0;last=-1;resets=0;
  reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4BE4B0)(raw);
  auto sound=*reinterpret_cast<int**>(raw+0x350);
  assert(selected==expectedSelected&&first==expectedFirst&&last==expectedLast);
  assert((sound?(int)(sound-&words[0][0])/2:-1)==expectedSound&&*reinterpret_cast<unsigned*>(raw+0xE1C)==p.mActionState);
  assert((sound?*sound:0)==expectedWord);
  std::cout<<'['<<(selected!=-1)<<','<<(selected==-1?clip:selected)<<','<<first<<','<<last<<','<<expectedSound<<','<<expectedResets<<','<<p.mActionState<<"]\n";
 }
}
'''
    cases = [[clip, 0, 0, 0, -1, 0] for clip in range(300)]
    clips = [0, 14, 20, 21, 50, 51, 52, 57, 58, 60, 63, 66, 69, 72, 75, 78, 81, 129, 130, 133, 134, 190, 194, 196, 200]
    for clip in clips:
        for frame in [-1, 3, 4, 9, 10, 14, 15, 17, 18, 32767]:
            for direction in [-1, 0, 1]:
                for crawl in [0, 1]:
                    for held in [-1, 0, 8]:
                        cases.append([clip, frame, direction, crawl, held, (frame + direction) & 1])
    rng = random.Random(0x4BE4B0)
    for _ in range(256):
        cases.append([rng.randrange(65536), rng.randint(-32768, 32767), rng.randint(-128, 127), rng.randrange(2), rng.choice([-1, 0, 8, 9, 256]), rng.randrange(2)])
    with tempfile.TemporaryDirectory(prefix="openspidey-stand-reference-") as folder:
        cpp = Path(folder) / "reference.cpp"
        binary = Path(folder) / "reference"
        cpp.write_text(code)
        subprocess.run(["g++", "-std=c++17", "-m32", "-fno-pie", "-no-pie", "-fwrapv", "-I", str(root / "src"),
                        "-I", str(root / "tests/runtime"), str(cpp), "-o", str(binary)], check=True)
        rows = "\n".join(" ".join(map(str, case)) for case in cases)+"\n"
        try:
            output = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=rows.encode()).decode().splitlines()
        except subprocess.CalledProcessError:
            shutil.copy2(cpp, "/tmp/openspidey-stand-diagnostic.cpp")
            shutil.copy2(binary, "/tmp/openspidey-stand-diagnostic")
            Path("/tmp/openspidey-stand-diagnostic-input.txt").write_text(rows)
            raise
    (root / "modern/OpenSpidey.Tests/Fixtures/player-stand.json").write_text(json.dumps(
        {"originalPcStandVerified": True, "cases": [dict(input=case, expected=json.loads(line))
        for case, line in zip(cases, output, strict=True)]}, separators=(",", ":"))+"\n")
    print(f"Verified {len(cases)} stand animation choices against original PC instructions, including carrying and source sound selection.")


if __name__ == "__main__":
    main()
