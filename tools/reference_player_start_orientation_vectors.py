"""Capture untouched PC SetStartOrientation/OrientToNormal math and caches; only stand-mode side effects are observed at their genuine call boundary."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <iostream>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
int stands;
void __attribute__((thiscall)) stand(void*){++stands;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4BE4B0,(void*)&stand);
 int matrix[9],normal[3],cache[3],delta,angles[3],repeats;
 while(std::cin>>matrix[0]){for(int i=1;i<9;i++)std::cin>>matrix[i];for(int&i:normal)std::cin>>i;for(int&i:cache)std::cin>>i;std::cin>>delta;for(int&i:angles)std::cin>>i;std::cin>>repeats;
 alignas(4)unsigned char player[0xF20]={};short rotation[4]={short(angles[0]),short(angles[1]),short(angles[2]),0};
 for(int i=0;i<9;i++)at<short>(player,0x164+i*2)=matrix[i];for(int i=0;i<3;i++){at<short>(player,0xA8+i*2)=normal[i];*reinterpret_cast<int*>(0x6A9074+i*4)=cache[i];}at<int>(player,0x548)=delta;
 stands=0;std::cout<<'[';for(int frame=0;frame<repeats;frame++){reinterpret_cast<void(__attribute__((thiscall))*)(void*,void*)>(0x4B9E50)(player,rotation);if(frame)std::cout<<',';
 std::cout<<'[';bool first=true;auto emit=[&](long long v){if(!first)std::cout<<',';first=false;std::cout<<v;};
 for(int i=0;i<9;i++)emit(at<short>(player,0x164+i*2));for(int i=0;i<3;i++)emit(*reinterpret_cast<int*>(0x6A9074+i*4));
 for(int i=0;i<9;i++)emit(at<int>(player,0xC6C+i*4));for(int i=0;i<3;i++)emit(at<int>(player,0xD18+i*4));for(int i=0;i<9;i++)emit(at<short>(player,0x89C+i*2));emit(stands);std::cout<<']';}std::cout<<"]\n";
 }}
'''
    rng = random.Random(0x4B9E50)
    bases = [[-4096, 0, 0, 0, 4096, 0, 0, 0, -4096],
             [4096, 0, 0, 0, 4096, 0, 0, 0, 4096],
             [4096, 0, 0, 0, 0, -4096, 0, 4096, 0],
             [0] * 9, [0, 0, 4096, 0, 4096, 0, -4096, 0, 0],
             [3000, 200, -2700, -700, 4010, 170, 2650, -900, 2800]]
    cases = []
    for matrix in bases:
        for normal in [[0, 4096, 0], [0, -4096, 0], [4096, 0, 0], [0, 0, 4096], [0, 0, 0], [1, -4096, 1]]:
            for cache in [normal, [0, 0, 0], [normal[0] + 16, normal[1], normal[2]], [normal[0] + 17, normal[1], normal[2]]]:
                for delta in [0, 27, -2048]:
                    for yaw in [-32768, -1, 0, 27, 1024, 2048, 32767]:
                        cases.append(dict(matrix=matrix, normal=normal, cache=cache, delta=delta,
                                          angles=[rng.randint(-32768, 32767), yaw, rng.randint(-32768, 32767)], repeats=3))
    inputs = [' '.join(map(str, [*case['matrix'], *case['normal'], *case['cache'], case['delta'], *case['angles'], case['repeats']])) for case in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-start-orientation-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-O0', '-fwrapv', '-fno-pie', '-no-pie',
                        '-I', str(root/'src'), '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                        input=('\n'.join(inputs)+'\n').encode()).decode().splitlines()
    for case, line in zip(cases, lines, strict=True):
        case['expected'] = json.loads(line)
    (root/'modern/OpenSpidey.Tests/Fixtures/player-start-orientation.json').write_text(json.dumps(cases, separators=(',', ':'))+'\n')
    selections = [(0, [0, 4096, 0], [0, 0, 0], 0),
                  (0, [0, 4096, 0], [0, 0, 0], 1024),
                  (0, [0, 4096, 0], [0, 4096, 0], 27),
                  (5, [0, 4096, 0], [17, 4096, 0], 27),
                  (2, [0, -4096, 0], [0, 0, 0], 0),
                  (3, [0, 4096, 0], [0, 4096, 0], 1024),
                  (1, [0, 0, 0], [0, 0, 0], 27),
                  (0, [0, 4096, 0], [16, 4096, 0], 32767),
                  (0, [0, 4096, 0], [17, 4096, 0], -1)]
    native = ['// Generated from the untouched original PC executable by reference_player_start_orientation_vectors.py.',
              'namespace OpenSpidey.Game.World;',
              'internal static partial class PlayerStartOrientationImportChecks', '{',
              '    private static readonly StartCase[] SourceCases =', '    [']
    numbers = lambda values: ', '.join(map(str, values))
    for basis, normal, cache, yaw in selections:
        case = next(case for case in cases if case['matrix'] == bases[basis] and case['normal'] == normal and
                    case['cache'] == cache and case['delta'] == 0 and case['angles'][1] == yaw)
        native.append(f'        new([{numbers(case["matrix"])}], new({numbers(normal)}), new({numbers(cache)}), {yaw}, ' +
                      ('true' if cache == [0, 0, 0] else 'false') + ',')
        native.append('            [' + ', '.join('[' + numbers(row) + ']' for row in case['expected']) + ']),')
    native += ['    ];', '}', '']
    (root/'modern/OpenSpidey.Game/Scripts/World/PlayerStartOrientationImportChecks.Fixtures.cs').write_text('\n'.join(native))
    print(f'Captured {len(cases)} untouched PC SetStartOrientation sequences / {len(cases)*3} visits, including repeated yaw, original normal cache16/17 threshold, twist and degenerate fallback.')


if __name__ == '__main__':
    main()
