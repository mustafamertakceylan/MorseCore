#include <stdio.h>
#include <string.h>
#include <ctype.h>

#include "stats.h"
#include "ui.h"

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

void printConversionStats(const ConversionStats *stats)
{
    printf(COLOR_CYAN "\nConversion Statistics\n" COLOR_RESET);
    printf("--------------------------------\n");
    printf("Input length: %d\n", stats->inputLength);
    printf("Output length: %d\n", stats->outputLength);
    printf("Word count: %d\n", stats->wordCount);
    printf("Converted characters: %d\n", stats->convertedCharacterCount);
    printf("Space count: %d\n", stats->spaceCount);
    printf("--------------------------------\n");
}

void printSessionSummary(const SessionStats *stats)
{
    printf(COLOR_CYAN "\nSession Summary\n" COLOR_RESET);
    printf("--------------------------------\n");
    printf("Text to Morse operations: %d\n", stats->textToMorseCount);
    printf("Morse to Text operations: %d\n", stats->morseToTextCount);
    printf("File operations: %d\n", stats->fileOperationCount);
    printf("Invalid inputs: %d\n", stats->invalidInputCount);
    printf("Saved outputs: %d\n", stats->savedOutputCount);
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