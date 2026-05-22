#include <stdio.h>
#include <string.h>
#include <ctype.h>

#include "stats.h"
#include "ui.h"
#include "language.h"

static int countWords(const char input[])
{
    int count = 0;
    int inWord = 0;

    for (int i = 0; input[i] != '\0'; i++) {
        if (isspace((unsigned char) input[i]) || input[i] == '/') {
            inWord = 0;
        } else if (!inWord) {
            inWord = 1;
            count++;
        }
    }

    return count;
}

static int countConvertedCharacters(const char input[])
{
    int count = 0;

    for (int i = 0; input[i] != '\0'; i++) {
        if (!isspace((unsigned char) input[i]) && input[i] != '/') {
            count++;
        }
    }

    return count;
}

static int countSpaces(const char input[])
{
    int count = 0;

    for (int i = 0; input[i] != '\0'; i++) {
        if (input[i] == ' ') {
            count++;
        }
    }

    return count;
}

void initializeSessionStats(SessionStats *stats)
{
    stats->textToMorseCount = 0;
    stats->morseToTextCount = 0;
    stats->fileOperationCount = 0;
    stats->invalidInputCount = 0;
    stats->savedOutputCount = 0;
}

void calculateConversionStats(const char input[], const char output[], ConversionStats *stats)
{
    stats->inputLength = strlen(input);
    stats->outputLength = strlen(output);
    stats->wordCount = countWords(input);
    stats->convertedCharacterCount = countConvertedCharacters(input);
    stats->spaceCount = countSpaces(input);
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