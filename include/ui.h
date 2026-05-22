#ifndef UI_H
#define UI_H

#define COLOR_RESET  "\033[0m"
#define COLOR_CYAN   "\033[36m"
#define COLOR_GREEN  "\033[32m"
#define COLOR_RED    "\033[31m"
#define COLOR_YELLOW "\033[33m"

void clearConsole(void);
void waitForEnter(const char language[]);
void waitForSeconds(int seconds);

int getMenuChoice(const char language[]);

void printMainMenu(const char language[]);
void printSettingsMenu(char wordSeparator, const char language[]);

void printSeparatorSavedMessage(char newSeparator, const char language[]);
void printLanguageSavedMessage(const char language[]);

void showAboutProject(const char language[]);
void printPreview(const char title[], const char content[]);

int askToSaveOutput(const char output[], const char language[]);

#endif