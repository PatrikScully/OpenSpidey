"""Capture full original PC Panel_DisplayTimer461D00, original textured border geometry and complete display/font/audio/module order."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "original_executable.h"
#include <iostream>
#include <vector>
std::vector<long long>events;int level,random20,random30;unsigned char queue[0x2000];
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
int getLevel(){return level;}int rnd(int range){int value=range==20?random20:random30;events.insert(events.end(),{1,range,value});return value;}
void module(const char*name,unsigned index,const unsigned*in,unsigned*out){assert(index==1&&!in&&!out);int id=!strcmp(name,"l2a1lsc")?0:!strcmp(name,"venom")?1:!strcmp(name,"l5a5lsc")?2:3;events.insert(events.end(),{2,id,index});}
void sound(unsigned id,short volume,int pitch){events.insert(events.end(),{3,id,volume,pitch});}
void useTexture(int clut,int blend){assert(clut==7&&blend==1);events.insert(events.end(),{4,clut,blend});}
void quad(float ax,float ay,float au,float av,unsigned ac,float bx,float by,float bu,float bv,unsigned bc,float cx,float cy,float cu,float cv,unsigned cc,float dx,float dy,float du,float dv,unsigned dc,float depth){assert(ac==bc&&bc==cc&&cc==dc);events.insert(events.end(),{5,(int)ax,(int)ay,(int)bx,(int)by,(int)cx,(int)cy,(int)dx,(int)dy,ac,(int)au,(int)av,(int)bu,(int)bv,(int)cu,(int)cv,(int)du,(int)dv,(int)std::lround(depth*10000)});}
void scale(int value){events.insert(events.end(),{6,value});}void justify(unsigned char value){events.insert(events.end(),{7,value});}
void rgb(unsigned char r,unsigned char g,unsigned char b,int arg){events.insert(events.end(),{8,r,g,b,arg});}
char*getFont(){events.push_back(9);static char font[]="sp_fnt00.fnt";return font;}
void setFont(const char*font){events.insert(events.end(),{10,!strcmp(font,"sp_fnt01.fnt")?1:0});}
int copyFormat(char*out,const char*format,...){strcpy(out,format);return strlen(format);}
int text(int x,int y,const char*value,int arg,unsigned depth){events.insert(events.end(),{11,x,y});for(int i=0;i<5;i++)events.push_back((unsigned char)value[i]);events.insert(events.end(),{arg,depth});return 0;}
void flat(float depth,int x,int y,int w,int h,unsigned char r,unsigned char g,unsigned char b,int arg,int blend){events.insert(events.end(),{12,(int)std::lround(depth*10000),x,y,w,h,r,g,b,arg,blend});}
void assertion(unsigned char good,const char*){assert(good);}void debug(...){}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4DE770,(void*)&getLevel);patch(0x4E5DA0,(void*)&rnd);patch(0x47D470,(void*)&module);patch(0x471C50,(void*)&sound);patch(0x506440,(void*)&useTexture);patch(0x507910,(void*)&quad);patch(0x458620,(void*)&scale);patch(0x458610,(void*)&justify);patch(0x458640,(void*)&rgb);patch(0x458D50,(void*)&getFont);patch(0x458C30,(void*)&setFont);patch(0x529F90,(void*)&copyFormat);patch(0x458700,(void*)&text);patch(0x462D60,(void*)&flat);patch(0x4015B0,(void*)&assertion);patch(0x46CB90,(void*)&debug);
*reinterpret_cast<int*>(0x568154)=*reinterpret_cast<int*>(0x61B5FC)=512;*reinterpret_cast<int*>(0x568158)=*reinterpret_cast<int*>(0x628614)=240;
long long v[15];while(std::cin>>v[0]){for(int i=1;i<15;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={},frames[20*8]={},texture[16]={};at<unsigned short>(texture,2)=7;at<void*>(frames,19*8+4)=v[12]?texture:nullptr;*reinterpret_cast<void**>(0x56EA9C)=frames;*reinterpret_cast<void**>(0x6A9038)=v[1]?player:nullptr;at<int>(player,0xE18)=v[2];player[0x1AC]=v[3];level=v[0];*reinterpret_cast<unsigned char*>(0x60F771)=v[4];*reinterpret_cast<unsigned*>(0x60F774)=v[5];*reinterpret_cast<unsigned char*>(0x60F784)=v[6];*reinterpret_cast<unsigned char*>(0x60F785)=v[7];*reinterpret_cast<int*>(0x60F66C)=v[8];*reinterpret_cast<int*>(0x60F74C)=v[9];*reinterpret_cast<int*>(0x60F780)=v[10];*reinterpret_cast<int*>(0x60F76C)=v[11];random20=v[13];random30=v[14];*reinterpret_cast<void**>(0x56FB04)=queue;*reinterpret_cast<void**>(0x5FCD1C)=queue+sizeof queue;events.clear();reinterpret_cast<void(*)()>(0x461D00)();
std::cout<<'['<<unsigned(*reinterpret_cast<unsigned char*>(0x60F784))<<','<<*reinterpret_cast<int*>(0x60F66C)<<','<<unsigned(*reinterpret_cast<unsigned char*>(0x60F785))<<','<<*reinterpret_cast<int*>(0x60F74C)<<','<<*reinterpret_cast<int*>(0x60F780)<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    code=code.replace('#include <iostream>','#include <iostream>\n#include <cmath>')
    cases=[[level,present,gate,synth,display,12345,0,0,0,0,0,0,1,0,0]
           for level in [0x101,513,1281,1285,2054] for present in [0,1] for gate in [0,1] for synth in [0,1] for display in [0,1]]
    cases += [[0x101,1,0,0,1,ticks,one,two,pos1,pos2,last,offset,texture,rnd1,rnd2]
              for ticks,last in [(0,0),(59,0),(60,0),(599,9),(3599,59),(3600,59),(36000,0),(2**32-1,-1)]
              for one,two,pos1,pos2 in [(0,0,0,0),(1,1,17<<12,23<<12),(1,1,23<<12,43<<12),(1,1,36<<12,176127),(1,1,37<<12,176128),(1,1,43<<12,176129),(1,1,-4096,-4096),(255,255,2147483647,2147483647)]
              for offset,texture,rnd1,rnd2 in [(0,1,0,0),(13,1,1,1),(-32,0,0,1),(2147483647,1,1,0)]]
    with tempfile.TemporaryDirectory(prefix='openspidey-panel-timer-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/mission-timer-display.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} full original PC timer displays, special-level suppression, unsigned digits, real stretched border/mirror calls and every font/audio/scan command.')


if __name__=='__main__':main()
