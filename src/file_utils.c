#include <stdio.h>
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