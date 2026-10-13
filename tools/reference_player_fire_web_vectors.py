"""Compile actual CPlayer::FireWeb, observing its original aim, resource and virtual-call boundaries."""
import json
import random
import subprocess
import tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/player/spidey.cpp').read_text()
    utils = (root / 'src/core/utils.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <cmath>
#include <iostream>
#include <vector>
using VECTOR=CVector;
std::vector<int> events;void emit(std::initializer_list<int> values){events.insert(events.end(),values);}
unsigned char vibration=0;int supply,autoAim,environmentHit,switchEnabled,trapAccepted,tugAccepted,actuatorClock;
int catan(int value){return int(std::atan(value/4096.)*651.0006103515625);}
int M3dMaths_SquareRoot0(int value){return value<=-32768?32768:value<0?0:int(std::sqrt(double(value)));}
struct CBody{int mType=304;CVector mPos{1048576,-327680,1572864};unsigned field_2A8=0;};
struct CBaddy:CBody{int TrapWeb(){emit({10});return trapAccepted;}int TugWeb(){emit({11});return tugAccepted;}};
struct CSuper:CBody{};
struct CSwitch:CBody{int field_100=1;void Flick(){emit({9});}};
CBaddy baddy;CSwitch control;
struct SHandle{};CVector aimPoint{2097152,-655360,3145728};
struct CWeb{unsigned short field_102=1;int mode=0;CVector hook,aim;int attached=0,splat=0;CSVector normal;
void Fire(CVector&h,CVector&a,CBody*p,bool hit,CSVector&n){hook=h;aim=a;attached=p?p->mType:0;splat=hit;normal=n;mode=1;}
void SwitchToBlob(){mode=3;}};
struct CPlayer{CVector mPos{123456,-654321,765432},mForwardDirection{0,0,4096};CBody*field_DCC=nullptr;
CVector*field_DD0=nullptr;CWeb*field_E6C=nullptr;int field_8F8=1,mAnim=255,field_5E8=0,field_5E4=0;
i32 FireWeb(bool,i32,CVector*,bool,CSVector*);
CVector*SelectTargetSwitch(int distance,int cone,SHandle*,int weight,int facing){emit({1,distance,cone,weight,facing});return autoAim?&aimPoint:nullptr;}
int DecreaseWebbing(int amount){emit({3,amount});return supply;}};
void M3dUtils_GetHookPosition(VECTOR*p,CPlayer*,int hook){emit({4,hook});*p=CVector(100000+hook*8193,-300000-hook*4097,500000+hook*12289);}
struct SLineInfo{CVector StartCoords,EndCoords,MinCoords,MaxCoords,Position;CSVector Normal;CBody*pItem=nullptr;unsigned*pFace=nullptr;};
int G_LINE_OF_SIGHT_CHECK=0;unsigned face[4]={0,0,0,0x02000000};
void M3dColij_InitLineInfo(SLineInfo*){}
void M3dZone_LineToItem(SLineInfo*l,int mode){emit({2,mode,G_LINE_OF_SIGHT_CHECK,l->StartCoords.vx,l->StartCoords.vy,l->StartCoords.vz,l->EndCoords.vx,l->EndCoords.vy,l->EndCoords.vz});
if(environmentHit){l->pItem=&control;l->pFace=face;l->Position=CVector(-1234567,-2345678,3456789);l->Normal=CSVector(0,-4096,0);}}
CSwitch*Switch_GetCSwitchObjectFromItem(CBody*){return &control;}
int Pad_GetActuatorTime(int controller,int actuator){emit({5,controller,actuator});return actuatorClock;}
void Pad_ActuatorOn(int controller,int duration,int actuator,int strength){emit({6,controller,duration,actuator,strength});}
int SFX_PlayPos(int sound,CVector*p,int pitch){emit({7,sound,p->vx,p->vy,p->vz,pitch});return 42;}
void SFX_Play(int sound,int volume,int pitch){emit({8,sound,volume,pitch});}
void Web_Trap(CSuper*,unsigned char variant){emit({12,variant});}
struct CImpactWeb{CImpactWeb(CVector&p,CSVector&direction,int speed,int damage,int duration){emit({13,p.vx,p.vy,p.vz,direction.vx,direction.vy,direction.vz,speed,damage,duration});}};
void Reloc_CallUserFunction(const char*,int function,unsigned*params,int final){emit({14,function,int(params[0]),int(params[1]),int(params[2]),int(params[3]),int(params[4]),int(params[5]),int(params[6]),final});}
'''
    code += controller_method(utils, 'int Utils_CalcAim(') + '\n'
    fire = controller_method(source, 'i32 CPlayer::FireWeb(')
    fire = fire.replace('static u8 * const gSaveGameVibration = (u8*)0x006828D3;',
                        'u8 * const gSaveGameVibration = &vibration;')
    code += fire + r'''
int main(){int useHeld,cost,explicitHit,targetKind,hasStrand,shot,animation,fireVariant,vibrate,oldSound;
while(std::cin>>useHeld>>cost>>explicitHit>>targetKind>>hasStrand>>shot>>animation>>fireVariant>>vibrate>>supply>>autoAim>>environmentHit>>switchEnabled>>trapAccepted>>tugAccepted>>actuatorClock>>oldSound){
events.clear();CPlayer p;CWeb web;control.mType=407;control.field_100=switchEnabled;baddy.field_2A8=0;
p.field_DCC=targetKind==0?nullptr:targetKind==1?static_cast<CBody*>(&baddy):static_cast<CBody*>(&control);
p.field_E6C=hasStrand?&web:nullptr;p.field_8F8=shot;p.mAnim=animation;p.field_5E8=fireVariant;p.field_5E4=oldSound;vibration=vibrate;
CVector target{4194304,-1048576,6291456};CSVector normal{7,-8,9};int result=p.FireWeb(useHeld,cost,&target,explicitHit,&normal);
std::cout<<'['<<result<<','<<(p.field_E6C!=nullptr)<<','<<(p.field_DD0!=nullptr)<<','<<p.field_5E4<<','<<baddy.field_2A8<<','<<web.mode<<','<<web.hook.vx<<','<<web.hook.vy<<','<<web.hook.vz<<','<<web.aim.vx<<','<<web.aim.vy<<','<<web.aim.vz<<','<<web.attached<<','<<web.splat<<','<<web.normal.vx<<','<<web.normal.vy<<','<<web.normal.vz<<",[";
for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    cases = []
    # Deliberate branch coverage followed by independent combinations, including callbacks that reject an action.
    for strand in [0, 1]:
        for target in [0, 1, 2]:
            for shot in [1, 2, 8]:
                for supplied in [0, 1]:
                    for animation in [139, 255]:
                        cases.append([1, 128, 0, target, strand, shot, animation, 0, 1,
                                      supplied, 1, 1, 1, 1, 1, 0, 0])
    rng = random.Random(0x4C5DD0)
    for _ in range(256):
        cases.append([rng.randrange(2), rng.choice([0, 128, 256, 600, 900]), rng.randrange(2),
                      rng.randrange(3), rng.randrange(2), rng.choice([0, 1, 2, 8]),
                      rng.choice([139, 255, 265]), rng.randrange(2), rng.randrange(2),
                      rng.randrange(2), rng.randrange(2), rng.randrange(2), rng.randrange(2),
                      rng.randrange(2), rng.randrange(2), rng.choice([0, 2, 3]), rng.choice([0, 77])])
    with tempfile.TemporaryDirectory(prefix='openspidey-fire-web-') as directory:
        cpp = Path(directory) / 'reference.cpp'
        binary = Path(directory) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), str(cpp), str(root / 'src/core/fixed_vector.cpp'),
                        '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary)], input=('\n'.join(' '.join(map(str, case))
                                          for case in cases) + '\n').encode()).decode().splitlines()
    output = root / 'modern/OpenSpidey.Tests/Fixtures/player-fire-web.json'
    output.write_text(json.dumps([dict(input=case, expected=json.loads(line))
                                  for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Generated {len(cases)} extracted CPlayer::FireWeb branch/aim/cost/callback cases.')


if __name__ == '__main__':
    main()
