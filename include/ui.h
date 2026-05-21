#ifndef UI_H
#define UI_H
#define COLOR_RESET  "\033[0m"
#define COLOR_CYAN   "\033[36m"
#define COLOR_GREEN  "\033[32m"
#define COLOR_RED    "\033[31m"
#define COLOR_YELLOW "\033[33m"
void clearConsole(void);
void waitForEnter(void);
void printPreview(const char title[], const char content[]);
void askToSaveOutput(const char output[]);
int getMenuChoice(void);
void showAboutProject(void);
int isEmptyInput(const char input[]);
#endif