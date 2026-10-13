"""Compile rock constructor/motion and compare every snapshot with original PC machine bodies."""
import argparse,json,random,subprocess,tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True);args=parser.parse_args()
    root=Path(__file__).resolve().parent.parent
    source=(root/'src/game/effects/effects.cpp').read_text()
    ctor=controller_method(source,'CBouncingRock::CBouncingRock(')
    move=controller_method(source,'void CBouncingRock::Move(').replace('if (this->mLifetime > 0x3C)','if (++this->mLifetime > 0x3C)')
    code=r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <cmath>
#include <iostream>
struct Texture{};struct Heading{short sin,cos;}G_RCOSSIN_TBL[4096];
unsigned rng;int textureIndex=-1;int Rnd(int n){rng=rng*1664525u+1013904223u;return rng%unsigned(n);}
struct CBouncingRock{
 unsigned char raw[0x70]={};CVector&mPos=*reinterpret_cast<CVector*>(raw+0x10);CVector&mVel=*reinterpret_cast<CVector*>(raw+0x1C);
 unsigned short&mLifetime=*reinterpret_cast<unsigned short*>(raw+0xE);short&mScale=*reinterpret_cast<short*>(raw+0x56);short&mAngle=*reinterpret_cast<short*>(raw+0x58);short&field_5A=*reinterpret_cast<short*>(raw+0x5A);unsigned&mPostScale=*reinterpret_cast<unsigned*>(raw+0x60);unsigned char&mSemiTransparencyRate=raw[0x65];int&field_68=*reinterpret_cast<int*>(raw+0x68);int&field_6C=*reinterpret_cast<int*>(raw+0x6C);
 CBouncingRock(CVector*,int,unsigned);void Move();void Die(){raw[0x37]=1;}void SetTexture(Texture*p){textureIndex=int(reinterpret_cast<unsigned>(p));}
};
unsigned frames[12]={};unsigned G_TEXTURE_RELATED=reinterpret_cast<unsigned>(frames);
'''
    code+=ctor+'\n'+move+r'''
void patch(unsigned address,void*fn){unsigned char*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(fn)-address-5;}
void*__attribute__((thiscall))baseCtor(unsigned char*p){*reinterpret_cast<short*>(p+0x56)=400;*reinterpret_cast<unsigned*>(p+0x60)=0x10001000;p[0x65]=32;*reinterpret_cast<unsigned*>(p+0x3C)=0x2C808080;return p;}
void __attribute__((thiscall))texture(unsigned char*p,Texture*t){textureIndex=int(reinterpret_cast<unsigned>(t));}
void __attribute__((thiscall))die(unsigned char*p){p[0x37]=1;}
void setup(const char*path){original_executable::loadOriginalImage(path);patch(0x40DA90,(void*)&baseCtor);patch(0x408DF0,(void*)&texture);patch(0x408930,(void*)&die);patch(0x4E5DA0,(void*)&Rnd);
 *reinterpret_cast<unsigned*>(0x56EA9C)=reinterpret_cast<unsigned>(frames);std::memcpy(reinterpret_cast<void*>(0x610C48),G_RCOSSIN_TBL,sizeof G_RCOSSIN_TBL);
 unsigned char*p=reinterpret_cast<unsigned char*>(0x43B557);assert(p[0]==0x64);p[0]=0x31;p[1]=0xC0;std::memset(p+2,0x90,4);
 for(unsigned a:{0x43B55Eu,0x43B666u}){p=reinterpret_cast<unsigned char*>(a);assert(p[0]==0x64);std::memset(p,0x90,7);}
}
std::vector<long long>snap(unsigned char*p){std::vector<long long>v;for(int o:{0x10,0x14,0x18,0x1C,0x20,0x24})v.push_back(*reinterpret_cast<int*>(p+o));v.push_back(*reinterpret_cast<unsigned short*>(p+0xE));for(int o:{0x56,0x58,0x5A})v.push_back(*reinterpret_cast<short*>(p+o));v.push_back(*reinterpret_cast<unsigned*>(p+0x60));v.push_back(p[0x65]);v.push_back(*reinterpret_cast<int*>(p+0x68));v.push_back(*reinterpret_cast<int*>(p+0x6C));v.push_back(p[0x37]==0);v.push_back(textureIndex);return v;}
void print(std::vector<long long>v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);for(int i=0;i<4096;i++){double a=i*0.001536096911877394;G_RCOSSIN_TBL[i]={short(std::sin(a)*4096),short(std::cos(a)*4096)};}frames[5]=2;frames[11]=5;setup(argv[1]);
 CVector pos;int ground;unsigned selector,seed;while(std::cin>>pos.vx>>pos.vy>>pos.vz>>ground>>selector>>seed){rng=seed;CBouncingRock recovered(&pos,ground,selector);recovered.mSemiTransparencyRate=selector==0x3288E271?0:32;
 unsigned char pc[0x70]={};rng=seed;reinterpret_cast<void*(__attribute__((thiscall))*)(void*,CVector*,int,unsigned)>(0x43B550)(pc,&pos,ground,selector);
 std::cout<<'[';int count=0;while(true){assert(snap(pc)==snap(recovered.raw));if(count++)std::cout<<',';print(snap(pc));if(pc[0x37])break;recovered.Move();reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x43B6B0)(pc);assert(count<100);}std::cout<<"]\n";
 }}
'''
    cases=[]
    for selector in [0x28001F00,0x3288E271]:
        for ground in [-2147483648,-1,0,1,100000,2147483647]:
            for seed in [0,1,2,99,0xffffffff]:cases.append([2147483600,0,-2147483600,ground,selector,seed])
    rng=random.Random(0x43B550)
    for i in range(100):cases.append([*[rng.randint(-2147483648,2147483647) for _ in range(4)],rng.choice([0x28001F00,0x3288E271]),rng.randrange(2**32)])
    with tempfile.TemporaryDirectory(prefix='rock-source-') as temp:
        path=Path(temp);(path/'reference.cpp').write_text(code)
        subprocess.run(['g++','-m32','-std=c++17','-O0','-fwrapv','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(path/'reference.cpp'),'-o',str(path/'reference')],check=True)
        result=subprocess.run([str(path/'reference'),str(args.original_exe)],input='\n'.join(' '.join(map(str,c)) for c in cases),text=True,capture_output=True,check=True)
    out=root/'modern/OpenSpidey.Tests/Fixtures/bouncing-rock.json';out.write_text(json.dumps({'originalPcConstructorAndMoveVerified':True,'cases':[{'input':c,'states':json.loads(s)} for c,s in zip(cases,result.stdout.splitlines(),strict=True)]},separators=(',',':'))+'\n')
    print(f'Original PC constructor and motion verified: {len(cases)} trajectories.')
if __name__=='__main__':main()
