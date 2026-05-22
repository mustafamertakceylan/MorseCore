#include <string.h>
#include "language.h"

int isSupportedLanguage(const char language[])
{
    return strcmp(language, "EN") == 0 || strcmp(language, "TR") == 0;
}

const char* getText(const char language[], LanguageTextKey key)
{
    int isTurkish = strcmp(language, "TR") == 0;

    switch (key) {
        case TXT_CHOOSE_OPTION:
            return isTurkish ? "Secim yapin: " : "Choose an option: ";

        case TXT_PRESS_ENTER:
            return isTurkish ? "Devam etmek icin Enter'a basin..." : "Press Enter to continue...";

        case TXT_MAIN_TITLE:
            return "MorseCore";

        case TXT_MENU_TEXT_TO_MORSE:
            return isTurkish ? "Metni Morse'a Cevir" : "Text to Morse";

        case TXT_MENU_MORSE_TO_TEXT:
            return isTurkish ? "Morse'u Metne Cevir" : "Morse to Text";

        case TXT_MENU_FILE_TEXT_TO_MORSE:
            return isTurkish ? "Dosyadan Metin Oku ve Morse'a Cevir" : "Read text from file and convert to Morse";

        case TXT_MENU_FILE_MORSE_TO_TEXT:
            return isTurkish ? "Dosyadan Morse Oku ve Metne Cevir" : "Read Morse from file and convert to Text";

        case TXT_MENU_SHOW_TABLE:
            return isTurkish ? "Morse Tablosunu Goster" : "Show Morse Table";

        case TXT_MENU_ABOUT:
            return isTurkish ? "Proje Hakkinda" : "About Project";

        case TXT_MENU_SETTINGS:
            return isTurkish ? "Ayarlar" : "Settings";

        case TXT_MENU_EXIT:
            return isTurkish ? "Cikis" : "Exit";

        case TXT_SETTINGS_TITLE:
            return isTurkish ? "Ayarlar" : "Settings";

        case TXT_CURRENT_SEPARATOR:
            return isTurkish ? "Gecerli kelime ayiraci" : "Current word separator";

        case TXT_CURRENT_LANGUAGE:
            return isTurkish ? "Gecerli dil" : "Current language";

        case TXT_CHANGE_SEPARATOR:
            return isTurkish ? "Kelime ayiracini degistir" : "Change word separator";

        case TXT_CHANGE_LANGUAGE:
            return isTurkish ? "Dili degistir" : "Change language";

        case TXT_BACK:
            return isTurkish ? "Geri" : "Back";

        case TXT_SEPARATOR_SAVED:
            return isTurkish ? "Kelime ayiraci basariyla kaydedildi." : "Separator saved successfully.";

        case TXT_LANGUAGE_SAVED:
            return isTurkish ? "Dil basariyla kaydedildi." : "Language saved successfully.";

        case TXT_NEW_SEPARATOR:
            return isTurkish ? "Yeni kelime ayiraci" : "New separator";

        case TXT_NEW_LANGUAGE:
            return isTurkish ? "Yeni dil" : "New language";

        case TXT_TITLE_TEXT_TO_MORSE:
            return isTurkish ? "Metni Morse'a Cevir" : "Text to Morse";

        case TXT_TITLE_MORSE_TO_TEXT:
            return isTurkish ? "Morse'u Metne Cevir" : "Morse to Text";

        case TXT_TITLE_FILE_TEXT_TO_MORSE:
            return isTurkish ? "Dosyadan Metni Morse'a Cevir" : "File Text to Morse";

        case TXT_TITLE_FILE_MORSE_TO_TEXT:
            return isTurkish ? "Dosyadan Morse'u Metne Cevir" : "File Morse to Text";

        case TXT_TITLE_MORSE_TABLE:
            return isTurkish ? "Morse Tablosu" : "Morse Table";

        case TXT_TITLE_CHANGE_SEPARATOR:
            return isTurkish ? "Kelime Ayiracini Degistir" : "Change Word Separator";

        case TXT_TITLE_CHANGE_LANGUAGE:
            return isTurkish ? "Dili Degistir" : "Change Language";

        case TXT_ENTER_TEXT:
            return isTurkish ? "Metin girin: " : "Enter text: ";

        case TXT_ENTER_MORSE_CODE:
            return isTurkish ? "Morse kodu girin: " : "Enter Morse code: ";

        case TXT_ENTER_INPUT_FILE_NAME:
            return isTurkish ? "Data klasorunden metin dosyasi adini girin: " : "Enter input file name from data folder: ";

        case TXT_ENTER_MORSE_FILE_NAME:
            return isTurkish ? "Data klasorunden Morse dosyasi adini girin: " : "Enter Morse file name from data folder: ";

        case TXT_ENTER_NEW_SEPARATOR:
            return isTurkish ? "Yeni ayiraci girin: " : "Enter new separator: ";

        case TXT_ENTER_NEW_LANGUAGE:
            return isTurkish ? "Yeni dili girin: " : "Enter new language: ";

        case TXT_MORSE_OUTPUT:
            return isTurkish ? "Morse ciktisi:" : "Morse output:";

        case TXT_TEXT_OUTPUT:
            return isTurkish ? "Metin ciktisi:" : "Text output:";

        case TXT_INPUT_FILE_PREVIEW:
            return isTurkish ? "Girdi dosyasi onizlemesi:" : "Input file preview:";

        case TXT_MORSE_OUTPUT_PREVIEW:
            return isTurkish ? "Morse ciktisi onizlemesi:" : "Morse output preview:";

        case TXT_MORSE_INPUT_FILE_PREVIEW:
            return isTurkish ? "Morse girdi dosyasi onizlemesi:" : "Morse input file preview:";

        case TXT_DECODED_TEXT_PREVIEW:
            return isTurkish ? "Cozulen metin onizlemesi:" : "Decoded text preview:";

        case TXT_DETECTED_SEPARATOR:
            return isTurkish ? "Algilanan/aktif ayirac" : "Detected/active separator";

        case TXT_FILE_CONVERTED_SUCCESSFULLY:
            return isTurkish ? "Dosya basariyla donusturuldu" : "File converted successfully";

        case TXT_OUTPUT_SAVED_SUCCESSFULLY:
            return isTurkish ? "Cikti basariyla kaydedildi" : "Output saved successfully";

        case TXT_DO_YOU_WANT_TO_SAVE:
            return isTurkish ? "Bu ciktiyi kaydetmek istiyor musunuz? (y/n): " : "Do you want to save this output? (y/n): ";

        case TXT_EXITING_IN_SECONDS:
            return isTurkish ? "MorseCore 3 saniye icinde kapatiliyor..." : "Exiting MorseCore in 3 seconds...";

        case TXT_CONVERSION_STATISTICS:
            return isTurkish ? "Donusum Istatistikleri" : "Conversion Statistics";

        case TXT_INPUT_LENGTH:
            return isTurkish ? "Girdi uzunlugu" : "Input length";

        case TXT_OUTPUT_LENGTH:
            return isTurkish ? "Cikti uzunlugu" : "Output length";

        case TXT_WORD_COUNT:
            return isTurkish ? "Kelime sayisi" : "Word count";

        case TXT_CONVERTED_CHARACTERS:
            return isTurkish ? "Donusturulen karakterler" : "Converted characters";

        case TXT_SPACE_COUNT:
            return isTurkish ? "Bosluk sayisi" : "Space count";

        case TXT_SESSION_SUMMARY:
            return isTurkish ? "Oturum Ozeti" : "Session Summary";

        case TXT_TEXT_TO_MORSE_OPERATIONS:
            return isTurkish ? "Metinden Morse'a islemler" : "Text to Morse operations";

        case TXT_MORSE_TO_TEXT_OPERATIONS:
            return isTurkish ? "Morse'dan metne islemler" : "Morse to Text operations";

        case TXT_FILE_OPERATIONS:
            return isTurkish ? "Dosya islemleri" : "File operations";

        case TXT_INVALID_INPUTS:
            return isTurkish ? "Gecersiz girdiler" : "Invalid inputs";

        case TXT_SAVED_OUTPUTS:
            return isTurkish ? "Kaydedilen ciktilar" : "Saved outputs";

        case TXT_CURRENT_SEPARATOR_SHORT:
            return isTurkish ? "Mevcut ayirac" : "Current separator";

        case TXT_ALLOWED_SEPARATORS:
            return isTurkish ? "Izin verilen ayiraclar: / | # *" : "Allowed separators: / | # *";

        case TXT_ALLOWED_LANGUAGES:
            return isTurkish ? "Izin verilen diller: EN / TR" : "Allowed languages: EN / TR";

        case TXT_ABOUT_TITLE:
            return isTurkish ? "MorseCore Hakkinda" : "About MorseCore";

        case TXT_ABOUT_DESCRIPTION:
            return isTurkish
                ? "MorseCore, C dili ile gelistirilmis konsol tabanli bir Morse kodlayici ve cozucu projesidir."
                : "MorseCore is a console-based Morse encoder and decoder written in C.";

        case TXT_ABOUT_DEMONSTRATES:
            return isTurkish ? "Bu proje sunlari gostermektedir:" : "This project demonstrates:";

        case TXT_ABOUT_2D_ARRAY:
            return isTurkish ? "2 boyutlu dizi kullanimi" : "2D array usage";

        case TXT_ABOUT_FILE_IO:
            return isTurkish ? "Dosya okuma ve dosya yazma islemleri" : "File input and output";

        case TXT_ABOUT_STRING_OPERATIONS:
            return isTurkish ? "String ve karakter dizisi islemleri" : "String operations";

        case TXT_ABOUT_MODULAR_DESIGN:
            return isTurkish ? "Fonksiyon tabanli moduler tasarim" : "Function-based modular design";

        case TXT_ABOUT_HEADER_SOURCE:
            return isTurkish ? "Header ve source dosyalarinin ayrilmasi" : "Header/source file separation";

        case TXT_ABOUT_SWITCH_CASE:
            return isTurkish ? "Switch-case tabanli menu kontrolu" : "Switch-case menu structure";

        case TXT_ABOUT_INPUT_VALIDATION:
            return isTurkish ? "Input dogrulama sistemi" : "Input validation";

        case TXT_ABOUT_TURKISH_NORMALIZATION:
            return isTurkish ? "Turkce karakter normalizasyonu" : "Turkish character normalization";

        case TXT_ABOUT_AUTO_FILE_NAMING:
            return isTurkish ? "Otomatik cikti dosyasi isimlendirme" : "Automatic output file naming";

        case TXT_ABOUT_PERSISTENT_SETTINGS:
            return isTurkish ? "Kalici separator ve dil ayarlari" : "Persistent separator and language settings";

        case TXT_ABOUT_STRUCT_POINTER:
            return isTurkish ? "Struct ve pointer kullanimi" : "Struct and pointer usage through statistics";

        case TXT_ABOUT_AUTHOR:
            return isTurkish ? "Gelistirici" : "Author";

        case TXT_ABOUT_STUDENT_NUMBER:
            return isTurkish ? "Ogrenci Numarasi" : "Student Number";

        case TXT_ERROR_UNKNOWN:
            return isTurkish ? "Bilinmeyen hata." : "Unknown error.";

        case TXT_ERROR_EMPTY_INPUT:
            return isTurkish ? "Girdi bos olamaz." : "Input cannot be empty.";

        case TXT_ERROR_INVALID_INPUT:
            return isTurkish ? "Gecersiz girdi." : "Invalid input.";

        case TXT_ERROR_UNSUPPORTED_CHARACTER:
            return isTurkish ? "Desteklenmeyen karakter algilandi." : "Unsupported character detected.";

        case TXT_ERROR_INVALID_MORSE:
            return isTurkish ? "Gecersiz veya bos Morse girdisi." : "Invalid or empty Morse input.";

        case TXT_ERROR_FILE_OPEN:
            return isTurkish ? "Dosya acilamadi." : "File could not be opened.";

        case TXT_ERROR_FILE_WRITE:
            return isTurkish ? "Dosyaya yazilamadi." : "File could not be written.";

        case TXT_ERROR_INVALID_SEPARATOR:
            return isTurkish ? "Gecersiz ayirac." : "Invalid separator.";

        case TXT_ERROR_SETTINGS_LOAD:
            return isTurkish ? "Ayarlar yuklenemedi." : "Settings could not be loaded.";

        case TXT_ERROR_SETTINGS_SAVE:
            return isTurkish ? "Ayarlar kaydedilemedi." : "Settings could not be saved.";

        default:
            return "";
    }
}