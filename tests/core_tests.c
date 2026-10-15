#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "morse.h"
#include "separator.h"
#include "stats.h"

#define CHECK(condition) do { \
    if (!(condition)) { \
        fprintf(stderr, "FAIL core line %d: %s\n", __LINE__, #condition); \
        return 1; \
    } \
} while (0)

static int testConversionStats(void)
{
    const struct {
        const char *text;
        size_t length, words, letters, spaces;
    } cases[] = {
        {"Hello World", 11, 2, 10, 1},
        {"Merhaba D\xc3\xbcnya", 13, 2, 12, 1},
        {"\xc3\x87\xc3\xa7\xc4\x9e\xc4\x9f\xc4\xb0\xc4\xb1"
         "\xc3\x96\xc3\xb6\xc5\x9e\xc5\x9f\xc3\x9c\xc3\xbc", 12, 1, 12, 0},
        {"\xc7\xe7\xd0\xf0\xdd\xfd\xd6\xf6\xde\xfe\xdc\xfc", 12, 1, 12, 0},
        {" \tHello\r\n\t  World\n", 18, 2, 10, 3},
        {"2026 123", 8, 2, 7, 1},
        {"E", 1, 1, 1, 0},
        {"HELLO, WORLD!", 13, 2, 12, 1}
    };
    ConversionStats stats;
    for (size_t i = 0; i < sizeof(cases) / sizeof(cases[0]); i++) {
        for (size_t j = 0; j < 4; j++) {
            char separator = "/|#*"[j];
            char *encoded = NULL, *decoded = NULL;
            CHECK(textToMorseAlloc(cases[i].text, &encoded, separator) == RESULT_SUCCESS);
            calculateConversionStats(cases[i].text, encoded, CONVERSION_TEXT_TO_MORSE, &stats);
            CHECK(stats.inputLength == cases[i].length);
            CHECK(stats.outputLength == strlen(encoded));
            CHECK(stats.wordCount == cases[i].words);
            CHECK(stats.convertedCharacterCount == cases[i].letters);
            CHECK(stats.spaceCount == cases[i].spaces);
            CHECK(morseToTextAlloc(encoded, &decoded, separator) == RESULT_SUCCESS);
            calculateConversionStats(encoded, decoded, CONVERSION_MORSE_TO_TEXT, &stats);
            CHECK(stats.inputLength == strlen(encoded));
            CHECK(stats.outputLength == cases[i].letters + cases[i].words - 1);
            CHECK(stats.wordCount == cases[i].words);
            CHECK(stats.convertedCharacterCount == cases[i].letters);
            CHECK(stats.spaceCount == cases[i].words - 1);
            free(decoded); free(encoded);
        }
    }
    const char *morse[] = {" . \t . ", "/ / .- / / -... / /"};
    const size_t words[] = {1, 2}, spaces[] = {0, 6};
    for (size_t i = 0; i < 2; i++) {
        char *decoded = NULL;
        CHECK(morseToTextAlloc(morse[i], &decoded, '/') == RESULT_SUCCESS);
        calculateConversionStats(morse[i], decoded, CONVERSION_MORSE_TO_TEXT, &stats);
        CHECK(stats.wordCount == words[i] && stats.convertedCharacterCount == 2 && stats.spaceCount == spaces[i]);
        free(decoded);
    }
    char multibyte[1000];
    for (size_t i = 0; i < 498; i++) {
        multibyte[i * 2] = (char) 0xc3;
        multibyte[i * 2 + 1] = (char) 0xa7;
    }
    memcpy(multibyte + 996, "ABC", 4);
    char *encoded = NULL;
    CHECK(textToMorseAlloc(multibyte, &encoded, '/') == RESULT_SUCCESS);
    calculateConversionStats(multibyte, encoded, CONVERSION_TEXT_TO_MORSE, &stats);
    CHECK(stats.inputLength == 501 && stats.outputLength == 2503);
    CHECK(stats.wordCount == 1 && stats.convertedCharacterCount == 501 && stats.spaceCount == 0);
    free(encoded);
    return 0;
}

static int testAlphabetAndSeparators(void)
{
    const char *alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ 0123456789";
    for (size_t i = 0; i < 4; i++) {
        char separator = "/|#*"[i];
        char *encoded = NULL, *decoded = NULL;
        CHECK(textToMorseAlloc(alphabet, &encoded, separator) == RESULT_SUCCESS);
        CHECK(resolveSeparatorForMorseInput(encoded, '/') == separator);
        CHECK(morseToTextAlloc(encoded, &decoded, separator) == RESULT_SUCCESS);
        CHECK(strcmp(decoded, alphabet) == 0);
        free(decoded); free(encoded);
    }
    CHECK(resolveSeparatorForMorseInput("... --- ...", '|') == '|');
    CHECK(resolveSeparatorForMorseInput("... --- ...", '?') == '/');
    char *encoded = NULL;
    CHECK(textToMorseAlloc(" \tHello\r\n\t  World\n", &encoded, '/') == RESULT_SUCCESS);
    CHECK(strcmp(encoded, ".... . .-.. .-.. --- / .-- --- .-. .-.. -.. ") == 0);
    free(encoded);
    return 0;
}

int main(void)
{
    CHECK(testConversionStats() == 0);
    CHECK(testAlphabetAndSeparators() == 0);
    puts("PASS: Unicode statistics, alphabet, separators and Turkish normalization");
    return 0;
}
