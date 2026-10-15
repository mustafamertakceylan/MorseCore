#ifndef MORSECORE_NATIVE_H
#define MORSECORE_NATIVE_H

#include <stdint.h>

#if defined(_WIN32)
#  if defined(MORSECORE_NATIVE_EXPORTS)
#    define MC_API __declspec(dllexport)
#  else
#    define MC_API __declspec(dllimport)
#  endif
#  define MC_CALL __cdecl
#else
#  define MC_API __attribute__((visibility("default")))
#  define MC_CALL
#endif
#ifdef __cplusplus
extern "C" {
#endif

#define MC_ABI_VERSION UINT32_C(1)
#define MC_ALPHABET_LATIN UINT32_C(0)
#define MC_ALPHABET_RUSSIAN UINT32_C(1)
#define MC_ALPHABET_ARABIC UINT32_C(2)
#define MC_OK INT32_C(0)
#define MC_EMPTY_INPUT INT32_C(-1)
#define MC_INVALID_INPUT INT32_C(-2)
#define MC_UNSUPPORTED_CHARACTER INT32_C(-3)
#define MC_INVALID_MORSE INT32_C(-4)
#define MC_OUT_OF_MEMORY INT32_C(-5)
#define MC_INPUT_TOO_LARGE INT32_C(-6)
#define MC_INVALID_SEPARATOR INT32_C(-20)
#define MC_INVALID_ARGUMENT INT32_C(-100)
#define MC_BUFFER_TOO_SMALL INT32_C(-101)
#define MC_INVALID_STATE INT32_C(-102)
#define MC_AUDIO_ERROR INT32_C(-103)
#define MC_CANCELLED INT32_C(-104)
#define MC_INVALID_UTF8 INT32_C(-105)

typedef struct mc_result mc_result;
typedef struct mc_practice mc_practice;
typedef struct mc_cancel mc_cancel;

typedef struct {
    uint64_t input_characters, output_characters, words, converted_characters, spaces;
} mc_statistics;

typedef struct {
    uint32_t character; /* Unicode scalar; legacy mc_alphabet_get returns ASCII. */
    uint32_t extension; /* 1 only for ! & ; _ $. */
    char code[8];
} mc_symbol;

typedef struct {
    uint32_t total, answered, correct, incorrect, skipped, mode;
    double accuracy;
} mc_practice_statistics;

typedef struct {
    int32_t verdict; /* 0 incorrect, 1 correct, 2 skipped. */
    uint32_t question_number;
    char expected_answer[64]; /* Revealed only after a response. */
} mc_feedback;

typedef struct { uint32_t wpm, frequency_hz, sample_rate, reserved; } mc_audio_options;
typedef struct { uint64_t units, samples, tones; } mc_audio_analysis;

/* ABI v1: cdecl; natural alignment; fixed-width fields; no CRT objects cross it.
 * All byte spans are strict UTF-8, may lack NUL, and reject embedded NUL.
 * Lengths are bytes; statistics count characters. Separator 0 means '/' for
 * encoding/audio and automatic detection for decoding. Calls never print,
 * initialize runtime folders, change locale, or load user settings.
 * Functions run synchronously: a GUI must schedule long work off its UI thread.
 * Independent handles/stateless calls are concurrent-safe. Serialize operations
 * on one practice handle. A handle must remain alive during every use; destroy
 * it exactly once through this DLL. No destroy may race a call on that handle.
 * Struct output arguments include caller capacity in bytes (at least sizeof).
 */
MC_API uint32_t MC_CALL mc_abi_version(void);
MC_API const char *MC_CALL mc_version(void); /* Borrowed static UTF-8. */
MC_API const char *MC_CALL mc_status_message(int32_t status); /* Borrowed static UTF-8. */

/* Output handles become NULL on failure. Text belongs to the result; copy it
 * with mc_result_copy_text and release using mc_result_destroy, never free(). */
MC_API int32_t MC_CALL mc_encode(const uint8_t *input, uint64_t length, uint32_t separator, mc_result **out);
MC_API int32_t MC_CALL mc_decode(const uint8_t *input, uint64_t length, uint32_t separator, mc_result **out);
MC_API void MC_CALL mc_result_destroy(mc_result *result);
/* required includes NUL; NULL buffer/capacity 0 queries and returns BUFFER_TOO_SMALL.
 * A short buffer is never modified. Encoded text retains its trailing space. */
MC_API int32_t MC_CALL mc_result_copy_text(const mc_result *result, char *buffer, uint64_t capacity, uint64_t *required);
MC_API int32_t MC_CALL mc_result_statistics(const mc_result *result, mc_statistics *out, uint32_t size);
MC_API uint32_t MC_CALL mc_alphabet_count(void);
MC_API int32_t MC_CALL mc_alphabet_get(uint32_t index, mc_symbol *out, uint32_t size);
/* Additive ABI v1 extensions: profile 0 Latin/Turkish, 1 Russian, 2 Arabic.
 * Profile must be chosen for decoding: a Morse signal carries no alphabet ID.
 * Existing mc_encode/mc_decode and their alphabet are unchanged. */
MC_API int32_t MC_CALL mc_encode_alphabet(const uint8_t *input, uint64_t length, uint32_t separator, uint32_t profile, mc_result **out);
MC_API int32_t MC_CALL mc_decode_alphabet(const uint8_t *input, uint64_t length, uint32_t separator, uint32_t profile, mc_result **out);
MC_API uint32_t MC_CALL mc_alphabet_profile_count(uint32_t profile);
MC_API int32_t MC_CALL mc_alphabet_profile_get(uint32_t profile, uint32_t index, mc_symbol *out, uint32_t size);

/* mode: 0 letters, 1 digits, 2 words. Zero questions/unknown modes are invalid.
 * next returns Morse only; INVALID_STATE means active question or end of session.
 * submit accepts an empty span to skip. A failed call does not consume a question. */
MC_API int32_t MC_CALL mc_practice_create(uint32_t mode, uint32_t questions, uint32_t seed, mc_practice **out);
MC_API void MC_CALL mc_practice_destroy(mc_practice *practice);
MC_API int32_t MC_CALL mc_practice_next(mc_practice *practice, mc_result **out);
MC_API int32_t MC_CALL mc_practice_submit(mc_practice *practice, const uint8_t *answer, uint64_t length,
                                       mc_feedback *out, uint32_t size);
MC_API int32_t MC_CALL mc_practice_stats(const mc_practice *practice, mc_practice_statistics *out, uint32_t size);

/* NULL options/size 0 selects defaults. reserved must be 0. WAV path is a
 * NUL-terminated UTF-8 path; writing explicitly replaces an existing file.
 * Validate/confirm output paths in the host. File writes are synchronous.
 * Audio error text belongs to this thread until its next audio operation. Copy
 * it immediately on the worker thread, before handing a failure to another thread. */
MC_API int32_t MC_CALL mc_audio_defaults(mc_audio_options *out, uint32_t size);
MC_API int32_t MC_CALL mc_audio_analyze(const uint8_t *morse, uint64_t length, uint32_t separator,
                                     const mc_audio_options *options, uint32_t options_size,
                                     mc_audio_analysis *out, uint32_t size);
MC_API int32_t MC_CALL mc_write_wav(const char *path, const uint8_t *morse, uint64_t length, uint32_t separator,
                                 const mc_audio_options *options, uint32_t options_size);
MC_API const char *MC_CALL mc_audio_last_error(void);
/* Cancellation is one-shot. request can run concurrently with playback; destroy
 * only after playback and all request calls finish. NULL token disables it.
 * Pre-cancelled playback never opens an audio device. WAV export is not cancellable. */
MC_API int32_t MC_CALL mc_cancel_create(mc_cancel **out);
MC_API void MC_CALL mc_cancel_request(mc_cancel *cancellation);
MC_API void MC_CALL mc_cancel_destroy(mc_cancel *cancellation);
MC_API int32_t MC_CALL mc_play(const uint8_t *morse, uint64_t length, uint32_t separator,
                            const mc_audio_options *options, uint32_t options_size, const mc_cancel *cancellation);

#ifdef __cplusplus
}
#endif
#endif
