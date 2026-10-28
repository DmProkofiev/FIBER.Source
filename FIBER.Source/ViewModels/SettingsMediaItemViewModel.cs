using FIBER.Source.Models.Enums;
using System;

namespace FIBER.Source.ViewModels
{
    public class SettingsMediaItemViewModel : IDisposable
    {
        public MediaItemViewModel Media { get; }
        public MediaOwnerType OwnerType { get; }
        public int OwnerId { get; }
        public string OwnerName { get; }

        public SettingsMediaItemViewModel(MediaItemViewModel media, MediaOwnerType ownerType, int ownerId, string ownerName)
        {
            Media = media;
            OwnerType = ownerType;
            OwnerId = ownerId;
            OwnerName = ownerName;
        }

        // Отформатированная строка для заголовка (например: "Человек (ID: 1) - Иванов Иван")
        public string OwnerDisplay => OwnerType switch
        {
            MediaOwnerType.Person => $"Человек (ID: {OwnerId}) - {OwnerName}",
            MediaOwnerType.Company => $"Организация (ID: {OwnerId}) - {OwnerName}",
            MediaOwnerType.Event => $"Событие (ID: {OwnerId}) - {OwnerName}",
            _ => $"Неизвестно (ID: {OwnerId})"
        };

        public void Dispose()
        {
            Media.Dispose();
        }
    }
}