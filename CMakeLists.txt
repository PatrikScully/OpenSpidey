cmake_minimum_required(VERSION 3.20)
project(OpenSpidey LANGUAGES NONE)

# The active runtime is C# / Godot. CMake is a convenience entry point only;
# it no longer configures the 32-bit executable, injected DLL or DirectX libraries.
find_package(Python3 3.12 REQUIRED COMPONENTS Interpreter)
add_custom_target(openspidey ALL
    COMMAND "${Python3_EXECUTABLE}" "${CMAKE_SOURCE_DIR}/tools/modern.py" build
    WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
    USES_TERMINAL)
enable_testing()
add_test(NAME modern-contracts
    COMMAND "${Python3_EXECUTABLE}" "${CMAKE_SOURCE_DIR}/tools/modern.py" test)
