"""Compile source BuildPose and verify mutable joint matrices against privately mapped original PC code."""
import argparse
import json
from pathlib import Path
import random
import struct
import subprocess
import tempfile
import zlib
from reference_actor_hook_vectors import original_bytes
from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path)
    parser.add_argument('--original-assets', type=Path, help='Append original enemy first-frame native rendering gold cases.')
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    source = (root/'src/engine/rendering/m3dutils.cpp').read_text()
    maths = (root/'src/engine/rendering/ps2funcs.cpp').read_text()
    packets = []
    for name, address, pairs in [('thug',0x557CE4,[(0,65535),(7,0),(8,7),(2,7),(1,2),(3,1),(4,3),(6,7),(5,6),(9,5),(10,0),(11,10),(12,11),(13,0),(14,13),(15,14)]),
        ('henchman',0x557DBC,[(0,65535),(12,0),(13,12),(11,12),(10,11),(14,10),(8,12),(7,8),(9,7),(4,0),(5,4),(6,5),(1,0),(2,1),(3,2)]),
        ('player',0x5564E4,[(0,65535),(2,0),(1,2),(7,1),(4,1),(3,4),(6,3),(5,3),(9,1),(8,9),(11,8),(10,8)])]:
        encoded = struct.pack('<HH',0,len(pairs))+b''.join(struct.pack('<6H',p,parent,0,0,0,0) for p,parent in pairs)
        if args.original_exe and original_bytes(args.original_exe,address,len(encoded)) != encoded:
            raise RuntimeError(f'{name} original pose packet differs from extracted links.')
        links = [[p,parent,0,0,0,next((i for i,pair in enumerate(pairs) if pair[0]==parent),65535)] for p,parent in pairs]
        packets.append(dict(name=name,address=address,bytes=encoded.hex(),links=links))
    code = r'''
#include <cmath>
#include <cstdint>
#include <cstring>
#include <iostream>
#include <vector>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
using i32=int32_t;using i16=int16_t;using u32=uint32_t;using u16=uint16_t;using u8=uint8_t;
struct SVECTOR{i16 vx,vy,vz;};struct SJoint{SVECTOR Angles,Displacement;};
struct SLink{u16 Part,ParentPart;SVECTOR Pivot;u16 ParentLink;};
struct SMatrix{i16 m[3][3];i16 t[3];};struct MATRIX{i16 m[3][3];i32 t[3];};
struct CSuper{u16 mFlags;u8 mRegion;u16 mAnim;SMatrix*mpPoseBuffer;SJoint*mpJoints;SLink*mpLinks;};
struct SPSXRegion{int NumParts;u32*pAnimFile;};SPSXRegion G_PSXREGION[1];SMatrix*raw;
SMatrix*Decomp_GetAnimTransform(CSuper*){return raw;}
void print_if_false(bool result,const char*){if(!result)std::abort();}
'''
    for signature in ['MATRIX* RotMatrixYXZ(', 'MATRIX* M3dMaths_RotMatrixYXZ(']:
        code += controller_method(maths,signature)+'\n'
    for signature in ['static void M3dUtils_ComposeTransform(', 'static void M3dUtils_ComposeWithInverse(', 'void M3dUtils_BuildPose(']:
        code += controller_method(source,signature)+'\n'
    code += r'''
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
 assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // BuildPose consumes a sampled keyframe pointer. Stub only that independent decoder boundary,
 // so its own original interval guard still sees the real requested flag.
 auto*resolver=reinterpret_cast<u8*>(0x433D60);resolver[0]=0xE9;
 *reinterpret_cast<i32*>(resolver+1)=reinterpret_cast<i32>(&Decomp_GetAnimTransform)-0x433D65;
#endif
 int parts,count,interval;
 while(std::cin>>parts>>count>>interval){
  std::vector<SMatrix>key(parts),pose(parts),originalPose(parts);std::vector<SJoint>joints(count);
  std::vector<u16>linksStorage(2+count*6);linksStorage[1]=count;SLink*links=reinterpret_cast<SLink*>(linksStorage.data()+2);
  for(int i=0;i<count;i++){std::cin>>links[i].Part>>links[i].ParentPart>>links[i].Pivot.vx>>links[i].Pivot.vy>>links[i].Pivot.vz>>links[i].ParentLink;}
  for(int i=0;i<parts;i++){for(auto&row:key[i].m)for(auto&value:row)std::cin>>value;for(auto&value:key[i].t)std::cin>>value;}
  for(int i=0;i<count;i++)std::cin>>joints[i].Angles.vx>>joints[i].Angles.vy>>joints[i].Angles.vz>>joints[i].Displacement.vx>>joints[i].Displacement.vy>>joints[i].Displacement.vz;
  u32 animWords[3]={1,12,u32(1|(interval<<16))};G_PSXREGION[0]={parts,animWords};raw=key.data();
  CSuper actor{4,0,0,pose.data(),joints.data(),links};M3dUtils_BuildPose(&actor);
#ifdef VALIDATE_ORIGINAL
  std::vector<u8>animation(20+parts*sizeof(SMatrix));u32*header=reinterpret_cast<u32*>(animation.data());
  header[0]=42;header[1]=animation.size()-8;header[2]=1;header[3]=12;header[4]=u32(1|(interval<<16));
  std::memcpy(animation.data()+20,key.data(),parts*sizeof(SMatrix));
  std::vector<u32>models(parts+1);models[0]=parts;int psx[3]={0,0,parts};
  *reinterpret_cast<u32*>(0x6B2454)=reinterpret_cast<u32>(models.data()+1);
  *reinterpret_cast<u32*>(0x6B2458)=reinterpret_cast<u32>(psx);
  *reinterpret_cast<u32*>(0x6B245C)=reinterpret_cast<u32>(animation.data()+8);
  *reinterpret_cast<u16*>(0x6B2478)=parts;
  alignas(4)u8 originalActor[0x200]={};*reinterpret_cast<u16*>(originalActor+4)=4;
  *reinterpret_cast<u32*>(originalActor+0x184)=reinterpret_cast<u32>(originalPose.data());
  *reinterpret_cast<u32*>(originalActor+0x188)=reinterpret_cast<u32>(joints.data());
  *reinterpret_cast<u32*>(originalActor+0x18C)=reinterpret_cast<u32>(links);
  reinterpret_cast<void(*)(void*)>(0x454450)(originalActor);
  if(std::memcmp(originalPose.data(),pose.data(),parts*sizeof(SMatrix))){
   for(int part=0;part<parts;part++)for(int component=0;component<12;component++)if(reinterpret_cast<i16*>(&originalPose[part])[component]!=reinterpret_cast<i16*>(&pose[part])[component])
    std::cerr<<"Original BuildPose differs part="<<part<<" component="<<component<<" original="<<reinterpret_cast<i16*>(&originalPose[part])[component]<<" recovered="<<reinterpret_cast<i16*>(&pose[part])[component]<<'\n';return 2;}
#endif
  std::cout<<'[';for(int i=0;i<parts;i++){if(i)std::cout<<',';std::cout<<'[';for(int c=0;c<12;c++){if(c)std::cout<<',';std::cout<<reinterpret_cast<i16*>(&pose[i])[c];}std::cout<<']';}std::cout<<"]\n";
 }}
'''
    rng = random.Random(0x454450)
    cases = []
    identity = [4096,0,0,0,4096,0,0,0,4096]
    for index in range(500):
        packet = packets[index%2]
        parts = len(packet['links'])
        key = [identity+[rng.randrange(-1000,1000) for _ in range(3)] for _ in range(parts)]
        if index%3 == 0:
            key = [[rng.randrange(-4096,4097) for _ in range(9)]+[rng.randrange(-32768,32768) for _ in range(3)] for _ in range(parts)]
        joints = [[0]*6 for _ in range(parts)]
        if index:
            for joint in rng.sample(range(parts),rng.randrange(1,5)):
                joints[joint] = [rng.randrange(-2048,2048) for _ in range(3)]+[rng.randrange(-32768,32768) for _ in range(3)]
        cases.append(dict(packet=packet['name'],parts=parts,interpolated=index%2!=0,key=key,joints=joints))
    # The player's original packet corrects twelve joints while its compressed rig owns eighteen parts.
    # Also prove the original interval-animation sentinel behavior for untouched parts.
    for index in range(64):
        key = [identity+[rng.randrange(-1000,1000) for _ in range(3)] for _ in range(18)]
        joints = [[0]*6 for _ in range(12)]
        if index > 1:
            for joint in rng.sample(range(12),rng.randrange(1,5)):
                joints[joint] = [rng.randrange(-2048,2048) for _ in range(3)]+[rng.randrange(-32768,32768) for _ in range(3)]
        cases.append(dict(packet='player',parts=18,interpolated=bool(index&1),key=key,joints=joints))
    if args.original_assets:
        archive = (args.original_assets/'data.pkr').read_bytes()
        directory = struct.unpack_from('<I', archive, 4)[0]
        _, directories, files = struct.unpack_from('<3I', archive, directory)
        file_table = directory + 12 + directories * 40
        for asset in ['thug', 'henchman', 'henchngt']:
            data = None
            for index in range(files):
                offset = file_table + index * 52
                name = archive[offset:offset+32].split(b'\0')[0].decode()
                if name.lower() != asset+'.psx':continue
                checksum, compression, position, size, stored = struct.unpack_from('<Ii3I',archive,offset+32)
                data = archive[position:position+stored]
                if compression == 2:data = zlib.decompress(data)
                elif compression != -2:raise RuntimeError('Unsupported original archive compression.')
                if len(data) != size or zlib.crc32(data) != checksum:raise RuntimeError('Original archive CRC differs.')
                break
            if data is None:raise RuntimeError(f'Original {asset}.psx not found.')
            parts = struct.unpack_from('<I',data,8)[0]
            cursor = struct.unpack_from('<I',data,4)[0]
            while True:
                kind = struct.unpack_from('<I',data,cursor)[0]
                if kind == 0xFFFFFFFF:raise RuntimeError('Original matrix animation packet absent.')
                size = struct.unpack_from('<I',data,cursor+4)[0]
                if kind == 42:
                    animation = data[cursor+8:cursor+8+size]
                    frame_offset, frame_word = struct.unpack_from('<2I',animation,4)
                    key = [list(struct.unpack_from('<12h',animation,frame_offset+part*24)) for part in range(parts)]
                    break
                cursor += 8+size
            joints = [[0]*6 for _ in range(parts)]
            joints[1] = [0,0,384,0,0,0]
            joints[3] = [-111,0,-76,32,-48,0]
            cases.append(dict(packet='thug' if asset=='thug' else 'henchman', parts=parts,
                              interpolated=frame_word>>16!=0,key=key,joints=joints,native=True,asset=asset,clip=0,frame=0))
    requests = ''
    for case in cases:
        packet = next(p for p in packets if p['name']==case['packet'])
        fields = [case['parts'],len(packet['links']),int(case['interpolated'])]
        fields += [v for link in packet['links'] for v in link]
        fields += [v for part in case['key'] for v in part]
        fields += [v for joint in case['joints'] for v in joint]
        requests += ' '.join(map(str,fields))+'\n'
    with tempfile.TemporaryDirectory(prefix='openspidey-joint-pose-reference-') as directory:
        path = Path(directory);(path/'reference.cpp').write_text(code)
        flags = ['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['c++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(path/'reference.cpp'),'-o',str(path/'reference')],check=True)
        command = [str(path/'reference')]+([str(args.original_exe.resolve())] if args.original_exe else [])
        outputs = subprocess.check_output(command,input=requests.encode()).decode().splitlines()
    for case, output in zip(cases,outputs,strict=True):case['expected']=json.loads(output)
    target = root/'modern/OpenSpidey.Tests/Fixtures/actor-joint-poses.json'
    target.write_text(json.dumps(dict(packets=packets,cases=cases),separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} compiled-source mutable joint cases.')
    if args.original_exe:print('Every pose also matches original PC BuildPose 0x454450 machine code.')


if __name__=='__main__':main()
