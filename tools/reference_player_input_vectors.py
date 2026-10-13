"""Compile the recovered analogue-input reader with a synthetic pad and scripted-axis provider."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    player = (root / "src/game/player/spidey.cpp").read_text()
    maths = (root / "src/engine/rendering/ps2funcs.cpp").read_text()
    harness = r'''
#include "core/types.h"
#include <cmath>
#include <iostream>
#include <cstring>
void print_if_false(int, const char*) {}
'''
    harness += controller_method(maths, "int ratan2(int x, int y)")
    harness += r'''
struct CPlayer {
    void* field_E0C;
    char field_E2D, field_E2E, field_E2F, field_E30;
    i32 field_E18, field_8F0;
    u8 mSynthesizedInputActive, field_AD8, field_AD9, field_8E8, field_8E9, field_211;
    u16 field_EA0, field_EA2;
    i16 field_E32, field_E34;
    char scriptedForward, scriptedSideways;
    void SynthesizeAnalogueInput() { field_E2D = scriptedForward; field_E2E = scriptedSideways; }
    void ReadAnalogueInput();
};
'''
    harness += controller_method(player, "void CPlayer::ReadAnalogueInput(void)")
    harness += r'''
int main() {
    int forward, sideways, ramp, heading, reference, primary, secondary, centered;
    int analog, af, as, up, down, right, left, leftOne, leftTwo, scripted, sf, ss;
    int ignore, numerator, denominator, invertPrimary, invertSecondary;
    while (std::cin >> forward >> sideways >> ramp >> heading >> reference >> primary >> secondary >> centered
           >> analog >> af >> as >> up >> down >> right >> left >> leftOne >> leftTwo >> scripted >> sf >> ss
           >> ignore >> numerator >> denominator >> invertPrimary >> invertSecondary) {
        alignas(4) u8 pad[0x170] = {};
        i32 type = analog ? 0x73 : 0;
        std::memcpy(pad+0x16C,&type,4);
        pad[0x168] = af; pad[0x169] = as;
        pad[0xA0] = up; pad[0xB0] = down; pad[0x90] = right; pad[0x80] = left;
        pad[0x40] = leftOne; pad[0x51] = leftTwo;
        CPlayer actor{};
        actor.field_E0C = pad;
        actor.field_E2D = forward; actor.field_E2E = sideways;
        actor.field_8F0 = ramp; actor.field_E32 = heading; actor.field_E34 = reference;
        actor.field_AD8 = primary; actor.field_AD9 = secondary; actor.field_211 = centered;
        actor.mSynthesizedInputActive = scripted; actor.scriptedForward = sf; actor.scriptedSideways = ss;
        actor.field_E18 = ignore; actor.field_EA0 = numerator; actor.field_EA2 = denominator;
        actor.field_8E9 = invertPrimary; actor.field_8E8 = invertSecondary;
        actor.ReadAnalogueInput();
        std::cout << int(actor.field_E2D) << ' ' << int(actor.field_E2E) << ' ' << int(actor.field_E2F) << ' '
                  << int(actor.field_E30) << ' ' << actor.field_8F0 << ' ' << actor.field_E32 << ' '
                  << unsigned(actor.field_AD8) << ' ' << unsigned(actor.field_AD9) << ' '
                  << unsigned(actor.field_211) << ' ' << unsigned(pad[0x40]) << ' ' << unsigned(pad[0x51]) << '\n';
    }
}
'''
    randomizer = random.Random(0x4C5AA0)
    cases = []
    for index in range(768):
        state = [randomizer.randint(-128, 127), randomizer.randint(-128, 127),
                 randomizer.choice([0, 32, 224, 256, 2147483647]), randomizer.randint(-32768, 32767),
                 randomizer.randint(-32768, 32767), *[randomizer.getrandbits(1) for _ in range(3)]]
        axes = [randomizer.randint(-128, 127), randomizer.randint(-128, 127)]
        if index % 4 == 0:
            axes = [0, 0]
        sample = [randomizer.getrandbits(1), *axes, *[randomizer.getrandbits(1) for _ in range(6)],
                  int(index % 3 == 0), *axes]
        options = [randomizer.getrandbits(1), randomizer.choice([0, 1, 2, 127, 65535]),
                   randomizer.choice([1, 2, 128, 65535]), randomizer.getrandbits(1), randomizer.getrandbits(1)]
        cases.append(dict(state=state, sample=sample, options=options))
    requests = "".join(" ".join(map(str, case["state"] + case["sample"] + case["options"])) + "\n" for case in cases)
    with tempfile.TemporaryDirectory(prefix="openspidey-input-reference-") as directory:
        path = Path(directory)
        (path / "reference.cpp").write_text(harness)
        subprocess.run(["c++", "-std=c++17", "-O0", "-fwrapv", "-fsigned-char", "-I", str(root / "src"),
                        str(path / "reference.cpp"), "-o", str(path / "reference")], check=True)
        result = subprocess.run([str(path / "reference")], input=requests, text=True, capture_output=True, check=True)
    for case, line in zip(cases, result.stdout.splitlines(), strict=True):
        case["result"] = list(map(int, line.split()))
        if len(case["result"]) != 11:
            raise RuntimeError("Unexpected player input reference output shape.")
    destination = root / "modern/OpenSpidey.Tests/Fixtures/player-input.json"
    destination.write_text(json.dumps(cases, indent=2) + "\n")
    print(f"Generated {len(cases)} recovered player-input fixtures.")


if __name__ == "__main__":
    main()
