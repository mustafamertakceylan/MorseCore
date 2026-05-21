#include "separator.h"
#include "app_config.h"

int isValidSeparator(char separator)
{
    return separator == '/' ||
           separator == '|' ||
           separator == '#' ||
           separator == '*';
}

char detectSeparatorFromMorse(const char morseInput[])
{
    for (int i = 0; morseInput[i] != '\0'; i++) {
        if (isValidSeparator(morseInput[i])) {
            return morseInput[i];
        }
    }

    return '\0';
}

char resolveSeparatorForMorseInput(const char morseInput[], char fallbackSeparator)
{
    char detectedSeparator = detectSeparatorFromMorse(morseInput);

    if (detectedSeparator != '\0') {
        return detectedSeparator;
    }

    if (isValidSeparator(fallbackSeparator)) {
        return fallbackSeparator;
    }

    return DEFAULT_WORD_SEPARATOR;
}