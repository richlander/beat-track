namespace BeatTrack.Core;

public sealed record BeatTrackSlice(
    string Name,
    IReadOnlyDictionary<string, double> ArtistWeights);

public sealed record BeatTrackSliceComparison(
    string SliceAName,
    string SliceBName,
    IReadOnlyList<BeatTrackSliceArtist> OnlyA,
    IReadOnlyList<BeatTrackSliceArtist> OnlyB,
    IReadOnlyList<BeatTrackSliceSharedArtist> Shared);

public sealed record BeatTrackSliceArtist(
    string CanonicalName,
    double Weight);

public sealed record BeatTrackSliceSharedArtist(
    string CanonicalName,
    double WeightA,
    double WeightB);

public static class BeatTrackSliceComparer
{
    public static BeatTrackSliceComparison Compare(BeatTrackSlice sliceA, BeatTrackSlice sliceB)
    {
        var allKeys = new HashSet<string>(sliceA.ArtistWeights.Keys, StringComparer.OrdinalIgnoreCase);
        allKeys.UnionWith(sliceB.ArtistWeights.Keys);

        var onlyA = new List<BeatTrackSliceArtist>();
        var onlyB = new List<BeatTrackSliceArtist>();
        var shared = new List<BeatTrackSliceSharedArtist>();

        foreach (var key in allKeys)
        {
            var inA = sliceA.ArtistWeights.TryGetValue(key, out var weightA);
            var inB = sliceB.ArtistWeights.TryGetValue(key, out var weightB);

            if (inA && inB)
            {
                shared.Add(new BeatTrackSliceSharedArtist(key, weightA, weightB));
            }
            else if (inA)
            {
                onlyA.Add(new BeatTrackSliceArtist(key, weightA));
            }
            else
            {
                onlyB.Add(new BeatTrackSliceArtist(key, weightB));
            }
        }

        return new BeatTrackSliceComparison(
            sliceA.Name,
            sliceB.Name,
            onlyA.OrderByDescending(static x => x.Weight).ToList(),
            onlyB.OrderByDescending(static x => x.Weight).ToList(),
            shared.OrderByDescending(static x => x.WeightA + x.WeightB).ToList());
    }
}
