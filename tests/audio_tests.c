#include "audio.h"
#include "platform_io.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#ifdef _WIN32
#include <windows.h>
#endif

#define CHECK(condition) do { \
    if (!(condition)) { \
        fprintf(stderr, "FAIL audio line %d: %s (%s)\n", __LINE__, #condition, audioLastError()); \
        return 1; \
    } \
} while (0)

static uint16_t readU16(const unsigned char *bytes)
{
    return (uint16_t) ((uint16_t) bytes[0] | ((uint16_t) bytes[1] << 8));
}

static uint32_t readU32(const unsigned char *bytes)
{
    uint32_t value = 0;
    for (unsigned i = 0; i < 4; i++) value |= (uint32_t) bytes[i] << (i * 8);
    return value;
}

static int removeUtf8TestFile(const char *path)
{
#ifdef _WIN32
    wchar_t wide[256];
    if (!MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, path, -1, wide, 256)) return -1;
    return _wremove(wide);
#else
    return remove(path);
#endif
}

static int testAnalysis(void)
{
    AudioOptions options = defaultAudioOptions();
    AudioAnalysis analysis;
    CHECK(options.wordsPerMinute == 20 && options.frequencyHz == 600 && options.sampleRate == 22050);
    CHECK(validateAudioOptions(&options));
    const struct { const char *input; uint64_t units; uint64_t tones; } cases[] = {
        {".", 1, 1}, {"-", 3, 1}, {".-", 5, 2}, {". .", 5, 2},
        {". / .", 9, 2}, {"  / \t. \n/ / \r. /  ", 9, 2},
        {"... --- ...", 27, 9}, {". . / -", 15, 3},
        {".--. .- .-. .. ... / .--. .- .-. .. ...", 93, 28},
        {".-.-.-", 17, 6}, {"..............", 27, 14}
    };
    for (size_t i = 0; i < sizeof(cases) / sizeof(cases[0]); i++) {
        CHECK(analyzeMorseAudio(cases[i].input, '/', &options, &analysis));
        CHECK(analysis.unitCount == cases[i].units);
        CHECK(analysis.toneCount == cases[i].tones);
        CHECK(analysis.sampleCount == cases[i].units * 1323);
    }
    for (size_t i = 0; i < 4; i++) {
        char input[] = ". / -";
        input[2] = "/|#*"[i];
        CHECK(analyzeMorseAudio(input, input[2], &options, &analysis));
        CHECK(analysis.unitCount == 11);
    }
    options.wordsPerMinute = 17;
    CHECK(analyzeMorseAudio("..............", '/', &options, &analysis));
    CHECK(analysis.sampleCount == (27ULL * 22050 * 6 + 42) / 85);
    options.wordsPerMinute = 60;
    options.frequencyHz = 2000;
    options.sampleRate = 8000;
    CHECK(validateAudioOptions(&options));
    CHECK(analyzeMorseAudio("-", '/', &options, &analysis));
    CHECK(analysis.sampleCount == 480);
    options.wordsPerMinute = 5;
    options.frequencyHz = 200;
    options.sampleRate = 48000;
    CHECK(validateAudioOptions(&options));
    CHECK(analyzeMorseAudio("-", '/', &options, &analysis));
    CHECK(analysis.sampleCount == 34560);
    /* The 2499-unit token fits below the 2500-unit duration ceiling at 5 WPM. */
    char boundary[2502];
    memset(boundary, '.', 1250);
    boundary[1250] = '\0';
    CHECK(analyzeMorseAudio(boundary, '/', &options, &analysis));
    CHECK(analysis.unitCount == 2499 && analysis.sampleCount == 28788480);
    boundary[1250] = '.';
    boundary[1251] = '\0';
    CHECK(!analyzeMorseAudio(boundary, '/', &options, &analysis));
    /* A final dash instead of a dot also crosses the ceiling (2501 units). */
    memset(boundary, '.', 1249);
    boundary[1249] = '-';
    boundary[1250] = '\0';
    CHECK(!analyzeMorseAudio(boundary, '/', &options, &analysis));
    return 0;
}

static int testErrors(void)
{
    AudioOptions options = defaultAudioOptions();
    AudioAnalysis analysis;
    CHECK(!validateAudioOptions(NULL));
    options.wordsPerMinute = 4; CHECK(!validateAudioOptions(&options));
    options.wordsPerMinute = 61; CHECK(!validateAudioOptions(&options));
    options.wordsPerMinute = 20;
    options.frequencyHz = 199; CHECK(!validateAudioOptions(&options));
    options.frequencyHz = 2001; CHECK(!validateAudioOptions(&options));
    options.frequencyHz = 600;
    options.sampleRate = 7999; CHECK(!validateAudioOptions(&options));
    options.sampleRate = 48001; CHECK(!validateAudioOptions(&options));
    options.sampleRate = 22050;
    CHECK(!analyzeMorseAudio(NULL, '/', &options, &analysis));
    CHECK(!analyzeMorseAudio(".", '/', &options, NULL));
    CHECK(!analyzeMorseAudio(".", '\0', &options, &analysis));
    CHECK(!analyzeMorseAudio(".", '.', &options, &analysis));
    CHECK(!analyzeMorseAudio("", '/', &options, &analysis));
    CHECK(!analyzeMorseAudio(" / / \t\r\n ", '/', &options, &analysis));
    CHECK(!analyzeMorseAudio("... X ---", '/', &options, &analysis));
    CHECK(!analyzeMorseAudio("... | ---", '/', &options, &analysis));
    CHECK(!analyzeMorseAudio("... \xc3\xa7", '/', &options, &analysis));
    CHECK(!playMorseAudio("bad input", '/', &options));
    CHECK(!writeMorseWav(NULL, ".", '/', &options));
    CHECK(!writeMorseWav("", ".", '/', &options));
    CHECK(!writeMorseWav("audio-test-missing-directory/file.wav", ".", '/', &options));
    CHECK(audioLastError()[0] != '\0');

    char *large = malloc(AUDIO_MAX_MORSE_BYTES + 2U);
    CHECK(large != NULL);
    memset(large, ' ', AUDIO_MAX_MORSE_BYTES + 1U);
    large[AUDIO_MAX_MORSE_BYTES + 1U] = '\0';
    CHECK(!analyzeMorseAudio(large, '/', &options, &analysis));
    CHECK(strstr(audioLastError(), "1 MiB") != NULL);
    memset(large, '.', 50001);
    large[50001] = '\0';
    CHECK(!analyzeMorseAudio(large, '/', &options, &analysis));
    CHECK(strstr(audioLastError(), "10 minute") != NULL);
    free(large);
    CHECK(validateAudioOptions(&options));
    CHECK(audioLastError()[0] == '\0');
    return 0;
}

static int testWav(void)
{
    AudioOptions options = defaultAudioOptions();
    const char *path = "audio-test-\xc3\xa7\xc4\xb1\xc4\x9f.wav";
    CHECK(writeMorseWav(path, ". . / -", '/', &options));
    FILE *file = openUtf8File(path, "rb");
    CHECK(file != NULL);
    unsigned char header[44];
    CHECK(fread(header, 1, sizeof(header), file) == sizeof(header));
    const size_t sampleCount = 15U * 1323U;
    CHECK(memcmp(header, "RIFF", 4) == 0 && memcmp(header + 8, "WAVEfmt ", 8) == 0);
    CHECK(readU32(header + 4) == 36 + sampleCount * 2);
    CHECK(readU32(header + 16) == 16);
    CHECK(readU16(header + 20) == 1 && readU16(header + 22) == 1);
    CHECK(readU32(header + 24) == 22050 && readU32(header + 28) == 44100);
    CHECK(readU16(header + 32) == 2 && readU16(header + 34) == 16);
    CHECK(memcmp(header + 36, "data", 4) == 0);
    CHECK(readU32(header + 40) == sampleCount * 2);
    unsigned char *bytes = malloc(sampleCount * 2);
    CHECK(bytes != NULL);
    CHECK(fread(bytes, 2, sampleCount, file) == sampleCount);
    CHECK(fgetc(file) == EOF && !ferror(file));
    CHECK(fclose(file) == 0);
    unsigned crossings = 0;
    int previous = 0, maximum = 0;
    for (size_t i = 0; i < sampleCount; i++) {
        uint16_t raw = readU16(bytes + i * 2);
        int sample = raw <= 32767 ? (int) raw : (int) raw - 65536;
        if ((i >= 1323 && i < 4U * 1323) || (i >= 5U * 1323 && i < 12U * 1323)) CHECK(sample == 0);
        CHECK(sample >= -12000 && sample <= 12000);
        if (abs(sample) > maximum) maximum = abs(sample);
        if (i >= 12U * 1323) {
            if (previous <= 0 && sample > 0) crossings++;
            previous = sample;
        }
    }
    CHECK(maximum > 11900);
    CHECK(crossings >= 107 && crossings <= 109);
    CHECK(readU16(bytes) == 0 && readU16(bytes + (sampleCount - 1) * 2) == 0);
    free(bytes);

    /* Invalid conversion must leave an existing output untouched. */
    CHECK(!writeMorseWav(path, "invalid", '/', &options));
    file = openUtf8File(path, "rb");
    CHECK(file != NULL && fread(header, 1, 4, file) == 4 && memcmp(header, "RIFF", 4) == 0);
    CHECK(fclose(file) == 0);
    CHECK(removeUtf8TestFile(path) == 0);

    options.wordsPerMinute = 17;
    options.frequencyHz = 997;
    options.sampleRate = 44100;
    CHECK(writeMorseWav(path, "..............", '/', &options));
    file = openUtf8File(path, "rb");
    CHECK(file != NULL && fread(header, 1, sizeof(header), file) == sizeof(header));
    CHECK(readU32(header + 40) == 2U * ((27ULL * 44100 * 6 + 42) / 85));
    CHECK(fclose(file) == 0 && removeUtf8TestFile(path) == 0);
    return 0;
}

int main(void)
{
    CHECK(testAnalysis() == 0);
    CHECK(testErrors() == 0);
    CHECK(testWav() == 0);
    puts("PASS: audio timing, collapsed gaps, PCM headers, frequency, limits, UTF-8 paths and errors (no audio device required)");
    return 0;
}
