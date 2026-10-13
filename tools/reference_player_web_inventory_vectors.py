"""Compare recovered web supply methods with original PC arithmetic, state and recorded audio/XA boundaries."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original-exe", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    source = (root / "src/game/player/spidey.cpp").read_text()
    code = r'''
#include <iostream>
#include <vector>
#include "core/fixed_vector.h"
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
int G_CURRENTSUIT, G_TIMER_RELATED, disabled, difficulty, randomValue;
char busy; int *gWebbingDecreaseDisabled=&disabled, *gDifficultyLevel=&difficulty;
char *gRedbookXaPlayingMaybe=&busy; std::vector<int> events;
int Rnd(int bound) { events.insert(events.end(), {1,bound}); return randomValue; }
void SFX_PlayPos(int sound,CVector *p,int pitch) { events.insert(events.end(), {2,sound,p->vx,p->vy,p->vz,pitch}); }
void Redbook_XAPlay(int bank,int slot,int priority) { events.insert(events.end(), {3,bank,slot,priority}); }
struct CPlayer {
 short mHealth; int mWebbing,field_5D8,field_5DC,field_5D0;
 char field_5E8,field_E10,mSynthesizedInputActive; CVector mPos;
 u8 IncreaseWebbing(i32); char DecreaseWebbing(i32); unsigned char SetFireWebbing();
};
'''
    for signature in ["u8 CPlayer::IncreaseWebbing(", "char CPlayer::DecreaseWebbing(", "unsigned char CPlayer::SetFireWebbing("]:
        code += controller_method(source, signature) + "\n"
    code += r'''
#ifdef VALIDATE_ORIGINAL
void patchJump(unsigned address,void *function) {
 auto p=reinterpret_cast<unsigned char*>(address); p[0]=0xE9;
 *reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;
}
#endif
int main(int argc,char **argv) {
#ifdef VALIDATE_ORIGINAL
 assert(argc==2); original_executable::loadOriginalImage(argv[1]);
 patchJump(0x4E5DA0,reinterpret_cast<void*>(&Rnd));
 patchJump(0x471EA0,reinterpret_cast<void*>(&SFX_PlayPos));
 patchJump(0x479EE0,reinterpret_cast<void*>(&Redbook_XAPlay));
#endif
 int mode,health,fire,synth,playing,overrideFlag,amount;
 while(std::cin>>mode>>amount>>health>>G_CURRENTSUIT>>G_TIMER_RELATED) {
  CPlayer p{};p.mHealth=health;
  std::cin>>p.mWebbing>>p.field_5D8>>fire>>p.field_5D0>>p.field_5DC
   >>synth>>disabled>>difficulty>>playing>>overrideFlag>>randomValue>>p.mPos.vx>>p.mPos.vy>>p.mPos.vz;
  p.field_5E8=fire;p.mSynthesizedInputActive=synth;busy=playing;p.field_E10=overrideFlag;
#ifdef VALIDATE_ORIGINAL
  alignas(4)unsigned char raw[0xE20]={};
  std::memcpy(raw+8,&p.mPos,12);*reinterpret_cast<short*>(raw+0xE2)=p.mHealth;
  *reinterpret_cast<int*>(raw+0x5D0)=p.field_5D0;*reinterpret_cast<int*>(raw+0x5D4)=p.mWebbing;
  *reinterpret_cast<int*>(raw+0x5D8)=p.field_5D8;*reinterpret_cast<int*>(raw+0x5DC)=p.field_5DC;
  raw[0x5E8]=fire;raw[0x1AC]=synth;raw[0xE10]=overrideFlag;
  *reinterpret_cast<int*>(0x5559DC)=G_CURRENTSUIT;*reinterpret_cast<int*>(0x60CFE8)=disabled;
  *reinterpret_cast<int*>(0x54D474)=difficulty;*reinterpret_cast<char*>(0x682770)=busy;
#endif
  std::cout<<'[';
  for(int frame=0;frame<4;frame++) {
   events.clear();int result=mode==0?p.IncreaseWebbing(amount):mode==1?p.DecreaseWebbing(amount):p.SetFireWebbing();
   auto expectedEvents=events;
#ifdef VALIDATE_ORIGINAL
   *reinterpret_cast<int*>(0x6B4CA8)=G_TIMER_RELATED;events.clear();
   int actual=mode==2?reinterpret_cast<unsigned char(__attribute__((thiscall)) *)(void*)>(0x4BAFC0)(raw):
    reinterpret_cast<unsigned char(__attribute__((thiscall)) *)(void*,int)>(mode==0?0x4BAFE0:0x4BB0A0)(raw,amount);
   assert(actual==result&&events==expectedEvents&&*reinterpret_cast<int*>(raw+0x5D0)==p.field_5D0&&
    *reinterpret_cast<int*>(raw+0x5D4)==p.mWebbing&&*reinterpret_cast<int*>(raw+0x5D8)==p.field_5D8&&
    *reinterpret_cast<int*>(raw+0x5DC)==p.field_5DC&&raw[0x5E8]==(unsigned char)p.field_5E8);
   assert(raw[0xE10]==(unsigned char)p.field_E10&&raw[0x1AC]==(unsigned char)p.mSynthesizedInputActive&&
    *reinterpret_cast<short*>(raw+0xE2)==p.mHealth&&!std::memcmp(raw+8,&p.mPos,12));
#endif
   if(frame)std::cout<<',';
   std::cout<<'['<<result<<','<<p.mWebbing<<','<<p.field_5D8<<','<<(unsigned)(unsigned char)p.field_5E8<<','<<p.field_5D0<<','<<(unsigned)p.field_5DC<<",[";
   for(unsigned i=0;i<expectedEvents.size();i++){if(i)std::cout<<',';std::cout<<expectedEvents[i];}
   std::cout<<"]]";G_TIMER_RELATED+=7;
  }
  std::cout<<"]\n";
 }
}
'''
    rng = random.Random(0x4BB0A0)
    cases = []
    for mode in range(3):
        for webbing in [0, 1, 300, 4095, 4096, 8192]:
            for cartridges in [0, 1, 2, 9, 10]:
                for suit in [0, 3, 4, 6, 9, 10]:
                    cases.append([mode, 300, 100, suit, 1000, webbing, cartridges, 1, 0, 0, 0, 0, 2, 0, 0, 2, 100, -200, 300])
    for _ in range(256):
        cases.append([rng.randrange(3), rng.choice([0, 1, 4096, -1, -4096, 2147483647, -2147483648]),
                      rng.choice([-32768, 0, 1, 32767]), rng.choice([0, 3, 4, 6, 9, 10]), rng.randint(-2147483648, 2147483647),
                      rng.randint(-2147483648, 2147483647), rng.choice([-2147483648, -1, 0, 1, 10, 2147483647]),
                      rng.choice([0, 1, 127, 255]), rng.randint(-2147483648, 2147483647), rng.randint(-2147483648, 2147483647),
                      rng.choice([0, 1, 255]), rng.choice([0, 1, -1]), rng.choice([0, 1, 2, 3, -1]), rng.choice([0, 1]),
                      rng.choice([0, 1, 255]), rng.randrange(3), *[rng.randint(-2147483648, 2147483647) for _ in range(3)]])
    with tempfile.TemporaryDirectory(prefix="openspidey-web-inventory-reference-") as folder:
        cpp = Path(folder) / "reference.cpp"
        binary = Path(folder) / "reference"
        cpp.write_text(code)
        flags = ["-m32", "-fno-pie", "-no-pie", "-DVALIDATE_ORIGINAL"] if args.original_exe else []
        subprocess.run(["g++", "-std=c++17", "-O0", "-fwrapv", *flags, "-I", str(root / "src"), "-I", str(root / "tests/runtime"),
                        str(cpp), "-o", str(binary)], check=True)
        command = [str(binary)]
        if args.original_exe:
            command.append(str(args.original_exe.resolve()))
        output = subprocess.check_output(command, input=("\n".join(" ".join(map(str, case)) for case in cases) + "\n").encode()).decode().splitlines()
    fixtures = [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, output, strict=True)]
    (root / "modern/OpenSpidey.Tests/Fixtures/player-web-inventory.json").write_text(json.dumps(fixtures, separators=(",", ":")) + "\n")
    print(f"Generated {len(cases)} web inventory sequences / {4 * len(cases)} updates.")
    if args.original_exe:
        print("Every state, return value and audio/XA boundary matches the original PC instructions.")


if __name__ == "__main__":
    main()
