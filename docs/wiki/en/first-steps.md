# First steps

![Text and Morse conversion]({{MEDIA_URL}}/converter.en.png)

## Convert a message

1. Open **Converter** and choose **Text → Morse**.
2. Select the conversion alphabet: **Latin / Turkish**, **Russian** or **Arabic**. This is separate from the interface language.
3. Enter a short message, such as `HELLO WORLD`, then select **Convert**.
4. Copy the result, save it as a text file, or choose **Open in Audio** to hear it. Playback and speech controls live on [Audio](audio.md).

For the reverse direction, choose **Morse → Text**, select the alphabet used to produce the message, and paste the code. Morse does not identify its own alphabet. In **Settings**, choose the word separator (`/`, `|`, `#` or `*`); use the same separator when decoding.

## What changes during conversion?

Turkish letters become their Latin equivalents; Russian `Ё` becomes `Е`. Arabic vowel marks and tatweel are omitted, and some letter variants are simplified. Original spelling cannot be recovered by decoding. Unsupported characters produce an error; try the matching alphabet or remove unsupported symbols.

Character counts use Unicode scalar values, excluding a leading byte-order mark. Words are counted in the plain text: input when encoding, result when decoding. Spaces and supported whitespace separate words. These counts are neither file sizes nor counts of visually combined characters.

## Convert files

Open **Files**, choose the direction, alphabet and destination folder, then select one or more UTF-8 `.txt` files. Each file may be up to **8 MiB**. Select **Convert** and check the result summary. Existing output files are preserved; choose another destination or rename the existing result before retrying.

For appearance, language and default folders, open **Settings**. Need the app first? Follow [Installation](installation.md).
