using FIBER.Source.Models.Enums;
using System;

namespace FIBER.Source.Contracts;

/// <summary>
/// Чистый DTO медиаэлемента без зависимости от слоя представления.
/// Используется <see cref="IMediaService"/> для передачи метаданных медиа
/// из сервисного слоя в ViewModels, которые сами создают MediaItemViewModel.
/// </summary>
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

/// <summary>
/// Медиа с указанием владельца — для списков «все медиа».
/// </summary>
public sealed record MediaForOwnerDto(
    MediaItemDto Media,
    MediaOwnerType OwnerType,
    int OwnerId
);
