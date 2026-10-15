cmake_minimum_required(VERSION 3.28)
project(MorseCore VERSION 0.2.0 LANGUAGES C)

set(CMAKE_C_STANDARD 23)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_C_EXTENSIONS OFF)
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
if(MSVC OR CMAKE_C_SIMULATE_ID STREQUAL "MSVC")
    # Keep portable ISO C functions; Microsoft's *_s replacements are not ISO C.
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS)
endif()
include(CheckCSourceCompiles)
check_c_source_compiles("
#include <stdint.h>
#include <stdatomic.h>
#include <stdckdint.h>
#if !defined(__STDC_VERSION__) || __STDC_VERSION__ < 202311L
#error Final C23 mode is required
#endif
static_assert(sizeof(uint64_t) == 8);
static thread_local int value;
int main(void) { atomic_bool flag = false; unsigned n; value = 1; return ckd_add(&n, 1U, 2U) || atomic_load(&flag) || value != 1 || nullptr != nullptr; }
" MORSECORE_HAS_C23)
if(NOT MORSECORE_HAS_C23)
    message(FATAL_ERROR "A final C23 compiler with atomics, thread_local and stdckdint.h is required (GCC 15+/Clang 18+; feature probe is authoritative).")
endif()

add_library(MorseCoreCore STATIC
    src/morse.c src/stats.c src/practice.c src/separator.c src/audio.c src/platform_io.c)
target_include_directories(MorseCoreCore PUBLIC "${CMAKE_CURRENT_SOURCE_DIR}/include")
set_target_properties(MorseCoreCore PROPERTIES POSITION_INDEPENDENT_CODE ON C_VISIBILITY_PRESET hidden)
if(WIN32)
    target_link_libraries(MorseCoreCore PUBLIC winmm)
else()
    target_link_libraries(MorseCoreCore PUBLIC m)
endif()

add_library(MorseCoreNative SHARED src/native_api.c)
target_link_libraries(MorseCoreNative PRIVATE MorseCoreCore)
target_compile_definitions(MorseCoreNative PRIVATE MORSECORE_NATIVE_EXPORTS MORSECORE_VERSION="${PROJECT_VERSION}")
set_target_properties(MorseCoreNative PROPERTIES OUTPUT_NAME "MorseCore.Native" C_VISIBILITY_PRESET hidden)
if(WIN32)
    set_target_properties(MorseCoreNative PROPERTIES PREFIX "")
endif()

add_executable(MorseCore
    src/main.c src/runtime_paths.c src/input.c src/cli.c src/console_features.c
    src/file_utils.c src/ui.c src/console_render.c src/app.c src/error.c
    src/validation.c src/settings.c src/language.c)
target_link_libraries(MorseCore PRIVATE MorseCoreCore)
target_compile_definitions(MorseCore PRIVATE MORSECORE_VERSION="${PROJECT_VERSION}")
if(WIN32)
    target_link_libraries(MorseCore PRIVATE shell32)
endif()
set_target_properties(MorseCore PROPERTIES VS_DEBUGGER_WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}")
if(MINGW)
    foreach(native_target MorseCore MorseCoreNative)
        target_link_options(${native_target} PRIVATE -static -static-libgcc)
    endforeach()
endif()

install(TARGETS MorseCore RUNTIME DESTINATION .)
install(TARGETS MorseCoreNative RUNTIME DESTINATION . LIBRARY DESTINATION . ARCHIVE DESTINATION lib)
install(FILES include/morsecore_native.h DESTINATION include)
install(FILES run.cmd README.md README.tr.md LICENSE NOTICE DESTINATION .)
install(FILES docs/native-api.md docs/native-api.tr.md DESTINATION docs)
install(DIRECTORY DESTINATION data)
file(MAKE_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/package-config")
file(WRITE "${CMAKE_CURRENT_BINARY_DIR}/package-config/settings.txt" "word_separator=/\nlanguage=EN\n")
install(FILES "${CMAKE_CURRENT_BINARY_DIR}/package-config/settings.txt" DESTINATION config)
set(CPACK_GENERATOR ZIP)
set(CPACK_PACKAGE_NAME MorseCore)
set(CPACK_PACKAGE_VERSION "${PROJECT_VERSION}")
set(CPACK_PACKAGE_VENDOR "Mustafa Mert Akceylan")
set(CPACK_PACKAGE_DESCRIPTION_SUMMARY "Text and Morse code, in both directions")
set(CPACK_PACKAGE_FILE_NAME "MorseCore-${PROJECT_VERSION}-${CMAKE_SYSTEM_NAME}-${CMAKE_SYSTEM_PROCESSOR}")
include(CPack)

include(CTest)
option(MORSECORE_BUILD_DOTNET_BINDINGS "Build the headless .NET 10 interop library and smoke consumer" OFF)
if(MORSECORE_BUILD_DOTNET_BINDINGS)
    find_program(DOTNET_EXECUTABLE dotnet REQUIRED)
    set(MORSECORE_DOTNET_ENV
        "DOTNET_CLI_HOME=${CMAKE_CURRENT_BINARY_DIR}/dotnet-home"
        "NUGET_PACKAGES=${CMAKE_CURRENT_BINARY_DIR}/nuget"
        "DOTNET_CLI_TELEMETRY_OPTOUT=1" "DOTNET_NOLOGO=1"
        "DOTNET_GENERATE_ASPNET_CERTIFICATE=false" "DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false")
    add_custom_target(MorseCoreInterop ALL
        COMMAND "${CMAKE_COMMAND}" -E env ${MORSECORE_DOTNET_ENV}
            "${DOTNET_EXECUTABLE}" build "${CMAKE_CURRENT_SOURCE_DIR}/tests/dotnet/MorseCore.Interop.Smoke/MorseCore.Interop.Smoke.csproj"
            --configuration "$<IF:$<CONFIG:Debug>,Debug,Release>" --artifacts-path "${CMAKE_CURRENT_BINARY_DIR}/dotnet"
            -p:ArtifactsPivots=native --nologo --ignore-failed-sources
        WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/bindings/dotnet"
        DEPENDS MorseCoreNative VERBATIM)
    if(BUILD_TESTING)
        add_test(NAME dotnet_interop COMMAND "${CMAKE_COMMAND}" -E env ${MORSECORE_DOTNET_ENV}
            "${DOTNET_EXECUTABLE}" "${CMAKE_CURRENT_BINARY_DIR}/dotnet/bin/MorseCore.Interop.Smoke/native/MorseCore.Interop.Smoke.dll"
            "$<TARGET_FILE:MorseCoreNative>")
    endif()
endif()
if(BUILD_TESTING)
    add_executable(MorseCoreTests tests/core_tests.c src/file_utils.c src/validation.c src/runtime_paths.c)
    target_link_libraries(MorseCoreTests PRIVATE MorseCoreCore)
    add_test(NAME core COMMAND MorseCoreTests)

    add_executable(MorseCoreAudioTests tests/audio_tests.c)
    target_link_libraries(MorseCoreAudioTests PRIVATE MorseCoreCore)
    add_test(NAME audio COMMAND MorseCoreAudioTests)

    add_executable(MorseCorePracticeTests tests/practice_tests.c)
    target_link_libraries(MorseCorePracticeTests PRIVATE MorseCoreCore)
    add_test(NAME practice COMMAND MorseCorePracticeTests)

    add_executable(MorseCoreDynamicTests tests/dynamic_tests.c src/file_utils.c src/input.c src/runtime_paths.c)
    target_link_libraries(MorseCoreDynamicTests PRIVATE MorseCoreCore)
    add_test(NAME dynamic COMMAND MorseCoreDynamicTests)

    add_executable(MorseCoreNativeTests tests/native_api_tests.c)
    target_include_directories(MorseCoreNativeTests PRIVATE include)
    target_link_libraries(MorseCoreNativeTests PRIVATE MorseCoreNative)
    add_test(NAME native_c COMMAND MorseCoreNativeTests)

    if(CMAKE_C_COMPILER_ID STREQUAL "GNU")
        target_compile_definitions(MorseCoreDynamicTests PRIVATE MORSECORE_TEST_ALLOC_FAILURE)
        target_link_options(MorseCoreDynamicTests PRIVATE "-Wl,--wrap=malloc,--wrap=realloc")
        add_executable(MorseCoreNativeAllocTests tests/native_alloc_tests.c src/native_api.c)
        target_link_libraries(MorseCoreNativeAllocTests PRIVATE MorseCoreCore)
        target_compile_definitions(MorseCoreNativeAllocTests PRIVATE MORSECORE_NATIVE_EXPORTS MORSECORE_VERSION="${PROJECT_VERSION}")
        target_link_options(MorseCoreNativeAllocTests PRIVATE "-Wl,--wrap=malloc")
        add_test(NAME native_alloc COMMAND MorseCoreNativeAllocTests)
        if(MINGW)
            target_link_options(MorseCoreNativeAllocTests PRIVATE -static -static-libgcc)
        endif()
    endif()
    if(MINGW)
        foreach(test_target MorseCoreTests MorseCoreAudioTests MorseCorePracticeTests MorseCoreDynamicTests MorseCoreNativeTests)
            target_link_options(${test_target} PRIVATE -static -static-libgcc)
        endforeach()
    endif()

    find_package(Python3 COMPONENTS Interpreter)
    if(Python3_Interpreter_FOUND)
        add_test(NAME native_abi COMMAND "${Python3_EXECUTABLE}" "${CMAKE_CURRENT_SOURCE_DIR}/tests/native_abi_tests.py" "$<TARGET_FILE:MorseCoreNative>")
        add_test(NAME cli COMMAND "${Python3_EXECUTABLE}" "${CMAKE_CURRENT_SOURCE_DIR}/tests/cli_tests.py" "$<TARGET_FILE:MorseCore>" "${CMAKE_CURRENT_SOURCE_DIR}")
        add_test(NAME portable COMMAND "${Python3_EXECUTABLE}" "${CMAKE_CURRENT_SOURCE_DIR}/tests/portable_tests.py" "$<TARGET_FILE:MorseCore>" "${CMAKE_CURRENT_SOURCE_DIR}" "${CMAKE_COMMAND}" "${CMAKE_CURRENT_BINARY_DIR}")
        add_test(NAME command COMMAND "${Python3_EXECUTABLE}" "${CMAKE_CURRENT_SOURCE_DIR}/tests/command_tests.py" "$<TARGET_FILE:MorseCore>")
        add_test(NAME console_features COMMAND "${Python3_EXECUTABLE}" "${CMAKE_CURRENT_SOURCE_DIR}/tests/console_features_tests.py" "$<TARGET_FILE:MorseCore>" "${CMAKE_CURRENT_SOURCE_DIR}")
    endif()
endif()
