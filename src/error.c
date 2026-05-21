#include <stdio.h>
#include "error.h"
#include "error_messages.h"
#include "ui.h"

const char* getErrorMessage(ResultCode resultCode)
{
    switch (resultCode) {
        case RESULT_EMPTY_INPUT:
            return ERROR_MSG_EMPTY_INPUT;

        case RESULT_INVALID_INPUT:
            return ERROR_MSG_INVALID_INPUT;

        case RESULT_UNSUPPORTED_CHARACTER:
            return ERROR_MSG_UNSUPPORTED_CHARACTER;

        case RESULT_INVALID_MORSE:
            return ERROR_MSG_INVALID_MORSE;

        case RESULT_FILE_OPEN_ERROR:
            return ERROR_MSG_FILE_OPEN;

        case RESULT_FILE_WRITE_ERROR:
            return ERROR_MSG_FILE_WRITE;

        case RESULT_INVALID_SEPARATOR:
            return ERROR_MSG_INVALID_SEPARATOR;

        case RESULT_SETTINGS_LOAD_ERROR:
            return ERROR_MSG_SETTINGS_LOAD;

        case RESULT_SETTINGS_SAVE_ERROR:
            return ERROR_MSG_SETTINGS_SAVE;

        default:
            return ERROR_MSG_UNKNOWN;
    }
}

void printError(ResultCode resultCode)
{
    printf(COLOR_RED "%s\n" COLOR_RESET, getErrorMessage(resultCode));
}