#include <stdio.h>

#include "error.h"
#include "language.h"
#include "ui.h"

const char* getErrorMessage(ResultCode resultCode, const char language[])
{
    switch (resultCode) {
        case RESULT_EMPTY_INPUT:
            return getText(language, TXT_ERROR_EMPTY_INPUT);

        case RESULT_INVALID_INPUT:
            return getText(language, TXT_ERROR_INVALID_INPUT);

        case RESULT_UNSUPPORTED_CHARACTER:
            return getText(language, TXT_ERROR_UNSUPPORTED_CHARACTER);

        case RESULT_INVALID_MORSE:
            return getText(language, TXT_ERROR_INVALID_MORSE);

        case RESULT_FILE_OPEN_ERROR:
            return getText(language, TXT_ERROR_FILE_OPEN);

        case RESULT_FILE_WRITE_ERROR:
            return getText(language, TXT_ERROR_FILE_WRITE);

        case RESULT_INVALID_SEPARATOR:
            return getText(language, TXT_ERROR_INVALID_SEPARATOR);

        case RESULT_SETTINGS_LOAD_ERROR:
            return getText(language, TXT_ERROR_SETTINGS_LOAD);

        case RESULT_SETTINGS_SAVE_ERROR:
            return getText(language, TXT_ERROR_SETTINGS_SAVE);

        default:
            return getText(language, TXT_ERROR_UNKNOWN);
    }
}

void printError(ResultCode resultCode, const char language[])
{
    printf(COLOR_RED "%s\n" COLOR_RESET, getErrorMessage(resultCode, language));
}