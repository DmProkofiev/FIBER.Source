using System;
using System.Collections.Generic;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.Models;

/// Метаданные и бинарное содержимое медиафайла
public class MediaAttachment
{
    /// Первичный ключ
    public int Id { get; set; }

    ///Исходное имя файла для отображения пользователю
    public string OriginalFileName { get; set; } = string.Empty;

    /// MIME-тип содержимого
    public string MimeType { get; set; } = string.Empty;

    /// Размер файла в байтах
    public long SizeBytes { get; set; }

    /// SHA-256 hex содержимого для дедупликации
    public string ContentHash { get; set; } = string.Empty;

    /// Бинарное содержимое файла, шифруемое SQLCipher
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// Дата добавления записи
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// Связи с владельцами (M2M)
    public ICollection<MediaAttachmentLink> Links { get; set; } = new List<MediaAttachmentLink>();
}