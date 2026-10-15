#ifndef MORSECORE_PLATFORM_IO_H
#define MORSECORE_PLATFORM_IO_H
#include <stdio.h>
#ifdef _WIN32
#include <wchar.h>
/* Caller frees this internal platform path with free(). */
wchar_t *platformUtf8PathToWide(const char *path);
#endif
FILE *openUtf8File(const char *path, const char *mode);
#endif
