# Compare against the version emitted by the configured native project.
if(NOT DEFINED VERSION_FILE OR NOT EXISTS "${VERSION_FILE}")
    message(FATAL_ERROR "Pass -DVERSION_FILE=<build>/MorseCoreVersion.cmake after configuring the project.")
endif()

if("$ENV{MORSECORE_RELEASE_TAG}" STREQUAL "")
    message(FATAL_ERROR "MORSECORE_RELEASE_TAG is required for release validation.")
endif()

include("${VERSION_FILE}")
if(NOT DEFINED MORSECORE_PROJECT_VERSION OR "${MORSECORE_PROJECT_VERSION}" STREQUAL "")
    message(FATAL_ERROR "The configured project version is missing.")
endif()

if(NOT "$ENV{MORSECORE_RELEASE_TAG}" STREQUAL "v${MORSECORE_PROJECT_VERSION}")
    message(FATAL_ERROR
        "Release tag must be v${MORSECORE_PROJECT_VERSION}; got '$ENV{MORSECORE_RELEASE_TAG}'.")
endif()

message(STATUS "Release tag matches MorseCore ${MORSECORE_PROJECT_VERSION}.")
