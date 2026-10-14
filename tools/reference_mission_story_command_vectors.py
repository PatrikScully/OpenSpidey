"""Capture original PC story commands12, 130, 157, 182, 197, 203-212, 217 and 220/221 through the whole interpreter."""
import argparse
import json
from pathlib import Path
import random
import struct
import subprocess
import tempfile


CODE = r'''
#include "original_executable.h"
#include <array>
#include <iostream>
#include <string>
#include <vector>
std::string events;
void event(const std::string& text){if(!events.empty())events+=',';events+=text;}
std::string arr(std::initializer_list<long long> values){std::string s="[";bool first=true;for(long long v:values){if(!first)s+=',';first=false;s+=std::to_string(v);}return s+"]";}
struct Body{void*vtable;unsigned char raw[0x400];};
std::vector<Body*> bodies;std::vector<unsigned char*> specials;std::vector<unsigned char*> items;std::vector<unsigned> itemChecksums;
unsigned char* camera;unsigned char* player;
int bodyId(void*self){for(unsigned i=0;i<bodies.size();i++)if(bodies[i]==self)return i;assert(false);return -1;}
void __attribute__((thiscall)) die(void*self){event(arr({0,bodyId(self)}));}
int __attribute__((thiscall)) hit(void*self,unsigned char*info){
 event(arr({1,bodyId(self),info[0],*(unsigned short*)(info+8),*(int*)(info+12),*(int*)(info+16),*(int*)(info+20)}));return 1;}
void __attribute__((thiscall)) missile(void*self,unsigned a){event(arr({2,bodyId(self),a}));}
void __attribute__((thiscall)) gun(void*self,unsigned a,unsigned b){event(arr({3,bodyId(self),a,b}));}
void __attribute__((thiscall)) flag(void*self,unsigned a,unsigned b){event(arr({4,bodyId(self),a&0xFFFF,(short)(b&0xFFFF)}));}
void __attribute__((thiscall)) enter(void*self){event(arr({5,bodyId(self)}));}
void __attribute__((thiscall)) leave(void*self,unsigned a,unsigned b){event(arr({6,bodyId(self),a,b}));}
void __attribute__((thiscall)) camAngle(void*self,unsigned a,unsigned b){assert(self==camera);event(arr({7,(short)(a&0xFFFF),b&0xFFFF}));}
void* findItem(unsigned checksum){for(unsigned i=0;i<items.size();i++)if(itemChecksums[i]==checksum)return items[i];return nullptr;}
void hitItem(unsigned char*item,unsigned face,unsigned damage){
 for(unsigned i=0;i<items.size();i++)if(items[i]==item){event(arr({8,i,face,damage}));return;}assert(false);}
void* generic[32];void* chopper[32];void* venom[32];
void patch(unsigned address,void*function){auto*p=(unsigned char*)address;p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=(unsigned)function-address-5;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 std::memset((void*)0x4E021D,0x90,6);*reinterpret_cast<unsigned short*>(0x4E021D)=0xC031;
 for(unsigned address:{0x4E0224u,0x4E2BA0u})std::memset((void*)address,0x90,7);
 patch(0x4178E0,(void*)&camAngle);patch(0x4C9230,(void*)&findItem);patch(0x43C480,(void*)&hitItem);
 for(void**table:{generic,chopper,venom}){for(int i=0;i<32;i++)table[i]=nullptr;table[1]=(void*)&die;table[3]=(void*)&hit;}
 chopper[0x44/4]=(void*)&missile;chopper[0x48/4]=(void*)&gun;chopper[0x4C/4]=(void*)&flag;
 venom[0x44/4]=(void*)&enter;venom[0x48/4]=(void*)&leave;
 const unsigned heads[]={0x56E990,0x56E994,0x60DAAC,0x60FB94};
 static unsigned char arenaStorage[0x10000+16];unsigned char*arena=(unsigned char*)(((unsigned)arenaStorage+15)&~15u);
 static unsigned char cameraStorage[0x400],playerStorage[0x1000];
 int nodes;
 while(std::cin>>nodes){
  events.clear();for(auto*b:bodies)delete b;bodies.clear();for(auto*s:specials)delete[]s;specials.clear();
  for(auto*i:items)delete[]i;items.clear();itemChecksums.clear();
  std::memset(arena,0xCD,0x10000);std::vector<unsigned> offsets(nodes);
  for(int n=0;n<nodes;n++){unsigned offset,length;std::cin>>offset>>length;offsets[n]=(unsigned)(arena+offset);
   for(unsigned i=0;i<length;i++){unsigned value;std::cin>>value;arena[offset+i]=(unsigned char)value;}}
  unsigned commandOffset;std::cin>>commandOffset;
  *(unsigned**)0x6B466C=offsets.data();*(int*)0x6B4670=nodes;*(void**)0x6B4708=nullptr;
  for(unsigned head:heads)*(void**)head=nullptr;*(void**)0x56EB34=nullptr;
  int count;std::cin>>count;std::vector<Body*> tails(4,nullptr);
  for(int i=0;i<count;i++){int list,type,node,health;std::cin>>list>>type>>node>>health;
   auto*b=new Body();std::memset(b->raw,0x5A,sizeof b->raw);
   b->vtable=type==0x13E?(void*)chopper:type==0x139?(void*)venom:(void*)generic;
   unsigned char*base=(unsigned char*)b;*(void**)(base+32)=nullptr;*(unsigned short*)(base+0x38)=type;
   *(unsigned short*)(base+222)=node;*(unsigned short*)(base+226)=health;
   for(unsigned f:{0x218u,0x1F4u,0x348u,0x350u,0x358u})*(int*)(base+f)=0x2468000+f;
   if(tails[list])*(void**)((unsigned char*)tails[list]+32)=b;else*(void**)heads[list]=b;tails[list]=b;bodies.push_back(b);}
  std::cin>>count;unsigned char*specialTail=nullptr;
  for(int i=0;i<count;i++){int kind,node;std::cin>>kind>>node;auto*s=new unsigned char[0x80]();s[0x3B]=kind;*(unsigned short*)(s+0x6A)=node;
   if(specialTail)*(void**)(specialTail+8)=s;else*(void**)0x56EB34=s;specialTail=s;specials.push_back(s);}
  std::cin>>count;for(int i=0;i<count;i++){unsigned checksum;std::cin>>checksum;auto*item=new unsigned char[16]();items.push_back(item);itemChecksums.push_back(checksum);}
  int cameraPresent;std::cin>>cameraPresent;
  std::memset(cameraStorage,0x77,sizeof cameraStorage);std::memset(playerStorage,0x66,sizeof playerStorage);
  camera=cameraPresent?cameraStorage:nullptr;player=playerStorage;
  *(void**)0x56F3B8=camera;*(void**)0x6A9038=player;*(unsigned char*)0x61919E=0xA5;*(int*)0x558050=-234;
  reinterpret_cast<void(*)(unsigned short*,unsigned,int)>(0x4E0210)((unsigned short*)(arena+commandOffset),0,0);
  std::cout<<"{\"events\":["<<events<<"],\"bodies\":[";
  for(unsigned i=0;i<bodies.size();i++){unsigned char*base=(unsigned char*)bodies[i];if(i)std::cout<<',';
   std::cout<<arr({*(int*)(base+0x218),*(int*)(base+0x1F4),*(int*)(base+0x348),*(int*)(base+0x350),*(int*)(base+0x358)});}
  std::cout<<"],\"specials\":[";for(unsigned i=0;i<specials.size();i++){if(i)std::cout<<',';std::cout<<(int)specials[i][0x37];}
  std::cout<<"],\"flags\":[";for(unsigned i=0;i<items.size();i++){if(i)std::cout<<',';std::cout<<*(unsigned short*)(items[i]+4);}
  std::cout<<"],\"light\":"<<*(int*)(player+0x578)<<",\"reference\":"<<*(short*)(player+0xE34)
   <<",\"lineOfSight\":"<<(camera?(int)camera[0xF9]:-1)<<",\"reverb\":"<<(int)*(unsigned char*)0x61919E
   <<",\"endNode\":"<<*(int*)0x558050<<"}\n";
 }
}
'''

ACTOR_TYPES = [304, 409, 0x13E, 0x139, 312]


def words(values):
    return b''.join(struct.pack('<H', value & 0xFFFF) for value in values)


def pad_to(data, base, alignment):
    while (base + len(data)) % alignment:
        data += b'\xEE'
    return data


def build_node(rng, kind, base):
    """Return the record for a source node whose first word is its kind."""
    links = [rng.randrange(0, 4) for _ in range(rng.randrange(0, 3))]
    position = struct.pack('<iii', *(rng.randrange(-40000, 40000) for _ in range(3)))
    if kind in (1, 7):
        data = words([kind, rng.choice(ACTOR_TYPES), rng.randrange(65536), len(links)] + links)
        data += bytes(rng.randrange(0, 255) for _ in range(rng.randrange(0, 4))) + b'\xFF'
        raw = len(data) - 1 + 4
    elif kind in (5, 20, 1002):
        data = words([kind, rng.randrange(65536), len(links)] + links)
        raw = 9 + len(links) * 2
    elif kind in (500, 501):
        data = words([kind, rng.randrange(65536)])
        raw = 5
    elif kind in (2, 9):
        data = words([kind, len(links)] + links)
        data = pad_to(data, base, 4) + struct.pack('<I', rng.choice([0x11112222, 0x33334444, 0x55556666]))
        return data + b'\0' * 4
    else:
        data = words([kind, len(links)] + links)
        raw = 7 + len(links) * 2
    offset = ((base + raw) & ~3) - base
    data = data.ljust(offset, b'\xEE')
    # Restart points carry angles, a name and an FFFF-terminated list after their position.
    tail = struct.pack('<hhh', 1, 2, 3) + b'R\0\xFF\xFF' if kind == 8 else b'\0' * 4
    return data[:offset] + position + tail


def operands(rng):
    return rng.choice([0, 1, 2, 255, 256, 0x1234, 0x7FFF, 0x8000, 0xFFFE, rng.randrange(65536)])


def make_case(rng, case_index):
    node_count = rng.randrange(4, 9)
    kinds = [rng.choice([1, 1, 7, 2, 9, 5, 20, 3, 8, 10, 12, 13, 1000, 1001, 1002, 500, 501]) for _ in range(node_count - 1)]
    bodies = []
    for _ in range(rng.randrange(0, 7)):
        bodies.append([rng.choice([0, 0, 0, 1, 2, 3]), rng.choice([0x13E, 0x139, 304, 312, 0x13E]),
                       rng.randrange(0, node_count + 1), rng.choice([0, 1, 100, 0x7FFF, 0x8000, 0xFFFF, rng.randrange(65536)])])
    specials = [[rng.choice([9, 9, 3]), rng.randrange(0, node_count)] for _ in range(rng.randrange(0, 3))]
    items = [rng.choice([0x11112222, 0x33334444, 0x55556666, 0x77778888]) for _ in range(rng.randrange(0, 4))]
    camera = rng.randrange(0, 4) != 0
    venom = any(body[0] == 0 and body[1] == 0x139 for body in bodies)
    program = []
    reinterpreted = case_index % 17 == 0 and not venom
    for _ in range(rng.randrange(1, 9)):
        choice = rng.choice([12, 12, 11, 157, 130, 182, 197, 203, 204, 205, 206, 207, 210, 211, 212, 217, 220, 221])
        if choice == 217 and not camera: continue
        if choice == 221 and not venom: continue
        if choice == 207: program += [207, rng.randrange(0, node_count)]
        elif choice in (11, 12, 205, 220): program += [choice]
        elif choice in (130, 210, 212, 221): program += [choice, operands(rng), operands(rng)]
        elif choice == 182: program += [182, operands(rng), operands(rng), operands(rng)]
        else: program += [choice, operands(rng)]
    # Without a Venom the PC leaves221's cursor on its operands, so this pair runs as EndLevelNode777.
    program += [221, 302, 777, 0xFFFF] if reinterpreted else [302, 1234, 0xFFFF]
    links = list(range(1, node_count))
    rng.shuffle(links)
    links = links[:rng.randrange(1, node_count)]
    records = []
    offset = rng.choice([0, 2])
    command = pad_to(words([6, len(links)] + links), offset, 4) + struct.pack('<I', 0xABCD0000 + case_index)
    command_offset = offset + len(command)
    command += words(program)
    records.append((offset, command))
    offset += len(command) + rng.choice([0, 2, 4, 6])
    for kind in kinds:
        data = build_node(rng, kind, offset)
        records.append((offset, data))
        offset += len(data) + rng.choice([0, 2])
        offset += offset & 1
    return dict(nodes=[[o, list(d)] for o, d in records], commandOffset=command_offset, bodies=bodies,
                specials=specials, items=items, camera=int(camera), reinterpreted=reinterpreted)


def serialize(case):
    parts = [len(case['nodes'])]
    for offset, data in case['nodes']:
        parts += [offset, len(data)] + data
    parts.append(case['commandOffset'])
    parts.append(len(case['bodies']))
    for body in case['bodies']: parts += body
    parts.append(len(case['specials']))
    for special in case['specials']: parts += special
    parts.append(len(case['items']))
    parts += case['items']
    parts.append(case['camera'])
    return ' '.join(map(str, parts))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x5717C0DE)
    cases = [make_case(rng, index) for index in range(600)]
    with tempfile.TemporaryDirectory(prefix='openspidey-mission-story-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CODE)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(serialize(case) for case in cases) + '\n').encode()).decode().splitlines()
    fixtures = [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/mission-story-commands.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} whole-PC command lists: loud kills, chopper/Venom dispatch, camera, player and reverb writes, '
          f'{sum(case["reinterpreted"] for case in cases)} operand reinterpretations after Venom-less221.')


if __name__ == '__main__':
    main()
