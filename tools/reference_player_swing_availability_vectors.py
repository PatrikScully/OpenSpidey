"""Capture the complete actual PC CheckSwingWebAvailability leaf and every partial/shared geometry field."""
import argparse
import json
from pathlib import Path
import random
import shutil
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    code = r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);long long v[11];while(std::cin>>v[0]){for(int i=1;i<11;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={},line[0xA4]={};unsigned face[4]={};at<short>(line,0x7A)=v[0];at<int>(line,0x40)=v[1];face[3]=v[2];at<void*>(line,0x80)=face;at<unsigned>(player,0xE1C)=v[3];player[0x8E9]=v[4];at<CVector>(player,8)=CVector(v[5],v[6],v[7]);at<CVector>(line,0x6C)=CVector(v[8],v[9],v[10]);at<CVector>(player,0xDC0)=CVector(11,-12,13);at<CVector>(player,0xDA0)=CVector(21,-22,23);at<CVector>(player,0xD64)=CVector(31,-32,33);at<CVector>(player,0xD70)=CVector(41,-42,43);player[0xD60]=77;
unsigned char result=reinterpret_cast<unsigned char(__attribute__((thiscall)) *)(void*,void*)>(0x4C31D0)(player,line);std::cout<<'['<<unsigned(result);for(unsigned o:{0xDC0u,0xDA0u,0xD64u,0xD70u}){auto*p=&at<CVector>(player,o);std::cout<<','<<p->vx<<','<<p->vy<<','<<p->vz;}std::cout<<','<<unsigned(player[0xD60])<<"]\n";}}
'''
    cases = []
    for normal in [-32768, -2601, -2600, 0, 3400, 3401]:
        for distance in [512, 513, 3072, 3073, 4095, 4096]:
            for action in [1, 2, 4, 0x40000, 0x8000]:
                cases.append([normal, distance, 0, action, 0, 123456, -234567, 345678, 123456 + 1800 * 4096, -234567 - 700 * 4096, 345678 + 900 * 4096])
    rng = random.Random(0x4C31D0)
    for _ in range(500):
        origin = [rng.randint(-10000000, 10000000) for _ in range(3)]
        target = [origin[0] + rng.randint(400, 2200) * 4096, origin[1] + rng.randint(-1800, 1800) * 4096, origin[2] + rng.randint(400, 2200) * 4096]
        cases.append([rng.choice([-32768, -2601, -2600, 0, 3400, 3401]), rng.choice([512, 513, 1800, 3072, 3073, 4095, 4096]), rng.choice([0, 0, 0x40000]), rng.choice([1, 2, 4, 0x40000, 0x8000]), rng.randrange(2), *origin, *target])
    with tempfile.TemporaryDirectory(prefix='openspidey-swing-availability-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        requests = '\n'.join(' '.join(map(str, case)) for case in cases) + '\n'
        try: lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=requests.encode()).decode().splitlines()
        except subprocess.CalledProcessError:
            shutil.copy2(binary, '/tmp/openspidey-swing-availability-reference'); shutil.copy2(cpp, '/tmp/openspidey-swing-availability-reference.cpp')
            Path('/tmp/openspidey-swing-availability-cases.txt').write_text(requests); raise
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-swing-availability.json').write_text(json.dumps([dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} full actual PC swing-availability cases, including partial DC0 writes and both near/far normal/centre branches.')


if __name__ == '__main__': main()
