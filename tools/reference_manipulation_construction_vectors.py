"""Compile original CManipOb constructor over aligned file offsets and original model/shadow/chunk packets."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent;source=(root/'src/game/objects/manipob.cpp').read_text();body=(root/'src/engine/world/ob.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <cstring>
#include <cstdint>
std::vector<long long>events;int G_OBJ_FILE=0,gObjFileRegion=7,G_ENVIRONMENTAL_OBJECT_LIST=0;struct CItem{};CItem shadow;
int Spool_GetModel(unsigned checksum,int region){events.insert(events.end(),{3,checksum,region});return checksum;}
CItem*Spool_FindEnviroItem(unsigned checksum){events.insert(events.end(),{4,checksum});return checksum==0x12345678?&shadow:nullptr;}
struct CBody{CVector mPos;CSVector mAngles;u16 mFlags=0,mType=0,mNode=0;CFriction mFric;int mModel=0;i16*SquirtPos(i16*);i16*SquirtAngles(i16*);void InitItem(int){events.push_back(1);}void AttachTo(void*){events.push_back(2);}};
struct CManipOb:CBody{uintptr_t field_120=0;CItem*field_11C=nullptr;int field_10C=0;short field_108=0,field_104=0;CManipOb(i16*,int);};
'''
    for sig in ['i16* CBody::SquirtPos(','i16* CBody::SquirtAngles(']:code+=controller_method(body,sig).replace('ASSERT(((i32)walker & 3) == 0, "Bad alignment");','')+'\n'
    method=controller_method(source,'CManipOb::CManipOb(').replace('reinterpret_cast<u32>(','reinterpret_cast<uintptr_t>(').replace('& 0xFFFFFFFC','& ~uintptr_t(3)').replace('reinterpret_cast<i32>(v8)','reinterpret_cast<uintptr_t>(v8)');code+=method+'\n'
    code+=r'''
int main(){int flags,param,sound,model,shadowId,count,px,py,pz,ax,ay,az;while(std::cin>>flags>>param>>sound>>model>>shadowId>>count>>px>>py>>pz>>ax>>ay>>az){alignas(4)unsigned char raw[256]={};int*p=reinterpret_cast<int*>(raw);p[0]=px;p[1]=py;p[2]=pz;short*a=reinterpret_cast<short*>(raw+12);a[0]=ax;a[1]=ay;a[2]=az;int*packet=reinterpret_cast<int*>(raw+20);*packet++=model;*packet++=shadowId;std::vector<int>chunks;for(int i=0;i<count;i++){int v;std::cin>>v;chunks.push_back(v);*packet++=v;}if(shadowId)*packet++=0;short*tail=reinterpret_cast<short*>(packet);if(!shadowId)tail=reinterpret_cast<short*>(raw+28);tail[0]=flags;tail[1]=param;tail[2]=sound;events.clear();CManipOb s(reinterpret_cast<i16*>(raw),9);
std::cout<<'['<<s.mPos.vx<<','<<s.mPos.vy<<','<<s.mPos.vz<<','<<s.mAngles.vx<<','<<s.mAngles.vy<<','<<s.mAngles.vz<<','<<s.mFlags<<','<<s.mType<<','<<s.mNode<<','<<int(s.mFric.vx)<<','<<int(s.mFric.vy)<<','<<int(s.mFric.vz)<<','<<s.field_10C<<','<<s.field_108<<','<<s.field_104<<','<<unsigned(s.mModel)<<','<<(s.field_11C!=nullptr)<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    rng=random.Random(0x456920);cases=[]
    for flags in [0,1,2,3,4,65535]:
        for shadow in [0,0x12345678,0x23456789]:
            for nodeOffset in [0,2]:
                cases.append(dict(flags=flags,param=rng.randint(-32768,32767),sound=rng.randint(-32768,32767),model=0x5301b900,shadow=shadow,chunks=[0x7640e7bd,0xef49b607,0x984e8691,0x062a1332] if shadow else [],position=[rng.randint(-600000,600000) for _ in range(3)],angles=[rng.randint(-32768,32767) for _ in range(3)],nodeOffset=nodeOffset,objectOffset=2 if nodeOffset else 0))
    rows=[]
    for c in cases:
        signed=lambda n:n if n<0x80000000 else n-0x100000000
        rows.append(' '.join(map(str,[c['flags'],c['param'],c['sound'],signed(c['model']),signed(c['shadow']),len(c['chunks']),*c['position'],*c['angles'],*[signed(v) for v in c['chunks']]])))
    with tempfile.TemporaryDirectory(prefix='openspidey-manipulation-construction-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code);subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(rows)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/manipulation-construction.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n');print(f'Generated{len(cases)} compiled-source manipulation constructors incl zero-shadow terminator and absolute file alignment.')

if __name__=='__main__':main()
