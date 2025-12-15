using System;

namespace I40Sharp.Messaging.Models;

/// <summary>
/// Sub-types that can further describe the intent of an I4.0 message frame.
/// Combined with a primary type (e.g. callForProposal) they form type/subtype tokens.
/// </summary>
public enum I40MessageTypeSubtypes
{
    None = 0,
    ProcessChain,
    ManufacturingSequence,
    TransportRequest
}

public static class I40MessageTypeSubtypesExtensions
{
    public static string ToProtocolString(this I40MessageTypeSubtypes subtype) =>
        subtype switch
        {
            I40MessageTypeSubtypes.ProcessChain => "ProcessChain",
            I40MessageTypeSubtypes.ManufacturingSequence => "ManufacturingSequence",
            I40MessageTypeSubtypes.TransportRequest => "TransportRequest",
            _ => string.Empty
        };

    public static bool TryParse(string? raw, out I40MessageTypeSubtypes subtype)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            subtype = I40MessageTypeSubtypes.None;
            return false;
        }

        if (Enum.TryParse<I40MessageTypeSubtypes>(raw.Trim(), ignoreCase: true, out var parsed))
        {
            subtype = parsed;
            return true;
        }

        subtype = I40MessageTypeSubtypes.None;
        return false;
    }
}
