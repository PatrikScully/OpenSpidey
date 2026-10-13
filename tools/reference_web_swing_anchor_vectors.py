"""Capture actual PC CSwinger constructor/AI/SetRenderEnd matrices, lines and direct clock policy."""
import argparse
import json
from pathlib import Path
import random
import shutil
import subprocess
import tempfile
from reference_knotted_web_vectors import build_reference


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    code = build_reference(root) + r'''
#undef mProtected
#include <new>
template<class T>T&at(void*p,unsigned o){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+o);}
void*__attribute__((thiscall)) bodyCtor(void*p){std::memset(p,0,0xF4);return p;}
void*bitNew(unsigned n){return calloc(1,n);}
void*__attribute__((thiscall)) lineCtor(void*p,const CVector*a,const CVector*b){return ::new(p) CKnottedWeb(*a,*b);}
void facing(const CVector*a,const CVector*b,CVector*c){Utils_CalcUnitFacingCamera(const_cast<CVector*>(a),const_cast<CVector*>(b),c);}
void emitVector(CVector v){std::cout<<','<<v.vx<<','<<v.vy<<','<<v.vz;}
void emitMatrix(void*p){for(unsigned o=0;o<18;o+=2)std::cout<<','<<at<short>(p,o);std::cout<<','<<at<unsigned short>(p,18);emitVector(at<CVector>(p,20));}
void state(void*p,int frameScale){auto&line=*at<CKnottedWeb*>(p,0x17C);std::cout<<'['<<at<unsigned>(p,0x7C)<<','<<at<short>(p,0x14)<<','<<at<short>(p,0x16)<<','<<at<short>(p,0x18)<<','<<at<int>(p,0x180)<<','<<at<int>(p,0x184)<<','<<at<int>(p,0x188)<<','<<at<int>(p,0x18C)<<','<<at<int>(p,0xF8);emitVector(at<CVector>(p,0xFC));emitVector(at<CVector>(p,0x10C));emitMatrix(static_cast<unsigned char*>(p)+0x118);emitMatrix(static_cast<unsigned char*>(p)+0x138);for(unsigned o:{0x158u,0x164u,0x170u})emitVector(at<CVector>(p,o));
CVector end=at<CVector>(p,0x10C)-at<int>(p,0x108)*at<CVector>(p,0x170);emitVector(end);std::cout<<','<<int(at<int>(p,0x180)>=4096)<<','<<short((frameScale*(at<int>(p,0x180)-2048))>>11)<<','<<line.mNumSegs<<','<<int(line.field_6C)<<','<<int(line.field_6D)<<','<<line.field_74;emitVector(line.mStart);emitVector(CVector(line.field_58,line.field_5C,line.field_60));for(int i=0;i<line.mNumSegs;i++){emitVector(line.mSegs[i].End);emitVector(line.mpExtraSegs[i].mPos);}emitVector(line.mSegs[line.mNumSegs-1].End+at<CVector>(p,0xFC));std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patchJump(0x460080,(void*)&bodyCtor);patchJump(0x4088A0,(void*)&bitNew);patchJump(0x4F8E20,(void*)&lineCtor);patchJump(0x4E7090,(void*)&facing);
// Windows SEH bookkeeping only; mathematical/control instructions remain original.
for(auto range:std::vector<std::pair<unsigned,int>>{{0x4F6F07,6},{0x4F6F0E,7},{0x4F71B1,7}})std::memset((void*)range.first,0x90,range.second);
for(int i=0;i<4096;i++){double angle=i*0.001536096911877394;G_RCOSSIN_TBL[i].sin=std::sin(angle)*4096.;G_RCOSSIN_TBL[i].cos=std::cos(angle)*4096.;}std::memcpy((void*)0x610C48,G_RCOSSIN_TBL,sizeof(G_RCOSSIN_TBL));
long long input[28];while(std::cin>>input[0]){for(int i=1;i<28;i++)std::cin>>input[i];alignas(4)unsigned char swing[0x190]={},basis[32]={};CVector anchor(input[0],input[1],input[2]),direction(input[17],input[18],input[19]),render(input[23],input[24],input[25]);for(int i=0;i<9;i++)at<short>(basis,2*i)=input[4+i];at<unsigned short>(basis,18)=input[13];at<CVector>(basis,20)=CVector(input[14],input[15],input[16]);unsigned clock=input[20];G_MIKE_CAMERA[0].Position=CVector(500,300,-700);*(unsigned*)0x6B4CA8=clock;events.clear();randomIndex=0;
reinterpret_cast<void*(__attribute__((thiscall)) *)(void*,CVector*,int,void*,CVector*)>(0x4F6F00)(swing,&anchor,input[3],basis,&direction);std::cout<<"{\"states\":[";state(swing,input[27]);at<int>(swing,0xF8)=input[22];at<short>(swing,0x18)=input[26];for(int frame=0;frame<8;frame++){clock+=unsigned(input[21]);*(unsigned*)0x6B4CA8=clock;reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4F7310)(swing);std::cout<<',';state(swing,input[27]);reinterpret_cast<void(__attribute__((thiscall)) *)(void*,CVector*)>(0x4F74B0)(swing,&render);std::cout<<',';state(swing,input[27]);}std::cout<<"],\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]}\n";
 auto*line=at<CKnottedWeb*>(swing,0x17C);for(int i=0;i<line->mNumSegs;i++)delete line->mpExtraSegs[i].mpBit;free(line->mpExtraSegs);free(line->mSegs);free(line->mpInnerLine->mSegs);delete line->mpInnerLine;free(line);}}
'''
    rng = random.Random(0x4F7310)
    matrices = [[4096,0,0,0,4096,0,0,0,4096],[0,0,4096,0,4096,0,-4096,0,0],
                [4096,0,0,0,0,-4096,0,4096,0],[3000,1000,2000,-1000,3500,700,1900,-800,3200]]
    cases = [[12345,-54321,67890,length,*matrix,0xBEEF,123456789,-987654321,213456789,
              0,-2048,0,clock,delta,fire,23456,-65432,78901,roll,scale]
             for matrix in matrices for length in [0,79,80,640,1800,3200,15000]
             for clock,delta in [(500,0),(500,2),(4294967292,6),(10,-2)]
             for fire,roll,scale in [(0,0,24),(1,900,48)]]
    with tempfile.TemporaryDirectory(prefix='openspidey-swing-anchor-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        requests='\n'.join(' '.join(map(str,c)) for c in cases)+'\n'
        try: lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=requests.encode()).decode().splitlines()
        except subprocess.CalledProcessError:
            shutil.copy2(binary,'/tmp/openspidey-swing-anchor-reference');shutil.copy2(cpp,'/tmp/openspidey-swing-anchor-reference.cpp');Path('/tmp/openspidey-swing-anchor-input.txt').write_text(requests);raise
    (root/'modern/OpenSpidey.Tests/Fixtures/web-swing-anchor.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} actual PC CSwinger sequences, with genuine unpatched arc/matrix routines and verified knotted allocation/camera boundaries.')

if __name__=='__main__':main()
