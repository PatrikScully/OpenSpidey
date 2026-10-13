"""Capture actual PC CThug TugImpulse/GetLaunched/WallHitCheck with typed path/procedure leaves."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True);args=parser.parse_args()
    root=Path(__file__).resolve().parent.parent
    code=r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
#include <cstring>
std::vector<int>events;int pathCode,addFailure,addCount;CVector hit;
void emit(std::initializer_list<int>v){events.insert(events.end(),v);}
void patch(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
int __attribute__((thiscall)) path(void*,CVector*from,CVector*to,CVector*out,int clearance){emit({1,from->vx,from->vy,from->vz,to->vx,to->vy,to->vz,clearance});*out=hit;return pathCode;}
int __attribute__((thiscall)) add(void*,CVector*position,int argument){emit({2,position->vx,position->vy,position->vz,argument});return ++addCount!=addFailure;}
void __attribute__((thiscall)) neutralize(void*){emit({3});}
void* __attribute__((thiscall)) look(void*self,void*,void*,CVector*target,int mask,int rate,int tolerance){emit({4,target->vx,target->vy,target->vz,mask,rate,tolerance});return self;}
int sound(unsigned sound,CVector*position,int pitch){emit({5,int(sound),position->vx,position->vy,position->vz,pitch});return 0;}
void release(void*){emit({6});}
alignas(4)unsigned char storage[64];void*allocate(unsigned){return storage;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x403310,reinterpret_cast<void*>(&path));patch(0x4048F0,reinterpret_cast<void*>(&add));patch(0x4040E0,reinterpret_cast<void*>(&neutralize));
patch(0x401180,reinterpret_cast<void*>(&look));patch(0x471EA0,reinterpret_cast<void*>(&sound));patch(0x458210,reinterpret_cast<void*>(&release));patch(0x455390,reinterpret_cast<void*>(&allocate));
for(auto entry:std::vector<std::pair<unsigned,int>>{{0x4D3807,6},{0x4D380E,7},{0x4D38CB,7}})std::memset(reinterpret_cast<void*>(entry.first),0x90,entry.second);
int method,health,pathPresent,oldPath,hitX,hitZ,keepFacing;unsigned behavior;CVector direction;
while(std::cin>>method>>health>>behavior>>pathPresent>>oldPath>>pathCode>>hitX>>hitZ>>addFailure>>keepFacing>>direction.vx>>direction.vy>>direction.vz){
alignas(4)unsigned char raw[0x3A8]={};CVector position(123456,-54321,76543),old(1,2,3),points[8]{};hit=position+CVector(hitX,0,hitZ);events.clear();addCount=0;
*reinterpret_cast<CVector*>(raw+8)=position;*reinterpret_cast<CVector*>(raw+0x60)=CVector(55,66,77);*reinterpret_cast<short*>(raw+0xE2)=health;
*reinterpret_cast<unsigned*>(raw+0x2A8)=behavior;*reinterpret_cast<unsigned*>(raw+0x218)=0xFFFFFFFF;*reinterpret_cast<short*>(raw+0x21E)=100;
*reinterpret_cast<int*>(raw+0x1F8)=777;*reinterpret_cast<int*>(raw+0x31C)=7;*reinterpret_cast<int*>(raw+0x320)=8;*reinterpret_cast<void**>(raw+0x3A4)=oldPath?&old:nullptr;
int result=method?reinterpret_cast<int(__attribute__((thiscall)) *)(void*,CVector*,int,int,int)>(0x4D3800)(raw,&direction,keepFacing,80,0):
reinterpret_cast<unsigned char(__attribute__((thiscall)) *)(void*,CVector*,CVector*,CVector*)>(0x4D3940)(raw,&position,&direction,pathPresent?points:nullptr);
auto*velocity=reinterpret_cast<CVector*>(raw+0x60);std::cout<<'['<<result<<','<<*reinterpret_cast<short*>(raw+0xE2)<<','<<*reinterpret_cast<unsigned*>(raw+0x2A8)<<','<<*reinterpret_cast<unsigned*>(raw+0x218)<<','<<*reinterpret_cast<int*>(raw+0x1F8)<<','<<*reinterpret_cast<int*>(raw+0x31C)<<','<<*reinterpret_cast<int*>(raw+0x320)<<','<<velocity->vx<<','<<velocity->vy<<','<<velocity->vz<<','<<(*reinterpret_cast<void**>(raw+0x3A4)?1:0)<<",[";
for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    cases=[]
    for method in [0,1]:
        for health in [-1,0,1,50,51]:
            for behavior in [8,0x10000018]:
                for path_present,old_path in [(0,0),(1,0),(1,1)]:
                    for path_code,hit_x,hit_z in [(1,400000,200000),(4,0,0),(2,123450,300),(2,-400000,69134),(2,3000000,-4000000)]:
                        for add_failure in [0,1,2]:
                            for keep_facing in [0,1]:
                                cases.append([method,health,behavior,path_present,old_path,path_code,hit_x,hit_z,add_failure,keep_facing,12345,-23456,34567])
    with tempfile.TemporaryDirectory(prefix='openspidey-thug-web-tug-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,case)) for case in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-web-tug.json').write_text(json.dumps([dict(input=case,expected=json.loads(line)) for case,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} actual PC thug virtual tug, launch and wall-check cases; path/procedure/audio leaves are explicit boundaries.')


if __name__=='__main__':main()
