#ifndef CONSTANTS_H
#define CONSTANTS_H

/* Only the legacy bounded APIs use these limits. UI/CLI use allocated buffers. */
#define MAX_TEXT_LENGTH 1000
#define MAX_OUTPUT_LENGTH (MAX_TEXT_LENGTH * MAX_MORSE_LENGTH)

#define MAX_FILE_NAME_LENGTH 100
#define MAX_FILE_PATH_LENGTH 4096

#define MORSE_COUNT 54
/* Longest supported code has seven signals; reserve one byte for NUL/space. */
#define MAX_MORSE_LENGTH 8

#endif
