#ifndef ERROR_H
#define ERROR_H
#include "error_codes.h"
const char* getErrorMessage(ResultCode resultCode);
void printError(ResultCode resultCode);
#endif