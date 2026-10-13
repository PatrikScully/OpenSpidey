"""Compile actual CItem::Burst and compare full geometry/face/RNG orchestration with original PC, for Dome's zero-angle source items."""
import argparse
import json
import subprocess
import tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True);args=parser.parse_args()
    root=Path(__file__).resolve().parent.parent;source=(root/'src/engine/world/ob.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
#include <sstream>
#include <cassert>
#include <cstring>
#include <cstdlib>
using VECTOR=CVector;struct SVECTOR{short vx,vy,vz,pad;};struct MATRIX{int words[8]={};};
struct SModel{unsigned short Flags=8,NumVertices=6,NumNormals=0,NumFaces=3;char header[20]={};SVECTOR Vertices[6];unsigned Faces[3][8]={};};static_assert(offsetof(SModel,Vertices)==28);
struct Region{SModel**ppModels;};Region G_PSXREGION[1];
struct CItem{unsigned vt=0;unsigned short mFlags=0,inquiry=0;CVector mPos;CSVector mAngles;unsigned short mModel=0;unsigned char pad1C[3]={},mRegion=0;void Burst(int,int);};
std::vector<void*>allocations;std::vector<int>events;int cursor,ground;CVector translation;SVECTOR*vertex;
void emit(std::initializer_list<int>v){events.insert(events.end(),v);}
int Rnd(int bound){int value=bound?(cursor*7+1)%bound:0;cursor++;emit({2,bound,value});return value;}
void print_if_false(unsigned char v,const char*){assert(v);}
int Web_GetGroundY(CVector*p){emit({1,p->vx,p->vy,p->vz});return ground;}
void*Mem_New(unsigned bytes){void*p=calloc(1,bytes);allocations.push_back(p);return p;}void Mem_Delete(void*){}
void*allocateDc(unsigned bytes,int,int,void*,bool){return Mem_New(bytes);}
void M3dMaths_RotMatrixYXZ(SVECTOR*a,MATRIX*){assert(a->vx==0&&a->vy==0&&a->vz==0);}void gte_SetRotMatrix(MATRIX*){}
void M3dAsm_SetTransVector(VECTOR*p){translation=*p>>12;}void gte_ldv0(SVECTOR*p){vertex=p;}void gte_rtv0tr(){}
void gte_stlvnl(VECTOR*out){*out=translation+CVector(vertex->vx,vertex->vy,vertex->vz);}
int sourceLength(CVector p){p>>=12;return reinterpret_cast<int(*)(int)>(0x46D430)(p.vx*p.vx+p.vy*p.vy+p.vz*p.vz);}
int CVector::Length(){return sourceLength(*this);}
struct CItemFrag{CItemFrag(unsigned*face,CVector*vertices,CVector*velocity,int floor){int life=Rnd(30)+1;emit({3,int(face[1]),velocity->vx,velocity->vy,velocity->vz,floor,life});for(int corner=0;corner<4;corner++){CVector p=vertices[(face[1]>>(corner*8))&255];emit({p.vx,p.vy,p.vz});}}};
'''+controller_method(source,'void CItem::Burst(')+r'''
void patch(unsigned a,void*t){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(t)-a-5;}
void __attribute__((thiscall)) fragmentCtor(void*,unsigned*face,CVector*vertices,CVector*velocity,int floor){CItemFrag(face,vertices,velocity,floor);}
int __attribute__((thiscall)) lengthThunk(CVector*p){return sourceLength(*p);}
std::string capture(){std::ostringstream t;t<<'[';for(unsigned i=0;i<events.size();i++){if(i)t<<',';t<<events[i];}t<<']';events.clear();return t.str();}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
// Validate the shared Length leaf before redirecting GTE output for the zero-angle item transform boundary.
for(CVector p:std::vector<CVector>{{0,0,0},{12345,-54321,67890},{-2147483647,2147483647,1},{4096,-4096,8192}})
assert(reinterpret_cast<int(__attribute__((thiscall)) *)(void*)>(0x4E74A0)(&p)==sourceLength(p));
patch(0x4E74A0,reinterpret_cast<void*>(&lengthThunk));
patch(0x4015B0,reinterpret_cast<void*>(&print_if_false));patch(0x4F54A0,reinterpret_cast<void*>(&Web_GetGroundY));patch(0x4581A0,reinterpret_cast<void*>(&allocateDc));patch(0x458210,reinterpret_cast<void*>(&Mem_Delete));patch(0x4088A0,reinterpret_cast<void*>(&Mem_New));patch(0x43C820,reinterpret_cast<void*>(&fragmentCtor));patch(0x4E5DA0,reinterpret_cast<void*>(&Rnd));
patch(0x46E730,reinterpret_cast<void*>(&M3dMaths_RotMatrixYXZ));patch(0x46D7B0,reinterpret_cast<void*>(&gte_SetRotMatrix));patch(0x46E770,reinterpret_cast<void*>(&M3dAsm_SetTransVector));patch(0x46D8A0,reinterpret_cast<void*>(&gte_ldv0));patch(0x46DAF0,reinterpret_cast<void*>(&gte_rtv0tr));patch(0x46D790,reinterpret_cast<void*>(&gte_stlvnl));
for(auto range:std::vector<std::pair<unsigned,int>>{{0x45FDC7,6},{0x45FDCE,7},{0x46000E,7}})std::memset(reinterpret_cast<void*>(range.first),0x90,range.second);
SModel model;for(int i=0;i<6;i++)model.Vertices[i]={short(i?i*23-31:0),short(i?i*17-13:0),short(i?i*11+7:0),0};model.Faces[0][0]=8u<<18;model.Faces[0][1]=0x03020100;model.Faces[1][0]=(8u<<18)|0x10;model.Faces[1][1]=0x03050400;model.Faces[2][0]=8u<<18;model.Faces[2][1]=0x03050401;SModel*models[]={&model};G_PSXREGION[0].ppModels=models;*reinterpret_cast<SModel***>(0x6B2454)=models;
CVector pos;int speed,range,seed;while(std::cin>>pos.vx>>pos.vy>>pos.vz>>speed>>range>>ground>>seed){events.clear();cursor=seed;CItem source;source.mPos=pos;source.Burst(speed,range);auto expected=capture();cursor=seed;events.clear();CItem actual;actual.mPos=pos;reinterpret_cast<void(__attribute__((thiscall)) *)(void*,int,int)>(0x45FDC0)(&actual,speed,range);auto value=capture();if(value!=expected){std::cerr<<"Source="<<expected<<"\nactual="<<value<<'\n';abort();}std::cout<<expected<<'\n';for(void*p:allocations)free(p);allocations.clear();}}
'''
    cases=[[x,y,z,speed,random_speed,ground,seed]
        for x,y,z in [(0,0,0),(12345,-54321,67890),(4096,-4096,8192),(2147483647,-2147483648,12345)]
        for speed,random_speed in [(0,0),(30,30),(-100,120)] for ground in [-54321,200000] for seed in [0,73,511]]
    with tempfile.TemporaryDirectory(prefix='openspidey-item-burst-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie',
            '-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/item-burst.json').write_text(json.dumps(
        [dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} complete source CItem::Burst geometry/face/RNG cases; original PC verified, zero-angle Dome scope.')


if __name__=='__main__':main()
