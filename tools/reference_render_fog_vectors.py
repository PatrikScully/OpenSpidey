"""Compile actual fog parameter, render-transition and reciprocal-alpha source blocks."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    init=(root/'src/engine/rendering/m3dinit.cpp').read_text();render=(root/'src/engine/rendering/scene_renderer.cpp').read_text();gfx=(root/'src/engine/rendering/PCGfx.cpp').read_text()
    harness=r'''
#include <iostream>
#include <iomanip>
#include <cstdint>
using i32=int32_t;using u32=uint32_t;using u8=uint8_t;using f32=float;
void print_if_false(bool condition,const char*){if(!condition)throw 1;}
i32 steps=0,nearValue=8048,farValue=6000,farRate=0,nearRate=0,shiftValue=0,finalShift=0,nearCopy=0,farCopy=0,white=0;
i32 *gFogTransitionSteps=&steps,*gFogNear=&nearValue,*gFogFar=&farValue,*gFogFarRate=&farRate,*gFogNearRate=&nearRate,*gFogRangeShift=&shiftValue,*gFogRangeShiftFinal=&finalShift,*gFogNearCopy=&nearCopy,*gFogFarCopy=&farCopy,*gFogColorIsWhite=&white;
u32 M3d_FadeColour=0xFFFFFF;
i32 *gM3dFadeCount=&steps,*gM3dFadeDist=&farValue,*gM3dFadeNear=&nearValue,*gM3dFadeStep=&farRate,*gM3dFadeNearStep=&nearRate,*gM3dFogFlag=&white;
u32 *gM3dFadeColour=&M3d_FadeColour;
f32 gFlFoggingParamOne,gFlFoggingParamTwo;u32 gU32FoggingParamThree;int G_BFOGGING_RELATED;
f32 projectionNear,projectionFar,projectionScale;f32 *gM3dProjNear=&projectionNear,*gM3dProjFar=&projectionFar,*gM3dProjScale=&projectionScale;
'''
    harness+=controller_method(gfx,'void PCGfx_SetFogParams(')+'\n'
    harness+=controller_method(init,'void M3dInit_SetFoggingParams(long Dummy, long Min, u32 Range)').replace('long Dummy','i32 Dummy').replace('long Min','i32 Min')+'\n'
    harness+='void advance(){'+controller_method(render,'if (*gM3dFadeCount > 0)')+'\n'
    setup=controller_method(render,'void M3d_RenderSetup(SCamera *pCam, SViewport *pView, u32 *a3)')
    clamp=controller_method(setup,'if (*gM3dFogFlag != 0)')
    harness+='i32 result=nearValue;'+clamp+'\n}\n'
    harness+=r'''
int alpha(int a,float depth){
 if(!white || depth>=1.0f/farValue || depth<=0)return a;
 float n=1.0f/nearValue,s=255.0f/(1.0f/farValue-n);int remap=0;
 if(depth>n)remap=(int)((depth-n)*s);
 u32 c=(u32)a<<24,blended=(u32)remap*(c>>8);u32 output=blended^(0xFFFFFFu&(c^blended));return output>>24;
}
int main(){int initialStart,initialEnd,duration,minimum;u32 range,colour;
 while(std::cin>>initialStart>>initialEnd>>duration>>minimum>>range>>colour){
  farValue=initialStart;nearValue=initialEnd;M3d_FadeColour=colour;farRate=nearRate=0;
  M3dInit_SetFoggingParams(duration,minimum,range);std::cout<<'[';
  for(int frame=0;frame<20;frame++){
   if(frame)std::cout<<',';advance();
   std::cout<<'['<<farValue<<','<<nearValue<<','<<steps<<','<<farRate<<','<<nearRate<<','<<shiftValue<<','<<farCopy<<','<<nearCopy<<','<<white<<','<<std::setprecision(9)<<gFlFoggingParamOne<<','<<gFlFoggingParamTwo<<','<<gU32FoggingParamThree;
   for(int depth:{100,6000,7000,8000,10000,12000,12088,20000})std::cout<<','<<alpha(255,1.0f/depth);
   std::cout<<']';
  }
  std::cout<<"]\n";
 }
}
'''
    rng=random.Random(0x4E1203)
    cases=[dict(start=6000,end=8048,duration=10,minimum=8000,range=4096,colour=0x02FFFFFF)]
    for _ in range(95):
        start=rng.randint(0,15000);end=start+rng.choice([1,4,2048,4096,8192])
        cases.append(dict(start=start,end=end,duration=rng.choice([0,1,3,10,25]),minimum=rng.randint(0,65535),range=rng.choice([1,4,2048,4096,8192,32768]),colour=rng.choice([0xFFFFFF,0x02FFFFFF,0x80FFFFFF,0x80123456,0xAB112233,0x0,0x466973])))
    lines=[' '.join(map(str,[c['start'],c['end'],c['duration'],c['minimum'],c['range'],c['colour']])) for c in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-render-fog-source-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(harness)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        results=subprocess.check_output([str(binary)],input='\n'.join(lines).encode()).decode().splitlines()
    for case,result in zip(cases,results,strict=True):case['expected']=json.loads(result)
    (root/'modern/OpenSpidey.Tests/Fixtures/render-fog.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} actual source fog cases /{len(cases)*20} render updates.')
if __name__=='__main__':main()
