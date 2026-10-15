#include <stdlib.h>
#include <string.h>
#include <stdint.h>

#include "morse.h"
#include "constants.h"

typedef struct {
    unsigned char character;
    const char *code;
} MorseEntry;

/* ITU-R M.1677-1, Annex 1, 1.1.1-1.1.3:
 * https://www.itu.int/dms_pubrec/itu-r/rec/m/R-REC-M.1677-1-200910-I!!PDF-E.pdf
 * ! & ; _ $ are common extensions, not ITU-standard punctuation:
 * https://morsecode.world/international/morse2.html */
static const MorseEntry alphabet[] = {
    {'A', ".-"}, {'B', "-..."}, {'C', "-.-."}, {'D', "-.."}, {'E', "."},
    {'F', "..-."}, {'G', "--."}, {'H', "...."}, {'I', ".."}, {'J', ".---"},
    {'K', "-.-"}, {'L', ".-.."}, {'M', "--"}, {'N', "-."}, {'O', "---"},
    {'P', ".--."}, {'Q', "--.-"}, {'R', ".-."}, {'S', "..."}, {'T', "-"},
    {'U', "..-"}, {'V', "...-"}, {'W', ".--"}, {'X', "-..-"}, {'Y', "-.--"},
    {'Z', "--.."}, {'0', "-----"}, {'1', ".----"}, {'2', "..---"},
    {'3', "...--"}, {'4', "....-"}, {'5', "....."}, {'6', "-...."},
    {'7', "--..."}, {'8', "---.."}, {'9', "----."},
    {'.', ".-.-.-"}, {',', "--..--"}, {'?', "..--.."}, {'\'', ".----."},
    {'/', "-..-."}, {'(', "-.--."}, {')', "-.--.-"}, {':', "---..."},
    {'=', "-...-"}, {'+', ".-.-."}, {'-', "-....-"}, {'"', ".-..-."},
    {'@', ".--.-."},
    {'!', "-.-.--"}, {'&', ".-..."}, {';', "-.-.-."}, {'_', "..--.-"},
    {'$', "...-..-"}
};

#define ALPHABET_SIZE (sizeof(alphabet) / sizeof(alphabet[0]))
static_assert(ALPHABET_SIZE == MORSE_COUNT, "Morse table and count must agree");

static int isGap(unsigned char ch)
{
    return ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n' ||
           ch == '\v' || ch == '\f';
}

static int validSeparator(char separator)
{
    return separator == '/' || separator == '|' || separator == '#' || separator == '*';
}

static unsigned char normalizeLegacy(unsigned char ch)
{
    switch (ch) {
        case 0xC7: case 0xE7: return 'C';
        case 0xD0: case 0xF0: return 'G';
        case 0xDD: case 0xFD: return 'I';
        case 0xD6: case 0xF6: return 'O';
        case 0xDE: case 0xFE: return 'S';
        case 0xDC: case 0xFC: return 'U';
        default: return 0;
    }
}

/* Validate the entire stream before selecting UTF-8 or the deliberately limited
 * Windows-1254 fallback. Invalid UTF-8 is never silently skipped. */
int morseIsValidUtf8(const unsigned char *text, size_t length)
{
    if (text == NULL) return length == 0;
    for (size_t i = 0; i < length;) {
        unsigned char first = text[i++];
        if (first < 0x80) continue;
        size_t continuation;
        uint32_t value, minimum;
        if (first >= 0xC2 && first <= 0xDF) {
            continuation = 1; value = first & 0x1F; minimum = 0x80;
        } else if (first >= 0xE0 && first <= 0xEF) {
            continuation = 2; value = first & 0x0F; minimum = 0x800;
        } else if (first >= 0xF0 && first <= 0xF4) {
            continuation = 3; value = first & 0x07; minimum = 0x10000;
        } else {
            return 0;
        }
        if (continuation > length - i) return 0;
        for (size_t j = 0; j < continuation; j++) {
            if ((text[i] & 0xC0) != 0x80) return 0;
            value = (value << 6) | (text[i++] & 0x3F);
        }
        if (value < minimum || value > 0x10FFFF ||
            (value >= 0xD800 && value <= 0xDFFF)) return 0;
    }
    return 1;
}

static ResultCode selectEncoding(const unsigned char *text, size_t length, int *utf8)
{
    *utf8 = morseIsValidUtf8(text, length);
    if (*utf8) return RESULT_SUCCESS;
    for (size_t i = 0; i < length; i++) {
        if (text[i] >= 0x80 && normalizeLegacy(text[i]) == 0) return RESULT_INVALID_INPUT;
    }
    return RESULT_SUCCESS;
}

static unsigned char nextCharacter(const unsigned char *text, size_t *offset, int utf8)
{
    unsigned char first = text[(*offset)++];
    if (first < 0x80) return first >= 'a' && first <= 'z' ? first - 'a' + 'A' : first;
    if (!utf8) return normalizeLegacy(first);
    unsigned char second = text[(*offset)++];
    if (first == 0xC3) {
        if (second == 0x87 || second == 0xA7) return 'C';
        if (second == 0x96 || second == 0xB6) return 'O';
        if (second == 0x9C || second == 0xBC) return 'U';
    } else if (first == 0xC4) {
        if (second == 0x9E || second == 0x9F) return 'G';
        if (second == 0xB0 || second == 0xB1) return 'I';
    } else if (first == 0xC5 && (second == 0x9E || second == 0x9F)) {
        return 'S';
    }
    return 0;
}

static const MorseEntry *findCharacter(unsigned char ch)
{
    for (size_t i = 0; i < ALPHABET_SIZE; i++) {
        if (alphabet[i].character == ch) return &alphabet[i];
    }
    return NULL;
}

static const MorseEntry *findCode(const char *token, size_t length)
{
    for (size_t i = 0; i < ALPHABET_SIZE; i++) {
        if (strlen(alphabet[i].code) == length &&
            memcmp(alphabet[i].code, token, length) == 0) return &alphabet[i];
    }
    return NULL;
}

static size_t skipBom(const char *text, size_t length)
{
    return length >= 3 && memcmp(text, "\xEF\xBB\xBF", 3) == 0 ? 3 : 0;
}

ResultCode textToMorseAlloc(const char *text, char **output, char wordSeparator)
{
    if (output == NULL) return RESULT_INVALID_INPUT;
    *output = NULL;
    if (text == NULL) return RESULT_INVALID_INPUT;
    if (!validSeparator(wordSeparator)) return RESULT_INVALID_SEPARATOR;
    size_t length = strlen(text), start = skipBom(text, length), needed = 1;
    const unsigned char *bytes = (const unsigned char *) text;
    int utf8, converted = 0, pendingGap = 0;
    ResultCode result = selectEncoding(bytes + start, length - start, &utf8);
    if (result != RESULT_SUCCESS) return result;
    if (start != 0 && !utf8) return RESULT_INVALID_INPUT;
    for (size_t i = start; i < length;) {
        unsigned char ch = nextCharacter(bytes, &i, utf8);
        if (isGap(ch)) { pendingGap = converted; continue; }
        const MorseEntry *entry = findCharacter(ch);
        if (entry == NULL) return RESULT_UNSUPPORTED_CHARACTER;
        size_t addition = strlen(entry->code) + 1 + (pendingGap ? 2 : 0);
        if (addition > SIZE_MAX - needed) return RESULT_INPUT_TOO_LARGE;
        needed += addition;
        converted = 1;
        pendingGap = 0;
    }
    if (!converted) return RESULT_EMPTY_INPUT;
    char *encoded = malloc(needed);
    if (encoded == NULL) return RESULT_OUT_OF_MEMORY;
    size_t written = 0;
    converted = pendingGap = 0;
    for (size_t i = start; i < length;) {
        unsigned char ch = nextCharacter(bytes, &i, utf8);
        if (isGap(ch)) { pendingGap = converted; continue; }
        const MorseEntry *entry = findCharacter(ch);
        if (pendingGap) { encoded[written++] = wordSeparator; encoded[written++] = ' '; }
        size_t codeLength = strlen(entry->code);
        memcpy(encoded + written, entry->code, codeLength);
        written += codeLength;
        encoded[written++] = ' ';
        converted = 1;
        pendingGap = 0;
    }
    encoded[written] = '\0';
    *output = encoded;
    return RESULT_SUCCESS;
}

ResultCode morseToTextAlloc(const char *morseInput, char **output, char wordSeparator)
{
    if (output == NULL) return RESULT_INVALID_INPUT;
    *output = NULL;
    if (morseInput == NULL) return RESULT_INVALID_INPUT;
    if (!validSeparator(wordSeparator)) return RESULT_INVALID_SEPARATOR;
    size_t length = strlen(morseInput), start = skipBom(morseInput, length), needed = 1;
    int converted = 0;
    for (size_t i = start; i < length;) {
        if (isGap((unsigned char) morseInput[i])) { i++; continue; }
        size_t tokenStart = i;
        while (i < length && !isGap((unsigned char) morseInput[i])) i++;
        size_t tokenLength = i - tokenStart;
        if (tokenLength != 1 || morseInput[tokenStart] != wordSeparator) {
            if (findCode(morseInput + tokenStart, tokenLength) == NULL) return RESULT_INVALID_MORSE;
            converted = 1;
        }
        if (needed == SIZE_MAX) return RESULT_INPUT_TOO_LARGE;
        needed++;
    }
    if (!converted) return RESULT_EMPTY_INPUT;
    char *decoded = malloc(needed);
    if (decoded == NULL) return RESULT_OUT_OF_MEMORY;
    size_t written = 0;
    for (size_t i = start; i < length;) {
        if (isGap((unsigned char) morseInput[i])) { i++; continue; }
        size_t tokenStart = i;
        while (i < length && !isGap((unsigned char) morseInput[i])) i++;
        size_t tokenLength = i - tokenStart;
        decoded[written++] = tokenLength == 1 && morseInput[tokenStart] == wordSeparator ?
            ' ' : (char) findCode(morseInput + tokenStart, tokenLength)->character;
    }
    decoded[written] = '\0';
    *output = decoded;
    return RESULT_SUCCESS;
}

size_t morseAlphabetSize(void) { return ALPHABET_SIZE; }

int morseAlphabetEntry(size_t index, unsigned char *character, const char **code)
{
    if (index >= ALPHABET_SIZE || character == NULL || code == NULL) return 0;
    *character = alphabet[index].character;
    *code = alphabet[index].code;
    return 1;
}
