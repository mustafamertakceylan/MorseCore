#ifndef VALIDATION_H
#define VALIDATION_H
int isEmptyInput(const char input[]);
int isValidFileName(const char fileName[]);
int hasTxtExtension(const char fileName[]);
void ensureTxtExtension(char fileName[], int maxLength);
#endif