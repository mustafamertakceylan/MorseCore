#include <stdio.h>
#include "morse.h"
#include "file_utils.h"

#define MAX_TEXT_LENGTH 1000
#define MAX_OUTPUT_LENGTH 5000

int main(void)
{
    char text[MAX_TEXT_LENGTH];
    char output[MAX_OUTPUT_LENGTH];
    int choice;

    printf("MorseCore\n");
    printf("1. Text to Morse\n");
    printf("2. Morse to Text\n");
    printf("3. Read text from file and convert to Morse\n");
    printf("4. Read Morse from file and convert to text\n");
    printf("Choose an option: ");
    scanf("%d", &choice);
    getchar();

    if (choice == 1) {
        printf("Enter text: ");
        fgets(text, MAX_TEXT_LENGTH, stdin);

        textToMorse(text, output);

        printf("Morse output:\n%s\n", output);
    } else if (choice == 2) {
        printf("Enter Morse code: ");
        fgets(text, MAX_TEXT_LENGTH, stdin);

        morseToText(text, output);

        printf("Text output:\n%s\n", output);
    } else if (choice == 3) {
        if (readFromFile("data/input.txt", text, MAX_TEXT_LENGTH)) {
            textToMorse(text, output);

            if (writeToFile("data/output.txt", output)) {
                printf("File converted successfully. Check data/output.txt\n");
            } else {
                printf("Output file could not be written.\n");
            }
        } else {
            printf("Input file could not be opened.\n");
        }
    } else if (choice == 4) {
        if (readFromFile("data/input.txt", text, MAX_TEXT_LENGTH)) {
            morseToText(text, output);

            if (writeToFile("data/output.txt", output)) {
                printf("File converted successfully. Check data/output.txt\n");
            } else {
                printf("Output file could not be written.\n");
            }
        } else {
            printf("Input file could not be opened.\n");
        }
    } else {
        printf("Invalid option.\n");
    }

    return 0;
}