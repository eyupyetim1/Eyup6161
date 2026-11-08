#pragma once

#include <cstddef>

namespace bluecore {

inline void copyText(char* dst, size_t capacity, const char* src) {
    if (capacity == 0) {
        return;
    }
    size_t i = 0;
    if (src != nullptr) {
        while (src[i] != '\0' && (i + 1) < capacity) {
            dst[i] = src[i];
            ++i;
        }
    }
    dst[i] = '\0';
}

inline void appendText(char* dst, size_t capacity, size_t& used, const char* src) {
    if (capacity == 0) {
        return;
    }
    if (used >= capacity) {
        dst[capacity - 1] = '\0';
        return;
    }
    if (src == nullptr) {
        dst[used] = '\0';
        return;
    }
    while (*src != '\0' && (used + 1) < capacity) {
        dst[used] = *src;
        ++used;
        ++src;
    }
    dst[used] = '\0';
}

}  // namespace bluecore
