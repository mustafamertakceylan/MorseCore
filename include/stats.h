#ifndef STATS_H
#define STATS_H

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

void calculateConversionStats(const char input[], const char output[], ConversionStats *stats);
void printConversionStats(const ConversionStats *stats, const char language[]);

void printSessionSummary(const SessionStats *stats, const char language[]);

void increaseTextToMorseCount(SessionStats *stats);
void increaseMorseToTextCount(SessionStats *stats);
void increaseFileOperationCount(SessionStats *stats);
void increaseInvalidInputCount(SessionStats *stats);
void increaseSavedOutputCount(SessionStats *stats);

#endif