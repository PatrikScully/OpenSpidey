"""Compile original CreateFists bit selection (all byte values) with independent initial masks."""
import json
from pathlib import Path
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    source=(root/'src/game/player/spidey.cpp').read_text()
    harness=r'''
#include <iostream>
#include <cstdint>
using u8=uint8_t;using u32=uint32_t;
struct CPlayer {u32 field_194;void CreateFists(u8);};
'''+controller_method(source,'void CPlayer::CreateFists(u8 a2)')+r'''
int main(){u32 mask;int variant;while(std::cin>>mask>>variant){CPlayer player;player.field_194=mask;player.CreateFists((u8)variant);std::cout<<player.field_194<<'\n';}}
'''
    cases=[dict(mask=mask,variant=variant) for mask in [0,0x840,0xFFFFFFFF,0xA5A5A5A5,0x12345678] for variant in range(256)]
    with tempfile.TemporaryDirectory(prefix='openspidey-player-hands-source-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(harness)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        results=subprocess.check_output([str(binary)],input='\n'.join(f"{case['mask']} {case['variant']}" for case in cases).encode()).decode().splitlines()
    for case,result in zip(cases,results,strict=True):case['expected']=int(result)
    (root/'modern/OpenSpidey.Tests/Fixtures/player-hand-parts.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} actual source player hand mask cases.')

if __name__=='__main__':main()
