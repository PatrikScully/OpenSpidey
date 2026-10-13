"""Compile recovered fixed-quaternion camera transitions and view damping."""
import json
import math
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method
from reference_quaternion_vectors import quaternion_harness


def main():
    root=Path(__file__).resolve().parent.parent
    source=(root/'src/game/cinematics/camera.cpp').read_text()
    harness=quaternion_harness(root)+r'''
using u16=uint16_t;
enum {CAMERAMODE_FAR=5};
struct CVector {
 int vx=0,vy=0,vz=0;
 CVector operator-(const CVector& b) const {return {vx-b.vx,vy-b.vy,vz-b.vz};}
 CVector operator/(int n) const {return {vx/n,vy/n,vz/n};}
 CVector operator*(int n) const {return {vx*n,vy*n,vz*n};}
 CVector& operator+=(const CVector& b) {vx+=b.vx;vy+=b.vy;vz+=b.vz;return *this;}
};
struct CCamera {
 CVector mPos,field_24C,field_2B0;
 CQuat field_2D4,field_2C4,field_1F4,field_1E4,field_204;
 int mCameraMode=3,field_2AC=0,field_2BC=0,field_2C0=0,mElapsedTicks=2;
 void SetFixedPosAnglesMode(CVector*,CQuat*,u16);
 void CM_FixedPosAngles();
};
'''
    harness+=controller_method(source,'void CCamera::SetFixedPosAnglesMode(\n')+'\n'
    harness+=controller_method(source,'void CCamera::CM_FixedPosAngles(void)')+'\n'
    harness+=r'''
void emit(CQuat q){std::cout<<q.x<<','<<q.y<<','<<q.z<<','<<q.w;}
int main() {
 for(int i=0;i<4096;i++){word_610C48[2*i]=sin(i*0.001536096911877394)*4096.0;word_610C48[2*i+1]=cos(i*0.001536096911877394)*4096.0;}
 CQuat current,target;int duration,ticks;
 while(std::cin>>current.x>>current.y>>current.z>>current.w>>target.x>>target.y>>target.z>>target.w>>duration>>ticks) {
  CCamera camera;CVector position;camera.field_1E4=current;
  camera.SetFixedPosAnglesMode(&position,&target,duration);camera.mElapsedTicks=ticks;
  std::cout<<'[';
  for(int frame=0;frame<20;frame++) {
   camera.CM_FixedPosAngles();camera.field_204=camera.field_1E4;
   Quat_Slerp(camera.field_204,camera.field_1F4,1023,camera.field_1E4);
   if(frame)std::cout<<',';std::cout<<'[';emit(camera.field_1E4);std::cout<<',';emit(camera.field_1F4);std::cout<<','<<camera.field_2BC<<']';
  }
  std::cout<<"]\n";
 }
}
'''
    rng=random.Random(0x418800)
    def quaternion():
        values=[rng.uniform(-1,1) for _ in range(4)];length=math.sqrt(sum(v*v for v in values))
        return [int(v*4096/length) for v in values]
    pairs=[([0,0,0,4096],[2896,0,0,2896]),([0,0,0,4096],[0,-4096,0,0]),
           ([0,0,0,4096],[0,0,0,-4096])]+[(quaternion(),quaternion()) for _ in range(12)]
    cases=[{'current':a,'target':b,'duration':duration,'ticks':ticks}
           for a,b in pairs for duration in [0,1,3,16,32] for ticks in [1,2,3]]
    lines=[' '.join(map(str,c['current']+c['target']+[c['duration'],c['ticks']])) for c in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-camera-rotation-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference'
        cpp.write_text(harness)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        results=subprocess.check_output([str(binary)],input='\n'.join(lines).encode()).decode().splitlines()
    for case,result in zip(cases,results,strict=True):case['expected']=json.loads(result)
    (root/'modern/OpenSpidey.Tests/Fixtures/camera-rotations.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} camera rotation sequences / {len(cases)*20} updates.')


if __name__=='__main__':main()
