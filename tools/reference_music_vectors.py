"""Execute untouched PC472150/471D40 voice mixing and whole459650 music scheduling against original image tables."""
import argparse
import json
from pathlib import Path
import random
import re
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/engine/audio/music.cpp').read_text()
    arrays = {name: [int(x, 0) for x in re.findall(r'0x[0-9A-Fa-f]+|\d+', values)]
              for name, values in re.findall(r'static const i32 (\w+)\[\d+\] = \{(.*?)\};', source, re.S)}
    voice = []
    for allocated in [0, 0xFFFFFFFE, 2, 0x80000000, 0x55555554, 0xAAAAAAAA]:
        for handle in [0, 1, 2, 3, 4, 6, 0x80000000, 0x80000002, 0xFFFFFFFF]:
            for volume in [-32768, -16384, -1, 0, 1, 4096, 16383, 16384, 32767]:
                voice.append([handle, volume, allocated])

    def row(level=0x103, **values):
        state = dict(level=level, vblank=1, post=0, training=0, player=1, track_clock=0, track_index=0,
                     ending=255, intro_clock=0, intro_index=0, phase=0, last=0, selected=0, variant=0,
                     completed=1, nearby=1, volume=4096, combat=0, player_clock=0, handle=0x80000000,
                     enabled=1, replay=300, gain=4096, down=0, up=0, play_result=0x40000000)
        state.update(values)
        return list(state.values())

    music = []
    for level in [0x101, 0x102, 0x103, 0x104, 0x301, 0x303, 0x304, 0x305, 0x503, 0x504, 0x506,
                  0x507, 0x703, 0x801, 0x803, 0x100, 0x105, 0x505, 0x804, 0x806, 0x1702, -1]:
        for clock in [0, 300, 301, -1, 2147483647]:
            for nearby, volume in [(0, 0), (0, 2048), (0, 2049), (0, 12288), (0, 12289), (1, 0), (5, 4096), (-12, 4096)]:
                music.append(row(level, player_clock=clock, nearby=nearby, volume=volume, down=1, gain=2048))
    # Every entry/end boundary and both actual phase tables, including the1701 nonzero wrap.
    for name, level, phase in [('0201', 0x201, 0), ('0202a', 0x202, 0), ('0202b', 0x202, 1), ('0302', 0x302, 0),
            ('0401', 0x401, 0), ('0501', 0x501, 0), ('0502', 0x502, 0), ('0604', 0x604, 0),
            ('0701', 0x701, 0), ('0702', 0x702, 0), ('0704', 0x704, 0), ('0705a', 0x705, 0), ('0705b', 0x705, 1),
            ('0802', 0x802, 0), ('0805', 0x805, 0), ('0901', 0x901, 0), ('1101', 0x1101, 0),
            ('1302', 0x1302, 0), ('1303', 0x1303, 0), ('1701', 0x1701, 0)]:
        for index, duration in enumerate(arrays['gMusicTimes' + name]):
            for timer in [0, duration - 5, duration - 4, duration - 3, duration - 1, duration, 6666, -1, 2147483647]:
                music.append(row(level, phase=phase, track_index=index, track_clock=timer))
    for level in [0x202, 0x705, 0x902, 0x904, 0x1102, 0x1104]:
        for phase in [-1, 0, 1, 2]:
            music.append(row(level, phase=phase, track_clock=6666))
    for variant in [-1, 0, 1, 2]:
        for selected in [0, 1, 2]:
            for index in range(3):
                for timer in [-1, 0, 64, 113, 177, 205, 207, 208, 209, 2147483647]:
                    music.append(row(0x506, variant=variant, selected=selected, intro_index=index, intro_clock=timer,
                                     completed=0, player_clock=301, nearby=1, up=1))
    for gain in [-2147483648, -1, 0, 2048, 4095, 4096, 4097, 2147483647]:
        for down in [0, 1, 255]:
            for up in [0, 1, 255]:
                for volume in [-2147483648, -1, 0, 16383, 32767, 2147483647]:
                    music.append(row(gain=gain, down=down, up=up, nearby=1, volume=volume))
    for vblank, last in [(0, 0), (1, 1), (0, 1), (0xFFFFFFFF, 0), (1, 0xFFFFFFFF), (0x80000000, 0)]:
        for level in [0x103, 0x201, 0x506, -1]:
            for post, training, player, enabled in [(0, 0, 1, 1), (1, 0, 1, 1), (0, 1, 1, 1),
                                                   (0, 0, 0, 1), (0, 0, 1, 0)]:
                music.append(row(level, vblank=vblank, last=last, post=post, training=training, player=player,
                                 enabled=enabled, player_clock=301, track_clock=6666, down=1, up=1))
    rng = random.Random(0x459650)
    for _ in range(256):
        music.append(row(rng.choice([0x101, 0x103, 0x506, 0x201, 0x1701]), vblank=rng.getrandbits(32),
                         last=rng.getrandbits(32), track_clock=rng.randrange(-2147483648, 2147483648),
                         player_clock=rng.randrange(-2147483648, 2147483648), nearby=rng.randrange(-100, 100),
                         volume=rng.randrange(-2147483648, 2147483648), gain=rng.randrange(-2147483648, 2147483648),
                         down=rng.randrange(256), up=rng.randrange(256), combat=rng.randrange(-1, 2),
                         play_result=rng.choice([0, 1, 0x80000000, 0xFFFFFFFF])))

    code = r'''
#include "original_executable.h"
#include <iostream>
template<class T>T&mem(unsigned address){return *reinterpret_cast<T*>(address);}
template<class T>T&at(void*p,unsigned offset){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+offset);}
void patch(unsigned address,void*function){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
struct Event{int kind;unsigned identity;int first,second;};std::vector<Event>events;int pans[32],volumes[32];
void diagnostic(){}void pan(int channel,int value){pans[channel]=value;}
void volume(int channel,int value){volumes[channel]=value;}
unsigned playResult;int level;int readLevel(){return level;}
unsigned play(unsigned sound,short volume,int pitch){events.push_back({0,sound,volume,pitch});return playResult;}
void voice(unsigned handle,short volume){events.push_back({1,handle,volume,0});}
unsigned introIds(int value){return value==1?0x54D86C:value==2?0x54D884:0;}
unsigned introTimes(int value){return value==1?0x54D878:value==2?0x54D890:0;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 patch(0x4015B0,(void*)&diagnostic);patch(0x504350,(void*)&pan);patch(0x5042F0,(void*)&volume);
 int family;bool musicPatched=false;while(std::cin>>family){if(family==0){unsigned handle,allocated;int value;std::cin>>handle>>value>>allocated;
  auto*entries=(unsigned char*)0x618730;memset(entries,0,32*40);for(int c=0;c<32;c++){at<unsigned short>(entries+c*40,0x16)=c?4096:0;at<unsigned char>(entries+c*40,0x1A)=(allocated>>c)&1;pans[c]=volumes[c]=-99;}
  ((void(*)(unsigned,short))0x472150)(handle,(short)value);
  std::cout<<"{\"stored\":[";for(int c=0;c<32;c++){if(c)std::cout<<',';std::cout<<at<unsigned short>(entries+c*40,0x16);}std::cout<<"],\"updates\":[";
  bool comma=false;for(int c=0;c<32;c++)if(pans[c]!=-99){if(comma)std::cout<<',';comma=true;std::cout<<'['<<c<<','<<at<unsigned short>(entries+c*40,0x16)<<','<<pans[c]<<','<<volumes[c]<<']';}std::cout<<"]}\n";continue;
 }
 if(!musicPatched){patch(0x4DE770,(void*)&readLevel);patch(0x471C50,(void*)&play);patch(0x472150,(void*)&voice);musicPatched=true;}
 long long v[26];for(auto&value:v)std::cin>>value;level=v[0];events.clear();playResult=v[25];
 mem<unsigned>(0x6B4CA0)=v[1];mem<int>(0x5FAE98)=v[2];mem<int>(0x60CFB0)=v[3];
 mem<int>(0x60D86C)=v[5];mem<int>(0x60D870)=v[6];mem<unsigned char>(0x60D874)=v[7];mem<int>(0x60D878)=v[8];mem<int>(0x60D87C)=v[9];
 mem<int>(0x60D880)=v[10];mem<unsigned>(0x60D884)=v[11];mem<unsigned>(0x60D888)=introIds(v[12]);mem<unsigned>(0x60D88C)=introTimes(v[12]);
 mem<int>(0x60D890)=v[13];mem<unsigned char>(0x54D868)=v[14];alignas(4)unsigned char player[0xD00]={};
 unsigned offsets[]={0x528,0x52C,0x534,0x530,0x538,0xC5C,0xC60,0xC64,0xC68,0xC69};
 for(int i=0;i<10;i++){if(i==5||i==8||i==9)at<unsigned char>(player,offsets[i])=v[15+i];else at<int>(player,offsets[i])=v[15+i];}
 mem<void*>(0x6A9038)=v[4]?player:nullptr;((void(*)())0x459650)();
 std::cout<<"{\"state\":["<<mem<int>(0x60D86C)<<','<<mem<int>(0x60D870)<<','<<unsigned(mem<unsigned char>(0x60D874))<<','<<mem<int>(0x60D878)<<','<<mem<int>(0x60D87C)<<','<<mem<int>(0x60D880)<<','<<mem<unsigned>(0x60D884)<<',';
 unsigned selected=mem<unsigned>(0x60D888);std::cout<<(selected==0?0:selected==0x54D86C?1:selected==0x54D884?2:-1)<<','<<mem<int>(0x60D890)<<','<<unsigned(mem<unsigned char>(0x54D868))<<"],\"player\":[";
 for(int i=0;i<10;i++){if(i)std::cout<<',';if(i==5||i==8||i==9)std::cout<<unsigned(at<unsigned char>(player,offsets[i]));else if(i==4)std::cout<<at<unsigned>(player,offsets[i]);else std::cout<<at<int>(player,offsets[i]);}
 std::cout<<"],\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';auto&e=events[i];std::cout<<'['<<e.kind<<','<<e.identity<<','<<e.first<<','<<e.second<<']';}std::cout<<"]}\n";
 }}
'''
    cases = [(0, values) for values in voice] + [(1, values) for values in music]
    with tempfile.TemporaryDirectory(prefix='openspidey-music-pc-') as directory:
        cpp, binary = Path(directory) / 'reference.cpp', Path(directory) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, [family, *values])) for family, values in cases) + '\n').encode()).decode().splitlines()
    fixture = [dict(family=family, input=values, expected=json.loads(line)) for (family, values), line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/source-music.json').write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    print(f'Captured {len(voice)} original voice-volume/mix cases and {len(music)} whole original music render cases.')


if __name__ == '__main__':
    main()
