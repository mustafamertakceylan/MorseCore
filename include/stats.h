#ifndef STATS_H
#define STATS_H

#include <stddef.h>

typedef enum {
    CONVERSION_TEXT_TO_MORSE,
    CONVERSION_MORSE_TO_TEXT
} ConversionDirection;

typedef struct {
    size_t inputLength;
    size_t outputLength;
    size_t wordCount;
    size_t convertedCharacterCount;
    size_t spaceCount;
} ConversionStats;

/* Call after a successful conversion. Lengths count characters, not bytes.
 * Words, converted letters/digits/punctuation, and literal spaces refer to plain text:
 * the input for encoding and the output for decoding. */
void calculateConversionStats(const char input[], const char output[],
                              ConversionDirection direction, ConversionStats *stats);

#endif
