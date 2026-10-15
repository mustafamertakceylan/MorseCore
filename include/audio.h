#ifndef AUDIO_H
#define AUDIO_H

#include <stdint.h>
#include <stdatomic.h>

#define AUDIO_MIN_WPM 5
#define AUDIO_MAX_WPM 60
#define AUDIO_MIN_FREQUENCY_HZ 200
#define AUDIO_MAX_FREQUENCY_HZ 2000
#define AUDIO_MIN_SAMPLE_RATE 8000
#define AUDIO_MAX_SAMPLE_RATE 48000
#define AUDIO_MAX_DURATION_SECONDS 600
#define AUDIO_MAX_MORSE_BYTES (1024U * 1024U)

typedef struct {
    int wordsPerMinute;
    int frequencyHz;
    int sampleRate;
} AudioOptions;

typedef struct {
    uint64_t unitCount;
    uint64_t sampleCount;
    uint64_t toneCount;
} AudioAnalysis;

typedef struct { atomic_bool requested; } AudioCancellation;
void initializeAudioCancellation(AudioCancellation *cancellation);
void requestAudioCancellation(AudioCancellation *cancellation);
int audioCancellationRequested(const AudioCancellation *cancellation);

/* PCM16 mono, PARIS timing: one unit is 1.2 / WPM seconds.
 * Dot/dash: 1/3 units; symbol/letter/word gaps: 1/3/7 units.
 * Whitespace separates letters. The selected / | # * separates words.
 * Repeated separators collapse; leading/trailing gaps are omitted.
 * Tokens are syntactic .- sequences, including long punctuation tokens.
 * WAV generation and playback use bounded streaming buffers, at most 10 min.
 * Public operations return 1 on success, 0 on failure. Paths are UTF-8.
 * Calls are synchronous; audioLastError remains valid until the next call.
 * Error text is local to the calling thread. Independent calls are thread safe.
 */
AudioOptions defaultAudioOptions(void);
int validateAudioOptions(const AudioOptions *options);
int analyzeMorseAudio(const char *morse, char wordSeparator,
                      const AudioOptions *options, AudioAnalysis *analysis);
int writeMorseWav(const char *path, const char *morse, char wordSeparator,
                  const AudioOptions *options);
int playMorseAudio(const char *morse, char wordSeparator, const AudioOptions *options);
int playMorseAudioControlled(const char *morse, char wordSeparator, const AudioOptions *options,
                            const AudioCancellation *cancellation);
const char *audioLastError(void);

#endif
