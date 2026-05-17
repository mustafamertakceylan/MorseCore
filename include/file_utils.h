#ifndef FILE_UTILS_H
#define FILE_UTILS_H

int readFromFile(const char fileName[], char content[], int maxLength);
int writeToFile(const char fileName[], const char content[]);
int fileExists(const char fileName[]);
void generateNextFileName(char fileName[], const char prefix[], const char extension[]);
void buildDataFilePath(char fullPath[], const char fileName[]);

#endif