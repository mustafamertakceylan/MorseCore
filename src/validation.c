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

    for (size_t i = 0; fileName[i] != '\0'; i++) {
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
    size_t length = strlen(fileName);
    const char extension[] = ".txt";
    size_t extensionLength = strlen(extension);

    if (length < extensionLength) {
        return 0;
    }

    return strcmp(fileName + length - extensionLength, extension) == 0;
}

void ensureTxtExtension(char fileName[], int maxLength)
{
    if (!hasTxtExtension(fileName)) {
        size_t length = strlen(fileName);

        if (maxLength > 4 && length < (size_t) maxLength - 4) {
            strcat(fileName, ".txt");
        }
    }
}
