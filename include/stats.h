#ifndef STATS_H
#define STATS_H

typedef enum {
    CONVERSION_TEXT_TO_MORSE,
    CONVERSION_MORSE_TO_TEXT
} ConversionDirection;

typedef struct {
    int inputLength;
    int outputLength;
    int wordCount;
    int convertedCharacterCount;
    int spaceCount;
} ConversionStats;

typedef struct {
    int textToMorseCount;
    int morseToTextCount;
    int fileOperationCount;
    int invalidInputCount;
    int savedOutputCount;
} SessionStats;

void initializeSessionStats(SessionStats *stats);

/* Call after a successful conversion. Lengths count characters, not bytes.
 * Words, converted letters/digits, and literal spaces refer to the plain text:
 * the input for encoding and the output for decoding. */
void calculateConversionStats(const char input[], const char output[],
                              ConversionDirection direction, ConversionStats *stats);
void printConversionStats(const ConversionStats *stats, const char language[]);

void printSessionSummary(const SessionStats *stats, const char language[]);

void increaseTextToMorseCount(SessionStats *stats);
void increaseMorseToTextCount(SessionStats *stats);
void increaseFileOperationCount(SessionStats *stats);
void increaseInvalidInputCount(SessionStats *stats);
void increaseSavedOutputCount(SessionStats *stats);

#endif
