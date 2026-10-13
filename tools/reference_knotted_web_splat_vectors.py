"""Compile real knotted splat constructor/orientation/Move; optionally compare its shared original PC Move."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    web = (root / 'src/game/player/web.cpp').read_text()
    bits = (root / 'src/engine/world/bit.cpp').read_text()
    maths = (root / 'src/engine/rendering/ps2funcs.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <cmath>
#include <iostream>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
using VECTOR=CVector;using SVECTOR=CSVector;struct SSinCos{short sin,cos;};SSinCos G_RCOSSIN_TBL[4096];
VECTOR G_OP12_RESULT,G_GENERAL_LONG_VECTOR,G_WTF_OP12,G_FTW_OP12;
void gte_stlvnl(VECTOR*v){*v=G_GENERAL_LONG_VECTOR;}int randomIndex;int Rnd(int n){return (randomIndex++*7+1)%n;}
void Bit_ReduceRGB(u32*,int);void Utils_CalcPerps(CVector*,CVector*,CVector*);
struct CQuadBit{CVector mPos,mPosB,mPosC,mPosD;u32 mTint=0x808080;short mAge=0;int mType=0;bool dead=false;
void SetTexture(unsigned){}void SetSemiTransparent(){}void Die(){dead=true;}void OrientUsing(CVector*,SVECTOR*,int,int,int);};
struct CKnottedWebSplat:CQuadBit{int field_84=0,field_88=0;CVector field_8C,field_98,field_A4;CKnottedWebSplat(const CVector*,const CVector*);void Move();};
'''
    for signature in ['i32 M3dMaths_SquareRoot0(', 'void gte_ldlvl(', 'void gte_ldopv1(',
                      'void gte_ldopv2(', 'void gte_op12(', 'void gte_sqr0(']:
        code += controller_method(maths, signature) + '\n'
    code += controller_method((root / 'src/compatibility/fixed_vector_bridge.cpp').read_text(), 'int CVector::Length(') + '\n'
    code += controller_method((root / 'src/core/utils.cpp').read_text(), 'void Utils_CalcPerps(') + '\n'
    code += controller_method(bits, 'void Bit_ReduceRGB(') + '\n'
    code += controller_method(bits, 'void CQuadBit::OrientUsing(CVector *a2, SVECTOR *a3, i32 a4, i32 a5, i32 a6)') + '\n'
    code += controller_method(web, 'void CKnottedWebSplat::Move(') + '\n'
    code += controller_method(web, 'CKnottedWebSplat::CKnottedWebSplat(') + r'''
void vector(CVector v){std::cout<<v.vx<<','<<v.vy<<','<<v.vz;}
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);
#endif
for(int i=0;i<4096;i++){double a=i*0.001536096911877394;G_RCOSSIN_TBL[i].sin=std::sin(a)*4096.;G_RCOSSIN_TBL[i].cos=std::cos(a)*4096.;}
CVector p,n;int variant;while(std::cin>>p.vx>>p.vy>>p.vz>>n.vx>>n.vy>>n.vz>>randomIndex>>variant){CKnottedWebSplat splat(&p,&n);splat.mTint=variant?0x007BFF:0x808080;
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0xB0]={};*reinterpret_cast<short*>(raw+0xC)=splat.mAge;*reinterpret_cast<unsigned*>(raw+0x6C)=splat.mTint;*reinterpret_cast<int*>(raw+0x84)=32;*reinterpret_cast<int*>(raw+0x88)=splat.field_88;
*reinterpret_cast<CVector*>(raw+0x10)=splat.mPos;*reinterpret_cast<CVector*>(raw+0x3C)=splat.mPosB;*reinterpret_cast<CVector*>(raw+0x48)=splat.mPosC;*reinterpret_cast<CVector*>(raw+0x54)=splat.mPosD;
*reinterpret_cast<CVector*>(raw+0x8C)=splat.field_8C;*reinterpret_cast<CVector*>(raw+0x98)=splat.field_98;*reinterpret_cast<CVector*>(raw+0xA4)=splat.field_A4;
#endif
std::cout<<'[';for(int frame=0;frame<85;frame++){if(frame){if(!splat.dead)splat.Move();std::cout<<',';}
#ifdef VALIDATE_ORIGINAL
if(frame&&!(raw[0x37]&1))reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4A5E40)(raw);
assert(splat.mAge==*reinterpret_cast<short*>(raw+0xC)&&splat.mTint==*reinterpret_cast<unsigned*>(raw+0x6C)&&splat.field_88==*reinterpret_cast<int*>(raw+0x88)&&splat.dead==bool(raw[0x37]&1));
assert(splat.mPos.vx==*reinterpret_cast<int*>(raw+0x10)&&splat.mPos.vy==*reinterpret_cast<int*>(raw+0x14)&&splat.mPos.vz==*reinterpret_cast<int*>(raw+0x18));
#endif
std::cout<<'['<<splat.field_88<<','<<splat.mAge<<','<<splat.mTint<<','<<splat.dead<<',';vector(splat.mPos);std::cout<<',';vector(splat.mPosB);std::cout<<',';vector(splat.mPosC);std::cout<<',';vector(splat.mPosD);std::cout<<']';}std::cout<<"]\n";}}
'''
    rng = random.Random(0x4F5BC0)
    normals = [(0, -4096, 0), (0, 4096, 0), (4096, 0, 0), (0, 0, 4096), (0, 0, 0),
               (-32768, 32767, -32768)] + [tuple(rng.randint(-32768, 32767) for _ in range(3)) for _ in range(18)]
    cases = [[12345, -54321, 67890, *normal, seed, variant]
             for normal in normals for seed in [0, 73, 511] for variant in [0, 1]]
    with tempfile.TemporaryDirectory(prefix='openspidey-knotted-splat-') as directory:
        cpp = Path(directory) / 'reference.cpp'
        binary = Path(directory) / 'reference'
        cpp.write_text(code)
        extra = ['-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', *extra,
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp),
                        str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        command = [str(binary)] + ([str(args.original_exe.resolve())] if args.original_exe else [])
        lines = subprocess.check_output(command, input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/knotted-web-splats.json').write_text(json.dumps(
        [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Generated {len(cases)} source knotted splat constructor/colour/lifetime cases; original PC shared Move verified={bool(args.original_exe)}.')


if __name__ == '__main__':
    main()
