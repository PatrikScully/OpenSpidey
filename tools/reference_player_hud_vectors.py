"""Compile the recovered player HUD drawing block over recorded sprite/text/quad boundaries."""
import json
from pathlib import Path
import random
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / "src/game/ui/panel.cpp").read_text()
    display = controller_method(source, "void Panel_Display(void)")
    player_block = display[display.index("POLY_FT4 *pWebcart"):display.index("\n\tPanel_DisplayHealthBar();")]
    code = r'''
#include <cmath>
#include <cstdlib>
#include <iostream>
#include <vector>
#include "core/types.h"
using f32=float;
struct Texture{int id;};struct SAnimFrame{char OffX,OffY;u8 Width,Height;Texture*pTexture;};
struct POLY_FT4{u32 tag;u8 r0,g0,b0,code;i16 x0,y0;u8 u0,v0;u16 clut;i16 x1,y1;u8 u1,v1;u16 tpage;i16 x2,y2;u8 u2,v2;u16 pad1;i16 x3,y3;u8 u3,v3;u16 pad2;};
struct Player{int field_5D8,mMaxHealth,field_5EC,mHealth,mWebbing;char field_5E8,field_5E9;int field_5DC,field_5E0;} player,*G_MECHLIST_PLAYER=&player;
int G_TIMER_RELATED,screenY,*gPanelScreenY=&screenY;char gWebCartDigits[3];
struct Trig{short sin,cos;};Trig G_RCOSSIN_TBL[4096];
SAnimFrame sp[5],cart,*G_ANIM_SP=sp,*G_ANIM_WEBCART=&cart,*gSpideyAnimTwo=nullptr,*gSpideyAnim=nullptr;
Texture textures[6];POLY_FT4 poly;std::vector<std::vector<long long>>events;
enum DCGfx_BlendingMode{DCGfx_BlendingMode_0};
void print_if_false(bool,const char*){}
void*Panel_DrawTexturedPoly(Texture*,int){poly={};poly.r0=poly.g0=poly.b0=128;return &poly;}
void PanelHB_DrawIconOverlay(POLY_FT4*p,Texture*texture,DCGfx_BlendingMode,float depth){events.push_back({0,(int)(depth*10),texture->id,p->x0,p->y0,p->x1,p->y1,p->x2,p->y2,p->x3,p->y3,p->r0,p->g0,p->b0});}
void Mess_SetScale(int){}void Mess_SetTextJustify(int){}void Mess_SetRGB(int,int,int,int){}void Mess_SetRGBBottom(int,int,int){}void Mess_SetSort(int){}
void Mess_DrawText(int x,int y,const char*text,int,int){events.push_back({1,61,x,y,(unsigned char)text[0],(unsigned char)text[1]});}
void DCDrawGouraudPoly(float depth,int x,int y,int w,int h,u32 c0,u32 c1,u32 c2,u32 c3){events.push_back({2,(int)(depth*10),x,y,w,h,c0,c1,c2,c3});}
void DCPanel_DrawFlatShadedPoly(float depth,int x,int y,int w,int h,int r,int g,int b,int,int){u32 colour=(u32)(r|(g<<8)|(b<<16));events.push_back({2,(int)(depth*10),x,y,w,h,colour,colour,colour,colour});}
'''
    code += controller_method(source, "void Panel_SetStretchedScreenCoords(\n\t\ti32 a1,") + "\n"
    code += controller_method(source, "static void PanelDisp_DrawIcon(") + "\n"
    code += "void DrawPlayer(){\n" + player_block + "\n}\n"
    code += r'''
int main(){
 for(int i=0;i<4096;i++){double angle=i*0.001536096911877394;G_RCOSSIN_TBL[i]={(short)(std::sin(angle)*4096.0),(short)(std::cos(angle)*4096.0)};}
 for(int i=0;i<6;i++)textures[i].id=i;
 for(int i=0;i<5;i++)sp[i]={(char)(i==0?-32:-16),(char)(i==0?-32:-16),(u8)(i==0?64:32),(u8)(i==0?64:32),textures+i};
 cart={-16,-16,32,32,textures+5};int fire,armor;
 while(std::cin>>player.mHealth>>player.mMaxHealth>>player.mWebbing>>player.field_5D8>>fire>>armor>>player.field_5EC>>G_TIMER_RELATED>>player.field_5E0>>player.field_5DC>>screenY){
  player.field_5E8=fire;player.field_5E9=armor;events.clear();DrawPlayer();std::cout<<'[';
  for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<'[';for(unsigned j=0;j<events[i].size();j++){if(j)std::cout<<',';std::cout<<events[i][j];}std::cout<<']';}
  std::cout<<"]\n";
 }
}
'''
    cases = []
    for health in [0, 1, 10, 39, 40, 50, 99, 100]:
        for age in [-1, 0, 1, 31, 32, 100]:
            for fire in [0, 1]:
                cases.append([health, 100, 2048, 10, fire, 0, 80, 1000, 1000-age, 1000-age, 0])
    rng = random.Random(0x4658C0)
    for _ in range(256):
        maximum = rng.choice([80, 100, 200, 600])
        clock = rng.randint(-2147483648, 2147483647)
        cases.append([rng.randint(-100, maximum+100), maximum, rng.choice([0, 1, 4095, 4096, 8192, -4096]),
                      rng.randint(0, 10), rng.randrange(2), rng.randrange(2), rng.randint(0, maximum), clock,
                      rng.randint(-2147483648, 2147483647), rng.randint(-2147483648, 2147483647), rng.randint(-20, 20)])
    with tempfile.TemporaryDirectory(prefix="openspidey-player-hud-reference-") as folder:
        cpp = Path(folder) / "reference.cpp"
        binary = Path(folder) / "reference"
        cpp.write_text(code)
        subprocess.run(["g++", "-std=c++17", "-O0", "-fwrapv", "-I", str(root / "src"), str(cpp), "-o", str(binary)], check=True)
        output = subprocess.check_output([str(binary)],input=("\n".join(" ".join(map(str,c)) for c in cases)+"\n").encode()).decode().splitlines()
    (root / "modern/OpenSpidey.Tests/Fixtures/player-hud.json").write_text(json.dumps(
        [dict(input=case,expected=json.loads(line)) for case,line in zip(cases,output,strict=True)],separators=(",", ":")) + "\n")
    print(f"Generated {len(cases)} compiled-source player HUD frames; sprite vertices have separate direct PC proof.")


if __name__ == "__main__":
    main()
