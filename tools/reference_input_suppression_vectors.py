"""Compile recovered input-lock setter and CPlayer::AI countdown/pad-clearing block."""
import json
from pathlib import Path
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root/'src/game/player/spidey.cpp').read_text()
    setter = controller_method(source,'void CPlayer::SetIgnoreInputTimer(int a2)')
    ai = controller_method(source,'void CPlayer::AI(void)')
    block = ai[ai.index('\tif (this->field_E18 != 0 && this->mSynthesizedInputActive == 0)'):ai.index('\t// Per-tick callback.')]
    harness = r'''
#include <iostream>
#include <cstdint>
using i32=int32_t;using u8=uint8_t;
struct CPlayer {
 int field_E18=0,mAnimSpeed=65536,mElapsedTicks=2,exits=0;
 int16_t field_E12=0;bool field_8EA=false,mSynthesizedInputActive=false;
 u8* field_E0C;
 void ExitLookaroundMode(){field_8EA=false;exits++;}
 void SetIgnoreInputTimer(int);
 void Advance();
};
'''
    harness += setter+'\nvoid CPlayer::Advance(){\n'+block+'}\n'
    harness += r'''
int main(){int duration,ticks,speed,scripted,look;
while(std::cin>>duration>>ticks>>speed>>scripted>>look){
 u8 pad[320]={};CPlayer player;player.field_E0C=pad;player.mAnimSpeed=speed;player.field_8EA=look;
 player.SetIgnoreInputTimer(duration);player.mElapsedTicks=ticks;std::cout<<'[';
 for(int frame=0;frame<12;frame++){
  for(int slot=0;slot<20;slot++){pad[slot*16]=1;pad[slot*16+1]=1;pad[slot*16+2]=7;}
  player.mSynthesizedInputActive=scripted && frame<3;
  player.Advance();if(frame)std::cout<<',';
  std::cout<<'['<<player.field_E18<<','<<player.field_E12<<','<<player.mAnimSpeed<<','<<player.exits<<','<<player.field_8EA;
  for(int slot=0;slot<20;slot++)for(int byte=0;byte<3;byte++)std::cout<<','<<int(pad[slot*16+byte]);
  std::cout<<']';
 }
 std::cout<<"]\n";
}}
'''
    cases = [{'duration':duration,'ticks':ticks,'speed':speed,'scripted':bool(scripted),'look':bool(look)}
             for duration in [0,1,2,3,15,65535,-1] for ticks in [1,2,3]
             for speed in [0,65536,12345,-40000] for scripted in [0,1] for look in [0,1]]
    lines = [' '.join(map(str,[c['duration'],c['ticks'],c['speed'],int(c['scripted']),int(c['look'])])) for c in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-input-lock-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(harness)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        results=subprocess.check_output([str(binary)],input='\n'.join(lines).encode()).decode().splitlines()
    for case,result in zip(cases,results,strict=True):case['expected']=json.loads(result)
    (root/'modern/OpenSpidey.Tests/Fixtures/input-suppression.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} input suppression sequences / {len(cases)*12} updates.')


if __name__=='__main__':main()
