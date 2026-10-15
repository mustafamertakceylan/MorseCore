#ifndef CONSOLE_FEATURES_H
#define CONSOLE_FEATURES_H

#include "settings.h"
#include "stats.h"

/* Return zero on end of input so the application can exit without another menu. */
int runAudioMenu(const AppSettings *settings, SessionStats *stats);
int runPracticeMenu(const AppSettings *settings, SessionStats *stats);

#endif
