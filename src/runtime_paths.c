#ifndef _WIN32
#define _POSIX_C_SOURCE 200809L
#endif

#ifdef _WIN32
#include <windows.h>
#else
#include <sys/stat.h>
#include <unistd.h>
#ifdef __APPLE__
#include <mach-o/dyld.h>
#endif
#endif

#include <errno.h>
#include <stdlib.h>
#include <string.h>
#include <wchar.h>

#include "constants.h"
#include "runtime_paths.h"

static char homePath[MAX_FILE_PATH_LENGTH];
static char dataPath[MAX_FILE_PATH_LENGTH];
static char settingsPath[MAX_FILE_PATH_LENGTH];

static int joinPath(char destination[], size_t capacity, const char directory[], const char name[])
{
    size_t length = strlen(directory);
    int hasSeparator = length > 0 && directory[length - 1] == '/';
#ifdef _WIN32
    hasSeparator = hasSeparator || (length > 0 && directory[length - 1] == '\\');
#endif
    const char *separator = hasSeparator ? "" : "/";
    int written = snprintf(destination, capacity, "%s%s%s", directory, separator, name);
    if (written < 0 || (size_t) written >= capacity) {
        if (capacity > 0) destination[0] = '\0';
        return 0;
    }
    return 1;
}

#ifdef _WIN32
static wchar_t *utf8ToWide(const char text[])
{
    int length = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, text, -1, NULL, 0);
    if (length == 0) return NULL;
    wchar_t *wide = malloc((size_t) length * sizeof(*wide));
    if (wide != NULL && MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, text, -1, wide, length) == 0) {
        free(wide);
        return NULL;
    }
    return wide;
}

static wchar_t *utf8PathToWide(const char path[])
{
    wchar_t *wide = utf8ToWide(path);
    if (wide == NULL || wcsncmp(wide, L"\\\\?\\", 4) == 0) return wide;
    for (wchar_t *cursor = wide; *cursor != L'\0'; cursor++) {
        if (*cursor == L'/') *cursor = L'\\';
    }
    DWORD capacity = GetFullPathNameW(wide, 0, NULL, NULL);
    if (capacity == 0 || capacity > 32760) {
        free(wide);
        return NULL;
    }
    wchar_t *absolute = malloc(((size_t) capacity + 8) * sizeof(*absolute));
    if (absolute == NULL) {
        free(wide);
        return NULL;
    }
    int unc = wide[0] == L'\\' && wide[1] == L'\\';
    size_t prefixLength = unc ? 8 : 4;
    DWORD length = GetFullPathNameW(wide, capacity, absolute + prefixLength, NULL);
    free(wide);
    if (length == 0 || length >= capacity) {
        free(absolute);
        return NULL;
    }
    if (unc) {
        memmove(absolute + 8, absolute + 10, (wcslen(absolute + 8) - 1) * sizeof(*absolute));
        wmemcpy(absolute, L"\\\\?\\UNC\\", 8);
    } else {
        wmemcpy(absolute, L"\\\\?\\", 4);
    }
    for (wchar_t *cursor = absolute; *cursor != L'\0'; cursor++) {
        if (*cursor == L'/') *cursor = L'\\';
    }
    return absolute;
}

static int findHomePath(const char executablePath[])
{
    (void) executablePath;
    wchar_t widePath[32768];
    wchar_t fullPath[32768];
    DWORD length = GetEnvironmentVariableW(L"MORSECORE_HOME", widePath, 32768);
    if (length >= 32768) return 0;
    if (length == 0) {
        length = GetModuleFileNameW(NULL, widePath, 32768);
        if (length == 0 || length >= 32768) return 0;
        wchar_t *lastSlash = wcsrchr(widePath, L'\\');
        if (lastSlash == NULL) return 0;
        lastSlash[1] = L'\0';
    }
    length = GetFullPathNameW(widePath, 32768, fullPath, NULL);
    if (length == 0 || length >= 32768) return 0;
    return WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, fullPath, -1,
                               homePath, sizeof(homePath) - MAX_FILE_NAME_LENGTH - 16, NULL, NULL) != 0;
}

static int createDirectory(const char path[])
{
    wchar_t *widePath = utf8PathToWide(path);
    if (widePath == NULL) return 0;
    int success = CreateDirectoryW(widePath, NULL) != 0;
    if (!success && GetLastError() == ERROR_ALREADY_EXISTS) {
        DWORD attributes = GetFileAttributesW(widePath);
        success = attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
    }
    free(widePath);
    return success;
}
#else
static int findHomePath(const char executablePath[])
{
    const char *override = getenv("MORSECORE_HOME");
    char executable[MAX_FILE_PATH_LENGTH];
    if (override != NULL && override[0] != '\0') {
        if (override[0] == '/') {
            if (strlen(override) >= sizeof(homePath)) return 0;
            strcpy(homePath, override);
        } else {
            char current[MAX_FILE_PATH_LENGTH];
            if (getcwd(current, sizeof(current)) == NULL || !joinPath(homePath, sizeof(homePath), current, override)) return 0;
        }
    } else {
#ifdef __APPLE__
        uint32_t capacity = sizeof(executable);
        if (_NSGetExecutablePath(executable, &capacity) != 0) return 0;
#else
        ssize_t length = readlink("/proc/self/exe", executable, sizeof(executable) - 1);
        if (length < 0 || (size_t) length >= sizeof(executable) - 1) {
            if (executablePath == NULL || executablePath[0] != '/' || strlen(executablePath) >= sizeof(executable)) return 0;
            strcpy(executable, executablePath);
        } else {
            executable[length] = '\0';
        }
#endif
        char *lastSlash = strrchr(executable, '/');
        if (lastSlash == NULL) return 0;
        lastSlash[1] = '\0';
        strcpy(homePath, executable);
    }
    return strlen(homePath) < sizeof(homePath) - MAX_FILE_NAME_LENGTH - 16;
}

static int createDirectory(const char path[])
{
    if (mkdir(path, 0755) == 0) return 1;
    struct stat status;
    return errno == EEXIST && stat(path, &status) == 0 && S_ISDIR(status.st_mode);
}
#endif

int initializeRuntimePaths(const char executablePath[])
{
    char configPath[MAX_FILE_PATH_LENGTH];
    if (!findHomePath(executablePath) ||
        !joinPath(dataPath, sizeof(dataPath), homePath, "data/") ||
        !joinPath(configPath, sizeof(configPath), homePath, "config") ||
        !joinPath(settingsPath, sizeof(settingsPath), configPath, "settings.txt") ||
        !createDirectory(dataPath) || !createDirectory(configPath)) {
        homePath[0] = dataPath[0] = settingsPath[0] = '\0';
        return 0;
    }
    return 1;
}

const char *runtimeHomePath(void) { return homePath; }
const char *runtimeDataPath(void) { return dataPath; }
const char *runtimeSettingsPath(void) { return settingsPath; }

int runtimeDataFilePath(char path[], size_t capacity, const char fileName[])
{
    if (dataPath[0] == '\0') {
        if (capacity > 0) path[0] = '\0';
        return 0;
    }
    return joinPath(path, capacity, dataPath, fileName);
}

FILE *openUtf8File(const char path[], const char mode[])
{
#ifdef _WIN32
    wchar_t *widePath = utf8PathToWide(path);
    wchar_t *wideMode = utf8ToWide(mode);
    FILE *file = widePath != NULL && wideMode != NULL ? _wfopen(widePath, wideMode) : NULL;
    free(wideMode);
    free(widePath);
    return file;
#else
    return fopen(path, mode);
#endif
}
