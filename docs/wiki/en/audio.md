# Audio and offline voices

Open **Audio** in the sidebar. Its four tabs keep different jobs separate.

![MorseCore audio workspace]({{MEDIA_URL}}/wiki/audio.en.png)

*The image shows macOS; the Windows layout may differ.*

## Morse audio

Enter text or Morse code and select the input type and alphabet. Press **Play Morse**, or **Export WAV** to save the tones. **Advanced settings** changes speed and tone frequency. Voice packs are not needed for Morse audio. This produces computer audio; it does not transmit radio signals.

## Read text aloud

Choose the speech language and an installed voice, enter your text and press **Speak**. You can also use Morse input, which is decoded before being spoken. **Stop** cancels generation or playback. Speech requests accept up to 4,000 characters; split longer messages.

## Decode audio

Choose a WAV file or record an audio input, then decode it. Use clean, single-tone Morse: speech, music and overlapping stations are unsupported.

- WAV files: up to **120 seconds / 64 MiB**, mono or stereo PCM/float.
- Microphone recording: up to **30 seconds on Windows**, **120 seconds on Mac**. Grant microphone permission when asked; stop recording to decode.
- `□` marks uncertain characters. If results are poor, reduce noise or adjust tone, speed and threshold under **Advanced settings**.

## Voice packs

Choose a language, press **Download and install**, confirm, then **Listen to sample → Use this voice**.

| Language | Voices |
|---|---|
| Turkish | EMA Lightning · Antalia-2 Mini |
| English | Joe · Piper |
| German | Thorsten · Kerstin · Piper |
| Russian | Dmitri · Denis · Piper |
| Arabic | Kareem · Piper, Jordanian Arabic |

The first installation needs internet and may prepare extra runtime components. Afterward speech is generated offline, without an API key. Model size excludes shared dependencies. Leaving the package page cancels installation; retry to reuse verified downloads. Removing a voice keeps other voices and your records.

Thanks to [EMA Lightning](https://huggingface.co/canberkkkkkk/ema-lightning), [Antalia](https://huggingface.co/cloud0day3/antalia-mini) and [Piper](https://github.com/OHF-Voice/piper1-gpl). EMA/Antalia code and weights use Apache-2.0; the Piper engine uses GPL-3.0. Voice models retain their own terms. Read **Source and license** before redistributing; Kareem's source card does not clearly state the dataset license.
