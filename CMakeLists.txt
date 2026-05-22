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