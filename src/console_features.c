#include <ctype.h>
#include <errno.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

#include "audio.h"
#include "console_features.h"
#include "constants.h"
#include "error.h"
#include "file_utils.h"
#include "input.h"
#include "language.h"
#include "morse.h"
#include "practice.h"
#include "ui.h"

static const char *localized(const char *language, const char *turkish, const char *english)
{
    return strcmp(language, "TR") == 0 ? turkish : english;
}

static int readNumber(const char *language, const char *label, int minimum,
                      int maximum, int defaultValue, int *value)
{
    char line[40];
    for (;;) {
        printf("%s [%d-%d, Enter=%d]: ", label, minimum, maximum, defaultValue);
        fflush(stdout);
        int status = readInputLine(line, (int) sizeof(line));
        if (status == 0) return 0;
        char *start = line;
        while (isspace((unsigned char) *start)) start++;
        if (status > 0 && *start == '\0') {
            *value = defaultValue;
            return 1;
        }
        errno = 0;
        char *end;
        long parsed = strtol(start, &end, 10);
        while (isspace((unsigned char) *end)) end++;
        if (status > 0 && end != start && *end == '\0' && errno != ERANGE &&
            parsed >= minimum && parsed <= maximum) {
            *value = (int) parsed;
            return 1;
        }
        printError(RESULT_INVALID_INPUT, language);
    }
}

static int readAudioOptions(const char *language, AudioOptions *options)
{
    *options = defaultAudioOptions();
    return readNumber(language, localized(language, "Hiz (WPM)", "Speed (WPM)"),
                      AUDIO_MIN_WPM, AUDIO_MAX_WPM, options->wordsPerMinute,
                      &options->wordsPerMinute) &&
           readNumber(language, localized(language, "Ton (Hz)", "Tone (Hz)"),
                      AUDIO_MIN_FREQUENCY_HZ, AUDIO_MAX_FREQUENCY_HZ,
                      options->frequencyHz, &options->frequencyHz);
}

static void printAudioError(const char *language)
{
    printf("%s[!] %s: %s%s\n", COLOR_RED,
           localized(language, "Ses islemi tamamlanamadi", "Audio operation failed"),
           audioLastError(), COLOR_RESET);
}

int runAudioMenu(const AppSettings *settings, SessionStats *stats)
{
    const char *language = settings->language;
    char *text = NULL;
    char *morse = NULL;
    AudioOptions options;
    int action;

    printSectionHeader(localized(language, "Sesli Morse / WAV", "Morse Audio / WAV"));
    printf("%s\n", localized(language,
           "Metni dinleyin veya WAV dosyasina kaydedin. En fazla 10 dakika.",
           "Listen to text or save a WAV file. Maximum duration: 10 minutes."));
    printf("%s", getText(language, TXT_ENTER_TEXT));
    fflush(stdout);
    ResultCode result = readInputLineAlloc(&text);
    if (result == RESULT_END_OF_INPUT) return 0;
    if (result == RESULT_SUCCESS) result = textToMorseAlloc(text, &morse, settings->wordSeparator);
    free(text);
    if (result != RESULT_SUCCESS) {
        printError(result, language);
        increaseInvalidInputCount(stats);
        return 1;
    }
    if (!readAudioOptions(language, &options)) {
        free(morse);
        return 0;
    }
    printf("\n%s\n", localized(language,
           "[1] Dinlet  [2] WAV kaydet  [3] Kaydet ve dinlet  [0] Geri",
           "[1] Play  [2] Save WAV  [3] Save and play  [0] Back"));
    if (!readNumber(language, localized(language, "Islem", "Action"), 0, 3, 1, &action)) {
        free(morse);
        return 0;
    }
    if (action == 0) {
        free(morse);
        return 1;
    }

    AudioAnalysis analysis;
    if (!analyzeMorseAudio(morse, settings->wordSeparator, &options, &analysis)) {
        printAudioError(language);
        increaseInvalidInputCount(stats);
        free(morse);
        return 1;
    }
    printf("%s: %.1f s\n", localized(language, "Sure", "Duration"),
           (double) analysis.sampleCount / options.sampleRate);
    if (action == 2 || action == 3) {
        char fileName[MAX_FILE_NAME_LENGTH];
        char path[MAX_FILE_PATH_LENGTH];
        generateNextFileName(fileName, "morse_audio", "wav");
        if (fileName[0] == '\0' || !buildDataFilePath(path, fileName)) {
            printError(RESULT_FILE_WRITE_ERROR, language);
            increaseInvalidInputCount(stats);
        } else if (!writeMorseWav(path, morse, settings->wordSeparator, &options)) {
            printAudioError(language);
            increaseInvalidInputCount(stats);
        } else {
            printf("%s[+] %s: %s%s\n", COLOR_GREEN,
                   localized(language, "WAV kaydedildi", "WAV saved"), path, COLOR_RESET);
            increaseSavedOutputCount(stats);
        }
    }
    if (action == 1 || action == 3) {
        printf("%s\n", localized(language, "Morse caliniyor...", "Playing Morse..."));
        fflush(stdout);
        if (!playMorseAudio(morse, settings->wordSeparator, &options)) {
            printAudioError(language);
            increaseInvalidInputCount(stats);
        } else {
            printf("%s[+] %s%s\n", COLOR_GREEN,
                   localized(language, "Oynatma tamamlandi", "Playback completed"), COLOR_RESET);
        }
    }
    free(morse);
    return 1;
}

static void printPracticeSummary(const PracticeSession *session, const char *language)
{
    printSectionHeader(localized(language, "Alistirma Ozeti", "Practice Summary"));
    printf("%s: %u / %u\n", localized(language, "Tamamlanan", "Completed"),
           session->answered, session->totalQuestions);
    printf("%s: %u   %s: %u   %s: %u\n",
           localized(language, "Dogru", "Correct"), session->correct,
           localized(language, "Yanlis", "Incorrect"), session->incorrect,
           localized(language, "Atlanan", "Skipped"), session->skipped);
    printf("%s: %.1f%%\n", localized(language, "Dogruluk", "Accuracy"),
           practiceAccuracy(session));
    printf("%s\n", localized(language,
           "Dogruluk, atlanan sorular dahil tamamlanan sorular uzerinden hesaplanir.",
           "Accuracy includes skipped questions among completed questions."));
}

int runPracticeMenu(const AppSettings *settings, SessionStats *stats)
{
    const char *language = settings->language;
    int presentation, category, count;
    AudioOptions options = defaultAudioOptions();
    PracticeSession session;
    PracticeQuestion question;
    int endOfInput = 0;

    printSectionHeader(localized(language, "Morse Alistirmasi", "Morse Practice"));
    printf("%s\n", localized(language, "[1] Morse'u okuyarak cevapla  [2] Dinleyerek cevapla",
           "[1] Read Morse and answer  [2] Listen and answer"));
    if (!readNumber(language, localized(language, "Yontem", "Method"), 1, 2, 1, &presentation)) return 0;
    printf("%s\n", localized(language, "[1] Harfler  [2] Rakamlar  [3] Kisa Ingilizce kelimeler",
           "[1] Letters  [2] Digits  [3] Short English words"));
    if (!readNumber(language, localized(language, "Kategori", "Category"), 1, 3, 1, &category) ||
        !readNumber(language, localized(language, "Soru sayisi", "Questions"), 1, 100, 10, &count) ||
        (presentation == 2 && !readAudioOptions(language, &options))) return 0;
    if (!practiceInit(&session, (PracticeMode) (category - 1), (uint32_t) count,
                      (uint32_t) time(NULL) ^ (uint32_t) clock())) {
        printError(RESULT_INVALID_INPUT, language);
        increaseInvalidInputCount(stats);
        return 1;
    }
    printf("\n%s\n", localized(language,
           "Duz metinle cevaplayin. Enter veya :skip = atla; :quit = bitir; :replay = tekrar.",
           "Answer in plain text. Enter or :skip = skip; :quit = finish; :replay = repeat."));

    while (practiceNext(&session, &question)) {
        char *morse = NULL;
        ResultCode result = textToMorseAlloc(question.answer, &morse, settings->wordSeparator);
        if (result != RESULT_SUCCESS) {
            printError(result, language);
            increaseInvalidInputCount(stats);
            break;
        }
        printf("\n%s %u / %u\n", localized(language, "Soru", "Question"), question.number, session.totalQuestions);
        if (presentation == 1) printf("%s%s%s\n", COLOR_CYAN, morse, COLOR_RESET);
        else if (!playMorseAudio(morse, settings->wordSeparator, &options)) {
            printAudioError(language);
            increaseInvalidInputCount(stats);
            free(morse);
            break;
        }

        int finished = 0;
        while (!finished) {
            char *answer = NULL;
            printf("%s", localized(language, "Cevap: ", "Answer: "));
            fflush(stdout);
            result = readInputLineAlloc(&answer);
            if (result == RESULT_END_OF_INPUT) {
                endOfInput = 1;
                finished = 1;
            } else if (result != RESULT_SUCCESS) {
                printError(result, language);
                increaseInvalidInputCount(stats);
                finished = 1;
            } else if (strcmp(answer, ":quit") == 0) {
                finished = 1;
            } else if (strcmp(answer, ":replay") == 0) {
                if (presentation == 1) printf("%s%s%s\n", COLOR_CYAN, morse, COLOR_RESET);
                else if (!playMorseAudio(morse, settings->wordSeparator, &options)) {
                    printAudioError(language);
                    increaseInvalidInputCount(stats);
                    finished = 1;
                }
            } else {
                PracticeResult submitted = strcmp(answer, ":skip") == 0
                                           ? practiceSkip(&session)
                                           : practiceSubmitAnswer(&session, answer);
                const char *feedback = submitted == PRACTICE_RESULT_CORRECT
                    ? localized(language, "Dogru", "Correct")
                    : submitted == PRACTICE_RESULT_SKIPPED
                    ? localized(language, "Atlandi", "Skipped")
                    : localized(language, "Yanlis", "Incorrect");
                printf("%s%s%s | %s: %s\n", submitted == PRACTICE_RESULT_CORRECT ? COLOR_GREEN : COLOR_YELLOW,
                       feedback, COLOR_RESET, localized(language, "Cevap", "Answer"), question.answer);
                finished = 1;
            }
            free(answer);
        }
        free(morse);
        if (session.questionActive) break;
    }
    printPracticeSummary(&session, language);
    return !endOfInput;
}
