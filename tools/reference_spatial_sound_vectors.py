"""Compile recovered camera-relative SFX attenuation with the actual distance and rotation helpers."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def harness(root):
    utils = (root/'src/core/utils.cpp').read_text()
    psx = (root/'src/engine/rendering/ps2funcs.cpp').read_text()
    result = r'''
#include "core/fixed_vector.h"
#include "core/distance.h"
#include <iostream>
#include <cmath>
struct VECTOR {i32 vx,vy,vz;long pad;};
struct Camera {CVector Position;};Camera G_MIKE_CAMERA[1];
i16 gCameraViewMatrix[3][3],G_ROT_MATRIX[3][3];VECTOR G_OP12_RESULT,G_GENERAL_LONG_VECTOR;
struct Heading {i16 sin,cos;};Heading G_RCOSSIN_TBL[4096];
void print_if_false(int,const char*){}
u32 Utils_CrapDist(const CVector&a,const CVector&b){return spidey::math::approximateDistance(a,b);}
void gte_SetRotMatrix(i16 matrix[3][3]){for(int i=0;i<3;i++)for(int j=0;j<3;j++)G_ROT_MATRIX[i][j]=matrix[i][j];}
void gte_ldlvl(VECTOR*v){G_OP12_RESULT.vx=v->vx;G_OP12_RESULT.vy=v->vy;G_OP12_RESULT.vz=v->vz;}
void gsub_46D9B0(VECTOR*v){v->vx=G_OP12_RESULT.vx;v->vy=G_OP12_RESULT.vy;v->vz=G_OP12_RESULT.vz;}
'''
    result += controller_method(psx,'EXPORT void  FixedXForm(')+'\n'
    result += 'void gte_rtir(){FixedXForm(G_ROT_MATRIX,&G_OP12_RESULT,&G_GENERAL_LONG_VECTOR);}\n'
    result += controller_method(psx,'int ratan2(int x, int y)')+'\n'
    result += controller_method(utils,'u32 Utils_CalculateSpatialAttenuation(')+'\n'
    result += 'void InitializeHeading(){for(int i=0;i<4096;i++){G_RCOSSIN_TBL[i].sin=sin(i*0.001536096911877394)*4096;G_RCOSSIN_TBL[i].cos=cos(i*0.001536096911877394)*4096;}}\n'
    return result


def main():
    root = Path(__file__).resolve().parent.parent
    code = harness(root)+r'''
int main(){InitializeHeading();CVector sound;int near,far;
while(std::cin>>sound.vx>>sound.vy>>sound.vz>>near>>far>>G_MIKE_CAMERA[0].Position.vx>>G_MIKE_CAMERA[0].Position.vy>>G_MIKE_CAMERA[0].Position.vz){
 for(int i=0;i<3;i++)for(int j=0;j<3;j++)std::cin>>gCameraViewMatrix[i][j];
 auto result=Utils_CalculateSpatialAttenuation(&sound,near,far);std::cout<<'['<<(result&4095)<<','<<((result>>16)&4095)<<"]\n";
}}
'''
    rng = random.Random(0x4e72b0)
    identity=[4096,0,0,0,4096,0,0,0,4096]
    cases=[]
    for near,far in [(0,100),(100,800),(3072,24576),(0,0)]:
        for position in [[0,0,0],[99<<12,0,0],[100<<12,0,0],[800<<12,0,0],[-200<<12,0,0],[0,200<<12,0]]:
            cases.append(dict(position=position,near=near,far=far,listener=[0,0,0],rotation=identity))
    for _ in range(300):
        cases.append(dict(position=[rng.randint(-20000<<12,20000<<12) for _ in range(3)],near=rng.randint(0,4000),far=rng.randint(8000,40000),listener=[rng.randint(-3000,3000) for _ in range(3)],rotation=[rng.randint(-4096,4096) for _ in range(9)]))
    lines=[' '.join(map(str,[*c['position'],c['near'],c['far'],*c['listener'],*c['rotation']])) for c in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-spatial-sound-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        results=subprocess.check_output([str(binary)],input='\n'.join(lines).encode()).decode().splitlines()
    for case,result in zip(cases,results,strict=True):case['expected']=json.loads(result)
    (root/'modern/OpenSpidey.Tests/Fixtures/spatial-sound.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} source stereo attenuation vectors.')


if __name__=='__main__':main()
