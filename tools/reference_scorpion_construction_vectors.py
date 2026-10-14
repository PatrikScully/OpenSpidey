"""Capture full original Scorpion/Jonah factories, base constructors, CycleAnim and constructor bytecode."""
import argparse
import json
from pathlib import Path
import random
import struct
import subprocess
import tempfile
import zlib


def read_assets(path):
    source = path.read_bytes()
    offset = struct.unpack_from('<I', source, 4)[0]
    _, directory_count, file_count = struct.unpack_from('<III', source, offset)
    offset += 12
    directories = []
    for _ in range(directory_count):
        name, first, count = struct.unpack_from('<32sII', source, offset)
        directories.append((name.split(b'\0')[0].decode().replace('\\', '/').rstrip('/'), first, count))
        offset += 40
    files = [struct.unpack_from('<32sIiIII', source, offset + index * 52) for index in range(file_count)]
    result = {}
    for directory, first, count in directories:
        for name, crc, compression, position, decoded, stored in files[first:first + count]:
            name = name.split(b'\0')[0].decode()
            asset = (directory + '/' + name).lower()
            if asset not in ['data/scorpion.psx', 'data/jameson.psx', 'data/jjjj.psx']:
                continue
            data = source[position:position + stored]
            if compression == 2:
                data = zlib.decompress(data)
            else:
                assert compression == -2
            assert len(data) == decoded and zlib.crc32(data) == crc
            result[asset] = data
    assert len(result) == 3
    return result


HARNESS = r'''
#include "original_executable.h"
#include <iostream>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
template<class T>T&mem(unsigned a){return *reinterpret_cast<T*>(a);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
void omit(unsigned a,int n){std::memset((void*)a,0x90,n);}
std::vector<unsigned char>model;std::vector<std::vector<long long>>events;unsigned char *actor;int type,allowAllocation,allocated;unsigned short*script;
unsigned char attachBytes[5],cycleBytes[5],parseBytes[5];
std::vector<long long> snapshot(void*p){std::vector<long long>v;for(unsigned a:{8u,12u,16u})v.push_back(at<int>(p,a));for(unsigned a:{0x14u,0x16u,0x18u})v.push_back(at<short>(p,a));
 for(unsigned a:{4u,0x46u,0x38u,0xDEu})v.push_back(at<unsigned short>(p,a));v.push_back(at<int>(p,0x1F4));v.push_back(at<unsigned short>(p,0xDC));v.push_back(at<int>(p,0xF4));
 v.push_back(at<short>(p,0x21E));v.push_back(at<short>(p,0x216));v.push_back(at<int>(p,0xD8));v.push_back(at<short>(p,0xE2));v.push_back(at<unsigned short>(p,0x1FC));
 for(unsigned a:{0x230u,0x31Cu,0x320u})v.push_back(at<int>(p,a));for(unsigned a:{0x2A8u,0x294u,0x298u})v.push_back(at<unsigned>(p,a));
 v.push_back(at<unsigned short>(p,0x12A));v.push_back(at<short>(p,0x128));v.push_back(at<unsigned short>(p,0x146));v.push_back(at<signed char>(p,0x141));
 v.push_back(at<unsigned char>(p,0x140));v.push_back(at<unsigned short>(p,0x148));v.push_back(at<short>(p,0x12C));v.push_back(at<unsigned char>(p,0x142));v.push_back(at<int>(p,0x14C));v.push_back(at<unsigned char>(p,0x21D));
 unsigned light=at<unsigned>(p,0x3C);v.push_back(light==0x5523D0?310:light==0x552010?316:0);
 v.push_back(at<unsigned>(p,0x194));v.push_back(type==310?at<unsigned char>(p,0x417):0);v.push_back(type==310?at<unsigned char>(p,0x45F):0);v.push_back(type==310?at<unsigned char>(p,0xC0C):0);
 for(unsigned a:{0x78u,0x79u,0x7Au,0x94u,0x95u,0x96u})v.push_back(at<unsigned char>(p,a));return v;}
void event(int kind,void*p,std::initializer_list<long long>args={}){std::vector<long long>v={kind};v.insert(v.end(),args);auto s=snapshot(p);v.insert(v.end(),s.begin(),s.end());events.push_back(v);}
void*allocate(unsigned size){assert(size==unsigned(type==310?0xC28:0x380));allocated=size;if(!allowAllocation)return nullptr;actor=(unsigned char*)std::calloc(1,size);assert(actor);return actor;}
void __attribute__((thiscall)) initItem(void*p,const char*name){int selector=!std::strcmp(name,"scorpion")?310:!std::strcmp(name,"jameson")?316:!std::strcmp(name,"jjjj")?999:0;assert(selector);event(1,p,{selector});at<unsigned char>(p,0x1F)=1;at<unsigned short>(p,0x1A)=0;
 unsigned offset=at<unsigned>(model.data(),4);while(at<unsigned>(model.data(),offset)!=42&&at<unsigned>(model.data(),offset)!=44){assert(at<unsigned>(model.data(),offset)!=0xFFFFFFFF);offset+=8+at<unsigned>(model.data(),offset+4);}mem<void*>(0x6B245C+68)=model.data()+offset+8;}
void __attribute__((thiscall)) attach(void*p,void**head){std::memcpy((void*)0x460260,attachBytes,5);reinterpret_cast<void(__attribute__((thiscall))*)(void*,void**)>(0x460260)(p,head);patch(0x460260,(void*)&attach);event(2,p);}
void __attribute__((thiscall)) cycle(void*p,int clip,signed char direction){event(3,p,{clip,direction});std::memcpy((void*)0x460D00,cycleBytes,5);reinterpret_cast<void(__attribute__((thiscall))*)(void*,int,signed char)>(0x460D00)(p,clip,direction);patch(0x460D00,(void*)&cycle);}
void __attribute__((thiscall)) parse(void*p,unsigned short*cursor){assert(cursor==script);event(4,p);std::memcpy((void*)0x404FD0,parseBytes,5);reinterpret_cast<void(__attribute__((thiscall))*)(void*,unsigned short*)>(0x404FD0)(p,cursor);patch(0x404FD0,(void*)&parse);}
void health(void*p,int kind){event(5,p,{kind});}
void diagnostic(int ok,const char*){assert(ok);}
void numbers(const std::vector<long long>&v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==4);original_executable::loadOriginalImage(argv[1]);
 // Only Windows SEH registration and external allocator/asset/panel transports are replaced.
 for(unsigned a:{0x4831A0u,0x444280u,0x483297u,0x483457u,0x4443F7u}){omit(a,6);mem<unsigned short>(a)=0xC031;}
 for(unsigned a:{0x4831B2u,0x4831F4u,0x48320Du,0x483249u,0x444292u,0x4442D3u,0x4442EDu,0x444378u,0x48329Eu,0x48341Du,0x48345Eu,0x4836BCu,0x4443FEu,0x44451Bu})omit(a,7);
 patch(0x455390,(void*)&allocate);patch(0x460020,(void*)&initItem);patch(0x463610,(void*)&health);patch(0x4015B0,(void*)&diagnostic);
 std::memcpy(attachBytes,(void*)0x460260,5);patch(0x460260,(void*)&attach);std::memcpy(cycleBytes,(void*)0x460D00,5);patch(0x460D00,(void*)&cycle);std::memcpy(parseBytes,(void*)0x404FD0,5);patch(0x404FD0,(void*)&parse);
 int node,ordinal,cheat,difficulty,px,py,pz,ax,ay,az,count;
 while(std::cin>>type){
  if(type==998){int state;unsigned signals;std::cin>>state>>signals;alignas(4)unsigned char jonah[0x380]={};at<int>(jonah,0x31C)=state;at<unsigned>(jonah,0x218)=signals;
   unsigned stack[1]={(unsigned)jonah},result=0;reinterpret_cast<void(*)(unsigned*,unsigned*)>(0x444390)(stack,&result);reinterpret_cast<void(*)(unsigned*,unsigned*)>(0x4443C0)(stack,&result);
   std::cout<<'['<<result<<','<<at<unsigned>(jonah,0x218)<<"]\n";continue;}
  std::cin>>node>>ordinal>>cheat>>difficulty>>allowAllocation>>px>>py>>pz>>ax>>ay>>az>>count;
  std::ifstream in(type==310?argv[2]:argv[3],std::ios::binary);model.assign(std::istreambuf_iterator<char>(in),{});assert(!model.empty());
  alignas(4)int placement[64]={px,py,pz};auto*angles=(short*)(placement+3);angles[0]=ax;angles[1]=ay;angles[2]=az;script=(unsigned short*)((unsigned char*)placement+18);for(int i=0;i<count;i++){unsigned x;std::cin>>x;script[i]=x;}
  mem<int>(0x56E98C)=ordinal;mem<void*>(0x56E990)=nullptr;mem<int>(0x60D004)=cheat;mem<short>(0x6B4822)=difficulty;mem<unsigned char>(0x60CFC4)=0;
  events.clear();actor=nullptr;allocated=0;unsigned arguments[2]={(unsigned)placement,(unsigned)node};void*result=nullptr;
  reinterpret_cast<void(*)(unsigned*,void**)>(type==310?0x4831A0:0x444280)(arguments,&result);assert(result==actor);
  std::cout<<"{\"allocated\":"<<allocated<<",\"accepted\":"<<(result?1:0)<<",\"count\":"<<mem<int>(0x56E98C)<<",\"head\":"<<(mem<void*>(0x56E990)==actor&&actor?1:0)<<",\"state\":";numbers(actor?snapshot(actor):std::vector<long long>{});
  std::cout<<",\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';numbers(events[i]);}std::cout<<"]}\n";std::free(actor);
 }
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    parser.add_argument('--assets', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    assets = read_assets(args.assets / 'data.pkr')
    rng = random.Random(0x483450)
    cases = []
    for actor in [310, 316]:
        for node in [0, 1, 2, 65535]:
            for cheat in [0, 1]:
                for accepted in [0, 1]:
                    cases.append(dict(type=actor, node=node, ordinal=255, cheat=cheat, difficulty=2, accepted=accepted,
                                      position=[-1735, -31, 443], angles=[0, 2504, 0], words=[0x2103, 15, 0x4100]))
        for _ in range(100):
            cases.append(dict(type=actor, node=rng.choice([0, 1, 2, 65535]), ordinal=rng.choice([-1, 0, 1, 255, 256, 1000]),
                              cheat=rng.randrange(2), difficulty=rng.randrange(4), accepted=rng.randrange(2),
                              position=[rng.randint(-524288, 524287) for _ in range(3)],
                              angles=[rng.randint(-32768, 32767) for _ in range(3)], words=[0x2103, rng.randrange(65536), 0x4100]))
    # The exact two authored L2A2 initial actor placement/script records.
    cases += [dict(type=310, node=1, ordinal=0, cheat=0, difficulty=2, accepted=1, position=[-1735, -31, 443], angles=[0, 2504, 0], words=[0x2103, 15, 0x4100]),
              dict(type=316, node=2, ordinal=1, cheat=0, difficulty=2, accepted=1, position=[-1480, -31, 839], angles=[0, 455, 0], words=[0x4100])]
    module = [dict(state=state, signals=rng.randrange(2**32)) for state in [-2147483648, -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 2147483647]]
    module += [dict(state=rng.randint(-2147483648, 2147483647), signals=rng.randrange(2**32)) for _ in range(100)]
    with tempfile.TemporaryDirectory(prefix='openspidey-scorpion-construction-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        scorpion, jameson = Path(folder) / 'scorpion.psx', Path(folder) / 'jameson.psx'
        scorpion.write_bytes(assets['data/scorpion.psx']); jameson.write_bytes(assets['data/jameson.psx'])
        cpp.write_text(HARNESS)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = [[c['type'], c['node'], c['ordinal'], c['cheat'], c['difficulty'], c['accepted'], *c['position'], *c['angles'], len(c['words']), *c['words']] for c in cases]
        rows += [[998, c['state'], c['signals']] for c in module]
        output = subprocess.check_output([str(binary), str(args.original_exe.resolve()), str(scorpion), str(jameson)],
                                         input=('\n'.join(' '.join(map(str, row)) for row in rows) + '\n').encode()).decode().splitlines()
    for case, line in zip(cases, output[:len(cases)], strict=True):
        case['expected'] = json.loads(line)
    for case, line in zip(module, output[len(cases):], strict=True):
        case['expected'] = json.loads(line)
    (root / 'modern/OpenSpidey.Tests/Fixtures/scorpion-construction.json').write_text(json.dumps(dict(constructors=cases, jonahModule=module), separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} complete PC Scorpion/Jonah factory/constructors plus {len(module)} original Jonah module decisions/plea calls; real base initialization, asset clip2 metadata, CycleAnim and constructor bytecode remain unchanged.')


if __name__ == '__main__':
    main()
