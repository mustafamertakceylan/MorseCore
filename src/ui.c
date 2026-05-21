#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "ui.h"
#include "morse.h"
#include "file_utils.h"
void clearConsole(void) //Konsol temizleme
{
    system("cls");
}
void waitForEnter(void) // Kullanıcıdan enter girdisi bekleme
{
    printf("\nPress Enter to continue...");
    getchar();
}
int isEmptyInput(const char input[]) //inputun boş olması hatası
{
    return input[0] == '\0';
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
void showAboutProject(void) //Proje hakkında bilgiler
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
    printf("--------------------------------\n");
    printf(COLOR_GREEN "Author: " COLOR_RED "Mustafa Mert Akceylan\n" COLOR_RESET);
    printf(COLOR_GREEN "Student Number: " COLOR_RED "030240117" COLOR_RESET);
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

