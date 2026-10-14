"""Execute the original PC save checksum, SPIDRMAN.DAT Card_Write/Card_Load, slot listing, record Merge and CMenu::Update.

With --verify-file the same harness only loads an existing SPIDRMAN.DAT through the original Card_Load and
reports each slot's original checksum verdict (used by the native frontend save round trip).
"""
import argparse
import json
from pathlib import Path
import random
import subprocess
import sys
import tempfile

CODE = r'''
#include "original_executable.h"
#include <cstdio>
#include <cstdlib>
#include <iostream>
#include <string>
#include <vector>
int&word(unsigned address){return *reinterpret_cast<int*>(address);}
unsigned char&byte(unsigned address){return *reinterpret_cast<unsigned char*>(address);}
void jump(unsigned address,void*target){byte(address)=0xE9;word(address+1)=reinterpret_cast<int>(target)-address-5;}
std::vector<int>events;std::vector<unsigned char>written,file;bool fileExists;int writtenBlocks;
std::vector<std::string>entries;int sounds;unsigned heldMask;
// Inlined Shell_CalculateGameChecksum from Shell_DisplayGameInfo 0x49F12D..0x49F15A; the exit jump replaces only cmp/jnz.
__attribute__((naked,noinline))unsigned checksum(void*){asm volatile(
 "push %ebp;push %esi;push %edi;mov 16(%esp),%ebp;sub $8,%esp;mov $0x49F12D,%ecx;jmp *%ecx");}
__attribute__((naked,noinline))void checksumExit(){asm volatile("pop %edi;pop %esi;pop %ebp;ret");}
void*dcmemNew(unsigned size,int,int,int,int){events.push_back(1);return std::malloc(size);}
void*syMalloc(unsigned size){events.push_back(2);void*value=std::malloc(size);std::memset(value,0xCD,size);return value;}
void syFree(void*value){events.push_back(3);std::free(value);}
void memDelete(void*value){events.push_back(4);std::free(value);}
unsigned cardTime[2];void*curTime(){return cardTime;}
unsigned char cardWait(int,int){events.push_back(5);return 1;}
void happy(int,int){events.push_back(6);}void sad(int,int){events.push_back(7);}
int saveFile(int,const char*name,void*buffer,int count,void*){events.push_back(8);assert(std::string(name)=="SPIDRMAN.DAT");
 writtenBlocks=count;written.assign(static_cast<unsigned char*>(buffer),static_cast<unsigned char*>(buffer)+count*0x200);return 0;}
int fileSize(int,const char*name){assert(std::string(name)=="SPIDRMAN.DAT");if(!fileExists)return -1;
 return reinterpret_cast<int(*)(unsigned,unsigned,unsigned)>(0x504990)(0,0,file.size());}
int loadFile(int,const char*,void*buffer,int count){unsigned bytes=std::min<unsigned>(count*0x200,file.size());
 std::memcpy(buffer,file.data(),bytes);return 0;}
void __attribute__((thiscall))addEntry(void*,const char*name){entries.push_back(name);}
int textWidth(const char*){return 0;}
int __attribute__((thiscall))processMouse(void*){return 0;}
unsigned char checkTriggers(unsigned mask,int held,int){assert(held==0);return (heldMask&mask)!=0;}
void sfx(unsigned id,short,int){assert(id==0x29);sounds++;}
std::vector<unsigned char>hex(const std::string&text){std::vector<unsigned char>out;if(text=="-")return out;
 for(unsigned i=0;i<text.size();i+=2)out.push_back(std::stoi(text.substr(i,2),nullptr,16));return out;}
void hexOut(const unsigned char*value,unsigned size){static const char*digits="0123456789abcdef";std::cout<<'"';
 for(unsigned i=0;i<size;i++)std::cout<<digits[value[i]>>4]<<digits[value[i]&15];std::cout<<'"';}
void array(const std::vector<int>&values){std::cout<<'[';for(unsigned i=0;i<values.size();i++){if(i)std::cout<<',';std::cout<<values[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 jump(0x49F15A,reinterpret_cast<void*>(&checksumExit));
 jump(0x4581A0,reinterpret_cast<void*>(&dcmemNew));jump(0x505470,reinterpret_cast<void*>(&syMalloc));
 jump(0x5054E0,reinterpret_cast<void*>(&syFree));jump(0x458210,reinterpret_cast<void*>(&memDelete));
 jump(0x431070,reinterpret_cast<void*>(&curTime));jump(0x431020,reinterpret_cast<void*>(&cardWait));
 jump(0x430E90,reinterpret_cast<void*>(&happy));jump(0x430F60,reinterpret_cast<void*>(&sad));
 jump(0x504890,reinterpret_cast<void*>(&saveFile));jump(0x5047D0,reinterpret_cast<void*>(&fileSize));
 jump(0x504710,reinterpret_cast<void*>(&loadFile));
 jump(0x4586C0,reinterpret_cast<void*>(&textWidth));jump(0x50C8A0,reinterpret_cast<void*>(&processMouse));
 jump(0x50C180,reinterpret_cast<void*>(&checkTriggers));jump(0x471C50,reinterpret_cast<void*>(&sfx));
 std::string mode;
 while(std::cin>>mode){
  if(mode=="C"){std::string data;std::cin>>data;std::vector<unsigned char>save=hex(data);assert(save.size()==0xBC);
   std::cout<<"{\"checksum\":"<<checksum(save.data())<<"}\n";}
  else if(mode=="S"){std::string data;std::cin>>data;std::vector<unsigned char>slots=hex(data);assert(slots.size()==8*0xBC);
   std::memcpy(reinterpret_cast<void*>(0x6101B0),slots.data(),slots.size());entries.clear();
   jump(0x43FFF0,reinterpret_cast<void*>(&addEntry));std::vector<unsigned char>menu(0x53C);
   reinterpret_cast<void(*)(void*)>(0x49F220)(menu.data());
   std::vector<int>kinds;for(unsigned i=0;i<entries.size();i++){const std::string&name=entries[i];
    kinds.push_back(name=="read error"?1:name=="---- empty ----"?2:0);}
   std::cout<<"{\"kinds\":";array(kinds);std::cout<<",\"dim\":";std::vector<int>dim;
   for(int i=0;i<8;i++)dim.push_back(menu[0x3C+i*0x20+0xE]);array(dim);std::cout<<"}\n";}
  else if(mode=="W"){std::string data;std::cin>>data;std::vector<unsigned char>head=hex(data);assert(head.size()==0xA24);
   std::memcpy(reinterpret_cast<void*>(0x60FFB0),head.data(),head.size());events.clear();written.clear();
   int result=reinterpret_cast<int(*)()>(0x46C500)();
   std::cout<<"{\"result\":"<<result<<",\"blocks\":"<<writtenBlocks<<",\"events\":";array(events);
   std::cout<<",\"file\":";hexOut(written.data(),written.size());std::cout<<",\"head\":";
   hexOut(reinterpret_cast<unsigned char*>(0x60FFB0),0xA24);std::cout<<"}\n";}
  else if(mode=="L"){std::string before,data;int exists;std::cin>>exists>>before>>data;fileExists=exists!=0;file=hex(data);
   std::vector<unsigned char>head=hex(before);assert(head.size()==0xA24);
   std::memcpy(reinterpret_cast<void*>(0x60FFB0),head.data(),head.size());events.clear();
   int result=reinterpret_cast<int(*)()>(0x46C7F0)();
   std::cout<<"{\"result\":"<<result<<",\"events\":";array(events);std::cout<<",\"head\":";
   hexOut(reinterpret_cast<unsigned char*>(0x60FFB0),0xA24);std::cout<<"}\n";}
  else if(mode=="V"){std::string path;std::cin>>path;std::FILE*input=std::fopen(path.c_str(),"rb");assert(input);
   file.clear();int value;while((value=std::fgetc(input))!=EOF)file.push_back(value);std::fclose(input);fileExists=true;
   std::memset(reinterpret_cast<void*>(0x60FFB0),0,0xA24);events.clear();
   int result=reinterpret_cast<int(*)()>(0x46C7F0)();std::vector<int>valid;std::vector<unsigned>sums;
   for(int i=0;i<8;i++){unsigned char*slot=reinterpret_cast<unsigned char*>(0x6101B0+i*0xBC);unsigned stored=word(0x6101B0+i*0xBC);
    unsigned computed=checksum(slot);sums.push_back(computed);valid.push_back(stored==0?2:stored==computed?0:1);}
   std::cout<<"{\"result\":"<<result<<",\"slots\":";array(valid);std::cout<<",\"names\":[";
   for(int i=0;i<8;i++){if(i)std::cout<<',';hexOut(reinterpret_cast<unsigned char*>(0x6101B0+i*0xBC+0x3F),9);}
   std::cout<<"],\"levels\":[";for(int i=0;i<8;i++){if(i)std::cout<<',';hexOut(reinterpret_cast<unsigned char*>(0x6101B0+i*0xBC+4),9);}
   std::cout<<"]}\n";}
  else if(mode=="M"){int lower;std::string left,right;std::cin>>lower>>left>>right;std::vector<unsigned char>a=hex(left),b=hex(right);
   assert(a.size()==25&&b.size()==25);reinterpret_cast<int(*)(void*,const void*,int)>(0x49EFF0)(a.data(),b.data(),lower);
   std::cout<<"{\"merged\":";hexOut(a.data(),25);std::cout<<"}\n";}
  else if(mode=="U"){int lines,visible,wrap,rate,start,timer,frames;unsigned enabled,blocked;
   std::cin>>lines>>visible>>wrap>>rate>>start>>timer>>enabled>>blocked>>frames;
   std::vector<unsigned char>menu(0x53C);
   reinterpret_cast<void(__attribute__((thiscall))*)(void*,int,int,unsigned char,int,int,int)>(0x43F9B0)(menu.data(),256,40,0,256,200,16);
   static const char*names[40];for(int i=0;i<lines;i++){names[i]="entry";
    reinterpret_cast<void(__attribute__((thiscall))*)(void*,const char*)>(0x43FFF0)(menu.data(),names[i]);}
   for(int i=0;i<lines;i++){menu[0x3C+i*0x20+0xD]=(enabled>>i)&1;*reinterpret_cast<int*>(&menu[0x3C+i*0x20+0x1C])=(blocked>>i)&1;}
   menu[0x1B]=visible;menu[0x11]=wrap;menu[0x10]=rate;
   reinterpret_cast<void(__attribute__((thiscall))*)(void*,char)>(0x43FF80)(menu.data(),start);
   word(0x5FAEA0)=timer;std::vector<int>out;
   for(int f=0;f<frames;f++){std::cin>>heldMask;sounds=0;
    reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x440600)(menu.data());
    out.insert(out.end(),{menu[0x14],menu[0x15],word(0x5FAEA0),sounds});}
   std::vector<int>scales;for(int i=0;i<lines;i++){scales.push_back(*reinterpret_cast<short*>(&menu[0x3C+i*0x20+4]));
    scales.push_back(*reinterpret_cast<short*>(&menu[0x3C+i*0x20+6]));}
   std::cout<<"{\"frames\":";array(out);std::cout<<",\"scales\":";array(scales);std::cout<<"}\n";}
  else if(mode=="X"){std::string code,save;std::cin>>code>>save;std::vector<unsigned char>record=hex(save);assert(record.size()==0xBC);
   static const unsigned flags[]={0x60CFD8,0x60CFE8,0x60CFF0,0x60CFC8,0x2E09BF4,0x2E09BF0};
   for(unsigned address:flags){int value;std::cin>>value;word(address)=value;}
   int whatIf,joel;std::cin>>whatIf>>joel;byte(0x60CFC5)=whatIf;word(0x60D004)=joel;
   std::memcpy(reinterpret_cast<void*>(0x682858),record.data(),0xBC);
   for(char&c:code)if(c=='_')c=' ';
   int result=reinterpret_cast<int(*)(const char*)>(0x47C440)(code.c_str());
   std::vector<int>after;for(unsigned address:flags)after.push_back(word(address));after.push_back(byte(0x60CFC5));after.push_back(word(0x60D004));
   std::cout<<"{\"result\":"<<result<<",\"flags\":";array(after);std::cout<<",\"save\":";
   hexOut(reinterpret_cast<unsigned char*>(0x682858),0xBC);std::cout<<"}\n";}
  else{std::cerr<<"unknown mode "<<mode<<"\n";return 2;}
  std::cout.flush();
 }}
'''


def build(root, folder):
    cpp = Path(folder) / 'reference.cpp'
    binary = Path(folder) / 'reference'
    cpp.write_text(CODE)
    subprocess.run(['g++', '-m32', '-std=c++17', '-O0', '-fno-pie', '-no-pie', '-fwrapv',
                    '-I', str(root/'src'), '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
    return binary


def run(binary, exe, rows):
    output = subprocess.check_output([str(binary), str(exe.resolve())], input=('\n'.join(rows)+'\n').encode())
    return [json.loads(line) for line in output.decode().splitlines()]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    parser.add_argument('--verify-file', type=Path, help='Load this SPIDRMAN.DAT through the original Card_Load and report slot checksums')
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    with tempfile.TemporaryDirectory(prefix='openspidey-save-') as folder:
        binary = build(root, folder)
        if args.verify_file:
            result = run(binary, args.original_exe, [f'V {args.verify_file.resolve()}'])[0]
            print(json.dumps(result))
            sys.exit(0 if result['result'] == 0 else 1)
        rng = random.Random(0x49F12D)
        image = args.original_exe.read_bytes()
        cases = dict(checksum=[], slots=[], write=[], load=[], merge=[], menu=[])
        rows = []

        def save(kind):
            data = bytearray(rng.getrandbits(8) for _ in range(0xBC))
            if kind == 'zero':
                data = bytearray(0xBC)
            elif kind == 'valid':
                data[0:4] = (0).to_bytes(4, 'little')
            elif kind == 'high':
                for i in range(4, 0xBC, 4):
                    data[i:i+4] = rng.choice([0xFFFFFFFF, 0x80000000, 0x7FFFFFFF, 1]).to_bytes(4, 'little')
            return data

        for kind in ['zero', 'random', 'high'] * 40 + ['random'] * 120:
            data = save(kind)
            cases['checksum'].append(dict(save=data.hex()))
            rows.append('C ' + data.hex())
        checksum_count = len(rows)
        checksum_results = run(binary, args.original_exe, rows)

        def valid_slot():
            data = save('random')
            data[0:4] = (0).to_bytes(4, 'little')
            return data

        def seal(data, value):
            data[0:4] = value.to_bytes(4, 'little')
            return data

        def calc(data):
            value = 0
            for i in range(4, 0xBC, 4):
                value = ((value << 1) | (value >> 31)) & 0xFFFFFFFF
                value = (value + int.from_bytes(data[i:i+4], 'little')) & 0xFFFFFFFF
            return value | 1

        rows = []
        slot_sets = []
        for _ in range(24):
            slots = []
            for _ in range(8):
                choice = rng.randrange(3)
                data = valid_slot()
                if choice == 0:
                    seal(data, calc(data))
                elif choice == 1:
                    seal(data, calc(data) ^ (1 << rng.randrange(1, 32)))
                else:
                    data[0:4] = bytes(4)
                slots.append(data)
            slot_sets.append(slots)
            rows.append('S ' + b''.join(slots).hex())
        slot_results = run(binary, args.original_exe, rows)
        cases['slots'] = [dict(slots=b''.join(s).hex(), expected=r) for s, r in zip(slot_sets, slot_results, strict=True)]

        rows = []
        heads = []
        for _ in range(10):
            head = bytearray(rng.getrandbits(8) for _ in range(0xA24))
            heads.append(head)
            rows.append('W ' + head.hex())
        write_results = run(binary, args.original_exe, rows)
        cases['write'] = [dict(head=h.hex(), expected=r) for h, r in zip(heads, write_results, strict=True)]

        rows = []
        loads = []
        for index in range(16):
            before = bytearray(rng.getrandbits(8) for _ in range(0xA24))
            body = bytearray(rng.getrandbits(8) for _ in range(0xA24))
            exists = 1
            if index < 6:
                data = bytes.fromhex(write_results[index]['file'])
            elif index == 6:
                exists, data = 0, b''
            elif index == 7:
                data = (0xA20).to_bytes(4, 'little') + body
            elif index == 8:
                data = (0xA24).to_bytes(4, 'little') + body
            elif index == 9:
                data = (0xA24).to_bytes(4, 'little') + body + bytes(0x1D8)
            elif index in (10, 11):
                data = (0xA24).to_bytes(4, 'little')[:3] if index == 10 else bytes(0x200)
            elif index == 12:
                data = (0xA24).to_bytes(4, 'little') + body + bytes(0x600)
            else:
                data = rng.getrandbits(32).to_bytes(4, 'little') + body
            loads.append((exists, before, data))
            rows.append(f'L {exists} {before.hex()} {data.hex() or "-"}')
        load_results = run(binary, args.original_exe, rows)
        cases['load'] = [dict(exists=e, before=b.hex(), file=d.hex(), expected=r)
                         for (e, b, d), r in zip(loads, load_results, strict=True)]

        rows = []
        merges = []

        def score():
            if rng.randrange(5) == 0:
                return bytes(5)
            return bytes([rng.choice([0, 65, 66]), rng.choice([65, 66]), 65, rng.choice([0, 1, 255, rng.randrange(256)]),
                          rng.choice([0, 0x7F, 0x80, 0xFF, rng.randrange(256)])])

        for _ in range(400):
            left = b''.join(score() for _ in range(5))
            right = b''.join(score() for _ in range(5))
            if rng.randrange(4) == 0:
                right = left[:10] + right[10:]
            lower = rng.choice([0, 1, 2, -1])
            merges.append((lower, left, right))
            rows.append(f'M {lower} {left.hex()} {right.hex()}')
        merge_results = run(binary, args.original_exe, rows)
        cases['merge'] = [dict(lower=l, left=a.hex(), right=b.hex(), expected=r)
                          for (l, a, b), r in zip(merges, merge_results, strict=True)]

        rows = []
        menus = []
        for index in range(300):
            lines = rng.randrange(1, 12)
            enabled = rng.getrandbits(lines) | (1 << rng.randrange(lines))
            blocked = rng.getrandbits(lines) & rng.getrandbits(lines) if rng.randrange(2) else 0
            if enabled & ~blocked == 0:
                blocked = 0
            visible = rng.choice([255, 255, 2, 3, 4, lines])
            wrap = rng.randrange(2)
            if not wrap:
                ends = 1 | (1 << (lines - 1))
                enabled |= ends
                blocked &= ~ends
            rate = rng.choice([1, 2, 2, 3])
            start = rng.randrange(lines)
            timer = rng.choice([0, 0, 5, 21, 22, 40])
            frames = [rng.choice([0, 0x1001, 0x2002, 0x3003, 0x1, 0x2000, 0x1000]) for _ in range(rng.randrange(1, 60))]
            menus.append(dict(lines=lines, visible=visible, wrap=wrap, rate=rate, start=start, timer=timer,
                              enabled=enabled, blocked=blocked, input=frames))
            rows.append(' '.join(map(str, ['U', lines, visible, wrap, rate, start, timer, enabled, blocked, len(frames), *frames])))
        menu_results = run(binary, args.original_exe, rows)
        cases['menu'] = [dict(**m, expected=r) for m, r in zip(menus, menu_results, strict=True)]

        codes = ['WEAKNESS', 'LEANEST', 'GLANDS', 'EGOTRIP', 'RULUR', 'SECRTWAR', 'MIGUELOH', 'TRISNTNL', 'SYNOPTIC',
                 'XILRTRNS', 'KICK_ME', 'MRWATSON', 'SM_LVIII', 'ROBRTSON', 'KIRBYFAN', 'MME_WEB', 'FANBOY', 'CINEMA',
                 'RGSGLLRY', 'UATUSEES', 'ADMNTIUM', 'CLUBNOIR', 'STICKMAN', 'FUNKYTWN']
        rows = []
        cheats = []
        for index in range(160):
            code = codes[index % len(codes)] if index < 96 else rng.choice(codes + ['LEANES', 'GLANDSX', 'NOPE', 'A'])
            code = ''.join(c.lower() if rng.randrange(3) == 0 else c for c in code)
            record = bytearray(rng.getrandbits(8) for _ in range(0xBC)) if index % 3 else bytearray(0xBC)
            flags = [rng.choice([0, 1, 0x55, -1]) for _ in range(6)]
            what_if, joel = rng.choice([0, 1, 7]), rng.choice([0, 1, 9])
            cheats.append(dict(code=code, save=record.hex(), flags=flags + [what_if, joel]))
            rows.append(' '.join(map(str, ['X', code, record.hex(), *flags, what_if, joel])))
        cases['cheat'] = [dict(**c, expected=r) for c, r in zip(cheats, run(binary, args.original_exe, rows), strict=True)]

        cases['checksum'] = [dict(**c, expected=r['checksum']) for c, r in zip(cases['checksum'], checksum_results, strict=True)]
        assert checksum_count == len(cases['checksum'])
        for case in cases['checksum']:
            assert calc(bytes.fromhex(case['save'])) == case['expected'], 'checksum model diverged from the original'
    (root/'modern/OpenSpidey.Tests/Fixtures/frontend-save.json').write_text(json.dumps(cases, separators=(',', ':'))+'\n')
    print('Captured original PC save behavior: ' + ', '.join(f'{len(v)} {k}' for k, v in cases.items()) + '.')


if __name__ == '__main__':
    main()
