#include "core/distance.h"
#include <algorithm>
#include <cassert>

int main() {
    using spidey::math::approximateDistance;
    using spidey::math::approximateHorizontalDistance;
    const CVector origin(0, 0, 0);
    int axes[3] = {4, 8, 16};
    do {
        for (int signs = 0; signs < 8; ++signs) {
            const CVector position((signs & 1 ? -axes[0] : axes[0]) * 4096,
                                   (signs & 2 ? -axes[1] : axes[1]) * 4096,
                                   (signs & 4 ? -axes[2] : axes[2]) * 4096);
            assert(approximateDistance(origin, position) == 21);
            assert(approximateDistance(position, origin) == 21);
            assert(approximateDistance(position, position) == 0);
        }
    } while (std::next_permutation(axes, axes + 3));
    assert(approximateHorizontalDistance(origin, CVector(16 * 4096, 99 * 4096, 4 * 4096)) == 18);
    assert(approximateDistance(origin, CVector(4095, 1, 1)) == 0);
    assert(approximateDistance(origin, CVector(4095, 2, 1)) == 1);
    assert(approximateDistance(CVector(100, 200, 300), CVector(4195, 202, 301)) == 1);
}
