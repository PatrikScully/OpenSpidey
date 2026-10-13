"""Compile recovered thug message traversal and source message insertion/cleanup."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    thug = (root / "src/game/actors/thug.cpp").read_text()
    baddy = (root / "src/game/actors/baddy.cpp").read_text()
    message = (root / "src/game/ui/message.cpp").read_text()
    harness = r'''
#include <cstdint>
#include <iostream>
#include <vector>
using i32=int32_t;using i16=int16_t;using u16=uint16_t;using u8=uint8_t;
struct CVector{int vx=0,vy=0,vz=0;};
struct CItem{CVector mPos;};struct CThug;using CBaddy=CThug;
struct SHandle{void*pWhatever=nullptr;};
struct SMessageData{int mNumParams=0,mNumVects=0,mParams[2];int*pVects[2];};
struct CMessage{CMessage*pPrev=nullptr,*mNext=nullptr;CThug*pBaddy=nullptr;int field_10=0,field_14=0;SHandle mHandle;int mParams[2]={},mVects[2][3]={},field_40=0;
 CMessage(CBaddy*,CBaddy*,i32,SMessageData*);~CMessage();};
struct Angles{i16 vx=0,vy=0,vz=0;};struct Joint{struct Angles Angles;};
struct SStateFlags{i16 flags[2];};
SStateFlags table[17]={{{2,96}},{{23,64}},{{24,64}},{{1,96}},{{26,11}},{{27,11}},{{22,27}},{{18,1}},{{14,7}},{{15,7}},{{12,1}},{{17,7}},{{19,3}},{{20,3}},{{13,5}},{{25,96}},{{0,19}}};
SStateFlags*G_THUG_STATE_FLAGS=table;
struct CTrapWebEffect{int event=0;void Burst();};
std::vector<int> events;bool readinessConsumes=false;int procedures=0;
void print_if_false(bool,const char*){}
void*Mem_RecoverPointer(SHandle*handle){return handle->pWhatever;}
SHandle Mem_MakeHandle(CBaddy*actor){return {actor};}
struct CThug:CItem{
 CMessage*pMessage=nullptr;int mType=304,field_314=0,dumbAssPad=0,field_330=0,field_350=0,field_218=0,field_2F0=0,field_1F8=0;
 struct{int bothFlags=0;short flags[2]={};}field_31C;
 u16 mFlags=0;struct Angles mAngles;Joint joints[8];Joint*mpJoints=nullptr;
 SHandle mHandle,field_104,field_10C;int*field_3A0=nullptr;
 void ProcessMessages();void CleanUpMessages(i32,i32);u16 CheckStateFlags(SStateFlags*,int);
 void RunToWhereTheActionIs(CVector*p){events.insert(events.end(),{1,p->vx,p->vy,p->vz});}
 void ApplyPose(i16*){events.insert(events.end(),{2,mType});mpJoints=joints;}
 void GetReadyToShootHostage(CMessage*m){events.insert(events.end(),{4});if(readinessConsumes)m->field_10|=1;}
 void Neutralize(){events.push_back(11);}
};
CThug*G_THUG_LIST=nullptr;
struct CAIProc_StateSwitchSendMessage{CAIProc_StateSwitchSendMessage(CThug*,int){procedures++;}};
int SFX_PlayPos(unsigned sound,CVector*p,int pitch){events.insert(events.end(),{10,int(sound),p->vx,p->vy,p->vz,pitch});return 0;}
void CTrapWebEffect::Burst(){events.push_back(event);}
'''
    # Source CMessage insertion/destruction compiled verbatim. Instrument only
    # externally sent replies, retaining self-reply insertion into the real list.
    constructor = controller_method(message, "CMessage::CMessage(")
    constructor = constructor.replace("{\n\tfor", "{\n if(pOwner==a2 && a4==1)events.insert(events.end(),{3,a4});\n\tfor", 1)
    harness += constructor
    harness += controller_method(message, "CMessage::~CMessage(void)")
    harness += controller_method(baddy, "void CBaddy::CleanUpMessages(i32 a2, i32 a3)").replace("CBaddy::", "CThug::")
    harness += controller_method(baddy, "u16 CBaddy::CheckStateFlags(SStateFlags *sFlags, int a3)").replace("CBaddy::", "CThug::")
    process = controller_method(thug, "void CThug::ProcessMessages(void)")
    # Semantic boundaries instrumented without changing branches/call order.
    process = process.replace("new CMessage(this, reinterpret_cast<CBaddy*>(message->mHandle.pWhatever), 1, 0);", "{events.insert(events.end(),{3,1});new CMessage(this, reinterpret_cast<CBaddy*>(message->mHandle.pWhatever), 1, 0);}")
    process = process.replace("new CMessage(this, reinterpret_cast<CBaddy*>(this->mHandle.pWhatever), 1, 0);", "{events.insert(events.end(),{5,1});new CMessage(this, reinterpret_cast<CBaddy*>(this->mHandle.pWhatever), 1, 0);}")
    process = process.replace("delete this->field_3A0;", "events.push_back(6);delete this->field_3A0;")
    process = process.replace("G_THUG_LIST = 0;", "{events.push_back(7);G_THUG_LIST = 0;}")
    harness += process
    harness += r'''
int main(){
 long long v[18];int messageCount;
 while(std::cin>>v[0]){
  for(int i=1;i<18;i++)std::cin>>v[i];std::cin>>messageCount;
  CThug actor,sender;actor.mType=v[0];actor.field_31C.bothFlags=v[1];actor.dumbAssPad=v[2];actor.mFlags=v[3];actor.field_218=v[4];actor.field_2F0=v[5];actor.field_350=v[6];actor.mAngles.vx=v[7];actor.mPos={123456,int(v[8]),-123456};actor.field_330=v[9];actor.field_1F8=v[10];
  actor.joints[1].Angles.vz=v[11];actor.joints[3].Angles.vz=v[12];actor.joints[6+(actor.mType==304)].Angles.vz=v[13];if(v[14])actor.mpJoints=actor.joints;
  readinessConsumes=v[15];G_THUG_LIST=v[16]?&actor:nullptr;procedures=0;
  CTrapWebEffect trap{8},tug{9};actor.field_104={&trap};actor.field_10C={&tug};actor.mHandle={&sender};sender.mPos={-10,20,-30};
  for(int i=0;i<messageCount;i++){
   int code,flags,age,handle;std::cin>>code>>flags>>age>>handle;
   CMessage*m=new CMessage(handle==2?&actor:handle==1?&sender:nullptr,&actor,code,nullptr);m->field_10=flags;m->field_40=age;m->mVects[0][0]=-40;m->mVects[0][1]=50;m->mVects[0][2]=-60;
  }
  events.clear();for(int i=0;i<v[17];i++)actor.ProcessMessages();
  std::cout<<"{\"state\":["<<actor.field_31C.bothFlags<<","<<actor.dumbAssPad<<","<<actor.mFlags<<","<<actor.field_218<<","<<actor.field_350<<","<<actor.mAngles.vx<<","<<actor.mPos.vy<<","<<actor.field_330<<","<<actor.field_1F8<<","<<actor.joints[1].Angles.vz<<","<<actor.joints[3].Angles.vz<<","<<actor.joints[6+(actor.mType==304)].Angles.vz<<","<<(actor.mpJoints!=nullptr)<<","<<(G_THUG_LIST==&actor)<<","<<procedures<<"],\"events\":[";
  for(size_t i=0;i<events.size();i++){if(i)std::cout<<",";std::cout<<events[i];}
  std::cout<<"],\"messages\":[";bool first=true;for(CMessage*m=actor.pMessage;m;m=m->mNext){if(!first)std::cout<<",";first=false;std::cout<<m->field_14<<","<<m->field_10<<","<<m->field_40;}
  std::cout<<"]}\n";actor.CleanUpMessages(1,0);sender.CleanUpMessages(1,0);
 }
}
'''
    # Avoid double-counting self-reply instrumentation; constructor stays original.
    harness = harness.replace(' if(pOwner==a2 && a4==1)events.insert(events.end(),{3,a4});\n', '')
    rng=random.Random(0x4D60)
    codes=[0,1,5,6,7,10,12,13,14,15,16,17,19,20,99]
    cases=[]
    for actor_type in [304,312]:
        for state in [1,2,8,9,14,18,19,26,28]:
            for code in codes:
                for boundary in range(3):
                    inputs=[actor_type,state,rng.choice([0,1,2,3,5]),rng.choice([0,4,0xffff]),rng.choice([0,0x20,0x40,0x60]),rng.choice([0,0x10]),rng.choice([-8,0,7]),rng.choice([-123,0,123]),rng.choice([-2147480000,0,2147480000]),77,25,rng.choice([-32768,-1,0,1,2,3,100,32767]),rng.choice([-32768,0,32767]),rng.choice([-32768,0,32767]),boundary%2,boundary%2,boundary%2,boundary+1]
                    messages=[[code,rng.choice([0,2]),rng.choice([0,2,3,7,8,9,2147483647]),boundary]]
                    cases.append([inputs,messages])
    # Ordered batches exercise state changes gating later messages and newly
    # enqueued self-replies that are deferred past the active traversal.
    for state in [1,8,14,18,19,26]:
        for batch in [[5,6,5],[10,5,10],[13,13],[20,12,20],[13,6,19],[15,14,15],[0,20,0],[16,13]]:
            cases.append([[304,state,0,4,0,0,3,123,0,0,9,32,800,-800,1,0,1,1],[[code,0,0,2] for code in batch]])
    requests="\n".join(" ".join(map(str,inputs+[len(messages)]+[value for row in messages for value in row])) for inputs,messages in cases)
    with tempfile.TemporaryDirectory(prefix="thug-message-reference-") as temporary:
        directory=Path(temporary);(directory/"reference.cpp").write_text(harness)
        subprocess.run(["g++","-std=c++17","-O0","-fwrapv",str(directory/"reference.cpp"),"-o",str(directory/"reference")],check=True)
        result=subprocess.run([str(directory/"reference")],input=requests,capture_output=True,text=True,check=True)
    rows=[{"input":inputs,"inputMessages":messages,**json.loads(output)} for (inputs,messages),output in zip(cases,result.stdout.splitlines(),strict=True)]
    destination=root/"modern/OpenSpidey.Tests/Fixtures/thug-messages.json"
    destination.write_text(json.dumps(rows,separators=(",",":"))+"\n")
    print(f"Compiled original message traversal/list methods: {len(rows)} comparisons written to {destination}")


if __name__=="__main__":main()
