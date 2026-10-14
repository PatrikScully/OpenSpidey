"""Capture CScorpion AI488590's boss-camera entry/exit calls against the real CCamera members, followed by whole original 417CB0 frames."""
import argparse
import json
import math
from pathlib import Path
import random
import subprocess
import tempfile

# Per-frame command kinds, shared with CameraAiTests.cs; the last two repeat the source call order of 488590.
NONE, SET_MODE, FIXED_POS, FIXED_POS_ANGLES, FIXED_FOCUS, CAM_ANGLE, SHAKE, TRIPOD_MOTION, XZ, Y, X_OFFSET, Y_OFFSET, Z_OFFSET, BOSS_ENTER, BOSS_EXIT = range(15)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <iostream>
#include <cmath>
#include <vector>
#include <array>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
template<class T>T&mem(unsigned n){return *reinterpret_cast<T*>(n);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
#define CALL(addr,...) reinterpret_cast<void(__attribute__((thiscall))*)(void*,##__VA_ARGS__)>(addr)
alignas(4)unsigned char item[0x40];
struct Hit{int present,distance,x,y,z,nx,ny,nz;};std::vector<Hit>script;unsigned cursor;std::vector<int>events;int diagnostics,headingCalls,heading,unknown;
void diagnostic(int condition,const char*message){if((condition&255)==0){if(!strcmp(message,"No valid tripod"))diagnostics++;else unknown++;}}
void init(int*line){}
int effectiveHeading(){headingCalls++;return heading;}
void query(int*line,int flag){assert(flag==1&&mem<int>(0x5FBEDC)==1);events.insert(events.end(),{line[0],line[1],line[2],line[3],line[4],line[5]});
 Hit hit=cursor<script.size()?script[cursor]:Hit{};cursor++;
 at<void*>(line,0x68)=hit.present?item:nullptr;at<int>(line,0x40)=hit.distance;at<int>(line,0x6C)=hit.x;at<int>(line,0x70)=hit.y;at<int>(line,0x74)=hit.z;
 at<short>(line,0x78)=hit.nx;at<short>(line,0x7A)=hit.ny;at<short>(line,0x7C)=hit.nz;}
int savedFlag;unsigned short savedH;short savedV;
long long next(){long long v;std::cin>>v;return v;}
void readVector(void*p,unsigned offset){for(int i=0;i<3;i++)at<int>(p,offset+4*i)=next();}
void out(int value){std::cout<<','<<value;}
void outInts(void*p,unsigned offset,int count){for(int i=0;i<count;i++)out(at<int>(p,offset+4*i));}
// Transition globals: value, mirrored gWtfCam value, target, rate, remaining.
const unsigned transitions[5][5]={{0x548860,0x56F1A4,0x56F0DC,0x56F0EC,0x56F3BC},{0x548864,0x56F1A8,0x56F258,0x56F290,0x56F3C0},
 {0x56F3C4,0x56F198,0x56F3A4,0x56EFF8,0x56F3C8},{0x548868,0x56F19C,0x56F11C,0x56F120,0x56F3CC},{0x56F3D0,0x56F1A0,0x56F078,0x56F394,0x56F3D4}};
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x4015B0,(void*)&diagnostic);patch(0x4524C0,(void*)&init);patch(0x4549A0,(void*)&query);patch(0x4C6AA0,(void*)&effectiveHeading);
// Shake tables come from static initializers that loadOriginalImage does not run (camera.cpp BigShakeAmp..LandShakeSpeed).
for(auto[a,x,y,z]:{std::array<int,4>{0x56F072,25,0,50},{0x56F068,0,0,30},{0x56F2B8,0,0,25},{0x56F2C8,0,0,16},{0x56EFF0,600,0,600}}){mem<short>(a)=x;mem<short>(a+2)=y;mem<short>(a+4)=z;}
mem<unsigned char>(0x56F34C)=3;mem<unsigned char>(0x56F34D)=0;mem<unsigned char>(0x56F34E)=3;
for(int i=0;i<4096;i++){reinterpret_cast<short*>(0x610C48)[2*i]=sin(i*0.001536096911877394)*4096;reinterpret_cast<short*>(0x610C48)[2*i+1]=cos(i*0.001536096911877394)*4096;}
long long marker;while(std::cin>>marker){
 alignas(4)static unsigned char camera[0x300],tripod[0x100],target[0x100],boss[0x100],player[0x1000];
 memset(camera,0,sizeof camera);memset(tripod,0,sizeof tripod);memset(target,0,sizeof target);memset(boss,0,sizeof boss);memset(player,0,sizeof player);
 mem<int>(0x5FAE98)=0;mem<int>(0x2E0988C)=0;mem<void*>(0x6A9038)=player;mem<void*>(0x56F3B8)=camera;mem<int>(0x5FBEDC)=0;
 at<unsigned char>(camera,0xF8)=1;at<unsigned char>(camera,0x23C)=1;at<int>(camera,0x12C)=-1;
 at<unsigned>(camera,0x2A0)=next();readVector(camera,8);for(int i=0;i<4;i++)at<int>(camera,0x1E4+4*i)=next();for(int i=0;i<4;i++)at<int>(camera,0x1F4+4*i)=next();
 at<void*>(camera,0xFC)=next()?tripod:nullptr;at<unsigned char>(camera,0x100)=next();readVector(camera,0x104);
 at<int>(camera,0x128)=next();readVector(camera,0x110);readVector(camera,0x11C);for(int i=0;i<3;i++)at<int>(camera,0x130+4*i)=next();
 int focusOwner=next();at<void*>(camera,0x13C)=focusOwner==1?(void*)tripod:focusOwner==2?(void*)target:nullptr;at<unsigned char>(camera,0x140)=next();
 readVector(camera,0x144);at<int>(camera,0x168)=next();readVector(camera,0x150);readVector(camera,0x15C);
 at<int>(camera,0x170)=next();at<unsigned short>(camera,0x174)=next();at<int>(camera,0x178)=next();at<int>(camera,0x17C)=next();
 at<int>(camera,0x1D0)=next();at<int>(camera,0x1D4)=next();
 at<short>(camera,0x234)=next();at<short>(camera,0x236)=next();at<short>(camera,0x238)=next();at<int>(camera,0x2A4)=next();at<int>(camera,0x2A8)=next();
 for(int i=0;i<3;i++)at<short>(camera,0x270+2*i)=next();for(int i=0;i<3;i++)at<unsigned char>(camera,0x276+i)=next();for(int i=0;i<3;i++)at<short>(camera,0x27A+2*i)=next();
 for(int t=0;t<5;t++){int value=next();mem<int>(transitions[t][0])=value;mem<int>(transitions[t][1])=value;mem<int>(transitions[t][2])=next();mem<int>(transitions[t][3])=next();mem<int>(transitions[t][4])=next();}
 mem<int>(0x56F1AC)=next();mem<int>(0x56F38C)=next();mem<int>(0x56F28C)=next();mem<int>(0x56F254)=next();
 mem<int>(0x548858)=next();mem<int>(0x54885C)=next();
 at<unsigned short>(player,4)=next();at<unsigned char>(player,0x8E8)=next();at<int>(player,0x60)=next();at<int>(player,0x68)=next();at<unsigned char>(player,0x57C)=next();heading=next();
 int hits=next();script.assign(hits,{});for(Hit&h:script){h.present=next();h.distance=next();h.x=next();h.y=next();h.z=next();h.nx=next();h.ny=next();h.nz=next();}
 cursor=0;events.clear();diagnostics=0;headingCalls=0;unknown=0;savedFlag=0;savedH=0;savedV=0;
 int frames=next();std::cout<<'[';
 for(int frame=0;frame<frames;frame++){
  at<int>(camera,0x80)=next();readVector(tripod,8);readVector(target,8);readVector(boss,8);mem<void*>(0x56E998)=next()?boss:nullptr;mem<int>(0x60CFA8)=next();
  int kind=next();int a[8];for(int i=0;i<8;i++)a[i]=next();
  int vector[3]={a[0],a[1],a[2]};int quat[4]={a[3],a[4],a[5],a[6]};
  switch(kind){
   case 0:break;
   case 1:reinterpret_cast<int(__attribute__((thiscall))*)(void*,int)>(0x4167F0)(camera,a[7]);break;
   case 2:CALL(0x416370,int*,int)(camera,vector,a[7]);break;
   case 3:CALL(0x416410,int*,int*,int)(camera,vector,quat,a[7]);break;
   case 4:CALL(0x4166C0,int*,int,int)(camera,vector,a[3],a[7]);break;
   case 5:CALL(0x4178E0,int,int)(camera,a[3],a[7]);break;
   case 6:CALL(0x416880,int*,int)(camera,vector,a[7]);break;
   case 7:CALL(0x417710,int*,unsigned)(camera,vector,a[7]);break;
   case 8:CALL(0x4179F0,int,int)(camera,a[3],a[7]);break;
   case 9:CALL(0x417A70,int,int)(camera,a[3],a[7]);break;
   case 10:CALL(0x417AE0,int,int)(camera,a[3],a[7]);break;
   case 11:CALL(0x417B60,int,int)(camera,a[3],a[7]);break;
   case 12:CALL(0x417BE0,int,int)(camera,a[3],a[7]);break;
   case 13:if(!savedFlag){savedH=reinterpret_cast<short(*)()>(0x4179E0)();savedV=reinterpret_cast<short(*)()>(0x417A60)();savedFlag=1;}
    reinterpret_cast<int(__attribute__((thiscall))*)(void*,int)>(0x4167F0)(camera,16);at<int>(camera,0x2A4)=127;
    CALL(0x4179F0,int,int)(camera,savedH,0);CALL(0x417A70,int,int)(camera,savedV,0);at<int>(camera,0x2A8)=0;mem<void*>(0x56E998)=boss;break;
   case 14:savedH=reinterpret_cast<short(*)()>(0x4179E0)();savedV=reinterpret_cast<short(*)()>(0x417A60)();
    reinterpret_cast<int(__attribute__((thiscall))*)(void*,int)>(0x4167F0)(camera,3);savedFlag=1;mem<void*>(0x56E998)=nullptr;break;
   default:assert(false);
  }
  CALL(0x417CB0)(camera);
  assert(mem<int>(0x5FBEDC)==0&&unknown==0);
  void*focus=at<void*>(camera,0x13C);
  std::cout<<(frame?",[":"[")<<at<unsigned>(camera,0x2A0);outInts(camera,8,3);outInts(camera,0x60,3);outInts(camera,0x104,3);outInts(camera,0x144,3);
  outInts(camera,0x1E4,4);outInts(camera,0x1F4,4);outInts(camera,0x214,4);out(at<short>(camera,0x14));out(at<short>(camera,0x16));
  out(at<short>(camera,0x236));out(at<short>(camera,0x23A));out(at<int>(camera,0x170));out(at<unsigned short>(camera,0x174));
  out(mem<int>(0x548858));out(mem<int>(0x54885C));for(int i=0;i<3;i++)out(mem<int>(0x56F260+4*i));
  out(at<int>(camera,0x1D0));out(at<int>(camera,0x1D4));out(at<unsigned char>(camera,0x180));outInts(camera,0x1B8,3);out(at<unsigned short>(player,4));
  out(focus==tripod?1:focus==target?2:focus==boss?3:focus==nullptr?0:-1);out(at<int>(camera,0x128));out(at<int>(camera,0x168));out(at<int>(camera,0x2BC));
  for(int i=0;i<3;i++)out(at<short>(camera,0x270+2*i));for(int t=0;t<5;t++){out(mem<int>(transitions[t][0]));out(mem<int>(transitions[t][4]));}
  out(mem<int>(0x56F1AC));out(mem<int>(0x56F254));out(diagnostics);out(headingCalls);
  out(at<int>(camera,0x2A4));out(at<int>(camera,0x2A8));out(savedFlag);out(savedH);out(savedV);out(mem<void*>(0x56E998)==boss);std::cout<<']';
 }
 std::cout<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
}}
'''
    rng = random.Random(0x488590)

    def quaternion():
        values = [rng.gauss(0, 1) for _ in range(4)]
        length = math.sqrt(sum(value * value for value in values))
        return [round(value / length * 4096) for value in values]

    def unit():
        values = [rng.gauss(0, 1) for _ in range(3)]
        length = math.sqrt(sum(value * value for value in values))
        return [round(value / length * 4096) for value in values]

    def near(base, spread):
        return [value + rng.randint(-spread, spread) for value in base]

    def hit():
        if rng.random() < 0.4: return [0] * 8
        normal = unit()
        if rng.random() < 0.3: normal[1] = rng.choice([-2601, 0, 3400, 4096])
        return [1, rng.choice([rng.randint(0, 900), 0x7FFFFFFF]), *[rng.randint(-2 ** 26, 2 ** 26) for _ in range(3)], *normal]

    def command(state, body):
        roll = rng.random()
        none = [NONE, 0, 0, 0, 0, 0, 0, 0, 0]
        if roll < 0.45: return none
        if roll < 0.8:
            kind = BOSS_EXIT if state['boss'] else BOSS_ENTER
            state['boss'] = kind == BOSS_ENTER
            return [kind, 0, 0, 0, 0, 0, 0, 0, 0]
        kind = rng.choice([SET_MODE, CAM_ANGLE, XZ, Y, X_OFFSET, Y_OFFSET, Z_OFFSET])
        position = near(body, 2000 << 12)
        # CM_FixedPosAngles divides by duration0x2C0, which only a nonzero quaternion setter writes.
        if kind == SET_MODE: return [kind, 0, 0, 0, 0, 0, 0, 0, rng.choice([3, 3, 15, 16, 17])]
        if kind == FIXED_POS: return [kind, *position, 0, 0, 0, 0, rng.choice([0, 1, 7, 30, 64])]
        if kind == FIXED_POS_ANGLES:
            frames = rng.choice([0, 1, 7, 30, 48]); state['duration'] |= frames != 0
            return [kind, *position, *quaternion(), frames]
        if kind == FIXED_FOCUS: return [kind, *position, rng.choice([0, 300, 1200, 1500, 2400, 65535]), 0, 0, 0, rng.choice([0, 5, 60])]
        if kind == CAM_ANGLE: return [kind, 0, 0, 0, rng.randint(-32768, 32767), 0, 0, 0, rng.choice([0, 0, 1, 16, 60])]
        if kind == SHAKE: return [kind, *position, 0, 0, 0, 0, rng.randrange(4)]
        if kind == TRIPOD_MOTION: return [kind, *position, 0, 0, 0, 0, rng.choice([1, 4, 30])]
        if kind == XZ: return [kind, 0, 0, 0, rng.choice([400, 512, 700, 800, 65000]), 0, 0, 0, rng.choice([0, 3, 30])]
        if kind == Y: return [kind, 0, 0, 0, rng.choice([-128, 0, 100, -300, 32767]), 0, 0, 0, rng.choice([0, 3, 30])]
        return [kind, 0, 0, 0, rng.randint(-600, 600), 0, 0, 0, rng.choice([0, 3, 30])]

    cases = []
    for index in range(400):
        mode = rng.choice([3, 3, 3, 16])
        body = [rng.randint(-2 ** 26, 2 ** 26) for _ in range(3)]
        current = quaternion()
        horizontal, vertical = rng.choice([512, 700, 800, rng.randint(200, 1500)]), rng.choice([-128, 0, 100, rng.randint(-400, 400)])
        transitions = [[horizontal, 0, 0, 0], [vertical, 0, 0, 0]]
        if rng.random() < 0.3: transitions[0] = [horizontal, horizontal + rng.randint(-300, 300), rng.randint(-20, 20), rng.choice([0, 1, 9, 30])]
        if rng.random() < 0.3: transitions[1] = [vertical, vertical + rng.randint(-300, 300), rng.randint(-20, 20), rng.choice([0, 1, 9, 30])]
        for _ in range(3):
            value = rng.choice([0, 0, rng.randint(-300, 300)])
            transitions.append([value, value + rng.randint(-200, 200), rng.randint(-60000, 60000), rng.choice([0, 0, 2, 30])]
                               if rng.random() < 0.3 else [value, 0, 0, 0])
        yaw = rng.randrange(4096)
        heading = [yaw, rng.randrange(4096), rng.randint(-60, 60), rng.choice([0, 0, 0, 1, 16, 60])]
        row = [mode, *near(body, 900 << 12), *current, *rng.choice([current, quaternion()]),
               1, rng.choice([1, 1, 1, 0]), *near(body, 50 << 12),
               rng.choice([0, 0, 0, 5]), *near(body, 300 << 12), *[rng.randint(-2 ** 16, 2 ** 16) for _ in range(3)],
               *rng.choice([[4, 8, 4], [16, 16, 16], [rng.randint(0, 16) for _ in range(3)]]),
               rng.choice([1, 1, 2, 0]), rng.choice([1, 1, 0]), *near(body, 50 << 12),
               rng.choice([0, 0, 0, 4]), *near(body, 300 << 12), *[rng.randint(-2 ** 16, 2 ** 16) for _ in range(3)],
               rng.choice([2365, 1733]), rng.choice([0, 0, 19]), rng.randint(-40, 40), rng.choice([2365, 1733]),
               rng.choice([0, 200, rng.randint(0, 2000)]), rng.randint(0, 2000),
               rng.choice([0, 0, 0, rng.randint(-300, 300)]), yaw, rng.choice([0, 0, 0, rng.randint(-300, 300)]),
               rng.choice([31, 127]), rng.choice([512, 0]),
               *rng.choice([[0, 0, 0], [25, 0, 50], [0, 0, 30], [rng.randint(-60, 60) for _ in range(3)]]),
               *rng.choice([[3, 0, 3], [rng.randint(0, 9) for _ in range(3)]]), *rng.choice([[600, 0, 600], [rng.randint(-900, 900) for _ in range(3)]]),
               *[value for transition in transitions for value in transition], *heading,
               rng.choice([300, rng.randint(-4096, 4096)]), rng.choice([200, rng.randint(0, 1500)]),
               rng.randrange(65536), rng.choice([0, 0, 0, 1]), *rng.choice([[0, 0], [rng.randint(-2 ** 14, 2 ** 14) for _ in range(2)]]),
               rng.choice([0, 0, 1]), rng.randint(-32768, 32767)]
        hits = [hit() for _ in range(rng.choice([0, 4, 12]))]
        frames = []
        position, focus, boss = body, near(body, 400 << 12), near(body, 3000 << 12)
        state = dict(duration=False, boss=False)
        for _ in range(rng.choice([4, 8, 12])):
            position = near(position, rng.choice([0, 1 << 12, 40 << 12]))
            focus = near(focus, 20 << 12)
            boss = near(boss, 30 << 12)
            present = int(state['boss'])
            frames.append([rng.choice([2, 2, 2, 1, 3]), *position, *focus, *boss, present, rng.randint(0, 100000), *command(state, position)])
        cases.append(dict(input=row, hits=hits, frames=frames))
    rows = [[0, *case['input'], len(case['hits']), *[value for hit in case['hits'] for value in hit], len(case['frames']),
             *[value for frame in case['frames'] for value in frame]] for case in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-original-scorpion-boss-camera-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        completed = subprocess.run([str(binary), str(args.original_exe.resolve())], text=True, capture_output=True,
                                   input='\n'.join(' '.join(map(str, row)) for row in rows) + '\n')
        if completed.returncode:
            done = len(completed.stdout.splitlines())
            raise RuntimeError(f"Original Scorpion boss camera fixture failed{completed.returncode} at case{done}: {completed.stderr}")
        output = completed.stdout.splitlines()
        if len(output) != len(cases): raise RuntimeError(f'Expected{len(cases)} cases, got{len(output)}')
    for case, line in zip(cases, output, strict=True):
        case['expected'] = json.loads(line)
    (root / 'modern/OpenSpidey.Tests/Fixtures/scorpion-boss-camera.json').write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    modes = {}
    for case in cases:
        for frame in case['expected'][:-1]: modes[frame[0]] = modes.get(frame[0], 0) + 1
    print(f'Captured {len(cases)} original Scorpion boss camera sequences, {sum(len(case["frames"]) for case in cases)} frames, '
          f'{sum(frame[12] in (BOSS_ENTER, BOSS_EXIT) for case in cases for frame in case["frames"])} entry/exit calls; '
          f'frames per mode {dict(sorted(modes.items()))}, rays {sum(len(case["expected"][-1]) // 6 for case in cases)}.')


if __name__ == '__main__': main()
