#ifndef MORSE_PROFILES_H
#define MORSE_PROFILES_H
#include "morse.h"
#include <stdint.h>
/* 0 = legacy Latin/Turkish, 1 = Russian, 2 = Arabic. UTF-8 only. */
ResultCode morseProfileConvert(const char *input, char **output, char separator, uint32_t profile, int decode);
size_t morseProfileSize(uint32_t profile);
int morseProfileEntry(uint32_t profile, size_t index, uint32_t *character, const char **code);
#endif
