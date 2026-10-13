cmake_minimum_required(VERSION 3.28)

project(MorseCore C)

set(CMAKE_C_STANDARD 17)
set(CMAKE_C_STANDARD_REQUIRED ON)

include_directories(include)

add_executable(MorseCore
        src/main.c
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
        include/error_codes.h
        include/error.h
        include/error_messages.h
        include/validation.h
        include/stats.h
        include/settings.h
        include/separator.h
        include/language.h
)
set_target_properties(MorseCore PROPERTIES
        VS_DEBUGGER_WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
)
target_compile_definitions(MorseCore PRIVATE
        MORSECORE_ROOT_PATH="${CMAKE_CURRENT_SOURCE_DIR}/"
)

include(CTest)
if(BUILD_TESTING)
    add_executable(MorseCoreTests tests/core_tests.c
            src/morse.c src/separator.c src/file_utils.c src/validation.c
            src/stats.c src/ui.c src/language.c src/error.c)
    add_test(NAME core COMMAND MorseCoreTests)

    find_package(Python3 COMPONENTS Interpreter)
    if(Python3_Interpreter_FOUND)
        add_test(NAME cli COMMAND "${Python3_EXECUTABLE}"
                "${CMAKE_CURRENT_SOURCE_DIR}/tests/cli_tests.py"
                "$<TARGET_FILE:MorseCore>" "${CMAKE_CURRENT_SOURCE_DIR}")
    endif()
endif()
