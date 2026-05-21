#ifdef _WIN32
#include <windows.h>
#else
#include <unistd.h>
#endif

#include <stdio.h>
#include <stdlib.h>

#include "ui.h"
#include "file_utils.h"
#include "app_config.h"
#include "error.h"

void clearConsole(void)
{
#ifdef _WIN32
    system("cls");
#else
    system("clear");
#endif
}

void waitForEnter(void)
{
    printf("\nPress Enter to continue...");
    getchar();
}

void waitForSeconds(int seconds)
{
#ifdef _WIN32
    Sleep(seconds * 1000);
#else
    sleep(seconds);
#endif
}

int getMenuChoice(void)
{
    char input[20];
    int choice;

    printf(COLOR_YELLOW "Choose an option: " COLOR_RESET);

    fgets(input, sizeof(input), stdin);

    if (sscanf(input, "%d", &choice) != 1) {
        return -1;
    }

    return choice;
}

void printMainMenu(void)
{
    printf(COLOR_CYAN);
    printf("+---------------------------------------------------+\n");
    printf("|                     MorseCore                     |\n");
    printf("+---------------------------------------------------+\n");
    printf("| [1] Text to Morse                                 |\n");
    printf("| [2] Morse to Text                                 |\n");
    printf("| [3] Read text from file and convert to Morse      |\n");
    printf("| [4] Read Morse from file and convert to Text      |\n");
    printf("| [5] Show Morse Table                              |\n");
    printf("| [6] About Project                                 |\n");
    printf("| [7] Settings                                      |\n");
    printf("| [0] Exit                                          |\n");
    printf("+---------------------------------------------------+\n");
    printf(COLOR_RESET);
}

void printSettingsMenu(char wordSeparator, const char language[])
{
    printf(COLOR_CYAN);
    printf("+---------------------------------------------------+\n");
    printf("|                      Settings                     |\n");
    printf("+---------------------------------------------------+\n");
    printf("| Current word separator: %-26c |\n", wordSeparator);
    printf("| Current language      : %-26s |\n", language);
    printf("+---------------------------------------------------+\n");
    printf("| [1] Change word separator                         |\n");
    printf("| [2] Change language                               |\n");
    printf("| [0] Back                                          |\n");
    printf("+---------------------------------------------------+\n");
    printf(COLOR_RESET);
}
void printLanguageSavedMessage(const char language[])
{
    printf(COLOR_GREEN "\nLanguage saved successfully.\n" COLOR_RESET);
    printf("New language: %s\n", language);
}
void printSeparatorSavedMessage(char newSeparator)
{
    printf(COLOR_GREEN "\nSeparator saved successfully.\n" COLOR_RESET);
    printf("New separator: %c\n", newSeparator);
}

void showAboutProject(void)
{
    printf(COLOR_CYAN "About MorseCore\n" COLOR_RESET);
    printf("--------------------------------\n");
    printf("MorseCore is a C-based Morse encoder and decoder.\n\n");

    printf("This project demonstrates:\n");
    printf("- 2D array usage\n");
    printf("- File input and output\n");
    printf("- String operations\n");
    printf("- Function-based modular design\n");
    printf("- Header/source file separation\n");
    printf("- Switch-case menu structure\n");
    printf("- Input validation\n");
    printf("- Turkish character normalization\n");
    printf("- Automatic output file naming\n");
    printf("- Persistent separator settings\n");
    printf("- Struct and pointer usage through statistics\n");

    printf("--------------------------------\n");
    printf(COLOR_GREEN "Author: " COLOR_RED "Mustafa Mert Akceylan\n" COLOR_RESET);
    printf(COLOR_GREEN "Student Number: " COLOR_RED "030240117\n" COLOR_RESET);
}

void printPreview(const char title[], const char content[])
{
    printf("\n%s\n", title);
    printf("--------------------------------\n");
    printf("%s\n", content);
    printf("--------------------------------\n");
}

int askToSaveOutput(const char output[])
{
    char answer;
    char fileName[100];
    char fullPath[200];

    printf("\nDo you want to save this output? (y/n): ");
    scanf(" %c", &answer);
    getchar();

    if (answer == 'y' || answer == 'Y') {
        generateNextFileName(fileName, MORSE_OUTPUT_PREFIX, DEFAULT_FILE_EXTENSION);
        buildDataFilePath(fullPath, fileName);

        if (writeToFile(fullPath, output)) {
            printf(COLOR_GREEN "Output saved successfully: data/%s\n" COLOR_RESET, fileName);
            return 1;
        }

        printError(RESULT_FILE_WRITE_ERROR);
        return 0;
    }

    return 0;
}