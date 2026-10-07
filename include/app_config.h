#ifndef APP_CONFIG_H
#define APP_CONFIG_H
#ifndef MORSECORE_ROOT_PATH
#define MORSECORE_ROOT_PATH "./"
#endif
#define DATA_FOLDER_PATH MORSECORE_ROOT_PATH "data/"
#define CONFIG_FOLDER_PATH MORSECORE_ROOT_PATH "config/"
#define SETTINGS_FILE_PATH CONFIG_FOLDER_PATH "settings.txt"
#define DEFAULT_WORD_SEPARATOR '/' // Varsayılan seperatör ayarı
#define DEFAULT_LANGUAGE "EN" //Varsayılan uygulama dili tercihi
#define MORSE_OUTPUT_PREFIX "morse_output" //Varsayılan morse çıktı dosya ismi ön eki
#define DECODED_OUTPUT_PREFIX "decoded_output" //Varsayılan çözümlenmiş çıktı dosya ismi ön eki
#define DEFAULT_FILE_EXTENSION "txt" //Varsayılan dosya çıktı türü

#endif
