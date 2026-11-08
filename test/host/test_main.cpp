#include <cstdio>

#include "test/host/check.hpp"

int g_checksFailed = 0;

void runPolicyTests();
void runInputTests();
void runAppTests();
void runUiTests();

int main() {
    runPolicyTests();
    runInputTests();
    runAppTests();
    runUiTests();
    if (g_checksFailed != 0) {
        std::printf("%d kontrol basarisiz\n", g_checksFailed);
        return 1;
    }
    std::printf("tum kontroller gecti\n");
    return 0;
}
