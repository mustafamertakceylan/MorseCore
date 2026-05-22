#ifndef ERROR_H
#define ERROR_H

#include "error_codes.h"

const char* getErrorMessage(ResultCode resultCode, const char language[]);
void printError(ResultCode resultCode, const char language[]);

#endif