#include <locale.h>

#ifdef _WIN32
#include <windows.h>
#endif

#include "ui.h"

int main(void)
{
    setlocale(LC_ALL, "");

#ifdef _WIN32
    SetConsoleOutputCP(CP_UTF8);
    SetConsoleCP(CP_UTF8);
#endif

    runApplication();

    return 0;
}