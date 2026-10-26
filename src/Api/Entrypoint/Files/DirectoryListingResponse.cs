using DTech.Core.Files;

namespace DTech.Entrypoint.Files;

public sealed record DirectoryListingResponse(string Path, string? ParentPath, IReadOnlyList<FileEntryResponse> Entries)
{
    public static DirectoryListingResponse From(DirectoryListing listing)
    {
        var entries = new List<FileEntryResponse>(listing.Entries.Count);
        for (var i = 0; i < listing.Entries.Count; i++)
        {
            var entry = FileEntryResponse.From(listing.Entries[i]);
            entries.Add(entry);
        }

        return new DirectoryListingResponse(listing.Path, listing.ParentPath, entries);
    }
}
