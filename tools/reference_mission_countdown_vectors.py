"""Compile the genuine shared Panel_UpdateTimer countdown, including unsigned rate/clock wrap and scripted pause."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/ui/panel.cpp').read_text()
    code = r'''
#include <iostream>
using u32=unsigned;
using u8=unsigned char;
u32 gBombRelated,gBombAIRelated,gBombDieTimerRelated,G_TIMER_RELATED;
u8 gBombDieRelatedTwo;
struct Player{unsigned field_E18=0;bool mSynthesizedInputActive=false;} player;
Player*G_MECHLIST_PLAYER=&player;
'''
    code += controller_method(source, ' void Panel_UpdateTimer(') + r'''
int main(){unsigned running,rate,remaining,last,clock,mask,synth;while(std::cin>>running>>rate>>remaining>>last>>clock>>mask>>synth){gBombDieRelatedTwo=running;gBombRelated=rate;gBombAIRelated=remaining;gBombDieTimerRelated=last;G_TIMER_RELATED=clock;player.field_E18=mask;player.mSynthesizedInputActive=synth;Panel_UpdateTimer();std::cout<<'['<<gBombAIRelated<<','<<gBombDieTimerRelated<<"]\n";}}
'''
    rng = random.Random(0x60F774)
    cases = [[running, rate, remaining, last, clock, mask, synth]
             for running in [0, 1, 255]
             for rate in [0, 4096, 8192, 0xFFFFFFFF]
             for remaining in [0, 1, 2, 4260, 7260, 0xFFFFFFFF]
             for last, clock in [(0, 2), (100, 100), (100, 99), (0xFFFFFFFF, 1)]
             for mask, synth in [(0, 0), (1, 0), (0, 1)]]
    cases += [[rng.choice([0, 1, 255]), *[rng.randrange(0x100000000) for _ in range(4)],
               rng.randrange(0x100000000), rng.randrange(2)] for _ in range(128)]
    with tempfile.TemporaryDirectory(prefix='openspidey-countdown-') as directory:
        cpp = Path(directory) / 'reference.cpp'; binary = Path(directory) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary)], input=('\n'.join(' '.join(map(str, row)) for row in cases) + '\n').encode()).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/mission-countdown.json').write_text(json.dumps(
        [dict(input=row, expected=json.loads(line)) for row, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Generated {len(cases)} exact shared source Panel_UpdateTimer cases.')


if __name__ == '__main__':
    main()
