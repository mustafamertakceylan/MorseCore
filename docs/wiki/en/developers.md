# Developers and contributions

MorseCore is open source. Bug reports, clearer translations and reproducible test results are contributions too; you do not need to write code.

## How to contribute

1. Check [Issues]({{REPO_URL}}/issues) for an existing report.
2. Include your operating system, MorseCore version, steps to reproduce and expected result.
3. Use example text or synthetic contact records. Do not attach a private logbook, access keys or personal audio recordings.
4. For code changes, keep the patch focused, run the relevant tests and open a pull request.

## Shared infrastructure

Windows and Mac use **the same C23 engine and AppCore business rules**. Their interfaces are WinUI and SwiftUI. Shared behaviour such as conversion, QSO records, voice packages and update verification should be implemented once.

- [Shared architecture](../../architecture.md)
- [Build Windows from source](../../desktop.md)
- [Build Mac from source](../../../apps/MorseCore.macOS/README.md)
- [Native API](../../native-api.md)

The Linux preview shares the XAML interface, AppCore and C23 engine. The [Linux guide](../../linux.md) separates package targets, automated checks and remaining distribution tests; CI success does not establish compatibility with every distribution.

## Update the Wiki

Pages are maintained in the repository's `docs/wiki/` folder. Edit that source; the publishing script generates language switches, navigation and links. See the [Wiki maintenance guide](../README.md).

## License and credits

MorseCore uses the [Apache 2.0 license]({{REPO_URL}}/blob/main/LICENSE). Speech engines and models have separate licenses. See [NOTICE]({{REPO_URL}}/blob/main/NOTICE) for EMA Lightning, Antalia-2 Mini, Piper and other credits. Preserve the relevant licenses and notices when redistributing.
