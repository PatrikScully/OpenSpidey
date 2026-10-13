"""Compile recovered full player combo methods using immutable original PC fight blobs and explicit world boundaries."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def original_blobs(path):
    image = path.read_bytes()
    pe = struct.unpack_from('<I', image, 60)[0]
    count = struct.unpack_from('<H', image, pe + 6)[0]
    size = struct.unpack_from('<H', image, pe + 20)[0]
    base = struct.unpack_from('<I', image, pe + 24 + 28)[0]
    def read(address, length):
        for index in range(count):
            section = pe + 24 + size + index * 40
            _, virtual, stored, offset = struct.unpack_from('<4I', image, section + 8)
            if virtual <= address - base < virtual + stored:
                at = offset + address - base - virtual
                return image[at:at + length]
        raise ValueError(f'Unmapped original data VA{address:X}')
    distance = read(0x569194, 0x108)
    moves = read(0x56929C, 8192)
    cursor = 0
    while struct.unpack_from('<I', moves, cursor)[0] != 6666:
        parts = struct.unpack_from('<H', moves, cursor + 32)[0]
        tail = cursor + 36 + parts * 12
        for _ in range(3):
            tail = moves.index(255, tail) + 1
        cursor = (tail + 3) & ~3
    return image, distance, moves[:cursor + 4]


def latent_branch_blob(blob, late):
    """Fixture-only valid records exercise supported branches absent from the shipped single-press inputs."""
    result = bytearray()
    cursor = 0
    while struct.unpack_from('<I', blob, cursor)[0] != 6666:
        parts = struct.unpack_from('<H', blob, cursor + 32)[0]
        header = bytearray(blob[cursor:cursor + 36])
        branches = blob[cursor + 36:cursor + 36 + parts * 12]
        streams = []
        tail = cursor + 36 + parts * 12
        for _ in range(3):
            end = blob.index(255, tail) + 1
            streams.append(blob[tail:end]); tail = end
        move = struct.unpack_from('<H', header)[0]
        if move == 0:
            struct.pack_into('<H', header, 32, 1)
            branches = branches[:12]
            if late:
                struct.pack_into('<HH', header, 0x16, 8, 12)
        if move == 8:
            streams[0] = bytes([0, 1, 5, 255])
            struct.pack_into('<H', header, 0x22, int(late))
        result += header + branches + b''.join(streams)
        result += bytes((-len(result)) % 4)
        cursor = (tail + 3) & ~3
    return bytes(result + struct.pack('<I', 6666))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    image, distance, moves = original_blobs(args.original_exe)
    source = (root / 'src/game/player/spidey.cpp').read_text()
    vector_source = (root / 'src/core/fixed_vector.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include "core/distance.h"
#include "game/player/player_action.h"
#include <algorithm>
#include <cassert>
#include <cmath>
#include <cstring>
#include <fstream>
#include <iostream>
#include <sys/mman.h>
#include <vector>
using namespace spidey::core;
using VECTOR=Vector;
std::vector<int> events;int tick=0,block=0,traceCursor=0,collision=0,accepted=0,selectTarget=0,groundGone=0,jump=0;
unsigned G_TIMER_RELATED=1000;int G_CURRENTSUIT=0,G_DIFFICULTY_LEVEL=2;
int sfx[300][2];int*G_SPIDEY_SFX_ENTRY[300];u8 camera[0x200]={};u8*G_CAMERA_LIST=camera;void*G_UNK_POSE=nullptr;
void event(std::initializer_list<int> values){events.insert(events.end(),values);}
void print_if_false(int,const char*){} // Original compiled assertion hook is a bare ret.
int Rnd(int bound){event({10,bound});return bound>0?bound/2:0;}
void SFX_Play(int sound,int volume,int pitch){event({9,sound,volume,pitch});}
void SFX_PlayPos(int sound,CVector*p,int pitch){event({19,sound,p->vx,p->vy,p->vz,pitch});}
int ratan2(int x,int y){if(!y)return(x>0?1:x<0?-1:0)*1024;return int(std::atan2(double(x),double(y))*651.0006103515625);}
struct SHook{CSVector Part;u16 Offset=0;};
struct SHitInfo{u8 field_0=0,field_1=0;int field_4=0;u16 field_8=0;CVector field_C;i16 field_18=0;u16 field_1A=0;};
struct CItem{CVector mPos;CItem*mNextItem=nullptr;};
struct CBody:CItem{u16 mFlags=2,mType=306;int id=0;int Hit(SHitInfo*h){event({18,id,h->field_0,h->field_1,h->field_4,h->field_8,h->field_C.vx,h->field_C.vy,h->field_C.vz,h->field_18,h->field_1A});return accepted;}};
using CBaddy=CBody;using CSuper=CBody;CBody targets[6];CBody*G_BADDY_LIST=nullptr;
struct SHandle{void*pWhatever=nullptr;};
SHandle Mem_MakeHandle(CBody*p){event({23,p?p->id:-1});return{p};}
struct SComboPart{u8 mActive=0,mWaitingFirst=0,mUseLateWindow=0,mStarted=0;int mLastPressTime=0,mFlags=0;u8*mInput=nullptr;};
struct SLineInfo{CVector StartCoords,EndCoords,MinCoords,MaxCoords,Position,Normal;CItem*pItem=nullptr;};
void M3dColij_InitLineInfo(SLineInfo*p){p->pItem=nullptr;}
void M3dZone_LineToItem(SLineInfo*p,int flags){event({12,p->StartCoords.vx,p->StartCoords.vy,p->StartCoords.vz,p->EndCoords.vx,p->EndCoords.vy,p->EndCoords.vz,flags});p->pItem=block==++traceCursor+1?targets:nullptr;}
int M3dColij_LineToSphere(CVector*a,CVector*b,CVector*d,CBody*,int argument,int scale){event({11,a->vx,a->vy,a->vz,b->vx,b->vy,b->vz,d->vx,d->vy,d->vz,argument,scale});return block==1;}
void M3d_BuildTransform(CBody*p){event({15,p->id});}
int Web_CollideWithSuper(CBody*p,CVector*a,CVector*b,SHook*h,int scale){event({16,p->id,a->vx,a->vy,a->vz,b->vx,b->vy,b->vz,scale});h->Offset=7;return collision;}
struct CPlayer{
 u8 raw[0x300]={};u8 input[0x140]={};void*field_E0C=input;
 CVector mPos={123456,-234567,345678},mForwardDirection={0,0,4096},mRightDirection={4096,0,0},mUpDirection={0,-4096,0};
 int mAccumulatedTicks=0,mHeadingDelta=17,mHeadingTurnTicksRemaining=0,mActionState=0x800;
 i16 mHealth=100,mAnim=0,mFrame=0,field_EA6=77;u8 mAnimFinished=0,mIsCrawling=0;
 u16 field_8FC=0,field_8FE=0,field_900=0,field_902=0,field_904=0,field_906=0,field_908=0,field_90A=0,field_90C=0,field_914=0,field_916=0;
 int field_910=0;unsigned field_918=0;u8*field_950=nullptr,*field_954=nullptr;u16*field_958=nullptr;
 u8 field_94C=0,field_94D=0,field_378=0,field_354=0;SComboPart field_95C[17];CVector field_37C[16],field_43C[16];CBody*field_A6C[4]={};int field_A7C=0;
 int field_91C=100,field_920=200,field_924=300,field_928=400,field_92C=500,field_930=600,field_934=700,field_938=800,field_93C=900,field_940=1000,field_944=1100,field_948=1200;
 int*field_350=nullptr;int field_5AC=0,field_528=3,field_52C=0,field_534=0;int field_358=0;unsigned field_35C=0;
 signed char field_E2D=0,field_E2E=0,field_E2F=0,field_E30=0;
 int field_898=1000,field_DE0=0,field_544=0;SHandle field_DD8;CBody*field_DCC=nullptr;u8 field_8F8=0,field_552=0,field_8ED=0;
 CBody*SelectTargetBaddy(int range,int low,int high,int flags){event({1,range,low,high,flags});return selectTarget?targets:nullptr;}
 int GetEffectiveHeading(){return 777;}
 void OrientToNormal(bool use,CVector*p){event({2,use,p->vx,p->vy,p->vz,mHeadingDelta});}
 void RunAnim(int animation,int frame,int last){event({8,animation,frame,last,sfx[animation][0]});mAnim=animation;mFrame=frame;mAnimFinished=0;}
 void PlaySingleAnim(int animation,int from,int to){event({24,animation,from,to});}
 void ApplyPose(void*){event({3});}
 void PutCameraBehind(int argument){event({17,argument});}
 void CreateCombatImpactEffect(CVector*p,int kind){event({20,p->vx,p->vy,p->vz,kind});}
 int CheckGroundGone(){event({21});return groundGone;}
 int CheckJump(){event({22});return jump;}
 void SwitchToStandMode(){event({25});}
 void LockTargetTorsoAngle(){event({26});}
 void SetTargetTorsoAngleToThisPoint(CVector*p){event({27,p->vx,p->vy,p->vz});}
 void SortAnimationFollowOnData(){}void SortFistsData(){}
 void ParseFightData();void InitiateCombo(u16,int);int UpdateAndTrackCombo();int GetDamageInflictedFromDifficulty(int);
};
void M3dUtils_GetHookPosition(VECTOR*out,CPlayer*,int hook){event({13,hook});out->vx=1000+tick*300+hook*100;out->vy=-2000+tick*101;out->vz=3000-tick*200;}
void M3dUtils_GetDynamicHookPosition(VECTOR*out,CPlayer*,SHook*h){event({14,h->Part.vx,h->Part.vy,h->Part.vz,h->Offset});out->vx=1000+tick*12000+h->Offset*100;out->vy=-2000+tick*101;out->vz=3000-tick*6000;}
void M3dUtils_GetDynamicHookPosition(VECTOR*out,CBody*p,SHook*h){event({28,p->id,h->Part.vx,h->Part.vy,h->Part.vz,h->Offset});out->vx=p->mPos.vx+70;out->vy=p->mPos.vy+80;out->vz=p->mPos.vz+90;}
#define PLR_U8(p,off) (reinterpret_cast<u8*>(p)[off])
namespace spidey{namespace player{struct PlayerBehaviorController{CPlayer&player_;void updatePunchCombo();};}}
'''
    for signature in ['CVector operator+(', 'CVector* CVector::operator+=(', 'CVector operator-(',
                      'CVector* CVector::operator-=(', 'CVector operator*(', 'CVector operator>>(']:
        code += controller_method(vector_source, signature) + '\n'
    code += controller_method((root / 'src/engine/rendering/ps2funcs.cpp').read_text(), 'void VectorNormal(') + '\n'
    for signature in ['void CPlayer::ParseFightData(', 'void CPlayer::InitiateCombo(',
                      'i32 CPlayer::UpdateAndTrackCombo(', 'i32 CPlayer::GetDamageInflictedFromDifficulty(']:
        code += controller_method(source, signature) + '\n'
    dispatch = controller_method((root / 'src/game/player/player_combat_actions.cpp').read_text(),
                                 'void PlayerBehaviorController::updatePunchCombo(')
    code += '\nnamespace spidey{namespace player{\n' + dispatch.replace('gCPlayer_UpdateAndTrackCombo(pPlayer)', 'pPlayer->UpdateAndTrackCombo()') + '\n}}\n'
    code += r'''
void emit(CPlayer&p,int result){
 std::cout<<"{\"state\":["<<result<<','<<p.mPos.vx<<','<<p.mPos.vy<<','<<p.mPos.vz<<','<<p.mAnim<<','<<p.mFrame<<','<<int(p.mAnimFinished)<<','<<p.field_8FC<<','<<p.field_910<<','<<p.field_918<<','<<p.field_914<<','<<p.field_916<<','<<int(p.field_94C)<<','<<int(p.field_94D)<<','<<int(p.field_378)<<','<<(p.field_954!=nullptr)<<','<<p.field_A7C<<','<<int(p.field_354)<<','<<p.field_358<<','<<p.field_35C<<','<<p.field_5AC<<','<<p.field_52C<<','<<p.field_534<<','<<p.mHeadingDelta<<','<<p.mActionState<<','<<p.field_EA6<<','<<int(p.field_8F8)<<','<<int(p.field_552)<<','<<int(p.field_8ED)<<','<<p.field_544<<','<<int(p.raw[0xAE])<<','<<p.field_DE0<<','<<(p.field_DD8.pWhatever!=nullptr)<<','<<(p.field_958?*p.field_958:-1)<<"],\"parts\":[";
 int count=std::min(int(*reinterpret_cast<u16*>((reinterpret_cast<u8**>(0x6A8CB4))[p.field_8FC]+0x20)),16);
 for(int i=0;i<count;i++){if(i)std::cout<<',';auto&part=p.field_95C[i];unsigned move=*reinterpret_cast<unsigned*>((reinterpret_cast<u8**>(0x6A8CB4))[p.field_8FC]+0x24+i*12);u8*start=*reinterpret_cast<u8**>(move+4);std::cout<<'['<<int(part.mActive)<<','<<int(part.mWaitingFirst)<<','<<int(part.mUseLateWindow)<<','<<int(part.mStarted)<<','<<part.mFlags<<','<<part.mLastPressTime<<','<<(part.mInput-start)<<']';}
 std::cout<<"],\"buttons\":[";for(int i=0;i<20;i++){if(i)std::cout<<',';std::cout<<'['<<int(p.input[i*16+1])<<','<<int(p.raw[0x1C1+i*16])<<']';}
 std::cout<<"],\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]}";
}
int main(int argc,char**argv){assert(argc==3);assert(mmap(reinterpret_cast<void*>(0x560000),0x20000,PROT_READ|PROT_WRITE,MAP_PRIVATE|MAP_ANONYMOUS|MAP_FIXED,-1,0)!=MAP_FAILED);assert(mmap(reinterpret_cast<void*>(0x6A8000),0x2000,PROT_READ|PROT_WRITE,MAP_PRIVATE|MAP_ANONYMOUS|MAP_FIXED,-1,0)!=MAP_FAILED);
 std::ifstream(argv[1],std::ios::binary).read(reinterpret_cast<char*>(0x569194),264);std::ifstream(argv[2],std::ios::binary).read(reinterpret_cast<char*>(0x56929C),65536);
 assert(mmap(reinterpret_cast<void*>(0x60D000),0x1000,PROT_READ|PROT_WRITE,MAP_PRIVATE|MAP_ANONYMOUS|MAP_FIXED,-1,0)!=MAP_FAILED);CPlayer parser;parser.ParseFightData();int v[18];while(std::cin>>v[0]){for(int i=1;i<18;i++)std::cin>>v[i];CPlayer p;int steps=v[17];p.field_5AC=v[4];p.mIsCrawling=v[12];p.field_DCC=v[15]?targets:nullptr;p.raw[0xAE]=255;
 G_CURRENTSUIT=v[2];G_DIFFICULTY_LEVEL=v[3];accepted=v[5];collision=v[6];block=v[7];selectTarget=v[11];camera[0x180]=v[13];
 for(int i=0;i<v[16];i++){targets[i]=CBody{};targets[i].id=i;targets[i].mPos=p.mPos+CVector(v[10]<<12,0,0);targets[i].mFlags=v[8];targets[i].mType=v[9];targets[i].mNextItem=i+1<v[16]?&targets[i+1]:nullptr;}G_BADDY_LIST=v[16]?targets:nullptr;
 for(int i=0;i<300;i++){sfx[i][0]=int(0xFFFF0001);sfx[i][1]=-1;G_SPIDEY_SFX_ENTRY[i]=sfx[i];}events.clear();tick=0;G_TIMER_RELATED=1000;p.InitiateCombo(v[0],v[1]);std::cout<<"[";emit(p,-1);
 for(int step=0;step<steps;step++){int s[10];for(int&i:s)std::cin>>i;tick=p.mAccumulatedTicks=s[0];G_TIMER_RELATED=1000+tick;traceCursor=0;events.clear();int mask=s[1];p.input[0x111]=mask&1;p.input[0x131]=(mask>>1)&1;p.input[0x121]=(mask>>2)&1;p.input[0x101]=(mask>>3)&1;p.input[0x110]=s[2];p.input[0x120]=s[3];p.input[0x130]=s[4];p.field_E2D=s[5];p.field_E2E=s[6];p.mAnimFinished=s[7];groundGone=s[8];jump=s[9];int result=-2;if(v[14])spidey::player::PlayerBehaviorController{p}.updatePunchCombo();else result=p.UpdateAndTrackCombo();std::cout<<',';emit(p,result);}
 std::cout<<"]\n";
 }
}
'''
    def case(move=0, **options):
        initial = dict(move=move, head=0, suit=0, difficulty=2, charge=0, accepted=1, collision=1,
                       block=0, flags=2, type=306, distance=100, target=0, crawl=0, camera=0, dispatch=0, aim=0, target_count=2)
        initial.update(options)
        return {'input': list(initial.values()), 'steps': []}
    cases = []
    ids = [0, 1, 3, 6, 8, 9, 10, 11, 12, 13, 16, 17]
    for move in ids:
        for suit in [0, 2, 3, 4]:
            for difficulty in [0, 1, 2, 3]:
                c = case(move, suit=suit, difficulty=difficulty, charge=2, camera=1)
                c['steps'] = [[tick, 0, 0, 0, 0, 0, 0, 0, 0, 0] for tick in range(0, 65, 2)]
                # Stop after source terminator; Track must not be called on an already completed action.
                cases.append(c)
    for move in [0, 1, 8, 11]:
        for button in [1, 2, 4, 8, 15]:
            for tick in [0, 1, 4, 14, 20, 21]:
                c = case(move, collision=0)
                c['steps'] = [[tick, button, 1, 0, 0, 1, 0, 0, 0, 0],
                              [tick+2, 0, 1, 0, 0, -1, 1, 0, 0, 0],
                              [tick+4, 0, 1, 0, 0, 0, -1, 1, 0, 0]]
                cases.append(c)
    for block in range(5):
        for move in ids:
            c = case(move, block=block, collision=0, target=1, head=4)
            c['steps'] = [[tick, 0, 0, 0, 0, 0, 0, 0, 0, 0] for tick in [0, 2, 4, 6, 8, 10]]
            cases.append(c)
    for flags in [0, 1, 2, 3]:
        for type_id in [305, 306, 316]:
            for distance_units in [699, 700]:
                for accept in [0, 1]:
                    c = case(10, flags=flags, type=type_id, distance=distance_units, accepted=accept)
                    c['steps'] = [[tick, 0, 0, 0, 0, 0, 0, 0, 0, 0] for tick in [0, 2, 4, 6, 8, 10]]
                    cases.append(c)
    for move in [0, 1, 8]:
        for elapsed in [0, 4, 5]:
            for ground in [0, 1]:
                for jump_value in [0, 1]:
                    c = case(move, dispatch=1)
                    c['steps'] = [[elapsed, 0, 1, 1, 0, 0, 0, 0, ground, jump_value]]
                    cases.append(c)
    # Dispatch follow-on6/3, with and without the stored torso target, and stand completion.
    for move, mask in [(0, 1), (1, 1), (8, 1), (11, 1)]:
        for aim in [0, 1]:
            for forward in [-1, 0, 1]:
                c = case(move, collision=0, dispatch=1, aim=aim)
                c['steps'] = [[0, mask, 0, 0, 0, 0, forward, 0, 0, 0],
                              [20, 0, 0, 0, 0, 0, forward, 0, 0, 0],
                              [22, 0, 0, 0, 0, 0, forward, 1, 0, 0]]
                cases.append(c)
    for count in [0, 1, 4, 5, 6]:
        for accept in [0, 1]:
            c = case(10, target_count=count, accepted=accept)
            c['steps'] = [[tick, 0, 0, 0, 0, 0, 0, 0, 0, 0] for tick in range(0, 24, 2)]
            cases.append(c)
    variants = [moves, latent_branch_blob(moves, False), latent_branch_blob(moves, True)]
    groups = [cases, [], []]
    for late in [False, True]:
        first_ticks = [0, 1, 7, 8, 12, 13] if late else [0, 1, 14, 20, 21]
        for first in first_ticks:
            for gap in [0, 4, 5, 6]:
                for held in [0, 1]:
                    c = case(0, collision=0)
                    c['steps'] = [[first, 1, 1, 0, 0, 0, 0, 0, 0, 0],
                                  [first + gap, 2, held, 0, 0, 0, 0, 0, 0, 0]]
                    groups[2 if late else 1].append(c)
    with tempfile.TemporaryDirectory(prefix='player-melee-source-') as temporary:
        directory = Path(temporary)
        (directory/'reference.cpp').write_text(code)
        (directory/'distance.bin').write_bytes(distance)
        subprocess.run(['g++', '-m32', '-std=c++17', '-O0', '-fwrapv', '-mfpmath=sse', '-msse2', '-I', str(root/'src'),
                        str(directory/'reference.cpp'), '-o', str(directory/'reference')], check=True)
        cases = []
        for variant, group in enumerate(groups):
            (directory/'moves.bin').write_bytes(variants[variant])
            lines = [' '.join(map(str, c['input']+[len(c['steps'])]+[v for step in c['steps'] for v in step])) for c in group]
            outputs = subprocess.run([str(directory/'reference'), str(directory/'distance.bin'), str(directory/'moves.bin')],
                                     input='\n'.join(lines), capture_output=True, text=True, check=True).stdout.splitlines()
            for case_value, output in zip(group, outputs, strict=True):
                case_value['expected'] = json.loads(output)
                case_value['variant'] = variant
            cases.extend(group)
    destination = root/'modern/OpenSpidey.Tests/Fixtures/player-melee.json'
    destination.write_text(json.dumps({'exeSha256': hashlib.sha256(image).hexdigest(),
        'scope': 'Compiled recovered full InitiateCombo/UpdateAndTrackCombo/PunchCombo; real unrelocated PC fight blobs; injected pose/world/contact/effect boundaries.',
        'distanceHex': distance.hex(), 'movesHex': moves.hex(),
        'variants': [{'scope': 'Fixture-only valid multi-press records; original source algorithm, not gameplay data.', 'movesHex': blob.hex()} for blob in variants[1:]],
        'cases': cases}, separators=(',', ':'))+'\n')
    print(f'Compiled original combo methods with original fight blobs: {len(cases)} sequences to {destination}')


if __name__ == '__main__':
    main()
