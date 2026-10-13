"""Capture original42B3/42B4 with actual variable reads, raw three-list signals and direct pulse routing."""
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
template<class T>T&at(void*body,unsigned offset){return *reinterpret_cast<T*>((unsigned char*)body+offset);}
int reads;std::vector<int>spawns;
short __attribute__((thiscall)) variable(void*body,unsigned short code){reads++;return reinterpret_cast<short(__attribute__((thiscall)) *)(void*,unsigned short)>(0x4072A0)(body,code);}
void create(int node){spawns.push_back(node);}
void jump(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<int*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Remove Windows SEH only; command decode/resolve and all three raw list scans stay original.
 std::memset((void*)0x4050B0,0x90,6);*reinterpret_cast<unsigned short*>(0x4050B0)=0xC031;
 for(unsigned address:{0x4050BEu,0x406ACBu})std::memset((void*)address,0x90,7);
 *reinterpret_cast<unsigned char*>(0x4015B0)=0xC3;
 // The actual object factory is the sole effect boundary replaced by a request recorder.
 jump(0x4DEE70,(void*)&create);
 int opcode,operand,resolved,kind;
 while(std::cin>>opcode>>operand>>resolved>>kind){
  unsigned short targetKind=kind,emptyKind=3,selector[]={1,203,4096,1,static_cast<unsigned short>(resolved)};
  std::vector<unsigned short*>nodes(65536,&emptyKind);nodes[resolved]=&targetKind;nodes[77]=selector;
  *reinterpret_cast<unsigned short***>(0x6B466C)=nodes.data();*reinterpret_cast<unsigned*>(0x6B4670)=nodes.size();
  *reinterpret_cast<unsigned short*>(0x54D474)=resolved;
  alignas(4)unsigned char bodies[10][0x300]={};
  for(auto&body:bodies){int id,flags;std::cin>>id>>flags;at<unsigned short>(body,0xDE)=id;at<unsigned short>(body,0x44)=flags;}
  for(int start:{0,3,6})for(int i=start;i<start+2;i++)at<void*>(bodies[i],0x20)=bodies[i+1];
  *reinterpret_cast<void**>(0x56E994)=bodies[0];*reinterpret_cast<void**>(0x56E990)=bodies[3];*reinterpret_cast<void**>(0x60DAAC)=bodies[6];
  unsigned short script[]={0xFFFF};alignas(4)unsigned char point[24]={};at<void*>(point,0)=script;
  at<unsigned char>(point,7)=255;at<unsigned short>(point,10)=resolved;
  *reinterpret_cast<void**>(0x6B4708)=point;std::memset((void*)0x6B4688,0,128);
  alignas(4)unsigned char caller[0x330]={};void*vtable[17]={};vtable[16]=(void*)&variable;at<void*>(caller,0)=vtable;
  at<unsigned short>(caller,0xDE)=resolved;unsigned short words[]={static_cast<unsigned short>(operand),77};at<unsigned short*>(caller,0x24C)=words;
  reads=0;spawns.clear();int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,unsigned short)>(0x4050B0)(caller,opcode);
  std::cout<<"{\"result\":"<<result<<",\"consumed\":"<<(reinterpret_cast<unsigned char*>(at<unsigned short*>(caller,0x24C))-reinterpret_cast<unsigned char*>(words))<<",\"reads\":"<<reads<<",\"signals\":[";
  for(int i=0;i<10;i++){if(i)std::cout<<',';std::cout<<at<unsigned short>(bodies[i],0x44);}std::cout<<"],\"spawns\":[";
  for(unsigned i=0;i<spawns.size();i++){if(i)std::cout<<',';std::cout<<spawns[i];}std::cout<<"],\"pending\":[";
  int queued=0;for(unsigned i=0;i<16;i++)if(*reinterpret_cast<void**>(0x6B468C+i*8)){if(queued++)std::cout<<',';std::cout<<*reinterpret_cast<unsigned short*>(0x6B4688+i*8);}
  std::cout<<"],\"pulses\":"<<static_cast<int>(at<unsigned char>(point,7))<<"}\n";
 }
}
'''
    ids = [2, 3, 2, 65535, 42, 2, 2, 65535, 42, 2]
    cases = []
    for opcode in [0x42B3, 0x42B4]:
        for operand, resolved in [(2, 2), (42, 42), (0x8000, 0x8000), (0x212A, 2),
                                  (0x212A, 65535), (0x2139, 32768), (0x212B, 42)]:
            for kind in [1, 7, 5, 20, 6, 3, 0, 255]:
                # SourceFFFF is never a valid command point: GetCommandPoint
                # rejects it before dereferencing. Keep the real invariant.
                if opcode == 0x42B4 and resolved == 65535 and kind == 6:
                    continue
                for signals in [[0] * 10, [0, 2, 4, 65535, 0x8000, 0xFFFE, 6, 8, 10, 0xA000], [65535] * 10]:
                    cases.append(dict(opcode=opcode, operand=operand, resolved=resolved, kind=kind, ids=ids, signals=signals))
    rows = [' '.join(map(str, [case['opcode'], case['operand'], case['resolved'], case['kind'],
                              *(value for pair in zip(case['ids'], case['signals']) for value in pair)])) for case in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-direct-events-reference-') as directory:
        cpp, binary = Path(directory)/'reference.cpp', Path(directory)/'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-m32', '-fno-pie', '-no-pie', '-I', str(root/'src'),
                        '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows)+'\n').encode())
    records = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/actor-direct-events.json').write_text(json.dumps(records, separators=(',', ':'))+'\n')
    print(f'Captured {len(records)} original42B3/42B4 commands: genuine variable reads, kind-independent raw signals, active/suspended identity and factory/queue routing.')


if __name__ == '__main__':
    main()
