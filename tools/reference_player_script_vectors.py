"""Compile the recovered player-input VM with synthetic actor/world callbacks and emit multi-frame fixtures."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / "src/game/player/scripted_input_controller.cpp").read_text()
    maths = (root / "src/engine/rendering/ps2funcs.cpp").read_text()
    utils = (root / "src/core/utils.cpp").read_text()
    harness = r'''
#include "core/fixed_vector.h"
#include "game/cinematics/script_command_list.h"
#include "compatibility/export.h"
#include <cmath>
#include <iostream>
#include <vector>
#include <cstring>
using spidey::cinematics::ScriptCommandView;
using VECTOR = Vector;
void print_if_false(int condition, const char* message) { if (!condition) { std::cerr << message; std::exit(2); } }
'''
    harness += controller_method(maths, "int ratan2(int x, int y)")
    harness += controller_method(maths, "void VectorNormal(VECTOR* a1, VECTOR* a2)")
    harness += controller_method(maths, "i32 M3dMaths_SquareRoot0(i32 i)")
    harness += controller_method(utils, "u32 Utils_Dist(const CVector &a1, const CVector &a2)")
    harness += controller_method(utils, "i32 Utils_XZDist(const CVector* a1, const CVector *a2)")
    harness += controller_method(source, "enum ScriptOpcode") + ";\n"
    harness += r'''
enum { RegisterCount = 20, RegisterStride = 16 };
struct SSinCos { short sin, cos; } headingTable[4096];
SSinCos* G_RCOSSIN_TBL = headingTable;
const int FLATBIT_VELOCITIES_SIZE = 4096;
const short* headingSine = reinterpret_cast<short*>(headingTable);
const short* headingCosine = headingSine + 1;
i32 scratch = 0, exitFlag = 0, gLevelStatus = 0;
u8 scriptFlag = 0, abortFlag = 0;
i32* gPlayerSynthTickScratch = &scratch;
i32* gPshellForceLevelExit = &exitFlag;
u8* gSynthInputScriptFlag = &scriptFlag;
u8* gSubmarinerDieRelated = &abortFlag;
struct Camera { short field_23A; } camera;
Camera* G_CAMERA_LIST = &camera;
struct Boss { CVector mPos; } boss;
Boss* G_MECHLIST = nullptr;
int levelId = 0, voiceCalls = 0;
int Trig_GetLevelId() { return levelId; }
void Trig_GetPosition(CVector* result, int id) { *result = CVector(id * 409600, (id % 3) * 409600, -id * 819200); }
void Redbook_XAPlay(short, short, short) { voiceCalls++; }
struct CPlayer {
    CVector mPos, mVel;
    i32 mScriptElapsedTicks = 0, mElapsedTicks = 0, mScriptCursorAddress = 0, mScriptCheckpointNode = 0;
    i32* mScriptCommandHead = nullptr;
    u8 mScriptBytecodeActive = 1, mSynthesizedInputActive = 1, mIsCrawling = 0;
    u8 pad[320] = {}, slots[320] = {};
    void* field_E0C = pad;
    i32 field_EA6 = 99, mAnim = 0, mAnimFinished = 0, mInputFlags = 0, mActionState = 0, field_E00 = 0;
    char field_E2D = 0, field_E2E = 0;
    int rawPlays = 0, policyPlays = 0, teleports = 0, stands = 0;
    void RunAnim(int id, int, int) { mAnim = id; mAnimFinished = 0; rawPlays++; }
    void SwitchToStandMode() { mActionState = 1; stands++; }
    i32* KillCommandBlock(i32* command) {
        i32* next = ScriptCommandView(command).next();
        if (mScriptCommandHead == command) mScriptCommandHead = next;
        else { i32* previous = mScriptCommandHead; while (ScriptCommandView(previous).next() != command) previous = ScriptCommandView(previous).next(); ScriptCommandView(previous).setNext(next); }
        delete[] command;
        return next;
    }
    void KillAllCommandBlocks() { while (mScriptCommandHead) KillCommandBlock(mScriptCommandHead); }
};
struct PlayerAnimationController {
    CPlayer& player;
    explicit PlayerAnimationController(CPlayer& p) : player(p) {}
    void play(int id) { player.mAnim = id; player.mAnimFinished = 0; player.policyPlays++; }
};
struct ScriptedInputController {
    CPlayer& player_;
    explicit ScriptedInputController(CPlayer& player) : player_(player) {}
    void update(); void prepareInput(); void executeDueInstructions();
    int updateCommands(); void publishInput(int); void finishIfIdle();
    u8* buttonState(int index) { return player_.slots + index * 16; }
    void teleportToNode(int id) { Trig_GetPosition(&player_.mPos, id); player_.teleports++; }
    i32* createCommand(i32 type, u32 count) {
        auto* record = new i32[count + 3]{};
        record[0] = type; record[1] = count + 3;
        if (!player_.mScriptCommandHead) player_.mScriptCommandHead = record;
        else { i32* tail = player_.mScriptCommandHead; while (ScriptCommandView(tail).next()) tail = ScriptCommandView(tail).next(); ScriptCommandView(tail).setNext(record); }
        return record;
    }
};
'''
    harness += controller_method(maths, "void DCInitSinCosTable(void)")
    for method in ["void ScriptedInputController::update()", "void ScriptedInputController::prepareInput()",
                   "void ScriptedInputController::executeDueInstructions()", "i32 ScriptedInputController::updateCommands()",
                   "void ScriptedInputController::publishInput(i32 heldMask)", "void ScriptedInputController::finishIfIdle()"]:
        harness += controller_method(source, method)
    harness += r'''
void emit(const CVector& v) { std::cout << v.vx << ' ' << v.vy << ' ' << v.vz << ' '; }
int main() {
    DCInitSinCosTable();
    for (const auto& heading : headingTable) std::cout << heading.sin << ' ' << heading.cos << ' ';
    std::cout << '\n';
    int wordCount, frameCount, initialElapsed;
    while (std::cin >> wordCount >> frameCount >> initialElapsed) {
        std::vector<short> words(wordCount);
        for (short& word : words) { int value; std::cin >> value; word = value; }
        CPlayer player;
        player.mScriptCursorAddress = reinterpret_cast<i32>(words.data());
        player.mScriptElapsedTicks = initialElapsed;
        scriptFlag = 0; gLevelStatus = 0; voiceCalls = 0;
        for (int frame = 0; frame < frameCount; frame++) {
            int finished, signal, crawl, heading, abort, force, present, b14, b15;
            std::cin >> player.mElapsedTicks >> player.mPos.vx >> player.mPos.vy >> player.mPos.vz
                >> finished >> signal >> crawl >> heading >> abort >> force >> levelId >> present
                >> boss.mPos.vx >> boss.mPos.vy >> boss.mPos.vz >> b14 >> b15;
            player.mAnimFinished = finished; player.mInputFlags = signal; player.mIsCrawling = crawl;
            camera.field_23A = heading; abortFlag = abort; exitFlag = force; G_MECHLIST = present ? &boss : nullptr;
            player.pad[14 * 16] = b14; player.pad[15 * 16] = b15;
            player.field_E2D = 0; player.field_E2E = 0;
            if (player.mSynthesizedInputActive) ScriptedInputController(player).update();
            int count = 0;
            for (i32* c = player.mScriptCommandHead; c; c = ScriptCommandView(c).next()) count++;
            std::cout << unsigned(player.mSynthesizedInputActive) << ' ' << unsigned(player.mScriptBytecodeActive) << ' '
                << player.mScriptElapsedTicks << ' ' << (reinterpret_cast<short*>(player.mScriptCursorAddress) - words.data()) << ' '
                << player.mScriptCheckpointNode << ' ' << count << ' ';
            emit(player.mPos); emit(player.mVel);
            std::cout << player.mAnim << ' ' << player.mAnimFinished << ' ' << player.mInputFlags << ' ' << player.mActionState << ' '
                << player.field_E00 << ' ' << unsigned(scriptFlag) << ' ' << gLevelStatus << ' ' << int(player.field_E2D) << ' '
                << int(player.field_E2E) << ' ' << player.field_EA6 << ' ' << player.rawPlays << ' ' << player.policyPlays << ' '
                << player.teleports << ' ' << player.stands << ' ' << voiceCalls << ' ';
            for (int index = 0; index < 20; index++) for (int byte = 0; byte < 3; byte++) std::cout << unsigned(player.pad[index * 16 + byte]) << ' ';
            std::cout << '\n';
        }
        player.KillAllCommandBlocks();
    }
}
'''
    randomizer = random.Random(0x4BC300)
    cases = []
    operations = [(1, [1]), (2, [2]), (3, [8, 5]), (3, [10, 4]), (3, [16, 6]), (4, [3, 7, 99]),
                  (5, [3, 1]), (6, [17, 3]), (7, [4]), (8, [215, 5]), (9, [3]), (15, [33]),
                  (16, [4]), (17, []), (18, [1])]
    for case_index in range(96):
        words = []
        chosen = operations if case_index == 0 else [randomizer.choice(operations) for _ in range(12)]
        for index, (opcode, operands) in enumerate(chosen):
            due = 0 if index == 0 else randomizer.choice([-1, 0, 1, 4])
            words.extend([due, opcode, *operands])
        words.extend([0, 255])
        frames = []
        for frame in range(36):
            node = frame % 5
            position = [node * 409600, (node % 3) * 409600, -node * 819200]
            # Include exact arrival targets, differing heights, and out-of-range positions.
            if frame % 6 == 0:
                position = [randomizer.randint(-3000000, 3000000) for _ in range(3)]
            frames.append([randomizer.choice([0, 1, 2, 4, 6]), *position,
                           int(frame % 3 == 0), int(frame % 5 == 0), frame % 2,
                           randomizer.randint(-32768, 32767), int(case_index % 3 == 0 and frame == 33),
                           int(frame > 30), 1795 if case_index % 4 == 0 else 1, int(case_index % 4 == 0),
                           -31900664, 0, 34009330, frame % 2, (frame + 1) % 2])
        cases.append(dict(words=words, initialElapsed=randomizer.choice([0, 3, 10]), frames=frames))
    requests = "".join(" ".join(map(str, [len(case["words"]), len(case["frames"]), case["initialElapsed"], *case["words"],
                                         *(value for frame in case["frames"] for value in frame)])) + "\n" for case in cases)
    with tempfile.TemporaryDirectory(prefix="openspidey-script-reference-") as directory:
        path = Path(directory)
        (path / "reference.cpp").write_text(harness)
        subprocess.run(["c++", "-m32", "-std=c++17", "-O0", "-fwrapv", "-fsigned-char", "-I", str(root / "src"),
                        str(path / "reference.cpp"), str(root / "src/core/fixed_vector.cpp"), "-o", str(path / "reference")], check=True)
        result = subprocess.run([str(path / "reference")], input=requests, text=True, capture_output=True, check=True)
    lines = iter(result.stdout.splitlines())
    heading_words = list(map(int, next(lines).split()))
    if len(heading_words) != 8192:
        raise RuntimeError("Unexpected heading-table size.")
    heading_target = root / "modern/OpenSpidey.Tests/Fixtures/heading-table.json"
    heading_target.write_text(json.dumps(heading_words, separators=(",", ":")) + "\n")
    for case in cases:
        case["results"] = [list(map(int, next(lines).split())) for _ in case["frames"]]
        if any(len(frame) != 87 for frame in case["results"]):
            raise RuntimeError("Unexpected player-script result shape.")
    if next(lines, None) is not None:
        raise RuntimeError("Unexpected extra player-script results.")
    target = root / "modern/OpenSpidey.Tests/Fixtures/player-scripts.json"
    target.write_text(json.dumps(cases, separators=(",", ":")) + "\n")
    print(f"Generated {len(cases)} recovered player-script sequences ({sum(len(case['frames']) for case in cases)} updates).")


if __name__ == "__main__":
    main()
