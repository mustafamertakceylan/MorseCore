#include <string.h>
#include <stdio.h>
#include "validation.h"
#include "app_config.h"

int isEmptyInput(const char input[])
{
    return input[0] == '\0';
}

int isValidFileName(const char fileName[])
{
    if (isEmptyInput(fileName)) {
        return 0;
    }

    for (int i = 0; fileName[i] != '\0'; i++) {
        char ch = fileName[i];

        if (ch == '/' || ch == '\\' || ch == ':' || ch == '*' ||
            ch == '?' || ch == '"' || ch == '<' || ch == '>' || ch == '|') {
            return 0;
            }
    }

    return 1;
}

int hasTxtExtension(const char fileName[])
{
    int length = strlen(fileName);
    const char extension[] = ".txt";
    int extensionLength = strlen(extension);

    if (length < extensionLength) {
        return 0;
    }

    return strcmp(fileName + length - extensionLength, extension) == 0;
}

void ensureTxtExtension(char fileName[], int maxLength)
{
    if (!hasTxtExtension(fileName)) {
        int length = strlen(fileName);

        if (length + 4 < maxLength) {
            strcat(fileName, ".txt");
        }
    }
}