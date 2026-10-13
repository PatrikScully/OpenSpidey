"""Compile actual CPlayer::CheckKick with explicit source world/handle/animation/combo boundaries."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument("--original-exe", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "core/fixed_vector.h"
#include "core/distance.h"
#include <iostream>
#include <vector>
#include <cstring>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
struct CBody {int id=0;u16 mType=0;CVector mPos;int field_10C=0;CBody*mNextItem=nullptr;int hit=0,distance=0;};
using CItem=CBody;using CManipOb=CBody;
struct SHandle {CBody*pWhatever=nullptr;unsigned validation=0;};
struct SLineInfo {CVector StartCoords,EndCoords,MinCoords,MaxCoords,Position,Normal;CItem*pItem=nullptr;int Distance=0;};
std::vector<int>events;std::vector<CBody> baddies,objects;CBody *G_BADDY_LIST,*G_ENVIRONMENTAL_OBJECT_LIST;
int G_TIMER_RELATED;int *G_SPIDEY_SFX_ENTRY[256];int sounds[3];int switchId;
CBody*get(int id){for(auto&item:baddies)if(item.id==id)return &item;for(auto&item:objects)if(item.id==id)return &item;return nullptr;}
SHandle Mem_MakeHandle(CBody*p){events.insert(events.end(),{1,p?p->id:0});return {p,unsigned(p?p->id+1000:0)};}
void M3dColij_InitLineInfo(SLineInfo*){}
void M3dZone_LineToItem(SLineInfo*l,int mode){for(auto&item:objects)if(!std::memcmp(&item.mPos,&l->EndCoords,12)){l->pItem=get(item.hit);l->Distance=item.distance;events.insert(events.end(),{2,l->EndCoords.vx,l->EndCoords.vy,l->EndCoords.vz,mode,item.hit,item.distance});return;}std::abort();}
struct CPlayer {
 unsigned char input[320]={};void*field_E0C=input;u8 mIsCrawling=0,field_8EA=0,field_2E1=0,field_2F1=0;
 CBody*mHeldObject=nullptr,*field_DCC=nullptr;unsigned mActionState;int*field_350=nullptr;
 CVector mPos,mForwardDirection;SHandle field_E5C,field_E4C,field_E54;int mHeadingTurnTicksRemaining,field_E20,field_898;
 i32 CheckKick();
 void RunAnim(int animation,int first,int last){events.insert(events.end(),{5,animation,first,last,int(mActionState)});}
 void SetTargetTorsoAngleToThisPoint(CVector*p){events.insert(events.end(),{6,p->vx,p->vy,p->vz});}
 CVector* SelectTargetSwitch(int range,int facing,SHandle*out,int weight,int facingWeight){events.insert(events.end(),{3,range,facing,weight,facingWeight});*out={get(switchId),unsigned(switchId?switchId+1000:0)};return get(switchId)?&get(switchId)->mPos:nullptr;}
 void InitiateCombo(int move,int first){events.insert(events.end(),{7,move,first,int(mActionState),field_898,mHeadingTurnTicksRemaining});}
};
'''
    method = controller_method((root/'src/game/player/spidey.cpp').read_text(), 'i32 CPlayer::CheckKick(')
    # Original PC0x4C0454 tests returned auto-aim pointer, unlike recovered handle gate.
    method = method.replace('this->SelectTargetSwitch(256, 2896, &this->field_E54, 4096, 4096);', 'CVector* switchPoint = this->SelectTargetSwitch(256, 2896, &this->field_E54, 4096, 4096);').replace('if (this->field_E54.pWhatever != 0)', 'if (switchPoint != 0)')
    code += method
    code += r'''

#ifdef VALIDATE_ORIGINAL
std::vector<std::vector<unsigned char>> originalBodies;int originalSounds[3];
void*originalBody(int id){for(auto&raw:originalBodies)if(*reinterpret_cast<int*>(raw.data())==id)return raw.data();return nullptr;}
int originalId(void*raw){return raw?*reinterpret_cast<int*>(raw):0;}
unsigned long long originalHandle(void*raw){int id=originalId(raw);events.insert(events.end(),{1,id});return static_cast<unsigned long long>(id?id+1000:0)<<32|reinterpret_cast<unsigned>(raw);}
void __attribute__((thiscall)) originalAnimation(void*self,int animation,int first,int last){events.insert(events.end(),{5,animation,first,last,*reinterpret_cast<int*>(static_cast<unsigned char*>(self)+0xE1C)});}
void __attribute__((thiscall)) originalTorso(void*,CVector*point){events.insert(events.end(),{6,point->vx,point->vy,point->vz});}
void __attribute__((thiscall)) originalCombo(void*self,int move,int first){auto raw=static_cast<unsigned char*>(self);events.insert(events.end(),{7,move,first,*reinterpret_cast<int*>(raw+0xE1C),*reinterpret_cast<int*>(raw+0x898),*reinterpret_cast<int*>(raw+0xDF8)});}
CVector* __attribute__((thiscall)) originalSwitch(void*,int range,int facing,unsigned*out,int weight,int facingWeight){events.insert(events.end(),{3,range,facing,weight,facingWeight});out[0]=reinterpret_cast<unsigned>(originalBody(switchId));out[1]=switchId?switchId+1000:0;return out[0]?reinterpret_cast<CVector*>(out[0]+8):nullptr;}
void originalLine(void*value,int mode){auto raw=static_cast<unsigned char*>(value);CVector&end=*reinterpret_cast<CVector*>(raw+12);for(auto&item:objects)if(!std::memcmp(&item.mPos,&end,12)){*reinterpret_cast<void**>(raw+0x68)=originalBody(item.hit);*reinterpret_cast<int*>(raw+0x40)=item.distance;events.insert(events.end(),{2,end.vx,end.vy,end.vz,mode,item.hit,item.distance});return;}std::abort();}
void originalInit(void*){}
void patch(unsigned address,void*function){auto p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
#endif
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x458360,reinterpret_cast<void*>(&originalHandle));patch(0x460C10,reinterpret_cast<void*>(&originalAnimation));patch(0x4C68A0,reinterpret_cast<void*>(&originalTorso));patch(0x4C87D0,reinterpret_cast<void*>(&originalCombo));patch(0x4C8570,reinterpret_cast<void*>(&originalSwitch));patch(0x4524C0,reinterpret_cast<void*>(&originalInit));patch(0x4549A0,reinterpret_cast<void*>(&originalLine));
#endif
int kick;while(std::cin>>kick){CPlayer p;int punch,crawl,surface,held,torso,mirrork,mirrorp,nb,no;
std::cin>>punch>>crawl>>surface>>held>>torso>>mirrork>>mirrorp>>p.mActionState>>p.mHeadingTurnTicksRemaining>>p.field_E20>>p.field_898>>G_TIMER_RELATED>>switchId>>p.mPos.vx>>p.mPos.vy>>p.mPos.vz>>p.mForwardDirection.vx>>p.mForwardDirection.vy>>p.mForwardDirection.vz>>nb>>no;
baddies.assign(nb,CBody());objects.assign(no,CBody());for(auto*list:{&baddies,&objects})for(auto&item:*list)std::cin>>item.id>>item.mType>>item.field_10C>>item.mPos.vx>>item.mPos.vy>>item.mPos.vz>>item.hit>>item.distance;
for(auto*list:{&baddies,&objects})for(unsigned i=0;i+1<list->size();i++)(*list)[i].mNextItem=&(*list)[i+1];
G_BADDY_LIST=nb?&baddies[0]:nullptr;G_ENVIRONMENTAL_OBJECT_LIST=no?&objects[0]:nullptr;
p.input[289]=kick;p.input[305]=punch;p.mIsCrawling=crawl;p.field_8EA=surface;p.field_2E1=mirrork;p.field_2F1=mirrorp;p.mHeldObject=get(held);p.field_DCC=get(torso);
p.field_E5C={get(1),101};p.field_E4C={get(2),102};p.field_E54={get(3),103};
sounds[0]=int(0xAAAB0023);sounds[1]=int(0xCCDDFFFF);sounds[2]=-1;for(auto&table:G_SPIDEY_SFX_ENTRY)table=sounds;

#ifdef VALIDATE_ORIGINAL
originalBodies.clear();for(auto*list:{&baddies,&objects})for(auto&item:*list){originalBodies.emplace_back(0x110,0);auto raw=originalBodies.back().data();*reinterpret_cast<int*>(raw)=item.id;std::memcpy(raw+8,&item.mPos,12);*reinterpret_cast<u16*>(raw+0x38)=item.mType;*reinterpret_cast<int*>(raw+0x10C)=item.field_10C;}
for(auto*list:{&baddies,&objects})for(unsigned i=0;i+1<list->size();i++)*reinterpret_cast<void**>(static_cast<unsigned char*>(originalBody((*list)[i].id))+0x20)=originalBody((*list)[i+1].id);
alignas(4)unsigned char raw[0xED0]={};unsigned char originalInput[320];std::memcpy(originalInput,p.input,320);std::memcpy(raw+8,&p.mPos,12);std::memcpy(raw+0xC6C,&p.mForwardDirection,12);*reinterpret_cast<void**>(raw+0xE0C)=originalInput;raw[0xAD4]=crawl;raw[0x8EA]=surface;raw[0x2E1]=mirrork;raw[0x2F1]=mirrorp;
*reinterpret_cast<void**>(raw+0xE48)=originalBody(held);*reinterpret_cast<void**>(raw+0xDCC)=originalBody(torso);*reinterpret_cast<unsigned*>(raw+0xE1C)=p.mActionState;*reinterpret_cast<int*>(raw+0xDF8)=p.mHeadingTurnTicksRemaining;*reinterpret_cast<int*>(raw+0xE20)=p.field_E20;*reinterpret_cast<int*>(raw+0x898)=p.field_898;
int handleOffsets[]={0xE5C,0xE4C,0xE54};for(int i=0;i<3;i++){*reinterpret_cast<void**>(raw+handleOffsets[i])=originalBody(i+1);*reinterpret_cast<unsigned*>(raw+handleOffsets[i]+4)=101+i;}
*reinterpret_cast<void**>(0x56E990)=nb?originalBody(baddies[0].id):nullptr;*reinterpret_cast<void**>(0x60DAAC)=no?originalBody(objects[0].id):nullptr;*reinterpret_cast<int*>(0x6B4CA8)=G_TIMER_RELATED;
originalSounds[0]=int(0xAAAB0023);originalSounds[1]=int(0xCCDDFFFF);originalSounds[2]=-1;for(unsigned i=0;i<256;i++)reinterpret_cast<int**>(0x6A82B8)[i]=originalSounds;
#endif
events.clear();int result=p.CheckKick();
#ifdef VALIDATE_ORIGINAL
auto expectedEvents=events;events.clear();int actual=reinterpret_cast<int(__attribute__((thiscall)) *)(void*)>(0x4C00B0)(raw);
assert(actual==result&&events==expectedEvents&&originalInput[289]==p.input[289]&&originalInput[305]==p.input[305]&&raw[0x2E1]==p.field_2E1&&raw[0x2F1]==p.field_2F1);
assert(*reinterpret_cast<unsigned*>(raw+0xE1C)==p.mActionState&&*reinterpret_cast<int*>(raw+0xDF8)==p.mHeadingTurnTicksRemaining&&*reinterpret_cast<int*>(raw+0xE20)==p.field_E20&&*reinterpret_cast<int*>(raw+0x898)==p.field_898);
SHandle*expectedHandles[]={&p.field_E5C,&p.field_E4C,&p.field_E54};for(int i=0;i<3;i++){assert(originalId(*reinterpret_cast<void**>(raw+handleOffsets[i]))==(expectedHandles[i]->pWhatever?expectedHandles[i]->pWhatever->id:0));assert(*reinterpret_cast<unsigned*>(raw+handleOffsets[i]+4)==expectedHandles[i]->validation);}
assert(!std::memcmp(sounds,originalSounds,sizeof(sounds))&&bool(*reinterpret_cast<void**>(raw+0x350))==bool(p.field_350));events=expectedEvents;
#endif
std::cout<<'['<<result<<','<<int(p.input[289])<<','<<int(p.input[305])<<','<<int(p.field_2E1)<<','<<int(p.field_2F1)<<','<<p.mActionState<<','<<p.mHeadingTurnTicksRemaining<<','<<p.field_E20<<','<<p.field_898;
for(auto*h:{&p.field_E5C,&p.field_E4C,&p.field_E54})std::cout<<','<<(h->pWhatever?h->pWhatever->id:0)<<','<<h->validation;
std::cout<<','<<(p.field_350?1:0)<<','<<sounds[0]<<','<<sounds[1]<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    rng = random.Random(0x4C00B0)
    cases = []
    def case(**updates):
        item=dict(kick=1,punch=0,crawl=0,surface=0,held=0,torso=0,mirrork=7,mirrorp=9,action=123,heading=17,progress=31,entry=-11,clock=0xf1234567,switch=0,pos=[0,0,0],forward=[0,0,-4096],baddies=[],objects=[])
        item.update(updates);cases.append(item)
    def body(identity,kind=304,flags=0,pos=None,hit=0,distance=0):
        return [identity,kind,flags,*(pos if pos is not None else [0,0,100*4096]),hit,distance]
    for kick,punch in [(0,0),(1,0),(0,1),(1,1),(255,2)]:
        for crawl,surface in [(0,0),(1,0),(0,1),(1,1)]:case(kick=kick,punch=punch,crawl=crawl,surface=surface)
    for punch in [0,1]:
        for flags in [0,8,-1]:
            for torso in [0,2]:case(punch=punch,held=1,torso=torso,baddies=[body(1,401,flags),body(2)])
    for kind in [304,305,306,312,315,316,320,324,401,0xffff]:
        for distance in [511,512,513]:case(baddies=[body(1,kind,pos=[0,0,distance*4096])],switch=2,objects=[body(2,400)])
    case(baddies=[body(1,pos=[0,0,300*4096]),body(2,pos=[0,0,100*4096]),body(3,pos=[0,0,100*4096])])
    for punch in [0,1]:
        for distance in [-1,255,256,257]:
            for flags in [0,8]:
                for hit in [0,1,2]:case(punch=punch,objects=[body(1,401,flags,hit=hit,distance=distance),body(2,400)],switch=2)
    for gap in [0,767,768,769]:case(objects=[body(1,401,pos=[0,0,gap*4096],hit=1,distance=100)])
    case(objects=[body(1,401,hit=1,distance=257),body(2,401,pos=[0,0,120*4096],hit=2,distance=256)])
    case(objects=[body(1,401,hit=1,distance=256),body(2,401,8,pos=[0,0,120*4096],hit=2,distance=0)])
    for _ in range(256):
        b=[body(i+1,rng.choice([304,305,315,316,401]),rng.randrange(16),[rng.randint(-2**31,2**31-1) for _ in range(3)]) for i in range(rng.randrange(5))]
        o=[body(i+10,rng.choice([400,401]),rng.randrange(16),[rng.randint(-2**31,2**31-1) for _ in range(3)],rng.choice([0,i+10]),rng.randint(-20,300)) for i in range(rng.randrange(4))]
        case(kick=rng.randrange(2),punch=rng.randrange(2),baddies=b,objects=o,held=rng.choice([0]+[x[0] for x in b]),torso=rng.choice([0]+[x[0] for x in o]),switch=rng.choice([0]+[x[0] for x in o]),pos=[rng.randint(-2**31,2**31-1) for _ in range(3)],forward=[rng.randint(-2**31,2**31-1) for _ in range(3)])
    rows=[]
    for c in cases:
        row=[c[k] for k in ['kick','punch','crawl','surface','held','torso','mirrork','mirrorp','action','heading','progress','entry','clock','switch']]+c['pos']+c['forward']+[len(c['baddies']),len(c['objects'])]
        row += [value for b in c['baddies']+c['objects'] for value in b]
        rows.append(' '.join(map(str,row)))
    # Source clock storage is signed, as in G_TIMER_RELATED's original i32 declaration.
    rows=[row.replace(str(0xf1234567),str(0xf1234567-2**32)) for row in rows]
    with tempfile.TemporaryDirectory(prefix='openspidey-kick-') as directory:
        cpp=Path(directory)/'reference.cpp';exe=Path(directory)/'reference';cpp.write_text(code)
        flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(exe)],check=True)
        command=[str(exe)]+([str(args.original_exe.resolve())] if args.original_exe else [])
        lines=subprocess.check_output(command,input=('\n'.join(rows)+'\n').encode()).decode().splitlines()
    output=[dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/player-kick.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Generated {len(output)} compiled-source CheckKick cases; world/switch/animation/combo boundaries are explicit; original PC verified when requested.')

if __name__=='__main__':main()
