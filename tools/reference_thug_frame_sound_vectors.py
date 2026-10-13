"""Compile original CThug::PlaySounds to recover exact frame audio/ring events."""
import json
from pathlib import Path
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / "src/game/actors/thug.cpp").read_text()
    harness = r'''
#include <iostream>
#include <vector>
struct CVector { int vx=0,vy=0,vz=0; };
std::vector<int> events;
int randomValue=0;
int Rnd(int bound) { events.insert(events.end(), {1,bound}); return randomValue; }
int SFX_PlayPos(unsigned sound,CVector* position,int pitchOffset) {
    events.insert(events.end(), {2,int(sound),position->vx,position->vy,position->vz,pitchOffset});return 0;
}
struct CThug {
    int mType=0;unsigned short mAnim=0,mFrame=0;CVector mPos;
    void PlaySounds();
    void MakeSpriteRing(CVector* offset) { events.insert(events.end(), {3,offset->vx,offset->vy,offset->vz}); }
};
'''
    harness += controller_method(source, "void CThug::PlaySounds(void)")
    harness += r'''
int main() {
    int type,animation,frame,random,updates;
    while(std::cin>>type>>animation>>frame>>random>>updates) {
        CThug actor;actor.mType=type;actor.mAnim=animation;actor.mFrame=frame;actor.mPos={-123456,987654,-56789};
        randomValue=random;events.clear();
        for(int i=0;i<updates;i++)actor.PlaySounds();
        std::cout<<"[";
        for(size_t i=0;i<events.size();i++){if(i)std::cout<<",";std::cout<<events[i];}
        std::cout<<"]\n";
    }
}
'''
    # Both model families, exact trigger boundaries, non-thug fallback, skipped
    # frames, random range and repeated frame updates (source has no cue cursor).
    triggers = {1: [13,27],28:[4,14],8:[5],14:[15],26:[23,28],5:[1,21],24:[1,12],7:[3],19:[15],0:[0],40:[15]}
    cases = []
    for actor_type in [304,312,0,305]:
        for animation,frames in triggers.items():
            for frame in sorted({f+d for f in frames for d in [-1,0,1] if f+d>=0}):
                for random in range(4):
                    for updates in [1,2]:
                        cases.append([actor_type,animation,frame,random,updates])
    with tempfile.TemporaryDirectory(prefix="thug-frame-sounds-") as temporary:
        directory = Path(temporary)
        (directory / "reference.cpp").write_text(harness)
        subprocess.run(["g++","-std=c++17","-O0","-fwrapv",str(directory/"reference.cpp"),"-o",str(directory/"reference")],check=True)
        result = subprocess.run([str(directory/"reference")],input="\n".join(" ".join(map(str,row)) for row in cases),capture_output=True,text=True,check=True)
    rows = [{"input":inputs,"events":json.loads(output)} for inputs,output in zip(cases,result.stdout.splitlines(),strict=True)]
    destination = root / "modern/OpenSpidey.Tests/Fixtures/thug-frame-sounds.json"
    destination.write_text(json.dumps(rows,separators=(",", ":"))+"\n")
    print(f"Compiled CThug::PlaySounds: {len(rows)} source comparisons written to {destination}")


if __name__ == "__main__":
    main()
