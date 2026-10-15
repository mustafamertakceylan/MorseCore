#ifndef RUNTIME_PATHS_H
#define RUNTIME_PATHS_H

#include <stddef.h>
#include "platform_io.h"

/* Paths are UTF-8. MORSECORE_HOME overrides the executable's directory. */
int initializeRuntimePaths(const char executablePath[]);
const char *runtimeHomePath(void);
const char *runtimeDataPath(void);
const char *runtimeSettingsPath(void);
int runtimeDataFilePath(char path[], size_t capacity, const char fileName[]);

#endif
