#include <stdio.h>
#include <string.h>

#include "settings.h"
#include "app_config.h"
#include "separator.h"

void loadDefaultSettings(AppSettings *settings)
{
    settings->wordSeparator = DEFAULT_WORD_SEPARATOR;
    strcpy(settings->language, DEFAULT_LANGUAGE);
}

int loadSettings(AppSettings *settings)
{
    FILE *file = fopen(SETTINGS_FILE_PATH, "r");

    loadDefaultSettings(settings);

    if (file == NULL) {
        return 0;
    }

    char line[100];

    while (fgets(line, sizeof(line), file) != NULL) {
        char separator;
        char language[3];

        if (sscanf(line, "word_separator=%c", &separator) == 1) {
            if (isValidSeparator(separator)) {
                settings->wordSeparator = separator;
            }
        }

        if (sscanf(line, "language=%2s", language) == 1) {
            strcpy(settings->language, language);
        }
    }

    fclose(file);
    return 1;
}

int saveSettings(const AppSettings *settings)
{
    FILE *file = fopen(SETTINGS_FILE_PATH, "w");

    if (file == NULL) {
        return 0;
    }

    fprintf(file, "word_separator=%c\n", settings->wordSeparator);
    fprintf(file, "language=%s\n", settings->language);

    fclose(file);
    return 1;
}