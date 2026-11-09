namespace FIBER.Source.MediaPlayer
{
    /// <summary>
    /// Контракт медиаплеера. Позволяет внедрить любой другой движок
    /// без изменения кода пользовательского интерфейса.
    /// </summary>
    public interface IMediaPlayerService
    {
        /// <summary>Путь к медиафайлу.</summary>
        string FilePath { get; set; }

        /// <summary>Текущая позиция воспроизведения в секундах.</summary>
        double Position { get; set; }

        /// <summary>Общая продолжительность в секундах.</summary>
        double Duration { get; }

        /// <summary>Признак того, что медиа сейчас воспроизводится.</summary>
        bool IsPlaying { get; }

        /// <summary>Начать воспроизведение.</summary>
        void Play();

        /// <summary>Поставить на паузу.</summary>
        void Pause();

        /// <summary>Остановить воспроизведение.</summary>
        void Stop();
    }
}
