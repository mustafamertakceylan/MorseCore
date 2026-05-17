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

void textToMorse(const char text[], char morseOutput[])
{
    morseOutput[0] = '\0';

    for (int i = 0; text[i] != '\0'; i++) {
        char currentChar = toupper(text[i]);

        if (currentChar == ' ') {
            strcat(morseOutput, "/ ");
            continue;
        }

        for (int j = 0; j < MORSE_COUNT; j++) {
            if (currentChar == characters[j]) {
                strcat(morseOutput, morseCodes[j]);
                strcat(morseOutput, " ");
                break;
            }
        }
    }
}

void morseToText(const char morseInput[], char textOutput[])
{
    char temp[1000];
    char *token;

    textOutput[0] = '\0';

    strcpy(temp, morseInput);
    token = strtok(temp, " ");

    while (token != NULL) {
        if (strcmp(token, "/") == 0) {
            strcat(textOutput, " ");
        } else {
            for (int i = 0; i < MORSE_COUNT; i++) {
                if (strcmp(token, morseCodes[i]) == 0) {
                    int len = strlen(textOutput);
                    textOutput[len] = characters[i];
                    textOutput[len + 1] = '\0';
                    break;
                }
            }
        }

        token = strtok(NULL, " ");
    }
}