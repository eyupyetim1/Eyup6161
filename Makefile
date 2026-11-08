CXX ?= g++
CXXFLAGS := -std=c++17 -Wall -Wextra -Werror -Wno-unused-parameter -I. -Iinclude -Itools -DBLUECORE_HOST=1 -fno-exceptions -fno-rtti -MMD -MP
BUILD := build/host

SRC := $(shell find src -name '*.cpp' ! -name 'main.cpp')
OBJ := $(patsubst src/%.cpp,$(BUILD)/%.o,$(SRC))
TEST_SRC := $(wildcard test/host/*.cpp)
TEST_OBJ := $(patsubst test/host/%.cpp,$(BUILD)/test/%.o,$(TEST_SRC))
DEPS := $(OBJ:.o=.d) $(TEST_OBJ:.o=.d) $(BUILD)/framebuffer.d $(BUILD)/preview.d

.PHONY: test preview audit clean

test: audit $(BUILD)/tests
	$(BUILD)/tests

audit:
	python3 tools/audit_boundaries.py

$(BUILD)/tests: $(OBJ) $(TEST_OBJ) $(BUILD)/framebuffer.o
	$(CXX) $(CXXFLAGS) -o $@ $^

$(BUILD)/%.o: src/%.cpp
	@mkdir -p $(dir $@)
	$(CXX) $(CXXFLAGS) -c $< -o $@

$(BUILD)/test/%.o: test/host/%.cpp
	@mkdir -p $(dir $@)
	$(CXX) $(CXXFLAGS) -c $< -o $@

$(BUILD)/framebuffer.o: tools/framebuffer.cpp
	@mkdir -p $(dir $@)
	$(CXX) $(CXXFLAGS) -c $< -o $@

preview: $(OBJ) $(BUILD)/framebuffer.o tools/render_preview.cpp
	@mkdir -p build/preview docs/ui $(BUILD)
	$(CXX) $(CXXFLAGS) -o $(BUILD)/preview tools/render_preview.cpp $(OBJ) $(BUILD)/framebuffer.o
	$(BUILD)/preview build/preview
	python3 tools/ppm_to_png.py build/preview docs/ui

clean:
	rm -rf build

-include $(DEPS)
