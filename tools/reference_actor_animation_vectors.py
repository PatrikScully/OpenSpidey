"""Compile recovered actor playback methods; optionally verify UpdateFrame against original x86 instructions."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original-exe", type=Path, help="Validate UpdateFrame with the original PC binary (Linux/i386)")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    source = (root / "src/engine/world/ob.cpp").read_text()
    harness = r'''
#include "core/types.h"
#include <iostream>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
void DoAssert(bool valid, const char*) { if (!valid) std::abort(); }
u32 animationFile[17] = {};
struct Region { u32* pAnimFile; } G_PSXREGION[1] = {{animationFile}};
struct CSuper {
    i32 mElapsedTicks, mAnimSpeed, mRegion = 0;
    u16 mAnim;
    i16 mFrame, mFrameFrac, mNumFrames, mTargetFrame;
    i8 mAnimDir;
    u8 mAnimMode, mAnimFinished;
    void UpdateFrame(); void RunAnim(i32, i32, i32); void CycleAnim(i32, i8);
};
'''
    for signature in ["void CSuper::UpdateFrame(void)", "void CSuper::RunAnim(", "void CSuper::CycleAnim(i32 anim, i8 animdir)"]:
        harness += controller_method(source, signature)
    harness += r'''
#ifdef VALIDATE_ORIGINAL
template<class T> void put(u8* actor, unsigned offset, T value) { std::memcpy(actor + offset, &value, sizeof(value)); }
template<class T> T get(u8* actor, unsigned offset) { T value; std::memcpy(&value, actor + offset, sizeof(value)); return value; }
void validate(const CSuper& initial, const CSuper& expected) {
    alignas(4) u8 actor[0x160] = {};
    put(actor, 0x80, initial.mElapsedTicks); put(actor, 0x128, initial.mFrame); put(actor, 0x12A, initial.mAnim);
    put(actor, 0x140, initial.mAnimMode); put(actor, 0x141, initial.mAnimDir); put(actor, 0x142, initial.mAnimFinished);
    put(actor, 0x144, initial.mTargetFrame); put(actor, 0x146, initial.mFrameFrac); put(actor, 0x148, initial.mNumFrames);
    put(actor, 0x14C, initial.mAnimSpeed);
    typedef void (__attribute__((thiscall)) *Original)(void*);
    reinterpret_cast<Original>(0x460DA0)(actor);
    assert(get<i32>(actor, 0x80) == expected.mElapsedTicks);
    assert(get<i16>(actor, 0x128) == expected.mFrame);
    assert(get<i16>(actor, 0x146) == expected.mFrameFrac);
    assert(get<u8>(actor, 0x142) == expected.mAnimFinished);
    assert(get<u16>(actor, 0x12A) == expected.mAnim && get<u8>(actor, 0x140) == expected.mAnimMode);
    assert(get<i8>(actor, 0x141) == expected.mAnimDir && get<i16>(actor, 0x144) == expected.mTargetFrame);
    assert(get<i16>(actor, 0x148) == expected.mNumFrames && get<i32>(actor, 0x14C) == expected.mAnimSpeed);
}
#endif
int main(int argc, char** argv) {
#ifdef VALIDATE_ORIGINAL
    assert(argc == 2);
    original_executable::loadOriginalImage(argv[1]);
    const u8 prologue[] = {0x8B,0x81,0x80,0x00,0x00,0x00};
    assert(!std::memcmp(reinterpret_cast<void*>(0x460DA0),prologue,sizeof(prologue)));
#endif
    animationFile[0] = 8;
    int operation, clip, frame, fraction, direction, mode, frames, target, finished, speed;
    int selectedClip, selectedFrames, from, to, selectedDirection, ticks;
    while (std::cin >> operation >> clip >> frame >> fraction >> direction >> mode >> frames >> target >> finished >> speed
           >> selectedClip >> selectedFrames >> from >> to >> selectedDirection >> ticks) {
        CSuper actor{};
        actor.mAnim = clip; actor.mFrame = frame; actor.mFrameFrac = fraction; actor.mAnimDir = direction;
        actor.mAnimMode = mode; actor.mNumFrames = frames; actor.mTargetFrame = target;
        actor.mAnimFinished = finished; actor.mAnimSpeed = speed; actor.mElapsedTicks = ticks;
        animationFile[selectedClip * 2 + 2] = selectedFrames;
        CSuper initial = actor;
        if (operation == 0) actor.RunAnim(selectedClip,from,to);
        else if (operation == 1) actor.CycleAnim(selectedClip,selectedDirection);
        else {
            actor.UpdateFrame();
#ifdef VALIDATE_ORIGINAL
            validate(initial,actor);
#endif
        }
        std::cout << actor.mAnim << ' ' << actor.mFrame << ' ' << u16(actor.mFrameFrac) << ' ' << int(actor.mAnimDir) << ' '
            << int(actor.mAnimMode) << ' ' << u16(actor.mNumFrames) << ' ' << actor.mTargetFrame << ' '
            << unsigned(actor.mAnimFinished) << ' ' << actor.mAnimSpeed << ' ' << actor.mElapsedTicks << '\n';
    }
}
'''
    randomizer = random.Random(0x460DA0)
    cases = []
    for index in range(1536):
        state = [randomizer.randrange(8), randomizer.choice([-32768, -1, 0, 1, 32767]), randomizer.randrange(65536),
                 randomizer.choice([-128, -1, 0, 1, 127]), randomizer.choice([0, 1, 2, 255]),
                 randomizer.choice([0, 1, 2, 30, 32767, 32768, 65535]), randomizer.randint(-32768, 32767),
                 randomizer.choice([0, 1, 255]), randomizer.choice([0, 0x8000, 0x10000, 0x18000, -2147483648, 2147483647])]
        selected_frames = randomizer.choice([0, 1, 2, 3, 30, 32767, 32768, 65535])
        arguments = [randomizer.randrange(8), selected_frames,
                     randomizer.choice([-2147483648, -2, -1, 0, selected_frames - 1, selected_frames, 2147483647]),
                     randomizer.choice([-2147483648, -2, -1, 0, selected_frames - 1, selected_frames, 2147483647]),
                     randomizer.choice([-128, -1, 0, 1, 127]), randomizer.choice([-2147483648, -1, 0, 1, 2, 3, 4, 6, 2147483647])]
        cases.append(dict(operation=index % 3, state=state, arguments=arguments))
    requests = "".join(" ".join(map(str, [case["operation"], *case["state"], *case["arguments"]])) + "\n" for case in cases)
    with tempfile.TemporaryDirectory(prefix="openspidey-playback-reference-") as directory:
        path = Path(directory)
        (path / "reference.cpp").write_text(harness)
        flags = ["-m32", "-fno-pie", "-no-pie", "-DVALIDATE_ORIGINAL"] if args.original_exe else []
        subprocess.run(["c++", "-std=c++17", "-O0", "-fwrapv", "-fsigned-char", *flags, "-I", str(root / "src"),
                        "-I", str(root / "tests/runtime"), str(path / "reference.cpp"), "-o", str(path / "reference")], check=True)
        command = [str(path / "reference")]
        if args.original_exe:
            command.append(str(args.original_exe.resolve()))
        result = subprocess.run(command, input=requests, text=True, capture_output=True, check=True)
    for case, line in zip(cases, result.stdout.splitlines(), strict=True):
        case["result"] = list(map(int, line.split()))
        if len(case["result"]) != 10:
            raise RuntimeError("Unexpected actor animation output shape.")
    target = root / "modern/OpenSpidey.Tests/Fixtures/actor-animation.json"
    target.write_text(json.dumps(cases, separators=(",", ":")) + "\n")
    print(f"Generated {len(cases)} recovered actor playback fixtures.")
    if args.original_exe:
        print(f"Validated {sum(case['operation'] == 2 for case in cases)} UpdateFrame cases against original x86 instructions.")


if __name__ == "__main__":
    main()
