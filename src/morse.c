#include <stdio.h>
#include <string.h>
#include <ctype.h>
#include "morse.h"

#define MORSE_COUNT 36
#define MAX_MORSE_LENGTH 6

char characters[MORSE_COUNT] = {
    'A','B','C','D','E','F','G','H','I','J','K','L','M',
    'N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
    '0','1','2','3','4','5','6','7','8','9'
};

char morseCodes[MORSE_COUNT][MAX_MORSE_LENGTH] = {
    ".-", "-...", "-.-.", "-..", ".", "..-.", "--.", "....", "..",
    ".---", "-.-", ".-..", "--", "-.", "---", ".--.", "--.-", ".-.",
    "...", "-", "..-", "...-", ".--", "-..-", "-.--", "--..",
    "-----", ".----", "..---", "...--", "....-", ".....",
    "-....", "--...", "---..", "----."
};
unsigned char normalizeTurkishCharacter(const char text[], int *index)
{
    unsigned char currentChar = (unsigned char) text[*index];

    // UTF-8 Turkish characters
    if (currentChar == 0xC3) {
        (*index)++;
        unsigned char nextChar = (unsigned char) text[*index];

        if (nextChar == 0x87 || nextChar == 0xA7) return 'C'; // Ç ç
        if (nextChar == 0x96 || nextChar == 0xB6) return 'O'; // Ö ö
        if (nextChar == 0x9C || nextChar == 0xBC) return 'U'; // Ü ü

        return 0;
    }

    if (currentChar == 0xC4) {
        (*index)++;
        unsigned char nextChar = (unsigned char) text[*index];

        if (nextChar == 0x9E || nextChar == 0x9F) return 'G'; // Ğ ğ
        if (nextChar == 0xB0 || nextChar == 0xB1) return 'I'; // İ ı

        return 0;
    }

    if (currentChar == 0xC5) {
        (*index)++;
        unsigned char nextChar = (unsigned char) text[*index];

        if (nextChar == 0x9E || nextChar == 0x9F) return 'S'; // Ş ş

        return 0;
    }

    // Windows-1254 Turkish characters
    if (currentChar == 0xC7 || currentChar == 0xE7) return 'C'; // Ç ç
    if (currentChar == 0xD0 || currentChar == 0xF0) return 'G'; // Ğ ğ
    if (currentChar == 0xDD || currentChar == 0xFD) return 'I'; // İ ı
    if (currentChar == 0xD6 || currentChar == 0xF6) return 'O'; // Ö ö
    if (currentChar == 0xDE || currentChar == 0xFE) return 'S'; // Ş ş
    if (currentChar == 0xDC || currentChar == 0xFC) return 'U'; // Ü ü

    return (unsigned char) toupper(currentChar);
}
int textToMorse(const char text[], char morseOutput[])
{
    morseOutput[0] = '\0';

    int convertedCharacterCount = 0;
    for (int i = 0; text[i] != '\0'; i++) {
        unsigned char currentChar = (unsigned char) text[i];

        if (currentChar == '\n' || currentChar == '\r' || currentChar == '\t') {
            continue;
        }
        if (currentChar == ' ') {
            strcat(morseOutput, "/ ");
            continue;
        }
        currentChar = normalizeTurkishCharacter(text, &i);

        if (currentChar == 0) {
            morseOutput[0] = '\0';
            return 0;
        }

        int found = 0;

        for (int j = 0; j < MORSE_COUNT; j++) {
            if (currentChar == characters[j]) {
                strcat(morseOutput, morseCodes[j]);
                strcat(morseOutput, " ");
                convertedCharacterCount++;
                found = 1;
                break;
            }
        }

        if (!found) {
            morseOutput[0] = '\0';
            return 0;
        }
    }

    if (convertedCharacterCount == 0) {
        return 0;
    }

    return 1;
}
int morseToText(const char morseInput[], char textOutput[])
{
    char temp[1000];
    char *token;
    int convertedCharacterCount = 0;

    textOutput[0] = '\0';

    strcpy(temp, morseInput);

    token = strtok(temp, " ");

    while (token != NULL) {
        if (strcmp(token, "/") == 0) {
            strcat(textOutput, " ");
        } else {
            int found = 0;

            for (int i = 0; i < MORSE_COUNT; i++) {
                if (strcmp(token, morseCodes[i]) == 0) {
                    int len = strlen(textOutput);

                    textOutput[len] = characters[i];
                    textOutput[len + 1] = '\0';

                    convertedCharacterCount++;
                    found = 1;
                    break;
                }
            }

            if (!found) {
                textOutput[0] = '\0';
                return 0;
            }
        }

        token = strtok(NULL, " ");
    }

    if (convertedCharacterCount == 0) {
        return 0;
    }

    return 1;
}
void printMorseTable(void)
{
    printf("\nMorse Table\n");
    printf("-------------------------\n");

    for (int i = 0; i < MORSE_COUNT; i++) {
        printf("%c  ->  %s\n", characters[i], morseCodes[i]);
    }

    printf("-------------------------\n");
}