#ifndef INPUT_H
#define INPUT_H

#include "error_codes.h"

/* Reads one stdin line without its final LF/CRLF. Caller frees *output.
 * Empty lines succeed; EOF before any bytes returns RESULT_END_OF_INPUT.
 * Embedded NUL bytes are rejected; every failure leaves *output NULL. */
ResultCode readInputLineAlloc(char **output);

#endif
