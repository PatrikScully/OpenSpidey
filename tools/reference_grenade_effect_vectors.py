"""Compile original glow flash and expgrnd model/carrier effects with source RNG."""
import json
from pathlib import Path
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    exp=(root/"src/game/effects/exp.cpp").read_text();bit=(root/"src/engine/world/bit.cpp").read_text();utils=(root/"src/core/utils.cpp").read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <cstdlib>
void print_if_false(bool,const char*){}void DoAssert(bool,const char*){}
int G_RND_RELATED_ONE=1,G_RND_RELATED_TWO=2,G_RND_RELATED_THREE=3;
std::vector<int> randomEvents;std::vector<long long> events;
'''
    code+=controller_method(utils,"int Rnd(i32 n)").replace("int Rnd(","int OriginalRnd(",1)
    code+=r'''
int Rnd(int bound){int value=OriginalRnd(bound);randomEvents.insert(randomEvents.end(),{bound,value});return value;}
struct CBit{short mAge=0;u16 mLifetime=0;CVector mPos;bool mDead=false;void Die(){mDead=true;}void AttachTo(void*){}};
struct SSection{u32 Radius=0,PadBGR=0;};struct SFringeQuad{u32 Width=0,CodeBGR=0;};
CBit*G_GLOW_LIST=nullptr;
void*DCMem_New(size_t bytes,int,int,int,int){return calloc(1,bytes);}
struct CGlow:CBit{
 SSection*mpSections=nullptr;SFringeQuad*mpFringes=nullptr;u32 mNumSections=0,mNumFringes=0,mCentreCodeBGR=0,mMask=0;short mStepAngle=0;
 CGlow(u32,u32);~CGlow(){free(mpSections);}void SetCentreRGB(u8,u8,u8);void SetRGB(u8,u8,u8);void SetFringeRGB(u32,u8,u8,u8);void SetFringeWidth(u32,u32);
};
struct CGlowFlash:CGlow{
 i16 field_5C=0,field_5E=0,field_60=0,field_62=0,field_64=0,field_66=0,field_68=0,field_6A=0;u8 field_6C=0;
 CGlowFlash(CVector*,i32,u8,u8,u8,i32,u8,u8,u8,i32,i32,i32,i32,i32,i32,i32,i32,i32,i32);void ChooseRadii();void Move();
};
void*G_BULLET_LIST=nullptr;
struct C3DExplosion{
 CVector mPos,mVel;CSVector mScale;u16 mModel=0,mFlags=0;u32 mRGB=0;u8 field_F8=0;int field_FC=0,field_100=0,field_104=0,field_108=0,field_10C=0,field_110=0;bool dead=false;
 C3DExplosion(const CVector*,char*,i32,i32,i32,i32,i32,i32,i32,i32,i32);void AI();
 void AttachTo(void*){}void InitItem(char*){}void Die(){dead=true;}
};
struct SHandle{void*pWhatever=nullptr;};int G_3D_EXPLOSIONS=0;bool handleAlive=true;std::vector<C3DExplosion*> created;
SHandle Mem_MakeHandle(C3DExplosion*model){created.push_back(model);return {model};}
void*Mem_RecoverPointer(SHandle*handle){events.insert(events.end(),{2,static_cast<C3DExplosion*>(handle->pWhatever)->mModel,handleAlive});return handleAlive?handle->pWhatever:nullptr;}
struct CGrenadeExplosion:CBit{SHandle hExp;CGrenadeExplosion(const CVector*);~CGrenadeExplosion();void Move();void Die(){mDead=true;events.push_back(4);}};
'''
    for signature in ["CGlow::CGlow(u32 NumPoints", "void CGlow::SetCentreRGB(", "void CGlow::SetRGB(", "void CGlow::SetFringeRGB(", "void CGlow::SetFringeWidth(", "void Bit_ReduceRGB("]:
        code+=controller_method(bit,signature)
    for signature in ["CGlowFlash::CGlowFlash(","void CGlowFlash::ChooseRadii(","void CGlowFlash::Move("]:
        code+=controller_method(exp,signature)
    model=controller_method(exp,"C3DExplosion::C3DExplosion(")
    opening=model.index("{",model.index(":") if ":" in model[model.index(")"):] else 0)
    # Record model requests at the constructor boundary; source body remains unchanged.
    model=model[:opening+1]+"\n events.insert(events.end(),{1,a2->vx,a2->vy,a2->vz,a4,a5,a6,a7,a8,a9,a10,a11,a12});\n"+model[opening+1:]
    code+=model+controller_method(exp,"void C3DExplosion::AI(")
    carrier=controller_method(exp,"CGrenadeExplosion::CGrenadeExplosion(")
    carrier=carrier.replace('"expgrnd"','const_cast<char*>("expgrnd")').replace("++G_3D_EXPLOSIONS;","++G_3D_EXPLOSIONS;events.insert(events.end(),{3,1});")
    destructor=controller_method(exp,"CGrenadeExplosion::~CGrenadeExplosion(").replace("--G_3D_EXPLOSIONS;","--G_3D_EXPLOSIONS;events.insert(events.end(),{3,-1});")
    code+=carrier+destructor+controller_method(exp,"void CGrenadeExplosion::Move(")
    code+=r'''
void printArrays(){std::cout<<",\"random\":[";for(size_t i=0;i<randomEvents.size();i++){if(i)std::cout<<",";std::cout<<randomEvents[i];}std::cout<<"],\"events\":[";for(size_t i=0;i<events.size();i++){if(i)std::cout<<",";std::cout<<events[i];}std::cout<<"]}\n";}
int main(){int op;while(std::cin>>op){randomEvents.clear();events.clear();
 if(op==0){long long a[26];for(int i=0;i<26;i++)std::cin>>a[i];G_RND_RELATED_ONE=a[23];G_RND_RELATED_TWO=a[24];G_RND_RELATED_THREE=a[25];CVector position{int(a[18]),int(a[19]),int(a[20])};
 CGlowFlash glow(&position,a[0],a[1],a[2],a[3],a[4],a[5],a[6],a[7],a[8],a[9],a[10],a[11],a[12],a[13],a[14],a[15],a[16],a[17]);glow.mAge=a[22];for(int tick=0;tick<a[21];tick++)glow.Move();
 std::cout<<"{\"state\":["<<glow.mCentreCodeBGR<<","<<glow.mAge<<","<<glow.mLifetime<<","<<glow.field_5C<<","<<glow.field_5E<<","<<glow.field_60<<","<<glow.field_62<<","<<glow.field_64<<","<<glow.field_66<<","<<glow.field_68<<","<<glow.field_6A<<","<<int(glow.field_6C)<<","<<glow.mNumSections<<","<<glow.mStepAngle<<","<<glow.mDead<<"],\"sections\":[";
 for(u32 i=0;i<glow.mNumSections;i++){if(i)std::cout<<",";std::cout<<glow.mpSections[i].Radius<<","<<glow.mpSections[i].PadBGR<<","<<glow.mpFringes[i].Width<<","<<glow.mpFringes[i].CodeBGR;}std::cout<<"]";printArrays();
 }else if(op==1){long long a[19];for(int i=0;i<19;i++)std::cin>>a[i];G_RND_RELATED_ONE=a[16];G_RND_RELATED_TWO=a[17];G_RND_RELATED_THREE=a[18];CVector position{int(a[9]),int(a[10]),int(a[11])};
 C3DExplosion model(&position,const_cast<char*>("expgrnd"),a[0],a[1],a[2],a[3],a[4],a[5],a[6],a[7],a[8]);if(a[13]>=0)model.field_F8=a[13];model.field_100=a[14];model.mRGB=a[15];for(int tick=0;tick<a[12];tick++)model.AI();
 std::cout<<"{\"state\":["<<model.mPos.vx<<","<<model.mPos.vy<<","<<model.mPos.vz<<","<<model.mScale.vx<<","<<model.mScale.vy<<","<<model.mScale.vz<<","<<model.mVel.vy<<","<<model.mRGB<<","<<model.mFlags<<","<<int(model.field_F8)<<","<<model.field_FC<<","<<model.field_100<<","<<model.dead<<","<<model.mModel<<"]";events.clear();printArrays();
 }else{long long a[11];for(int i=0;i<11;i++)std::cin>>a[i];G_RND_RELATED_ONE=a[8];G_RND_RELATED_TWO=a[9];G_RND_RELATED_THREE=a[10];CVector position{int(a[0]),int(a[1]),int(a[2])};handleAlive=a[3];G_3D_EXPLOSIONS=a[5];created.clear();
 CGrenadeExplosion*carrier=new CGrenadeExplosion(&position);for(int tick=0;tick<a[4];tick++)carrier->Move();bool dead=carrier->mDead;int before=G_3D_EXPLOSIONS;if(a[6])delete carrier;
 std::cout<<"{\"state\":["<<before<<","<<G_3D_EXPLOSIONS<<","<<dead<<"]";printArrays();if(!a[6])delete carrier;for(C3DExplosion*p:created)delete p;
 }
}}
'''
    glow_cases=[];model_cases=[];carrier_cases=[]
    grenade=[5,255,255,255,1,128,100,0,1,60,0,1,100,120,80,100,10,4]
    henchman=[5,255,128,0,32,0,0,0,0,50,20,1,20,10,40,20,10,1]
    for arguments in [grenade,henchman,[3,90,120,255,2,127,128,255,3,65535,0,0,1,0,2,0,0,0]]:
        for moves in [0,1,2,4,5,10,20]:
            for age in [0,32767,-32768]:glow_cases.append(arguments+[123456,-234567,345678,moves,age,1,2,3])
    for fade in [-32768,-1,0,1,127,255,32767,65537]:
        for decay in [-32768,-1,0,1,10,32767]:
            arguments=grenade.copy();arguments[4]=fade;arguments[8]=fade;arguments[16]=decay
            glow_cases.append(arguments+[0,0,0,3,0,-123456,987654,-77777])
    for lifetime in [0,1,4,65535,65536]:
        for reselect in [0,1,256]:
            arguments=grenade.copy();arguments[17]=lifetime;arguments[11]=reselect
            glow_cases.append(arguments+[0,0,0,5,32765,42,84,126])
    for points in [1,2,16]:
        arguments=grenade.copy();arguments[0]=points
        glow_cases.append(arguments+[123456,-234567,345678,2,0,42,84,126])
    for model in [0,1,65535,65536]:
        for delay in [0,1,2,-1]:
            for moves in [0,1,2,10,11,19,20]:model_cases.append([model,delay,300,789,256,0,13,0,0,123456,-234567,345678,moves,-1,0,0x808080,1,2,3])
    for scale in [-32769,-32768,32767,32768,65535]:
        for step in [-2147483648,-500,0,500,2147483647]:
            for stage in [0,1,2]:model_cases.append([0,0,scale,step,32767,2,7,-100,0,0,2147480000,0,3,stage,2147483647,0x12345678,42,84,126])
    for fade in [-1,0,1,255,2147483647]:model_cases.append([1,0,300,500,256,0,fade,20,30,0,0,0,4,-1,0,0x808080,42,84,126])
    for alive in [0,1]:
        for moves in [0,1,2]:
            for dispose in [0,1]:carrier_cases.append([123456,-234567,345678,alive,moves,12,dispose,0,1,2,3])
    groups=[("glow-flash",glow_cases),("explosion-model",model_cases),("grenade-explosion",carrier_cases)]
    requests=[str(op)+" "+" ".join(map(str,row)) for op,(_,cases) in enumerate(groups) for row in cases]
    with tempfile.TemporaryDirectory(prefix="grenade-effect-reference-") as temporary:
        directory=Path(temporary);(directory/"reference.cpp").write_text(code)
        subprocess.run(["g++","-std=c++17","-O0","-fwrapv","-I",str(root/"src"),str(directory/"reference.cpp"),"-o",str(directory/"reference")],check=True)
        result=subprocess.run([str(directory/"reference")],input="\n".join(requests),capture_output=True,text=True,check=True)
    outputs=iter(result.stdout.splitlines())
    for name,cases in groups:
        rows=[{"input":inputs,**json.loads(next(outputs))} for inputs in cases]
        destination=root/f"modern/OpenSpidey.Tests/Fixtures/{name}.json"
        destination.write_text(json.dumps(rows,separators=(",",":"))+"\n")
        print(f"Compiled recovered {name}: {len(rows)} comparisons written to {destination}")
    assert next(outputs,None) is None


if __name__=="__main__":main()
