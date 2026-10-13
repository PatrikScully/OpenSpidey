"""Compile recovered camera parameter setters and the scalar AI update block."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/cinematics/camera.cpp').read_text()
    harness = '#include <iostream>\n#include <cstdint>\nusing i16=int16_t;using u16=uint16_t;using i32=int32_t;\n'
    names = ['gCamXOffsetRelatedOne','gCamXOffsetRelatedTwo','gCamXOffsetRelatedThree','gCamXOffsetRelatedFour',
             'gCamYOffsetOne','gCamYOffsetTwo','gCamYOffsetThree','gCamYOffsetFour',
             'gCamZOffsetOne','gCamZOffsetTwo','gCamZOffsetThree','gCamZOffsetFour',
             'gCamXZRelatedTwo','gCamXZRelatedThree','gCamXZDistanceRelated','camXZDist',
             'gCamYDistanceRelatedTwo','gCamYDistanceRelatedThree','gCamYDistanceRelated','camYDist']
    harness += 'int '+','.join(n+'=0' for n in names)+'; int gWtfCam[35]={};\n'
    macros = {'G_WTF_CAM':'gWtfCam','G_CAM_X_OFFSET_ONE':names[0],'G_CAM_X_OFFSET_TWO':names[1],
              'G_CAM_X_OFFSET_THREE':names[2],'G_CAM_X_OFFSET_FOUR':names[3],
              'G_CAM_Y_OFFSET_ONE':names[4],'G_CAM_Y_OFFSET_TWO':names[5],'G_CAM_Y_OFFSET_THREE':names[6],'G_CAM_Y_OFFSET_FOUR':names[7],
              'G_CAM_Z_OFFSET_ONE':names[8],'G_CAM_Z_OFFSET_TWO':names[9],'G_CAM_Z_OFFSET_THREE':names[10],'G_CAM_Z_OFFSET_FOUR':names[11],
              'G_CAM_XZ_RELATED_TWO':names[12],'G_CAM_XZ_RELATED_THREE':names[13],'G_CAM_XZ_DISTANCE_RELATED':names[14],'G_CAM_XZ_DIST':names[15],
              'G_CAM_Y_DISTANCE_RELATED_TWO':names[16],'G_CAM_Y_DISTANCE_RELATED_THREE':names[17],
              'G_CAM_Y_DISTANCE_RELATED':names[18],'G_CAM_Y_DIST':names[19]}
    harness += ''.join(f'#define {key} {value}\n' for key,value in macros.items())
    harness += 'enum {CAMERAMODE_START=4,CAMERAMODE_FAR=5};struct CCamera {int mCameraMode=3,field_23C=1;'
    harness += 'int mZoom=2365,field_178=0,field_17C=0;u16 field_174=0;void SetZoom(i32,u16);'
    harness += 'void SetCamXOffset(i16,u16);void SetCamYOffset(i16,u16);void SetCamZOffset(i16,u16);'
    harness += 'void SetCamXZDistance(u16,u16);void SetCamYDistance(i16,u16);};\n'
    for signature in ['void CCamera::SetCamXOffset(i16 offset, u16 frames)','void CCamera::SetCamYOffset(i16 offset, u16 frames)',
                      'void CCamera::SetCamZOffset(i16 offset, u16 frames)','void CCamera::SetCamXZDistance(u16 dist, u16 frames)',
                      'void CCamera::SetCamYDistance(i16 dist, u16 frames)', 'void CCamera::SetZoom(i32 a2, u16 a3)']:
        harness += controller_method(source,signature)+'\n'
    ai = controller_method(source,'void CCamera::AI(void)')
    update = ai[ai.index('\tif (G_CAM_XZ_DISTANCE_RELATED <= dt)'):ai.index('\tgCameraOffset->vy = yOffset << 12;')]
    harness += 'void update(int dt) {\n'+update+'\n'
    harness += 'G_CAM_X_OFFSET_FOUR=G_WTF_CAM[29];G_CAM_Y_OFFSET_FOUR=G_WTF_CAM[30];G_CAM_Z_OFFSET_FOUR=G_WTF_CAM[31];'
    harness += 'G_CAM_XZ_DIST=G_WTF_CAM[32];G_CAM_Y_DIST=G_WTF_CAM[33];}\n'
    zoom_update = ai[ai.index('\tu16 zoomFrames = this->field_174;'):ai.index('\tif (G_SCONTROL[0].Select.Pressed)')]
    harness += 'void updateZoom(CCamera* camera,int dt) {\n' + zoom_update.replace('this->','camera->') + '}\n'
    harness += r'''
int main() {
 int parameter,initial,target,duration,enabled,ticks;
 while(std::cin>>parameter>>initial>>target>>duration>>enabled>>ticks) {
  CCamera camera;camera.field_23C=enabled;
  gCamXOffsetRelatedTwo=gCamYOffsetTwo=gCamZOffsetTwo=gCamXZDistanceRelated=gCamYDistanceRelated=0;
  switch(parameter) {
   case 0:camera.SetCamXOffset(initial,0);camera.SetCamXOffset(target,duration);break;
   case 1:camera.SetCamYOffset(initial,0);camera.SetCamYOffset(target,duration);break;
   case 2:camera.SetCamZOffset(initial,0);camera.SetCamZOffset(target,duration);break;
   case 3:camera.SetCamXZDistance(initial,0);camera.SetCamXZDistance(target,duration);break;
   case 4:camera.SetCamYDistance(initial,0);camera.SetCamYDistance(target,duration);break;
   case 5:camera.SetZoom(initial,0);camera.SetZoom(target,duration);break;
  }
  std::cout<<'[';
  for(int i=0;i<20;i++) {
   update(ticks);
   updateZoom(&camera,ticks);
   int remaining[]={gCamXOffsetRelatedTwo,gCamYOffsetTwo,gCamZOffsetTwo,gCamXZDistanceRelated,gCamYDistanceRelated,camera.field_174};
   if(i) std::cout<<',';
   std::cout<<'['<<(parameter==5 ? camera.mZoom : gWtfCam[29+parameter])<<','<<remaining[parameter]<<']';
  }
  std::cout<<"]\n";
 }
}
'''
    rng=random.Random(417)
    cases=[]
    for parameter in range(5):
      for initial,target in [(0,-40),(-40,17),(100,-150),(512,528),(32767,-32768)]:
       for duration in [0,1,3,16,32]:
        for enabled in [False,True]:
         cases.append({'parameter':parameter,'initial':initial,'target':target,'duration':duration,'enabled':enabled,'ticks':rng.choice([1,2,3])})
    for initial,target in [(2365,1000),(1000,4096),(4096,2365),(2365,65535),(65535,1)]:
     for duration in [0,1,3,16,32]:
      for ticks in [1,2,3]:
       cases.append({'parameter':5,'initial':initial,'target':target,'duration':duration,'enabled':True,'ticks':ticks})
    lines=[' '.join(map(str,[c['parameter'],c['initial'],c['target'],c['duration'],int(c['enabled']),c['ticks']])) for c in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-camera-reference-') as directory:
      cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference'
      cpp.write_text(harness)
      subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
      results=subprocess.check_output([str(binary)],input='\n'.join(lines).encode()).decode().splitlines()
    for case,result in zip(cases,results,strict=True):
      case['expected']=json.loads(result)
    destination=root/'modern/OpenSpidey.Tests/Fixtures/camera-parameters.json'
    destination.write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} camera parameter sequences / {len(cases)*20} updates.')


if __name__=='__main__':
    main()
