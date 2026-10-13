#pragma once

class CPlayer;
struct SLineInfo;
class CVector;

namespace spidey {
namespace player {

class ZipWebController {
public:
    explicit ZipWebController(CPlayer& player) : player_(player) {}
    bool tryStraightZip();
    bool trySwingZip();

private:
    bool canStart() const;
    bool findTarget(const CVector& rayEnd, int availabilityRange, SLineInfo& target);
    void startZip(const SLineInfo& target, bool releaseLookaroundTarget);
    CPlayer& player_;
};

} // namespace player
} // namespace spidey
