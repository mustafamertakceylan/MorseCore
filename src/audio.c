#include "audio.h"
#include "runtime_paths.h"

#include <ctype.h>
#include <math.h>
#include <stddef.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#include <windows.h>
#include <mmsystem.h>
#endif

#define PCM_CHUNK_SAMPLES 4096U
#define AUDIO_AMPLITUDE 12000.0
#define AUDIO_PI 3.14159265358979323846

static char lastError[256];

static int fail(const char *message)
{
    snprintf(lastError, sizeof(lastError), "%s", message);
    return 0;
}

const char *audioLastError(void) { return lastError; }

AudioOptions defaultAudioOptions(void)
{
    AudioOptions options = {20, 600, 22050};
    return options;
}

int validateAudioOptions(const AudioOptions *options)
{
    lastError[0] = '\0';
    if (options == NULL) return fail("Audio options are missing.");
    if (options->wordsPerMinute < AUDIO_MIN_WPM || options->wordsPerMinute > AUDIO_MAX_WPM)
        return fail("Audio speed must be between 5 and 60 WPM.");
    if (options->frequencyHz < AUDIO_MIN_FREQUENCY_HZ || options->frequencyHz > AUDIO_MAX_FREQUENCY_HZ)
        return fail("Audio frequency must be between 200 and 2000 Hz.");
    if (options->sampleRate < AUDIO_MIN_SAMPLE_RATE || options->sampleRate > AUDIO_MAX_SAMPLE_RATE)
        return fail("Audio sample rate must be between 8000 and 48000 Hz.");
    return 1;
}

typedef struct {
    const char *morse;
    size_t offset;
    char separator;
    int haveTone;
    unsigned pendingGap;
    unsigned queuedTone;
} MorseCursor;

/* Returns one tone or silence segment, 0 at end, -1 for invalid input.
 * Delaying gaps until the next tone prevents additive 3+7 unit pauses. */
static int nextSegment(MorseCursor *cursor, unsigned *units, int *tone)
{
    if (cursor->queuedTone != 0) {
        *units = cursor->queuedTone;
        cursor->queuedTone = 0;
        *tone = 1;
        return 1;
    }
    while (cursor->morse[cursor->offset] != '\0') {
        if (cursor->offset >= AUDIO_MAX_MORSE_BYTES) {
            fail("Morse audio input exceeds the 1 MiB limit.");
            return -1;
        }
        unsigned char current = (unsigned char) cursor->morse[cursor->offset++];
        if (current == '.' || current == '-') {
            unsigned toneUnits = current == '.' ? 1U : 3U;
            unsigned gap = cursor->haveTone ? (cursor->pendingGap != 0 ? cursor->pendingGap : 1U) : 0;
            cursor->haveTone = 1;
            cursor->pendingGap = 0;
            if (gap != 0) {
                cursor->queuedTone = toneUnits;
                *units = gap;
                *tone = 0;
            } else {
                *units = toneUnits;
                *tone = 1;
            }
            return 1;
        }
        if (current == (unsigned char) cursor->separator) {
            if (cursor->haveTone) cursor->pendingGap = 7;
        } else if (current < 128 && isspace(current)) {
            if (cursor->haveTone && cursor->pendingGap < 3) cursor->pendingGap = 3;
        } else {
            fail("Invalid Morse audio input or word separator.");
            return -1;
        }
    }
    return 0;
}

static uint64_t samplesAtUnit(uint64_t units, const AudioOptions *options)
{
    uint64_t divisor = (uint64_t) options->wordsPerMinute * 5;
    return (units * (uint64_t) options->sampleRate * 6 + divisor / 2) / divisor;
}

int analyzeMorseAudio(const char *morse, char wordSeparator,
                      const AudioOptions *options, AudioAnalysis *analysis)
{
    if (analysis != NULL) memset(analysis, 0, sizeof(*analysis));
    if (!validateAudioOptions(options)) return 0;
    if (analysis == NULL) return fail("Audio analysis output is missing.");
    if (morse == NULL) return fail("Morse audio input is missing.");
    if (strchr("/|#*", wordSeparator) == NULL || wordSeparator == '\0')
        return fail("Audio word separator must be /, |, # or *.");
    MorseCursor cursor = {morse, 0, wordSeparator, 0, 0, 0};
    AudioAnalysis result = {0, 0, 0};
    uint64_t maximumUnits = (uint64_t) AUDIO_MAX_DURATION_SECONDS * (uint64_t) options->wordsPerMinute * 5 / 6;
    unsigned units;
    int tone, status;
    while ((status = nextSegment(&cursor, &units, &tone)) > 0) {
        if (result.unitCount > maximumUnits - units)
            return fail("Morse audio exceeds the 10 minute duration limit.");
        result.unitCount += units;
        if (tone) result.toneCount++;
    }
    if (status < 0) return 0;
    if (result.toneCount == 0) return fail("Morse audio input has no dots or dashes.");
    result.sampleCount = samplesAtUnit(result.unitCount, options);
    if (result.sampleCount > (UINT32_MAX - 36U) / 2U)
        return fail("Morse audio exceeds the PCM WAV size limit.");
    *analysis = result;
    return 1;
}

typedef struct {
    MorseCursor cursor;
    AudioOptions options;
    uint64_t units;
    uint64_t emitted;
    uint64_t segmentLength;
    uint64_t segmentOffset;
    int tone;
} PcmGenerator;

static PcmGenerator makeGenerator(const char *morse, char separator, const AudioOptions *options)
{
    PcmGenerator generator;
    memset(&generator, 0, sizeof(generator));
    generator.cursor.morse = morse;
    generator.cursor.separator = separator;
    generator.options = *options;
    return generator;
}

static size_t generatePcm(PcmGenerator *generator, int16_t *output, size_t capacity)
{
    size_t written = 0;
    while (written < capacity) {
        if (generator->segmentOffset == generator->segmentLength) {
            unsigned units;
            if (nextSegment(&generator->cursor, &units, &generator->tone) <= 0) break;
            generator->units += units;
            generator->segmentLength = samplesAtUnit(generator->units, &generator->options) - generator->emitted;
            generator->segmentOffset = 0;
        }
        int16_t sample = 0;
        if (generator->tone) {
            /* Five millisecond attack/release avoids clicks at symbol edges. */
            uint64_t ramp = (uint64_t) generator->options.sampleRate / 200;
            if (ramp > generator->segmentLength / 2) ramp = generator->segmentLength / 2;
            uint64_t edge = generator->segmentOffset;
            uint64_t remaining = generator->segmentLength - 1 - generator->segmentOffset;
            if (remaining < edge) edge = remaining;
            double envelope = ramp != 0 && edge < ramp ? (double) edge / (double) ramp : 1.0;
            double phase = 2.0 * AUDIO_PI * generator->options.frequencyHz *
                           (double) generator->segmentOffset / generator->options.sampleRate;
            sample = (int16_t) (AUDIO_AMPLITUDE * envelope * sin(phase));
        }
        output[written++] = sample;
        generator->segmentOffset++;
        generator->emitted++;
    }
    return written;
}

static void putU16(unsigned char *bytes, uint16_t value)
{
    bytes[0] = (unsigned char) value;
    bytes[1] = (unsigned char) (value >> 8);
}

static void putU32(unsigned char *bytes, uint32_t value)
{
    for (unsigned i = 0; i < 4; i++) bytes[i] = (unsigned char) (value >> (i * 8));
}

int writeMorseWav(const char *path, const char *morse, char wordSeparator, const AudioOptions *options)
{
    AudioAnalysis analysis;
    if (!analyzeMorseAudio(morse, wordSeparator, options, &analysis)) return 0;
    if (path == NULL || path[0] == '\0') return fail("WAV output path is missing.");
    FILE *file = openUtf8File(path, "wb");
    if (file == NULL) return fail("Cannot open the WAV output file.");

    uint32_t dataSize = (uint32_t) analysis.sampleCount * 2U;
    unsigned char header[44] = {0};
    memcpy(header, "RIFF", 4);
    putU32(header + 4, dataSize + 36);
    memcpy(header + 8, "WAVEfmt ", 8);
    putU32(header + 16, 16);
    putU16(header + 20, 1);
    putU16(header + 22, 1);
    putU32(header + 24, (uint32_t) options->sampleRate);
    putU32(header + 28, (uint32_t) options->sampleRate * 2U);
    putU16(header + 32, 2);
    putU16(header + 34, 16);
    memcpy(header + 36, "data", 4);
    putU32(header + 40, dataSize);
    int success = fwrite(header, 1, sizeof(header), file) == sizeof(header);
    PcmGenerator generator = makeGenerator(morse, wordSeparator, options);
    int16_t samples[PCM_CHUNK_SAMPLES];
    unsigned char bytes[PCM_CHUNK_SAMPLES * 2U];
    size_t count;
    while (success && (count = generatePcm(&generator, samples, PCM_CHUNK_SAMPLES)) != 0) {
        for (size_t i = 0; i < count; i++) putU16(bytes + i * 2, (uint16_t) samples[i]);
        success = fwrite(bytes, 2, count, file) == count;
    }
    if (fclose(file) != 0) success = 0;
    if (!success) return fail("Cannot finish writing the WAV output file.");
    return 1;
}

#ifdef _WIN32
typedef struct {
    WAVEHDR headers[2];
    int16_t samples[2][PCM_CHUNK_SAMPLES];
    HANDLE event;
} PlaybackBuffers;

static int failWave(const char *operation, MMRESULT result)
{
    char message[160];
    if (waveOutGetErrorTextA(result, message, sizeof(message)) != MMSYSERR_NOERROR)
        snprintf(message, sizeof(message), "Windows audio error %u", (unsigned) result);
    snprintf(lastError, sizeof(lastError), "%s: %s", operation, message);
    return 0;
}

static int playWindowsAudio(const char *morse, char separator,
                           const AudioOptions *options, const AudioAnalysis *analysis)
{
    PlaybackBuffers *buffers = calloc(1, sizeof(*buffers));
    if (buffers == NULL) return fail("Cannot allocate audio playback buffers.");
    buffers->event = CreateEventW(NULL, FALSE, FALSE, NULL);
    if (buffers->event == NULL) {
        free(buffers);
        return fail("Cannot create the audio playback event.");
    }
    WAVEFORMATEX format;
    memset(&format, 0, sizeof(format));
    format.wFormatTag = WAVE_FORMAT_PCM;
    format.nChannels = 1;
    format.nSamplesPerSec = (DWORD) options->sampleRate;
    format.wBitsPerSample = 16;
    format.nBlockAlign = 2;
    format.nAvgBytesPerSec = format.nSamplesPerSec * format.nBlockAlign;
    HWAVEOUT device = NULL;
    MMRESULT result = waveOutOpen(&device, WAVE_MAPPER, &format,
                                  (DWORD_PTR) buffers->event, 0, CALLBACK_EVENT);
    if (result != MMSYSERR_NOERROR) {
        CloseHandle(buffers->event);
        free(buffers);
        return failWave("Cannot open audio output", result);
    }
    PcmGenerator generator = makeGenerator(morse, separator, options);
    int success = 1, prepared[2] = {0, 0}, active[2] = {0, 0};
    unsigned pending = 0;
    for (unsigned i = 0; i < 2; i++) {
        WAVEHDR *header = &buffers->headers[i];
        header->lpData = (LPSTR) buffers->samples[i];
        header->dwBufferLength = (DWORD) sizeof(buffers->samples[i]);
        result = waveOutPrepareHeader(device, header, sizeof(*header));
        if (result != MMSYSERR_NOERROR) { success = failWave("Cannot prepare audio buffer", result); break; }
        prepared[i] = 1;
        size_t count = generatePcm(&generator, buffers->samples[i], PCM_CHUNK_SAMPLES);
        if (count != 0) {
            header->dwBufferLength = (DWORD) (count * sizeof(int16_t));
            result = waveOutWrite(device, header, sizeof(*header));
            if (result != MMSYSERR_NOERROR) { success = failWave("Cannot queue audio buffer", result); break; }
            active[i] = 1;
            pending++;
        }
    }
    ULONGLONG deadline = GetTickCount64() + analysis->sampleCount * 1000 / (uint64_t) options->sampleRate + 10000;
    while (success && pending != 0) {
        DWORD waited = WaitForSingleObject(buffers->event, 2000);
        if (waited == WAIT_FAILED) { success = fail("Waiting for audio output failed."); break; }
        if (GetTickCount64() > deadline) { success = fail("Audio output timed out."); break; }
        for (unsigned i = 0; i < 2; i++) {
            WAVEHDR *header = &buffers->headers[i];
            if (!active[i] || !(header->dwFlags & WHDR_DONE)) continue;
            active[i] = 0;
            pending--;
            size_t count = generatePcm(&generator, buffers->samples[i], PCM_CHUNK_SAMPLES);
            if (count == 0) continue;
            header->dwBufferLength = (DWORD) (count * sizeof(int16_t));
            result = waveOutWrite(device, header, sizeof(*header));
            if (result != MMSYSERR_NOERROR) { success = failWave("Cannot queue audio buffer", result); break; }
            active[i] = 1;
            pending++;
        }
    }
    int cleanupComplete = 1;
    if (!success) {
        result = waveOutReset(device);
        if (result != MMSYSERR_NOERROR) cleanupComplete = 0;
    }
    for (unsigned i = 0; i < 2; i++) {
        if (!prepared[i]) continue;
        result = waveOutUnprepareHeader(device, &buffers->headers[i], sizeof(WAVEHDR));
        if (result != MMSYSERR_NOERROR) {
            if (success) success = failWave("Cannot release audio buffer", result);
            cleanupComplete = 0;
        }
    }
    result = waveOutClose(device);
    if (result != MMSYSERR_NOERROR) {
        if (success) success = failWave("Cannot close audio output", result);
        cleanupComplete = 0;
    }
    /* A failed driver cleanup may retain buffer/event references. Keep this
     * bounded allocation alive rather than letting the driver access freed memory. */
    if (cleanupComplete) {
        CloseHandle(buffers->event);
        free(buffers);
    }
    return success;
}
#endif

int playMorseAudio(const char *morse, char wordSeparator, const AudioOptions *options)
{
    AudioAnalysis analysis;
    if (!analyzeMorseAudio(morse, wordSeparator, options, &analysis)) return 0;
#ifdef _WIN32
    return playWindowsAudio(morse, wordSeparator, options, &analysis);
#else
    return fail("Live audio playback is available only on Windows; export a WAV file instead.");
#endif
}
