"""Capture CSwingBack source and verify actual PC retraction/fade/age/death Move instructions."""
import argparse,json,random,subprocess,tempfile
from pathlib import Path
from reference_knotted_web_vectors import build_reference
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=build_reference(root)
    code=code.replace('unsigned char pad[0x3C]={};int mNumSegs','unsigned char pad[0xC]={};short mAge=0;unsigned short lifetime=0;unsigned char pad10[0x27]={};unsigned char dead=0;unsigned char pad38[4]={};int mNumSegs')
    code=code.replace('void SetSemiTransparent(){}', 'void SetSemiTransparent(){mCode|=2;for(int i=0;i<mNumSegs;i++)mSegs[i].code|=2;}void Die(){dead=1;}')
    code+=r'''
#undef mProtected
struct Player{unsigned pad[2]={};CVector mPos;}player;Player*G_MECHLIST_PLAYER=&player;
unsigned Utils_Dist(const CVector&a,const CVector&b){auto d=(a-b)>>12;return M3dMaths_SquareRoot0(d.vx*d.vx+d.vy*d.vy+d.vz*d.vz);}
unsigned Utils_CrapDist(const CVector&a,const CVector&b){auto d=a-b;int x=abs(d.vx),y=abs(d.vy),z=abs(d.vz);if(x<y)std::swap(x,y);if(y<z)std::swap(y,z);if(x<y)std::swap(x,y);return unsigned((x+(y>>1)+(z>>2))>>12);}
namespace spidey{namespace math{int approximateDistance(const CVector&a,const CVector&b){return Utils_CrapDist(a,b);}}}
struct CSwingBack:CKnottedWeb{int mRetracting=0;CSwingBack(const CVector&a,const CVector&b):CKnottedWeb(a,b){}void Move();};
void __attribute__((thiscall)) tint(CFlatBit*b,unsigned char r,unsigned char g,unsigned char blue){b->SetTint(r,g,blue);}
void __attribute__((thiscall)) die(CGPolyLine*p){p->Die();}
void state(CSwingBack&s){std::cout<<'['<<s.mRetracting<<','<<s.mAge<<','<<int(s.dead)<<','<<s.mNumSegs<<','<<s.mStart.vx<<','<<s.mStart.vy<<','<<s.mStart.vz<<','<<s.field_58<<','<<s.field_5C<<','<<s.field_60<<','<<unsigned(s.mStartR|(s.mStartG<<8)|(s.mStartB<<16)|(unsigned(s.mCode)<<24))<<','<<unsigned(s.mpInnerLine->mStartR|(s.mpInnerLine->mStartG<<8)|(s.mpInnerLine->mStartB<<16)|(unsigned(s.mpInnerLine->mCode)<<24));for(int i=0;i<s.mNumSegs;i++){auto&e=s.mSegs[i];auto&k=s.mpExtraSegs[i];std::cout<<','<<e.End.vx<<','<<e.End.vy<<','<<e.End.vz<<','<<unsigned(e.r|(e.g<<8)|(e.b<<16)|(unsigned(e.code)<<24))<<','<<k.mPos.vx<<','<<k.mPos.vy<<','<<k.mPos.vz<<','<<k.mpBit->mPos.vx<<','<<k.mpBit->mPos.vy<<','<<k.mpBit->mPos.vz<<','<<k.mpBit->mCodeBGR;}for(int i=0;i<s.mpInnerLine->mNumSegs;i++){auto&e=s.mpInnerLine->mSegs[i];std::cout<<','<<e.End.vx<<','<<e.End.vy<<','<<e.End.vz<<','<<unsigned(e.r|(e.g<<8)|(e.b<<16)|(unsigned(e.code)<<24));}std::cout<<']';}
'''
    code+=controller_method((root/'src/engine/world/bit2.cpp').read_text(),'void CSwingBack::Move(')+'\n'
    code+=r'''
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patchJump(0x4E5DA0,(void*)&Rnd);patchJump(0x408CC0,(void*)&tint);patchJump(0x408930,(void*)&die);patchJump(0x4E7090,(void*)&Utils_CalcUnitFacingCamera);
for(int i=0;i<4096;i++){double a=i*0.001536096911877394;G_RCOSSIN_TBL[i].sin=std::sin(a)*4096.;G_RCOSSIN_TBL[i].cos=std::cos(a)*4096.;}std::memcpy((void*)0x610C48,G_RCOSSIN_TBL,sizeof(G_RCOSSIN_TBL));*(Player**)0x6A9038=&player;
CVector start,end;int fire;while(std::cin>>start.vx>>start.vy>>start.vz>>end.vx>>end.vy>>end.vz>>player.mPos.vx>>player.mPos.vy>>player.mPos.vz>>fire){G_MIKE_CAMERA[0].Position=CVector(500,300,-700);randomIndex=0;events.clear();CSwingBack s(start,end);s.SetStartAndEnd(&start,&end);Utils_CalcUnitFacingCamera(&start,&end,(CVector*)&s.field_58);for(int i=0;i<s.mNumSegs;i++)s.mpExtraSegs[i].mPos=s.mSegs[i].End;s.field_6D=1;s.SetSemiTransparent();s.mpInnerLine->SetSemiTransparent();s.field_74=fire;std::cout<<"{\"states\":[";state(s);for(int frame=0;frame<65&&!s.dead;frame++){G_TIMER_RELATED=500+frame*2;*(unsigned*)0x6B4CA8=G_TIMER_RELATED;
// Geometry/age/fade run genuine PC; already-verified knotted Move is the explicit source boundary including fire relocation.
patchJump(0x4F9150,(void*)+[] __attribute__((thiscall)) (CKnottedWeb*p){p->Move();});
reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4F5960)(&s);std::cout<<',';state(s);}std::cout<<"],\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]}\n";}}
'''
    cases=[[12345,-54321,67890,12345+dx*4096,-54321+dy*4096,67890+dz*4096,12345,-54321,67890,fire]
           for dx,dy,dz in [(0,0,0),(0,100,0),(0,900,0),(0,1800,0),(0,-900,0),(640,0,0),(640,900,320),(2400,0,0)] for fire in [0,1]]
    with tempfile.TemporaryDirectory(prefix='openspidey-swing-back-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/released-swing-web.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} actual PC released swing web sequences through retraction/age/death, including source fire boundary.')

if __name__=='__main__':main()
