"""Apache-2.0 adapter for local Turkish models. No model downloads at runtime."""
import os
import sys
from pathlib import Path

os.environ["HF_HUB_OFFLINE"] = "1"
os.environ["TRANSFORMERS_OFFLINE"] = "1"
os.environ["HF_HUB_DISABLE_TELEMETRY"] = "1"


def main():
    engine, model_directory, output = sys.argv[1:]
    text = sys.stdin.buffer.read(16001).decode("utf-8", errors="strict").strip()
    if not text or len(text) > 4000:
        raise ValueError("Speech requires 1–4000 characters")
    model_directory = Path(model_directory).resolve(strict=True)
    os.environ["XDG_CACHE_HOME"] = str(model_directory.parent.parent / "cache")
    # Reject accidental network attempts even if a dependency changes its loader.
    def offline(event, _args):
        if event in ("socket.connect", "socket.getaddrinfo"):
            raise RuntimeError("Network is disabled during speech synthesis")
    sys.addaudithook(offline)
    import torch
    torch.set_num_threads(2)
    torch.set_num_interop_threads(1)
    if engine == "ema":
        # EMA's public constructor uses the Hub. Redirect its file resolver to
        # our installed files so it cannot check or fetch a remote revision.
        import ema_lightning.api as api

        def local_file(_repo, filename, **_kwargs):
            return str((model_directory / filename).resolve(strict=True))

        api.hf_hub_download = local_file
        model = api.EMA(device="cpu")
        # One desktop request at a time; skip GPU-oriented batch-size tuning.
        model._batch_size = 1
    elif engine == "antalia":
        from antalia_mini import Antalia
        model = Antalia(model=str(model_directory), device="cpu", threads=2, warmup=False)
    else:
        raise ValueError("Unknown speech engine")
    model.say(text, path=output, seed=0)


if __name__ == "__main__":
    main()
