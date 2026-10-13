"""Compile the actual recovered player sense/fist preparation and inline cheat scaler as independent resource-boundary fixtures."""
import itertools
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
struct CBody;std::vector<CBody*>bodies;std::vector<long long>events;
struct Record{int id,alive,model,type,flags,bodyFlags,x,y,z;unsigned rgb;};std::vector<Record>records;
struct SHandle{CBody*pWhatever=nullptr;unsigned Id=0;};
struct CBody{int id;bool valid=true;int mFlags=0,mModel=0,mType=0,mCBodyFlags=0x16;unsigned mRGB=0;CSVector mScale=CSVector(4096,4096,4096);CBody(){id=bodies.size()+1;bodies.push_back(this);records.push_back({});events.insert(events.end(),{0,id,mCBodyFlags});}void InitItem(const char*){events.insert(events.end(),{2,id});mModel=0;}void AttachTo(CBody**){events.insert(events.end(),{3,id});}void DeleteFrom(CBody**){events.insert(events.end(),{10,id});}Record snapshot(int alive){return {id,alive,mModel,mType,mFlags,mCBodyFlags,mScale.vx,mScale.vy,mScale.vz,mRGB};}~CBody(){events.insert(events.end(),{5,id});records[id-1]=snapshot(0);}static void operator delete(void*){}};
int G_TOON_SPIDEY_FLAG,G_STICKMAN_FLAG,G_PULSATING_HEAD_FLAG,gWaterEffect,actuatorTime,vibration;int*gSaveGameVibration=&vibration;CBody*SpideyAdditionalBodyPartsList=nullptr;
SHandle Mem_MakeHandle(CBody*b){events.insert(events.end(),{1,b?b->id:0});return {b,b?unsigned(b->id+1000):0};}
void*Mem_RecoverPointer(SHandle*h){events.insert(events.end(),{4,h->pWhatever?h->pWhatever->id:0,h->Id});if(h->pWhatever&&!h->pWhatever->valid){h->pWhatever=nullptr;return nullptr;}return h->pWhatever;}
unsigned SFX_Play(int sound,int volume,int pitch){events.insert(events.end(),{6,sound,volume,pitch});return 0x80000000;}
void SFX_Stop(unsigned handle){events.insert(events.end(),{7,handle});}int Pad_GetActuatorTime(int controller,int actuator){events.insert(events.end(),{8,controller,actuator});return actuatorTime;}
void Pad_ActuatorOn(int controller,unsigned short duration,int actuator,int strength){events.insert(events.end(),{9,controller,duration,actuator,strength});}
struct CPlayer{int field_ECC,kind;unsigned field_ED0;SHandle field_ED4,field_5B8[2];int field_5AC,field_5B0,mElapsedTicks;};
#define PLR_I32(player,offset) ((player)->kind)
struct PlayerFramePreparation{CPlayer&player_;void updateSenseEffect();void updateFistBonus();};
'''
    bridge = (root / 'src/compatibility/player_behavior_bridge.h').read_text()
    source = (root / 'src/game/player/player_status_updates.cpp').read_text()
    code += controller_method(bridge, 'inline void SpideyAI0_ApplyCheatScale(') + '\n'
    for signature in ['void PlayerFramePreparation::updateSenseEffect(', 'void PlayerFramePreparation::updateFistBonus(']:
        code += controller_method(source, signature) + '\n'
    code += r'''
int main(){long long v[14];while(std::cin>>v[0]){for(int i=1;i<14;i++)std::cin>>v[i];bodies.clear();records.clear();CPlayer p{int(v[0]),int(v[1]),unsigned(v[2]),{},{{},{}},int(v[3]),int(v[4]),int(v[5])};for(int i=0;i<3;i++){CBody*b=new CBody();b->mType=i?505:504;b->mModel=i?8:4;b->mFlags=0x8400;b->mScale=CSVector(1111,2222,3333);b->valid=v[6+i]!=2;SHandle&h=i?p.field_5B8[i-1]:p.field_ED4;h={v[6+i]?b:nullptr,unsigned(1001+i)};}
G_PULSATING_HEAD_FLAG=v[9];G_TOON_SPIDEY_FLAG=v[10];G_STICKMAN_FLAG=v[11];vibration=v[12];actuatorTime=v[13];gWaterEffect=73;events.clear();PlayerFramePreparation prep{p};prep.updateSenseEffect();prep.updateFistBonus();std::cout<<'['<<p.field_ECC<<','<<p.field_ED0<<','<<gWaterEffect<<','<<p.field_5AC<<','<<p.field_5B0;for(SHandle*h:{&p.field_ED4,&p.field_5B8[0],&p.field_5B8[1]})std::cout<<','<<(h->pWhatever?h->pWhatever->id:0)<<','<<h->Id;std::cout<<",[";for(unsigned i=0;i<bodies.size();i++){if(i)std::cout<<',';Record r=records[i].id?records[i]:bodies[i]->snapshot(1);std::cout<<'['<<r.id<<','<<r.alive<<','<<r.model<<','<<r.type<<','<<r.flags<<','<<r.bodyFlags<<','<<r.x<<','<<r.y<<','<<r.z<<','<<r.rgb<<']';}std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    base = [24, 1, 0, 1, 0, 2, 0, 0, 0, 0, 0, 0, 1, 0]
    cases = []
    for handles in itertools.product(range(3), repeat=3):
        for cheats in [(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 0, 1)]:
            for status in [(0, 0, 0), (1, 0, 17), (24, 1, 8191), (24, 0, 8192)]:
                case = base.copy(); case[6:9] = handles; case[9:12] = cheats
                case[0], case[3], case[4] = status; cases.append(case)
    for ticks in [-2147483648, -1, 0, 1, 2, 6, 2147483647]:
        for timer in [-2147483648, -1, 0, 1, 2, 6, 2147483647]:
            case = base.copy(); case[0] = timer; case[5] = ticks; cases.append(case)
    rng = random.Random(0x4B16DE)
    for _ in range(200):
        case = base.copy(); case[0] = rng.choice([0, rng.randint(-2147483648, 2147483647)])
        case[1] = rng.randrange(3); case[2] = rng.choice([0, 0x80000000]); case[3] = rng.randrange(2)
        case[4] = rng.randint(-2147483648, 2147483647); case[5] = rng.choice([0, 2, 6, -1])
        case[6:9] = [rng.randrange(3) for _ in range(3)]; case[9:13] = [rng.randrange(2) for _ in range(4)]
        case[13] = rng.randrange(1 << 32); cases.append(case)
    with tempfile.TemporaryDirectory(prefix='openspidey-additional-effects-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-I', str(root / 'src'), str(cpp), '-o', str(binary)], check=True)
        rows = ('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()
        lines = subprocess.check_output([str(binary)], input=rows).decode().splitlines()
    result = [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-additional-effects.json').write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(f'Compiled {len(cases)} actual source sense/fist preparations with timer wrap, source factory/model/list order, actuator low word, stale handle preservation and all original cheat scale branches.')


if __name__ == '__main__':
    main()
