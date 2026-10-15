#ifndef UI_H
#define UI_H

#include <stddef.h>

typedef enum {
    UI_RESET, UI_ACCENT, UI_SUCCESS, UI_ERROR, UI_KEY, UI_TEXT, UI_MUTED
} ConsoleColor;

const char *consoleColor(ConsoleColor color);
#define COLOR_RESET  consoleColor(UI_RESET)
#define COLOR_CYAN   consoleColor(UI_ACCENT)
#define COLOR_GREEN  consoleColor(UI_SUCCESS)
#define COLOR_RED    consoleColor(UI_ERROR)
#define COLOR_YELLOW consoleColor(UI_KEY)
#define COLOR_TEXT   consoleColor(UI_TEXT)
#define COLOR_MUTED  consoleColor(UI_MUTED)

void initializeConsoleUi(void);
void printSectionHeader(const char title[]);
void printSectionRule(void);
void printResult(const char title[], const char content[]);
void printStatLine(const char label[], size_t value);

void clearConsole(void);
void waitForEnter(const char language[]);
void waitForSeconds(int seconds);

int getMenuChoice(const char language[]);
/* 1: complete line, 0: end of input, -1: line exceeds the buffer. */
int readInputLine(char input[], int capacity);

void printMainMenu(char wordSeparator, const char language[]);
void printSettingsMenu(char wordSeparator, const char language[]);

void printSeparatorSavedMessage(char newSeparator, const char language[]);
void printLanguageSavedMessage(const char language[]);

void showAboutProject(const char language[]);
void printPreview(const char title[], const char content[], const char language[]);

int askToSaveOutput(const char output[], const char language[]);

#endif
