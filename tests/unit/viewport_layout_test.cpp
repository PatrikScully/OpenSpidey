#include "engine/rendering/viewport_layout.h"
#include <cassert>
#include <cmath>

using spidey::rendering::ViewportLayout;
using spidey::rendering::ViewMode;

int main() {
    const auto menu = ViewportLayout::calculate(1280, 720, 640, 480, ViewMode::Menu);
    assert(menu.viewport().x == 160 && menu.viewport().width == 960);
    assert(menu.horizontalScale() == 1.0f && menu.canvasExtension() == 0.0f);

    const auto game = ViewportLayout::calculate(1280, 720, 640, 480, ViewMode::Gameplay);
    assert(game.viewport().x == 0 && game.viewport().width == 1280);
    assert(std::abs(game.horizontalScale() - 4.0f / 3.0f) < 0.0001f);
    assert(game.hudOffset(20, 100) < 0 && game.hudOffset(540, 620) > 0);
    assert(game.hudOffset(300, 340) == 0);

    const auto portrait = ViewportLayout::calculate(480, 640, 640, 480, ViewMode::Gameplay);
    assert(portrait.viewport().height == 360 && portrait.viewport().y == 140);
    assert(portrait.horizontalScale() == 1 && portrait.canvasExtension() == 0);

    const auto invalid = ViewportLayout::calculate(0, 720, 640, 480, ViewMode::Gameplay);
    assert(invalid.viewport().width == 0 && invalid.viewport().height == 0);
}
