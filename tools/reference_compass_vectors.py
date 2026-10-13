"""Compile recovered compass with PC-corrected stores and execute the actual PC463860 control/math body."""
import argparse,json,random,subprocess,tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--original-exe',type=Path,required=True);a=p.parse_args();root=Path(__file__).resolve().parent.parent
 source=(root/'src/game/ui/panel.cpp').read_text();math=(root/'src/engine/rendering/ps2funcs.cpp').read_text()
 display=controller_method(source,'void Panel_DisplayCompass(').replace('i32 dist =','u32 dist =').replace('(((320 * ((9 * G_RCOSSIN_TBL[idxA].sin) >> 12)) >> 9))','320 * ((9 * G_RCOSSIN_TBL[idxA].sin) >> 12) / 512')
 # Actual PC quad call differs from recovered source's unused PSX triangle corner order.
 old='''(xOff1 + 432) * xScale, (199 - tipY) * yScale, 0.0f, 0.0f, arrowColor,
				(sideA + 432) * xScale, (199 - tailA) * yScale, 0.0f, 0.0f, arrowColor,
				(xOff2 + 432) * xScale, (199 - tipY2) * yScale, 0.0f, 0.0f, arrowColor,'''
 new='''(sideA + 432) * xScale, (199 - tailA) * yScale, 0.0f, 0.0f, arrowColor,
				(xOff2 + 432) * xScale, (199 - tipY2) * yScale, 0.0f, 0.0f, arrowColor,
				(xOff1 + 432) * xScale, (199 - tipY) * yScale, 0.0f, 0.0f, arrowColor,'''
 if old in display:display=display.replace(old,new)
 code=r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <cmath>
#include <iostream>
#include <vector>
using f32=float;using f64=double;
struct VECTOR{int vx,vy,vz,pad;};struct MATRIX{short m[3][3];};struct Texture{unsigned char u0,v0;unsigned short clut;unsigned char u1,v1;unsigned short tpage;};
struct SAnimFrame{char OffX,OffY;unsigned char Width,Height;Texture*pTexture;};
'''
 header=(root/'src/game/ui/panel.h').read_text();code+=controller_method(header,'struct POLY_FT4')+';\n'
 code+=r'''
struct POLY_F3{unsigned tag;unsigned char r0,g0,b0,code;short x0,y0,x1,y1,x2,y2;};
CVector G_COMPASS_POSITION;unsigned char G_COMPASS_STATUS;CVector camera;MATRIX matrix;CVector*stru_56F1B4=&camera;MATRIX*stru_56F224=&matrix;
int* gCompassFlashTimer=reinterpret_cast<int*>(0x60F768);int*gPanelScreenY=reinterpret_cast<int*>(0x60F76C);
unsigned G_TIMER_RELATED;int G_GAME_RESOLUTION_X=512,G_GAME_RESOLUTION_Y=240,G_XRES=512,G_YRES=240;unsigned buffer[1000],*G_PPOLY;
Texture tex={0,0,7,63,63,0};SAnimFrame frame={-32,-32,64,64,&tex};SAnimFrame*G_ANIM_COMPASS=&frame;
struct Heading{short sin,cos;}G_RCOSSIN_TBL[4096];VECTOR loaded,result;MATRIX current;std::vector<long long> events;int fail,allocation;POLY_FT4 quads[2];
void gte_SetRotMatrix(MATRIX*p){current=*p;}void gte_ldlvl(VECTOR*p){loaded=*p;}void gte_rtir(){int v[3]={loaded.vx,loaded.vy,loaded.vz};int*out=&result.vx;for(int row=0;row<3;row++)out[row]=(v[0]*current.m[row][0]+v[1]*current.m[row][1]+v[2]*current.m[row][2])>>12;}void gte_stlvnl(VECTOR*p){p->vx=result.vx;p->vy=result.vy;p->vz=result.vz;}
void print_if_false(int,const char*){}void stubbed_printf(...){}
enum{DCGfx_BlendingMode_0};using DCGfx_BlendingMode=int;void PCGfx_UseTexture(int,int){}
void PCGfx_DrawQPoly2D(float x0,float y0,float u0,float v0,unsigned c0,float x1,float y1,float u1,float v1,unsigned c1,float x2,float y2,float u2,float v2,unsigned c2,float x3,float y3,float u3,float v3,unsigned c3,float depth){events.insert(events.end(),{3,(int)x0,(int)y0,(int)x1,(int)y1,(int)x2,(int)y2,(int)x3,(int)y3,c0,std::lround(depth*10000),std::lround(u0*10000),std::lround(u1*10000)});}
POLY_FT4*Panel_DrawTexturedPoly(Texture*,int){int half=allocation++;events.insert(events.end(),{1,half});if(fail==half)return nullptr;auto*p=&quads[half];std::memset(p,0,sizeof(*p));p->code=0x2C;p->r0=p->g0=p->b0=128;p->u1=63;p->u3=63;return p;}
'''
 for marker in ['void VectorNormal(', 'i32 M3dMaths_SquareRoot0(', 'int ratan2(']:code+=controller_method(math,marker)+'\n'
 code+=controller_method(source,'void Panel_SetStretchedScreenCoords(')+'\n'+controller_method(source,'static void PanelCompass_DrawNeedleHalf(')+'\n'+display+'\n'
 code+=r'''
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void setup(const char*path){original_executable::loadOriginalImage(path);
 patch(0x46D7B0,(void*)&gte_SetRotMatrix);patch(0x46D870,(void*)&gte_ldlvl);patch(0x46DA40,(void*)&gte_rtir);patch(0x46D790,(void*)&gte_stlvnl);patch(0x470430,(void*)&VectorNormal);patch(0x46D430,(void*)&M3dMaths_SquareRoot0);patch(0x46D130,(void*)&ratan2);patch(0x506440,(void*)&PCGfx_UseTexture);patch(0x507910,(void*)&PCGfx_DrawQPoly2D);patch(0x462BB0,(void*)&Panel_DrawTexturedPoly);patch(0x462C30,(void*)&Panel_SetStretchedScreenCoords);patch(0x46CB90,(void*)&stubbed_printf);patch(0x4015B0,(void*)&print_if_false);
 std::memcpy(reinterpret_cast<void*>(0x610C48),G_RCOSSIN_TBL,sizeof G_RCOSSIN_TBL);*reinterpret_cast<unsigned*>(0x60F75C)=reinterpret_cast<unsigned>(&frame);*reinterpret_cast<int*>(0x568154)=512;*reinterpret_cast<int*>(0x568158)=240;*reinterpret_cast<int*>(0x61B5FC)=512;*reinterpret_cast<int*>(0x628614)=240;*reinterpret_cast<unsigned char*>(0x54D341)=1;
}
int main(int argc,char**argv){assert(argc==2);for(int i=0;i<4096;i++){double a=i*.001536096911877394;G_RCOSSIN_TBL[i]={short(std::sin(a)*4096),short(std::cos(a)*4096)};}setup(argv[1]);int active,flash,y,heading;
 while(std::cin>>active>>G_COMPASS_POSITION.vx>>G_COMPASS_POSITION.vy>>G_COMPASS_POSITION.vz>>camera.vx>>camera.vy>>camera.vz>>heading>>G_TIMER_RELATED>>y>>flash>>fail){G_COMPASS_STATUS=active;matrix={};matrix.m[0][0]=G_RCOSSIN_TBL[heading&4095].cos;matrix.m[0][2]=G_RCOSSIN_TBL[heading&4095].sin;matrix.m[1][1]=4096;matrix.m[2][0]=-G_RCOSSIN_TBL[heading&4095].sin;matrix.m[2][2]=G_RCOSSIN_TBL[heading&4095].cos;*gPanelScreenY=y;*gCompassFlashTimer=flash;allocation=0;events.clear();G_PPOLY=buffer;Panel_DisplayCompass();auto gold=events;int goldFlash=*gCompassFlashTimer;
 *reinterpret_cast<unsigned char*>(0x60F77C)=active;std::memcpy(reinterpret_cast<void*>(0x60F708),&G_COMPASS_POSITION,12);std::memcpy(reinterpret_cast<void*>(0x56F1B4),&camera,12);std::memcpy(reinterpret_cast<void*>(0x56F224),&matrix,18);*reinterpret_cast<unsigned*>(0x6B4CA8)=G_TIMER_RELATED;*reinterpret_cast<unsigned*>(0x56FB04)=reinterpret_cast<unsigned>(buffer);*gCompassFlashTimer=flash;events.clear();allocation=0;reinterpret_cast<void(*)()>(0x463860)();
 if(events!=gold || *gCompassFlashTimer!=goldFlash){std::cerr<<"Mismatch actual ";for(auto v:events)std::cerr<<v<<',';std::cerr<<" expected ";for(auto v:gold)std::cerr<<v<<',';std::abort();}
 std::cout<<'['<<goldFlash;for(auto v:gold)std::cout<<','<<v;std::cout<<"]\n";
 }}
'''
 cases=[]
 for pos in [[0,0,0],[0,0,64],[64,0,0],[-64,0,-64],[16384,256,0],[16385,0,0],[-100000,500,200000]]:
  for heading in [0,512,1024,2048,4095]:
   for fail in [-1,0,1]:cases.append([1,*pos,0,0,0,heading,17,0,5,fail])
 rng=random.Random(0x463860)
 for i in range(150):cases.append([rng.choice([0,1]),*[rng.randint(-500000,500000) for _ in range(6)],rng.randrange(4096),rng.randrange(2**32),rng.choice([-100,0,20]),rng.choice([-1,0,1,2,32,2147483647]),rng.choice([-1,0,1])])
 with tempfile.TemporaryDirectory(prefix='compass-source-') as t:
  path=Path(t);(path/'reference.cpp').write_text(code)
  subprocess.run(['g++','-m32','-std=c++17','-O0','-fwrapv','-fno-pie','-no-pie','-mfpmath=sse','-msse2','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(path/'reference.cpp'),'-o',str(path/'reference')],check=True)
  result=subprocess.run([str(path/'reference'),str(a.original_exe)],input='\n'.join(' '.join(map(str,c)) for c in cases),text=True,capture_output=True)
  if result.returncode:print(result.stderr);raise RuntimeError(f'Original comparator failed {result.returncode}')
 (root/'modern/OpenSpidey.Tests/Fixtures/compass.json').write_text(json.dumps({'originalPcDisplayVerified':True,'cases':[{'input':c,'events':json.loads(s)} for c,s in zip(cases,result.stdout.splitlines(),strict=True)]},separators=(',',':'))+'\n')
 print(f'Original PC compass verified: {len(cases)} cases.')
if __name__=='__main__':main()
