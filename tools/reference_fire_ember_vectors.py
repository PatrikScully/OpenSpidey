"""Verify full CEmber ctor and its shared ShellEmber Move against the original PC instructions."""
import argparse
import json
import random
import shutil
import subprocess
import tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    simby = (root / 'src/game/actors/simby.cpp').read_text()
    shell = (root / 'src/game/ui/shell.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <cmath>
#include <iostream>
#include <vector>
#include <sstream>
#include <cassert>
#include <cstring>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
short word_610C48[8192];
struct Texture{} dummyTexture;
struct CBit{unsigned vtable=0,previous=0,next=0;short mAge=0;unsigned short mLifetime=0;CVector mPos,mVel,mAcc;unsigned char friction[3]={1,1,1},mDead=0;unsigned short depth=0;unsigned char protectedBit=0,mType=0;void Die(){mDead=1;}};
struct CFlatBit:CBit{short mTransDecay=0;unsigned short field_3E=0;unsigned mCodeBGR=0x2C808080;unsigned deleteAnim=0;void*mpPSXAnim=nullptr,*mpPSXFrame=nullptr;unsigned char flags=0,mNumFrames=1;signed char mFrame=0;unsigned char frac=0;short speed=128,mScale=400,mAngle=0,field_5A=0;unsigned short pad5C=0,frictionAngle=1;unsigned mPostScale=0x10001000;unsigned char pad64=0,semi=32;unsigned short clut=0;
void SetTexture(Texture*){}void SetTint(unsigned r,unsigned g,unsigned b){mCodeBGR=(mCodeBGR&0xFF000000)|r|(g<<8)|(b<<16);}void SetSemiTransparent(){mCodeBGR|=0x02000000;}};
struct CEmber:CFlatBit{CVector field_68;int field_74=0;CVector field_78;int field_84=0,field_88=0,field_8C=0;CEmber(const CVector*,int);void Move();};
static_assert(sizeof(CFlatBit)==0x68);static_assert(sizeof(CEmber)==0x90);
std::vector<int>events;int cursor;
int Rnd(int bound){int value=(cursor++*7+1)%bound;events.insert(events.end(),{bound,value});return value;}
'''
    ctor = controller_method(simby, 'CEmber::CEmber(')
    ctor = ctor.replace('*(Texture **)(*reinterpret_cast<i32*>(0x56EA9C) + 44)', '&dummyTexture')
    move = controller_method(shell, 'void CShellEmber::Move(')
    move = move.replace('CShellEmber::Move', 'CEmber::Move')
    move = move.replace('this->field_68', 'this->field_68.vx').replace('this->field_70', 'this->field_68.vz')
    move = move.replace('this->field_78', 'this->field_78.vx')
    move = move.replace('this->field_7C', 'this->field_78.vy').replace('this->field_80', 'this->field_78.vz')
    code += ctor + '\n' + move + r'''
void vec(CVector v){std::cout<<v.vx<<','<<v.vy<<','<<v.vz;}
void output(CEmber&s){std::cout<<'[';vec(s.mPos);std::cout<<',';vec(s.field_68);std::cout<<',';vec(s.mVel);std::cout<<','<<s.mScale<<','<<s.field_78.vx<<','<<s.field_78.vy<<','<<s.field_78.vz<<','<<s.field_74<<','<<s.field_84<<','<<s.field_88<<','<<s.field_8C<<','<<s.mCodeBGR<<','<<int(s.mDead)<<",[";
for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}
std::string capture(CEmber&s){std::ostringstream text;auto*old=std::cout.rdbuf(text.rdbuf());output(s);std::cout.rdbuf(old);events.clear();return text.str();}
#ifdef VALIDATE_ORIGINAL
void patch(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
void* __attribute__((thiscall)) flatConstructor(void*p){*static_cast<CFlatBit*>(p)=CFlatBit();return p;}
void __attribute__((thiscall)) setTexture(void*,Texture*t){assert(t==&dummyTexture);}
#endif
int main(int argc,char**argv){for(int i=0;i<4096;i++){double angle=i*0.001536096911877394;word_610C48[i*2]=std::sin(angle)*4096.;word_610C48[i*2+1]=std::cos(angle)*4096.;}
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);std::memcpy(reinterpret_cast<void*>(0x610C48),word_610C48,sizeof(word_610C48));
alignas(4)unsigned char frames[48]={};*reinterpret_cast<Texture**>(frames+44)=&dummyTexture;*reinterpret_cast<void**>(0x56EA9C)=frames;
patch(0x40DA90,reinterpret_cast<void*>(&flatConstructor));patch(0x408DF0,reinterpret_cast<void*>(&setTexture));patch(0x4E5DA0,reinterpret_cast<void*>(&Rnd));
// Only Windows exception-chain access is omitted. All ctor branches, arithmetic and RNG remain genuine PC instructions.
for(auto range:std::vector<std::pair<unsigned,int>>{{0x4A28C7,6},{0x4A28CE,7},{0x4A29DD,7}})std::memset(reinterpret_cast<void*>(range.first),0x90,range.second);
#endif
CVector position;int scale,seed;
while(std::cin>>position.vx>>position.vy>>position.vz>>scale>>seed){events.clear();cursor=seed;CEmber source(&position,scale);std::vector<std::string>states{capture(source)};
for(int frame=1;frame<=48;frame++){if(!source.mDead)source.Move();states.push_back(capture(source));}
#ifdef VALIDATE_ORIGINAL
events.clear();cursor=seed;alignas(4)unsigned char raw[0x90]={};auto&actual=*reinterpret_cast<CEmber*>(raw);
reinterpret_cast<void(__attribute__((thiscall)) *)(void*,CVector*,int)>(0x4A28C0)(raw,&position,scale);
auto compare=[&](int frame){auto value=capture(actual);if(value!=states[frame]){std::cerr<<"Mismatch Ember frame "<<frame<<"\nsource="<<states[frame]<<"\nactual="<<value<<'\n';abort();}};compare(0);
for(int frame=1;frame<=48;frame++){if(!actual.mDead)reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4A2A20)(raw);compare(frame);}
#endif
std::cout<<'[';for(unsigned i=0;i<states.size();i++){if(i)std::cout<<',';std::cout<<states[i];}std::cout<<"]\n";}}
'''
    rng = random.Random(0x4A28C0)
    cases = [[12345, -54321, 67890, scale, seed]
             for scale in [0, 1, 128, 256, 512, -1, -256, 2147483647, -2147483648]
             for seed in [0, 1, 73, 511]]
    cases += [[*[rng.randint(-2147483648, 2147483647) for _ in range(3)],
               rng.randint(-2147483648, 2147483647), rng.randrange(4096)] for _ in range(28)]
    with tempfile.TemporaryDirectory(prefix='openspidey-fire-ember-') as directory:
        cpp = Path(directory) / 'reference.cpp'
        binary = Path(directory) / 'reference'
        cpp.write_text(code)
        flags = ['-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', *flags,
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp),
                        str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        command = [str(binary)] + ([str(args.original_exe.resolve())] if args.original_exe else [])
        inputs = ('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()
        try:
            lines = subprocess.check_output(command, input=inputs).decode().splitlines()
        except subprocess.CalledProcessError:
            shutil.copyfile(cpp, '/tmp/openspidey-fire-ember-reference.cpp')
            shutil.copyfile(binary, '/tmp/openspidey-fire-ember-reference')
            Path('/tmp/openspidey-fire-ember-cases.txt').write_bytes(inputs)
            raise
    (root / 'modern/OpenSpidey.Tests/Fixtures/fire-embers.json').write_text(json.dumps(
        [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Generated {len(cases)} CEmber ctor/48Move sequences; original PC instructions verified={bool(args.original_exe)}.')


if __name__ == '__main__':
    main()
