using System;
[Flags]
public enum IntegrationMark : byte
{
    None = 0,
    LOSPass = 1,
    LOSBlock = 2,
    LOSC = 4,
    Awaiting = 8,
    Integrated = 16,
}