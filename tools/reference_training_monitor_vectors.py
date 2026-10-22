"""Capture the original training module: init 0x4DDC60, clear 0x4DDCC0, SpideyDied 0x4DDCE0, MonitorLevel 0x4DDCF0."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <iostream>
#include <vector>
std::vector<long long> events;
int level, positions[8][3];
alignas(16) unsigned char player[0x1000];
unsigned short nodes[8][4]; unsigned short* offsets[8];
extern "C" {
void setUser(const char* name, unsigned mask) { assert(std::strcmp(name, "training") == 0); events.insert(events.end(), {6, (long long)mask}); }
int levelId() { return level; }
void scale(int value) { events.insert(events.end(), {1, value}); }
void justify(int value) { events.insert(events.end(), {2, value}); }
void draw(int x, int y, const unsigned char* text, int argument, int depth) {
  events.insert(events.end(), {3, x, y, argument, depth});
  int length = 0; while (text[length]) length++;
  events.push_back(length); for (int i = 0; i < length; i++) events.push_back(text[i]);
}
void endTraining() { events.push_back(4); }
void position(int* out, int node) { for (int i = 0; i < 3; i++) out[i] = positions[node][i]; }
void pulse(int node) { events.insert(events.end(), {5, node}); }
}
void patch(unsigned address, void* function) { auto* p = (unsigned char*)address; p[0] = 0xE9; *reinterpret_cast<unsigned*>(p + 1) = (unsigned)function - address - 5; }
template<class T> T& at(unsigned address) { return *reinterpret_cast<T*>(address); }
int main(int argc, char** argv) {
  assert(argc == 2); original_executable::loadOriginalImage(argv[1]);
  patch(0x4B9320, (void*)&setUser); patch(0x4DE770, (void*)&levelId); patch(0x458620, (void*)&scale);
  patch(0x458610, (void*)&justify); patch(0x458700, (void*)&draw); patch(0x47B720, (void*)&endTraining);
  patch(0x4E3940, (void*)&position); patch(0x4DFC20, (void*)&pulse);
  for (int i = 0; i < 8; i++) offsets[i] = nodes[i];
  at<unsigned short**>(0x6B466C) = offsets;
  int seconds, score, rate, nodeCount, frames;
  while (std::cin >> level >> seconds >> score >> rate >> nodeCount) {
    at<int>(0x551288) = seconds; at<int>(0x55129C) = score; at<int>(0x54E8D4) = rate; at<int>(0x6B4670) = nodeCount;
    for (int i = 0; i < 8; i++) { std::cin >> nodes[i][0]; for (int j = 0; j < 3; j++) std::cin >> positions[i][j]; }
    for (unsigned a = 0x6B36E8; a < 0x6B3708; a += 4) at<int>(a) = 0x5A5A5A5A;
    at<unsigned char>(0x6B378C) = at<unsigned char>(0x6B378D) = at<unsigned char>(0x6B378E) = 7;
    at<unsigned char>(0x60F771) = at<unsigned char>(0x60F772) = at<unsigned char>(0x60CFC6) = 0; at<unsigned>(0x60F774) = at<unsigned>(0x60F778) = 0;
    std::cin >> frames;
    std::cout << '[';
    for (int frame = 0; frame < frames; frame++) {
      int op, present, health, e18, f568, f5d0, clock, kills, remaining, tac, flags, offsetY, display, running;
      std::cin >> op >> present >> health >> e18 >> f568 >> f5d0 >> clock >> kills >> remaining >> tac >> flags >> offsetY >> display >> running;
      at<void*>(0x6A9038) = present ? player : nullptr;
      *reinterpret_cast<short*>(player + 0xE2) = health; *reinterpret_cast<int*>(player + 0xE18) = e18;
      *reinterpret_cast<int*>(player + 0x568) = f568; *reinterpret_cast<int*>(player + 0x5D0) = f5d0;
      at<int>(0x6B4CA8) = clock; at<int>(0x60CFBC) = kills; at<int>(0x60F76C) = offsetY;
      if (remaining >= 0) at<int>(0x60F774) = remaining;
      if (tac >= 0) at<unsigned char>(0x60CFC6) = tac;
      if (display >= 0) at<unsigned char>(0x60F771) = display;
      if (running >= 0) at<unsigned char>(0x60F772) = running;
      at<unsigned>(0x6828D8) = flags;
      events.clear();
      if (op == 0) reinterpret_cast<void(*)(void*)>(0x4DDC60)(player + 0x800);
      else if (op == 1) reinterpret_cast<void(*)()>(0x4DDCF0)();
      else if (op == 2) reinterpret_cast<void(*)()>(0x4DDCE0)();
      else reinterpret_cast<void(*)()>(0x4DDCC0)();
      if (frame) std::cout << ',';
      std::cout << "{\"state\":[";
      for (unsigned a = 0x6B36E8; a < 0x6B3708; a += 4) std::cout << at<int>(a) << ',';
      std::cout << (int)at<unsigned char>(0x6B378C) << ',' << (int)at<unsigned char>(0x6B378D) << ',' << (int)at<unsigned char>(0x6B378E) << ','
        << (int)at<unsigned char>(0x60CFC6) << ',' << (int)at<unsigned char>(0x60F771) << ',' << (int)at<unsigned char>(0x60F772) << ','
        << at<unsigned>(0x60F774) << ',' << at<unsigned>(0x54E8D4) << ',' << at<unsigned>(0x60F778) << ',' << at<int>(0x55129C) << ','
        << at<unsigned>(0x6828D8) << "],\"events\":[";
      for (unsigned i = 0; i < events.size(); i++) { if (i) std::cout << ','; std::cout << events[i]; }
      std::cout << "]}";
    }
    std::cout << "]\n";
  }
}
'''
    rng = random.Random(0x7A1)
    levels = [0x901, 0x902, 0x903, 0x904, 0x900, 0x905, 0x1101, 0x1104, 0x1105, 0x1201, 0x1204, 0x1200, 0x1301, 0x1302,
              0x1303, 0x1304, 0x1501, 0x1601, 0x1701, 0x1702, 0x1801, 0x501]
    special = [-2147483648, -100000, -1000, -101, -100, -99, -9, -1, 0, 1, 9, 10, 39, 40, 41, 99, 100, 7499, 7500, 7501,
               9999, 10000, 12345, 99999, 100000, 2147483647]
    cases = []
    for case in range(1800):
        level = levels[case % len(levels)]
        # The original dereferences the player unconditionally in item hunt and zip-line training.
        needs_player = level in (0x1601, 0x1701)
        node_count = rng.choice([0, 1, 2, 5, 8])
        nodes = []
        for _ in range(8):
            zero = rng.random() < 0.4
            nodes.append([rng.choice([1, 1, 0, 2, 0x101]),
                          *([0, 0, 0] if zero else [rng.choice([0, 4096, -4096, 1]) for _ in range(3)])])
        seconds = rng.choice([-1, 0, 1, 30, 60, 90, 120, 71582789])
        clock = rng.choice([0, 1, 1000, 0x7FFFFFF0, 0xFFFFFFF0 - 2**32, 36000])
        frames = []
        frames.append([0, 1, 100, 0, 0, 0, clock, 0, -1, -1, 0, 0, -1, -1])
        for _ in range(rng.randint(1, 24)):
            clock = (clock + rng.choice([0, 1, 2, 15, 16, 17, 30, 31, 32, 3600, 0x4650, 0x8CA0, 0x8CA1])) & 0xFFFFFFFF
            if clock >= 2**31:
                clock -= 2**32
            present = 1 if needs_player else int(rng.random() < 0.85)
            frames.append([rng.choice([1, 1, 1, 1, 1, 1, 2, 3]), present,
                           rng.choice([100, 1, 0, -1, 200, -32768]), rng.choice([0, 0, 0, 1, -1]),
                           rng.choice(special), rng.choice(special), clock, rng.choice(special),
                           rng.choice([-1, -1, -1, 0, 1, 0x4650, 0x464F, 3600]), rng.choice([-1, -1, 0, 1, 255]),
                           rng.choice([0, 0x100, 0x200, 0xFFFFFFFF - 2**32, 0xFEFF]), rng.choice([0, 12, -30]),
                           rng.choice([-1, -1, 0, 1]), rng.choice([-1, -1, 0, 1])])
        cases.append(dict(level=level, seconds=seconds, score=rng.choice([-1000, 0, 55]), rate=rng.choice([0, 4096, 8192]),
                          nodeCount=node_count, nodes=nodes, frames=frames))

    def encode(item):
        values = [item['level'], item['seconds'], item['score'], item['rate'], item['nodeCount']]
        for node in item['nodes']:
            values.extend(node)
        values.append(len(item['frames']))
        for frame in item['frames']:
            values.extend(frame)
        return ' '.join(map(str, values))

    with tempfile.TemporaryDirectory(prefix='openspidey-training-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                        input=('\n'.join(map(encode, cases)) + '\n').encode()).decode().splitlines()
    fixtures = [dict(input=item, expected=json.loads(line)) for item, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/training-monitor.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} original training sequences ({sum(len(c["frames"]) for c in cases)} module calls).')


if __name__ == '__main__':
    main()
