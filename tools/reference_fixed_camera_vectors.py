"""Compile the recovered fixed-position camera setter and movement block."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/cinematics/camera.cpp').read_text()
    setter = controller_method(source, 'void CCamera::SetFixedPosMode(CVector &a2, u16 a3)')
    method = controller_method(source, 'void CCamera::CM_FixedPos(void)')
    movement = method[method.index('\tv2 = this->field_2BC;'):method.index('\tthis->field_258 =')]
    harness = r'''
#include <iostream>
#include <cstdint>
using i32=int32_t;using u16=uint16_t;
enum {CAMERAMODE_START=4};
struct CVector {
 i32 vx=0,vy=0,vz=0;
 CVector operator-(const CVector& b) const {return {vx-b.vx,vy-b.vy,vz-b.vz};}
 CVector operator/(i32 n) const {return {vx/n,vy/n,vz/n};}
 CVector operator*(i32 n) const {return {vx*n,vy*n,vz*n};}
 CVector& operator+=(const CVector& b) {vx+=b.vx;vy+=b.vy;vz+=b.vz;return *this;}
};
struct CCamera {
 CVector mPos,field_24C,field_2B0;
 int field_2AC=0,mCameraMode=3,field_2BC=0,mElapsedTicks=2;
 void SetFixedPosMode(CVector&,u16);
 void Advance();
};
'''
    harness += setter + '\nvoid CCamera::Advance() {int v2;\n' + movement + '}\n'
    harness += r'''
int main() {
 CCamera camera;CVector target;int duration,ticks;
 while(std::cin>>camera.mPos.vx>>camera.mPos.vy>>camera.mPos.vz
       >>target.vx>>target.vy>>target.vz>>duration>>ticks) {
  camera.SetFixedPosMode(target,duration);camera.mElapsedTicks=ticks;
  std::cout<<'[';
  for(int frame=0;frame<20;frame++) {
   camera.Advance();if(frame)std::cout<<',';
   std::cout<<'['<<camera.mPos.vx<<','<<camera.mPos.vy<<','<<camera.mPos.vz<<','<<camera.field_2BC<<']';
  }
  std::cout<<"]\n";
 }
}
'''
    rng = random.Random(0x416370)
    cases = []
    for duration in [0,1,3,16,64,128,256,65535]:
        for ticks in [1,2,3,7]:
            for _ in range(5):
                cases.append({'current':[rng.randint(-2**31,2**31-1) for _ in range(3)],
                              'target':[rng.randint(-2**31,2**31-1) for _ in range(3)],
                              'duration':duration,'ticks':ticks})
    lines = [' '.join(map(str,c['current']+c['target']+[c['duration'],c['ticks']])) for c in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-fixed-camera-reference-') as directory:
        cpp = Path(directory)/'reference.cpp'; binary = Path(directory)/'reference'
        cpp.write_text(harness)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        results = subprocess.check_output([str(binary)],input='\n'.join(lines).encode()).decode().splitlines()
    for case,result in zip(cases,results,strict=True):
        case['expected'] = json.loads(result)
    (root/'modern/OpenSpidey.Tests/Fixtures/fixed-camera.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} fixed-camera sequences / {len(cases)*20} updates.')


if __name__ == '__main__':
    main()
