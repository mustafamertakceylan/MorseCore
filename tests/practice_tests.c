#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "practice.h"

#define CHECK(condition) do { \
    if (!(condition)) { \
        fprintf(stderr, "FAIL line %d: %s\n", __LINE__, #condition); \
        return 1; \
    } \
} while (0)

static int nearlyEqual(double actual, double expected)
{
    double difference = actual - expected;
    return difference > -0.000001 && difference < 0.000001;
}

static int testGeneratedQuestions(void)
{
    const PracticeMode modes[] = {PRACTICE_MODE_LETTER, PRACTICE_MODE_DIGIT, PRACTICE_MODE_WORD};
    const uint32_t seeds[] = {0, 1, UINT32_MAX};

    for (size_t modeIndex = 0; modeIndex < sizeof(modes) / sizeof(modes[0]); modeIndex++) {
        for (size_t seedIndex = 0; seedIndex < sizeof(seeds) / sizeof(seeds[0]); seedIndex++) {
            PracticeSession first;
            PracticeSession second;
            CHECK(practiceInit(&first, modes[modeIndex], 2048, seeds[seedIndex]));
            CHECK(practiceInit(&second, modes[modeIndex], 2048, seeds[seedIndex]));
            unsigned char seen[128] = {0};
            char firstWord[PRACTICE_ANSWER_CAPACITY] = {0};
            int variedWords = 0;
            for (uint32_t number = 1; number <= 2048; number++) {
                struct {
                    PracticeQuestion question;
                    unsigned char guard[32];
                } bounded;
                PracticeQuestion repeated;
                memset(bounded.guard, 0xA5, sizeof(bounded.guard));
                CHECK(practiceNext(&first, &bounded.question));
                CHECK(practiceNext(&second, &repeated));
                CHECK(bounded.question.number == number);
                CHECK(bounded.question.mode == modes[modeIndex]);
                CHECK(memchr(bounded.question.answer, '\0', sizeof(bounded.question.answer)) != NULL);
                CHECK(memchr(repeated.answer, '\0', sizeof(repeated.answer)) != NULL);
                CHECK(strcmp(bounded.question.answer, repeated.answer) == 0);
                size_t length = strlen(bounded.question.answer);
                CHECK(length > 0 && length < PRACTICE_ANSWER_CAPACITY);
                for (size_t i = 0; i < sizeof(bounded.guard); i++) CHECK(bounded.guard[i] == 0xA5);
                for (size_t i = 0; i < length; i++) {
                    unsigned char value = (unsigned char) bounded.question.answer[i];
                    if (modes[modeIndex] == PRACTICE_MODE_DIGIT) CHECK(value >= '0' && value <= '9');
                    else CHECK(value >= 'A' && value <= 'Z');
                    seen[value] = 1;
                }
                if (modes[modeIndex] != PRACTICE_MODE_WORD) CHECK(length == 1);
                else {
                    CHECK(length >= 2);
                    if (number == 1) strcpy(firstWord, bounded.question.answer);
                    else if (strcmp(firstWord, bounded.question.answer) != 0) variedWords = 1;
                }
                CHECK(practiceSubmitAnswer(&first, bounded.question.answer) == PRACTICE_RESULT_CORRECT);
                CHECK(practiceSkip(&second) == PRACTICE_RESULT_SKIPPED);
            }
            CHECK(first.answered == 2048 && first.correct == 2048);
            CHECK(second.answered == 2048 && second.skipped == 2048);
            CHECK(nearlyEqual(practiceAccuracy(&first), 100.0));
            CHECK(nearlyEqual(practiceAccuracy(&second), 0.0));
            PracticeQuestion finished;
            CHECK(!practiceNext(&first, &finished));
            if (modes[modeIndex] == PRACTICE_MODE_WORD) CHECK(variedWords);
            else {
                char begin = modes[modeIndex] == PRACTICE_MODE_LETTER ? 'A' : '0';
                char end = modes[modeIndex] == PRACTICE_MODE_LETTER ? 'Z' : '9';
                for (char value = begin; value <= end; value++) CHECK(seen[(unsigned char) value]);
            }
        }
    }
    return 0;
}

static int testSessionAnswers(void)
{
    PracticeSession session;
    PracticeQuestion question;
    CHECK(practiceInit(&session, PRACTICE_MODE_WORD, 6, 27));
    CHECK(nearlyEqual(practiceAccuracy(&session), 0.0));
    CHECK(practiceSubmitAnswer(&session, "X") == PRACTICE_RESULT_INVALID);
    CHECK(practiceSkip(&session) == PRACTICE_RESULT_INVALID);
    CHECK(practiceNext(&session, &question));

    PracticeQuestion active = question;
    CHECK(!practiceNext(&session, &question));
    CHECK(strcmp(question.answer, active.answer) == 0);
    CHECK(session.answered == 0);
    CHECK(practiceSubmitAnswer(&session, NULL) == PRACTICE_RESULT_INVALID);

    char response[PRACTICE_ANSWER_CAPACITY + 20];
    strcpy(response, " \t\r\n\f\v");
    size_t offset = strlen(response);
    for (size_t i = 0; i < strlen(question.answer); i++) {
        response[offset++] = (char) (question.answer[i] - 'A' + 'a');
    }
    strcpy(response + offset, "\t\r\n\f\v ");
    CHECK(practiceSubmitAnswer(&session, response) == PRACTICE_RESULT_CORRECT);
    CHECK(practiceSubmitAnswer(&session, response) == PRACTICE_RESULT_INVALID);
    CHECK(session.answered == 1 && session.correct == 1);

    CHECK(practiceNext(&session, &question));
    snprintf(response, sizeof(response), "X%s", question.answer);
    CHECK(practiceSubmitAnswer(&session, response) == PRACTICE_RESULT_INCORRECT);
    CHECK(practiceNext(&session, &question));
    snprintf(response, sizeof(response), "%sX", question.answer);
    CHECK(practiceSubmitAnswer(&session, response) == PRACTICE_RESULT_INCORRECT);
    CHECK(practiceNext(&session, &question));
    CHECK(practiceSubmitAnswer(&session, question.answer + 1) == PRACTICE_RESULT_INCORRECT);
    CHECK(practiceNext(&session, &question));
    CHECK(practiceSubmitAnswer(&session, " \t\r\n\f\v") == PRACTICE_RESULT_SKIPPED);
    CHECK(practiceNext(&session, &question));
    CHECK(practiceSubmitAnswer(&session, "") == PRACTICE_RESULT_SKIPPED);
    CHECK(!practiceNext(&session, &question));
    CHECK(practiceSkip(&session) == PRACTICE_RESULT_INVALID);
    CHECK(session.answered == 6 && session.correct == 1 && session.incorrect == 3 && session.skipped == 2);
    CHECK(nearlyEqual(practiceAccuracy(&session), 100.0 / 6.0));
    return 0;
}

static int testInvalidInputsAndLimits(void)
{
    PracticeSession session;
    PracticeQuestion question;
    CHECK(!practiceInit(NULL, PRACTICE_MODE_LETTER, 1, 1));
    CHECK(practiceInit(&session, PRACTICE_MODE_LETTER, 1, 1));
    CHECK(!practiceInit(&session, (PracticeMode) -1, 1, 1));
    CHECK(!practiceInit(&session, PRACTICE_MODE_WORD, 0, 1));
    CHECK(!practiceNext(NULL, &question));
    CHECK(!practiceNext(&session, NULL));
    CHECK(practiceSubmitAnswer(NULL, "A") == PRACTICE_RESULT_INVALID);
    CHECK(practiceSkip(NULL) == PRACTICE_RESULT_INVALID);
    CHECK(nearlyEqual(practiceAccuracy(NULL), 0.0));
    CHECK(practiceNext(&session, &question));
    char longAnswer[8192];
    memset(longAnswer, 'A', sizeof(longAnswer) - 1);
    longAnswer[sizeof(longAnswer) - 1] = '\0';
    CHECK(practiceSubmitAnswer(&session, longAnswer) == PRACTICE_RESULT_INCORRECT);
    CHECK(session.answered == 1 && session.incorrect == 1);

    /* Model the final question of the maximum-sized session without billions
     * of iterations, keeping the documented counter invariant intact. */
    CHECK(practiceInit(&session, PRACTICE_MODE_DIGIT, UINT32_MAX, UINT32_MAX));
    session.answered = UINT32_MAX - 1;
    session.correct = UINT32_MAX - 1;
    CHECK(practiceNext(&session, &question));
    CHECK(question.number == UINT32_MAX);
    CHECK(practiceSubmitAnswer(&session, question.answer) == PRACTICE_RESULT_CORRECT);
    CHECK(session.answered == UINT32_MAX && session.correct == UINT32_MAX);
    CHECK(nearlyEqual(practiceAccuracy(&session), 100.0));
    CHECK(!practiceNext(&session, &question));
    CHECK(practiceSkip(&session) == PRACTICE_RESULT_INVALID);
    CHECK(practiceSubmitAnswer(&session, "0") == PRACTICE_RESULT_INVALID);
    CHECK(session.answered == UINT32_MAX);

    CHECK(practiceInit(&session, PRACTICE_MODE_DIGIT, UINT32_MAX, 0));
    session.answered = UINT32_MAX - 1;
    session.skipped = UINT32_MAX - 1;
    CHECK(practiceNext(&session, &question));
    CHECK(practiceSkip(&session) == PRACTICE_RESULT_SKIPPED);
    CHECK(session.answered == UINT32_MAX && session.skipped == UINT32_MAX);
    CHECK(nearlyEqual(practiceAccuracy(&session), 0.0));
    return 0;
}

static int testIndependentRandomState(void)
{
    srand(724);
    int expected = rand();
    srand(724);
    PracticeSession session;
    PracticeQuestion question;
    CHECK(practiceInit(&session, PRACTICE_MODE_LETTER, 100, 18));
    for (int i = 0; i < 100; i++) {
        CHECK(practiceNext(&session, &question));
        CHECK(practiceSkip(&session) == PRACTICE_RESULT_SKIPPED);
    }
    CHECK(rand() == expected);
    return 0;
}

int main(void)
{
    CHECK(testGeneratedQuestions() == 0);
    CHECK(testSessionAnswers() == 0);
    CHECK(testInvalidInputsAndLimits() == 0);
    CHECK(testIndependentRandomState() == 0);
    puts("PASS: practice generation, reproducible sessions, answer scoring, skips, limits, independent RNG");
    return 0;
}
