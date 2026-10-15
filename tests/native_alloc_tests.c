#include "morsecore_native.h"
#include <stdlib.h>
#include <stdio.h>
#include <string.h>

static int failAt = -1, allocation;
void *__real_malloc(size_t size);
void *__wrap_malloc(size_t size)
{
    if (failAt >= 0 && allocation++ == failAt) return NULL;
    return __real_malloc(size);
}
#define CHECK(x) do { if (!(x)) { fprintf(stderr, "FAIL allocation line %d: %s\n", __LINE__, #x); return 1; } } while (0)
static void failNext(int index) { allocation = 0; failAt = index; }

int main(void)
{
    mc_result *result = NULL;
    for (int i = 0; i < 3; i++) {
        failNext(i);
        CHECK(mc_encode((const uint8_t *) "SOS", 3, 0, &result) == MC_OUT_OF_MEMORY && result == NULL);
    }
    failNext(0);
    mc_practice *practice = NULL;
    CHECK(mc_practice_create(0, 2, 123, &practice) == MC_OUT_OF_MEMORY && practice == NULL);
    failNext(-1);
    CHECK(mc_practice_create(0, 2, 123, &practice) == MC_OK);
    for (int i = 0; i < 3; i++) {
        failNext(i);
        CHECK(mc_practice_next(practice, &result) == MC_OUT_OF_MEMORY && result == NULL);
    }
    failNext(-1);
    CHECK(mc_practice_next(practice, &result) == MC_OK);
    mc_result_destroy(result);
    mc_feedback feedback;
    memset(&feedback, 0x55, sizeof(feedback));
    failNext(0);
    CHECK(mc_practice_submit(practice, NULL, 0, &feedback, sizeof(feedback)) == MC_OUT_OF_MEMORY);
    CHECK((unsigned char)feedback.expected_answer[0] == 0x55);
    failNext(-1);
    mc_practice_statistics stats;
    CHECK(mc_practice_stats(practice, &stats, sizeof(stats)) == MC_OK && stats.answered == 0);
    CHECK(mc_practice_submit(practice, NULL, 0, &feedback, sizeof(feedback)) == MC_OK);
    mc_practice_destroy(practice);
    failNext(0);
    mc_cancel *cancel = NULL;
    CHECK(mc_cancel_create(&cancel) == MC_OUT_OF_MEMORY && cancel == NULL);
    failNext(-1);
    puts("PASS: native allocation failures, NULL outputs, unchanged feedback and practice state");
    return 0;
}
