#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <stdint.h>
#include "file_utils.h"
#include "app_config.h"
#include "constants.h"

ResultCode readTextStreamAlloc(FILE *stream, char **output)
{
    if (output == NULL) return RESULT_INVALID_INPUT;
    *output = NULL;
    if (stream == NULL) return RESULT_INVALID_INPUT;
    size_t length = 0, capacity = 1;
    char *content = malloc(capacity);
    if (content == NULL) return RESULT_OUT_OF_MEMORY;
    char block[16384];
    size_t count;
    while ((count = fread(block, 1, sizeof(block), stream)) != 0) {
        if (memchr(block, '\0', count) != NULL) {
            free(content);
            return RESULT_INVALID_INPUT;
        }
        if (count > SIZE_MAX - length - 1) {
            free(content);
            return RESULT_INPUT_TOO_LARGE;
        }
        size_t needed = length + count + 1;
        if (needed > capacity) {
            size_t grown = capacity;
            while (grown < needed) {
                if (grown > SIZE_MAX / 2) { grown = needed; break; }
                grown *= 2;
            }
            char *resized = realloc(content, grown);
            if (resized == NULL) { free(content); return RESULT_OUT_OF_MEMORY; }
            content = resized;
            capacity = grown;
        }
        memcpy(content + length, block, count);
        length += count;
    }
    if (ferror(stream)) { free(content); return RESULT_FILE_READ_ERROR; }
    content[length] = '\0';
    *output = content;
    return RESULT_SUCCESS;
}

ResultCode readTextFileAlloc(const char *path, char **output)
{
    if (output == NULL) return RESULT_INVALID_INPUT;
    *output = NULL;
    if (path == NULL) return RESULT_INVALID_INPUT;
    FILE *file = openUtf8File(path, "rb");
    if (file == NULL) return RESULT_FILE_OPEN_ERROR;
    ResultCode result = readTextStreamAlloc(file, output);
    if (fclose(file) != 0 && result == RESULT_SUCCESS) {
        free(*output);
        *output = NULL;
        return RESULT_FILE_READ_ERROR;
    }
    return result;
}

ResultCode writeTextFileChecked(const char *path, const char *content)
{
    if (path == NULL || content == NULL) return RESULT_INVALID_INPUT;
    FILE *file = openUtf8File(path, "wb");
    if (file == NULL) return RESULT_FILE_OPEN_ERROR;
    size_t length = strlen(content);
    int success = fwrite(content, 1, length, file) == length;
    if (fclose(file) != 0) success = 0;
    return success ? RESULT_SUCCESS : RESULT_FILE_WRITE_ERROR;
}

int readFromFile(const char fileName[], char content[], int maxLength)
{
    if (content == NULL || maxLength <= 0) return 0;
    content[0] = '\0';
    if (fileName == NULL) return 0;
    FILE *file = openUtf8File(fileName, "r");

    if (file == NULL) {
        return 0;
    }

    int i = 0;
    int ch;

    while ((ch = fgetc(file)) != EOF) {
        if (i >= maxLength - 1) {
            content[0] = '\0';
            fclose(file);
            return 0;
        }
        content[i++] = (char) ch;
    }

    content[i] = '\0';

    int success = !ferror(file);
    fclose(file);
    return success;
}

int writeToFile(const char fileName[], const char content[])
{
    return writeTextFileChecked(fileName, content) == RESULT_SUCCESS;
}

int fileExists(const char fileName[])
{
    FILE *file = openUtf8File(fileName, "r");

    if (file == NULL) {
        return 0;
    }

    fclose(file);
    return 1;
}

void generateNextFileName(char fileName[], const char prefix[], const char extension[])
{
    char fullPath[MAX_FILE_PATH_LENGTH];

    for (int i = 1; i < 10000; i++) {
        int length = snprintf(fileName, MAX_FILE_NAME_LENGTH, "%s_%d.%s", prefix, i, extension);
        if (length < 0 || length >= MAX_FILE_NAME_LENGTH || !buildDataFilePath(fullPath, fileName)) {
            fileName[0] = '\0';
            return;
        }

        if (!fileExists(fullPath)) {
            return;
        }
    }

    /* Never overwrite a previous output when all generated names are taken. */
    fileName[0] = '\0';
}

int buildDataFilePath(char fullPath[], const char fileName[])
{
    return runtimeDataFilePath(fullPath, MAX_FILE_PATH_LENGTH, fileName);
}
