#pragma once

namespace spidey {
namespace application {

// Native application orchestration. Original entry points remain adapters
// for game behavior and executable-data compatibility.
class GameApplication {
public:
    int run(const char* gameDirectory);

private:
    bool loadGameData(const char* gameDirectory);
    void configureRenderDefaults();
};

} // namespace application
} // namespace spidey
