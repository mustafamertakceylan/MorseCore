#ifndef CLI_H
#define CLI_H

/* UTF-8 arguments; main calls this only when argc > 1. Returns a process exit code. */
int runCommandLine(int argc, char *argv[]);

#endif
