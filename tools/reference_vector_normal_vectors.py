"""Capture original PC VectorNormal directly, including its x87 rounding boundaries."""
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
struct Vector { int x,y,z,pad; };
int main(int argc,char**argv){
 assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 Vector input{},output{};
 while(std::cin>>input.x>>input.y>>input.z){
  reinterpret_cast<void(*)(Vector*,Vector*)>(0x470430)(&input,&output);
  std::cout<<'['<<output.x<<','<<output.y<<','<<output.z<<"]\n";
 }
}
'''
    rng = random.Random(0x470430)
    cases = [[0, 0, 0]]
    for value in [-2147483648, -2147483647, -524288, -1, 1, 524287, 2147483647]:
        for axis in range(3):
            vector = [0, 0, 0]
            vector[axis] = value
            cases.append(vector)
    for limit in [500, 4096, 524288, 2147483647]:
        cases.extend([[rng.randint(-limit, limit) for _ in range(3)] for _ in range(400)])
    with tempfile.TemporaryDirectory(prefix='openspidey-vector-normal-') as folder:
        source = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        source.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(source), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, vector)) for vector in cases)+'\n').encode())
    fixtures = [dict(input=vector, expected=json.loads(row))
                for vector, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/vector-normal.json').write_text(
        json.dumps(fixtures, separators=(',', ':'))+'\n')
    print(f'Captured {len(fixtures)} actual PC VectorNormal cases without a normalization stub.')


if __name__ == '__main__':
    main()
