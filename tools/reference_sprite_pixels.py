"""Independently hash genuine indexed sprite pixels after the original PC palette preprocessing."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import tempfile
import zlib

from reference_ground_response_vectors import controller_method


def read_entry(path, target):
    data=path.read_bytes();directory=struct.unpack_from('<I',data,4)[0]
    _,directories,count=struct.unpack_from('<III',data,directory)
    records=directory+12+directories*40
    for index in range(directories):
        position=directory+12+index*40
        prefix=data[position:position+32].split(b'\0')[0].decode('ascii').replace('\\','/')
        first,length=struct.unpack_from('<II',data,position+32)
        for file_index in range(first,first+length):
            record=records+file_index*52
            name=data[record:record+32].split(b'\0')[0].decode('ascii')
            if (prefix.rstrip('/')+'/'+name).lower()!=target.lower():continue
            checksum,compression,offset,decoded,size=struct.unpack_from('<IiIII',data,record+32)
            value=data[offset:offset+size];value=zlib.decompress(value) if compression==2 else value
            assert len(value)==decoded and zlib.crc32(value)==checksum
            return value
    raise FileNotFoundError(target)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive',type=Path,required=True)
    parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    data=read_entry(args.archive,'data/bits.psx')
    offset=struct.unpack_from('<I',data,4)[0]
    while struct.unpack_from('<I',data,offset)[0]!=0xffffffff:
        offset+=8+struct.unpack_from('<I',data,offset+4)[0]
    offset+=4
    models=struct.unpack_from('<I',data,12+struct.unpack_from('<I',data,8)[0]*36)[0]
    offset+=models*4
    count=struct.unpack_from('<I',data,offset)[0];offset+=4
    checksums=struct.unpack_from('<'+'I'*count,data,offset);offset+=count*4
    palettes={}
    for size in [16,256]:
        count=struct.unpack_from('<I',data,offset)[0];offset+=4
        for _ in range(count):
            checksum=struct.unpack_from('<I',data,offset)[0];offset+=4
            palettes[checksum]=list(struct.unpack_from('<'+'H'*size,data,offset));offset+=size*2
    count=struct.unpack_from('<I',data,offset)[0];offset+=4
    textures=[]
    for record in struct.unpack_from('<'+'I'*count,data,offset):
        flags,format,palette,index,width,height=struct.unpack_from('<IIIIHH',data,record)
        if checksums[index] not in [0x7D3FEC27,0xC11FEECC]:continue
        if any(texture['checksum']==checksums[index] for texture in textures):continue
        assert format in [16,256]
        textures.append(dict(checksum=checksums[index],width=width,height=height,format=format,
                             palette=palettes[palette],record=record))
    assert len(textures)==2
    code='#include <iostream>\n#include <vector>\n#include "original_executable.h"\n'
    code+=controller_method((root/'src/engine/rendering/pal.cpp').read_text(),'void Pal_ProcessPalette(')+'\n'
    code+='int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);int count;while(std::cin>>count){std::vector<u16>raw(count);unsigned word;for(auto&v:raw){std::cin>>word;v=word;}auto gold=raw;Pal_ProcessPalette(raw.data(),count);reinterpret_cast<void(*)(u16*,int)>(0x4618B0)(gold.data(),count);assert(raw==gold);for(auto v:gold)std::cout<<v<<" ";std::cout<<"\\n";}}\n'
    with tempfile.TemporaryDirectory(prefix='openspidey-sprite-pixels-reference-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        rows='\n'.join(str(len(t['palette']))+' '+' '.join(map(str,t['palette'])) for t in textures)+'\n'
        output=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=rows.encode()).decode().splitlines()
    result=[]
    for texture,line in zip(textures,output,strict=True):
        palette=list(map(int,line.split()));pixels=bytearray()
        width=texture['width'];height=texture['height'];format=texture['format']
        stride=((width+3)&~3)//2 if format==16 else (width+1)&~1
        for y in range(height):
            for x in range(width):
                sample=data[texture['record']+20+y*stride+(x//2 if format==16 else x)]
                index=(sample>>((x&1)*4))&15 if format==16 else sample
                colour=palette[index]
                def channel(value):return ((value&31)<<3)|((value&31)>>2)
                pixels.extend([channel(colour),channel(colour>>5),channel(colour>>10),255 if colour else 0])
        result.append(dict(checksum=f"{texture['checksum']:08X}",width=width,height=height,
                           hasMagentaKey=any((v&0x7fff)==0x7c1f for v in texture['palette']),
                           rgbaSha256=hashlib.sha256(pixels).hexdigest()))
    (root/'docs/reference/sprite-textures.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))


if __name__=='__main__':main()
