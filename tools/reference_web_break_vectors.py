"""Capture actual PC BreakIntoBits emission order and RNG using independently verified fragment/glow constructor leaves."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile
from reference_knotted_web_vectors import build_reference


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True);args=parser.parse_args()
    root=Path(__file__).resolve().parent.parent
    code=build_reference(root)+r'''
#undef mProtected
std::vector<void*> allocations;std::vector<unsigned char*> glows;
void*allocate(unsigned size){void*p=calloc(1,size);allocations.push_back(p);return p;}
void ground(CVector point){events.insert(events.end(),{3,point.vx,point.vy,point.vz});}
void* __attribute__((thiscall)) fragment(void*self,const CVector*start,const CVector*end,const CVector*first,const CVector*last){
events.insert(events.end(),{4,start->vx,start->vy,start->vz,end->vx,end->vy,end->vz,first->vx,first->vy,first->vz,last->vx,last->vy,last->vz});
CKnottedWeb base(*start,*end);ground((*start+*end)>>1);for(int i=0;i<base.mNumSegs;i++)delete base.mpExtraSegs[i].mpBit;free(base.mpExtraSegs);free(base.mSegs);free(base.mpInnerLine->mSegs);delete base.mpInnerLine;return self;}
void* __attribute__((thiscall)) glow(unsigned char*self,CVector*position,int points,unsigned char cr,unsigned char cg,unsigned char cb,int cf,unsigned char sr,unsigned char sg,unsigned char sb,int sf,int fringe,int width,int reselect,int odd,int oddRange,int even,int evenRange,int decay,int lifetime){
events.insert(events.end(),{5,position->vx,position->vy,position->vz,points,cr,cg,cb,cf,sr,sg,sb,sf,fringe,width,reselect,odd,oddRange,even,evenRange,decay,lifetime});
for(int i=0;i<points*2;i++)Rnd(i&1?oddRange:evenRange);glows.push_back(self);return self;}
void side(const CVector*start,const CVector*end,CVector*out){Utils_CalcUnitFacingCamera(start,end,out);}
void __attribute__((thiscall)) die(unsigned char*web){*reinterpret_cast<unsigned short*>(web+0x46)|=0x40;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patchJump(0x4E5DA0,reinterpret_cast<void*>(&Rnd));patchJump(0x4088A0,reinterpret_cast<void*>(&allocate));patchJump(0x4F6290,reinterpret_cast<void*>(&fragment));patchJump(0x43BEE0,reinterpret_cast<void*>(&glow));patchJump(0x4E7090,reinterpret_cast<void*>(&side));
for(auto entry:std::vector<std::pair<unsigned,int>>{{0x4F66A7,6},{0x4F66AE,7},{0x4F69DC,7}})std::memset(reinterpret_cast<void*>(entry.first),0x90,entry.second);
CVector start,end;unsigned seed;while(std::cin>>start.vx>>start.vy>>start.vz>>end.vx>>end.vy>>end.vz>>G_MIKE_CAMERA[0].Position.vx>>G_MIKE_CAMERA[0].Position.vy>>G_MIKE_CAMERA[0].Position.vz>>seed){
int count=std::max(1,std::min(40,(end-start).Length()/80));CGPolyLine line(count);line.SetStartAndEnd(&start,&end);alignas(4)unsigned char raw[0x13C]={};void*table[2]={nullptr,reinterpret_cast<void*>(&die)};*reinterpret_cast<void**>(raw)=table;*reinterpret_cast<void**>(raw+0x12C)=&line;*reinterpret_cast<unsigned short*>(raw+0x46)=0x16;randomIndex=seed;events.clear();glows.clear();
reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4F66A0)(raw);std::cout<<'['<<*reinterpret_cast<unsigned short*>(raw+0x46)<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"],[";for(unsigned i=0;i<glows.size();i++){if(i)std::cout<<',';std::cout<<*reinterpret_cast<unsigned short*>(glows[i]+0x54);}std::cout<<"]]\n";free(line.mSegs);for(void*p:allocations)free(p);allocations.clear();}}
'''
    cases=[]
    for distance in [0,79,99,100,199,599,600,601,3200,4000]:
        for fractional in [0,123,-789]:
            for seed in [0,7]:
                cases.append([123456,-54321,76543,123456+distance*4096+fractional,-54321+fractional*2,76543+fractional*3,500,300,-700,seed])
    with tempfile.TemporaryDirectory(prefix='openspidey-web-break-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,case)) for case in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/web-break.json').write_text(json.dumps([dict(input=case,expected=json.loads(line)) for case,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} actual PC BreakIntoBits requests, constructor interleaving, glow angles and body retirement cases.')


if __name__=='__main__':main()
