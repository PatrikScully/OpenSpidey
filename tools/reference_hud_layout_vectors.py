"""Compare HUD sprite rectangles with original Panel_SetStretchedScreenCoords machine code."""
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
    source = (root / "src/game/ui/panel.cpp").read_text()
    code = r'''
#include <iostream>
#include "core/types.h"
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
struct SAnimFrame { char OffX,OffY;u8 Width,Height;void*pTexture; };
struct POLY_FT4 { u32 tag,color;i16 x0,y0;u32 uv0;i16 x1,y1;u32 uv1;i16 x2,y2;u32 uv2;i16 x3,y3;u32 uv3; };
'''
    code += controller_method(source, "void Panel_SetStretchedScreenCoords(\n\t\ti32 a1,") + "\n"
    code += r'''
int main(int argc,char **argv) {
#ifdef VALIDATE_ORIGINAL
 assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 static_assert(sizeof(SAnimFrame)==8&&sizeof(POLY_FT4)==40);
#endif
 int x,y,width,height,offX,offY,frameWidth,frameHeight;
 while(std::cin>>x>>y>>width>>height>>offX>>offY>>frameWidth>>frameHeight) {
  SAnimFrame frame{(char)offX,(char)offY,(u8)frameWidth,(u8)frameHeight,nullptr};
  POLY_FT4 source{};Panel_SetStretchedScreenCoords(x,y,&source,&frame,width,height);
#ifdef VALIDATE_ORIGINAL
  POLY_FT4 original{};
  reinterpret_cast<void(*)(int,int,POLY_FT4*,SAnimFrame*,int,int)>(0x462C30)(x,y,&original,&frame,width,height);
  assert(!std::memcmp(&original,&source,sizeof(original)));
#endif
  std::cout<<'['<<source.x0<<','<<source.y0<<','<<source.x1<<','<<source.y1<<','<<source.x2<<','<<source.y2<<','<<source.x3<<','<<source.y3<<"]\n";
 }
}
'''
    # Genuine Sp/Webcart rectangles from the supplied bits sequencer, plus short/rounding boundaries.
    cases = [[67,45,28,30,-32,-32,64,64], [85,36,15,15,-16,-16,32,32], [110,36,15,15,-16,-16,32,32],
             [132,36,11,16,-16,-16,32,32], [53,59,14,16,-16,-16,32,32], [53,73,14,12,-16,-16,32,32],
             [80,58,20,16,-16,-16,32,32], [458,215,32,40,-32,-32,64,64]]
    rng = random.Random(0x462C30)
    for _ in range(512):
        cases.append([rng.choice([-2147483648,-32768,-1,0,32767,2147483647]), rng.randint(-2147483648,2147483647),
                      rng.choice([-2147483648,-32768,-3,0,1,2,3,11,15,28,32767,2147483647]),
                      rng.choice([-2147483648,-32768,-1,0,1,32767,2147483647]), rng.randint(-128,127), rng.randint(-128,127),
                      rng.randrange(256), rng.randrange(256)])
    with tempfile.TemporaryDirectory(prefix="openspidey-hud-layout-reference-") as folder:
        cpp = Path(folder) / "reference.cpp"
        binary = Path(folder) / "reference"
        cpp.write_text(code)
        flags = ["-m32", "-fno-pie", "-no-pie", "-DVALIDATE_ORIGINAL"] if args.original_exe else []
        subprocess.run(["g++", "-std=c++17", "-O0", "-fwrapv", *flags, "-I", str(root / "src"),
                        "-I", str(root / "tests/runtime"), str(cpp), "-o", str(binary)], check=True)
        command = [str(binary)]
        if args.original_exe: command.append(str(args.original_exe.resolve()))
        output = subprocess.check_output(command, input=("\n".join(" ".join(map(str, case)) for case in cases) + "\n").encode()).decode().splitlines()
    (root / "modern/OpenSpidey.Tests/Fixtures/hud-layout.json").write_text(json.dumps(
        [dict(input=case, expected=json.loads(line)) for case,line in zip(cases,output,strict=True)],separators=(",", ":")) + "\n")
    print(f"Generated {len(cases)} source HUD sprite rectangles.")
    if args.original_exe: print("Every vertex matches original PC Panel_SetStretchedScreenCoords instructions.")


if __name__ == "__main__":
    main()
