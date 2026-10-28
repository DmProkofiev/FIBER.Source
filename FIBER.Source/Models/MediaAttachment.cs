using System;
using System.Collections.Generic;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.Models;

/// <summary>
/// Метаданные и бинарное содержимое медиафайла, хранимые внутри зашифрованной БД.
/// </summary>
public class MediaAttachment
{
    /// <summary>Первичный ключ.</summary>
    public int Id { get; set; }

    /// <summary>Исходное имя файла для отображения пользователю.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>MIME-тип содержимого.</summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>Размер файла в байтах.</summary>
    public long SizeBytes { get; set; }

    /// <summary>SHA-256 hex содержимого для дедупликации.</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>Бинарное содержимое файла, шифруемое SQLCipher.</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>Дата добавления записи.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Связи с владельцами (M2M).</summary>
    public ICollection<MediaAttachmentLink> Links { get; set; } = new List<MediaAttachmentLink>();
}