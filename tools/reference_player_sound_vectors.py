"""Compile recovered player frame sounds with deterministic sound/random callbacks."""
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
using i32=int32_t;using u32=uint32_t;
u32 last=0;u32* gLastPlayedSfx=&last;int randomCursor=0,played=-1;
int Rnd(int maximum){return randomCursor++%maximum;}
void SFX_Play(int sound,int,int){played=sound;}
struct CPlayer {int* field_350=nullptr;int mFrame=0,mAnim=0,field_34C=0;int ProcessSFXArray();};
'''
    harness+=controller_method(source,'i32 CPlayer::ProcessSFXArray(void)')+'\n'
    harness+=r'''
int main(){int selected,animation,alias,count;
while(std::cin>>selected>>animation>>alias>>count){
 int frames[5]={-1,-1,-1,-1,-1};
 switch(selected){case 21:frames[0]=1;frames[1]=11;break;case 59:frames[0]=4;break;
 case 52:frames[0]=7;frames[1]=15;break;case 50:case 51:frames[0]=6;break;
 case 60:frames[0]=6;frames[1]=15;break;case 63:frames[0]=7;frames[1]=19;frames[2]=26;break;
 case 192:frames[0]=2;frames[1]=14;break;case 198:frames[0]=1;frames[1]=21;break;}
 int length=0;while(frames[length]!=-1)length++;
 CPlayer player;player.field_350=frames;player.mAnim=animation;player.field_34C=alias;
 last=0;randomCursor=0;std::cout<<'[';
 for(int step=0;step<count;step++){
  std::cin>>player.mFrame;played=-1;player.ProcessSFXArray();
  if(step)std::cout<<',';std::cout<<'['<<played<<','<<(player.field_350 ? player.field_350-frames : length)<<','
   <<(player.field_350!=nullptr)<<','<<last<<']';
 }
 std::cout<<"]\n";
}}
'''
    cases=[]
    frames=[0,1,4,6,7,11,15,19,26,100,100,0,100,100,100,100,100,100,100,100]
    for selected in [21,59,52,50,51,60,63,192,198]:
        for animation in [selected,21,0]:
            for alias in [0,1]:cases.append({'selected':selected,'animation':animation,'alias':alias,'frames':frames})
    lines=[' '.join(map(str,[c['selected'],c['animation'],c['alias'],len(c['frames'])]+c['frames'])) for c in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-player-sound-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(harness)
        subprocess.run(['g++','-m32','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        results=subprocess.check_output([str(binary)],input='\n'.join(lines).encode()).decode().splitlines()
    for case,result in zip(cases,results,strict=True):case['expected']=json.loads(result)
    (root/'modern/OpenSpidey.Tests/Fixtures/player-sounds.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} player sound sequences / {len(cases)*len(frames)} updates.')


if __name__=='__main__':main()
