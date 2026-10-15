#include "morse_profiles.h"
#include <stdlib.h>
#include <string.h>
#include <stdckdint.h>

typedef struct { uint32_t character; const char *code; } Entry;
/* Signal Corps TM 11-459 (1943), Appendix VII, pp. 71-72.
 * https://www.govinfo.gov/content/pkg/GOVPUB-W-824b48e17943859ed2af2b45952379df/pdf/GOVPUB-W-824b48e17943859ed2af2b45952379df.pdf
 * Profiles are separate from the ITU Latin table, NOT ITU-standard alphabets.
 * Russian Yo is normalized to Ye. Arabic hamza has its own dot signal.
 * Modern Unicode normalization choices are documented in docs/desktop.tr.md. */
static const Entry russian[] = {
    {0x410,".-"},{0x411,"-..."},{0x412,".--"},{0x413,"--."},{0x414,"-.."},
    {0x415,"."},{0x416,"...-"},{0x417,"--.."},{0x418,".."},{0x419,".---"},
    {0x41A,"-.-"},{0x41B,".-.."},{0x41C,"--"},{0x41D,"-."},{0x41E,"---"},
    {0x41F,".--."},{0x420,".-."},{0x421,"..."},{0x422,"-"},{0x423,"..-"},
    {0x424,"..-."},{0x425,"...."},{0x426,"-.-."},{0x427,"---."},{0x428,"----"},
    {0x429,"--.-"},{0x42A,"--.--"},{0x42B,"-.--"},{0x42C,"-..-"},
    {0x42D,"..-.."},{0x42E,"..--"},{0x42F,".-.-"}
};
static const Entry arabic[] = {
    {0x627,".-"},{0x628,"-..."},{0x62A,"-"},{0x62B,"-.-."},{0x62C,".---"},
    {0x62D,"...."},{0x62E,"---"},{0x62F,"-.."},{0x630,"--.."},{0x631,".-."},
    {0x632,"---."},{0x633,"..."},{0x634,"----"},{0x635,"-..-"},{0x636,"...-"},
    {0x637,"..-"},{0x638,"-.--"},{0x639,".-.-"},{0x63A,"--."},{0x641,"..-."},
    {0x642,"--.-"},{0x643,"-.-"},{0x644,".-.."},{0x645,"--"},{0x646,"-."},
    {0x647,"..-.."},{0x648,".--"},{0x64A,".."},{0x621,"."}
};
static const Entry *table(uint32_t profile, size_t *size)
{
    if (profile == 1) { *size = sizeof(russian)/sizeof(*russian); return russian; }
    if (profile == 2) { *size = sizeof(arabic)/sizeof(*arabic); return arabic; }
    *size = 0; return NULL;
}
size_t morseProfileSize(uint32_t profile)
{
    if (profile == 0) return morseAlphabetSize();
    size_t size; (void)table(profile, &size);
    return size ? size + morseAlphabetSize() - 26 : 0;
}
int morseProfileEntry(uint32_t profile, size_t index, uint32_t *character, const char **code)
{
    if (character == NULL || code == NULL) return 0;
    if (profile != 0) {
        size_t size; const Entry *entries = table(profile, &size);
        if (entries == NULL) return 0;
        if (index < size) { *character=entries[index].character; *code=entries[index].code; return 1; }
        index = index-size+26;
    }
    unsigned char value;
    if (!morseAlphabetEntry(index, &value, code)) return 0;
    *character = value; return 1;
}
static uint32_t next(const unsigned char *text, size_t *offset)
{
    uint32_t value=text[(*offset)++];
    if (value < 128) return value;
    unsigned remaining=value < 224 ? 1U : value < 240 ? 2U : 3U;
    value &= (1U << (6U-remaining))-1U;
    while (remaining-- > 0) value=(value<<6) | (uint32_t)(text[(*offset)++] & 63);
    return value;
}
static int gap(uint32_t ch)
{ return ch==' ' || (ch>=9 && ch<=13) || ch==0xA0 || ch==0x202F; }
static uint32_t normalize(uint32_t ch, uint32_t profile)
{
    if (profile == 1) {
        if (ch >= 0x430 && ch <= 0x44F) ch-=32;
        if (ch==0x401 || ch==0x451) ch=0x415;
    } else if (profile == 2) {
        if (ch==0x622 || ch==0x623 || ch==0x625 || ch==0x671) ch=0x627;
        if (ch==0x624) ch=0x648;
        if (ch==0x626) ch=0x64A;
        if (ch==0x649) ch=0x64A;
        if (ch==0x629) ch=0x647;
        if (ch>=0x660 && ch<=0x669) ch=ch-0x660+'0';
        if (ch==0x60C) ch=',';
        if (ch==0x61B) ch=';';
        if (ch==0x61F) ch='?';
    }
    return ch;
}
static int ignored(uint32_t ch, uint32_t profile)
{ return profile==2 && (ch==0x640 || (ch>=0x64B && ch<=0x65F) || ch==0x670); }
static const char *findCharacter(uint32_t profile, uint32_t ch)
{
    for (size_t i=0;i<morseProfileSize(profile);i++) {
        uint32_t candidate; const char *code;
        if (morseProfileEntry(profile,i,&candidate,&code) && candidate==ch) return code;
    }
    return NULL;
}
static uint32_t findCode(uint32_t profile, const char *text, size_t length)
{
    for (size_t i=0;i<morseProfileSize(profile);i++) {
        uint32_t ch; const char *code;
        if (morseProfileEntry(profile,i,&ch,&code) && strlen(code)==length && memcmp(code,text,length)==0) return ch;
    }
    return 0;
}
static size_t writeCharacter(char *out, uint32_t ch)
{
    if (ch<128) { if(out) out[0]=(char)ch; return 1; }
    if(out) { out[0]=(char)(0xC0 | (ch>>6)); out[1]=(char)(0x80 | (ch&63)); }
    return 2; /* All entries in these profiles are below U+0800. */
}
static ResultCode run(const char *input, char *out, size_t *written, char separator, uint32_t profile, int decode)
{
    size_t length=strlen(input), offset=0, count=0;
    int converted=0, pending=0;
    if (length>=3 && memcmp(input,"\xEF\xBB\xBF",3)==0) offset=3;
    while (offset<length) {
        if (decode) {
            unsigned char ch=(unsigned char)input[offset];
            if (gap(ch)) { offset++; continue; }
            if (ch==(unsigned char)separator) { if(converted) pending=1; offset++; continue; }
            size_t start=offset;
            while(offset<length && !gap((unsigned char)input[offset]) && input[offset]!=separator) offset++;
            uint32_t value=findCode(profile,input+start,offset-start);
            if (!value) return RESULT_INVALID_MORSE;
            if(pending) { if(out) out[count]=' '; if(ckd_add(&count,count,(size_t)1)) return RESULT_INPUT_TOO_LARGE; }
            size_t addition=writeCharacter(NULL,value), position=count;
            if(ckd_add(&count,count,addition)) return RESULT_INPUT_TOO_LARGE;
            if(out) (void)writeCharacter(out+position,value);
        } else {
            uint32_t ch=next((const unsigned char *)input,&offset);
            if(ignored(ch,profile)) continue;
            if(gap(ch)) { if(converted) pending=1; continue; }
            ch=normalize(ch,profile);
            const char *code=findCharacter(profile,ch);
            if(code==NULL) return RESULT_UNSUPPORTED_CHARACTER;
            size_t addition=strlen(code)+1+(pending?2U:0U), position=count;
            if(ckd_add(&count,count,addition)) return RESULT_INPUT_TOO_LARGE;
            if(out) {
                if(pending) { out[position++]=separator; out[position++]=' '; }
                memcpy(out+position,code,strlen(code)); out[position+strlen(code)]=' ';
            }
        }
        converted=1; pending=0;
    }
    if(!converted) return RESULT_EMPTY_INPUT;
    *written=count;
    if(out) out[count]='\0';
    return RESULT_SUCCESS;
}
ResultCode morseProfileConvert(const char *input, char **output, char separator, uint32_t profile, int decode)
{
    if(output==NULL) return RESULT_INVALID_INPUT;
    *output=NULL;
    if(input==NULL) return RESULT_INVALID_INPUT;
    if(profile==0) return decode ? morseToTextAlloc(input,output,separator) : textToMorseAlloc(input,output,separator);
    if(morseProfileSize(profile)==0) return RESULT_INVALID_INPUT;
    if(separator!='/' && separator!='|' && separator!='#' && separator!='*') return RESULT_INVALID_SEPARATOR;
    if(!morseIsValidUtf8((const unsigned char *)input,strlen(input))) return RESULT_INVALID_INPUT;
    size_t size=0;
    ResultCode result=run(input,NULL,&size,separator,profile,decode);
    if(result!=RESULT_SUCCESS) return result;
    if(ckd_add(&size,size,(size_t)1)) return RESULT_INPUT_TOO_LARGE;
    char *buffer=malloc(size);
    if(buffer==NULL) return RESULT_OUT_OF_MEMORY;
    result=run(input,buffer,&size,separator,profile,decode);
    if(result!=RESULT_SUCCESS) { free(buffer); return result; }
    *output=buffer; return RESULT_SUCCESS;
}
