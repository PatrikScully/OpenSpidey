"""Compile the recovered PC pitch-to-integer-frequency operation across source sample clocks."""
import json
from pathlib import Path
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    source=(root/'src/engine/audio/sound_channels.cpp').read_text()
    code=r'''
#define SPIDEY_STANDALONE 1
#include <iostream>
using i32=int;using f32=float;
struct PlatSoundVoice{int frequency;};
struct Holder{void*pDSB;int mFrequency;};Holder gDxSoundHolder[32];
void Plat_SndSetFrequency(PlatSoundVoice*v,int frequency){v->frequency=frequency;}
'''
    code+=controller_method(source,'void DXSOUND_SetPitch(i32 channelIndex, i32 pitchScale)')
    code+=r'''
int main(){int rate,pitch;while(std::cin>>rate>>pitch){PlatSoundVoice voice{};gDxSoundHolder[1]={&voice,rate};DXSOUND_SetPitch(1,pitch);std::cout<<voice.frequency<<'\n';}}
'''
    cases=[dict(rate=rate,pitch=pitch) for rate in [99,11025,22050,44100,48000,96000] for pitch in range(256)]
    with tempfile.TemporaryDirectory(prefix='openspidey-frequency-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0',str(cpp),'-o',str(binary)],check=True)
        results=subprocess.check_output([str(binary)],input='\n'.join(f"{c['rate']} {c['pitch']}" for c in cases).encode()).decode().splitlines()
    for case,result in zip(cases,results,strict=True):case['frequency']=int(result)
    (root/'modern/OpenSpidey.Tests/Fixtures/sound-frequency.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} source integer-frequency vectors.')


if __name__=='__main__':main()
