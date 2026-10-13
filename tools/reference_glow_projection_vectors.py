"""Compile DisplayGlowList and actual PCGfx_DrawQPoly3D to capture source projected triangles."""
import json
from pathlib import Path
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    bit=(root/"src/engine/world/bit.cpp").read_text();pc=(root/"src/engine/rendering/PCGfx.cpp").read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <algorithm>
#include <cmath>
#include <iostream>
#include <vector>
using f32=float;using VECTOR=Vector;
void print_if_false(bool,const char*){}
struct SSection{u32 Radius=0,PadBGR=0;};struct SFringeQuad{u32 Width=0,CodeBGR=0;};
struct CGlow{CGlow*mNext=nullptr;CVector mPos;int mNumSections=0,mNumFringes=0,mStepAngle=0;u16 mAngle=0;u32 mMask=~0u,mCentreCodeBGR=0;u8 mSkipTriangles=0;SSection*mpSections;SFringeQuad*mpFringes;};
u8 clip[24]={};u8*G_VIEW_CLIP_INFO=clip;u32 scratch[65536];u32*G_PPOLY=scratch;u8*G_POLY_BUFFER_END=reinterpret_cast<u8*>(scratch+65536);
CVector cameraPos;CVector*gCameraViewPos=&cameraPos;u8 rings[1024];u8*gGlowRingBase=rings;
float focus[3]={},nearThreshold=100;float*gGlowFocusPoint=focus,*gGlowNearThreshold=&nearThreshold;
short word_610C48[8192];int depth=0,centreX=0,centreY=0;
#define GLOW_RING_X(buf,i) (*(i16*)((buf)+4*(i)))
#define GLOW_RING_Y(buf,i) (*(i16*)((buf)+4*(i)+2))
int G_GAME_RESOLUTION_X=512,G_GAME_RESOLUTION_Y=240,G_XRES=512,G_YRES=240;
void gte_ldlv0(VECTOR*){}void gte_rtps(){}void gte_stlvnl(VECTOR*p){p->vx=p->vy=0;p->vz=depth;}
void gte_stsxy(i32*p){*p=(u16)centreX|((u32)(u16)centreY<<16);}
void Algebra_Transform4(float*out,float*){out[3]=float(depth);}
enum DCGfx_BlendingMode{DCGfx_BlendingMode_2=2};void PCGfx_UseTexture(int,DCGfx_BlendingMode){}
struct _DXVERT{float x=0,y=0,depth=0,reciprocalW=0,u=0,v=0;u32 colorArgb=0;};
int gPcGfxDrawRelated=0,gNonRendderSettingE=0,gLowGraphics=0;float gRenderInitOne[3]={0,0,1},gRenderInitTwo[2]={1,1};
float gPcGfxBrightnessPower[8]={.8f,.85f,.9f,.95f,1,1.05f,1.1f,1.15f};u8 gPcGfxBrightnessValues[256];
std::vector<std::vector<long long>> triangles;
void PCGfx_ClipTriToNearPlane(_DXVERT**verts,_DXVERT**){verts[3]=nullptr;}
u32 gsub_506D70(u32 colour,float){return colour;}
void submitPoly(_DXVERT**verts,int count){
 for(int i=0;i<count;i++){
  _DXVERT*v=verts[i];u32 colour=v->colorArgb;
  colour=(colour&0xff000000)|gPcGfxBrightnessValues[colour&255]|(u32(gPcGfxBrightnessValues[(colour>>8)&255])<<8)|(u32(gPcGfxBrightnessValues[(colour>>16)&255])<<16);
  triangles.push_back({static_cast<int>(v->x),static_cast<int>(v->y),colour});
 }
}
'''
    code+=controller_method(pc,"void PCGfx_SetBrightness(")
    code+=controller_method(pc,"void PCGfx_DrawQPoly3D(")
    for signature in ["static i16 ClampGlowX(","static i16 ClampGlowY(","static u8 GlowFade(","static u32 GlowColor("]:code+=controller_method(bit,signature)
    code+=controller_method(bit,"void DisplayGlowList(")
    code+=r'''
int main(){
 for(int i=0;i<4096;i++){double angle=i*.001536096911877394;word_610C48[2*i]=short(sin(angle)*4096);word_610C48[2*i+1]=short(cos(angle)*4096);}
 PCGfx_SetBrightness(4);long long a[9];while(std::cin>>a[0]){
  for(int i=1;i<9;i++)std::cin>>a[i];CGlow glow;glow.mNumSections=2*a[0];glow.mNumFringes=a[1];glow.mStepAngle=4096/glow.mNumSections;glow.mAngle=a[2];glow.mMask=a[3];glow.mSkipTriangles=a[4];depth=a[5];centreX=std::clamp<int>(a[6],-1024,1023);centreY=std::clamp<int>(a[7],-1024,1023);glow.mCentreCodeBGR=a[8];
  std::vector<SSection>sections(glow.mNumSections);std::vector<SFringeQuad>fringes(glow.mNumSections*glow.mNumFringes);
  for(SSection&s:sections)std::cin>>s.Radius>>s.PadBGR;for(SFringeQuad&f:fringes)std::cin>>f.Width>>f.CodeBGR;
  glow.mpSections=sections.data();glow.mpFringes=fringes.data();G_PPOLY=scratch;triangles.clear();
  *reinterpret_cast<u16*>(clip+0xe)=400;*reinterpret_cast<i16*>(clip+0x10)=centreX;*reinterpret_cast<i16*>(clip+0x12)=centreY;
  void*head=&glow;DisplayGlowList(&head);
  std::cout<<"[";for(size_t i=0;i<triangles.size();i++){if(i)std::cout<<",";std::cout<<"["<<triangles[i][0]<<","<<triangles[i][1]<<","<<triangles[i][2]<<"]";}std::cout<<"]\n";
 }
}
'''
    cases=[]
    for points in [1,3,5]:
        for rows in [0,1,2]:
            for skip in [0,1]:
                for depth in [4,5,199,500,699,700,1000,20000,20001]:
                    inputs=[points,rows,123,0xffffffff,skip,depth,256,120,0x3200c8ff]
                    sections=[[60+(i%2)*49,0x00193264] for i in range(2*points)]
                    fringes=[[16+row*20,0x3A000000 if row else 0x3A006432] for row in range(rows) for _ in range(2*points)]
                    cases.append([inputs,sections,fringes])
    for mask in [0,1,0x55,0xffffffff]:
        for cx,cy in [(-1024,120),(1023,120),(256,-1024),(256,1023),(0,0),(612,340),(1000,900)]:
            cases.append([[5,2,4095,mask,0,500,cx,cy,0x32000000],[[100,0] for _ in range(10)],[[8192,0x3A000000] for _ in range(20)]])
    requests=[" ".join(map(str,inputs+[value for row in sections+fringes for value in row])) for inputs,sections,fringes in cases]
    with tempfile.TemporaryDirectory(prefix="glow-projection-reference-") as temporary:
        directory=Path(temporary);(directory/"reference.cpp").write_text(code)
        subprocess.run(["g++","-std=c++17","-O0","-fwrapv","-I",str(root/"src"),str(directory/"reference.cpp"),"-o",str(directory/"reference")],check=True)
        result=subprocess.run([str(directory/"reference")],input="\n".join(requests),capture_output=True,text=True,check=True)
    fixtures=[{"input":inputs,"sections":sections,"fringes":fringes,"vertices":json.loads(output)} for (inputs,sections,fringes),output in zip(cases,result.stdout.splitlines(),strict=True)]
    destination=root/"modern/OpenSpidey.Tests/Fixtures/glow-projection.json"
    destination.write_text(json.dumps(fixtures,separators=(",",":"))+"\n")
    print(f"Compiled full source DisplayGlowList/DrawQPoly3D: {len(fixtures)} projection/colour cases written to {destination}")


if __name__=="__main__":main()
