#include "platform_io.h"
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#include <windows.h>
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

wchar_t *platformUtf8PathToWide(const char path[])
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

#endif

FILE *openUtf8File(const char path[], const char mode[])
{
#ifdef _WIN32
    wchar_t *widePath = platformUtf8PathToWide(path);
    wchar_t *wideMode = utf8ToWide(mode);
    FILE *file = widePath != NULL && wideMode != NULL ? _wfopen(widePath, wideMode) : NULL;
    free(wideMode);
    free(widePath);
    return file;
#else
    return fopen(path, mode);
#endif
}
