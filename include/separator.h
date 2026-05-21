#ifndef SEPARATOR_H
#define SEPARATOR_H

int isValidSeparator(char separator);
char detectSeparatorFromMorse(const char morseInput[]);
char resolveSeparatorForMorseInput(const char morseInput[], char fallbackSeparator);

#endif