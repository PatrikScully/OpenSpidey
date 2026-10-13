"""Compile actual CCamera::Shake and CameraAI shake block with original quaternion helpers."""
import json
import math
from pathlib import Path
import random
import re
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method
from reference_quaternion_vectors import quaternion_harness


def main():
    root=Path(__file__).resolve().parent.parent
    camera=(root/'src/game/cinematics/camera.cpp').read_text()
    quaternion=(root/'src/core/quaternion.cpp').read_text()
    harness=quaternion_harness(root)+r'''
using u8=uint8_t;using u16=uint16_t;using u32=uint32_t;
struct CVector {int vx,vy,vz;};
struct CSVector {i16 vx,vy,vz;CSVector(i16 x=0,i16 y=0,i16 z=0):vx(x),vy(y),vz(z){}};
struct CFriction {u8 vx,vy,vz;CFriction(u8 x=0,u8 y=0,u8 z=0):vx(x),vy(y),vz(z){}};
enum EShakeType {CAMERASHAKE_BIG,CAMERASHAKE_MEDIUM,CAMERASHAKE_SMALL,CAMERASHAKE_UNK};
void DoAssert(int condition,const char*) {if(!condition)throw 1;}
u32 G_TTIME;
struct CCamera {CSVector mShakeAmp,mShakeSpeed;CFriction mShakeDecay;CQuat field_214,field_1E4;
void Shake(CVector&,EShakeType);void Advance();};
'''
    for name in ['BigShakeAmp','MediumShakeAmp','SmallShakeAmp','UnkShakeAmp','LandShakeDecay','LandShakeSpeed']:
        harness+=re.search(r'EXPORT (?:CSVector|CFriction) '+name+r'\([^;]+;',camera).group(0).replace('EXPORT ','')+'\n'
    harness+=controller_method(camera,'void CCamera::Shake(CVector& pos, EShakeType ShakeMagnitude)')+'\n'
    for signature in ['CQuat operator*(const CQuat& a, const CQuat& b)','CQuat QFromXRot(i32 angle)','CQuat QFromYRot(i32 angle)','CQuat QFromZRot(i32 angle)']:
        harness+=controller_method(quaternion,signature)+'\n'
    block=controller_method(camera,'if (this->mShakeAmp.vx || this->mShakeAmp.vy || this->mShakeAmp.vz)')
    harness+='void CCamera::Advance(){this->field_214=this->field_1E4;\n'+block+'\n}\n'
    harness+=r'''
int main(){
 for(int i=0;i<4096;i++){word_610C48[2*i]=sin(i*0.001536096911877394)*4096.0;word_610C48[2*i+1]=cos(i*0.001536096911877394)*4096.0;}
 int magnitude,ax,ay,az,dx,dy,dz,sx,sy,sz,px,py,pz,step;u32 time;CQuat baseline;
 while(std::cin>>magnitude>>ax>>ay>>az>>dx>>dy>>dz>>sx>>sy>>sz>>px>>py>>pz>>time>>step>>baseline.x>>baseline.y>>baseline.z>>baseline.w){
  CCamera state;state.mShakeAmp=CSVector(ax,ay,az);state.mShakeDecay=CFriction(dx,dy,dz);state.mShakeSpeed=CSVector(sx,sy,sz);
  CVector position={px,py,pz};if(magnitude>=0)state.Shake(position,static_cast<EShakeType>(magnitude));
  std::cout<<'[';
  for(int frame=0;frame<24;frame++){
   if(frame)std::cout<<',';G_TTIME=time;state.field_1E4=baseline;state.Advance();
   std::cout<<'['<<state.mShakeAmp.vx<<','<<state.mShakeAmp.vy<<','<<state.mShakeAmp.vz<<','<<(int)state.mShakeDecay.vx<<','<<(int)state.mShakeDecay.vy<<','<<(int)state.mShakeDecay.vz<<','<<state.mShakeSpeed.vx<<','<<state.mShakeSpeed.vy<<','<<state.mShakeSpeed.vz<<','<<state.field_214.x<<','<<state.field_214.y<<','<<state.field_214.z<<','<<state.field_214.w<<']';
   time+=step;
  }
  std::cout<<"]\n";
 }
}
'''
    rng=random.Random(0x416880)
    def quat():
        values=[rng.uniform(-1,1) for _ in range(4)];length=math.sqrt(sum(v*v for v in values))
        return [int(v*4096/length) for v in values]
    bases=[[0,0,0,4096],[0,0,0,-4096],[2896,0,0,2896],[0,4096,0,0]]+[quat() for _ in range(8)]
    cases=[]
    for magnitude in range(4):
        for time in [0,1,2,4094,0xFFFFFE00,0xFFFFFFFF]:
            for baseline in bases:
                cases.append(dict(magnitude=magnitude,amplitude=[0,0,0],decay=[0,0,0],speed=[0,0,0],position=[rng.randint(-2147483648,2147483647) for _ in range(3)],time=time,step=rng.choice([1,2,17]),baseline=baseline))
    for index in range(128):
        cases.append(dict(magnitude=-1,amplitude=[rng.choice([-32768,-31,-3,-1,0,1,3,31,32767]) for _ in range(3)],decay=[rng.choice([0,1,3,255]) for _ in range(3)],speed=[rng.randint(-32768,32767) for _ in range(3)],position=[0,0,0],time=rng.getrandbits(32),step=rng.choice([1,2,65535]),baseline=quat()))
    lines=[' '.join(map(str,[c['magnitude']]+c['amplitude']+c['decay']+c['speed']+c['position']+[c['time'],c['step']]+c['baseline'])) for c in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-camera-shake-source-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(harness)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        results=subprocess.check_output([str(binary)],input='\n'.join(lines).encode()).decode().splitlines()
    for case,result in zip(cases,results,strict=True):case['expected']=json.loads(result)
    (root/'modern/OpenSpidey.Tests/Fixtures/camera-shake.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} actual source camera shake cases / {len(cases)*24} updates.')

if __name__=='__main__':main()
