"""Compile actual source six-slot sense rebuild/refresh, validating PC0x4C5250/0x4C5130."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path);args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <cmath>
#include <cstring>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
struct VECTOR{int vx,vy,vz,pad;};struct MATRIX{short m[3][3];};MATRIX*rotation;VECTOR loaded,rotated;
'''
    rendering=(root/'src/engine/rendering/ps2funcs.cpp').read_text()
    code+=controller_method(rendering,'EXPORT void  FixedXForm(').replace('EXPORT ','')+'\n'+controller_method(rendering,'void VectorNormal(')+'\n'
    code+=r'''
void gte_SetRotMatrix(MATRIX*p){rotation=p;}void gte_ldlvl(VECTOR*p){loaded=*p;}void gte_rtir(){FixedXForm(rotation->m,&loaded,&rotated);}void gte_stlvnl(VECTOR*p){*p=rotated;}
struct CBaddy{int id;u16 mRMinor,mCBodyFlags,mFlags;u32 mPlayerDist,field_2A8;u8 field_310;CVector mPos;CBaddy*mNextItem=nullptr;unsigned generation;int recoverable,clearMissing;};
struct SHandle{void*pWhatever=nullptr;unsigned Id=0;};struct SIndicator{CVector mDirection;SHandle field_C;int mInUse=0;};
std::vector<CBaddy>items;CBaddy*G_BADDY_LIST;std::vector<int>events;
int G_TIMER_RELATED;u32 rebuildClock,refreshClock;u32*gSpideySenseListLastUpdateTime=&rebuildClock,*gSpideySenseIndicatorLastUpdateTime=&refreshClock;CVector reference;CVector*stru_56F1B4=&reference;MATRIX referenceRotation;MATRIX*stru_56F224=&referenceRotation;
SHandle Mem_MakeHandle(CBaddy*b){events.insert(events.end(),{1,b->id,int(b->generation)});return {b,b->generation};}
void*Mem_RecoverPointer(SHandle*h){auto b=static_cast<CBaddy*>(h->pWhatever);events.insert(events.end(),{2,b->id,int(h->Id),b->recoverable,b->clearMissing});if(!b->recoverable){if(b->clearMissing)h->pWhatever=nullptr;return nullptr;}return b;}
struct CPlayer{int field_528;u32 field_8BC,field_8C0;int field_EC0;u8 field_354;SIndicator field_5F0[6];int GetFreeIndicatorListEntry();void BuildOffscreenSpideySenseIndicatorList();void UpdateOffscreenSpideySenseIndicatorList();};
'''
    source=(root/'src/game/player/spidey.cpp').read_text()
    for signature in ['i32 CPlayer::GetFreeIndicatorListEntry(','void CPlayer::BuildOffscreenSpideySenseIndicatorList(','void CPlayer::UpdateOffscreenSpideySenseIndicatorList(']:code+=controller_method(source,signature).replace('rotated.pad = 0;', 'rotated.vz = 0;')+'\n'
    code+=r'''
#ifdef VALIDATE_ORIGINAL
std::vector<std::vector<unsigned char>>rawItems;CBaddy*sourceBody(void*p){for(unsigned i=0;i<items.size();i++)if(p==rawItems[i].data())return &items[i];return nullptr;}
void*rawBody(CBaddy*b){if(!b)return nullptr;return rawItems[b-items.data()].data();}
unsigned long long originalMake(void*p){auto b=sourceBody(p);events.insert(events.end(),{1,b->id,int(b->generation)});return static_cast<unsigned long long>(b->generation)<<32|reinterpret_cast<unsigned>(p);}
void*originalRecover(unsigned*h){auto b=sourceBody(reinterpret_cast<void*>(h[0]));events.insert(events.end(),{2,b->id,int(h[1]),b->recoverable,b->clearMissing});if(!b->recoverable){if(b->clearMissing)h[0]=0;return nullptr;}return reinterpret_cast<void*>(h[0]);}
void patch(unsigned address,void*function){auto p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
#endif
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x458360,reinterpret_cast<void*>(&originalMake));patch(0x4583F0,reinterpret_cast<void*>(&originalRecover));patch(0x470430,reinterpret_cast<void*>(&VectorNormal));
#endif
unsigned start;while(std::cin>>start){CPlayer p;int n,preload;std::cin>>rebuildClock>>refreshClock>>p.field_528>>p.field_8BC>>p.field_8C0>>p.field_EC0;int encounter;std::cin>>encounter>>reference.vx>>reference.vy>>reference.vz;p.field_354=encounter;for(auto&row:referenceRotation.m)for(auto&entry:row)std::cin>>entry;std::cin>>n>>preload;items.assign(n,CBaddy());for(auto&b:items)std::cin>>b.id>>b.mRMinor>>b.mCBodyFlags>>b.mFlags>>b.mPlayerDist>>b.field_2A8>>encounter>>b.generation>>b.recoverable>>b.clearMissing>>b.mPos.vx>>b.mPos.vy>>b.mPos.vz,b.field_310=encounter;
for(int i=0;i+1<n;i++)items[i].mNextItem=&items[i+1];G_BADDY_LIST=n?&items[0]:nullptr;for(int i=0;i<6;i++){auto&entry=p.field_5F0[i];entry.mDirection=CVector(i+1,i+2,i+3);entry.field_C={preload&&n?&items[i%n]:nullptr,unsigned(preload&&n?items[i%n].generation:70+i)};entry.mInUse=100+i;}
#ifdef VALIDATE_ORIGINAL
rawItems.clear();for(auto&b:items){rawItems.emplace_back(0x320,0);auto raw=rawItems.back().data();std::memcpy(raw+8,&b.mPos,12);*reinterpret_cast<u16*>(raw+0xDC)=b.mRMinor;*reinterpret_cast<u16*>(raw+0x46)=b.mCBodyFlags;*reinterpret_cast<u16*>(raw+4)=b.mFlags;*reinterpret_cast<u32*>(raw+0xE4)=b.mPlayerDist;*reinterpret_cast<u32*>(raw+0x2A8)=b.field_2A8;raw[0x310]=b.field_310;}
for(int i=0;i+1<n;i++)*reinterpret_cast<void**>(rawItems[i].data()+0x20)=rawItems[i+1].data();*reinterpret_cast<void**>(0x56E990)=n?rawItems[0].data():nullptr;
alignas(4)unsigned char rawPlayer[0xED0]={};*reinterpret_cast<int*>(rawPlayer+0x528)=p.field_528;*reinterpret_cast<u32*>(rawPlayer+0x8BC)=p.field_8BC;*reinterpret_cast<u32*>(rawPlayer+0x8C0)=p.field_8C0;*reinterpret_cast<int*>(rawPlayer+0xEC0)=p.field_EC0;rawPlayer[0x354]=p.field_354;
for(int i=0;i<6;i++){auto raw=rawPlayer+0x5F0+104*i;auto&e=p.field_5F0[i];std::memcpy(raw,&e.mDirection,12);*reinterpret_cast<void**>(raw+12)=rawBody(static_cast<CBaddy*>(e.field_C.pWhatever));*reinterpret_cast<unsigned*>(raw+16)=e.field_C.Id;*reinterpret_cast<int*>(raw+100)=e.mInUse;}
*reinterpret_cast<unsigned*>(0x6A9084)=rebuildClock;*reinterpret_cast<unsigned*>(0x6A9080)=refreshClock;std::memcpy(reinterpret_cast<void*>(0x56F1B4),&reference,12);std::memcpy(reinterpret_cast<void*>(0x56F224),&referenceRotation,18);
#endif
unsigned deltas[]={0,2,3,4,20,21,23,24};std::cout<<'[';for(int frame=0;frame<8;frame++){G_TIMER_RELATED=start+deltas[frame];events.clear();p.UpdateOffscreenSpideySenseIndicatorList();p.BuildOffscreenSpideySenseIndicatorList();auto expected=events;
#ifdef VALIDATE_ORIGINAL
*reinterpret_cast<int*>(0x6B4CA8)=G_TIMER_RELATED;events.clear();reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4C5130)(rawPlayer);reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4C5250)(rawPlayer);
assert(events==expected&&*reinterpret_cast<unsigned*>(0x6A9084)==rebuildClock&&*reinterpret_cast<unsigned*>(0x6A9080)==refreshClock&&*reinterpret_cast<int*>(rawPlayer+0x528)==p.field_528&&*reinterpret_cast<unsigned*>(rawPlayer+0x8BC)==p.field_8BC&&*reinterpret_cast<unsigned*>(rawPlayer+0x8C0)==p.field_8C0&&*reinterpret_cast<int*>(rawPlayer+0xEC0)==p.field_EC0&&rawPlayer[0x354]==p.field_354);
for(int i=0;i<6;i++){auto raw=rawPlayer+0x5F0+104*i;auto&e=p.field_5F0[i];if(!(!std::memcmp(raw,&e.mDirection,12)&&*reinterpret_cast<void**>(raw+12)==rawBody(static_cast<CBaddy*>(e.field_C.pWhatever))&&*reinterpret_cast<unsigned*>(raw+16)==e.field_C.Id&&*reinterpret_cast<int*>(raw+100)==e.mInUse)){std::cerr<<"start "<<start<<" frame "<<frame<<" slot "<<i<<" actual "<<*reinterpret_cast<int*>(raw)<<","<<*reinterpret_cast<int*>(raw+4)<<","<<*reinterpret_cast<int*>(raw+8)<<" expected "<<e.mDirection.vx<<","<<e.mDirection.vy<<","<<e.mDirection.vz<<" ages "<<*reinterpret_cast<int*>(raw+100)<<","<<e.mInUse<<"\n";std::abort();}}events=expected;
#endif
if(frame)std::cout<<',';std::cout<<'['<<rebuildClock<<','<<refreshClock<<','<<p.field_528<<','<<p.field_8BC<<','<<p.field_8C0<<','<<p.field_EC0<<','<<int(p.field_354)<<",[";for(int i=0;i<6;i++){if(i)std::cout<<',';auto&e=p.field_5F0[i];auto*b=static_cast<CBaddy*>(e.field_C.pWhatever);std::cout<<'['<<e.mDirection.vx<<','<<e.mDirection.vy<<','<<e.mDirection.vz<<','<<(b?b->id:0)<<','<<e.field_C.Id<<','<<e.mInUse<<']';}std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}std::cout<<"]\n";}}
'''
    rng=random.Random(0x4c5250);cases=[];matrix=[4096,0,0,0,4096,0,0,0,4096]
    def body(identity,radius=100,flags=0x210,itemFlags=0x8000,distance=100,behavior=0x20,show=1,generation=None,alive=1,clear=0):return [identity,radius,flags,itemFlags,distance,behavior,show,generation or identity+1000,alive,clear,identity*8192,identity*-4096,identity*12288]
    def add(start=100,previous=0,refresh=0,preload=0,bodies=None):cases.append(dict(start=start,previous=previous,refresh=refresh,count=37,maximum=41,minimum=43,has=47,encounter=1,reference=[5,-7,11],matrix=matrix,preload=preload,bodies=bodies or []))
    for start in [0,2,3,4,19,20,21,100,0xffffffef,0xffffffff]:
        for previous in [0,start,(start-20)&0xffffffff,(start-21)&0xffffffff,(start+1)&0xffffffff]:add(start,previous,previous,bodies=[body(1),body(2)])
    for flags in [0,0x10,0x40,0x200,0x210,0x250]:
        for show in [0,1]:
            for radius in [0,100]:add(bodies=[body(1,radius=radius,flags=flags,show=show)])
    for preload in [0,1]:
        for count in [1,6,7,10]:
            for alive in [0,1]:
                for clear in [0,1]:add(preload=preload,bodies=[body(i+1,alive=alive,clear=clear) for i in range(count)])
    add(bodies=[body(i+1,generation=77) for i in range(10)])
    for _ in range(150):add(start=rng.randrange(2**32),previous=rng.randrange(2**32),refresh=rng.randrange(2**32),preload=rng.randrange(2),bodies=[body(i+1,radius=rng.choice([0,100]),flags=rng.choice([0,0x10,0x200,0x210,0x250]),itemFlags=rng.choice([0,0x8000]),distance=rng.randrange(2**32),behavior=rng.choice([0,0x20]),show=rng.randrange(2),alive=rng.randrange(2),clear=rng.randrange(2)) for i in range(rng.randrange(12))])
    rows=[' '.join(map(str,[c[k] for k in ['start','previous','refresh','count','maximum','minimum','has','encounter']]+c['reference']+c['matrix']+[len(c['bodies']),c['preload']]+[v for b in c['bodies'] for v in b])) for c in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-sense-') as folder:
        cpp=Path(folder)/'reference.cpp';exe=Path(folder)/'reference';cpp.write_text(code);flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(exe)],check=True)
        command=[str(exe)]+([str(args.original_exe.resolve())] if args.original_exe else []);lines=subprocess.check_output(command,input=('\n'.join(rows)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-sense.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} eight-update sense cases; original PC rebuild/refresh verified when requested.')

if __name__=='__main__':main()
