#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "constants.h"
#include "file_utils.h"
#include "morse.h"
#include "separator.h"
#include "stats.h"
#include "validation.h"

#define CHECK(condition) do { \
    if (!(condition)) { \
        fprintf(stderr, "FAIL line %d: %s\n", __LINE__, #condition); \
        return 1; \
    } \
} while (0)

static int testConversionStats(void)
{
    const struct {
        const char *text;
        int length;
        int words;
        int letters;
        int spaces;
    } cases[] = {
        {"Hello World", 11, 2, 10, 1},
        {"Merhaba D\xc3\xbcnya", 13, 2, 12, 1},
        {"\xc3\x87\xc3\xa7\xc4\x9e\xc4\x9f\xc4\xb0\xc4\xb1"
         "\xc3\x96\xc3\xb6\xc5\x9e\xc5\x9f\xc3\x9c\xc3\xbc", 12, 1, 12, 0},
        {"\xc7\xe7\xd0\xf0\xdd\xfd\xd6\xf6\xde\xfe\xdc\xfc", 12, 1, 12, 0},
        {" \tHello\r\n\t  World\n", 18, 2, 10, 3},
        {"2026 123", 8, 2, 7, 1},
        {"E", 1, 1, 1, 0}
    };
    char encoded[MAX_OUTPUT_LENGTH];
    char decoded[MAX_OUTPUT_LENGTH];
    ConversionStats stats;
    for (size_t i = 0; i < sizeof(cases) / sizeof(cases[0]); i++) {
        for (size_t j = 0; j < 4; j++) {
            char separator = "/|#*"[j];
            CHECK(textToMorse(cases[i].text, encoded, separator));
            calculateConversionStats(cases[i].text, encoded, CONVERSION_TEXT_TO_MORSE, &stats);
            CHECK(stats.inputLength == cases[i].length);
            CHECK(stats.outputLength == (int) strlen(encoded));
            CHECK(stats.wordCount == cases[i].words);
            CHECK(stats.convertedCharacterCount == cases[i].letters);
            CHECK(stats.spaceCount == cases[i].spaces);

            CHECK(morseToText(encoded, decoded, separator));
            calculateConversionStats(encoded, decoded, CONVERSION_MORSE_TO_TEXT, &stats);
            CHECK(stats.inputLength == (int) strlen(encoded));
            CHECK(stats.outputLength == cases[i].letters + cases[i].words - 1);
            CHECK(stats.wordCount == cases[i].words);
            CHECK(stats.convertedCharacterCount == cases[i].letters);
            CHECK(stats.spaceCount == cases[i].words - 1);
        }
    }

    CHECK(morseToText(" . \t . ", decoded, '/'));
    calculateConversionStats(" . \t . ", decoded, CONVERSION_MORSE_TO_TEXT, &stats);
    CHECK(stats.wordCount == 1 && stats.convertedCharacterCount == 2 && stats.spaceCount == 0);
    CHECK(morseToText("/ / .- / / -... / /", decoded, '/'));
    calculateConversionStats("/ / .- / / -... / /", decoded, CONVERSION_MORSE_TO_TEXT, &stats);
    CHECK(stats.wordCount == 2 && stats.convertedCharacterCount == 2 && stats.spaceCount == 6);
    CHECK(stats.inputLength == 19 && stats.outputLength == 8);

    char limit[MAX_TEXT_LENGTH];
    memset(limit, '9', sizeof(limit) - 1);
    limit[sizeof(limit) - 1] = '\0';
    CHECK(textToMorse(limit, encoded, '/'));
    calculateConversionStats(limit, encoded, CONVERSION_TEXT_TO_MORSE, &stats);
    CHECK(stats.inputLength == 999 && stats.outputLength == 5994);
    CHECK(stats.wordCount == 1 && stats.convertedCharacterCount == 999 && stats.spaceCount == 0);
    for (int i = 0; i < 498; i++) {
        limit[i * 2] = (char) 0xC3;
        limit[i * 2 + 1] = (char) 0xA7;
    }
    memcpy(limit + 996, "ABC", 4);
    CHECK(textToMorse(limit, encoded, '/'));
    calculateConversionStats(limit, encoded, CONVERSION_TEXT_TO_MORSE, &stats);
    CHECK(stats.inputLength == 501 && stats.outputLength == 2503);
    CHECK(stats.wordCount == 1 && stats.convertedCharacterCount == 501 && stats.spaceCount == 0);
    return 0;
}

int main(void)
{
    CHECK(testConversionStats() == 0);
    char output[MAX_OUTPUT_LENGTH];
    char decoded[MAX_OUTPUT_LENGTH];
    const char *alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ 0123456789";
    const char *hello = ".... . .-.. .-.. --- / .-- --- .-. .-.. -.. ";
    CHECK(textToMorse("Hello World", output, '/'));
    CHECK(strcmp(output, hello) == 0);
    CHECK(morseToText(hello, decoded, '/'));
    CHECK(strcmp(decoded, "HELLO WORLD") == 0);
    CHECK(textToMorse(" \tHello\r\n\t  World\n", output, '/'));
    CHECK(strcmp(output, hello) == 0);

    for (size_t i = 0; i < 4; i++) {
        char separator = "/|#*"[i];
        CHECK(textToMorse(alphabet, output, separator));
        CHECK(resolveSeparatorForMorseInput(output, '/') == separator);
        CHECK(morseToText(output, decoded, separator));
        CHECK(strcmp(decoded, alphabet) == 0);
    }
    CHECK(resolveSeparatorForMorseInput("... --- ...", '|') == '|');
    CHECK(resolveSeparatorForMorseInput("... --- ...", '?') == '/');

    CHECK(textToMorse("\xc3\x87\xc3\xa7\xc4\x9e\xc4\x9f\xc4\xb0\xc4\xb1"
                      "\xc3\x96\xc3\xb6\xc5\x9e\xc5\x9f\xc3\x9c\xc3\xbc", output, '/'));
    CHECK(morseToText(output, decoded, '/'));
    CHECK(strcmp(decoded, "CCGGIIOOSSUU") == 0);
    CHECK(textToMorse("\xc7\xe7\xd0\xf0\xdd\xfd\xd6\xf6\xde\xfe\xdc\xfc", output, '/'));
    CHECK(morseToText(output, decoded, '/'));
    CHECK(strcmp(decoded, "CCGGIIOOSSUU") == 0);

    CHECK(!textToMorse("", output, '/'));
    CHECK(!textToMorse(" \t\r\n", output, '/'));
    CHECK(!textToMorse("HELLO!", output, '/'));
    CHECK(!textToMorse("\xc3", output, '/'));
    CHECK(!morseToText("", decoded, '/'));
    CHECK(!morseToText("......", decoded, '/'));
    CHECK(!morseToText("... /| ---", decoded, '/'));

    char digits[MAX_TEXT_LENGTH];
    memset(digits, '9', sizeof(digits) - 1);
    digits[sizeof(digits) - 1] = '\0';
    struct {
        char output[MAX_OUTPUT_LENGTH];
        unsigned char guard[1024];
    } bounded;
    memset(bounded.guard, 0xa5, sizeof(bounded.guard));
    CHECK(textToMorse(digits, bounded.output, '/'));
    CHECK(strlen(bounded.output) == (MAX_TEXT_LENGTH - 1) * MAX_MORSE_LENGTH);
    for (size_t i = 0; i < sizeof(bounded.guard); i++) CHECK(bounded.guard[i] == 0xa5);

    char oversized[MAX_TEXT_LENGTH + 1];
    memset(oversized, 'E', sizeof(oversized) - 1);
    oversized[sizeof(oversized) - 1] = '\0';
    CHECK(!textToMorse(oversized, output, '/'));
    CHECK(!morseToText(oversized, decoded, '/'));

    const char *fileName = "morsecore-core-test.tmp";
    CHECK(writeToFile(fileName, digits));
    CHECK(readFromFile(fileName, decoded, MAX_TEXT_LENGTH));
    CHECK(strcmp(decoded, digits) == 0);
    CHECK(writeToFile(fileName, oversized));
    CHECK(!readFromFile(fileName, decoded, MAX_TEXT_LENGTH));
    CHECK(decoded[0] == '\0');
    FILE *file = fopen(fileName, "wb");
    CHECK(file != NULL);
    CHECK(fputc(0xff, file) == 0xff);
    CHECK(fputc('A', file) == 'A');
    CHECK(fclose(file) == 0);
    CHECK(readFromFile(fileName, decoded, MAX_TEXT_LENGTH));
    CHECK((unsigned char) decoded[0] == 0xff && decoded[1] == 'A');
    CHECK(remove(fileName) == 0);
    CHECK(!fileExists(fileName));

    CHECK(!isValidFileName("../settings.txt"));
    CHECK(!isValidFileName("C:\\test.txt"));
    CHECK(isValidFileName("input.txt"));
    char name[30] = "input";
    ensureTxtExtension(name, sizeof(name));
    CHECK(strcmp(name, "input.txt") == 0);
    ensureTxtExtension(name, sizeof(name));
    CHECK(strcmp(name, "input.txt") == 0);
    puts("PASS: statistics, alphabet, separators, Turkish encodings, invalid input, buffer limits, file I/O");
    return 0;
}
