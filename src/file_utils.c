#include <stdio.h>
#include <string.h>
#include "file_utils.h"

int readFromFile(const char fileName[], char content[], int maxLength)
{
    FILE *file = fopen(fileName, "r");

    if (file == NULL) {
        return 0;
    }

    int i = 0;
    char ch;

    while ((ch = fgetc(file)) != EOF && i < maxLength - 1) {
        content[i] = ch;
        i++;
    }

    content[i] = '\0';

    fclose(file);
    return 1;
}

int writeToFile(const char fileName[], const char content[])
{
    FILE *file = fopen(fileName, "w");

    if (file == NULL) {
        return 0;
    }

    fprintf(file, "%s", content);

    fclose(file);
    return 1;
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
        sprintf(fullPath, "../data/%s", fileName);

        if (!fileExists(fullPath)) {
            return;
        }
    }

    strcpy(fileName, "output_error.txt");
}

void buildDataFilePath(char fullPath[], const char fileName[])
{
    sprintf(fullPath, "../data/%s", fileName);
}