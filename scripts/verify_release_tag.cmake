# Compare against CPack's configured project version, not a second version literal.
if(NOT DEFINED CPACK_CONFIG OR NOT EXISTS "${CPACK_CONFIG}")
    message(FATAL_ERROR "Pass -DCPACK_CONFIG=<build>/CPackConfig.cmake after configuring the project.")
endif()

if("$ENV{MORSECORE_RELEASE_TAG}" STREQUAL "")
    message(FATAL_ERROR "MORSECORE_RELEASE_TAG is required for release validation.")
endif()

include("${CPACK_CONFIG}")
if(NOT DEFINED CPACK_PACKAGE_VERSION OR "${CPACK_PACKAGE_VERSION}" STREQUAL "")
    message(FATAL_ERROR "The CPack project version is missing.")
endif()

if(NOT "$ENV{MORSECORE_RELEASE_TAG}" STREQUAL "v${CPACK_PACKAGE_VERSION}")
    message(FATAL_ERROR
        "Release tag must be v${CPACK_PACKAGE_VERSION}; got '$ENV{MORSECORE_RELEASE_TAG}'.")
endif()

message(STATUS "Release tag matches MorseCore ${CPACK_PACKAGE_VERSION}.")
