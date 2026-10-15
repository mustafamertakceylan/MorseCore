#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <ctype.h>

#include "app.h"
#include "ui.h"
#include "morse.h"
#include "file_utils.h"
#include "input.h"
#include "constants.h"
#include "app_config.h"
#include "validation.h"
#include "error.h"
#include "stats.h"
#include "settings.h"
#include "separator.h"
#include "language.h"
#include "console_features.h"

static void runSettingsMenu(AppSettings *settings, SessionStats *sessionStats);
static void changeWordSeparator(AppSettings *settings, SessionStats *sessionStats);
static void changeLanguage(AppSettings *settings, SessionStats *sessionStats);

static void reportConversionError(ResultCode result, const AppSettings *settings, SessionStats *stats)
{
    printError(result, settings->language);
    increaseInvalidInputCount(stats);
}

static int runTextConversion(const AppSettings *settings, SessionStats *sessionStats, int decode)
{
    const char *language = settings->language;
    char *text = NULL;
    char *output = NULL;
    char activeSeparator = settings->wordSeparator;
    ConversionStats conversionStats;

    printSectionHeader(getText(language, decode ? TXT_TITLE_MORSE_TO_TEXT : TXT_TITLE_TEXT_TO_MORSE));
    printf("%s", getText(language, decode ? TXT_ENTER_MORSE_CODE : TXT_ENTER_TEXT));
    fflush(stdout);
    ResultCode result = readInputLineAlloc(&text);
    if (result == RESULT_END_OF_INPUT) return 0;
    if (result == RESULT_SUCCESS) {
        if (decode) {
            activeSeparator = resolveSeparatorForMorseInput(text, settings->wordSeparator);
            result = morseToTextAlloc(text, &output, activeSeparator);
        } else {
            result = textToMorseAlloc(text, &output, activeSeparator);
        }
    }
    if (result == RESULT_SUCCESS) {
        printResult(getText(language, decode ? TXT_TEXT_OUTPUT : TXT_MORSE_OUTPUT), output);
        if (decode) {
            printf("%s: %c\n", getText(language, TXT_DETECTED_SEPARATOR), activeSeparator);
        }
        calculateConversionStats(text, output, decode ? CONVERSION_MORSE_TO_TEXT : CONVERSION_TEXT_TO_MORSE,
                                 &conversionStats);
        printConversionStats(&conversionStats, language);
        if (decode) increaseMorseToTextCount(sessionStats);
        else {
            increaseTextToMorseCount(sessionStats);
            if (askToSaveOutput(output, language)) increaseSavedOutputCount(sessionStats);
        }
    } else {
        reportConversionError(result, settings, sessionStats);
    }
    free(text);
    free(output);
    return 1;
}

static int runFileConversion(const AppSettings *settings, SessionStats *sessionStats, int decode)
{
    const char *language = settings->language;
    char inputFileName[MAX_FILE_NAME_LENGTH];
    char inputPath[MAX_FILE_PATH_LENGTH];
    char outputFileName[MAX_FILE_NAME_LENGTH];
    char outputPath[MAX_FILE_PATH_LENGTH];
    char *text = NULL;
    char *output = NULL;
    char activeSeparator = settings->wordSeparator;
    ConversionStats conversionStats;

    printSectionHeader(getText(language, decode ? TXT_TITLE_FILE_MORSE_TO_TEXT : TXT_TITLE_FILE_TEXT_TO_MORSE));
    printf("%s", getText(language, decode ? TXT_ENTER_MORSE_FILE_NAME : TXT_ENTER_INPUT_FILE_NAME));
    fflush(stdout);
    int inputStatus = readInputLine(inputFileName, (int) sizeof(inputFileName));
    if (inputStatus == 0) return 0;
    ResultCode result = RESULT_SUCCESS;
    if (inputStatus < 0 || !isValidFileName(inputFileName)) {
        result = inputFileName[0] == '\0' && inputStatus > 0 ? RESULT_EMPTY_INPUT : RESULT_INVALID_INPUT;
    } else if (!hasTxtExtension(inputFileName) && strlen(inputFileName) + 4 >= sizeof(inputFileName)) {
        result = RESULT_INVALID_INPUT;
    } else {
        ensureTxtExtension(inputFileName, (int) sizeof(inputFileName));
        if (!buildDataFilePath(inputPath, inputFileName)) result = RESULT_INVALID_INPUT;
        else result = readTextFileAlloc(inputPath, &text);
    }
    if (result == RESULT_SUCCESS) {
        if (decode) {
            activeSeparator = resolveSeparatorForMorseInput(text, settings->wordSeparator);
            result = morseToTextAlloc(text, &output, activeSeparator);
        } else {
            result = textToMorseAlloc(text, &output, activeSeparator);
        }
    }
    if (result == RESULT_SUCCESS) {
        if (decode) printf("%s: %c\n", getText(language, TXT_DETECTED_SEPARATOR), activeSeparator);
        printPreview(getText(language, decode ? TXT_MORSE_INPUT_FILE_PREVIEW : TXT_INPUT_FILE_PREVIEW),
                     text, language);
        printPreview(getText(language, decode ? TXT_DECODED_TEXT_PREVIEW : TXT_MORSE_OUTPUT_PREVIEW),
                     output, language);
        generateNextFileName(outputFileName, decode ? DECODED_OUTPUT_PREFIX : MORSE_OUTPUT_PREFIX,
                             DEFAULT_FILE_EXTENSION);
        if (outputFileName[0] == '\0' || !buildDataFilePath(outputPath, outputFileName)) result = RESULT_FILE_WRITE_ERROR;
        else result = writeTextFileChecked(outputPath, output);
        if (result == RESULT_SUCCESS) {
            printf("\n%s[+] %s: %s%s\n", COLOR_GREEN,
                   getText(language, TXT_FILE_CONVERTED_SUCCESSFULLY), outputPath, COLOR_RESET);
            calculateConversionStats(text, output, decode ? CONVERSION_MORSE_TO_TEXT : CONVERSION_TEXT_TO_MORSE,
                                     &conversionStats);
            printConversionStats(&conversionStats, language);
            if (decode) increaseMorseToTextCount(sessionStats);
            else increaseTextToMorseCount(sessionStats);
            increaseFileOperationCount(sessionStats);
            increaseSavedOutputCount(sessionStats);
        }
    }
    if (result != RESULT_SUCCESS) reportConversionError(result, settings, sessionStats);
    free(text);
    free(output);
    return 1;
}

void runApplication(void)
{
    int choice;
    SessionStats sessionStats;
    AppSettings settings;
    initializeSessionStats(&sessionStats);
    if (!loadSettings(&settings)) saveSettings(&settings);

    do {
        clearConsole();
        printMainMenu(settings.wordSeparator, settings.language);
        choice = getMenuChoice(settings.language);
        clearConsole();
        switch (choice) {
            case 1:
            case 2:
                if (!runTextConversion(&settings, &sessionStats, choice == 2)) return;
                waitForEnter(settings.language);
                break;
            case 3:
            case 4:
                if (!runFileConversion(&settings, &sessionStats, choice == 4)) return;
                waitForEnter(settings.language);
                break;
            case 5:
                printSectionHeader(getText(settings.language, TXT_TITLE_MORSE_TABLE));
                printMorseTable();
                waitForEnter(settings.language);
                break;
            case 6:
                showAboutProject(settings.language);
                waitForEnter(settings.language);
                break;
            case 7:
                runSettingsMenu(&settings, &sessionStats);
                break;
            case 8:
                if (!runAudioMenu(&settings, &sessionStats)) return;
                waitForEnter(settings.language);
                break;
            case 9:
                if (!runPracticeMenu(&settings, &sessionStats)) return;
                waitForEnter(settings.language);
                break;
            case 0:
                printSessionSummary(&sessionStats, settings.language);
                printf("\n%s%s%s\n", COLOR_MUTED, getText(settings.language, TXT_EXITING_IN_SECONDS), COLOR_RESET);
                fflush(stdout);
                waitForSeconds(3);
                break;
            default:
                printError(RESULT_INVALID_INPUT, settings.language);
                increaseInvalidInputCount(&sessionStats);
                waitForEnter(settings.language);
                break;
        }
    } while (choice != 0);
}
static void runSettingsMenu(AppSettings *settings, SessionStats *sessionStats)
{
    int settingsChoice;

    do {
        clearConsole();

        printSettingsMenu(settings->wordSeparator, settings->language);

        settingsChoice = getMenuChoice(settings->language);
        clearConsole();

        switch (settingsChoice) {
            case 1:
            {
                changeWordSeparator(settings, sessionStats);
                break;
            }

            case 2:
            {
                changeLanguage(settings, sessionStats);
                break;
            }

            case 0:
            {
                break;
            }

            default:
            {
                printError(RESULT_INVALID_INPUT, settings->language);
                increaseInvalidInputCount(sessionStats);
                waitForEnter(settings->language);
                break;
            }
        }

    } while (settingsChoice != 0);
}

static void changeWordSeparator(AppSettings *settings, SessionStats *sessionStats)
{
    char newSeparator[20];

    printSectionHeader(getText(settings->language, TXT_TITLE_CHANGE_SEPARATOR));
    printf("%s: %c\n", getText(settings->language, TXT_CURRENT_SEPARATOR_SHORT), settings->wordSeparator);
    printf("%s\n", getText(settings->language, TXT_ALLOWED_SEPARATORS));
    printSectionRule();
    printf("%s", getText(settings->language, TXT_ENTER_NEW_SEPARATOR));

    int inputStatus = readInputLine(newSeparator, sizeof(newSeparator));
    if (inputStatus == 0) return;
    if (inputStatus < 0 || strlen(newSeparator) != 1 || !isValidSeparator(newSeparator[0])) {
        printError(RESULT_INVALID_SEPARATOR, settings->language);
        increaseInvalidInputCount(sessionStats);
        waitForEnter(settings->language);
        return;
    }

    settings->wordSeparator = newSeparator[0];

    if (saveSettings(settings)) {
        printSeparatorSavedMessage(settings->wordSeparator, settings->language);
    } else {
        printError(RESULT_SETTINGS_SAVE_ERROR, settings->language);
        increaseInvalidInputCount(sessionStats);
    }

    waitForEnter(settings->language);
}

static void changeLanguage(AppSettings *settings, SessionStats *sessionStats)
{
    char newLanguage[10];

    printSectionHeader(getText(settings->language, TXT_TITLE_CHANGE_LANGUAGE));
    printf("%s: %s\n", getText(settings->language, TXT_CURRENT_LANGUAGE), settings->language);
    printf("%s\n", getText(settings->language, TXT_ALLOWED_LANGUAGES));
    printSectionRule();
    printf("%s", getText(settings->language, TXT_ENTER_NEW_LANGUAGE));

    int inputStatus = readInputLine(newLanguage, sizeof(newLanguage));
    if (inputStatus == 0) return;
    for (size_t i = 0; newLanguage[i] != '\0'; i++) {
        newLanguage[i] = (char) toupper((unsigned char) newLanguage[i]);
    }
    if (inputStatus < 0 || !isSupportedLanguage(newLanguage)) {
        printError(RESULT_INVALID_INPUT, settings->language);
        increaseInvalidInputCount(sessionStats);
        waitForEnter(settings->language);
        return;
    }

    strcpy(settings->language, newLanguage);

    if (saveSettings(settings)) {
        printLanguageSavedMessage(settings->language);
    } else {
        printError(RESULT_SETTINGS_SAVE_ERROR, settings->language);
        increaseInvalidInputCount(sessionStats);
    }

    waitForEnter(settings->language);
}
