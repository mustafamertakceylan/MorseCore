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

        case TXT_SECTION_CONVERT:
            return isTurkish ? "DONUSTUR" : "CONVERT";
        case TXT_SECTION_FILES:
            return isTurkish ? "DOSYALAR" : "FILES";
        case TXT_SECTION_TOOLS:
            return isTurkish ? "ARACLAR" : "TOOLS";

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
            return isTurkish ? "MorseCore Hakkinda" : "About MorseCore";

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
                ? "Metinleri ve metin dosyalarini Morse koduna cevirin veya cozumleyin."
                : "Convert text and text files to Morse code and back.";

        case TXT_ABOUT_CONVERSION:
            return isTurkish ? "Harfleri ve rakamlari iki yonde donusturun."
                             : "Convert letters and numbers in either direction.";

        case TXT_ABOUT_TURKISH:
            return isTurkish ? "Turkce harfleri donusturun; kelime ayiracini otomatik algilayin."
                             : "Normalize Turkish letters and detect word separators.";

        case TXT_ABOUT_FILES:
            return isTurkish ? "Sonuclari data/ klasorunde numarali .txt dosyalarina kaydedin."
                             : "Save results as numbered .txt files in data/.";

        case TXT_ABOUT_SETTINGS:
            return isTurkish ? "Dil ve ayirac tercihlerinizi sonraki oturumlarda da kullanin."
                             : "Keep your language and separator preferences between sessions.";

        case TXT_ABOUT_SUPPORTED:
            return isTurkish ? "Desteklenen karakterler" : "Supported characters";

        case TXT_ABOUT_INPUT_LIMIT:
            return isTurkish ? "En fazla girdi" : "Maximum input size";

        case TXT_BYTES:
            return isTurkish ? "bayt" : "bytes";

        case TXT_ABOUT_AUTHOR:
            return isTurkish ? "Gelistirici" : "Author";

        case TXT_ABOUT_LICENSE:
            return isTurkish ? "Lisans" : "License";

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
