"""Source player target selectors, independently checked against PC0x4C8410/0x4C8570."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path);args=parser.parse_args()
    root=Path(__file__).resolve().parent.parent
    code=r'''
#include "core/fixed_vector.h"
#include "core/distance.h"
#include <vector>
#include <iostream>
#include <cmath>
#include <cstring>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
struct VECTOR{int vx,vy,vz,pad;};
struct MATRIX{short m[3][3];};MATRIX*rotation;VECTOR loaded,rotated;
'''
    rendering=(root/'src/engine/rendering/ps2funcs.cpp').read_text()
    code+=controller_method(rendering,'EXPORT void  FixedXForm(').replace('EXPORT ','')+'\n'
    code+=controller_method(rendering,'void VectorNormal(')+'\n'
    code+=r'''
void gte_SetRotMatrix(MATRIX*p){rotation=p;}void gte_ldlvl(VECTOR*p){loaded=*p;}void gte_rtir(){FixedXForm(rotation->m,&loaded,&rotated);}void gte_stlvnl(VECTOR*p){*p=rotated;}
struct CItem{int id;u16 mType;CVector mPos;CItem*mNextItem=nullptr;ushort mRMinor;ushort mCBodyFlags;int mPlayerDist;int visible,mode,hit;unsigned face;CVector first,second;CVector*GetAutoAimTargetPointer(){return mode==1?&first:mode>=2&&mode<=5?&second:nullptr;}};using CBody=CItem;using CSwitch=CItem;
struct SHandle{void*pWhatever;unsigned validation;};struct SLineInfo{CVector StartCoords,EndCoords,MinCoords,MaxCoords,Position,Normal;void*pItem;unsigned*pFace;};
std::vector<CItem>items;CItem*G_BADDY_LIST,*G_CONTROL_BADDY_LIST;std::vector<int>events;unsigned faceWords[4];
int pointId(void*p){for(auto&item:items){if(p==&item.first)return item.id+10000;if(p==&item.second)return item.id+20000;}return 0;}
SHandle Mem_MakeHandle(CItem*p){int id=p?p->id:0;events.insert(events.end(),{3,id});return {p,unsigned(id?id+1000:0)};}
int Utils_LineOfSight(CVector*start,CVector*end,int a,int b){for(auto&item:items)if(!std::memcmp(end,&item.mPos,12)){events.insert(events.end(),{1,item.id,a,b,item.visible});return item.visible;}std::abort();}
void M3dColij_InitLineInfo(SLineInfo*){}
void M3dZone_LineToItem(SLineInfo*l,int mode){for(auto&item:items)if(!std::memcmp(&l->EndCoords,&item.first,12)||!std::memcmp(&l->EndCoords,&item.second,12)){l->pItem=item.hit?&item:nullptr;faceWords[3]=item.face;l->pFace=faceWords;events.insert(events.end(),{2,item.id,mode,item.hit,int(item.face)});return;}std::abort();}
struct CPlayer{CVector mPos;MATRIX field_89C;CBody*SelectTargetBaddy(int,int,int,int);CVector*SelectTargetSwitch(int,int,SHandle*,int,int);};
'''
    source=(root/'src/game/player/spidey.cpp').read_text()
    baddy=controller_method(source,'CBody *CPlayer::SelectTargetBaddy(')
    # Actual JGE/IDIV/SAR and failed-cone JL reject contradict recovered u32 header/bonus-only branch.
    baddy=baddy.replace('if (-dir.vz >= coneThreshold)\n\t\t\t\tscore +=', 'if (-dir.vz < coneThreshold) continue;\n\t\t\t\tscore +=')
    switch=controller_method(source,'CVector *CPlayer::SelectTargetSwitch(').replace('if (node->mType == 407)','if (node->mType != 407)').replace('u32 dist =','i32 dist =').replace('(u32)maxDist','maxDist')
    code+=baddy+'\n'+switch+'\n'
    code+=r'''
#ifdef VALIDATE_ORIGINAL
std::vector<std::vector<unsigned char>>rawItems;
void*rawPoint(int id){for(unsigned i=0;i<items.size();i++){if(id==items[i].id+10000)return rawItems[i].data()+0x10C;if(id==items[i].id+20000)return rawItems[i].data()+0x118;}return nullptr;}
int rawPointId(void*p){for(unsigned i=0;i<items.size();i++){if(p==rawItems[i].data()+0x10C)return items[i].id+10000;if(p==rawItems[i].data()+0x118)return items[i].id+20000;}return 0;}
int rawItemId(void*p){for(unsigned i=0;i<items.size();i++)if(p==rawItems[i].data())return items[i].id;return 0;}
unsigned long long originalHandle(void*p){int id=rawItemId(p);events.insert(events.end(),{3,id});return static_cast<unsigned long long>(id?id+1000:0)<<32|reinterpret_cast<unsigned>(p);}
void originalLine(void*value,int mode){auto raw=static_cast<unsigned char*>(value);CVector&end=*reinterpret_cast<CVector*>(raw+12);for(auto&item:items)if(!std::memcmp(&end,&item.first,12)||!std::memcmp(&end,&item.second,12)){*reinterpret_cast<void**>(raw+0x68)=item.hit?raw:nullptr;faceWords[3]=item.face;*reinterpret_cast<unsigned**>(raw+0x80)=faceWords;events.insert(events.end(),{2,item.id,mode,item.hit,int(item.face)});return;}std::abort();}
void originalInit(void*){}
void patch(unsigned address,void*function){auto p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
#endif
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x458360,reinterpret_cast<void*>(&originalHandle));patch(0x4E67A0,reinterpret_cast<void*>(&Utils_LineOfSight));patch(0x4549A0,reinterpret_cast<void*>(&originalLine));patch(0x4524C0,reinterpret_cast<void*>(&originalInit));
// VectorNormal's CRT sqrt boundary is replaced by the extracted identical numerical function.
patch(0x470430,reinterpret_cast<void*>(&VectorNormal));
#endif
int caseIndex=0;int kind;while(std::cin>>kind){CPlayer p;int range,cone,weight,facing,n;std::cin>>range>>cone>>weight>>facing>>p.mPos.vx>>p.mPos.vy>>p.mPos.vz;for(auto&row:p.field_89C.m)for(auto&entry:row)std::cin>>entry;std::cin>>n;items.assign(n,CItem());for(auto&item:items)std::cin>>item.id>>item.mType>>item.mRMinor>>item.mCBodyFlags>>item.mPlayerDist>>item.visible>>item.mode>>item.hit>>item.face>>item.mPos.vx>>item.mPos.vy>>item.mPos.vz>>item.first.vx>>item.first.vy>>item.first.vz>>item.second.vx>>item.second.vy>>item.second.vz;
for(int i=0;i+1<n;i++)items[i].mNextItem=&items[i+1];G_BADDY_LIST=n?&items[0]:nullptr;G_CONTROL_BADDY_LIST=G_BADDY_LIST;
events.clear();SHandle out={nullptr,99};CBody*best=nullptr;CVector*aim=nullptr;if(kind==0)best=p.SelectTargetBaddy(range,cone,weight,facing);else aim=p.SelectTargetSwitch(range,cone,&out,weight,facing);auto expected=events;
#ifdef VALIDATE_ORIGINAL
rawItems.clear();for(auto&item:items){rawItems.emplace_back(0x130,0);auto raw=rawItems.back().data();std::memcpy(raw+8,&item.mPos,12);*reinterpret_cast<u16*>(raw+0x38)=item.mType;*reinterpret_cast<u16*>(raw+0xDC)=item.mRMinor;*reinterpret_cast<u16*>(raw+0x46)=item.mCBodyFlags;*reinterpret_cast<int*>(raw+0xE4)=item.mPlayerDist;*reinterpret_cast<int*>(raw+0x100)=item.mode;std::memcpy(raw+0x10C,&item.first,12);std::memcpy(raw+0x118,&item.second,12);}
for(int i=0;i+1<n;i++)*reinterpret_cast<void**>(rawItems[i].data()+0x20)=rawItems[i+1].data();*reinterpret_cast<void**>(0x56E990)=n?rawItems[0].data():nullptr;*reinterpret_cast<void**>(0x56E994)=n?rawItems[0].data():nullptr;
alignas(4)unsigned char rawPlayer[0x8C0]={};std::memcpy(rawPlayer+8,&p.mPos,12);std::memcpy(rawPlayer+0x89C,&p.field_89C,18);events.clear();
if(kind==0){void*actual=reinterpret_cast<void*(__attribute__((thiscall)) *)(void*,int,int,int,int)>(0x4C8410)(rawPlayer,range,cone,weight,facing);int id=0;for(unsigned i=0;i<items.size();i++)if(actual==rawItems[i].data())id=items[i].id;assert(id==(best?best->id:0));}
else {unsigned originalOut[2]={0,99};void*actual=reinterpret_cast<void*(__attribute__((thiscall)) *)(void*,int,int,unsigned*,int,int)>(0x4C8570)(rawPlayer,range,cone,originalOut,weight,facing);if(!(rawPointId(actual)==pointId(aim)&&rawItemId(reinterpret_cast<void*>(originalOut[0]))==(out.pWhatever?static_cast<CItem*>(out.pWhatever)->id:0)&&originalOut[1]==out.validation)){std::cerr<<"case "<<caseIndex<<" PCaim "<<rawPointId(actual)<<" sourceaim "<<pointId(aim)<<" PChandle "<<rawItemId(reinterpret_cast<void*>(originalOut[0]))<<" sourcehandle "<<(out.pWhatever?static_cast<CItem*>(out.pWhatever)->id:0)<<"\n";std::abort();}}
assert(events==expected);events=expected;
#endif
std::cout<<'['<<(best?best->id:pointId(aim))<<','<<(out.pWhatever?static_cast<CItem*>(out.pWhatever)->id:0)<<','<<out.validation<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";caseIndex++;}}
'''
    rng=random.Random(0x4c8410);cases=[]
    matrices=[[4096,0,0,0,4096,0,0,0,4096],[0,0,4096,0,4096,0,-4096,0,0],[-4096,0,0,0,4096,0,0,0,-4096]]
    def item(identity,kind=407,radius=100,flags=0x210,distance=100,visible=1,mode=1,hit=0,face=0,pos=None):
        pos=pos or [0,0,-100*4096];return [identity,kind,radius,flags,distance,visible,mode,hit,face,*pos,*pos,pos[0]+4096,pos[1]-4096,pos[2]+4096]
    def add(kind=0,range=1024,cone=2896,weight=4096,facing=4096,items=None,matrix=None,pos=None):cases.append(dict(kind=kind,range=range,cone=cone,weight=weight,facing=facing,items=items or [],matrix=matrix or matrices[0],pos=pos or [0,0,0]))
    for kind in [0,1]:
        add(kind)
        for facing in [0,4096,-4096]:
            for matrix in matrices:
                for flags in [0,0x10,0x40,0x50,0x210]:add(kind,facing=facing,matrix=matrix,items=[item(1,flags=flags)])
        for distance in [-1,0,1023,1024,1025,-2147483648]:add(kind,items=[item(1,distance=distance)])
        for visible in [0,1]:
            for mode in [-1,0,1,2,5,6]:
                for type in [304,407,408]:add(kind,items=[item(1,kind=type,visible=visible,mode=mode)])
        for hit in [0,1]:
            for face in [0,0x02000000,0x04000000]:add(kind,items=[item(1,hit=hit,face=face)])
        add(kind,items=[item(1,distance=400,pos=[0,0,-400*4096]),item(2,distance=100,pos=[0,0,-100*4096]),item(3,distance=100,pos=[4096,0,-100*4096])])
    for _ in range(300):
        items=[item(i+1,kind=rng.choice([304,407,408]),radius=rng.choice([0,100]),flags=rng.choice([0,0x10,0x50,0x210]),distance=rng.randint(-1000000,2000),visible=rng.randrange(2),mode=rng.randint(0,6),hit=rng.randrange(2),face=rng.choice([0,0x02000000]),pos=[rng.randint(-2000,2000)*4096 for _ in range(3)]) for i in range(rng.randrange(8))]
        add(rng.randrange(2),range=rng.choice([256,1024,2048]),cone=rng.choice([-4096,0,2896,4096]),weight=rng.choice([-4096,0,4096,32767]),facing=rng.choice([-4096,0,4096]),items=items,matrix=rng.choice(matrices))
    rows=[' '.join(map(str,[c[k] for k in ['kind','range','cone','weight','facing']]+c['pos']+c['matrix']+[len(c['items'])]+[v for item in c['items'] for v in item])) for c in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-player-target-') as folder:
        cpp=Path(folder)/'reference.cpp';exe=Path(folder)/'reference';cpp.write_text(code)
        flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(exe)],check=True)
        cmd=[str(exe)]+([str(args.original_exe.resolve())] if args.original_exe else [])
        lines=subprocess.check_output(cmd,input=('\n'.join(rows)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-targets.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} source target cases; selectors/GTE/switch mode helper verified against original PC when requested.')

if __name__=='__main__':main()
