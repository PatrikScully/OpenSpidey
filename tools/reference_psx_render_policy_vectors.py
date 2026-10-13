"""Compile the actual M3dInit_ParsePSX face preparation block (no rewritten oracle)."""
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/engine/rendering/m3dinit.cpp').read_text()
    start = source.index('u8 *pFace = pVert + 8 * numNorms;')
    end = source.index('i32 formatFlags = isLegacyFormat ? 1 : 0;', start)
    block = source[start:end]
    code = r'''
#include <cstdint>
#include <cstring>
#include <iostream>
#include <vector>
using u8=uint8_t; using u16=uint16_t; using u32=uint32_t; using i16=int16_t; using i32=int32_t;
struct Part { u16 Flags; };
int main() { unsigned flags; int numFaces;
while (std::cin >> flags >> numFaces) {
    Part part{u16(flags)}; Part* pPart=&part; bool fastFlag=true; int numNorms=0;
    std::vector<u32> packets; std::vector<size_t> offsets;
    for (int f=0;f<numFaces;f++) {
        u32 raw,color,collision; unsigned a,b,c,d;
        std::cin>>raw>>color>>collision>>a>>b>>c>>d;
        offsets.push_back(packets.size()); size_t base=packets.size();
        packets.resize(base+(raw>>18),0);
        packets[base]=raw; packets[base+1]=a|(b<<8)|(c<<16)|(d<<24);
        packets[base+2]=color; packets[base+3]=collision;
    }
    if (packets.empty()) packets.resize(1);
    u8* pVert=reinterpret_cast<u8*>(packets.data());
'''+block+r'''
    std::cout<<"{\"modelFlags\":"<<part.Flags<<",\"aggregate\":"<<faceFlagsOr<<",\"lodMask\":"<<lodMask<<",\"fast\":"<<(fastFlag?"true":"false")<<",\"faces\":[";
    for(int f=0;f<numFaces;f++) { if(f)std::cout<<','; u32* p=packets.data()+offsets[f]; u8* indices=reinterpret_cast<u8*>(p+1);
        std::cout<<"{\"flags\":"<<p[0]<<",\"color\":"<<p[2]<<",\"collision\":"<<p[3]<<",\"indices\":["<<unsigned(indices[0])<<','<<unsigned(indices[1])<<','<<unsigned(indices[2])<<','<<unsigned(indices[3])<<"]}";
    }
    std::cout<<"],\"visibleIndices\":[";bool first=true;
    for(int f=0;f<numFaces;f++) if(packets[offsets[f]]&0xC0) { if(!first)std::cout<<',';first=false;std::cout<<f; }
    std::cout<<"]}\n";
}}
'''
    rng = random.Random(0x439467)
    def face(raw):
        return dict(flags=raw,color=rng.getrandbits(32),collision=rng.getrandbits(32),indices=rng.sample(range(64),4))
    cases = [dict(flags=0,faces=[face(0x100000 | low)]) for low in range(256)]
    for high in [0x100000,0x180000,0x200000,0x2100000,0x8100000,0x80100000]:
        for low in [0,0x10,0x40,0x80,0x90,0xC0,0xFF]:
            for middle in [0,0x800,0x1000,0x1800]:
                cases.append(dict(flags=rng.choice([0,1,4,0x20,0x40]),faces=[face(high|middle|low)]))
    for flags in [0,1,4,5,0x10,0x20,0x40,0xFFFF]:
        cases.append(dict(flags=flags,faces=[]))
        cases.append(dict(flags=flags,faces=[face(0x100880),face(0x100890)]))
        cases.append(dict(flags=flags,faces=[face(0x100880),face(0x100040),face(0x100080),face(0x100010),face(0x1000C0)]))
    # Original L1A1 G invisible wall face, explicit regression independent of random coverage.
    cases.append(dict(flags=0,faces=[dict(flags=0x00100880,color=0xFF00FF00,collision=0,indices=[0,1,2,3])]))
    rows = []
    for case in cases:
        values = [case['flags'], len(case['faces'])]
        for f in case['faces']:
            values.extend([f['flags'], f['color'], f['collision'], *f['indices']])
        rows.append(' '.join(map(str, values)))
    with tempfile.TemporaryDirectory(prefix='openspidey-render-policy-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        output=subprocess.check_output([str(binary)],input=('\n'.join(rows)+'\n').encode()).decode().splitlines()
    target=root/'modern/OpenSpidey.Tests/Fixtures/psx-render-policy.json'
    target.write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,output,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} compiled-original face preparation cases, all 256 low-byte branches, high flags, empty/hidden/mixed models.')

if __name__=='__main__':main()
