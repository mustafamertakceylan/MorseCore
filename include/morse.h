#ifndef MORSE_H
#define MORSE_H

int textToMorse(const char text[], char morseOutput[], char wordSeparator);
int morseToText(const char morseInput[], char textOutput[], char wordSeparator);
void printMorseTable(void);
#endif