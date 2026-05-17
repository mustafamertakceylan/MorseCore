#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "ui.h"
#include "morse.h"
#include "file_utils.h"
#define MAX_TEXT_LENGTH 1000
#define MAX_OUTPUT_LENGTH 5000
#define COLOR_RESET  "\033[0m"
#define COLOR_CYAN   "\033[36m"
#define COLOR_GREEN  "\033[32m"
#define COLOR_RED    "\033[31m"
#define COLOR_YELLOW "\033[33m"
void clearConsole(void)
{
    system("cls");
}
void waitForEnter(void)
{
    printf("\nPress Enter to continue...");
    getchar();
}
void printPreview(const char title[], const char content[])
{
    printf("\n%s\n", title);
    printf("--------------------------------\n");
    printf("%s\n", content);
    printf("--------------------------------\n");
}
void askToSaveOutput(const char output[])
{
    char answer;
    char fileName[100];
    char fullPath[200];

    printf("\nDo you want to save this output? (y/n): ");
    scanf(" %c", &answer);
    getchar();

    if (answer == 'y' || answer == 'Y') {
        generateNextFileName(fileName, "morse_output", "txt");
        buildDataFilePath(fullPath, fileName);

        if (writeToFile(fullPath, output)) {
            printf(COLOR_GREEN "Output saved successfully: data/%s\n" COLOR_RESET, fileName);
        } else {
            printf(COLOR_RED "Output file could not be written.\n" COLOR_RESET);
        }
    }
}
void runApplication(void)
{
    char text[MAX_TEXT_LENGTH];
    char output[MAX_OUTPUT_LENGTH];
    int choice;
    do {
        clearConsole();
        printf(COLOR_CYAN);
        printf("+---------------------------------------------------+\n");
        printf("|                     MorseCore                     |\n");
        printf("+---------------------------------------------------+\n");
        printf("| [1] Text to Morse                                 |\n");
        printf("| [2] Morse to Text                                 |\n");
        printf("| [3] Read text from file and convert to Morse      |\n");
        printf("| [4] Read Morse from file and convert to Text      |\n");
        printf("| [0] Exit                                          |\n");
        printf("+---------------------------------------------------+\n");
        printf(COLOR_RESET);
        printf(COLOR_YELLOW "Choose an option: " COLOR_RESET);
        scanf("%d", &choice);
        getchar();
        clearConsole();
        switch (choice) {
            case 1:
            {
                printf(COLOR_CYAN "Text to Morse\n" COLOR_RESET);
                printf("Enter text: ");
                fgets(text, MAX_TEXT_LENGTH, stdin);

                if (textToMorse(text, output)) {
                    printf(COLOR_GREEN "\nMorse output:\n" COLOR_RESET);
                    printf("%s\n", output);

                    askToSaveOutput(output);
                } else {
                    printf(COLOR_RED "\nUnsupported character detected. Only letters, numbers, spaces, and Turkish letters are supported.\n" COLOR_RESET);
                }

                waitForEnter();
                break;
            }

            case 2:
            {
                printf(COLOR_CYAN "Morse to Text\n" COLOR_RESET);
                printf("Enter Morse code: ");
                fgets(text, MAX_TEXT_LENGTH, stdin);

                if (morseToText(text, output)) {
                    printf(COLOR_GREEN "\nText output:\n" COLOR_RESET);
                    printf("%s\n", output);
                } else {
                    printf(COLOR_RED "\nInvalid or empty Morse input.\n" COLOR_RESET);
                }
                waitForEnter();
                break;
            }

            case 3:
            {
                char inputFileName[100];
                char inputPath[200];
                char outputFileName[100];
                char outputPath[200];

                printf(COLOR_CYAN "File Text to Morse\n" COLOR_RESET);

                printf("Enter input file name from data folder: ");
                fgets(inputFileName, sizeof(inputFileName), stdin);
                inputFileName[strcspn(inputFileName, "\n")] = '\0';

                buildDataFilePath(inputPath, inputFileName);

                if (readFromFile(inputPath, text, MAX_TEXT_LENGTH)) {
                    if (textToMorse(text, output)) {
                        printPreview("Input file preview:", text);
                        printPreview("Morse output preview:", output);

                        generateNextFileName(outputFileName, "morse_output", "txt");
                        buildDataFilePath(outputPath, outputFileName);

                        if (writeToFile(outputPath, output)) {
                            printf(COLOR_GREEN "\nFile converted successfully: data/%s\n" COLOR_RESET, outputFileName);
                        } else {
                            printf(COLOR_RED "\nOutput file could not be written.\n" COLOR_RESET);
                        }
                    } else {
                        printf(COLOR_RED "\nUnsupported character detected in input file.\n" COLOR_RESET);
                    }
                } else {
                    printf(COLOR_RED "\nInput file could not be opened.\n" COLOR_RESET);
                }

                waitForEnter();
                break;
            }
            case 4:
            {
                char inputFileName[100];
                char inputPath[200];
                char outputFileName[100];
                char outputPath[200];

                printf(COLOR_CYAN "File Morse to Text\n" COLOR_RESET);

                printf("Enter Morse file name from data folder: ");
                fgets(inputFileName, sizeof(inputFileName), stdin);
                inputFileName[strcspn(inputFileName, "\n")] = '\0';

                buildDataFilePath(inputPath, inputFileName);

                if (readFromFile(inputPath, text, MAX_TEXT_LENGTH)) {
                    if (morseToText(text, output)) {
                        printPreview("Morse input file preview:", text);
                        printPreview("Decoded text preview:", output);

                        generateNextFileName(outputFileName, "decoded_output", "txt");
                        buildDataFilePath(outputPath, outputFileName);

                        if (writeToFile(outputPath, output)) {
                            printf(COLOR_GREEN "\nFile converted successfully: data/%s\n" COLOR_RESET, outputFileName);
                        } else {
                            printf(COLOR_RED "\nDecoded output file could not be written.\n" COLOR_RESET);
                        }
                    } else {
                        printf(COLOR_RED "\nInvalid or empty Morse input file.\n" COLOR_RESET);
                    }
                } else {
                    printf(COLOR_RED "\nMorse input file could not be opened.\n" COLOR_RESET);
                }

                waitForEnter();
                break;
            }
            case 0:
                printf(COLOR_GREEN "Exiting MorseCore...\n" COLOR_RESET);
                break;

            default:
                printf(COLOR_RED "Invalid option. Please try again.\n" COLOR_RESET);
                waitForEnter();
                break;
        }

    } while (choice != 0);
}