#ifndef MORSE_H
#define MORSE_H

#include "error_codes.h"
#include <stddef.h>

/* The caller owns successful outputs and must free them. On failure *output is
 * NULL. Whitespace becomes word gaps; decoded letters are uppercase. A leading
 * UTF-8 BOM is ignored. UTF-8/Windows-1254 Turkish letters map to Latin letters.
 * These APIs have no fixed input-length limit. */
ResultCode textToMorseAlloc(const char *text, char **output, char wordSeparator);
ResultCode morseToTextAlloc(const char *morseInput, char **output, char wordSeparator);

/* Legacy APIs: input < MAX_TEXT_LENGTH; output buffer MAX_OUTPUT_LENGTH bytes. */
int textToMorse(const char text[], char morseOutput[], char wordSeparator);
int morseToText(const char morseInput[], char textOutput[], char wordSeparator);
/* Read-only alphabet access; code strings have static lifetime. */
size_t morseAlphabetSize(void);
int morseAlphabetEntry(size_t index, unsigned char *character, const char **code);
int morseIsValidUtf8(const unsigned char *text, size_t length);
#endif
