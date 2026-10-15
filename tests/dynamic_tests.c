#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>

#include "constants.h"
#include "file_utils.h"
#include "input.h"
#include "morse.h"
#include "stats.h"

#define CHECK(condition) do { \
    if (!(condition)) { \
        fprintf(stderr, "FAIL line %d: %s\n", __LINE__, #condition); \
        return 1; \
    } \
} while (0)

/* GNU/MinGW builds can additionally exercise the real allocation-error paths
 * with -DMORSECORE_TEST_ALLOC_FAILURE -Wl,--wrap=malloc,--wrap=realloc. */
#ifdef MORSECORE_TEST_ALLOC_FAILURE
static int failAllocation;
static int failReallocation;
void *__real_malloc(size_t size);
void *__real_realloc(void *pointer, size_t size);
void *__wrap_malloc(size_t size)
{
    return failAllocation ? NULL : __real_malloc(size);
}
void *__wrap_realloc(void *pointer, size_t size)
{
    return failAllocation || failReallocation ? NULL : __real_realloc(pointer, size);
}
#endif

static int testPunctuation(void)
{
    const char punctuation[] = ".,?'/():=+-\"@!&;_$";
    const char *codes[] = {
        ".-.-.-", "--..--", "..--..", ".----.", "-..-.", "-.--.", "-.--.-",
        "---...", "-...-", ".-.-.", "-....-", ".-..-.", ".--.-.",
        "-.-.--", ".-...", "-.-.-.", "..--.-", "...-..-"
    };
    char *encoded = NULL, *decoded = NULL;
    for (size_t i = 0; i < sizeof(punctuation) - 1; i++) {
        char input[] = {punctuation[i], '\0'};
        CHECK(textToMorseAlloc(input, &encoded, '/') == RESULT_SUCCESS);
        CHECK(strncmp(encoded, codes[i], strlen(codes[i])) == 0);
        CHECK(strlen(encoded) == strlen(codes[i]) + 1);
        CHECK(morseToTextAlloc(encoded, &decoded, '/') == RESULT_SUCCESS);
        CHECK(strcmp(decoded, input) == 0);
        free(encoded); free(decoded);
    }
    for (size_t i = 0; i < 4; i++) {
        char separator = "/|#*"[i];
        CHECK(textToMorseAlloc("A/B / C", &encoded, separator) == RESULT_SUCCESS);
        CHECK(morseToTextAlloc(encoded, &decoded, separator) == RESULT_SUCCESS);
        CHECK(strcmp(decoded, "A/B / C") == 0);
        free(encoded); free(decoded);
    }
    CHECK(textToMorseAlloc(punctuation, &encoded, '/') == RESULT_SUCCESS);
    CHECK(morseToTextAlloc(encoded, &decoded, '/') == RESULT_SUCCESS);
    CHECK(strcmp(decoded, punctuation) == 0);
    ConversionStats stats;
    calculateConversionStats(punctuation, encoded, CONVERSION_TEXT_TO_MORSE, &stats);
    CHECK(stats.convertedCharacterCount == 18 && stats.wordCount == 1);
    free(encoded); free(decoded);
    return 0;
}

static int testEncodingAndFailure(void)
{
    char *output = (char *) (uintptr_t) 1;
    CHECK(textToMorseAlloc(NULL, &output, '/') == RESULT_INVALID_INPUT && output == NULL);
    CHECK(textToMorseAlloc("A", NULL, '/') == RESULT_INVALID_INPUT);
    CHECK(textToMorseAlloc("A", &output, '.') == RESULT_INVALID_SEPARATOR && output == NULL);
    CHECK(morseToTextAlloc(NULL, &output, '/') == RESULT_INVALID_INPUT && output == NULL);
    CHECK(morseToTextAlloc(".-", NULL, '/') == RESULT_INVALID_INPUT);
    CHECK(morseToTextAlloc(".-", &output, '.') == RESULT_INVALID_SEPARATOR && output == NULL);
    CHECK(textToMorseAlloc(" \r\n\t\v\f", &output, '/') == RESULT_EMPTY_INPUT && output == NULL);
    CHECK(textToMorseAlloc("\xEF\xBB\xBF", &output, '/') == RESULT_EMPTY_INPUT && output == NULL);
    CHECK(textToMorseAlloc("\xEF\xBB\xBF\xDD", &output, '/') == RESULT_INVALID_INPUT && output == NULL);
    const char *malformed[] = {"\xC3", "\xC4\x41", "\xC0\xAF", "\xED\xA0\x80", "\xF4\x90\x80\x80", "\xFF"};
    for (size_t i = 0; i < sizeof(malformed) / sizeof(malformed[0]); i++) {
        CHECK(textToMorseAlloc(malformed[i], &output, '/') == RESULT_INVALID_INPUT && output == NULL);
    }
    CHECK(textToMorseAlloc("\xC3\xA9", &output, '/') == RESULT_UNSUPPORTED_CHARACTER && output == NULL);
    CHECK(textToMorseAlloc("\xF0\x9F\x98\x80", &output, '/') == RESULT_UNSUPPORTED_CHARACTER && output == NULL);
    CHECK(textToMorseAlloc("A%Z", &output, '/') == RESULT_UNSUPPORTED_CHARACTER && output == NULL);
    CHECK(morseToTextAlloc(".- .......", &output, '/') == RESULT_INVALID_MORSE && output == NULL);
    CHECK(morseToTextAlloc("/ /", &output, '/') == RESULT_EMPTY_INPUT && output == NULL);
    CHECK(morseToTextAlloc(".- /| -...", &output, '/') == RESULT_INVALID_MORSE && output == NULL);
    CHECK(morseToTextAlloc(".-\xFF", &output, '/') == RESULT_INVALID_MORSE && output == NULL);
    char *encoded, *decoded;
    const char *utf8 = "\xEF\xBB\xBF\xC3\x87\xC4\xB1\xC4\x9F \xC3\x96\xC5\x9E\xC3\x9C\r\n";
    CHECK(textToMorseAlloc(utf8, &encoded, '/') == RESULT_SUCCESS);
    CHECK(morseToTextAlloc(encoded, &decoded, '/') == RESULT_SUCCESS);
    CHECK(strcmp(decoded, "CIG OSU") == 0);
    ConversionStats stats;
    calculateConversionStats(utf8, encoded, CONVERSION_TEXT_TO_MORSE, &stats);
    CHECK(stats.inputLength == 9 && stats.convertedCharacterCount == 6 && stats.wordCount == 2);
    free(encoded); free(decoded);
    CHECK(textToMorseAlloc("\xC7\xFD\xF0 \xD6\xDE\xDC\r\n", &encoded, '/') == RESULT_SUCCESS);
    CHECK(morseToTextAlloc(encoded, &decoded, '/') == RESULT_SUCCESS);
    CHECK(strcmp(decoded, "CIG OSU") == 0);
    free(encoded); free(decoded);
    CHECK(morseToTextAlloc("\xEF\xBB\xBF.-\r\n/\t-...\n", &output, '/') == RESULT_SUCCESS);
    CHECK(strcmp(output, "A B") == 0);
    free(output);
    return 0;
}

static int testLargeRoundtrip(void)
{
    const char unit[] = "HELLO, WORLD!/$ ";
    const size_t repeats = (2 * 1024 * 1024) / (sizeof(unit) - 1) + 9;
    const size_t length = repeats * (sizeof(unit) - 1);
    char *text = malloc(length + 1), *encoded = NULL, *decoded = NULL, *loaded = NULL;
    CHECK(text != NULL);
    for (size_t i = 0; i < repeats; i++) memcpy(text + i * (sizeof(unit) - 1), unit, sizeof(unit) - 1);
    text[length] = '\0';
    CHECK(length > 2 * 1024 * 1024);
    CHECK(textToMorseAlloc(text, &encoded, '/') == RESULT_SUCCESS);
    CHECK(strlen(encoded) > length * 4);
    CHECK(morseToTextAlloc(encoded, &decoded, '/') == RESULT_SUCCESS);
    CHECK(strlen(decoded) == length - 1 && strncmp(decoded, text, length - 1) == 0);
    ConversionStats stats;
    calculateConversionStats(text, encoded, CONVERSION_TEXT_TO_MORSE, &stats);
    CHECK(stats.inputLength == length);
    CHECK(stats.convertedCharacterCount == repeats * (sizeof(unit) - 3));
    CHECK(stats.wordCount == repeats * 2 && stats.spaceCount == repeats * 2);
    const char path[] = "morsecore-dynamic-large-test.tmp";
    CHECK(writeTextFileChecked(path, text) == RESULT_SUCCESS);
    CHECK(readTextFileAlloc(path, &loaded) == RESULT_SUCCESS && strcmp(loaded, text) == 0);
    free(loaded);
    CHECK(writeTextFileChecked(path, encoded) == RESULT_SUCCESS);
    CHECK(readTextFileAlloc(path, &loaded) == RESULT_SUCCESS && strcmp(loaded, encoded) == 0);
    CHECK(remove(path) == 0);
    free(loaded); free(decoded); free(encoded); free(text);
    return 0;
}

static int testFileErrorsAndRawBytes(void)
{
    char *output = (char *) (uintptr_t) 1;
    CHECK(readTextFileAlloc("morsecore-nonexistent-dynamic-test/missing.txt", &output) == RESULT_FILE_OPEN_ERROR);
    CHECK(output == NULL);
    CHECK(readTextFileAlloc(NULL, &output) == RESULT_INVALID_INPUT && output == NULL);
    CHECK(readTextStreamAlloc(NULL, &output) == RESULT_INVALID_INPUT && output == NULL);
    CHECK(writeTextFileChecked(NULL, "A") == RESULT_INVALID_INPUT);
    CHECK(writeTextFileChecked("x", NULL) == RESULT_INVALID_INPUT);
    const char path[] = "morsecore-dynamic-raw-test.tmp";
    const char raw[] = "\xEF\xBB\xBF" "A\r\nB\n";
    CHECK(writeTextFileChecked(path, raw) == RESULT_SUCCESS);
    CHECK(readTextFileAlloc(path, &output) == RESULT_SUCCESS);
    CHECK(strcmp(output, raw) == 0);
    free(output);
    FILE *file = fopen(path, "wb");
    CHECK(file != NULL && fwrite("A\0B", 1, 3, file) == 3 && fclose(file) == 0);
    CHECK(readTextFileAlloc(path, &output) == RESULT_INVALID_INPUT && output == NULL);
    CHECK(writeTextFileChecked(path, "") == RESULT_SUCCESS);
    CHECK(readTextFileAlloc(path, &output) == RESULT_SUCCESS && output[0] == '\0');
    free(output);
    CHECK(remove(path) == 0);
    return 0;
}

static int testLineInput(void)
{
    const char path[] = "morsecore-dynamic-input-test.tmp";
    FILE *file = fopen(path, "wb");
    CHECK(file != NULL);
    for (size_t i = 0; i < 100000; i++) CHECK(fputc('A', file) == 'A');
    const char tail[] = "\r\n\nBAD\0LINE\nTAIL";
    CHECK(fwrite(tail, 1, sizeof(tail) - 1, file) == sizeof(tail) - 1);
    CHECK(fclose(file) == 0);
    CHECK(freopen(path, "rb", stdin) != NULL);
    char *line = NULL;
    CHECK(readInputLineAlloc(&line) == RESULT_SUCCESS && strlen(line) == 100000);
    free(line);
    CHECK(readInputLineAlloc(&line) == RESULT_SUCCESS && line[0] == '\0');
    free(line);
    CHECK(readInputLineAlloc(&line) == RESULT_INVALID_INPUT && line == NULL);
    CHECK(readInputLineAlloc(&line) == RESULT_SUCCESS && strcmp(line, "TAIL") == 0);
    free(line);
    CHECK(readInputLineAlloc(&line) == RESULT_END_OF_INPUT && line == NULL);
    CHECK(fclose(stdin) == 0);
    CHECK(remove(path) == 0);
    return 0;
}

#ifdef MORSECORE_TEST_ALLOC_FAILURE
static int testAllocationFailure(void)
{
    char *output = NULL;
    failAllocation = 1;
    ResultCode encode = textToMorseAlloc("SOS", &output, '/');
    ResultCode decode = morseToTextAlloc("... --- ...", &output, '/');
    failAllocation = 0;
    CHECK(encode == RESULT_OUT_OF_MEMORY && decode == RESULT_OUT_OF_MEMORY && output == NULL);
    FILE *file = tmpfile();
    CHECK(file != NULL);
    CHECK(fputs("TEST", file) >= 0 && fseek(file, 0, SEEK_SET) == 0);
    failAllocation = 1;
    ResultCode read = readTextStreamAlloc(file, &output);
    failAllocation = 0;
    CHECK(read == RESULT_OUT_OF_MEMORY && output == NULL);
    failReallocation = 1;
    read = readTextStreamAlloc(file, &output);
    failReallocation = 0;
    CHECK(read == RESULT_OUT_OF_MEMORY && output == NULL);
    CHECK(fclose(file) == 0);
    failAllocation = 1;
    read = readInputLineAlloc(&output);
    failAllocation = 0;
    CHECK(read == RESULT_OUT_OF_MEMORY && output == NULL);
    return 0;
}
#endif

int main(void)
{
    CHECK(testPunctuation() == 0);
    CHECK(testEncodingAndFailure() == 0);
    CHECK(testLargeRoundtrip() == 0);
    CHECK(testFileErrorsAndRawBytes() == 0);
#ifdef MORSECORE_TEST_ALLOC_FAILURE
    CHECK(testAllocationFailure() == 0);
#endif
    CHECK(testLineInput() == 0);
    puts("PASS: punctuation, encodings, malformed input, large roundtrip, dynamic I/O and line input");
    return 0;
}
