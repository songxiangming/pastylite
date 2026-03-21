using System.Runtime.InteropServices;

namespace Pasty.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct INPUT
{
    public int Type;
    public INPUTUNION Union;
}

[StructLayout(LayoutKind.Explicit)]
internal struct INPUTUNION
{
    [FieldOffset(0)] public KEYBDINPUT Keyboard;
    [FieldOffset(0)] public MOUSEINPUT Mouse;
    [FieldOffset(0)] public HARDWAREINPUT Hardware;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    public ushort VirtualKey;
    public ushort ScanCode;
    public uint Flags;
    public uint Time;
    public IntPtr ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MOUSEINPUT
{
    public int Dx;
    public int Dy;
    public uint MouseData;
    public uint Flags;
    public uint Time;
    public IntPtr ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HARDWAREINPUT
{
    public uint Msg;
    public ushort ParamL;
    public ushort ParamH;
}
