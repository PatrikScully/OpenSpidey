"""Capture whole actual PC CSwinger.SwingBack, resolving its recovered-source endpoint-sign discrepancy."""
import argparse,json,subprocess,tempfile
from pathlib import Path
from reference_knotted_web_vectors import build_reference


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=build_reference(root)+r'''
#undef mProtected
#include <new>
template<class T>T&at(void*p,unsigned o){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+o);}
void*created=nullptr;CVector createdStart,createdEnd;
void*bitNew(unsigned n){assert(n==0x7C);created=calloc(1,n);return created;}
void*__attribute__((thiscall)) ctor(void*p,CVector*a,CVector*b){createdStart=*a;createdEnd=*b;return ::new(p)CKnottedWeb(*a,*b);}
void vec(CVector v){std::cout<<','<<v.vx<<','<<v.vy<<','<<v.vz;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patchJump(0x4088A0,(void*)&bitNew);patchJump(0x4F8E20,(void*)&ctor);patchJump(0x4E7090,(void*)&Utils_CalcUnitFacingCamera);
for(auto range:std::vector<std::pair<unsigned,int>>{{0x4F7550,6},{0x4F755E,7},{0x4F7667,7}})std::memset((void*)range.first,0x90,range.second);
CVector start,end,render;int low,fire;
while(std::cin>>start.vx>>start.vy>>start.vz>>end.vx>>end.vy>>end.vz>>render.vx>>render.vy>>render.vz>>low>>fire){G_MIKE_CAMERA[0].Position=CVector(500,300,-700);randomIndex=0;CKnottedWeb held(start,end);held.SetStartAndEnd(&start,&end);alignas(4)unsigned char swing[0x190]={};at<CKnottedWeb*>(swing,0x17C)=&held;at<CVector>(swing,0x10C)=start;at<int>(swing,0xF8)=fire;
reinterpret_cast<void(__attribute__((thiscall)) *)(void*,CVector*)>(0x4F74B0)(swing,&render);events.clear();created=nullptr;*(int*)0x60D224=low;reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4F7550)(swing);
std::cout<<'['<<(created?1:0);vec(at<CVector>(swing,0xFC));vec(held.mSegs[held.mNumSegs-1].End);if(created){auto&web=*static_cast<CKnottedWeb*>(created);vec(createdStart);vec(createdEnd);std::cout<<','<<web.mNumSegs<<','<<int(web.field_6C)<<','<<int(web.field_6D)<<','<<web.field_74<<','<<int(web.mCode)<<','<<int(web.mpInnerLine->mCode);vec(web.mStart);vec(CVector(web.field_58,web.field_5C,web.field_60));for(int i=0;i<web.mNumSegs;i++){vec(web.mSegs[i].End);vec(web.mpExtraSegs[i].mPos);std::cout<<','<<int(web.mSegs[i].code);}for(int i=0;i<web.mpInnerLine->mNumSegs;i++)std::cout<<','<<int(web.mpInnerLine->mSegs[i].code);}std::cout<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    cases=[[12345,-54321,67890,12345+length*4096,-54321+300*4096,67890+160*4096,23456,-65432,78901,low,fire]
           for length in [0,79,80,160,640,1800,3200] for low in [0,1,-1] for fire in [0,1,255]]
    with tempfile.TemporaryDirectory(prefix='openspidey-swing-back-create-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/swing-back-creation.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} whole actual PC4F7550 endpoint/low-memory/constructor/code2 sequences; original uses endpoint PLUS stored offset.')

if __name__=='__main__':main()
