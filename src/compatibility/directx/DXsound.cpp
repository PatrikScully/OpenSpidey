// Original aggregate entry points retained for binary/layout validation.
// Input, sound and polygon implementation live in their engine systems.
#include "compatibility/directx/DXsound.h"
#include "compatibility/validate.h"

void validate_DXsound() {
    VALIDATE_SIZE(_GUID, 0x10);
}

void stateLog(char const*, ...) {}
