#ifndef FILE_UTILS_H
#define FILE_UTILS_H

int readFromFile(const char fileName[], char content[], int maxLength);
int writeToFile(const char fileName[], const char content[]);

#endif