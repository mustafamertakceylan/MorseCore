#ifndef MORSECORE_CONSOLE_RENDER_H
#define MORSECORE_CONSOLE_RENDER_H
#include "stats.h"
void printMorseTable(void);
void printConversionStats(const ConversionStats *stats, const char language[]);
void printSessionSummary(const SessionStats *stats, const char language[]);
#endif
