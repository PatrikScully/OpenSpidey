"""Verify CDomePiece ctor/AI and CDomeRing vertex/tint AI with the actual PC instructions."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    code = r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <sstream>
#include <cassert>
#include <cstring>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
struct Body{unsigned vt=0;unsigned short flags=0,inquiry=0;CVector pos;short angles[3]={};unsigned short model=0;unsigned char pad1C[3]={},region=0;unsigned next=0,rgb=0;short scale[3]={4096,4096,4096};unsigned char pad2E[0x18]={};unsigned short bodyFlags=0x16;unsigned char tail[0xAC]={};};
struct Piece:Body{int scratch=0,delay=0,step=4;};static_assert(sizeof(Body)==0xF4);static_assert(sizeof(Piece)==0x100);
std::vector<int>events;void emit(std::initializer_list<int>v){events.insert(events.end(),v);}
void init(Piece&p,CVector pos,int model,int delay,int fire){p.pos=pos;p.model=model;p.delay=delay;emit({1,fire?1:0});emit({2});p.flags|=0x400;p.rgb=0;p.step=4;}
void move(Piece&p){if(p.delay)p.delay--;else{int value=p.step+int(p.rgb&255);if(value>32)value=32;else if(value<0)value=0;p.rgb=value|(value<<8)|(value<<16);}}
void output(Piece&p){std::cout<<'['<<p.pos.vx<<','<<p.pos.vy<<','<<p.pos.vz<<','<<p.model<<','<<p.delay<<','<<p.step<<','<<p.flags<<','<<p.bodyFlags<<','<<p.rgb<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}
std::string capture(Piece&p){std::ostringstream text;auto*old=std::cout.rdbuf(text.rdbuf());output(p);std::cout.rdbuf(old);events.clear();return text.str();}
#ifdef VALIDATE_ORIGINAL
void patch(unsigned a,void*t){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(t)-a-5;}
void*__attribute__((thiscall)) baseCtor(void*p){*static_cast<Body*>(p)=Body();return p;}
void __attribute__((thiscall)) initItem(void*,const char*name){emit({1,int(std::strcmp(name,"firedome")==0)});}
void __attribute__((thiscall)) attach(void*,void*){emit({2});}
void printAssert(unsigned char value,const char*){assert(value);}
#endif
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);unsigned models[7]={6,0,0,0,0,0,0};*reinterpret_cast<unsigned**>(0x6B2454)=models+1;
patch(0x460080,reinterpret_cast<void*>(&baseCtor));patch(0x460020,reinterpret_cast<void*>(&initItem));patch(0x460260,reinterpret_cast<void*>(&attach));patch(0x4015B0,reinterpret_cast<void*>(&printAssert));
for(auto range:std::vector<std::pair<unsigned,int>>{{0x4FA487,6},{0x4FA48E,7},{0x4FA545,7}})std::memset(reinterpret_cast<void*>(range.first),0x90,range.second);
#endif
CVector pos;int model,delay,fire,fadeAt,fadeStep;
while(std::cin>>pos.vx>>pos.vy>>pos.vz>>model>>delay>>fire>>fadeAt>>fadeStep){events.clear();Piece source;init(source,pos,model,delay,fire);std::vector<std::string>states{capture(source)};for(int frame=1;frame<=36;frame++){if(frame==fadeAt)source.step=fadeStep;move(source);states.push_back(capture(source));}
#ifdef VALIDATE_ORIGINAL
events.clear();Piece actual;reinterpret_cast<void(__attribute__((thiscall)) *)(void*,CVector*,int,int,int)>(0x4FA480)(&actual,&pos,model,delay,fire);
assert(capture(actual)==states[0]);for(int frame=1;frame<=36;frame++){if(frame==fadeAt)actual.step=fadeStep;reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4FA5E0)(&actual);auto captured=capture(actual);if(captured!=states[frame]){std::cerr<<states[frame]<<"\n"<<captured<<"\n";abort();}}
#endif
std::cout<<'[';for(unsigned i=0;i<states.size();i++){if(i)std::cout<<',';std::cout<<states[i];}std::cout<<"]\n";}}
'''
    rng = random.Random(0x4FA5E0)
    cases = [[12345, -54321, 67890, model, delay, fire, fade_at, step]
        for model in range(5) for delay in [-1, 0, 2, 4, 6, 8] for fire in [0, 1]
        for fade_at, step in [(0, 4), (16, -10)]]
    cases += [[*[rng.randint(-2147483648, 2147483647) for _ in range(3)], rng.randrange(5),
               rng.randint(-2147483648, 2147483647), rng.choice([0, 1, 255]), rng.randrange(37),
               rng.randint(-2147483648, 2147483647)] for _ in range(24)]
    with tempfile.TemporaryDirectory(prefix='openspidey-dome-piece-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        flags=['-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie',*flags,
            '-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        command=[str(binary)]+([str(args.original_exe.resolve())] if args.original_exe else [])
        lines=subprocess.check_output(command,input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/web-dome-pieces.json').write_text(json.dumps(
        [dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} CDomePiece ctor/36AI cases; original PC verified={bool(args.original_exe)}.')


if __name__=='__main__':main()
