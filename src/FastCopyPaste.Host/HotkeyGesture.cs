using System.Text.Json.Serialization;

namespace FastCopyPaste.Host;

[Flags]
internal enum HotkeyModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Windows = 8
}

internal sealed record HotkeyGesture(
    [property: JsonPropertyName("virtualKey")] int VirtualKey,
    [property: JsonPropertyName("modifiers")] HotkeyModifiers Modifiers)
{
    private const HotkeyModifiers SupportedModifiers =
        HotkeyModifiers.Control |
        HotkeyModifiers.Alt |
        HotkeyModifiers.Shift |
        HotkeyModifiers.Windows;

    internal static HotkeyGesture Default { get; } =
        new(NativeMethods.VkV, HotkeyModifiers.Control);

    internal bool IsUsable =>
        VirtualKey is > 0 and <= 0xFE &&
        !IsModifierKey(VirtualKey) &&
        (Modifiers & ~SupportedModifiers) == HotkeyModifiers.None;

    internal bool Matches(int virtualKey, HotkeyModifiers pressedModifiers) =>
        IsUsable && VirtualKey == virtualKey && Modifiers == pressedModifiers;

    internal HotkeyGesture Normalize() => IsUsable ? this : Default;

    internal string ToDisplayString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
        parts.Add(GetKeyName(VirtualKey));
        return string.Join('+', parts);
    }

    internal static bool IsModifierKey(int virtualKey) => virtualKey is
        NativeMethods.VkShift or
        NativeMethods.VkControl or
        NativeMethods.VkAlt or
        NativeMethods.VkLeftShift or
        NativeMethods.VkRightShift or
        NativeMethods.VkLeftControl or
        NativeMethods.VkRightControl or
        NativeMethods.VkLeftAlt or
        NativeMethods.VkRightAlt or
        NativeMethods.VkLeftWindows or
        NativeMethods.VkRightWindows;

    private static string GetKeyName(int virtualKey)
    {
        if (virtualKey is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        var name = Enum.GetName((Keys)virtualKey);
        return string.IsNullOrWhiteSpace(name) ? $"VK_{virtualKey:X2}" : name;
    }
}
