#pragma once

// Compatibility umbrella for existing callers. New code includes only its
// engine contract rather than acquiring input, audio and renderer together.
#include "compatibility/legacy_log.h"
#include "engine/input/input_devices.h"
#include "engine/audio/sound_channels.h"
#include "engine/rendering/polygon_pipeline.h"

void validate_DXsound();
