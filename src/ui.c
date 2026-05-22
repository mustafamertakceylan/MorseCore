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
#include "language.h"

void clearConsole(void)
{
#ifdef _WIN32
    system("cls");
#else
    system("clear");
#endif
}

void waitForEnter(const char language[])
{
    printf("\n%s", getText(language, TXT_PRESS_ENTER));
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

int getMenuChoice(const char language[])
{
    char input[20];
    int choice;

    printf(COLOR_YELLOW "%s" COLOR_RESET, getText(language, TXT_CHOOSE_OPTION));

    fgets(input, sizeof(input), stdin);

    if (sscanf(input, "%d", &choice) != 1) {
        return -1;
    }

    return choice;
}

void printMainMenu(const char language[])
{
    printf(COLOR_CYAN);
    printf("+---------------------------------------------------+\n");
    printf("|                     %-29s|\n", getText(language, TXT_MAIN_TITLE));
    printf("+---------------------------------------------------+\n");
    printf("| [1] %-47s|\n", getText(language, TXT_MENU_TEXT_TO_MORSE));
    printf("| [2] %-47s|\n", getText(language, TXT_MENU_MORSE_TO_TEXT));
    printf("| [3] %-47s|\n", getText(language, TXT_MENU_FILE_TEXT_TO_MORSE));
    printf("| [4] %-47s|\n", getText(language, TXT_MENU_FILE_MORSE_TO_TEXT));
    printf("| [5] %-47s|\n", getText(language, TXT_MENU_SHOW_TABLE));
    printf("| [6] %-47s|\n", getText(language, TXT_MENU_ABOUT));
    printf("| [7] %-47s|\n", getText(language, TXT_MENU_SETTINGS));
    printf("| [0] %-47s|\n", getText(language, TXT_MENU_EXIT));
    printf("+---------------------------------------------------+\n");
    printf(COLOR_RESET);
}

void printSettingsMenu(char wordSeparator, const char language[])
{
    printf(COLOR_CYAN);
    printf("+---------------------------------------------------+\n");
    printf("|                      %-29s|\n", getText(language, TXT_SETTINGS_TITLE));
    printf("+---------------------------------------------------+\n");
    printf("| %-27s: %-20c|\n", getText(language, TXT_CURRENT_SEPARATOR), wordSeparator);
    printf("| %-27s: %-20s|\n", getText(language, TXT_CURRENT_LANGUAGE), language);
    printf("+---------------------------------------------------+\n");
    printf("| [1] %-47s|\n", getText(language, TXT_CHANGE_SEPARATOR));
    printf("| [2] %-47s|\n", getText(language, TXT_CHANGE_LANGUAGE));
    printf("| [0] %-47s|\n", getText(language, TXT_BACK));
    printf("+---------------------------------------------------+\n");
    printf(COLOR_RESET);
}

void printSeparatorSavedMessage(char newSeparator, const char language[])
{
    printf(COLOR_GREEN "\n%s\n" COLOR_RESET, getText(language, TXT_SEPARATOR_SAVED));
    printf("%s: %c\n", getText(language, TXT_NEW_SEPARATOR), newSeparator);
}

void printLanguageSavedMessage(const char language[])
{
    printf(COLOR_GREEN "\n%s\n" COLOR_RESET, getText(language, TXT_LANGUAGE_SAVED));
    printf("%s: %s\n", getText(language, TXT_NEW_LANGUAGE), language);
}

void showAboutProject(const char language[])
{
    printf(COLOR_CYAN "%s\n" COLOR_RESET, getText(language, TXT_ABOUT_TITLE));
    printf("--------------------------------\n");
    printf("%s\n\n", getText(language, TXT_ABOUT_DESCRIPTION));

    printf("%s\n", getText(language, TXT_ABOUT_DEMONSTRATES));
    printf("- %s\n", getText(language, TXT_ABOUT_2D_ARRAY));
    printf("- %s\n", getText(language, TXT_ABOUT_FILE_IO));
    printf("- %s\n", getText(language, TXT_ABOUT_STRING_OPERATIONS));
    printf("- %s\n", getText(language, TXT_ABOUT_MODULAR_DESIGN));
    printf("- %s\n", getText(language, TXT_ABOUT_HEADER_SOURCE));
    printf("- %s\n", getText(language, TXT_ABOUT_SWITCH_CASE));
    printf("- %s\n", getText(language, TXT_ABOUT_INPUT_VALIDATION));
    printf("- %s\n", getText(language, TXT_ABOUT_TURKISH_NORMALIZATION));
    printf("- %s\n", getText(language, TXT_ABOUT_AUTO_FILE_NAMING));
    printf("- %s\n", getText(language, TXT_ABOUT_PERSISTENT_SETTINGS));
    printf("- %s\n", getText(language, TXT_ABOUT_STRUCT_POINTER));

    printf("--------------------------------\n");
    printf(COLOR_GREEN "%s: " COLOR_RED "Mustafa Mert Akceylan\n" COLOR_RESET,
           getText(language, TXT_ABOUT_AUTHOR));

    printf(COLOR_GREEN "%s: " COLOR_RED "030240117\n" COLOR_RESET,
           getText(language, TXT_ABOUT_STUDENT_NUMBER));
}

void printPreview(const char title[], const char content[])
{
    printf("\n%s\n", title);
    printf("--------------------------------\n");
    printf("%s\n", content);
    printf("--------------------------------\n");
}

int askToSaveOutput(const char output[], const char language[])
{
    char answer;
    char fileName[100];
    char fullPath[200];

    printf("\n%s", getText(language, TXT_DO_YOU_WANT_TO_SAVE));
    scanf(" %c", &answer);
    getchar();

    if (answer == 'y' || answer == 'Y') {
        generateNextFileName(fileName, MORSE_OUTPUT_PREFIX, DEFAULT_FILE_EXTENSION);
        buildDataFilePath(fullPath, fileName);

        if (writeToFile(fullPath, output)) {
            printf(COLOR_GREEN "%s: data/%s\n" COLOR_RESET,
                   getText(language, TXT_OUTPUT_SAVED_SUCCESSFULLY),
                   fileName);
            return 1;
        }

        printError(RESULT_FILE_WRITE_ERROR, language);
        return 0;
    }

    return 0;
}