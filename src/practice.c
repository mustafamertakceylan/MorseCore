#include "practice.h"

#include <stddef.h>
#include <string.h>

static const char *const practiceWords[] = {
    "AIR", "BIRD", "BOAT", "BOOK", "CAMP", "CAT", "CODE", "CORE",
    "DOG", "EARTH", "FIRE", "FISH", "FLAG", "HOME", "LIGHT", "MAP",
    "MOON", "MORSE", "NIGHT", "NORTH", "OCEAN", "RADIO", "RAIN", "ROAD",
    "ROCK", "SEA", "SHIP", "SKY", "STAR", "SUN", "TREE", "WAVE"
};

static int validMode(PracticeMode mode)
{
    return mode == PRACTICE_MODE_LETTER || mode == PRACTICE_MODE_DIGIT ||
           mode == PRACTICE_MODE_WORD;
}

static int validSession(const PracticeSession *session)
{
    if (session == NULL || !validMode(session->mode) ||
        session->totalQuestions == 0 || session->randomState == 0 ||
        session->answered > session->totalQuestions ||
        session->correct > session->answered ||
        session->incorrect > session->answered - session->correct) {
        return 0;
    }
    return session->skipped == session->answered - session->correct - session->incorrect &&
           (session->questionActive == 0 || session->questionActive == 1);
}

static uint32_t nextRandom(PracticeSession *session)
{
    uint32_t value = session->randomState;
    value ^= value << 13;
    value ^= value >> 17;
    value ^= value << 5;
    session->randomState = value;
    return value;
}

static uint32_t randomIndex(PracticeSession *session, uint32_t count)
{
    /* xorshift32 produces 1..UINT32_MAX. Reject the incomplete final bucket
     * before mapping that range to 0..count-1, avoiding modulo bias. */
    uint32_t ceiling = UINT32_MAX - UINT32_MAX % count;
    uint32_t value;
    do {
        value = nextRandom(session);
    } while (value > ceiling);
    return (value - 1) % count;
}

static int asciiSpace(unsigned char value)
{
    return value == ' ' || value == '\t' || value == '\n' ||
           value == '\r' || value == '\f' || value == '\v';
}

static unsigned char asciiUpper(unsigned char value)
{
    return value >= 'a' && value <= 'z' ? (unsigned char) (value - 'a' + 'A') : value;
}

static int matchesAnswer(const char *expected, const char *answer)
{
    while (*expected != '\0') {
        if (asciiUpper((unsigned char) *answer) != (unsigned char) *expected) {
            return 0;
        }
        expected++;
        answer++;
    }
    while (asciiSpace((unsigned char) *answer)) answer++;
    return *answer == '\0';
}

int practiceInit(PracticeSession *session, PracticeMode mode,
                 uint32_t totalQuestions, uint32_t seed)
{
    if (session == NULL || !validMode(mode) || totalQuestions == 0) return 0;
    *session = (PracticeSession) {0};
    session->mode = mode;
    session->totalQuestions = totalQuestions;
    /* Zero is the only absorbing xorshift state. */
    session->randomState = seed != 0 ? seed : UINT32_C(0x6D2B79F5);
    return 1;
}

int practiceNext(PracticeSession *session, PracticeQuestion *question)
{
    if (!validSession(session) || question == NULL || session->questionActive ||
        session->answered == session->totalQuestions) {
        return 0;
    }

    PracticeQuestion next = {0};
    next.mode = session->mode;
    next.number = session->answered + 1;
    if (session->mode == PRACTICE_MODE_WORD) {
        uint32_t count = (uint32_t) (sizeof(practiceWords) / sizeof(practiceWords[0]));
        const char *word = practiceWords[randomIndex(session, count)];
        size_t length = strlen(word);
        if (length >= sizeof(next.answer)) return 0;
        memcpy(next.answer, word, length + 1);
    } else {
        uint32_t first = session->mode == PRACTICE_MODE_LETTER ? (uint32_t) 'A' : (uint32_t) '0';
        uint32_t count = session->mode == PRACTICE_MODE_LETTER ? 26 : 10;
        next.answer[0] = (char) (first + randomIndex(session, count));
    }
    session->currentQuestion = next;
    session->questionActive = 1;
    *question = next;
    return 1;
}

PracticeResult practiceSkip(PracticeSession *session)
{
    if (!validSession(session) || !session->questionActive ||
        session->answered == session->totalQuestions) {
        return PRACTICE_RESULT_INVALID;
    }
    session->skipped++;
    session->answered++;
    session->questionActive = 0;
    return PRACTICE_RESULT_SKIPPED;
}

PracticeResult practiceSubmitAnswer(PracticeSession *session, const char *answer)
{
    if (!validSession(session) || answer == NULL || !session->questionActive ||
        session->answered == session->totalQuestions) {
        return PRACTICE_RESULT_INVALID;
    }
    while (asciiSpace((unsigned char) *answer)) answer++;
    if (*answer == '\0') return practiceSkip(session);

    PracticeResult result = matchesAnswer(session->currentQuestion.answer, answer)
                            ? PRACTICE_RESULT_CORRECT : PRACTICE_RESULT_INCORRECT;
    if (result == PRACTICE_RESULT_CORRECT) session->correct++;
    else session->incorrect++;
    session->answered++;
    session->questionActive = 0;
    return result;
}

double practiceAccuracy(const PracticeSession *session)
{
    if (!validSession(session) || session->answered == 0) return 0.0;
    return (double) session->correct / (double) session->answered * 100.0;
}
