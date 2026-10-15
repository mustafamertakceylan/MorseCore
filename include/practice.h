#ifndef PRACTICE_H
#define PRACTICE_H

#include <stdint.h>

#define PRACTICE_ANSWER_CAPACITY 64

typedef enum {
    PRACTICE_MODE_LETTER,
    PRACTICE_MODE_DIGIT,
    PRACTICE_MODE_WORD
} PracticeMode;

typedef enum {
    PRACTICE_RESULT_INVALID = -1,
    PRACTICE_RESULT_INCORRECT = 0,
    PRACTICE_RESULT_CORRECT = 1,
    PRACTICE_RESULT_SKIPPED = 2
} PracticeResult;

typedef struct {
    char answer[PRACTICE_ANSWER_CAPACITY];
    uint32_t number; /* One-based question number. */
    PracticeMode mode;
} PracticeQuestion;

/* Inspect counters freely, but change session state only through this API.
 * A question stays active until one answer or skip consumes it. */
typedef struct {
    uint32_t totalQuestions;
    uint32_t answered; /* Includes skipped questions. */
    uint32_t correct;
    uint32_t incorrect;
    uint32_t skipped;
    PracticeMode mode;
    uint32_t randomState;
    int questionActive;
    PracticeQuestion currentQuestion;
} PracticeSession;

/* Returns 1 on success; NULL, an unknown mode, or zero questions returns 0
 * without changing the session. All seeds are valid and deterministic,
 * including zero. No global random state, allocation, or I/O is used. */
int practiceInit(PracticeSession *session, PracticeMode mode,
                 uint32_t totalQuestions, uint32_t seed);

/* Returns 1 with an uppercase ASCII answer, otherwise 0. Calling this while
 * a question is active, after completion, or with NULL leaves state unchanged.
 * The caller encodes answer to Morse or plays it through a separate audio API. */
int practiceNext(PracticeSession *session, PracticeQuestion *question);

/* One response consumes the active question. Answers must be NUL-terminated.
 * ASCII case and surrounding ASCII whitespace are ignored; a blank response
 * skips. NULL or no active question returns INVALID without consuming it. */
PracticeResult practiceSubmitAnswer(PracticeSession *session, const char *answer);
PracticeResult practiceSkip(PracticeSession *session);

/* Correct / answered * 100, counting skips in the denominator; 0 before the
 * first answer or for NULL. Session progress is answered / totalQuestions. */
double practiceAccuracy(const PracticeSession *session);

#endif
