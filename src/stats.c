#include <stdio.h>

#include "stats.h"
#include "ui.h"
#include "language.h"

static int characterByteLength(const unsigned char text[])
{
    /* Supported UTF-8 Turkish letters occupy two bytes. ASCII and
     * Windows-1254 Turkish letters occupy one byte. */
    if ((text[0] == 0xC3 || text[0] == 0xC4 || text[0] == 0xC5) &&
        (text[1] & 0xC0) == 0x80) {
        return 2;
    }
    return 1;
}

static int countCharacters(const char text[])
{
    int count = 0;
    const unsigned char *current = (const unsigned char *) text;
    while (*current != '\0') {
        count++;
        current += characterByteLength(current);
    }
    return count;
}

static void countPlainText(const char text[], ConversionStats *stats)
{
    int inWord = 0;
    const unsigned char *current = (const unsigned char *) text;
    stats->wordCount = 0;
    stats->convertedCharacterCount = 0;
    stats->spaceCount = 0;

    while (*current != '\0') {
        if (*current == ' ') stats->spaceCount++;
        if (*current == ' ' || *current == '\t' || *current == '\r' || *current == '\n') {
            inWord = 0;
        } else {
            stats->convertedCharacterCount++;
            if (!inWord) {
                stats->wordCount++;
                inWord = 1;
            }
        }
        current += characterByteLength(current);
    }
}

void initializeSessionStats(SessionStats *stats)
{
    stats->textToMorseCount = 0;
    stats->morseToTextCount = 0;
    stats->fileOperationCount = 0;
    stats->invalidInputCount = 0;
    stats->savedOutputCount = 0;
}

void calculateConversionStats(const char input[], const char output[],
                              ConversionDirection direction, ConversionStats *stats)
{
    stats->inputLength = countCharacters(input);
    stats->outputLength = countCharacters(output);
    const char *plainText = direction == CONVERSION_TEXT_TO_MORSE ? input : output;
    countPlainText(plainText, stats);
}

void printConversionStats(const ConversionStats *stats, const char language[])
{
    printf(COLOR_CYAN "\n%s\n" COLOR_RESET, getText(language, TXT_CONVERSION_STATISTICS));
    printf("--------------------------------\n");
    printf("%s: %d\n", getText(language, TXT_INPUT_LENGTH), stats->inputLength);
    printf("%s: %d\n", getText(language, TXT_OUTPUT_LENGTH), stats->outputLength);
    printf("%s: %d\n", getText(language, TXT_WORD_COUNT), stats->wordCount);
    printf("%s: %d\n", getText(language, TXT_CONVERTED_CHARACTERS), stats->convertedCharacterCount);
    printf("%s: %d\n", getText(language, TXT_SPACE_COUNT), stats->spaceCount);
    printf("--------------------------------\n");
}

void printSessionSummary(const SessionStats *stats, const char language[])
{
    printf(COLOR_CYAN "\n%s\n" COLOR_RESET, getText(language, TXT_SESSION_SUMMARY));
    printf("--------------------------------\n");
    printf("%s: %d\n", getText(language, TXT_TEXT_TO_MORSE_OPERATIONS), stats->textToMorseCount);
    printf("%s: %d\n", getText(language, TXT_MORSE_TO_TEXT_OPERATIONS), stats->morseToTextCount);
    printf("%s: %d\n", getText(language, TXT_FILE_OPERATIONS), stats->fileOperationCount);
    printf("%s: %d\n", getText(language, TXT_INVALID_INPUTS), stats->invalidInputCount);
    printf("%s: %d\n", getText(language, TXT_SAVED_OUTPUTS), stats->savedOutputCount);
    printf("--------------------------------\n");
}

void increaseTextToMorseCount(SessionStats *stats)
{
    stats->textToMorseCount++;
}

void increaseMorseToTextCount(SessionStats *stats)
{
    stats->morseToTextCount++;
}

void increaseFileOperationCount(SessionStats *stats)
{
    stats->fileOperationCount++;
}

void increaseInvalidInputCount(SessionStats *stats)
{
    stats->invalidInputCount++;
}

void increaseSavedOutputCount(SessionStats *stats)
{
    stats->savedOutputCount++;
}
