"""Compare recovered palette processing with original PC Pal_ProcessPalette, including its RGB-preserving opaque branch."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original-exe",type=Path)
    args=parser.parse_args()
    root=Path(__file__).resolve().parent.parent
    code=r'''
#include <iostream>
#include <vector>
#include "core/types.h"
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
'''
    code+=controller_method((root/"src/engine/rendering/pal.cpp").read_text(),"void Pal_ProcessPalette(")+"\n"
    code+=r'''
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);
#endif
int count;while(std::cin>>count){std::vector<u16>palette(count);unsigned value;for(auto&colour:palette){std::cin>>value;colour=value;}auto original=palette;
Pal_ProcessPalette(palette.data(),count);
#ifdef VALIDATE_ORIGINAL
reinterpret_cast<void(*)(u16*,int)>(0x4618B0)(original.data(),count);assert(original==palette);
#endif
std::cout<<'[';for(int i=0;i<count;i++){if(i)std::cout<<',';std::cout<<palette[i];}std::cout<<"]\n";}}
'''
    rng=random.Random(0x4618B0)
    cases=[]
    for count in [16,256]:
        cases.extend([[0]*count,[0x7C1F]*count,[0xFC1F]*count,[0x1234]*count,
                      [0,0x7C1F,0x8000,*([0x1234]*(count-3))]])
        for _ in range(128):
            cases.append([rng.randrange(65536) for _ in range(count)])
            mixed=[rng.randrange(65536) for _ in range(count)]
            mixed[rng.randrange(count)]=rng.choice([0,0x7C1F,0xFC1F])
            cases.append(mixed)
    with tempfile.TemporaryDirectory(prefix="openspidey-palette-reference-") as folder:
        cpp=Path(folder)/"reference.cpp";binary=Path(folder)/"reference";cpp.write_text(code)
        flags=["-m32","-fno-pie","-no-pie","-DVALIDATE_ORIGINAL"] if args.original_exe else []
        subprocess.run(["g++","-std=c++17","-O0","-fwrapv",*flags,"-I",str(root/"src"),"-I",str(root/"tests/runtime"),str(cpp),"-o",str(binary)],check=True)
        command=[str(binary)]
        if args.original_exe:command.append(str(args.original_exe.resolve()))
        output=subprocess.check_output(command,input=("\n".join(str(len(c))+" "+" ".join(map(str,c)) for c in cases)+"\n").encode()).decode().splitlines()
    (root/"modern/OpenSpidey.Tests/Fixtures/psx-palette.json").write_text(json.dumps([dict(input=case,expected=json.loads(line))
        for case,line in zip(cases,output,strict=True)],separators=(",", ":"))+"\n")
    print(f"Generated {len(cases)} PSX palettes; direct original PC comparison when requested.")


if __name__=="__main__":main()
