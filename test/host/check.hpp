#pragma once

#include <cstdio>
#include <cstring>

extern int g_checksFailed;

inline void checkFail(const char* file, int line, const char* expr) {
    std::printf("FAIL %s:%d  %s\n", file, line, expr);
    ++g_checksFailed;
}

#define CHECK(expr)                         \
    do {                                    \
        if (!(expr)) {                      \
            checkFail(__FILE__, __LINE__, #expr); \
        }                                   \
    } while (0)

#define CHECK_STREQ(a, b)                                                      \
    do {                                                                       \
        const char* _a = (a);                                                  \
        const char* _b = (b);                                                  \
        if (std::strcmp(_a, _b) != 0) {                                        \
            std::printf("FAIL %s:%d  \"%s\" != \"%s\"\n", __FILE__, __LINE__, _a, _b); \
            ++g_checksFailed;                                                  \
        }                                                                      \
    } while (0)
