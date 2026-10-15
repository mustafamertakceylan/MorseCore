#include <string.h>

#include "stats.h"

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

static const unsigned char *skipBom(const char text[])
{
    return (const unsigned char *) text +
        (strncmp(text, "\xEF\xBB\xBF", 3) == 0 ? 3 : 0);
}

static size_t countCharacters(const char text[])
{
    size_t count = 0;
    const unsigned char *current = skipBom(text);
    while (*current != '\0') {
        count++;
        current += characterByteLength(current);
    }
    return count;
}

static void countPlainText(const char text[], ConversionStats *stats)
{
    int inWord = 0;
    const unsigned char *current = skipBom(text);
    stats->wordCount = 0;
    stats->convertedCharacterCount = 0;
    stats->spaceCount = 0;

    while (*current != '\0') {
        if (*current == ' ') stats->spaceCount++;
        if (*current == ' ' || *current == '\t' || *current == '\r' || *current == '\n' ||
            *current == '\v' || *current == '\f') {
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
