#include "morsecore_native.h"
#include "morse.h"
#include "morse_profiles.h"
#include "stats.h"
#include "separator.h"
#include "practice.h"
#include "audio.h"
#include <stdlib.h>
#include <string.h>
#include <limits.h>
#include <stdckdint.h>

static_assert(sizeof(mc_statistics) == 40);
static_assert(sizeof(mc_symbol) == 16);
static_assert(sizeof(mc_practice_statistics) == 32);
static_assert(sizeof(mc_feedback) == 72);
static_assert(sizeof(mc_audio_options) == 16);
static_assert(sizeof(mc_audio_analysis) == 24);

struct mc_result { char *text; mc_statistics stats; };
struct mc_practice { PracticeSession session; };
struct mc_cancel { AudioCancellation cancellation; };

uint32_t MC_CALL mc_abi_version(void) { return MC_ABI_VERSION; }
const char *MC_CALL mc_version(void) { return MORSECORE_VERSION; }

const char *MC_CALL mc_status_message(int32_t status)
{
    switch (status) {
        case MC_OK: return "Success";
        case MC_EMPTY_INPUT: return "Input is empty";
        case MC_INVALID_INPUT: return "Invalid input";
        case MC_UNSUPPORTED_CHARACTER: return "Unsupported character";
        case MC_INVALID_MORSE: return "Invalid Morse code";
        case MC_OUT_OF_MEMORY: return "Not enough memory";
        case MC_INPUT_TOO_LARGE: return "Input size is not representable";
        case MC_INVALID_SEPARATOR: return "Invalid word separator";
        case MC_INVALID_ARGUMENT: return "Invalid argument or struct size";
        case MC_BUFFER_TOO_SMALL: return "Output buffer is too small";
        case MC_INVALID_STATE: return "No next question or no active question";
        case MC_AUDIO_ERROR: return "Audio operation failed";
        case MC_CANCELLED: return "Audio operation cancelled";
        case MC_INVALID_UTF8: return "Input must be valid UTF-8";
        default: return "Unknown native status";
    }
}

static int32_t copyInput(const uint8_t *bytes, uint64_t length, char **out)
{
    *out = NULL;
    if (length > SIZE_MAX) return MC_INPUT_TOO_LARGE;
    if (bytes == NULL && length != 0) return MC_INVALID_ARGUMENT;
    size_t count = (size_t) length;
    size_t capacity;
    if (ckd_add(&capacity, count, (size_t) 1)) return MC_INPUT_TOO_LARGE;
    if (count != 0 && memchr(bytes, 0, count) != NULL) return MC_INVALID_INPUT;
    if (!morseIsValidUtf8(bytes, count)) return MC_INVALID_UTF8;
    char *copy = malloc(capacity);
    if (copy == NULL) return MC_OUT_OF_MEMORY;
    if (count != 0) memcpy(copy, bytes, count);
    copy[count] = '\0';
    *out = copy;
    return MC_OK;
}

static int32_t getSeparator(uint32_t value, char *out)
{
    if (value == 0) value = '/';
    if (value > CHAR_MAX || !isValidSeparator((char) value)) return MC_INVALID_SEPARATOR;
    *out = (char) value;
    return MC_OK;
}

static int32_t convert(const uint8_t *bytes, uint64_t length, uint32_t separator, uint32_t profile, int decode, mc_result **out)
{
    if (out == NULL) return MC_INVALID_ARGUMENT;
    *out = NULL;
    if (morseProfileSize(profile) == 0) return MC_INVALID_ARGUMENT;
    char actualSeparator;
    int32_t status = getSeparator(separator, &actualSeparator);
    if (status != MC_OK) return status;
    char *input = NULL, *text = NULL;
    status = copyInput(bytes, length, &input);
    if (status != MC_OK) return status;
    if (decode && separator == 0) actualSeparator = resolveSeparatorForMorseInput(input, '/');
    ResultCode result = morseProfileConvert(input, &text, actualSeparator, profile, decode);
    if (result != RESULT_SUCCESS) { free(input); return (int32_t) result; }
    mc_result *converted = malloc(sizeof(*converted));
    if (converted == NULL) { free(input); free(text); return MC_OUT_OF_MEMORY; }
    ConversionStats stats;
    calculateConversionStats(input, text, decode ? CONVERSION_MORSE_TO_TEXT : CONVERSION_TEXT_TO_MORSE, &stats);
    converted->text = text;
    converted->stats = (mc_statistics) {stats.inputLength, stats.outputLength, stats.wordCount, stats.convertedCharacterCount, stats.spaceCount};
    free(input);
    *out = converted;
    return MC_OK;
}

int32_t MC_CALL mc_encode(const uint8_t *input, uint64_t length, uint32_t separator, mc_result **out)
{ return convert(input, length, separator, 0, 0, out); }
int32_t MC_CALL mc_decode(const uint8_t *input, uint64_t length, uint32_t separator, mc_result **out)
{ return convert(input, length, separator, 0, 1, out); }

int32_t MC_CALL mc_encode_alphabet(const uint8_t *input, uint64_t length, uint32_t separator, uint32_t profile, mc_result **out)
{ return convert(input, length, separator, profile, 0, out); }
int32_t MC_CALL mc_decode_alphabet(const uint8_t *input, uint64_t length, uint32_t separator, uint32_t profile, mc_result **out)
{ return convert(input, length, separator, profile, 1, out); }
uint32_t MC_CALL mc_alphabet_profile_count(uint32_t profile) { return (uint32_t)morseProfileSize(profile); }
int32_t MC_CALL mc_alphabet_profile_get(uint32_t profile, uint32_t index, mc_symbol *out, uint32_t size)
{
    if(out==NULL || size<sizeof(*out)) return MC_INVALID_ARGUMENT;
    uint32_t ch; const char *code;
    if(!morseProfileEntry(profile,index,&ch,&code)) return MC_INVALID_ARGUMENT;
    *out=(mc_symbol){0}; out->character=ch;
    out->extension=ch<128 && strchr("!&;_$",(int)ch)!=NULL ? 1U : 0U;
    memcpy(out->code,code,strlen(code)+1); return MC_OK;
}

void MC_CALL mc_result_destroy(mc_result *result)
{
    if (result != NULL) { free(result->text); free(result); }
}

int32_t MC_CALL mc_result_copy_text(const mc_result *result, char *buffer, uint64_t capacity, uint64_t *required)
{
    if (required == NULL) return MC_INVALID_ARGUMENT;
    *required = 0;
    if (result == NULL) return MC_INVALID_ARGUMENT;
    size_t size = strlen(result->text) + 1;
    *required = (uint64_t) size;
    if (buffer == NULL || capacity < size) return MC_BUFFER_TOO_SMALL;
    memcpy(buffer, result->text, size);
    return MC_OK;
}

int32_t MC_CALL mc_result_statistics(const mc_result *result, mc_statistics *out, uint32_t size)
{
    if (result == NULL || out == NULL || size < sizeof(*out)) return MC_INVALID_ARGUMENT;
    *out = result->stats;
    return MC_OK;
}

uint32_t MC_CALL mc_alphabet_count(void) { return (uint32_t) morseAlphabetSize(); }
int32_t MC_CALL mc_alphabet_get(uint32_t index, mc_symbol *out, uint32_t size)
{
    if (out == NULL || size < sizeof(*out)) return MC_INVALID_ARGUMENT;
    unsigned char character;
    const char *code;
    if (!morseAlphabetEntry(index, &character, &code)) return MC_INVALID_ARGUMENT;
    *out = (mc_symbol) {0};
    out->character = character;
    out->extension = strchr("!&;_$", character) != NULL ? 1U : 0U;
    memcpy(out->code, code, strlen(code) + 1);
    return MC_OK;
}

int32_t MC_CALL mc_practice_create(uint32_t mode, uint32_t questions, uint32_t seed, mc_practice **out)
{
    if (out == NULL) return MC_INVALID_ARGUMENT;
    *out = NULL;
    if (mode > PRACTICE_MODE_WORD || questions == 0) return MC_INVALID_ARGUMENT;
    mc_practice *practice = malloc(sizeof(*practice));
    if (practice == NULL) return MC_OUT_OF_MEMORY;
    if (!practiceInit(&practice->session, (PracticeMode) mode, questions, seed)) {
        free(practice);
        return MC_INVALID_ARGUMENT;
    }
    *out = practice;
    return MC_OK;
}

void MC_CALL mc_practice_destroy(mc_practice *practice) { free(practice); }
int32_t MC_CALL mc_practice_next(mc_practice *practice, mc_result **out)
{
    if (out == NULL) return MC_INVALID_ARGUMENT;
    *out = NULL;
    if (practice == NULL) return MC_INVALID_ARGUMENT;
    PracticeSession trial = practice->session;
    PracticeQuestion question;
    if (!practiceNext(&trial, &question)) return MC_INVALID_STATE;
    int32_t status = mc_encode((const uint8_t *) question.answer, strlen(question.answer), 0, out);
    if (status == MC_OK) practice->session = trial;
    return status;
}

int32_t MC_CALL mc_practice_submit(mc_practice *practice, const uint8_t *answer, uint64_t length,
                                 mc_feedback *out, uint32_t size)
{
    if (practice == NULL || out == NULL || size < sizeof(*out)) return MC_INVALID_ARGUMENT;
    if (!practice->session.questionActive) return MC_INVALID_STATE;
    char *text = NULL;
    int32_t status = copyInput(answer, length, &text);
    if (status != MC_OK) return status;
    mc_feedback feedback = {0};
    feedback.question_number = practice->session.currentQuestion.number;
    memcpy(feedback.expected_answer, practice->session.currentQuestion.answer, sizeof(feedback.expected_answer));
    feedback.verdict = (int32_t) practiceSubmitAnswer(&practice->session, text);
    free(text);
    if (feedback.verdict == PRACTICE_RESULT_INVALID) return MC_INVALID_STATE;
    *out = feedback;
    return MC_OK;
}

int32_t MC_CALL mc_practice_stats(const mc_practice *practice, mc_practice_statistics *out, uint32_t size)
{
    if (practice == NULL || out == NULL || size < sizeof(*out)) return MC_INVALID_ARGUMENT;
    const PracticeSession *session = &practice->session;
    *out = (mc_practice_statistics) {session->totalQuestions, session->answered, session->correct,
        session->incorrect, session->skipped, (uint32_t) session->mode, practiceAccuracy(session)};
    return MC_OK;
}

static int32_t getAudioOptions(const mc_audio_options *source, uint32_t size, AudioOptions *out)
{
    *out = defaultAudioOptions();
    if (source == NULL) return size == 0 ? MC_OK : MC_INVALID_ARGUMENT;
    if (size < sizeof(*source) || source->reserved != 0 || source->wpm > INT_MAX ||
        source->frequency_hz > INT_MAX || source->sample_rate > INT_MAX) return MC_INVALID_ARGUMENT;
    *out = (AudioOptions) {(int) source->wpm, (int) source->frequency_hz, (int) source->sample_rate};
    return validateAudioOptions(out) ? MC_OK : MC_AUDIO_ERROR;
}

int32_t MC_CALL mc_audio_defaults(mc_audio_options *out, uint32_t size)
{
    if (out == NULL || size < sizeof(*out)) return MC_INVALID_ARGUMENT;
    AudioOptions options = defaultAudioOptions();
    *out = (mc_audio_options) {(uint32_t) options.wordsPerMinute, (uint32_t) options.frequencyHz, (uint32_t) options.sampleRate, 0};
    return MC_OK;
}

static int32_t audioInput(const uint8_t *bytes, uint64_t length, uint32_t separator,
                          const mc_audio_options *source, uint32_t size, AudioOptions *options,
                          char **input, char *actualSeparator)
{
    *input = NULL;
    int32_t status = getAudioOptions(source, size, options);
    if (status == MC_OK) status = getSeparator(separator, actualSeparator);
    if (status == MC_OK && length > AUDIO_MAX_MORSE_BYTES) status = MC_INPUT_TOO_LARGE;
    if (status == MC_OK) status = copyInput(bytes, length, input);
    return status;
}

int32_t MC_CALL mc_audio_analyze(const uint8_t *morse, uint64_t length, uint32_t separator,
                               const mc_audio_options *source, uint32_t options_size,
                               mc_audio_analysis *out, uint32_t size)
{
    if (out == NULL || size < sizeof(*out)) return MC_INVALID_ARGUMENT;
    AudioOptions options;
    char *input, actualSeparator;
    int32_t status = audioInput(morse, length, separator, source, options_size, &options, &input, &actualSeparator);
    if (status != MC_OK) return status;
    AudioAnalysis analysis;
    int success = analyzeMorseAudio(input, actualSeparator, &options, &analysis);
    free(input);
    if (!success) return MC_AUDIO_ERROR;
    *out = (mc_audio_analysis) {analysis.unitCount, analysis.sampleCount, analysis.toneCount};
    return MC_OK;
}

int32_t MC_CALL mc_write_wav(const char *path, const uint8_t *morse, uint64_t length, uint32_t separator,
                           const mc_audio_options *source, uint32_t options_size)
{
    if (path == NULL || path[0] == '\0') return MC_INVALID_ARGUMENT;
    if (!morseIsValidUtf8((const uint8_t *) path, strlen(path))) return MC_INVALID_UTF8;
    AudioOptions options;
    char *input, actualSeparator;
    int32_t status = audioInput(morse, length, separator, source, options_size, &options, &input, &actualSeparator);
    if (status != MC_OK) return status;
    int success = writeMorseWav(path, input, actualSeparator, &options);
    free(input);
    return success ? MC_OK : MC_AUDIO_ERROR;
}

const char *MC_CALL mc_audio_last_error(void) { return audioLastError(); }
int32_t MC_CALL mc_cancel_create(mc_cancel **out)
{
    if (out == NULL) return MC_INVALID_ARGUMENT;
    *out = malloc(sizeof(**out));
    if (*out == NULL) return MC_OUT_OF_MEMORY;
    initializeAudioCancellation(&(*out)->cancellation);
    return MC_OK;
}
void MC_CALL mc_cancel_request(mc_cancel *cancellation)
{ if (cancellation != NULL) requestAudioCancellation(&cancellation->cancellation); }
void MC_CALL mc_cancel_destroy(mc_cancel *cancellation) { free(cancellation); }

int32_t MC_CALL mc_play(const uint8_t *morse, uint64_t length, uint32_t separator,
                      const mc_audio_options *source, uint32_t options_size, const mc_cancel *cancellation)
{
    AudioOptions options;
    char *input, actualSeparator;
    int32_t status = audioInput(morse, length, separator, source, options_size, &options, &input, &actualSeparator);
    if (status != MC_OK) return status;
    const AudioCancellation *token = cancellation != NULL ? &cancellation->cancellation : NULL;
    int success = playMorseAudioControlled(input, actualSeparator, &options, token);
    free(input);
    if (success) return MC_OK;
    return audioCancellationRequested(token) ? MC_CANCELLED : MC_AUDIO_ERROR;
}
