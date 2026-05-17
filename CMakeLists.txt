cmake_minimum_required(VERSION 3.28)

project(MorseCore C)

set(CMAKE_C_STANDARD 17)
set(CMAKE_C_STANDARD_REQUIRED ON)

include_directories(include)

add_executable(MorseCore
        src/main.c
        src/morse.c
        src/file_utils.c
        include/morse.h
        include/file_utils.h
)