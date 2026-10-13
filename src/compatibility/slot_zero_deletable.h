#pragma once

// Compatibility for recovered untyped objects whose deleting destructor is
// virtual slot zero. Use only where the original executable proves this layout.
struct SVTableSlot0Deletable {
    virtual ~SVTableSlot0Deletable() {}
};
