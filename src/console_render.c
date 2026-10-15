#include <stdio.h>
#include "console_render.h"
#include "morse.h"
#include "stats.h"
#include "ui.h"
#include "language.h"

void printMorseTable(void)
{
    size_t count = morseAlphabetSize();
    size_t rows = (count + 2) / 3;
    for (size_t row = 0; row < rows; row++) {
        for (size_t column = 0; column < 3; column++) {
            unsigned char character;
            const char *code;
            if (morseAlphabetEntry(row + column * rows, &character, &code)) {
                printf("  %s%c%s  ->  %s%-7s%s   ", COLOR_YELLOW, character,
                       COLOR_MUTED, COLOR_TEXT, code, COLOR_RESET);
            }
        }
        putchar('\n');
    }
    printSectionRule();
}

void printConversionStats(const ConversionStats *stats, const char language[])
{
    printSectionHeader(getText(language, TXT_CONVERSION_STATISTICS));
    printStatLine(getText(language, TXT_INPUT_LENGTH), stats->inputLength);
    printStatLine(getText(language, TXT_OUTPUT_LENGTH), stats->outputLength);
    printStatLine(getText(language, TXT_WORD_COUNT), stats->wordCount);
    printStatLine(getText(language, TXT_CONVERTED_CHARACTERS), stats->convertedCharacterCount);
    printStatLine(getText(language, TXT_SPACE_COUNT), stats->spaceCount);
    printSectionRule();
}

void printSessionSummary(const SessionStats *stats, const char language[])
{
    printSectionHeader(getText(language, TXT_SESSION_SUMMARY));
    printStatLine(getText(language, TXT_TEXT_TO_MORSE_OPERATIONS), stats->textToMorseCount);
    printStatLine(getText(language, TXT_MORSE_TO_TEXT_OPERATIONS), stats->morseToTextCount);
    printStatLine(getText(language, TXT_FILE_OPERATIONS), stats->fileOperationCount);
    printStatLine(getText(language, TXT_INVALID_INPUTS), stats->invalidInputCount);
    printStatLine(getText(language, TXT_SAVED_OUTPUTS), stats->savedOutputCount);
    printSectionRule();
}
