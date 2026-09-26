using System;
using Unity.Collections;

[Serializable]
public struct LeaderboardEntry : IEquatable<LeaderboardEntry>
{
    public int Id;
    public FixedString64Bytes Username;
    public long Score;
    public int OriginalIndex;

    public bool Equals(LeaderboardEntry other) => Id == other.Id;
}
