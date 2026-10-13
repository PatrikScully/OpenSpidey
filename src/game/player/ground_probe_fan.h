#pragma once
#include "core/types.h"

namespace spidey {
namespace player {
struct GroundProbePoint { float x, z; };

// Eight offsets around the player followed by one unoffset repeat. Float
// coordinates intentionally retain the original world-unit quantization.
class GroundProbeFan {
public:
    static GroundProbePoint point(float x, float z, float spread, unsigned index) {
        const float half = spread * 0.5f;
        switch (index) {
            case 0: return GroundProbePoint{x + spread, z};
            case 1: return GroundProbePoint{x - spread, z};
            case 2: return GroundProbePoint{x, z + spread};
            case 3: return GroundProbePoint{x, z - spread};
            case 4: return GroundProbePoint{half + x, half + z};
            case 5: return GroundProbePoint{half + x, z - half};
            case 6: return GroundProbePoint{x - half, half + z};
            case 7: return GroundProbePoint{x - half, z - half};
            default: return GroundProbePoint{x, z};
        }
    }
};
} // namespace player
} // namespace spidey
