"""Compile actual SelectAutoAimTarget with explicit selector/resource/list ownership boundaries."""
import json, random, subprocess, tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method

root=Path(__file__).resolve().parent.parent
method=controller_method((root/'src/game/player/spidey.cpp').read_text(),'u8 CPlayer::SelectAutoAimTarget(void)')
method=method.replace('static u8 * const gRegionByte = (u8*)0x6B4678;', 'static u8 localRegion=3;u8 * const gRegionByte=&localRegion;')
method=method.replace('(*(u16*)((char*)target + 0xF4) << 12)','(target->height << 12)')
code=r'''
#include <cstdint>
#include <iostream>
#include <vector>
using i32=int32_t;using u32=uint32_t;using u16=uint16_t;using u8=uint8_t;
struct Vec{i32 vx=0,vy=0,vz=0;};struct Ang{int16_t vy=0;};
std::vector<int>events;void*G_MISCELLANEOUS_RENDERING_LIST=nullptr;
struct CBody{Vec mPos;Ang mAngles;u16 mModel=0,mType=0;u16 height=0;u8 padding[256]={};
 void InitItem(const char*){events.push_back(1);}void AttachTo(CBody**){events.push_back(2);}void DeleteFrom(CBody**){events.push_back(3);}~CBody(){events.push_back(4);}};
// Radius write is an explicit source CBody offset boundary; isolate it from fixture-only layout.
int Spool_GetModel(u32 checksum,int region){events.insert(events.end(),{5,int(checksum),region});return 77;}
struct CPlayer{u16 field_E04=8,field_E06=9,field_E08=10;Vec*field_DD0=reinterpret_cast<Vec*>(1);u8 field_8EA=0;
 CBody*field_878=nullptr,*field_DCC=nullptr,*selected=nullptr;i32 mElapsedTicks=2;
 CBody*SelectTargetBaddy(int a,int b,int c,int d){events.insert(events.end(),{6,a,b,c,d});return selected;}u8 SelectAutoAimTarget();};
'''
# Preserve the actual source radius store while ensuring the fixture's CBody contains its byte offset.
code+=method+r'''
int main(){int transition,have,target,ticks,yaw,x,y,z,height;while(std::cin>>transition>>have>>target>>ticks>>yaw>>x>>y>>z>>height){
 CPlayer p;CBody selected,previous;selected.mPos={x,y,z};selected.height=height;p.selected=target?&selected:nullptr;
 p.field_DCC=&previous;p.field_8EA=transition;p.mElapsedTicks=ticks;if(have){p.field_878=new CBody();p.field_878->mAngles.vy=yaw;}
 events.clear();int result=p.SelectAutoAimTarget();std::cout<<result<<' '<<p.field_E04<<' '<<p.field_E06<<' '<<p.field_E08<<' '<<(p.field_DD0==nullptr)<<' '<<(p.field_DCC==&selected?1:p.field_DCC==&previous?2:0)<<' '<<(p.field_878!=nullptr);
 if(p.field_878)std::cout<<' '<<p.field_878->mPos.vx<<' '<<p.field_878->mPos.vy<<' '<<p.field_878->mPos.vz<<' '<<p.field_878->mAngles.vy;else std::cout<<" 0 0 0 0";
 std::cout<<' '<<events.size();for(int v:events)std::cout<<' '<<v;std::cout<<'\n';delete p.field_878;}}
'''
rng=random.Random(0x4C5AA0)
cases=[[transition,have,target,ticks,yaw,123456,-234567,345678,height]
       for transition in [0,1,255] for have in [0,1] for target in [0,1]
       for ticks,yaw,height in [(2,0,128),(65537,32760,65535),(-1,-32768,0)]]
for _ in range(192):cases.append([rng.randrange(256),rng.randrange(2),rng.randrange(2),rng.randrange(-100000,100000),rng.randrange(-32768,32768),*[rng.randrange(-10000000,10000000) for _ in range(3)],rng.randrange(65536)])
with tempfile.TemporaryDirectory() as temp:
 path=Path(temp);(path/'autoaim.cpp').write_text(code)
 subprocess.run(['c++','-std=c++17','-O0','-fwrapv',str(path/'autoaim.cpp'),'-o',str(path/'autoaim')],check=True)
 output=subprocess.check_output([str(path/'autoaim')],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
(root/'modern/OpenSpidey.Tests/Fixtures/player-autoaim.json').write_text(json.dumps({'source':'Actual CPlayer::SelectAutoAimTarget; source selector/resource/list and u16 height read are explicit fixture boundaries. No original allocator or rendering called.','cases':[{'input':c,'expected':list(map(int,o.split()))} for c,o in zip(cases,output,strict=True)]},separators=(',',':'))+'\n')
print(f'Generated {len(cases)} compiled source autoaim state/reticle ownership cases.')
