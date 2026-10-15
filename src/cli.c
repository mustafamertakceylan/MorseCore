#ifndef _WIN32
#define _POSIX_C_SOURCE 200809L
#define _XOPEN_SOURCE 700
#endif

#include <ctype.h>
#include <errno.h>
#include <limits.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#include <windows.h>
#include <fcntl.h>
#include <io.h>
#include <wchar.h>
#else
#include <dirent.h>
#include <fcntl.h>
#include <sys/stat.h>
#include <unistd.h>
#endif

#include "audio.h"
#include "cli.h"
#include "error_codes.h"
#include "file_utils.h"
#include "morse.h"
#include "separator.h"

#ifndef MORSECORE_VERSION
#define MORSECORE_VERSION "development"
#endif

typedef struct {
    int encode;
    int decode;
    int overwrite;
    int play;
    int customAudio;
    const char *text;
    const char *input;
    const char *output;
    const char *batch;
    const char *outputDirectory;
    const char *wav;
    char separator;
    AudioOptions audio;
} CommandOptions;

typedef struct {
    char **items;
    size_t count;
} FileNames;

static void showHelp(void)
{
    puts("MorseCore " MORSECORE_VERSION " - text and Morse conversion\n"
         "Usage:\n"
         "  MorseCore                         Open the interactive menu\n"
         "  MorseCore --encode [TEXT] [options]\n"
         "  MorseCore --decode [MORSE] [options]\n"
         "  MorseCore --batch DIR --encode|--decode --output-dir DIR [--overwrite]\n\n"
         "Input: TEXT/MORSE, --input FILE, or all standard input until EOF.\n"
         "  --input -             Read standard input explicitly\n"
         "  --output FILE         Write UTF-8 output (otherwise print only the result)\n"
         "  --separator CHAR      Word separator: / (default), |, #, or *\n"
         "  --wav FILE            Save encoded Morse as PCM WAV\n"
         "  --play                Play encoded Morse (Windows)\n"
         "  --wpm N               Audio speed: 5-60 words/minute (default 20)\n"
         "  --frequency N         Audio tone: 200-2000 Hz (default 600)\n"
         "  --overwrite           Explicitly replace existing output files\n"
         "  --help                Show this help\n"
         "  --version             Show the version\n\n"
         "Batch reads regular .txt files only, without recursion, in name order.\n"
         "Outputs are NAME.morse.txt or NAME.text.txt; existing files are protected.\n"
         "Audio options apply to a single --encode operation. Paths may contain spaces.\n"
         "Exit codes: 0 success; 1 conversion/file/audio error; 2 invalid arguments.");
}

static int usageError(const char *message)
{
    fprintf(stderr, "MorseCore: %s\nUse --help for usage.\n", message);
    return 2;
}

static int parseInteger(const char *text, int *value)
{
    char *end;
    errno = 0;
    long parsed = strtol(text, &end, 10);
    if (text[0] == '\0' || end == text || *end != '\0' || errno != 0 ||
        parsed < INT_MIN || parsed > INT_MAX) return 0;
    *value = (int) parsed;
    return 1;
}

static int looksLikeMorse(const char *text)
{
    int symbol = 0;
    for (const unsigned char *cursor = (const unsigned char *) text; *cursor; cursor++) {
        if (*cursor == '.' || *cursor == '-') symbol = 1;
        else if (!isspace(*cursor) && !isValidSeparator((char) *cursor)) return 0;
    }
    return symbol;
}

static int parseOptions(int argc, char *argv[], CommandOptions *options)
{
    memset(options, 0, sizeof(*options));
    options->separator = '/';
    options->audio = defaultAudioOptions();
    for (int i = 1; i < argc; i++) {
        const char *argument = argv[i];
        if (strcmp(argument, "--encode") == 0) {
            if (options->encode) return usageError("--encode was specified more than once.");
            options->encode = 1;
        } else if (strcmp(argument, "--decode") == 0) {
            if (options->decode) return usageError("--decode was specified more than once.");
            options->decode = 1;
        } else if (strcmp(argument, "--overwrite") == 0) {
            options->overwrite = 1;
        } else if (strcmp(argument, "--play") == 0) {
            options->play = 1;
        } else if (strcmp(argument, "--") == 0) {
            if (options->decode && options->text == NULL &&
                (i + 1 == argc || (strncmp(argv[i + 1], "--", 2) == 0 && !looksLikeMorse(argv[i + 1])))) {
                options->text = argument;
            } else {
                if (options->text != NULL || i + 2 != argc)
                return usageError("Exactly one text argument is allowed after --.");
                options->text = argv[++i];
            }
        } else if (strcmp(argument, "--input") == 0 || strcmp(argument, "--output") == 0 ||
                   strcmp(argument, "--batch") == 0 || strcmp(argument, "--output-dir") == 0 ||
                   strcmp(argument, "--wav") == 0 || strcmp(argument, "--separator") == 0 ||
                   strcmp(argument, "--wpm") == 0 || strcmp(argument, "--frequency") == 0) {
            if (++i >= argc || argv[i][0] == '\0' || strncmp(argv[i], "--", 2) == 0)
                return usageError("An option is missing its value.");
            const char *value = argv[i];
            const char **path = NULL;
            if (strcmp(argument, "--input") == 0) path = &options->input;
            else if (strcmp(argument, "--output") == 0) path = &options->output;
            else if (strcmp(argument, "--batch") == 0) path = &options->batch;
            else if (strcmp(argument, "--output-dir") == 0) path = &options->outputDirectory;
            else if (strcmp(argument, "--wav") == 0) path = &options->wav;
            if (path != NULL) {
                if (*path != NULL) return usageError("An input/output option was specified more than once.");
                *path = value;
            } else if (strcmp(argument, "--separator") == 0) {
                if (strlen(value) != 1 || !isValidSeparator(value[0]))
                    return usageError("--separator must be one of / | # *.");
                options->separator = value[0];
            } else {
                int *number = strcmp(argument, "--wpm") == 0 ?
                              &options->audio.wordsPerMinute : &options->audio.frequencyHz;
                if (!parseInteger(value, number)) return usageError("Audio values must be whole numbers.");
                options->customAudio = 1;
            }
        } else if (strncmp(argument, "--", 2) == 0) {
            if (options->text == NULL && looksLikeMorse(argument)) {
                options->text = argument;
                continue;
            }
            fprintf(stderr, "MorseCore: unknown option '%s'.\nUse --help for usage.\n", argument);
            return 2;
        } else {
            if (options->text != NULL) return usageError("Use one quoted text argument, --input, or standard input.");
            options->text = argument;
        }
    }
    if (options->encode == options->decode) return usageError("Choose exactly one of --encode or --decode.");
    if (options->input != NULL && options->text != NULL)
        return usageError("Use either a text argument or --input.");
    if (options->batch != NULL) {
        if (options->outputDirectory == NULL)
            return usageError("--batch requires --output-dir.");
        if (options->input != NULL || options->text != NULL || options->output != NULL ||
            options->wav != NULL || options->play || options->customAudio)
            return usageError("Batch accepts --encode/--decode, --separator, --output-dir, and --overwrite only.");
    } else if (options->outputDirectory != NULL) {
        return usageError("--output-dir requires --batch.");
    }
    if ((options->wav != NULL || options->play) && !options->encode)
        return usageError("--wav and --play require --encode.");
    if (options->customAudio && options->wav == NULL && !options->play)
        return usageError("--wpm and --frequency require --wav or --play.");
    if (!validateAudioOptions(&options->audio)) return usageError(audioLastError());
    return 0;
}

static char *joinPath(const char *directory, const char *name)
{
    size_t a = strlen(directory), b = strlen(name);
    if (a > SIZE_MAX - b - 2) return NULL;
    char *result = malloc(a + b + 2);
    if (result != NULL) {
        memcpy(result, directory, a);
        int hasSeparator = a != 0 && directory[a - 1] == '/';
#ifdef _WIN32
        hasSeparator = hasSeparator || (a != 0 && directory[a - 1] == '\\');
#endif
        if (a != 0 && !hasSeparator) result[a++] = '/';
        memcpy(result + a, name, b + 1);
    }
    return result;
}

#ifdef _WIN32
/* Extended paths keep Unicode and long names intact in Win32 file operations. */
static wchar_t *widePath(const char *path)
{
    int length = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, path, -1, NULL, 0);
    if (length <= 0) return NULL;
    wchar_t *wide = malloc((size_t) length * sizeof(*wide));
    if (wide == NULL) return NULL;
    if (MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, path, -1, wide, length) == 0) {
        free(wide);
        return NULL;
    }
    for (wchar_t *p = wide; *p; p++) if (*p == L'/') *p = L'\\';
    if (wcsncmp(wide, L"\\\\?\\", 4) == 0) return wide;
    DWORD capacity = GetFullPathNameW(wide, 0, NULL, NULL);
    if (capacity == 0 || capacity > 32760) { free(wide); return NULL; }
    int unc = wide[0] == L'\\' && wide[1] == L'\\';
    size_t prefix = unc ? 8 : 4;
    wchar_t *absolute = malloc(((size_t) capacity + 8) * sizeof(*absolute));
    if (absolute == NULL) { free(wide); return NULL; }
    DWORD written = GetFullPathNameW(wide, capacity, absolute + prefix, NULL);
    free(wide);
    if (written == 0 || written >= capacity) { free(absolute); return NULL; }
    if (unc) {
        memmove(absolute + 8, absolute + 10, (wcslen(absolute + 8) - 1) * sizeof(*absolute));
        wmemcpy(absolute, L"\\\\?\\UNC\\", 8);
    } else wmemcpy(absolute, L"\\\\?\\", 4);
    return absolute;
}

static char *utf8Name(const wchar_t *wide)
{
    int length = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, wide, -1, NULL, 0, NULL, NULL);
    if (length <= 0) return NULL;
    char *result = malloc((size_t) length);
    if (result != NULL && WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, wide, -1,
                                            result, length, NULL, NULL) == 0) {
        free(result);
        result = NULL;
    }
    return result;
}

static int sameFile(const char *first, const char *second)
{
    wchar_t *a = widePath(first), *b = widePath(second);
    HANDLE ha = a == NULL ? INVALID_HANDLE_VALUE : CreateFileW(a, 0,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, NULL, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, NULL);
    HANDLE hb = b == NULL ? INVALID_HANDLE_VALUE : CreateFileW(b, 0,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, NULL, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, NULL);
    BY_HANDLE_FILE_INFORMATION ia, ib;
    int same = ha != INVALID_HANDLE_VALUE && hb != INVALID_HANDLE_VALUE &&
               GetFileInformationByHandle(ha, &ia) && GetFileInformationByHandle(hb, &ib) &&
               ia.dwVolumeSerialNumber == ib.dwVolumeSerialNumber &&
               ia.nFileIndexHigh == ib.nFileIndexHigh && ia.nFileIndexLow == ib.nFileIndexLow;
    if (ha != INVALID_HANDLE_VALUE) CloseHandle(ha);
    if (hb != INVALID_HANDLE_VALUE) CloseHandle(hb);
    free(a); free(b);
    return same;
}

static int sameDestination(const char *first, const char *second)
{
    if (sameFile(first, second)) return 1;
    wchar_t *a = widePath(first), *b = widePath(second);
    int same = a != NULL && b != NULL && CompareStringOrdinal(a, -1, b, -1, TRUE) == CSTR_EQUAL;
    free(a); free(b);
    return same;
}

static int usableDirectory(const char *path, int create)
{
    wchar_t *wide = widePath(path);
    if (wide == NULL) return 0;
    if (create) CreateDirectoryW(wide, NULL);
    DWORD attributes = GetFileAttributesW(wide);
    free(wide);
    return attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY) &&
           !(attributes & FILE_ATTRIBUTE_REPARSE_POINT);
}

static int outputAvailable(const char *path, int overwrite)
{
    wchar_t *wide = widePath(path);
    if (wide == NULL) return 0;
    DWORD attributes = GetFileAttributesW(wide), error = GetLastError();
    free(wide);
    if (attributes == INVALID_FILE_ATTRIBUTES)
        return error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND;
    return overwrite && !(attributes & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT));
}

static FILE *exclusiveFile(const char *path)
{
    wchar_t *wide = widePath(path);
    HANDLE handle = wide == NULL ? INVALID_HANDLE_VALUE : CreateFileW(wide, GENERIC_WRITE, 0, NULL,
                                                                     CREATE_NEW, FILE_ATTRIBUTE_NORMAL, NULL);
    free(wide);
    if (handle == INVALID_HANDLE_VALUE) return NULL;
    int fd = _open_osfhandle((intptr_t) handle, _O_WRONLY | _O_BINARY);
    if (fd == -1) { CloseHandle(handle); return NULL; }
    FILE *file = _fdopen(fd, "wb");
    if (file == NULL) _close(fd);
    return file;
}

static void removePath(const char *path)
{
    wchar_t *wide = widePath(path);
    if (wide != NULL) DeleteFileW(wide);
    free(wide);
}

static int publishOutput(const char *temporary, const char *destination, int overwrite)
{
    wchar_t *a = widePath(temporary), *b = widePath(destination);
    int result = a != NULL && b != NULL &&
                 MoveFileExW(a, b, overwrite ? MOVEFILE_REPLACE_EXISTING : 0);
    free(a); free(b);
    return result;
}
#else
static int sameFile(const char *first, const char *second)
{
    struct stat a, b;
    return stat(first, &a) == 0 && stat(second, &b) == 0 && a.st_dev == b.st_dev && a.st_ino == b.st_ino;
}

static char *canonicalDestination(const char *path)
{
    const char *slash = strrchr(path, '/');
    size_t parentLength = slash == NULL ? 1 : slash == path ? 1 : (size_t) (slash - path);
    char *parent = malloc(parentLength + 1);
    if (parent == NULL) return NULL;
    if (slash == NULL) parent[0] = '.';
    else memcpy(parent, path, parentLength);
    parent[parentLength] = '\0';
    char *absolute = realpath(parent, NULL);
    free(parent);
    if (absolute == NULL) return NULL;
    char *result = joinPath(absolute, slash == NULL ? path : slash + 1);
    free(absolute);
    return result;
}

static int sameDestination(const char *first, const char *second)
{
    if (sameFile(first, second)) return 1;
    char *a = canonicalDestination(first), *b = canonicalDestination(second);
    int same = a != NULL && b != NULL && strcmp(a, b) == 0;
    free(a); free(b);
    return same;
}

static int usableDirectory(const char *path, int create)
{
    struct stat information;
    if (create) mkdir(path, 0755);
    return lstat(path, &information) == 0 && S_ISDIR(information.st_mode);
}

static int outputAvailable(const char *path, int overwrite)
{
    struct stat information;
    if (lstat(path, &information) != 0) return errno == ENOENT;
    return overwrite && S_ISREG(information.st_mode);
}

static FILE *exclusiveFile(const char *path)
{
    int fd = open(path, O_WRONLY | O_CREAT | O_EXCL, 0666);
    if (fd < 0) return NULL;
    FILE *file = fdopen(fd, "wb");
    if (file == NULL) close(fd);
    return file;
}

static void removePath(const char *path) { unlink(path); }

static int publishOutput(const char *temporary, const char *destination, int overwrite)
{
    if (overwrite) return rename(temporary, destination) == 0;
    if (link(temporary, destination) != 0) return 0;
    unlink(temporary);
    return 1;
}
#endif

static FILE *temporaryOutput(const char *destination, char **path)
{
    static unsigned counter;
    size_t length = strlen(destination);
    if (length > SIZE_MAX - 80) return NULL;
    *path = malloc(length + 80);
    if (*path == NULL) return NULL;
#ifdef _WIN32
    unsigned long process = GetCurrentProcessId();
#else
    unsigned long process = (unsigned long) getpid();
#endif
    for (unsigned i = 0; i < 128; i++) {
        snprintf(*path, length + 80, "%s.tmp-%lu-%u", destination, process, ++counter);
        FILE *file = exclusiveFile(*path);
        if (file != NULL) return file;
    }
    free(*path);
    *path = NULL;
    return NULL;
}

static int writeOutput(const char *path, const char *text, const CommandOptions *options, int wav)
{
    if (!outputAvailable(path, options->overwrite)) {
        fprintf(stderr, "MorseCore: output '%s' already exists or is not a regular file; use --overwrite to replace a regular file.\n", path);
        return 0;
    }
    char *temporary = NULL;
    FILE *file = temporaryOutput(path, &temporary);
    if (file == NULL) {
        fprintf(stderr, "MorseCore: cannot create output '%s'.\n", path);
        return 0;
    }
    int success;
    if (wav) {
        success = fclose(file) == 0;
        if (success) success = writeMorseWav(temporary, text, options->separator, &options->audio);
        if (!success) fprintf(stderr, "MorseCore: WAV '%s': %s\n", path, audioLastError());
    } else {
        size_t length = strlen(text);
        success = fwrite(text, 1, length, file) == length;
        if (fclose(file) != 0) success = 0;
        if (!success) fprintf(stderr, "MorseCore: cannot write output '%s'.\n", path);
    }
    if (success && !publishOutput(temporary, path, options->overwrite)) {
        fprintf(stderr, "MorseCore: cannot publish output '%s' (it may already exist).\n", path);
        success = 0;
    }
    if (!success) removePath(temporary);
    free(temporary);
    return success;
}

static const char *conversionError(ResultCode code)
{
    switch (code) {
        case RESULT_EMPTY_INPUT: return "input is empty";
        case RESULT_INVALID_INPUT: return "input is not valid text";
        case RESULT_UNSUPPORTED_CHARACTER: return "input contains an unsupported character";
        case RESULT_INVALID_MORSE: return "input contains invalid Morse code";
        case RESULT_INVALID_SEPARATOR: return "invalid word separator";
        case RESULT_FILE_OPEN_ERROR: return "cannot read the input file";
        case RESULT_FILE_READ_ERROR: return "cannot read the input stream";
        case RESULT_INPUT_READ_ERROR: return "cannot read the input stream";
        case RESULT_OUT_OF_MEMORY: return "not enough memory";
        case RESULT_INPUT_TOO_LARGE: return "input is too large";
        default: return "conversion failed (allocation or input error)";
    }
}

static ResultCode convert(const char *input, char **output, const CommandOptions *options)
{
    ResultCode code = options->encode ? textToMorseAlloc(input, output, options->separator) :
        morseToTextAlloc(input, output, resolveSeparatorForMorseInput(input, options->separator));
    if (code == RESULT_SUCCESS && options->encode) {
        size_t length = strlen(*output);
        if (length > 0 && (*output)[length - 1] == ' ') (*output)[length - 1] = '\0';
    }
    return code;
}

static ResultCode readStandardInput(char **output)
{
    *output = NULL;
#ifdef _WIN32
    if (_setmode(_fileno(stdin), _O_BINARY) == -1) return RESULT_INPUT_READ_ERROR;
#endif
    return readTextStreamAlloc(stdin, output);
}

static int processSingle(const CommandOptions *options)
{
    const char *fileInput = options->input != NULL && strcmp(options->input, "-") != 0 ? options->input : NULL;
    if ((fileInput != NULL && options->output != NULL && sameFile(fileInput, options->output)) ||
        (fileInput != NULL && options->wav != NULL && sameFile(fileInput, options->wav)) ||
        (options->output != NULL && options->wav != NULL &&
         sameDestination(options->output, options->wav))) {
        fputs("MorseCore: input, text output, and WAV output must be different files.\n", stderr);
        return 1;
    }
    char *ownedInput = NULL, *output = NULL;
    const char *input = options->text;
    ResultCode code = RESULT_SUCCESS;
    if (input == NULL) {
        code = fileInput != NULL ? readTextFileAlloc(fileInput, &ownedInput) : readStandardInput(&ownedInput);
        input = ownedInput;
    }
    if (code == RESULT_SUCCESS) code = convert(input, &output, options);
    free(ownedInput);
    if (code != RESULT_SUCCESS) {
        fprintf(stderr, "MorseCore: %s (code %d).\n", conversionError(code), code);
        free(output);
        return 1;
    }
    int success = 1;
    if (options->wav != NULL || options->play) {
        AudioAnalysis analysis;
        success = analyzeMorseAudio(output, options->separator, &options->audio, &analysis);
        if (!success) fprintf(stderr, "MorseCore: %s\n", audioLastError());
    }
    if (success && options->output != NULL) success = writeOutput(options->output, output, options, 0);
    if (success && options->wav != NULL) success = writeOutput(options->wav, output, options, 1);
    if (success && options->play) {
        success = playMorseAudio(output, options->separator, &options->audio);
        if (!success) fprintf(stderr, "MorseCore: %s\n", audioLastError());
    }
    if (success && options->output == NULL) {
        if (fputs(output, stdout) == EOF || putchar('\n') == EOF || fflush(stdout) != 0) {
            fputs("MorseCore: cannot write standard output.\n", stderr);
            success = 0;
        }
    }
    free(output);
    return success ? 0 : 1;
}

static int isTextFile(const char *name)
{
    size_t length = strlen(name);
    return length > 4 && name[length - 4] == '.' &&
           tolower((unsigned char) name[length - 3]) == 't' &&
           tolower((unsigned char) name[length - 2]) == 'x' &&
           tolower((unsigned char) name[length - 1]) == 't';
}

static int addName(FileNames *names, char *name)
{
    if (names->count >= SIZE_MAX / sizeof(*names->items) - 1) return 0;
    char **grown = realloc(names->items, (names->count + 1) * sizeof(*names->items));
    if (grown == NULL) return 0;
    names->items = grown;
    names->items[names->count++] = name;
    return 1;
}

static int nameOrder(const void *a, const void *b)
{
    return strcmp(*(const char *const *) a, *(const char *const *) b);
}

static int collectNames(const char *directory, FileNames *names)
{
    int success = 1;
#ifdef _WIN32
    char *pattern = joinPath(directory, "*");
    wchar_t *wide = pattern == NULL ? NULL : widePath(pattern);
    free(pattern);
    if (wide == NULL) return 0;
    WIN32_FIND_DATAW entry;
    HANDLE search = FindFirstFileW(wide, &entry);
    free(wide);
    if (search == INVALID_HANDLE_VALUE) return GetLastError() == ERROR_FILE_NOT_FOUND;
    do {
        if (entry.dwFileAttributes & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT)) continue;
        char *name = utf8Name(entry.cFileName);
        if (name == NULL) { success = 0; break; }
        if (isTextFile(name)) {
            if (!addName(names, name)) { free(name); success = 0; break; }
        } else free(name);
    } while (FindNextFileW(search, &entry));
    if (success && GetLastError() != ERROR_NO_MORE_FILES) success = 0;
    FindClose(search);
#else
    DIR *search = opendir(directory);
    if (search == NULL) return 0;
    struct dirent *entry;
    for (;;) {
        errno = 0;
        entry = readdir(search);
        if (entry == NULL) { if (errno != 0) success = 0; break; }
        if (!isTextFile(entry->d_name)) continue;
        char *path = joinPath(directory, entry->d_name);
        struct stat information;
        if (path == NULL) { success = 0; break; }
        int regular = lstat(path, &information) == 0 && S_ISREG(information.st_mode);
        free(path);
        if (!regular) continue;
        size_t length = strlen(entry->d_name) + 1;
        char *name = malloc(length);
        if (name == NULL) { success = 0; break; }
        memcpy(name, entry->d_name, length);
        if (!addName(names, name)) { free(name); success = 0; break; }
    }
    closedir(search);
#endif
    if (success && names->count > 1) qsort(names->items, names->count, sizeof(*names->items), nameOrder);
    return success;
}

static int processBatch(const CommandOptions *options)
{
    if (!usableDirectory(options->batch, 0) || !usableDirectory(options->outputDirectory, 1)) {
        fputs("MorseCore: batch input/output must be accessible directories, without symbolic links/reparse points; output parent must exist.\n", stderr);
        return 1;
    }
    if (sameFile(options->batch, options->outputDirectory)) {
        fputs("MorseCore: batch input and output directories must be different.\n", stderr);
        return 1;
    }
    FileNames names = {NULL, 0};
    if (!collectNames(options->batch, &names)) {
        fputs("MorseCore: cannot enumerate batch input files.\n", stderr);
        for (size_t i = 0; i < names.count; i++) free(names.items[i]);
        free(names.items);
        return 1;
    }
    size_t passed = 0, failed = 0;
    for (size_t i = 0; i < names.count; i++) {
        char *inputPath = joinPath(options->batch, names.items[i]);
        size_t stem = strlen(names.items[i]) - 4;
        const char *suffix = options->encode ? ".morse.txt" : ".text.txt";
        char *outputName = malloc(stem + strlen(suffix) + 1);
        if (outputName != NULL) {
            memcpy(outputName, names.items[i], stem);
            strcpy(outputName + stem, suffix);
        }
        char *outputPath = outputName == NULL ? NULL : joinPath(options->outputDirectory, outputName);
        char *input = NULL, *output = NULL;
        ResultCode code = RESULT_FAILURE;
        if (inputPath != NULL && outputPath != NULL && !sameFile(inputPath, outputPath)) {
            code = readTextFileAlloc(inputPath, &input);
            if (code == RESULT_SUCCESS) code = convert(input, &output, options);
        }
        if (code == RESULT_SUCCESS && writeOutput(outputPath, output, options, 0)) passed++;
        else {
            failed++;
            if (code != RESULT_SUCCESS)
                fprintf(stderr, "MorseCore: '%s': %s (code %d).\n", names.items[i], conversionError(code), code);
        }
        free(inputPath); free(outputName); free(outputPath); free(input); free(output);
        free(names.items[i]);
    }
    free(names.items);
    fprintf(stderr, "Batch complete: %zu succeeded, %zu failed.\n", passed, failed);
    return failed == 0 ? 0 : 1;
}

int runCommandLine(int argc, char *argv[])
{
    if (argc == 2 && strcmp(argv[1], "--help") == 0) { showHelp(); return 0; }
    if (argc == 2 && strcmp(argv[1], "--version") == 0) { puts("MorseCore " MORSECORE_VERSION); return 0; }
    CommandOptions options;
    int result = parseOptions(argc, argv, &options);
    if (result != 0) return result;
    return options.batch != NULL ? processBatch(&options) : processSingle(&options);
}
