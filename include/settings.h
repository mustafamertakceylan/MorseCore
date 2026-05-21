#ifndef SETTINGS_H
#define SETTINGS_H

typedef struct {
    char wordSeparator;
    char language[3];
} AppSettings;

void loadDefaultSettings(AppSettings *settings);
int loadSettings(AppSettings *settings);
int saveSettings(const AppSettings *settings);

#endif