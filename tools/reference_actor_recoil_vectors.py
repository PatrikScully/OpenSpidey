"""Compile recoil list/sphere/environment and elastic/fall-back leaves; verify original PC numerical/caller stages."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method
from reference_thug_brain_vectors import literal_header


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent;maths=(root/'src/engine/rendering/ps2funcs.cpp').read_text();ai=(root/'src/engine/world/ai.cpp').read_text()
    code=literal_header(root/'tools/reference_actor_laser_vectors.py')
    code=code.replace('#include <vector>','#include <vector>\n#ifdef VALIDATE_ORIGINAL\n#include "original_executable.h"\n#endif\nstruct SHitInfo{u8 field_0=0,field_1=0;short pad=0;int field_4=0;u16 field_8=0;short pad2=0;CVector field_C;short field_18=0;u16 field_1A=0;};')
    code=code.replace('CBaddy*G_LINE_TO_SPHERE_BEST_BODY', 'CBaddy*G_LINE_TO_SPHERE_BEST_BODY')
    code=code.replace('int LaserCollision(SLineInfo*,CVector*,int);', 'CSVector mAngles;CVector field_2FC;int field_2A8=0;short mHealth=90;int StruckId=-1;void Hit(SHitInfo*);CBody*StruckGameObject(int,int);void ElasticCollision(CBody*,CBody*,CVector&,int);void CheckFallBack();int PathCheck(CVector*,CVector*,int,int);')
    code=code.replace('CBaddy bodies[4]', 'CBaddy bodies[6]')
    code=code.replace('int queryIndex=0,hitCodes[2]={},environmentHit=0,randomValue=0;', 'int queryIndex=0,hitCodes[2]={},environmentHit=0,randomValue=0;int environmentHits[2]={},environmentIndex=0;')
    code=code.replace('l->pItem=pointer(environmentHit);', 'l->pItem=environmentHits[environmentIndex++]?&bodies[5]:nullptr;')
    code+='\nCBaddy*G_BADDY_LIST=nullptr;\n#define DoAssert(...) ((void)0)\nstruct SSinCos{short sin,cos;};SSinCos G_RCOSSIN_TBL[4096];int pathResult=0;\n'
    code+='void CBaddy::Hit(SHitInfo*h){events.insert(events.end(),{7,id,h->field_0,h->field_4,h->field_C.vx,h->field_C.vy,h->field_C.vz,h->field_18,h->field_1A});}\n'
    code+='int CBaddy::PathCheck(CVector*a,CVector*b,int hit,int range){events.insert(events.end(),{8,a->vx,a->vy,a->vz,b->vx,b->vy,b->vz,hit,range});return pathResult;}\n'
    code+='struct CAIProc_RotY{CBaddy*pBaddy;int field_20=0,field_24=0,field_10=0,field_14=0;CAIProc_RotY(CBaddy*a,int yaw,int frames,int mask){pBaddy=a;field_20=frames;field_24=yaw/frames;field_14=mask;events.insert(events.end(),{9,yaw,frames,mask});}};\n'
    for sig in ['i32 M3dMaths_SquareRoot0(', 'void gte_ldlvl(', 'void gte_ldopv1(', 'void gte_ldopv2(', 'void gte_op12(', 'void gte_sqr0(', 'void gte_stlvnl0(', 'void gte_ldlv0(', 'void gte_lddp(', 'void gsub_46D930(', 'void gsub_46DEB0(', 'void gte_gpf(', 'void VectorNormal(']:code+=controller_method(maths,sig).replace('vertexRegister','G_VERTEX_REGISTER')+'\n'
    code+=controller_method((root/'src/compatibility/fixed_vector_bridge.cpp').read_text(),'int CVector::Length(')+'\n'
    code+=controller_method((root/'src/engine/world/collision.cpp').read_text(),'CBody * M3dColij_LineToSphere(')+'\n'
    code+=controller_method((root/'src/core/utils.cpp').read_text(),'CBody* Utils_CheckObjectCollision(')+'\n'
    code+=controller_method((root/'src/core/utils.cpp').read_text(),'void Utils_RotateY(')+'\n'
    baddy=(root/'src/game/actors/baddy.cpp').read_text()
    for sig in ['CBody* CBaddy::StruckGameObject(', 'void CBaddy::ElasticCollision(']:code+=controller_method(baddy,sig)+'\n'
    code+=controller_method((root/'src/game/actors/thug.cpp').read_text(),'void CThug::CheckFallBack(').replace('if ( this->PathCheck(&this->mPos, &(this->mPos + a3), 0, 55) == 2 )', 'CVector end = this->mPos + a3;\n\tif ( this->PathCheck(&this->mPos, &end, 0, 55) == 2 )')+'\n'
    code+=r'''
#ifdef VALIDATE_ORIGINAL
void patchJump(unsigned address,void*function){auto p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
void __attribute__((thiscall)) hitOriginal(void*,SHitInfo*h){bodies[1].Hit(h);}
CBody* checkOriginal(CVector*a,CVector*b,CBody*list,CBody*){return Utils_CheckObjectCollision(a,b,list,&bodies[0]);}
void noAssert(int,const char*){}
void* allocateOriginal(int){static unsigned char memory[0x28];return memory;}
void* __attribute__((thiscall)) rotateOriginal(void*self,void*,int yaw,int frames,int mask){events.insert(events.end(),{9,yaw,frames,mask});return self;}
int __attribute__((thiscall)) pathOriginal(void*,CVector*a,CVector*b,int hit,int range){return bodies[0].PathCheck(a,b,hit,range);}
#endif
void eventJson(){std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<']';}
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patchJump(0x4E6470,reinterpret_cast<void*>(&checkOriginal));patchJump(0x4015B0,reinterpret_cast<void*>(&noAssert));
patchJump(0x455390,reinterpret_cast<void*>(&allocateOriginal));patchJump(0x401060,reinterpret_cast<void*>(&rotateOriginal));patchJump(0x403310,reinterpret_cast<void*>(&pathOriginal));
// Omit only Windows SEH registration for the CheckFallBack caller.
std::memset(reinterpret_cast<void*>(0x4D8D77),0x90,6);std::memset(reinterpret_cast<void*>(0x4D8D7E),0x90,7);std::memset(reinterpret_cast<void*>(0x4D8E3D),0x90,7);
#endif
for(int i=0;i<4096;i++){double a=i*0.001536096911877394;G_RCOSSIN_TBL[i].sin=std::sin(a)*4096.0;G_RCOSSIN_TBL[i].cos=std::cos(a)*4096.0;}
#ifdef VALIDATE_ORIGINAL
std::memcpy(reinterpret_cast<void*>(0x610C48),G_RCOSSIN_TBL,sizeof(G_RCOSSIN_TBL));
#endif
int mode;while(std::cin>>mode){events.clear();environmentIndex=0;for(int i=0;i<6;i++){bodies[i]={};bodies[i].id=i;}G_MECHLIST_PLAYER=&bodies[1];G_BADDY_LIST=&bodies[0];G_ENVIRONMENTAL_OBJECT_LIST=&bodies[5];
if(mode==0){int player,baddy,ignore,los;std::cin>>player>>baddy>>ignore>>los>>environmentHits[0]>>environmentHits[1]>>bodies[0].field_2FC.vx>>bodies[0].field_2FC.vy>>bodies[0].field_2FC.vz;
for(int i=0;i<6;i++)std::cin>>bodies[i].mPos.vx>>bodies[i].mPos.vy>>bodies[i].mPos.vz>>bodies[i].mRMinor>>bodies[i].mCBodyFlags>>bodies[i].mHealth>>bodies[i].mType;
bodies[0].mNextItem=&bodies[3];bodies[1].mNextItem=&bodies[2];bodies[3].mNextItem=&bodies[4];G_LINE_TO_SPHERE_IGNORE_RADIUS=ignore;G_LINE_OF_SIGHT_CHECK=los;
auto result=bodies[0].StruckGameObject(player,baddy);auto expectedEvents=events;int expectedLos=G_LINE_OF_SIGHT_CHECK;
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0x310]={};std::memcpy(raw+8,&bodies[0].mPos,12);std::memcpy(raw+0x2FC,&bodies[0].field_2FC,12);*reinterpret_cast<void**>(0x6A9038)=G_MECHLIST_PLAYER;*reinterpret_cast<void**>(0x56E990)=G_BADDY_LIST;
events.clear();environmentIndex=0;G_LINE_OF_SIGHT_CHECK=los;auto actual=reinterpret_cast<CBody*(__attribute__((thiscall)) *)(void*,int,int)>(0x404AE0)(raw,player,baddy);assert(actual==result&&events==expectedEvents&&G_LINE_OF_SIGHT_CHECK==expectedLos);
#endif
std::cout<<'['<<(result?result->id:-1)<<','<<expectedLos<<',';events=expectedEvents;eventJson();std::cout<<"]\n";
}else if(mode==1){CVector velocity;int strength;std::cin>>bodies[0].mPos.vx>>bodies[0].mPos.vy>>bodies[0].mPos.vz>>bodies[1].mPos.vx>>bodies[1].mPos.vy>>bodies[1].mPos.vz>>bodies[1].mHealth>>velocity.vx>>velocity.vy>>velocity.vz>>strength;
bodies[0].ElasticCollision(&bodies[0],&bodies[1],velocity,strength);auto expectedEvents=events;
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char rawSource[0x100]={},rawTarget[0x100]={};void*vtable[4]={};vtable[3]=reinterpret_cast<void*>(&hitOriginal);*reinterpret_cast<void**>(rawTarget)=vtable;
std::memcpy(rawSource+8,&bodies[0].mPos,12);std::memcpy(rawTarget+8,&bodies[1].mPos,12);*reinterpret_cast<short*>(rawTarget+0xE2)=bodies[1].mHealth;
events.clear();reinterpret_cast<void(__attribute__((thiscall)) *)(void*,void*,void*,CVector&,int)>(0x404700)(rawSource,rawSource,rawTarget,velocity,strength);assert(events==expectedEvents);
#endif
events=expectedEvents;eventJson();std::cout<<'\n';
}else if(mode==2){int yaw;std::cin>>bodies[0].mPos.vx>>bodies[0].mPos.vy>>bodies[0].mPos.vz>>bodies[0].field_2A8>>yaw>>pathResult;bodies[0].mAngles.vy=yaw;bodies[0].CheckFallBack();auto expectedEvents=events;
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0x300]={};std::memcpy(raw+8,&bodies[0].mPos,12);*reinterpret_cast<int*>(raw+0x2A8)=bodies[0].field_2A8;*reinterpret_cast<short*>(raw+0x16)=yaw;
events.clear();reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4D8D70)(raw);assert(events==expectedEvents);
#endif
events=expectedEvents;eventJson();std::cout<<'\n';
}else{int yaw,frames,amount,flags;unsigned mask,completion,movement;std::cin>>yaw>>frames>>amount>>flags>>mask>>completion>>movement;int remaining=frames,step=amount/frames;completion&=~mask;
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char actor[0x300]={},proc[0x28]={};*reinterpret_cast<short*>(actor+0x16)=yaw;*reinterpret_cast<unsigned*>(actor+0x288)=completion;*reinterpret_cast<unsigned*>(actor+0x2AC)=movement;
*reinterpret_cast<void**>(proc+4)=actor;*reinterpret_cast<int*>(proc+0x20)=frames;*reinterpret_cast<int*>(proc+0x24)=step;*reinterpret_cast<int*>(proc+0x10)=flags;*reinterpret_cast<unsigned*>(proc+0x14)=mask;
#endif
std::cout<<'[';for(int tick=0;tick<12;tick++){yaw=short(yaw+step);movement|=step<0?0x10000:0x20000;remaining--;if(remaining<=0){if(mask){completion|=mask;mask&=0xF0F0;}flags|=1;}
#ifdef VALIDATE_ORIGINAL
reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x401110)(proc);assert(*reinterpret_cast<short*>(actor+0x16)==yaw&&*reinterpret_cast<unsigned*>(actor+0x288)==completion&&*reinterpret_cast<unsigned*>(actor+0x2AC)==movement);
assert(*reinterpret_cast<int*>(proc+0x20)==remaining&&*reinterpret_cast<unsigned*>(proc+0x14)==mask&&*reinterpret_cast<int*>(proc+0x10)==flags);
#endif
if(tick)std::cout<<',';std::cout<<'['<<yaw<<','<<movement<<','<<remaining<<','<<completion<<','<<mask<<','<<flags<<']';}std::cout<<"]\n";
}}}
'''
    rng=random.Random(0x404700);cases=[]
    for _ in range(256):
        start=[rng.randint(-3000,3000)*4096 for _ in range(3)];end=[x+rng.randint(-1000,1000)*4096 for x in start];case=[0,rng.randrange(2),rng.randrange(2),rng.randrange(2),rng.randrange(2),rng.randrange(2),rng.randrange(2),*start]
        for i in range(6):
            pos=end if i==0 else [x+rng.randint(-150,150)*4096 for x in start]
            case.extend([*pos,rng.choice([0,96,150,65535]),rng.choice([0,0x40]),rng.choice([-1,0,90]),rng.choice([0,304,312])])
        cases.append(case)
    for _ in range(256):cases.append([1,*[rng.randint(-2147483648,2147483647) for _ in range(6)],rng.choice([-1,0,1,90]),*[rng.randint(-10000000,10000000) for _ in range(3)],rng.randint(-100000,100000)])
    for _ in range(128):cases.append([2,*[rng.randint(-2147483648,2147483647) for _ in range(3)],rng.choice([0,0x10,-1]),rng.randint(-32768,32767),rng.randrange(4)])
    for _ in range(128):cases.append([3,rng.randint(-32768,32767),rng.randint(1,10),rng.randint(-2147483648,2147483647),rng.randrange(8),rng.randrange(2**32),rng.randrange(2**32),rng.randrange(2**32)])
    cases.extend([[1,0,0,0,0,0,0,90,0,0,0,5],[1,0,0,0,0,409600,0,90,0,0,0,-1],[3,4000,4,2047,0,0,0,0]])
    with tempfile.TemporaryDirectory(prefix='openspidey-recoil-reference-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        cmd=[str(binary)]
        if args.original_exe:cmd.append(str(args.original_exe.resolve()))
        lines=subprocess.check_output(cmd,input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/actor-recoil.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated{len(cases)} source recoil cases; original PC caller/numerical/procedure stages checked when requested.')

if __name__=='__main__':main()
