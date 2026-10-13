"""Compile recovered quaternion/matrix conversion and sine-table interpolation."""
import json
import math
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def quaternion_harness(root):
    source=(root/'src/core/quaternion.cpp').read_text()
    utils=(root/'src/core/utils.cpp').read_text()
    psx=(root/'src/engine/rendering/ps2funcs.cpp').read_text()
    harness=r'''
#include <iostream>
#include <cstdint>
#include <cmath>
using i32=int32_t;using i16=int16_t;using f32=float;
struct MATRIX {i16 m[3][3];int t[3];};
struct CQuat {i32 x=0,y=0,z=0,w=4096;};
int gQuatAxisNext[]={1,2,0};i16 word_610C48[8192];
const f32 FOUR_NINETY_SIX=4096.0f,TWO_FOURTY_EIGHT=2048.0f,PI=3.1415927f;
'''
    for text,signature in [(psx,'i32 M3dMaths_SquareRoot0(i32 i)'),(utils,'int Utils_ArcCos(int val)'),
                           (source,'void MToQ(MATRIX const & a1, CQuat& a2)'),(source,'void QToM(CQuat* q, MATRIX* m)'),
                           (source,'void Quat_Slerp (CQuat& a1, CQuat const & a2, int a3, CQuat& a4)')]:
        harness+=controller_method(text,signature)+'\n'
    return harness


def main():
    root=Path(__file__).resolve().parent.parent
    harness=quaternion_harness(root)
    harness+=r'''
int main() {
 for(int i=0;i<4096;i++){word_610C48[2*i]=sin(i*0.001536096911877394)*4096.0;word_610C48[2*i+1]=cos(i*0.001536096911877394)*4096.0;}
 CQuat start,target,result,round;int fraction;
 while(std::cin>>start.x>>start.y>>start.z>>start.w>>target.x>>target.y>>target.z>>target.w>>fraction) {
  MATRIX matrix;QToM(&start,&matrix);MToQ(matrix,round);Quat_Slerp(start,target,fraction,result);
  std::cout<<'['<<start.x<<','<<start.y<<','<<start.z<<','<<start.w<<','
   <<result.x<<','<<result.y<<','<<result.z<<','<<result.w;
  for(int i=0;i<3;i++)for(int j=0;j<3;j++)std::cout<<','<<matrix.m[i][j];
  std::cout<<','<<round.x<<','<<round.y<<','<<round.z<<','<<round.w<<"]\n";
 }
}
'''
    rng=random.Random(0x47CAC0)
    def quaternion():
        v=[rng.uniform(-1,1) for _ in range(4)]
        norm=math.sqrt(sum(x*x for x in v))
        return [int(x*4096/norm) for x in v]
    pairs=[([0,0,0,4096],[0,0,0,4096]),([0,0,0,4096],[0,0,0,-4096]),
           ([0,0,0,0],[0,0,0,4096]),([0,0,0,4096],[0,0,0,0]),
           ([2048,-2048,2048,-2048],[0,4096,0,0])]
    for axis in range(3):
        half=[0,0,0,0];half[axis]=4096
        pairs.append((half,[0,0,0,4096]))
    for _ in range(100):
        first=quaternion();pairs.append((first,quaternion()));pairs.append((first,[-x for x in first]))
    cases=[{'start':first,'target':second,'fraction':fraction}
           for first,second in pairs for fraction in [0,1,1023,2048,4096]]
    lines=[' '.join(map(str,c['start']+c['target']+[c['fraction']])) for c in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-quaternion-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference'
        cpp.write_text(harness)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        results=subprocess.check_output([str(binary)],input='\n'.join(lines).encode()).decode().splitlines()
    for case,result in zip(cases,results,strict=True):case['expected']=json.loads(result)
    (root/'modern/OpenSpidey.Tests/Fixtures/quaternions.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} recovered quaternion cases.')


if __name__=='__main__':main()
