#ifndef FILE_UTILS_H
#define FILE_UTILS_H

#include <stdio.h>
#include "error_codes.h"

/* Binary reads preserve CRLF/BOM and reject embedded NUL bytes. Outputs are
 * caller-owned (free); on failure *output is NULL. Streams remain open. */
ResultCode readTextStreamAlloc(FILE *stream, char **output);
ResultCode readTextFileAlloc(const char *path, char **output);
/* Writes all bytes except the terminating NUL and checks write/close errors. */
ResultCode writeTextFileChecked(const char *path, const char *content);

int readFromFile(const char fileName[], char content[], int maxLength);
int writeToFile(const char fileName[], const char content[]);
int fileExists(const char fileName[]);
/* fileName must have MAX_FILE_NAME_LENGTH bytes. */
void generateNextFileName(char fileName[], const char prefix[], const char extension[]);
/* fullPath must have MAX_FILE_PATH_LENGTH bytes. Empty on failure. */
int buildDataFilePath(char fullPath[], const char fileName[]);

#endif
