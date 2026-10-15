namespace MorseCore.AppCore;

public sealed record OpenSourceCredit(string Name,string Author,string License,Uri Source);

public static class OpenSourceCredits
{
    public static IReadOnlyList<OpenSourceCredit> Projects { get; }=Array.AsReadOnly<OpenSourceCredit>([
        new("EMA Lightning","Canberk Aslan","Apache-2.0",new("https://github.com/canberk7/ema-lightning")),
        new("Antalia-2 Mini","PatientDesk AI","Apache-2.0",new("https://huggingface.co/cloud0day3/antalia-mini")),
        new("Piper","Open Home Foundation / Home Assistant contributors","GPL-3.0-or-later",new("https://github.com/OHF-Voice/piper1-gpl")),
        new("normalizer-tr","Erdem Tuna","Apache-2.0",new("https://github.com/erdemtuna/normalizer-tr")),
        new("PyTorch","PyTorch contributors","BSD-3-Clause",new("https://github.com/pytorch/pytorch")),
        new("ONNX Runtime","Microsoft / ONNX Runtime contributors","MIT",new("https://github.com/microsoft/onnxruntime"))
    ]);
}
