#include <locale.h>
#include <stdio.h>
#include <stdlib.h>
#ifdef _WIN32
#include <windows.h>
#include <shellapi.h>
#endif
#include "app.h"
#include "cli.h"
#include "runtime_paths.h"

#ifdef _WIN32
static void freeUtf8Arguments(int argc, char **argv)
{
    for (int i = 0; i < argc; i++) free(argv[i]);
    free(argv);
}

static char **utf8Arguments(int *argc)
{
    wchar_t **wide = CommandLineToArgvW(GetCommandLineW(), argc);
    if (wide == NULL) return NULL;
    char **arguments = calloc((size_t) *argc + 1, sizeof(*arguments));
    if (arguments == NULL) { LocalFree(wide); return NULL; }
    for (int i = 0; i < *argc; i++) {
        int length = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, wide[i], -1, NULL, 0, NULL, NULL);
        if (length <= 0 || (arguments[i] = malloc((size_t) length)) == NULL ||
            WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, wide[i], -1,
                                arguments[i], length, NULL, NULL) == 0) {
            freeUtf8Arguments(*argc, arguments);
            LocalFree(wide);
            return NULL;
        }
    }
    LocalFree(wide);
    return arguments;
}
#endif

int main(int argc, char *argv[])
{
    setlocale(LC_ALL, "");
#ifdef _WIN32
    SetConsoleOutputCP(CP_UTF8);
    SetConsoleCP(CP_UTF8);
    argv = utf8Arguments(&argc);
    if (argv == NULL) {
        fputs("MorseCore: cannot read command-line arguments as UTF-8.\n", stderr);
        return 1;
    }
#endif
    int result = 0;
    if (!initializeRuntimePaths(argv[0])) {
        fputs("MorseCore: cannot prepare data/config directories beside the executable.\n"
              "Use a writable folder or set MORSECORE_HOME to an existing writable directory.\n", stderr);
        result = 1;
    } else if (argc > 1) {
        result = runCommandLine(argc, argv);
    } else {
        runApplication();
    }
#ifdef _WIN32
    freeUtf8Arguments(argc, argv);
#endif
    return result;
}
