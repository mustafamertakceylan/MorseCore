#include <stdio.h>
#include <string.h>

#include "settings.h"
#include "app_config.h"
#include "separator.h"
#include "language.h"

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
        char language[100];

        if (sscanf(line, "word_separator=%c", &separator) == 1) {
            if (isValidSeparator(separator)) {
                settings->wordSeparator = separator;
            }
        }

        if (sscanf(line, "language=%99s", language) == 1 && isSupportedLanguage(language)) {
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

    int success = fprintf(file, "word_separator=%c\nlanguage=%s\n",
                          settings->wordSeparator, settings->language) >= 0;
    if (fclose(file) != 0) success = 0;
    return success;
}
