"""Extract real CSwitch constructor/AI/Flick and prove PC behavior over explicit trigger/ENV boundaries."""
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
#include <vector>
#include <iostream>
#include <cstring>
#include <cstdint>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
using VECTOR=Vector;
std::vector<int>events;void print_if_false(int condition,const char*){if(!condition)events.push_back(4);}
struct CItem{int id;CVector mPos;u16 mFlags=0;u32 mRGB=0;};CItem offItem,onItem;
struct Player{int field_568;};Player player;Player*G_MECHLIST_PLAYER;
struct PulseNode{u16 type;CVector position;};std::vector<PulseNode>nodes;int G_NUMNODES;std::vector<u16*>offsets;u16**G_OFFSETLIST;
void Trig_GetPosition(CVector*p,int index){*p=nodes[index].position;}void Trig_SendPulseToNode(int index){events.insert(events.end(),{2,index});}
u16 links[2];u16*Trig_GetLinksPointer(int node){links[0]=node;return links;}void Trig_SendPulse(u16*p){events.insert(events.end(),{1,p[0]});}
CItem*Spool_FindEnviroItem(u32 checksum){events.insert(events.end(),{5,int(checksum)});return checksum==0x12345678?&offItem:checksum==0x89ABCDEF?&onItem:nullptr;}
void Switch_SetSwitchFaceFlags(CItem*p){events.insert(events.end(),{6,p->id});}
void Switch_SetVisible(bool,CItem*);
struct CBody{CVector mPos;CSVector mAngles;u16 mCBodyFlags=0x16,mType=0,mRMinor=128,mNode=65535,mInputFlags=0;int mElapsedTicks=2;u32 mRGB=0;i16*SquirtPos(i16*);i16*SquirtAngles(i16*);void AttachTo(void*){events.push_back(7);}void Die(){mCBodyFlags|=0x40;}};
int G_CONTROL_BADDY_LIST;
struct CSwitch:CBody{u32 field_F8=0;u16 field_FC=0;int field_100=0;CItem*field_104=nullptr,*field_108=nullptr;CVector field_10C,field_118;u8 field_124=0;CSwitch(){}CSwitch(i16*,int);void AI();void Flick();void PulseLFA1Node(int);void SwitchOn();void SwitchOff();void SwitchInactive();void SignalAttachedItems();CVector*GetAutoAimTargetPointer();};
'''
    source=(root/'src/game/objects/switch.cpp').read_text();body=(root/'src/engine/world/ob.cpp').read_text()
    for signature in ['i16* CBody::SquirtPos(','i16* CBody::SquirtAngles(']:
        method=controller_method(body,signature).replace('ASSERT(((i32)walker & 3) == 0, "Bad alignment");','');code+=method+'\n'
    signatures=['void Switch_SetVisible(',' void CSwitch::SwitchOn(','void CSwitch::SwitchOff(','void CSwitch::SwitchInactive(',' void CSwitch::SignalAttachedItems(','void CSwitch::PulseLFA1Node(','CSwitch::CSwitch(','void CSwitch::AI(','void CSwitch::Flick(','CVector* CSwitch::GetAutoAimTargetPointer(']
    for signature in signatures:
        method=controller_method(source,signature).replace('reinterpret_cast<i32>(pCursor)','reinterpret_cast<uintptr_t>(pCursor)').replace('this->mRGB = res;','this->field_108->mRGB = res;').replace('reinterpret_cast<CBody*>(this->field_108)->Die();','this->Die();');code+=method+'\n'
    code+=r'''
#ifdef VALIDATE_ORIGINAL
void patch(unsigned address,void*function){auto p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
#endif
void emitPosition(const CVector&p){std::cout<<'['<<p.vx<<','<<p.vy<<','<<p.vz<<']';}
void emit(CSwitch&s){auto aim=s.GetAutoAimTargetPointer();std::cout<<'['<<s.field_100<<','<<s.field_F8<<','<<s.field_FC<<','<<s.mInputFlags<<','<<s.mCBodyFlags<<','<<s.mRGB<<','<<int(s.field_124)<<','<<player.field_568<<','<<offItem.mFlags<<','<<offItem.mRGB<<','<<onItem.mFlags<<','<<onItem.mRGB<<','<<(aim==&s.field_10C?1:aim==&s.field_118?2:0)<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"],";emitPosition(aim?*aim:CVector(0,0,0));std::cout<<',';emitPosition(offItem.mPos);std::cout<<',';emitPosition(onItem.mPos);std::cout<<']';}
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4015B0,reinterpret_cast<void*>(&print_if_false));patch(0x4E3880,reinterpret_cast<void*>(&Trig_GetLinksPointer));patch(0x4DFD30,reinterpret_cast<void*>(&Trig_SendPulse));
// This host boundary runs the actual extracted coordinate-coded LFA1 search over recorded source nodes.
patch(0x4D1C60,reinterpret_cast<void*>((void(__attribute__((thiscall)) *)(CSwitch*,int))nullptr));
#endif
int kind;while(std::cin>>kind){CSwitch s;int hasPlayer,sub,mode,counter,nodeCount;std::cin>>mode>>s.field_F8>>s.field_FC>>s.mInputFlags>>s.mCBodyFlags>>sub>>counter>>hasPlayer>>offItem.mFlags>>offItem.mRGB>>onItem.mFlags>>onItem.mRGB>>nodeCount;s.field_100=mode;s.field_124=sub;player.field_568=counter;G_MECHLIST_PLAYER=hasPlayer?&player:nullptr;s.field_104=&offItem;s.field_108=&onItem;s.mNode=23;offItem.id=1;onItem.id=2;offItem.mPos=CVector(100,200,300);onItem.mPos=CVector(400,500,600);s.field_10C=offItem.mPos;s.field_118=onItem.mPos;
nodes.assign(nodeCount,PulseNode());offsets.clear();for(auto&node:nodes){std::cin>>node.type>>node.position.vx>>node.position.vy>>node.position.vz;offsets.push_back(&node.type);}G_NUMNODES=nodeCount;G_OFFSETLIST=offsets.data();
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0x128]={},rawOff[0x40]={},rawOn[0x40]={},rawPlayer[0x56C]={};*reinterpret_cast<unsigned*>(raw)=0x53C51C;*reinterpret_cast<int*>(raw+0x100)=mode;*reinterpret_cast<u32*>(raw+0xF8)=s.field_F8;*reinterpret_cast<u16*>(raw+0xFC)=s.field_FC;*reinterpret_cast<u16*>(raw+0x44)=s.mInputFlags;*reinterpret_cast<u16*>(raw+0x46)=s.mCBodyFlags;raw[0x124]=sub;*reinterpret_cast<u16*>(raw+0xDE)=23;*reinterpret_cast<void**>(raw+0x104)=rawOff;*reinterpret_cast<void**>(raw+0x108)=rawOn;std::memcpy(raw+0x10C,&s.field_10C,12);std::memcpy(raw+0x118,&s.field_118,12);*reinterpret_cast<u16*>(rawOff+4)=offItem.mFlags;*reinterpret_cast<u32*>(rawOff+0x24)=offItem.mRGB;*reinterpret_cast<u16*>(rawOn+4)=onItem.mFlags;*reinterpret_cast<u32*>(rawOn+0x24)=onItem.mRGB;*reinterpret_cast<int*>(rawPlayer+0x568)=counter;*reinterpret_cast<void**>(0x6A9038)=hasPlayer?rawPlayer:nullptr;
#endif
if(kind==1){int initial;std::cin>>initial;alignas(4)unsigned char script[40]={};*reinterpret_cast<int*>(script)=11;*reinterpret_cast<int*>(script+4)=-13;*reinterpret_cast<int*>(script+8)=17;*reinterpret_cast<short*>(script+12)=19;*reinterpret_cast<short*>(script+14)=23;*reinterpret_cast<short*>(script+16)=-29;*reinterpret_cast<u32*>(script+20)=0x12345678;*reinterpret_cast<u32*>(script+24)=0x89ABCDEF;*reinterpret_cast<short*>(script+28)=initial;*reinterpret_cast<u16*>(script+30)=s.field_FC;events.clear();CSwitch created(reinterpret_cast<i16*>(script),23);emit(created);std::cout<<'\n';continue;}
std::cout<<'[';for(int frame=0;frame<12;frame++){int op,ticks,input;std::cin>>op>>ticks>>input;s.mElapsedTicks=ticks;s.mInputFlags|=input;events.clear();if(op==0)s.AI();else s.Flick();auto expectedEvents=events;
#ifdef VALIDATE_ORIGINAL
*reinterpret_cast<int*>(raw+0x80)=ticks;*reinterpret_cast<u16*>(raw+0x44)|=input;events.clear();if(op==0)reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4D1A10)(raw);else reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4D1870)(raw);
assert(events==expectedEvents&&*reinterpret_cast<int*>(raw+0x100)==s.field_100&&*reinterpret_cast<u32*>(raw+0xF8)==s.field_F8&&*reinterpret_cast<u16*>(raw+0xFC)==s.field_FC&&*reinterpret_cast<u16*>(raw+0x44)==s.mInputFlags&&*reinterpret_cast<u16*>(raw+0x46)==s.mCBodyFlags&&*reinterpret_cast<u32*>(raw+0x24)==s.mRGB&&raw[0x124]==s.field_124&&*reinterpret_cast<int*>(rawPlayer+0x568)==player.field_568&&*reinterpret_cast<u16*>(rawOff+4)==offItem.mFlags&&*reinterpret_cast<u32*>(rawOff+0x24)==offItem.mRGB&&*reinterpret_cast<u16*>(rawOn+4)==onItem.mFlags&&*reinterpret_cast<u32*>(rawOn+0x24)==onItem.mRGB);events=expectedEvents;
#endif
// A genuine CItem movement changes the pointed-to ENV records, while CSwitch owns constructor copies at10C/118.
offItem.mPos.vx+=17;offItem.mPos.vy-=31;offItem.mPos.vz+=43;
onItem.mPos.vx-=47;onItem.mPos.vy-=59;onItem.mPos.vz+=61;
#ifdef VALIDATE_ORIGINAL
std::memcpy(rawOff+8,&offItem.mPos,12);std::memcpy(rawOn+8,&onItem.mPos,12);
void*actualAim=reinterpret_cast<void*(__attribute__((thiscall)) *)(void*)>(0x4D19E0)(raw);auto sourceAim=s.GetAutoAimTargetPointer();
assert(actualAim==(sourceAim==&s.field_10C?raw+0x10C:sourceAim==&s.field_118?raw+0x118:nullptr));
if(actualAim)assert(std::memcmp(actualAim,sourceAim,12)==0);
#endif
if(frame)std::cout<<',';emit(s);}std::cout<<"]\n";}}
'''
    # PC thiscall bridge retains the independent source node search and event recording.
    code=code.replace('int main(int argc,char**argv){','#ifdef VALIDATE_ORIGINAL\nvoid __attribute__((thiscall)) originalPulse(CSwitch*,int selector){CSwitch search;search.PulseLFA1Node(selector);}\n#endif\nint main(int argc,char**argv){').replace('reinterpret_cast<void*>((void(__attribute__((thiscall)) *)(CSwitch*,int))nullptr)','reinterpret_cast<void*>(&originalPulse)')
    rng=random.Random(0x4d1a10);cases=[]
    pulseNodes=[[1,0,0,0],[7,0,0,0],[1,0,0,0],[1,409600+4095,0,0],[1,409600+1,0,409600+4095],[1,0,0,409600+7]]
    def add(kind=0,mode=0,timer=0,timeout=0,signal=0,flags=6,subtract=0,counter=41,player=1,offFlags=0,onFlags=33,offRgb=0x123456,onRgb=0x808080,nodes=None,initial=0,steps=None):cases.append(dict(kind=kind,mode=mode,timer=timer,timeout=timeout,signal=signal,flags=flags,subtract=int(subtract),counter=counter,player=player,offFlags=offFlags,onFlags=onFlags,offRgb=offRgb,onRgb=onRgb,nodes=pulseNodes if nodes is None else nodes,initial=initial,steps=steps or [[0,2,0]]*12))
    for mode in [-1,0,1,2,3,4,5,6]:
        for timeout in [0,1,2,3,65535]:
            for signal in [0,1]:add(mode=mode,timeout=timeout,signal=signal,subtract=mode==4)
    for mode in [1,2,3,4,5]:
        for player in [0,1]:
            for timeout in [0,10]:add(mode=mode,player=player,timeout=timeout,subtract=mode==4,steps=[[1,2,0],[0,2,0]]*6)
    for timer in [0,65535,0xfffffffe,0xffffffff]:add(mode=2,timer=timer,timeout=65535)
    for rgb in [0,1,7,127,128,255,0xffffffff]:add(mode=3,onRgb=rgb);add(mode=5,onRgb=rgb)
    for initial in [-1,0,1,2,3,4,5]:add(kind=1,initial=initial,timeout=7)
    add(mode=3,timeout=1,nodes=[]);add(mode=4,steps=[[1,2,0]]*12,nodes=[])
    for _ in range(200):add(mode=rng.randrange(6),timer=rng.randrange(2**32),timeout=rng.choice([0,1,20,65535]),subtract=rng.randrange(2),counter=rng.randint(-2**31,2**31-1),player=rng.randrange(2),offFlags=rng.randrange(65536),onFlags=rng.randrange(65536),offRgb=rng.randrange(2**32),onRgb=rng.randrange(2**32),steps=[[rng.randrange(2),rng.choice([0,1,2,5,129,-1]),rng.choice([0,1,2])] for _ in range(12)])
    rows=[]
    for c in cases:
        row=[int(c[k]) for k in ['kind','mode','timer','timeout','signal','flags','subtract','counter','player','offFlags','offRgb','onFlags','onRgb']]+[len(c['nodes'])]+[v for node in c['nodes'] for v in node]
        row+=[c['initial']] if c['kind']==1 else [v for step in c['steps'] for v in step];rows.append(' '.join(map(str,row)))
    with tempfile.TemporaryDirectory(prefix='openspidey-switch-') as folder:
        cpp=Path(folder)/'reference.cpp';exe=Path(folder)/'reference';cpp.write_text(code);flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(exe)],check=True)
        command=[str(exe)]+([str(args.original_exe.resolve())] if args.original_exe else []);lines=subprocess.check_output(command,input=('\n'.join(rows)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/switch.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} switch cases; PC AI/Flick and actual base Die proved, constructor/trigger search boundaries source compiled; all PC aim getters retain original snapshots after real ENV record movement.')

if __name__=='__main__':main()
