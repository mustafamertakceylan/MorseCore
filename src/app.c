#include <stdio.h>
#include <string.h>
#include <ctype.h>

#include "app.h"
#include "ui.h"
#include "morse.h"
#include "file_utils.h"
#include "constants.h"
#include "app_config.h"
#include "validation.h"
#include "error.h"
#include "stats.h"
#include "settings.h"
#include "separator.h"
#include "language.h"

static void runSettingsMenu(AppSettings *settings, SessionStats *sessionStats);
static void changeWordSeparator(AppSettings *settings, SessionStats *sessionStats);
static void changeLanguage(AppSettings *settings, SessionStats *sessionStats);

void runApplication(void)
{
    char text[MAX_TEXT_LENGTH];
    char output[MAX_OUTPUT_LENGTH];
    int choice;

    ConversionStats conversionStats;
    SessionStats sessionStats;
    AppSettings settings;

    initializeSessionStats(&sessionStats);

    if (!loadSettings(&settings)) {
        saveSettings(&settings);
    }

    do {
        clearConsole();

        printMainMenu(settings.wordSeparator, settings.language);

        choice = getMenuChoice(settings.language);
        clearConsole();

        switch (choice) {
            case 1:
            {
                printSectionHeader(getText(settings.language, TXT_TITLE_TEXT_TO_MORSE));
                printf("%s", getText(settings.language, TXT_ENTER_TEXT));
                int inputStatus = readInputLine(text, MAX_TEXT_LENGTH);
                if (inputStatus == 0) return;
                if (inputStatus < 0) {
                    printError(RESULT_INVALID_INPUT, settings.language);
                    increaseInvalidInputCount(&sessionStats);
                    waitForEnter(settings.language);
                    break;
                }

                if (textToMorse(text, output, settings.wordSeparator)) {
                    printResult(getText(settings.language, TXT_MORSE_OUTPUT), output);

                    calculateConversionStats(text, output, CONVERSION_TEXT_TO_MORSE, &conversionStats);
                    printConversionStats(&conversionStats, settings.language);
                    increaseTextToMorseCount(&sessionStats);

                    if (askToSaveOutput(output, settings.language)) {
                        increaseSavedOutputCount(&sessionStats);
                    }
                } else {
                    printError(RESULT_UNSUPPORTED_CHARACTER, settings.language);
                    increaseInvalidInputCount(&sessionStats);
                }

                waitForEnter(settings.language);
                break;
            }

            case 2:
            {
                char activeSeparator;

                printSectionHeader(getText(settings.language, TXT_TITLE_MORSE_TO_TEXT));
                printf("%s", getText(settings.language, TXT_ENTER_MORSE_CODE));
                int inputStatus = readInputLine(text, MAX_TEXT_LENGTH);
                if (inputStatus == 0) return;
                if (inputStatus < 0) {
                    printError(RESULT_INVALID_INPUT, settings.language);
                    increaseInvalidInputCount(&sessionStats);
                    waitForEnter(settings.language);
                    break;
                }

                activeSeparator = resolveSeparatorForMorseInput(text, settings.wordSeparator);

                if (morseToText(text, output, activeSeparator)) {
                    printResult(getText(settings.language, TXT_TEXT_OUTPUT), output);
                    printf("%s: %c\n", getText(settings.language, TXT_DETECTED_SEPARATOR), activeSeparator);

                    calculateConversionStats(text, output, CONVERSION_MORSE_TO_TEXT, &conversionStats);
                    printConversionStats(&conversionStats, settings.language);
                    increaseMorseToTextCount(&sessionStats);
                } else {
                    printError(RESULT_INVALID_MORSE, settings.language);
                    increaseInvalidInputCount(&sessionStats);
                }

                waitForEnter(settings.language);
                break;
            }

            case 3:
            {
                char inputFileName[MAX_FILE_NAME_LENGTH];
                char inputPath[MAX_FILE_PATH_LENGTH];
                char outputFileName[MAX_FILE_NAME_LENGTH];
                char outputPath[MAX_FILE_PATH_LENGTH];

                printSectionHeader(getText(settings.language, TXT_TITLE_FILE_TEXT_TO_MORSE));

                printf("%s", getText(settings.language, TXT_ENTER_INPUT_FILE_NAME));
                int inputStatus = readInputLine(inputFileName, sizeof(inputFileName));
                if (inputStatus == 0) return;
                if (inputStatus < 0) {
                    printError(RESULT_INVALID_INPUT, settings.language);
                    increaseInvalidInputCount(&sessionStats);
                    waitForEnter(settings.language);
                    break;
                }

                if (isEmptyInput(inputFileName)) {
                    printError(RESULT_EMPTY_INPUT, settings.language);
                    increaseInvalidInputCount(&sessionStats);
                    waitForEnter(settings.language);
                    break;
                }

                if (!isValidFileName(inputFileName)) {
                    printError(RESULT_INVALID_INPUT, settings.language);
                    increaseInvalidInputCount(&sessionStats);
                    waitForEnter(settings.language);
                    break;
                }

                ensureTxtExtension(inputFileName, MAX_FILE_NAME_LENGTH);
                buildDataFilePath(inputPath, inputFileName);

                if (readFromFile(inputPath, text, MAX_TEXT_LENGTH)) {
                    if (textToMorse(text, output, settings.wordSeparator)) {
                        printPreview(getText(settings.language, TXT_INPUT_FILE_PREVIEW), text);
                        printPreview(getText(settings.language, TXT_MORSE_OUTPUT_PREVIEW), output);

                        generateNextFileName(outputFileName, MORSE_OUTPUT_PREFIX, DEFAULT_FILE_EXTENSION);
                        buildDataFilePath(outputPath, outputFileName);

                        if (writeToFile(outputPath, output)) {
                            printf("\n%s[+] %s: data/%s%s\n", COLOR_GREEN,
                                   getText(settings.language, TXT_FILE_CONVERTED_SUCCESSFULLY),
                                   outputFileName, COLOR_RESET);

                            calculateConversionStats(text, output, CONVERSION_TEXT_TO_MORSE, &conversionStats);
                            printConversionStats(&conversionStats, settings.language);

                            increaseTextToMorseCount(&sessionStats);
                            increaseFileOperationCount(&sessionStats);
                            increaseSavedOutputCount(&sessionStats);
                        } else {
                            printError(RESULT_FILE_WRITE_ERROR, settings.language);
                            increaseInvalidInputCount(&sessionStats);
                        }
                    } else {
                        printError(RESULT_UNSUPPORTED_CHARACTER, settings.language);
                        increaseInvalidInputCount(&sessionStats);
                    }
                } else {
                    printError(RESULT_FILE_OPEN_ERROR, settings.language);
                    increaseInvalidInputCount(&sessionStats);
                }

                waitForEnter(settings.language);
                break;
            }

            case 4:
            {
                char inputFileName[MAX_FILE_NAME_LENGTH];
                char inputPath[MAX_FILE_PATH_LENGTH];
                char outputFileName[MAX_FILE_NAME_LENGTH];
                char outputPath[MAX_FILE_PATH_LENGTH];
                char activeSeparator;

                printSectionHeader(getText(settings.language, TXT_TITLE_FILE_MORSE_TO_TEXT));

                printf("%s", getText(settings.language, TXT_ENTER_MORSE_FILE_NAME));
                int inputStatus = readInputLine(inputFileName, sizeof(inputFileName));
                if (inputStatus == 0) return;
                if (inputStatus < 0) {
                    printError(RESULT_INVALID_INPUT, settings.language);
                    increaseInvalidInputCount(&sessionStats);
                    waitForEnter(settings.language);
                    break;
                }

                if (isEmptyInput(inputFileName)) {
                    printError(RESULT_EMPTY_INPUT, settings.language);
                    increaseInvalidInputCount(&sessionStats);
                    waitForEnter(settings.language);
                    break;
                }

                if (!isValidFileName(inputFileName)) {
                    printError(RESULT_INVALID_INPUT, settings.language);
                    increaseInvalidInputCount(&sessionStats);
                    waitForEnter(settings.language);
                    break;
                }

                ensureTxtExtension(inputFileName, MAX_FILE_NAME_LENGTH);
                buildDataFilePath(inputPath, inputFileName);

                if (readFromFile(inputPath, text, MAX_TEXT_LENGTH)) {
                    activeSeparator = resolveSeparatorForMorseInput(text, settings.wordSeparator);

                    if (morseToText(text, output, activeSeparator)) {
                        printf("%s: %c\n", getText(settings.language, TXT_DETECTED_SEPARATOR), activeSeparator);

                        printPreview(getText(settings.language, TXT_MORSE_INPUT_FILE_PREVIEW), text);
                        printPreview(getText(settings.language, TXT_DECODED_TEXT_PREVIEW), output);

                        generateNextFileName(outputFileName, DECODED_OUTPUT_PREFIX, DEFAULT_FILE_EXTENSION);
                        buildDataFilePath(outputPath, outputFileName);

                        if (writeToFile(outputPath, output)) {
                            printf("\n%s[+] %s: data/%s%s\n", COLOR_GREEN,
                                   getText(settings.language, TXT_FILE_CONVERTED_SUCCESSFULLY),
                                   outputFileName, COLOR_RESET);

                            calculateConversionStats(text, output, CONVERSION_MORSE_TO_TEXT, &conversionStats);
                            printConversionStats(&conversionStats, settings.language);

                            increaseMorseToTextCount(&sessionStats);
                            increaseFileOperationCount(&sessionStats);
                            increaseSavedOutputCount(&sessionStats);
                        } else {
                            printError(RESULT_FILE_WRITE_ERROR, settings.language);
                            increaseInvalidInputCount(&sessionStats);
                        }
                    } else {
                        printError(RESULT_INVALID_MORSE, settings.language);
                        increaseInvalidInputCount(&sessionStats);
                    }
                } else {
                    printError(RESULT_FILE_OPEN_ERROR, settings.language);
                    increaseInvalidInputCount(&sessionStats);
                }

                waitForEnter(settings.language);
                break;
            }

            case 5:
            {
                printSectionHeader(getText(settings.language, TXT_TITLE_MORSE_TABLE));

                printMorseTable();

                waitForEnter(settings.language);
                break;
            }

            case 6:
            {
                showAboutProject(settings.language);

                waitForEnter(settings.language);
                break;
            }

            case 7:
            {
                runSettingsMenu(&settings, &sessionStats);
                break;
            }

            case 0:
            {
                printSessionSummary(&sessionStats, settings.language);
                printf("\n%s%s%s\n", COLOR_MUTED, getText(settings.language, TXT_EXITING_IN_SECONDS), COLOR_RESET);
                fflush(stdout);
                waitForSeconds(3);
                break;
            }

            default:
            {
                printError(RESULT_INVALID_INPUT, settings.language);
                increaseInvalidInputCount(&sessionStats);
                waitForEnter(settings.language);
                break;
            }
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
