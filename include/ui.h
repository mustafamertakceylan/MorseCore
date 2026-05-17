#ifndef UI_H
#define UI_H
void runApplication(void);
void clearConsole(void);
void waitForEnter(void);
void printPreview(const char title[], const char content[]);
void askToSaveOutput(const char output[]);
int getMenuChoice(void);
void showAboutProject(void);
int isEmptyInput(const char input[]);
#endif