#ifdef _WIN32
#include <windows.h>
#else
#include <unistd.h>
#endif

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <ctype.h>
#include <errno.h>
#include <limits.h>
#include "constants.h"

#include "ui.h"
#include "file_utils.h"
#include "app_config.h"
#include "error.h"
#include "language.h"

static int consoleInitialized = 0;
static int interactiveConsole = 0;
static int ansiConsole = 0;
static int colorsEnabled = 0;

void initializeConsoleUi(void)
{
    if (consoleInitialized) return;
    consoleInitialized = 1;
#ifdef _WIN32
    HANDLE output = GetStdHandle(STD_OUTPUT_HANDLE);
    DWORD mode;
    interactiveConsole = GetConsoleMode(output, &mode) != 0;
    if (interactiveConsole) {
        ansiConsole = SetConsoleMode(output, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING) != 0;
    }
#else
    interactiveConsole = isatty(STDOUT_FILENO);
    const char *term = getenv("TERM");
    ansiConsole = interactiveConsole && (term == NULL || strcmp(term, "dumb") != 0);
#endif
    colorsEnabled = ansiConsole && getenv("NO_COLOR") == NULL;
}

const char *consoleColor(ConsoleColor color)
{
    static const char *palette[] = {
        "\033[0m", "\033[1;96m", "\033[92m", "\033[91m", "\033[93m", "\033[97m", "\033[90m"
    };
    initializeConsoleUi();
    return colorsEnabled ? palette[color] : "";
}

void printSectionRule(void)
{
    printf("%s  ------------------------------------------------------------%s\n", COLOR_MUTED, COLOR_RESET);
}

void printSectionHeader(const char title[])
{
    printf("\n%s  %s%s\n", COLOR_CYAN, title, COLOR_RESET);
    printSectionRule();
}

void printResult(const char title[], const char content[])
{
    printf("\n%s%s%s\n%s%s%s\n", COLOR_GREEN, title, COLOR_RESET, COLOR_TEXT, content, COLOR_RESET);
}

void printStatLine(const char label[], int value)
{
    printf("%s%s:%s %s%d%s\n", COLOR_MUTED, label, COLOR_RESET, COLOR_TEXT, value, COLOR_RESET);
}

void clearConsole(void)
{
    initializeConsoleUi();
    if (!interactiveConsole) return;
    if (ansiConsole) {
        fputs("\033[2J\033[H", stdout);
        return;
    }
#ifdef _WIN32
    system("cls");
#endif
}

void waitForEnter(const char language[])
{
    printf("\n%s  [Enter]%s %s%s%s", COLOR_YELLOW, COLOR_RESET,
           COLOR_MUTED, getText(language, TXT_PRESS_ENTER), COLOR_RESET);
    fflush(stdout);
    getchar();
}

void waitForSeconds(int seconds)
{
    if (seconds <= 0) return;
#ifdef _WIN32
    Sleep((DWORD) seconds * 1000U);
#else
    sleep((unsigned int) seconds);
#endif
}

int readInputLine(char input[], int capacity)
{
    if (fgets(input, capacity, stdin) == NULL) {
        input[0] = '\0';
        return 0;
    }
    size_t length = strcspn(input, "\n");
    if (input[length] == '\n') {
        input[length] = '\0';
        if (length > 0 && input[length - 1] == '\r') input[length - 1] = '\0';
        return 1;
    }
    int next = getchar();
    if (next == '\n' || next == EOF) return 1;
    while (next != '\n' && next != EOF) next = getchar();
    input[0] = '\0';
    return -1;
}

int getMenuChoice(const char language[])
{
    char input[20];
    char *end;

    printf("\n%s  > %s%s%s", COLOR_YELLOW, COLOR_TEXT, getText(language, TXT_CHOOSE_OPTION), COLOR_RESET);
    fflush(stdout);

    int status = readInputLine(input, sizeof(input));
    if (status == 0) return 0;
    if (status < 0) return -1;
    errno = 0;
    long choice = strtol(input, &end, 10);
    if (end == input || errno == ERANGE || choice < 0 || choice > INT_MAX) {
        return -1;
    }
    while (isspace((unsigned char) *end)) end++;
    if (*end != '\0') return -1;

    return (int) choice;
}

static void printPanelRule(const char title[])
{
    printf("%s  +", COLOR_MUTED);
    int used = 0;
    if (title[0] != '\0') {
        printf("-- %s%s%s ", COLOR_CYAN, title, COLOR_MUTED);
        used = (int) strlen(title) + 4;
    }
    for (int i = used; i < 60; i++) putchar('-');
    printf("+%s\n", COLOR_RESET);
}

static void printMenuItem(char key, const char label[])
{
    const char *keyColor = key == '0' ? COLOR_MUTED : COLOR_YELLOW;
    printf("%s  |%s  %s[%c]%s  %-52s %s|%s\n", COLOR_MUTED, COLOR_RESET,
           keyColor, key, COLOR_TEXT, label, COLOR_MUTED, COLOR_RESET);
}

static void printBanner(void)
{
    static const char *morse[] = {
        "M   M  OOO  RRRR   SSS  EEEEE",
        "MM MM O   O R   R S     E    ",
        "M M M O   O RRRR   SSS  EEEE ",
        "M   M O   O R R       S E    ",
        "M   M  OOO  R  RR  SSS  EEEEE"
    };
    static const char *core[] = {
        " CCC   OOO  RRRR  EEEEE",
        "C   C O   O R   R E    ",
        "C     O   O RRRR  EEEE ",
        "C   C O   O R R   E    ",
        " CCC   OOO  R  RR EEEEE"
    };
    putchar('\n');
    for (size_t i = 0; i < 5; i++) {
        printf("  %s%s   %s%s%s\n", COLOR_CYAN, morse[i], COLOR_TEXT, core[i], COLOR_RESET);
    }
    printf("%s  -- --- .-. ... . / -.-. --- .-. .%s\n\n", COLOR_MUTED, COLOR_RESET);
}

void printMainMenu(char wordSeparator, const char language[])
{
    printBanner();
    printPanelRule(getText(language, TXT_SECTION_CONVERT));
    printMenuItem('1', getText(language, TXT_MENU_TEXT_TO_MORSE));
    printMenuItem('2', getText(language, TXT_MENU_MORSE_TO_TEXT));
    printPanelRule(getText(language, TXT_SECTION_FILES));
    printMenuItem('3', getText(language, TXT_MENU_FILE_TEXT_TO_MORSE));
    printMenuItem('4', getText(language, TXT_MENU_FILE_MORSE_TO_TEXT));
    printPanelRule(getText(language, TXT_SECTION_TOOLS));
    printMenuItem('5', getText(language, TXT_MENU_SHOW_TABLE));
    printMenuItem('6', getText(language, TXT_MENU_ABOUT));
    printMenuItem('7', getText(language, TXT_MENU_SETTINGS));
    printPanelRule("");
    printMenuItem('0', getText(language, TXT_MENU_EXIT));
    printPanelRule("");
    printf("%s  %s: %s%c%s    |    %s: %s%s%s\n", COLOR_MUTED,
           getText(language, TXT_CURRENT_SEPARATOR_SHORT), COLOR_TEXT, wordSeparator,
           COLOR_MUTED, getText(language, TXT_CURRENT_LANGUAGE), COLOR_TEXT, language, COLOR_RESET);
}

void printSettingsMenu(char wordSeparator, const char language[])
{
    printSectionHeader(getText(language, TXT_SETTINGS_TITLE));
    printf("  %s%-27s%s %s%c%s\n", COLOR_MUTED, getText(language, TXT_CURRENT_SEPARATOR),
           COLOR_RESET, COLOR_TEXT, wordSeparator, COLOR_RESET);
    printf("  %s%-27s%s %s%s%s\n\n", COLOR_MUTED, getText(language, TXT_CURRENT_LANGUAGE),
           COLOR_RESET, COLOR_TEXT, language, COLOR_RESET);
    printPanelRule("");
    printMenuItem('1', getText(language, TXT_CHANGE_SEPARATOR));
    printMenuItem('2', getText(language, TXT_CHANGE_LANGUAGE));
    printMenuItem('0', getText(language, TXT_BACK));
    printPanelRule("");
}

void printSeparatorSavedMessage(char newSeparator, const char language[])
{
    printf("\n%s[+] %s%s\n", COLOR_GREEN, getText(language, TXT_SEPARATOR_SAVED), COLOR_RESET);
    printf("%s: %c\n", getText(language, TXT_NEW_SEPARATOR), newSeparator);
}

void printLanguageSavedMessage(const char language[])
{
    printf("\n%s[+] %s%s\n", COLOR_GREEN, getText(language, TXT_LANGUAGE_SAVED), COLOR_RESET);
    printf("%s: %s\n", getText(language, TXT_NEW_LANGUAGE), language);
}

void showAboutProject(const char language[])
{
    printSectionHeader(getText(language, TXT_ABOUT_TITLE));
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

    printSectionRule();
    printf("%s%s:%s %sMustafa Mert Akceylan%s\n", COLOR_MUTED,
           getText(language, TXT_ABOUT_AUTHOR), COLOR_RESET, COLOR_TEXT, COLOR_RESET);
    printf("%s%s:%s %s030240117%s\n", COLOR_MUTED,
           getText(language, TXT_ABOUT_STUDENT_NUMBER), COLOR_RESET, COLOR_TEXT, COLOR_RESET);
}

void printPreview(const char title[], const char content[])
{
    printSectionHeader(title);
    printf("%s%s%s\n", COLOR_TEXT, content, COLOR_RESET);
    printSectionRule();
}

int askToSaveOutput(const char output[], const char language[])
{
    char answer[20];
    char fileName[100];
    char fullPath[200];

    printf("\n%s  > %s%s%s", COLOR_YELLOW, COLOR_TEXT, getText(language, TXT_DO_YOU_WANT_TO_SAVE), COLOR_RESET);
    fflush(stdout);
    if (readInputLine(answer, sizeof(answer)) != 1) return 0;

    if (strcmp(answer, "y") == 0 || strcmp(answer, "Y") == 0) {
        generateNextFileName(fileName, MORSE_OUTPUT_PREFIX, DEFAULT_FILE_EXTENSION);
        buildDataFilePath(fullPath, fileName);

        if (writeToFile(fullPath, output)) {
            printf("%s[+] %s: data/%s%s\n", COLOR_GREEN,
                   getText(language, TXT_OUTPUT_SAVED_SUCCESSFULLY),
                   fileName, COLOR_RESET);
            return 1;
        }

        printError(RESULT_FILE_WRITE_ERROR, language);
        return 0;
    }

    return 0;
}
