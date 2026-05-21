#include <stdio.h>
#include <string.h>

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

        printMainMenu();

        choice = getMenuChoice();
        clearConsole();

        switch (choice) {
            case 1:
            {
                printf(COLOR_CYAN "Text to Morse\n" COLOR_RESET);
                printf("Enter text: ");
                fgets(text, MAX_TEXT_LENGTH, stdin);

                if (textToMorse(text, output, settings.wordSeparator)) {
                    printf(COLOR_GREEN "\nMorse output:\n" COLOR_RESET);
                    printf("%s\n", output);

                    calculateConversionStats(text, output, &conversionStats);
                    printConversionStats(&conversionStats);
                    increaseTextToMorseCount(&sessionStats);

                    if (askToSaveOutput(output)) {
                        increaseSavedOutputCount(&sessionStats);
                    }
                } else {
                    printError(RESULT_UNSUPPORTED_CHARACTER);
                    increaseInvalidInputCount(&sessionStats);
                }

                waitForEnter();
                break;
            }

            case 2:
            {
                char activeSeparator;

                printf(COLOR_CYAN "Morse to Text\n" COLOR_RESET);
                printf("Enter Morse code: ");
                fgets(text, MAX_TEXT_LENGTH, stdin);

                activeSeparator = resolveSeparatorForMorseInput(text, settings.wordSeparator);

                if (morseToText(text, output, activeSeparator)) {
                    printf(COLOR_GREEN "\nText output:\n" COLOR_RESET);
                    printf("%s\n", output);
                    printf("Detected/active separator: %c\n", activeSeparator);

                    calculateConversionStats(text, output, &conversionStats);
                    printConversionStats(&conversionStats);
                    increaseMorseToTextCount(&sessionStats);
                } else {
                    printError(RESULT_INVALID_MORSE);
                    increaseInvalidInputCount(&sessionStats);
                }

                waitForEnter();
                break;
            }

            case 3:
            {
                char inputFileName[MAX_FILE_NAME_LENGTH];
                char inputPath[MAX_FILE_PATH_LENGTH];
                char outputFileName[MAX_FILE_NAME_LENGTH];
                char outputPath[MAX_FILE_PATH_LENGTH];

                printf(COLOR_CYAN "File Text to Morse\n" COLOR_RESET);

                printf("Enter input file name from data folder: ");
                fgets(inputFileName, sizeof(inputFileName), stdin);
                inputFileName[strcspn(inputFileName, "\n")] = '\0';

                if (isEmptyInput(inputFileName)) {
                    printError(RESULT_EMPTY_INPUT);
                    increaseInvalidInputCount(&sessionStats);
                    waitForEnter();
                    break;
                }

                if (!isValidFileName(inputFileName)) {
                    printError(RESULT_INVALID_INPUT);
                    increaseInvalidInputCount(&sessionStats);
                    waitForEnter();
                    break;
                }

                ensureTxtExtension(inputFileName, MAX_FILE_NAME_LENGTH);
                buildDataFilePath(inputPath, inputFileName);

                if (readFromFile(inputPath, text, MAX_TEXT_LENGTH)) {
                    if (textToMorse(text, output, settings.wordSeparator)) {
                        printPreview("Input file preview:", text);
                        printPreview("Morse output preview:", output);

                        generateNextFileName(outputFileName, MORSE_OUTPUT_PREFIX, DEFAULT_FILE_EXTENSION);
                        buildDataFilePath(outputPath, outputFileName);

                        if (writeToFile(outputPath, output)) {
                            printf(COLOR_GREEN "\nFile converted successfully: data/%s\n" COLOR_RESET, outputFileName);

                            calculateConversionStats(text, output, &conversionStats);
                            printConversionStats(&conversionStats);

                            increaseTextToMorseCount(&sessionStats);
                            increaseFileOperationCount(&sessionStats);
                            increaseSavedOutputCount(&sessionStats);
                        } else {
                            printError(RESULT_FILE_WRITE_ERROR);
                            increaseInvalidInputCount(&sessionStats);
                        }
                    } else {
                        printError(RESULT_UNSUPPORTED_CHARACTER);
                        increaseInvalidInputCount(&sessionStats);
                    }
                } else {
                    printError(RESULT_FILE_OPEN_ERROR);
                    increaseInvalidInputCount(&sessionStats);
                }

                waitForEnter();
                break;
            }

            case 4:
            {
                char inputFileName[MAX_FILE_NAME_LENGTH];
                char inputPath[MAX_FILE_PATH_LENGTH];
                char outputFileName[MAX_FILE_NAME_LENGTH];
                char outputPath[MAX_FILE_PATH_LENGTH];
                char activeSeparator;

                printf(COLOR_CYAN "File Morse to Text\n" COLOR_RESET);

                printf("Enter Morse file name from data folder: ");
                fgets(inputFileName, sizeof(inputFileName), stdin);
                inputFileName[strcspn(inputFileName, "\n")] = '\0';

                if (isEmptyInput(inputFileName)) {
                    printError(RESULT_EMPTY_INPUT);
                    increaseInvalidInputCount(&sessionStats);
                    waitForEnter();
                    break;
                }

                if (!isValidFileName(inputFileName)) {
                    printError(RESULT_INVALID_INPUT);
                    increaseInvalidInputCount(&sessionStats);
                    waitForEnter();
                    break;
                }

                ensureTxtExtension(inputFileName, MAX_FILE_NAME_LENGTH);
                buildDataFilePath(inputPath, inputFileName);

                if (readFromFile(inputPath, text, MAX_TEXT_LENGTH)) {
                    activeSeparator = resolveSeparatorForMorseInput(text, settings.wordSeparator);

                    if (morseToText(text, output, activeSeparator)) {
                        printf("Detected/active separator: %c\n", activeSeparator);

                        printPreview("Morse input file preview:", text);
                        printPreview("Decoded text preview:", output);

                        generateNextFileName(outputFileName, DECODED_OUTPUT_PREFIX, DEFAULT_FILE_EXTENSION);
                        buildDataFilePath(outputPath, outputFileName);

                        if (writeToFile(outputPath, output)) {
                            printf(COLOR_GREEN "\nFile converted successfully: data/%s\n" COLOR_RESET, outputFileName);

                            calculateConversionStats(text, output, &conversionStats);
                            printConversionStats(&conversionStats);

                            increaseMorseToTextCount(&sessionStats);
                            increaseFileOperationCount(&sessionStats);
                            increaseSavedOutputCount(&sessionStats);
                        } else {
                            printError(RESULT_FILE_WRITE_ERROR);
                            increaseInvalidInputCount(&sessionStats);
                        }
                    } else {
                        printError(RESULT_INVALID_MORSE);
                        increaseInvalidInputCount(&sessionStats);
                    }
                } else {
                    printError(RESULT_FILE_OPEN_ERROR);
                    increaseInvalidInputCount(&sessionStats);
                }

                waitForEnter();
                break;
            }

            case 5:
            {
                printf(COLOR_CYAN "Morse Table\n" COLOR_RESET);

                printMorseTable();

                waitForEnter();
                break;
            }

            case 6:
            {
                showAboutProject();

                waitForEnter();
                break;
            }

            case 7:
            {
                runSettingsMenu(&settings, &sessionStats);
                break;
            }

            case 0:
            {
                printSessionSummary(&sessionStats);
                printf(COLOR_GREEN "\nExiting MorseCore in 3 seconds...\n" COLOR_RESET);
                fflush(stdout);
                waitForSeconds(3);
                break;
            }

            default:
            {
                printError(RESULT_INVALID_INPUT);
                increaseInvalidInputCount(&sessionStats);
                waitForEnter();
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

        settingsChoice = getMenuChoice();
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
                printError(RESULT_INVALID_INPUT);
                increaseInvalidInputCount(sessionStats);
                waitForEnter();
                break;
            }
        }

    } while (settingsChoice != 0);
}

static void changeWordSeparator(AppSettings *settings, SessionStats *sessionStats)
{
    char newSeparator;

    printf(COLOR_CYAN "Change Word Separator\n" COLOR_RESET);
    printf("--------------------------------\n");
    printf("Current separator: %c\n", settings->wordSeparator);
    printf("Allowed separators: / | # *\n");
    printf("--------------------------------\n");
    printf("Enter new separator: ");

    scanf(" %c", &newSeparator);
    getchar();

    if (!isValidSeparator(newSeparator)) {
        printError(RESULT_INVALID_SEPARATOR);
        increaseInvalidInputCount(sessionStats);
        waitForEnter();
        return;
    }

    settings->wordSeparator = newSeparator;

    if (saveSettings(settings)) {
        printSeparatorSavedMessage(settings->wordSeparator);
    } else {
        printError(RESULT_SETTINGS_SAVE_ERROR);
        increaseInvalidInputCount(sessionStats);
    }

    waitForEnter();
}
static void changeLanguage(AppSettings *settings, SessionStats *sessionStats)
{
    char newLanguage[10];

    printf(COLOR_CYAN "Change Language\n" COLOR_RESET);
    printf("--------------------------------\n");
    printf("Current language: %s\n", settings->language);
    printf("Allowed languages: EN / TR\n");
    printf("--------------------------------\n");
    printf("Enter new language: ");

    scanf("%9s", newLanguage);
    getchar();

    if (strcmp(newLanguage, "EN") != 0 && strcmp(newLanguage, "TR") != 0) {
        printError(RESULT_INVALID_INPUT);
        increaseInvalidInputCount(sessionStats);
        waitForEnter();
        return;
    }

    strcpy(settings->language, newLanguage);

    if (saveSettings(settings)) {
        printLanguageSavedMessage(settings->language);
    } else {
        printError(RESULT_SETTINGS_SAVE_ERROR);
        increaseInvalidInputCount(sessionStats);
    }

    waitForEnter();
}