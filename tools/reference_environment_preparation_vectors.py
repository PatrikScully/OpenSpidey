"""Capture complete original M3dInit_ParsePSX/DCModel preparation on explicit import boundary vectors."""
import argparse
import json
from pathlib import Path
import struct
import subprocess
import tempfile


def source_bank(version, model_flags, face_flags, collision, vertex_flags, pulse, wibble):
    out = bytearray(struct.pack('<III', version, 0, 2))
    for index in range(2): out.extend(struct.pack('<HhiiihhhHIII', 0, 0, index * 4096, 0, 0, 0, 0, 0, index, 0, 0, 0))
    table = len(out); out.extend(struct.pack('<III', 2, 0, 0))
    for index in range(2):
        struct.pack_into('<I', out, table + 4 + 4 * index, len(out))
        out.extend(struct.pack('<HHHHiiiiHH', model_flags, 4, 1, 1, 100, 0, 0, 0, 100, 65535))
        for x, y, z in [(0, 0, 0), (100, 0, 0), (0, 100, 0), (100, 100, 0)]: out.extend(struct.pack('<hhhH', x, y, z, vertex_flags))
        out.extend(struct.pack('<hhhH', 0, 0, 4096, 0))
        words = 9 if version == 0x20006 else 8
        flags = (face_flags & 0xFFFF) | words << 18
        out.extend(struct.pack('<IIIII', flags, 0x03020100, 0x030201, collision, index))
        out.extend(bytes(words * 4 - 20))
    struct.pack_into('<I', out, 4, len(out))
    if pulse: out.extend(struct.pack('<IIBBHII', 7, 12, 2, 2, 0, 0, 0))
    if wibble: out.extend(struct.pack('<IIIII', 6, 20, 12, 0, 0)); out.extend(struct.pack('<II', 0, 0))
    out.extend(struct.pack('<IIIIII', 0xFFFFFFFF, 0x102A9, 0x103A9, 2, 0x10A, 0x20A))
    return out


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <array>
#include <cmath>
#include <cstdlib>
#include <iostream>
#include <string>
#include <vector>
template<class T>T& at(void* raw,unsigned offset){return *reinterpret_cast<T*>(reinterpret_cast<unsigned char*>(raw)+offset);}
unsigned& ptr(unsigned address){return *reinterpret_cast<unsigned*>(address);}
void patch(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
std::vector<void*>allocated;
void*allocate(unsigned size){void*p=std::calloc(1,size);allocated.push_back(p);return p;}
void*allocateDc(unsigned size,int,int,int,int){return allocate(size);}
void diagnostic(int,const char*){}
int flags[3];int textureFlags(int index){assert(index>=1&&index<=3);return flags[index-1];}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 patch(0x4581A0,reinterpret_cast<void*>(&allocateDc));patch(0x505470,reinterpret_cast<void*>(&allocate));
 patch(0x4015B0,reinterpret_cast<void*>(&diagnostic));patch(0x50F210,reinterpret_cast<void*>(&textureFlags));
 // Host CRT pow is the same platform arithmetic boundary; all model preprocessing, DCFace conversion, vertex welding and flag branches remain original.
 patch(0x529779,reinterpret_cast<void*>(static_cast<double(*)(double,double)>(&std::pow)));
 ptr(0x5498FC)=reinterpret_cast<unsigned>(allocate(1024*1024));
 std::array<unsigned char,36>textures[2]{};std::array<unsigned char,8>pvr[2]{};std::array<unsigned short,4>packs[2]{};
 for(int i=0;i<2;i++){at<unsigned short>(textures[i].data(),2)=i+2;at<void*>(textures[i].data(),28)=pvr[i].data();
  at<unsigned char>(pvr[i].data(),0)=0x10;at<void*>(pvr[i].data(),4)=packs[i].data();packs[i][2]=packs[i][3]=64;}
 unsigned flags0,flags1;std::string hex;
 while(std::cin>>flags0>>flags1>>hex){
  flags[0]=0;flags[1]=flags0;flags[2]=flags1;
  std::vector<unsigned char>bytes;for(unsigned i=0;i<hex.size();i+=2)bytes.push_back(std::stoul(hex.substr(i,2),nullptr,16));
  std::array<unsigned,3>models{};models[0]=2;std::array<std::array<unsigned char,64>,2>items{};
  std::array<unsigned,2>faceOffsets{};
  for(int i=0;i<2;i++){unsigned model=at<unsigned>(bytes.data(),88+i*4);models[i+1]=reinterpret_cast<unsigned>(bytes.data()+model);
   faceOffsets[i]=model+28+4*8+8;at<unsigned short>(items[i].data(),26)=i;
   if((at<unsigned>(bytes.data(),faceOffsets[i])&3)==3)at<void*>(bytes.data(),faceOffsets[i]+16)=textures[i].data();}
  std::memset(reinterpret_cast<void*>(0x6B2440),0,68);ptr(0x5F6764)=0;
  ptr(0x6B2454)=reinterpret_cast<unsigned>(models.data()+1);ptr(0x6B2450)=reinterpret_cast<unsigned>(items.data());ptr(0x6B2458)=reinterpret_cast<unsigned>(bytes.data());
  unsigned packet=at<unsigned>(bytes.data(),4);
  while(at<unsigned>(bytes.data(),packet)!=0xFFFFFFFF){unsigned kind=at<unsigned>(bytes.data(),packet);
   if(kind==6)ptr(0x6B246C)=reinterpret_cast<unsigned>(bytes.data()+packet+8);
   if(kind==7)ptr(0x6B2470)=reinterpret_cast<unsigned>(bytes.data()+packet+8);
   packet+=8+at<unsigned>(bytes.data(),packet+4);}
  reinterpret_cast<void(*)(int)>(0x4534A0)(0);
  auto*dc=reinterpret_cast<unsigned char*>(ptr(0x5F6764));
  std::cout<<'[';
  for(int i=0;i<2;i++){if(i)std::cout<<',';auto*model=reinterpret_cast<unsigned char*>(models[i+1]);auto*face=bytes.data()+faceOffsets[i];
   std::cout<<'['<<at<unsigned short>(model,0)<<','<<at<unsigned short>(items[i].data(),4)<<','<<at<unsigned>(face,0)<<','<<at<unsigned>(face,12)
    <<','<<at<unsigned>(dc+i*36,12)<<','<<at<unsigned short>(at<void*>(dc+i*36,4),0)<<']';}
  std::cout<<"]\n";
  // DCClearRegion on the next imported fixture has no previous native region. Own the bounded heap instead.
  ptr(0x5F6764)=0;
  for(unsigned i=1;i<allocated.size();i++)std::free(allocated[i]);allocated.resize(1);
 }
 std::free(allocated[0]);
}
'''
    cases = []
    for version in [0x20004, 0x20006]:
        for flags in [0, 0x80, 0x40, 0xC0, 0x811, 0x1013, 0x13]:
            for collision, vertex, pulse, wibble in [(0, 0, 0, 0), (0x10000, 0x10, 0, 1), (0x180000, 0, 1, 0)]:
                raw = source_bank(version, 0, flags, collision, vertex, pulse, wibble)
                cases.append(dict(source=raw.hex(), textureFlags=[0, 16], textureFormats=[0x10000, 0x10000]))
    rows = [' '.join(map(str, [*case['textureFlags'], case['source']])) for case in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-environment-preparation-') as folder:
        cpp,binary=Path(folder)/'reference.cpp',Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-m32','-std=c++17','-fno-pie','-no-pie','-fwrapv','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        output=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(rows)+'\n').encode())
    fixture=dict(originalPcPreparation='4534A0',originalPcModelConstructor='431430',
        boundaries=[dict(**case,expected=json.loads(line)) for case,line in zip(cases,output.decode().splitlines(),strict=True)])
    (root/'modern/OpenSpidey.Tests/Fixtures/environment-preparation.json').write_text(json.dumps(fixture,separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} whole PC region imports/{len(cases)*2} actual DCModel constructors with source face/model/item and native renderer flags.')


if __name__=='__main__':main()
