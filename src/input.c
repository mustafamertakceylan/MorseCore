#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>

#include "input.h"

ResultCode readInputLineAlloc(char **output)
{
    if (output == NULL) return RESULT_INVALID_INPUT;
    *output = NULL;
    size_t length = 0, capacity = 256;
    char *line = malloc(capacity);
    if (line == NULL) return RESULT_OUT_OF_MEMORY;
    int ch;
    while ((ch = fgetc(stdin)) != EOF && ch != '\n') {
        if (ch == '\0') {
            while ((ch = fgetc(stdin)) != EOF && ch != '\n') { }
            free(line);
            return RESULT_INVALID_INPUT;
        }
        if (length == SIZE_MAX - 1) { free(line); return RESULT_INPUT_TOO_LARGE; }
        if (length + 1 >= capacity) {
            size_t grown = capacity > SIZE_MAX / 2 ? SIZE_MAX : capacity * 2;
            char *resized = realloc(line, grown);
            if (resized == NULL) { free(line); return RESULT_OUT_OF_MEMORY; }
            line = resized;
            capacity = grown;
        }
        line[length++] = (char) ch;
    }
    if (ferror(stdin)) { free(line); return RESULT_INPUT_READ_ERROR; }
    if (ch == EOF && length == 0) { free(line); return RESULT_END_OF_INPUT; }
    if (length > 0 && line[length - 1] == '\r') length--;
    line[length] = '\0';
    *output = line;
    return RESULT_SUCCESS;
}
