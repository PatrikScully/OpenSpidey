"""Run the entire original PC action16 and4BFBC0 at their real typed animation/guard boundaries."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
std::vector<long long>events;int accepted,webCalls,effective;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
int guard(int index){events.insert(events.end(),{1,index});return accepted==index;}
int __attribute__((thiscall)) ground(void*){return guard(1);}int __attribute__((thiscall)) web(void*){return guard(++webCalls==1?2:6);}int __attribute__((thiscall)) jump(void*){return guard(3);}unsigned char __attribute__((thiscall)) ceiling(void*){return guard(4);}int __attribute__((thiscall)) kick(void*){return guard(5);}int __attribute__((thiscall)) wall(void*){return guard(7);}unsigned char __attribute__((thiscall)) swing(void*){return guard(8);}unsigned char __attribute__((thiscall)) zip1(void*){return guard(9);}unsigned char __attribute__((thiscall)) zip2(void*){return guard(10);}int __attribute__((thiscall)) interior(void*){return guard(11);}int __attribute__((thiscall)) exterior(void*){return guard(12);}int __attribute__((thiscall)) fence(void*){return guard(13);}
void __attribute__((thiscall)) play(void*p,int clip,int first,int last){events.insert(events.end(),{2,clip,first,last});at<unsigned short>(p,0x12A)=clip;}
void __attribute__((thiscall)) stand(void*){events.push_back(3);}short __attribute__((thiscall)) heading(void*){events.push_back(4);return effective;}
void __attribute__((thiscall)) lock(void*p){events.push_back(5);at<int>(p,0xDF8)=0;}
void* sourcePlayer;
void __attribute__((thiscall)) smash(void*){assert(at<void*>(sourcePlayer,0xE48)==nullptr);events.push_back(6);}
extern "C" __attribute__((naked)) void leafEnd(){asm volatile("pop %edi;pop %esi;pop %ebp;pop %ebx;add $0x4B0,%esp;ret");}
extern "C" __attribute__((naked)) void leafStep(void*,int){asm volatile("sub $0x4B0,%esp;push %ebx;push %ebp;push %esi;push %edi;mov 0x4C4(%esp),%esi;mov 0x4C8(%esp),%eax;mov %eax,0x20(%esp);xor %ebx,%ebx;mov $0x4B2D43,%eax;jmp *%eax");}
void outputEvents(){std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x4C23A0,(void*)&ground);patch(0x4C0510,(void*)&web);patch(0x4C2090,(void*)&jump);patch(0x4C0B80,(void*)&ceiling);patch(0x4C00B0,(void*)&kick);patch(0x4C18A0,(void*)&swing);patch(0x4C0EE0,(void*)&zip1);patch(0x4C1460,(void*)&zip2);patch(0x4BEB70,(void*)&interior);patch(0x4BF070,(void*)&exterior);patch(0x4BE8C0,(void*)&fence);patch(0x4C9130,(void*)&play);patch(0x460C10,(void*)&play);patch(0x4BE4B0,(void*)&stand);patch(0x4C6AA0,(void*)&heading);patch(0x4C6960,(void*)&lock);patch(0x4B6E8C,(void*)&leafEnd);
std::memset((void*)0x6A82B8,0,300*4);void* smashVtable[6]={};smashVtable[5]=(void*)&smash;
int family;while(std::cin>>family){alignas(4)unsigned char player[0xEC0]={},camera[0x300]={},held[0x20]={},face[0x20]={};sourcePlayer=player;at<void*>(held,0)=smashVtable;*(void**)0x56F3B8=camera;events.clear();
if(family==0){long long v[14];for(auto&item:v)std::cin>>item;patch(0x4BFBC0,(void*)&wall);at<unsigned short>(player,0x12A)=v[0];at<void*>(player,0xE48)=v[2]?held:nullptr;at<unsigned short>(player,0xE0)=v[3];player[0xAD4]=v[4];player[0x8EA]=v[5];player[0xE2D]=v[6];player[0xE2E]=v[7];player[0xAD5]=v[8];player[0x8E8]=v[9];at<short>(player,0xE32)=v[10];at<short>(camera,0x23A)=v[11];effective=v[12];accepted=v[13];webCalls=0;at<short>(player,0xEA6)=456;at<unsigned>(player,0xE1C)=16;at<int>(player,0xDF8)=31;at<int>(player,0x60)=12345;at<int>(player,0x64)=-23456;at<int>(player,0x68)=34567;leafStep(player,v[1]);
std::cout<<'['<<at<short>(player,0xEA6)<<','<<(at<void*>(player,0xE48)!=nullptr)<<','<<at<unsigned>(player,0xE1C)<<','<<at<unsigned short>(player,0x12A)<<','<<at<int>(player,0x60)<<','<<at<int>(player,0x64)<<','<<at<int>(player,0x68)<<','<<at<int>(player,0xDF8)<<',';outputEvents();std::cout<<"]\n";
}else{long long v[13];for(auto&item:v)std::cin>>item;
// Restore the exact original CheckRunIntoWall instructions after action16's typed guard patch.
original_executable::restoreOriginalRange(0x4BFBC0,5);
at<void*>(player,0xE48)=v[0]?held:nullptr;at<unsigned short>(player,0xE0)=v[1];at<void*>(player,0xB74)=v[2]?held:nullptr;at<short>(player,0xB84)=v[3];at<short>(player,0xB86)=v[4];at<short>(player,0xB88)=v[5];at<void*>(player,0xB8C)=face;at<unsigned>(face,0xC)=v[6];at<int>(player,0xC6C)=v[7];at<int>(player,0xC70)=v[8];at<int>(player,0xC74)=v[9];player[0xAD7]=v[10];at<int>(player,0x80)=v[11];at<unsigned>(player,0xE1C)=16;at<unsigned short>(player,0x12A)=21;at<void*>(player,0x350)=(void*)1;
int result=reinterpret_cast<int(__attribute__((thiscall))*)(void*)>(0x4BFBC0)(player);
std::cout<<'['<<result<<','<<unsigned(player[0xAD7])<<','<<at<unsigned>(player,0xE1C)<<','<<at<unsigned short>(player,0x12A)<<','<<(at<void*>(player,0x350)!=nullptr)<<',';outputEvents();std::cout<<"]\n";
}}
}
'''
    # Guard outcomes include the second invocation of the same CheckWebShot function.
    walking = [[21, 65535, held, collision, crawling, active, -127, 0, 0, 0, 0, 0, 0, guard]
               for held in [0, 1] for collision in [0, 0x40] for crawling in [0, 255]
               for active in [0, 255] for guard in range(14)]
    walking += [[clip, done, 0, 0, 0, 0, primary, secondary, hard, wall_surface, delta, 0, 0, 0]
                for clip in [0, 1, 21, 25, 50, 51, 52, 57, 58, 91, 89, 197, 191]
                for done in [65535, 52, 51, 85] for hard in [0, 255] for wall_surface in [0, 255]
                for primary, secondary in [(0, 0), (-127, 0), (0, 127), (127, -127)]
                for delta in [0, 1536, 1537, 2048, 2559, 2560]]
    walking += [[clip, 65535, 0, 0, 0, 0, -127, 0, 0, wall_surface, input_heading, camera_heading, effective, 0]
                for clip in [21, 50, 51, 52] for wall_surface in [0, 1]
                for input_heading, camera_heading, effective in [(32767, -32768, -1), (-32768, 32767, 2048),
                    (-32768, -32768, -32768), (32767, 32767, 32767), (-2048, 0, 0)]]
    walls = [[held, collision, present, 4096, ny, 0, flags, 4096, 0, 0, counter, 2, 0]
             for held in [0, 1] for collision in [0, 1, 0x41] for present in [0, 1]
             for ny in [-2601, -2600, 0, 3400, 3401] for flags in [0, 0x40000] for counter in [0, 20, 21, 255]]
    walls += [[0, 1, 1, nx, 0, nz, 0, fx, fy, fz, counter, ticks, 0]
              for nx, nz, fx, fy, fz in [(4096, 0, 3800, 0, 0), (4096, 0, 3801, 0, 0),
                  (4096, 0, 4096, 0, 0), (4096, 0, -4096, 0, 0), (32767, -32768, 2147483647, 0, -2147483648)]
              for counter in [0, 19, 20, 21, 250, 255] for ticks in [0, 1, 2, 7, 255, 256, 65535, -1]]
    rows = [(0, 'walking', case) for case in walking] + [(1, 'wall', case) for case in walls]
    # The loader keeps one pristine loaded copy; save the first five bytes in the harness.
    code = code.replace('std::memset((void*)0x6A82B8',
                        'unsigned char originalWall[5];std::memcpy(originalWall,(void*)0x4BFBC0,5);std::memset((void*)0x6A82B8')
    code = code.replace('original_executable::restoreOriginalRange(0x4BFBC0,5);',
                        'std::memcpy((void*)0x4BFBC0,originalWall,5);')
    with tempfile.TemporaryDirectory(prefix='openspidey-walking-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root/'src'), '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        inputs = '\n'.join(str(family)+' '+' '.join(map(str, case)) for family, _, case in rows)+'\n'
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=inputs.encode()).decode().splitlines()
    fixture = [dict(kind=kind, input=case, expected=json.loads(line))
               for (_, kind, case), line in zip(rows, lines, strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/player-walking.json').write_text(json.dumps(fixture, separators=(',', ':'))+'\n')
    print(f'Captured {len(walking)} entire original PC action16 calls and {len(walls)} RunIntoWall calls, including exact guard order, held-object smash, byte overflow and original reverse clips.')


if __name__ == '__main__':
    main()
