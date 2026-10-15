#include "morsecore_native.h"
#include <stdio.h>
#include <string.h>
#include <stdint.h>

#define CHECK(x) do { if (!(x)) { fprintf(stderr, "FAIL native line %d: %s\n", __LINE__, #x); return 1; } } while (0)

int main(void)
{
    CHECK(mc_abi_version() == MC_ABI_VERSION);
    CHECK(strcmp(mc_version(), "0.4.0") == 0);
    mc_result *result = NULL;
    CHECK(mc_encode((const uint8_t *) "SOS", 3, 0, &result) == MC_OK && result != NULL);
    char text[64];
    uint64_t required = 0;
    CHECK(mc_result_copy_text(result, NULL, 0, &required) == MC_BUFFER_TOO_SMALL && required == 13);
    memset(text, 0x55, sizeof(text));
    CHECK(mc_result_copy_text(result, text, 1, &required) == MC_BUFFER_TOO_SMALL && text[0] == 0x55);
    CHECK(mc_result_copy_text(result, text, sizeof(text), &required) == MC_OK);
    CHECK(strcmp(text, "... --- ... ") == 0);
    mc_statistics statistics;
    CHECK(mc_result_statistics(result, &statistics, sizeof(statistics) - 1) == MC_INVALID_ARGUMENT);
    CHECK(mc_result_statistics(result, &statistics, sizeof(statistics)) == MC_OK);
    CHECK(statistics.input_characters == 3 && statistics.converted_characters == 3);
    mc_result_destroy(result);
    result = (mc_result *) (uintptr_t) 1;
    CHECK(mc_encode((const uint8_t *) "X", UINT64_MAX, 0, &result) == MC_INPUT_TOO_LARGE && result == NULL);
    CHECK(mc_encode((const uint8_t *) "\xc7", 1, 0, &result) == MC_INVALID_UTF8 && result == NULL);
    CHECK(mc_encode((const uint8_t *) "A\0B", 3, 0, &result) == MC_INVALID_INPUT && result == NULL);
    CHECK(mc_encode(NULL, 1, 0, &result) == MC_INVALID_ARGUMENT);
    CHECK(mc_encode(NULL, 0, 0, &result) == MC_EMPTY_INPUT);
    mc_symbol symbol;
    CHECK(mc_alphabet_count() == 54);
    CHECK(mc_alphabet_get(53, &symbol, sizeof(symbol)) == MC_OK && symbol.character == '$' && symbol.extension == 1);
    CHECK(mc_alphabet_get(54, &symbol, sizeof(symbol)) == MC_INVALID_ARGUMENT);

    mc_practice *practice = NULL;
    CHECK(mc_practice_create(0, 1, 123, &practice) == MC_OK);
    CHECK(mc_practice_next(practice, &result) == MC_OK);
    mc_result_destroy(result);
    mc_feedback feedback;
    CHECK(mc_practice_submit(practice, NULL, 0, &feedback, sizeof(feedback)) == MC_OK && feedback.verdict == 2);
    mc_practice_statistics progress;
    CHECK(mc_practice_stats(practice, &progress, sizeof(progress)) == MC_OK && progress.skipped == 1);
    CHECK(mc_practice_next(practice, &result) == MC_INVALID_STATE && result == NULL);
    mc_practice_destroy(practice);

    mc_audio_analysis analysis;
    CHECK(mc_audio_analyze((const uint8_t *) text, strlen(text), 0, NULL, 0, &analysis, sizeof(analysis)) == MC_OK);
    CHECK(analysis.units == 27 && analysis.samples == 35721 && analysis.tones == 9);
    mc_cancel *cancellation = NULL;
    CHECK(mc_cancel_create(&cancellation) == MC_OK);
    mc_cancel_request(cancellation);
    CHECK(mc_play((const uint8_t *) text, strlen(text), 0, NULL, 0, cancellation) == MC_CANCELLED);
    mc_cancel_destroy(cancellation);
    mc_result_destroy(NULL); mc_practice_destroy(NULL); mc_cancel_destroy(NULL); mc_cancel_request(NULL);
    puts("PASS: C consumer dynamically linked to ABI v1, ownership, struct sizes, strict UTF-8, overflow and pre-cancellation");
    return 0;
}
