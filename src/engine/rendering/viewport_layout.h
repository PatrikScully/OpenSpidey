#pragma once

namespace spidey {
namespace rendering {

struct PixelRectangle {
    int x;
    int y;
    int width;
    int height;
};

enum class ViewMode {
    Menu,
    Gameplay
};

// Pure display geometry. SDL supplies physical pixels; the renderer applies
// the resulting viewport and logical canvas to its projection.
class ViewportLayout {
public:
    static ViewportLayout calculate(int outputWidth, int outputHeight,
                                    int canvasWidth, int canvasHeight,
                                    ViewMode mode) {
        ViewportLayout layout;
        if (outputWidth <= 0 || outputHeight <= 0 ||
            canvasWidth <= 0 || canvasHeight <= 0) {
            return layout;
        }

        layout.viewport_ = {0, 0, outputWidth, outputHeight};
        const double outputAspect = static_cast<double>(outputWidth) / outputHeight;
        const double canvasAspect = static_cast<double>(canvasWidth) / canvasHeight;
        if (mode == ViewMode::Menu && outputAspect > canvasAspect) {
            layout.viewport_.width = static_cast<int>(static_cast<double>(outputHeight) * canvasWidth / canvasHeight);
        } else if (outputAspect < canvasAspect) {
            layout.viewport_.height = static_cast<int>(static_cast<double>(outputWidth) * canvasHeight / canvasWidth);
        }
        if (layout.viewport_.width < 1) layout.viewport_.width = 1;
        if (layout.viewport_.height < 1) layout.viewport_.height = 1;
        layout.viewport_.x = (outputWidth - layout.viewport_.width) / 2;
        layout.viewport_.y = (outputHeight - layout.viewport_.height) / 2;

        if (mode == ViewMode::Gameplay && outputAspect > canvasAspect) {
            layout.horizontalScale_ = static_cast<float>(outputAspect / canvasAspect);
            layout.canvasExtension_ = canvasWidth * (layout.horizontalScale_ - 1.0f) * 0.5f;
        }
        layout.canvasWidth_ = canvasWidth;
        return layout;
    }

    const PixelRectangle& viewport() const { return viewport_; }
    float horizontalScale() const { return horizontalScale_; }
    float canvasExtension() const { return canvasExtension_; }

    // HUD shapes remain their original size. Only corner groups translate;
    // centered prompts and aiming indicators retain their center position.
    float hudOffset(float minimumX, float maximumX) const {
        if (maximumX < canvasWidth_ * 0.35f) return -canvasExtension_;
        if (minimumX > canvasWidth_ * 0.65f) return canvasExtension_;
        return 0.0f;
    }

private:
    PixelRectangle viewport_ = {0, 0, 0, 0};
    int canvasWidth_ = 0;
    float horizontalScale_ = 1.0f;
    float canvasExtension_ = 0.0f;
};

} // namespace rendering
} // namespace spidey
