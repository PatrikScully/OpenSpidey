"""Compile recovered player Hit, death entry and knockdown recovery with explicit instrumented boundaries."""
import json
import copy
from pathlib import Path
import random
import re
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


FIELDS = {
    "Health": "p.mHealth", "MaxHealth": "p.mMaxHealth", "ActionState": "p.mActionState", "AnimationId": "p.mAnim",
    "AnimationFrame": "p.mFrame", "ElapsedTicks": "p.mElapsedTicks", "CollisionFlags": "p.mCollision",
    "Position": "p.mPos", "Velocity": "p.mVel", "ForwardDirection": "p.mForwardDirection", "UpDirection": "p.mUpDirection",
    "CrawlSurfaceNormal": "p.field_A8", "IsCrawling": "p.mIsCrawling", "SynthesizedInputActive": "p.mSynthesizedInputActive",
    "FieldE18": "p.field_E18", "Field36C": "p.field_36C", "Field8DC": "p.field_8DC", "Field8E8": "p.field_8E8",
    "Field8E9": "p.field_8E9", "LookaroundActive": "p.field_8EA", "Field54C": "p.field_54C", "FlailDropChain": "p.flailDrop",
    "FieldAE5": "p.field_AE5", "FieldEA6": "p.field_EA6", "AirborneTicks": "p.airborneTicks",
    "HeadingTurnTicksRemaining": "p.mHeadingTurnTicksRemaining", "LoopingSoundHandle": "p.field_5E4",
    "FirstSmokeTrailActive": "bool(p.field_584)", "SecondSmokeTrailActive": "bool(p.field_588)",
    "HeldObjectPresent": "bool(p.mHeldObject)", "WebPresent": "bool(p.field_E6C)", "DeathEffectPresent": "bool(p.field_E64)",
    "ArmorActive": "p.field_5E9", "ArmorAmount": "p.field_5EC", "ArmorTexturesActive": "G_SPIDEY_ARMOR_SET",
    "ArmorAnimationVariant": "G_SPIDEY_ANIM_TWO", "SavedArmorAmount": "*reinterpret_cast<i32*>(saveBytes+0x50)",
    "SavedArmorActive": "saveBytes[0x79]", "LastHitClock": "p.field_500", "LastHitAction": "p.field_504",
    "PainSoundClock": "p.field_EEC", "Field528": "p.field_528", "Field52C": "p.field_52C", "Field534": "p.field_534",
    "Electrified": "p.field_508", "ElectrificationTicks": "p.field_50C", "Field5AC": "p.field_5AC",
    "Field5B0": "p.field_5B0", "Field5B4": "p.field_5B4",
}
VECTORS = {"Position", "Velocity", "ForwardDirection", "UpDirection", "CrawlSurfaceNormal"}
BOOLS = {"IsCrawling", "SynthesizedInputActive", "LookaroundActive", "FirstSmokeTrailActive", "SecondSmokeTrailActive",
         "HeldObjectPresent", "WebPresent", "DeathEffectPresent", "ArmorActive", "ArmorTexturesActive"}


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root/"src/game/player/spidey.cpp").read_text()
    locomotion = (root/"src/game/player/player_locomotion_actions.cpp").read_text()
    maths = (root/"src/engine/rendering/ps2funcs.cpp").read_text()
    code = r'''
#include <cmath>
#include <cstdint>
#include <iostream>
#include <type_traits>
#include <vector>
using i32=int32_t;using i16=int16_t;using u32=uint32_t;using u16=uint16_t;using u8=uint8_t;
std::vector<int64_t>events;void event(std::initializer_list<int64_t>v){events.insert(events.end(),v.begin(),v.end());}
struct CVector{int vx=0,vy=0,vz=0;};using VECTOR=CVector;
struct CSVector{i16 vx=0,vy=0,vz=0;};
struct SHitInfo{u8 field_0=0,field_1=0;int field_4=0;u16 field_8=0;CVector field_C;i16 field_18=0;u16 field_1A=0;};
struct FadeFlag{int slot=0;void operator=(int){event({3,slot});}};
struct Smoke{FadeFlag mFadeAway;};struct CManipOb{void Smash(){event({4});}};
struct CWeb{void SwitchToBlob(){event({9});}};
struct SVTableSlot0Deletable{virtual~SVTableSlot0Deletable(){event({16});}};
int G_CURRENTSUIT=0,symbiote=0,G_TIMER_RELATED=0,randomValue=0;CVector zero;
bool los=true;u8 G_SPIDEY_ARMOR_SET=0;int G_SPIDEY_ANIM_TWO=0,gLevelStatus=0;u8 saveBytes[0x100]={},cameraBytes[0x200]={};
void*G_CAMERA_LIST=cameraBytes;int soundData[330][2];i32*G_SPIDEY_SFX_ENTRY[330];
int Rnd(int bound){event({20,bound,randomValue});return randomValue;}
void SFX_Stop(int handle){event({1,handle});}
void SFX_Play(int sound,int volume,int pitch){event({21,sound,volume,pitch});}
void SFX_PlayPos(int sound,CVector*position,int volume){event({22,sound,position->vx,position->vy,position->vz,volume});}
void Spidey_DoArmorVRAMProcessing(bool enabled){event({5,enabled});}
bool Utils_LineOfSight(CVector*from,CVector*to,int flags,int argument){event({13,from->vx,from->vy,from->vz,to->vx,to->vy,to->vz,flags,argument,los});return los;}
int Trig_GetLevelID(){return 0;}void Reloc_CallUserFunction(char*,int,int,int){}
void SetCameraField12C(int value){event({17,value});*reinterpret_cast<int*>(cameraBytes+0x12c)=value;}
int*SelectDeathAnimationSounds(int clip){event({14,clip});return G_SPIDEY_SFX_ENTRY[clip];}
struct CPlayer{
 i16 mHealth=100;int mMaxHealth=100;u32 mActionState=1;u16 mAnim=0;int mFrame=0,mElapsedTicks=2;u16 mCollision=0;
 CVector mPos,mVel,mForwardDirection,mUpDirection;CSVector field_A8;
 bool mIsCrawling=false,mSynthesizedInputActive=false;int field_E18=0,field_36C=0,field_8DC=0;
 u8 field_8E8=0,field_8E9=0,field_8EA=0,field_54C=0,flailDrop=0,field_AE5=0; i16 field_EA6=0;u16 airborneTicks=0;
 int mHeadingTurnTicksRemaining=0,field_5E4=0;Smoke*field_584=nullptr,*field_588=nullptr;CManipOb*mHeldObject=nullptr;
 int*field_E6C=nullptr,*field_E64=nullptr;bool field_5E9=false;int field_5EC=0;
 u32 field_500=0,field_504=0,field_EEC=0;int field_528=0,field_52C=0,field_534=0;u8 field_508=0;
 int field_50C=0,field_5AC=0,field_5B0=0;u32 field_5B4=0;i32*field_350=nullptr;
 i32 Hit(SHitInfo*);void SwitchToDeathMode(bool);u8 KnockSpideyFromCrawlPosition();
 void PutCameraBehind(int arg){event({2,arg});}void StopMyXA(){event({7});}
 void ExitLookaroundMode(){event({8});field_8EA=0;}
 void PlaySingleAnim(int clip,int from,int to){event({10,clip,from,to});mAnim=clip;mFrame=from;}
 void RunAnim(int clip,int from,int to){event({15,clip,from,to});mAnim=clip;mFrame=from;}
 void OrientToNormal(bool use,CVector*target){event({11,use,target->vx,target->vy,target->vz});}
 void SwitchToStandMode(){event({18});mActionState=1;mAnim=0;mFrame=0;}
};
void Effects_Electrify(CPlayer*){event({12});}
enum class PlayerAction{Falling=4};int actionWord(PlayerAction action){return int(action);}
#define PLR_U8(p,offset) ((p)->flailDrop)
#define PLR_U16(p,offset) ((p)->airborneTicks)
struct PlayerBehaviorController{CPlayer&player_;u16 completedAnimation_;void updateKnockdownRecovery();};
'''
    code += controller_method(maths, "void VectorNormal(") + "\n"
    code += controller_method(source, "u8 CPlayer::KnockSpideyFromCrawlPosition(") + "\n"
    death = controller_method(source, "void CPlayer::SwitchToDeathMode(")
    death = re.sub(r"G_SPIDEY_SFX_ENTRY\[(0x[0-9A-Fa-f]+)\]", r"SelectDeathAnimationSounds(\1)", death)
    death = death.replace("*(i32*)((u8*)G_CAMERA_LIST + 0x12C) = -1;", "SetCameraField12C(-1);")
    # CBody::mPos really is at +8. The instrumented stub uses named fields instead of
    # the original packed ABI, so bind this raw-offset sound argument to that same field.
    death = death.replace("(CVector*)((u8*)this + 8)", "&this->mPos")
    code += death + "\n"
    hit = controller_method(source, "i32 CPlayer::Hit(")
    hit = hit.replace("(i32*)0x0060CFC8", "&symbiote").replace("(CVector*)0x0060D9D0", "&zero").replace("(u8*)0x00682858", "saveBytes")
    hit = hit.replace("*reinterpret_cast<i32*>(gSaveGameBytes + 0x50) = 0;", "*reinterpret_cast<i32*>(gSaveGameBytes + 0x50) = 0; event({6,0,0});")
    code += hit + "\n"
    recovery = controller_method(locomotion, "void PlayerBehaviorController::updateKnockdownRecovery(")
    recovery = recovery.replace("gLevelStatus = 8;", "gLevelStatus = 8; event({19,8});")
    code += recovery + "\n"
    code += "int main(){for(int i=0;i<330;i++){soundData[i][0]=0x12345678;soundData[i][1]=-1;G_SPIDEY_SFX_ENTRY[i]=soundData[i];}int mode;while(std::cin>>mode){CPlayer p;Smoke smoke0{{0}},smoke1{{1}};CManipOb held;CWeb web;int64_t value;\n"
    pointer_values = {"FirstSmokeTrailActive": "p.field_584=value?&smoke0:nullptr;", "SecondSmokeTrailActive": "p.field_588=value?&smoke1:nullptr;",
                      "HeldObjectPresent": "p.mHeldObject=value?&held:nullptr;", "WebPresent": "p.field_E6C=value?reinterpret_cast<int*>(&web):nullptr;",
                      "DeathEffectPresent": "p.field_E64=value?reinterpret_cast<int*>(new SVTableSlot0Deletable):nullptr;"}
    for name, field in FIELDS.items():
        if name in VECTORS:
            for axis in ["vx", "vy", "vz"]:code += f"std::cin>>value;{field}.{axis}=value;\n"
        elif name in pointer_values:code += "std::cin>>value;" + pointer_values[name] + "\n"
        else:code += f"std::cin>>value;{field}=static_cast<std::remove_reference_t<decltype({field})>>(value);\n"
    code += r'''
 unsigned timer,flags,part,damage,slideFrames;int kind,slideDistance,camera,losValue,completed;
 SHitInfo info;
 std::cin>>G_CURRENTSUIT>>symbiote>>timer>>camera>>losValue>>randomValue>>completed>>flags>>part>>kind>>damage
 >>info.field_C.vx>>info.field_C.vy>>info.field_C.vz>>slideDistance>>slideFrames;
 G_TIMER_RELATED=int(timer);cameraBytes[0x180]=camera;los=losValue;
 info.field_0=flags;info.field_1=part;info.field_4=kind;info.field_8=damage;info.field_18=slideDistance;info.field_1A=slideFrames;
 events.clear();int result=1;
 if(mode==0)result=p.Hit(&info);else if(mode==1)p.SwitchToDeathMode(false);
 else PlayerBehaviorController{p,u16(completed)}.updateKnockdownRecovery();
 std::cout<<"{\"result\":"<<result<<",\"state\":[";
'''
    expressions = []
    for name, field in FIELDS.items():
        if name in VECTORS:expressions += [f"int64_t({field}.{axis})" for axis in ["vx", "vy", "vz"]]
        else:expressions.append(f"int64_t({field})")
    code += 'std::cout<<' + '<<\',\'<<'.join(expressions) + ';\n'
    code += r'''
 std::cout<<"],\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]}\n";
 if(p.field_E64)delete reinterpret_cast<SVTableSlot0Deletable*>(p.field_E64);
 }}
'''
    randomizer = random.Random(0x4BD890)
    cases = []
    actions = [1, 2, 4, 8, 16, 64, 128, 2048, 0x800000, 0x1000000, 0x10000000, 0x20000000, 0x10043606]
    def vector(values):return dict(zip(["X", "Y", "Z"], values))
    for index in range(650):
        mode = 0 if index < 350 else 1 if index < 450 else 2
        initial = {name:False if name in BOOLS else vector([0, 0, 0]) if name in VECTORS else 0 for name in FIELDS}
        initial.update(Health=randomizer.choice([0, 1, 10, 100, 32767, -32768]), MaxHealth=100,
                       ActionState=randomizer.choice(actions) if index % 4 else 1,
                       AnimationId=randomizer.choice([0, 170, 172, 175, 176, 177, 178, 180, 181, 285]),
                       AnimationFrame=randomizer.choice([0, 6, 7, 30]), ElapsedTicks=randomizer.choice([-1, 0, 1, 2, 5, 2147483647]),
                       CollisionFlags=randomizer.choice([0, 2, 6]), Position=vector([randomizer.randint(-2000000, 2000000) for _ in range(3)]),
                       Velocity=vector([123, -456, 789]), ForwardDirection=vector([randomizer.choice([-4096, 0, 4096]), 123, randomizer.choice([-4096, 0, 4096])]),
                       UpDirection=vector([0, -4096, 0]), CrawlSurfaceNormal=vector([0, 4096, 0]),
                       IsCrawling=randomizer.choice([False, True]), Field8E8=randomizer.randrange(2), Field8E9=randomizer.randrange(2),
                       LookaroundActive=randomizer.choice([False, True]), Field54C=randomizer.randrange(2), FlailDropChain=randomizer.randrange(2),
                       FieldAE5=1, FieldEA6=123, AirborneTicks=randomizer.choice([0, 480, 481]), HeadingTurnTicksRemaining=30,
                       LoopingSoundHandle=randomizer.choice([0, 123]), FirstSmokeTrailActive=True, SecondSmokeTrailActive=True,
                       HeldObjectPresent=randomizer.choice([False, True]), WebPresent=randomizer.choice([False, True]), DeathEffectPresent=randomizer.choice([False, True]),
                       ArmorActive=randomizer.choice([False, True]), ArmorAmount=randomizer.choice([-100, 0, 5, 100, 600, -2147483648, 2147483647]),
                       ArmorTexturesActive=randomizer.choice([False, True]), ArmorAnimationVariant=1, SavedArmorAmount=123, SavedArmorActive=1,
                       PainSoundClock=randomizer.choice([0, 31, 100, 4294967290]), Field528=randomizer.choice([0, 10, 2147483647]),
                       Field52C=1, Field534=1, Field5AC=1, Field5B0=1, Field5B4=randomizer.choice([0, 31, 100, 4294967290]))
        if mode == 0 and index % 15 == 0:initial["FieldE18"]=1
        if mode == 0 and index % 17 == 0:initial["Field36C"]=1
        if mode == 0 and index % 19 == 0:initial["SynthesizedInputActive"]=True
        initial["Field8DC"] = randomizer.randrange(2)
        context = dict(CurrentSuit=4 if index % 23 == 0 else 0, SymbioteRelated=1 if index % 29 == 0 else 0,
                       Timer=randomizer.choice([0, 30, 31, 61, 100, 1000, 4294967295]), CameraFlag180=randomizer.choice([False, True]),
                       LineOfSight=randomizer.choice([False, True]), RandomValue=randomizer.randrange(3))
        hit_info = dict(Flags=randomizer.choice([0, 2, 4, 6, 10, 15]), PartIndex=randomizer.randrange(18),
                        AttackKind=randomizer.choice([0, 8, 9, 10, 11, 12, 14, 15, 16, 17, 26]),
                        Damage=randomizer.choice([0, 1, 10, 100, 600, 65535]),
                        Direction=vector([randomizer.choice([-4096, -1, 0, 1, 4096, 2147483647]) for _ in range(3)]),
                        SlideDistance=0, SlideFrames=0)
        completed = randomizer.choice([0xFFFF, 0xAF, 0xB1, 0xAA, 0xBB, 0xB5, 0xAE, 0xB4, 0x11D])
        cases.append(dict(mode=mode, initial=initial, context=context, hit=hit_info, completed=completed))
    baseline = copy.deepcopy(cases[0])
    baseline["mode"] = 0
    baseline["initial"].update(Health=100, ActionState=1, AnimationId=0, AnimationFrame=0, ElapsedTicks=2,
                               FieldE18=0, Field36C=0, Field8DC=0, SynthesizedInputActive=False,
                               IsCrawling=False, Field8E8=0, Field8E9=0, LookaroundActive=False,
                               Field54C=0, FlailDropChain=0, ArmorActive=False, ArmorAmount=0,
                               ArmorTexturesActive=False, PainSoundClock=0, LoopingSoundHandle=0,
                               FirstSmokeTrailActive=False, SecondSmokeTrailActive=False,
                               HeldObjectPresent=False, WebPresent=False, DeathEffectPresent=False,
                               Velocity=vector([0, 0, 0]), ForwardDirection=vector([0, 0, 4096]))
    baseline["context"].update(CurrentSuit=0, SymbioteRelated=0, Timer=31, CameraFlag180=False, LineOfSight=True, RandomValue=0)
    baseline["hit"].update(Flags=15, AttackKind=8, Damage=10, Direction=vector([4096, 0, 0]))
    cases.append(baseline)
    for state_changes, context_changes, hit_changes in [
        ({}, {"LineOfSight":False}, {}), ({}, {"Timer":30}, {}),
        ({"ArmorActive":True,"ArmorAmount":100,"ArmorTexturesActive":True}, {}, {}),
        ({"ArmorActive":True,"ArmorAmount":5,"ArmorTexturesActive":True}, {}, {}),
        ({"ArmorActive":True,"ArmorAmount":5,"ArmorTexturesActive":False}, {}, {}),
        ({"Health":5}, {}, {}), ({"Health":5}, {}, {"AttackKind":16}),
        ({"Field36C":30}, {}, {}), ({"ActionState":2}, {}, {}),
        ({"ActionState":0x10000000,"LoopingSoundHandle":123}, {"CameraFlag180":True}, {}),
        ({"IsCrawling":True,"Field8E8":1}, {}, {"AttackKind":9}),
        ({"IsCrawling":True,"Field8E9":1}, {}, {"AttackKind":12}),
        ({}, {}, {"AttackKind":26}), ({}, {}, {"AttackKind":10}),
    ]:
        case=copy.deepcopy(baseline);case["initial"].update(state_changes);case["context"].update(context_changes);case["hit"].update(hit_changes);cases.append(case)
    def flattened_state(state):
        for name in FIELDS:
            if name in VECTORS:yield from state[name].values()
            else:yield int(state[name])
    requests = ""
    for case in cases:
        context = case["context"];hit_info=case["hit"]
        values = [case["mode"], *flattened_state(case["initial"]), context["CurrentSuit"], context["SymbioteRelated"], context["Timer"],
                  int(context["CameraFlag180"]), int(context["LineOfSight"]), context["RandomValue"], case["completed"],
                  hit_info["Flags"], hit_info["PartIndex"], hit_info["AttackKind"], hit_info["Damage"], *hit_info["Direction"].values(),
                  hit_info["SlideDistance"], hit_info["SlideFrames"]]
        requests += " ".join(map(str,values))+"\n"
    with tempfile.TemporaryDirectory(prefix="openspidey-player-hit-reference-") as directory:
        path=Path(directory);(path/"reference.cpp").write_text(code)
        subprocess.run(["c++", "-std=c++17", "-O0", "-fwrapv", str(path/"reference.cpp"), "-o", str(path/"reference")], check=True)
        outputs=subprocess.check_output([str(path/"reference")],input=requests.encode()).decode().splitlines()
    for case,output in zip(cases,outputs,strict=True):
        expected=json.loads(output);values=iter(expected["state"]);state={}
        for name in FIELDS:
            if name in VECTORS:state[name]=vector([next(values),next(values),next(values)])
            else:
                value=next(values);state[name]=bool(value) if name in BOOLS else value
        expected["state"]=state;case["expected"]=expected
    destination=root/"modern/OpenSpidey.Tests/Fixtures/player-hit.json"
    destination.write_text(json.dumps(cases,separators=(",", ":"))+"\n")
    print(f"Generated {len(cases)} player hit/death/recovery source cases at {destination}")


if __name__ == "__main__":main()
