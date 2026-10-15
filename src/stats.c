#include <string.h>

#include "stats.h"
#include "morse.h"

static int characterByteLength(const unsigned char text[], int utf8)
{
    if(utf8 && text[0]>=0xF0) return 4;
    if(utf8 && text[0]>=0xE0) return 3;
    if(utf8 && text[0]>=0xC0) return 2;
    return 1;
}

static const unsigned char *skipBom(const char text[])
{
    return (const unsigned char *) text +
        (strncmp(text, "\xEF\xBB\xBF", 3) == 0 ? 3 : 0);
}

static size_t countCharacters(const char text[])
{
    size_t count = 0;
    const unsigned char *current = skipBom(text);
    int utf8=morseIsValidUtf8(current,strlen((const char *)current));
    while (*current != '\0') {
        count++;
        current += characterByteLength(current,utf8);
    }
    return count;
}

static void countPlainText(const char text[], ConversionStats *stats)
{
    int inWord = 0;
    const unsigned char *current = skipBom(text);
    int utf8=morseIsValidUtf8(current,strlen((const char *)current));
    stats->wordCount = 0;
    stats->convertedCharacterCount = 0;
    stats->spaceCount = 0;

    while (*current != '\0') {
        int width=characterByteLength(current,utf8);
        unsigned ch=*current;
        if(utf8 && width>1) {
            ch &= width==2 ? 31U : width==3 ? 15U : 7U;
            for(int i=1;i<width;i++) ch=(ch<<6) | (unsigned)(current[i]&63);
        }
        if(ch==0x640 || (ch>=0x64B && ch<=0x65F) || ch==0x670) { current+=width; continue; }
        if (ch == ' ') stats->spaceCount++;
        if (ch == ' ' || (ch>=9 && ch<=13) || ch==0xA0 || ch==0x202F) {
            inWord = 0;
        } else {
            stats->convertedCharacterCount++;
            if (!inWord) {
                stats->wordCount++;
                inWord = 1;
            }
        }
        current += width;
    }
}

void calculateConversionStats(const char input[], const char output[],
                              ConversionDirection direction, ConversionStats *stats)
{
    stats->inputLength = countCharacters(input);
    stats->outputLength = countCharacters(output);
    const char *plainText = direction == CONVERSION_TEXT_TO_MORSE ? input : output;
    countPlainText(plainText, stats);
}
