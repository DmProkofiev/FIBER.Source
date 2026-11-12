using FIBER.Source.Models.Enums;
using System;

namespace FIBER.Source.Contracts;

public sealed record MediaItemDto(
    int? MediaId,
    string FilePath,
    string OriginalFileName,
    string MimeType,
    long SizeBytes,
    string ContentHash,
    bool IsImage,
    bool IsVideo,
    bool IsAudio
);

public sealed record MediaForOwnerDto(
    MediaItemDto Media,
    MediaOwnerType OwnerType,
    int OwnerId
);
