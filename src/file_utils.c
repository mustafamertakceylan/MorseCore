#include <stdio.h>
#include <string.h>
#include "file_utils.h"
#include "app_config.h"

int readFromFile(const char fileName[], char content[], int maxLength)
{
    FILE *file = fopen(fileName, "r");

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
    FILE *file = fopen(fileName, "w");

    if (file == NULL) {
        return 0;
    }

    int success = fprintf(file, "%s", content) >= 0;
    if (fclose(file) != 0) {
        success = 0;
    }
    return success;
}

int fileExists(const char fileName[])
{
    FILE *file = fopen(fileName, "r");

    if (file == NULL) {
        return 0;
    }

    fclose(file);
    return 1;
}

void generateNextFileName(char fileName[], const char prefix[], const char extension[])
{
    char fullPath[200];

    for (int i = 1; i < 10000; i++) {
        sprintf(fileName, "%s_%d.%s", prefix, i, extension);
        sprintf(fullPath, "%s%s", DATA_FOLDER_PATH, fileName);

        if (!fileExists(fullPath)) {
            return;
        }
    }

    strcpy(fileName, "output_error.txt");
}

void buildDataFilePath(char fullPath[], const char fileName[])
{
    sprintf(fullPath, "%s%s", DATA_FOLDER_PATH, fileName);
}
