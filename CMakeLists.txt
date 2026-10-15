cmake_minimum_required(VERSION 3.28)

project(MorseCore VERSION 0.1.0 LANGUAGES C)

set(CMAKE_C_STANDARD 17)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_C_EXTENSIONS OFF)

include_directories(include)

add_executable(MorseCore
        src/main.c
        src/runtime_paths.c
        src/audio.c
        src/practice.c
        src/input.c
        src/cli.c
        src/console_features.c
        src/morse.c
        src/file_utils.c
        src/ui.c
        src/app.c
        src/error.c
        src/validation.c
        src/stats.c
        src/settings.c
        src/separator.c
        src/language.c
        include/morse.h
        include/file_utils.h
        include/ui.h
        include/app.h
        include/constants.h
        include/app_config.h
        include/runtime_paths.h
        include/error_codes.h
        include/error.h
        include/error_messages.h
        include/validation.h
        include/stats.h
        include/settings.h
        include/separator.h
        include/language.h
)
target_compile_definitions(MorseCore PRIVATE MORSECORE_VERSION="${PROJECT_VERSION}")
if(WIN32)
    target_link_libraries(MorseCore PRIVATE winmm shell32)
else()
    target_link_libraries(MorseCore PRIVATE m)
endif()
set_target_properties(MorseCore PROPERTIES
        VS_DEBUGGER_WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
)
if(MINGW)
    # Keep compiler runtimes inside the executable; Windows supplies UCRT/system DLLs.
    target_link_options(MorseCore PRIVATE -static -static-libgcc)
endif()

install(TARGETS MorseCore RUNTIME DESTINATION .)
install(FILES run.cmd README.md README.tr.md LICENSE NOTICE DESTINATION .)
install(DIRECTORY DESTINATION data)
file(MAKE_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/package-config")
file(WRITE "${CMAKE_CURRENT_BINARY_DIR}/package-config/settings.txt"
        "word_separator=/\nlanguage=EN\n")
install(FILES "${CMAKE_CURRENT_BINARY_DIR}/package-config/settings.txt" DESTINATION config)

set(CPACK_GENERATOR ZIP)
set(CPACK_PACKAGE_NAME MorseCore)
set(CPACK_PACKAGE_VERSION "${PROJECT_VERSION}")
set(CPACK_PACKAGE_VENDOR "Mustafa Mert Akceylan")
set(CPACK_PACKAGE_DESCRIPTION_SUMMARY "Text and Morse code, in both directions")
set(CPACK_PACKAGE_FILE_NAME "MorseCore-${PROJECT_VERSION}-${CMAKE_SYSTEM_NAME}-${CMAKE_SYSTEM_PROCESSOR}")
include(CPack)

include(CTest)
if(BUILD_TESTING)
    add_executable(MorseCoreTests tests/core_tests.c
            src/morse.c src/separator.c src/file_utils.c src/validation.c
            src/stats.c src/ui.c src/language.c src/error.c src/runtime_paths.c src/input.c)
    if(MINGW)
        target_link_options(MorseCoreTests PRIVATE -static -static-libgcc)
    endif()
    add_test(NAME core COMMAND MorseCoreTests)

    add_executable(MorseCoreAudioTests tests/audio_tests.c src/audio.c src/runtime_paths.c)
    if(WIN32)
        target_link_libraries(MorseCoreAudioTests PRIVATE winmm)
    else()
        target_link_libraries(MorseCoreAudioTests PRIVATE m)
    endif()
    add_test(NAME audio COMMAND MorseCoreAudioTests)

    add_executable(MorseCorePracticeTests tests/practice_tests.c src/practice.c)
    add_test(NAME practice COMMAND MorseCorePracticeTests)

    add_executable(MorseCoreDynamicTests tests/dynamic_tests.c
            src/morse.c src/separator.c src/file_utils.c src/input.c
            src/stats.c src/ui.c src/language.c src/error.c src/runtime_paths.c)
    add_test(NAME dynamic COMMAND MorseCoreDynamicTests)
    if(CMAKE_C_COMPILER_ID STREQUAL "GNU")
        target_compile_definitions(MorseCoreDynamicTests PRIVATE MORSECORE_TEST_ALLOC_FAILURE)
        target_link_options(MorseCoreDynamicTests PRIVATE "-Wl,--wrap=malloc,--wrap=realloc")
    endif()

    if(MINGW)
        foreach(test_target MorseCoreAudioTests MorseCorePracticeTests MorseCoreDynamicTests)
            target_link_options(${test_target} PRIVATE -static -static-libgcc)
        endforeach()
    endif()

    find_package(Python3 COMPONENTS Interpreter)
    if(Python3_Interpreter_FOUND)
        add_test(NAME cli COMMAND "${Python3_EXECUTABLE}"
                "${CMAKE_CURRENT_SOURCE_DIR}/tests/cli_tests.py"
                "$<TARGET_FILE:MorseCore>" "${CMAKE_CURRENT_SOURCE_DIR}")
        add_test(NAME portable COMMAND "${Python3_EXECUTABLE}"
                "${CMAKE_CURRENT_SOURCE_DIR}/tests/portable_tests.py"
                "$<TARGET_FILE:MorseCore>" "${CMAKE_CURRENT_SOURCE_DIR}"
                "${CMAKE_COMMAND}" "${CMAKE_CURRENT_BINARY_DIR}")
        add_test(NAME command COMMAND "${Python3_EXECUTABLE}"
                "${CMAKE_CURRENT_SOURCE_DIR}/tests/command_tests.py"
                "$<TARGET_FILE:MorseCore>")
        add_test(NAME console_features COMMAND "${Python3_EXECUTABLE}"
                "${CMAKE_CURRENT_SOURCE_DIR}/tests/console_features_tests.py"
                "$<TARGET_FILE:MorseCore>" "${CMAKE_CURRENT_SOURCE_DIR}")
    endif()
endif()
