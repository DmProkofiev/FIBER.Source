# Техническое задание: FIBER Source 1.0.0

## 1. Назначение

FIBER Source — десктопное приложение для хранения и управления связанными данными о личностях (`Person`), организациях (`Company`) и событиях (`Event`). Данные имеют сложную взаимосвязь между собой. К каждой записи можно прикреплять медиафайлы: изображения, видео, аудио. Все данные (база данных и медиафайлы) хранятся локально в **едином зашифрованном файле базы данных** (`fiber.db`), защищённом паролем.

---

## 2. Технологический стек

- **Язык:** C# (.NET 10).
- **UI-паттерн:** MVVM.
- **Архитектура:** Асинхронное оконное приложение (WPF).
- **Хостинг:** `Microsoft.Extensions.Hosting` для управления жизненным циклом, DI и фоновыми службами.
- **Хранение данных:** SQLite (с расширением SQLCipher) + EF Core.
- **MVVM-инфраструктура:** `CommunityToolkit.Mvvm` (команды, `ObservableObject`, генераторы свойств, `ObservableValidator`).

---

## 3. Хранение данных и безопасность

### 3.1. Формат хранения данных

Все данные хранятся в **едином файле** `fiber.db` (база данных SQLite).
- Файл базы данных **полностью зашифрован** с использованием алгоритма AES-256 (через SQLCipher).
- Медиафайлы (изображения, видео, аудио) хранятся непосредственно внутри базы данных в виде BLOB-полей (`byte[]`).
- Отдельные папки для медиафайлов, ZIP-архивы или временные рабочие директории для БД не создаются.

### 3.2. Жизненный цикл данных

1. **Запуск:** Пользователь запускает приложение и вводит пароль. Приложение открывает файл БД с этим паролем. Если файла нет — он создаётся автоматически, с присвоением наименования и пароля.
2. **Работа:** Все операции чтения и записи (CRUD) выполняются напрямую через EF Core. SQLCipher на лету расшифровывает нужные страницы в оперативной памяти и шифрует их обратно при записи.
3. **Временные файлы:** Для воспроизведения видео и аудио (так как `MediaPlayer` в WPF требует физический путь к файлу) BLOB-данные извлекаются во временную директорию `%Temp%\FIBER\`. Эти файлы удаляются сразу после закрытия карточки или приложения.
4. **Завершение:** При выходе из приложения временные файлы очищаются, соединение с базой данных закрывается. На диске остаётся только зашифрованный файл БД.

### 3.3. Защита от повреждения при аварийном завершении

Поскольку приложение работает напрямую с базой данных, защита обеспечивается встроенными механизмами SQLite/SQLCipher:
1. Включён режим WAL (Write-Ahead Logging) для предотвращения повреждения базы при сбоях питания.
2. Все транзакции атомарны. При аварийном завершении незавершённые транзакции откатываются, база данных остаётся валидной и зашифрованной.
3. Никакие незашифрованные данные не остаются на диске.

### 3.4. Аутентификация и управление

- При запуске: ввод пароля для расшифровки базы данных (окно идентификации).
- В настройках приложения доступен функционал:
  - Смена пароля (перешифровка всего файла `fiber.db`).
  - Экспорт (создание резервной копии зашифрованного/незашифрованного (по выбору) файла `fiber.db` в выбранную папку).

---

## 4. Модели данных и связи

Модели анемичные: содержат только свойства. Конфигурация EF Core вынесена в `FiberSourceDbContext.OnModelCreating` (и отдельные классы `IEntityTypeConfiguration`).

### 4.1. Сущности

**Доменные:**
- `Person` — личность. Может быть идентифицированной или анонимной. ФИО разбито на три поля; добавлено поле `Gender`.
- `PersonAlias` — имя, под которым известна личность (ник, кличка, ФИО).
- `Company` — организация.
- `Event` — событие.
- `MediaAttachment` — файл с метаданными и бинарными данными (BLOB).
- `Country` — справочник стран (ISO 3166-1 alpha-2), заполняется через `HasData`.
- `AuditLogEntry` — журнал действий приложения.

**Связующие:**
- `EventParticipant` — участие в событии (payload: `Role`, `Notes`).
- `PersonRelationship` — связь между людьми, self-ref (payload: `Kind`, `Notes`).
- `CompanyInteraction` — связь между организациями, self-ref. В фазе 1.0.0 **не развивается**, сохраняется в БД без изменений (см. §8).
- `MediaAttachmentLink` — M2M-связь медиа с владельцами (Person/Company/Event).

**Удалённые:**
- `Employment` — признан недостаточным (composite key запрещает повторный найм, нет полей подразделения/типа занятости, нет `Id` для `Update`). Полностью удаляется в 1.0.0. Замена (`PersonCompanyRole`) — в фазе будущего (см. §8).

**Перечисления:**
- `IdentityStatus` — `Anonymous = 0`, `Identified = 1`.
- `Gender` — `Unknown = 0`, `Male = 1`, `Female = 2`.
- `AliasType` — `LegalName`, `Nickname`, `Alias`, `Handle`, `Other`.
- `MediaOwnerType` — `Person`, `Company`, `Event`.
- `PersonRelationKind` — вид связи Person↔Person (см. §4.4).
- `EventParticipantRole` — вид участия Person↔Event (см. §4.4).

### 4.2. Ключевые модели

`Person`:

~~~csharp
public class Person
{
    public int Id { get; set; }
    public IdentityStatus Status { get; set; } = IdentityStatus.Anonymous;

    public string? LastName { get; set; }
    public string? FirstName { get; set; }
    public string? SecondName { get; set; }

    public Gender Gender { get; set; } = Gender.Unknown;

    public DateTime? BirthDate { get; set; }
    public string? Passport { get; set; }
    public string? Job { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? CountryCode { get; set; }
    public string? Car { get; set; }
    public string? Mail { get; set; }
    public string? SocialMedia { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? MergedIntoPersonId { get; set; }

    public Country? Country { get; set; }
    public Person? MergedInto { get; set; }

    public ICollection<PersonAlias> Aliases { get; set; } = new List<PersonAlias>();
    public ICollection<EventParticipant> EventParticipations { get; set; } = new List<EventParticipant>();
    public ICollection<Event> OrganizerEvents { get; set; } = new List<Event>();
    public ICollection<PersonRelationship> Relationships { get; set; } = new List<PersonRelationship>();
    public ICollection<PersonRelationship> ReverseRelationships { get; set; } = new List<PersonRelationship>();

    [NotMapped]
    public string? FullName =>
        Status == IdentityStatus.Identified && !string.IsNullOrWhiteSpace(LastName)
            ? string.Join(" ",
                new[] { LastName, FirstName, SecondName }
                    .Where(s => !string.IsNullOrWhiteSpace(s)))
            : null;

    [NotMapped]
    public string BirthDateDisplay => BirthDate?.ToString("dd.MM.yyyy") ?? "Сведения отсутствуют";
}
~~~

Check-constraint `CK_Person_Status_FullName`:

~~~sql
(Status = 0 AND LastName IS NULL AND FirstName IS NULL AND SecondName IS NULL)
OR
(Status = 1 AND LastName IS NOT NULL AND FirstName IS NOT NULL)
~~~

Требование: у идентифицированного обязательно `LastName` + `FirstName`; `SecondName` опционален. Аноним не имеет ни одного из трёх.

`Company` — без `Employees` (удалено вместе с `Employment`):

~~~csharp
public class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Mail { get; set; }
    public string? Website { get; set; }
    public string? Notes { get; set; }

    public ICollection<Event> OrganizedEvents { get; set; } = new List<Event>();
    public ICollection<EventParticipant> EventParticipations { get; set; } = new List<EventParticipant>();
    public ICollection<CompanyInteraction> OutgoingInteractions { get; set; } = new List<CompanyInteraction>();
    public ICollection<CompanyInteraction> IncomingInteractions { get; set; } = new List<CompanyInteraction>();
}
~~~

`Event`:

~~~csharp
public class Event
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }

    public int? OrganizerCompanyId { get; set; }
    public Company? OrganizerCompany { get; set; }

    public int? OrganizerPersonId { get; set; }
    public Person? OrganizerPerson { get; set; }

    public ICollection<EventParticipant> Participants { get; set; } = new List<EventParticipant>();

    [NotMapped]
    public string DateDisplay => Date?.ToString("dd.MM.yyyy") ?? "Сведения отсутствуют";

    [NotMapped]
    public int ParticipantCount => Participants?.Count ?? 0;

    [NotMapped]
    public string OrganizerDisplay
    {
        get
        {
            var company = OrganizerCompany?.Name;
            var person = OrganizerPerson?.FullName
                ?? OrganizerPerson?.Aliases.FirstOrDefault(a => a.IsPrimary)?.Value
                ?? (OrganizerPerson != null ? $"Аноним #{OrganizerPerson.Id}" : null);

            if (!string.IsNullOrWhiteSpace(company) && !string.IsNullOrWhiteSpace(person))
                return $"{company} ({person})";
            if (!string.IsNullOrWhiteSpace(company)) return company;
            if (!string.IsNullOrWhiteSpace(person)) return person;
            return "Сведения отсутствуют";
        }
    }
}
~~~

`EventParticipant` (Person↔Event):

~~~csharp
public class EventParticipant
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public int? PersonId { get; set; }
    public Person? Person { get; set; }

    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    public EventParticipantRole Role { get; set; } = EventParticipantRole.Participant;
    public string Notes { get; set; } = string.Empty;
}
~~~

Ограничение: ровно один из `PersonId`/`CompanyId` задан (CHECK-constraint `CK_EventParticipant_OneOwner`). В 1.0.0 UI и сервис развивают **только** ветку Person; ветка Company сохраняется в БД для обратной совместимости.

`PersonRelationship` (Person↔Person):

~~~csharp
public class PersonRelationship
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public int RelatedPersonId { get; set; }
    public PersonRelationKind Kind { get; set; }
    public string Notes { get; set; } = string.Empty;

    public Person Person { get; set; } = null!;
    public Person RelatedPerson { get; set; } = null!;
}
~~~

Семантика строки `(PersonId = A, RelatedPersonId = B, Kind = X)`: **«B является A-у X»**. Пример: `(A, B, Parent)` — B является A-у родителем. При просмотре связей A показываются только строки `PersonId == A`; подпись в UI зависит от `Kind` и `B.Gender`. Обратная запись `(B, A, InverseOf(X))` создаётся автоматически в сервисе.

`MediaAttachment` — без изменений.

### 4.3. Анонимные и идентифицированные личности

Два логических типа, хранятся в одной таблице `Person`, разделяются полем `Status`:

1. **Неидентифицированная** (`Anonymous`): нет `LastName`/`FirstName`/`SecondName`, есть обязательный основной псевдоним.
2. **Идентифицированная** (`Identified`): `LastName` и `FirstName` заполнены.

**Процесс идентификации (Promote):**
1. Вызывается `IPersonService.PromoteAsync(personId, PromotePersonInput)`.
2. Заполняются `LastName`, `FirstName`, `SecondName?`, `Gender`, `BirthDate?`, `Passport?`.
3. `Status` меняется на `Identified`.
4. Текущий основной псевдоним теряет флаг `IsPrimary`.
5. В `PersonAliases` добавляется запись с `LegalName`, `IsPrimary = true`, `Value` = склейка ФИО.
6. Обратный переход не предусмотрен.

### 4.4. Перечисления

`Gender`:

~~~csharp
public enum Gender
{
    Unknown = 0,
    Male = 1,
    Female = 2
}
~~~

`PersonRelationKind` — гендерно-нейтральные имена, гендерные формы — только в `DisplayName`:

~~~csharp
public enum PersonRelationKind
{
    // Родство
    Parent,
    Child,
    Sibling,
    Spouse,
    Grandparent,
    Grandchild,
    UncleAunt,
    NephewNiece,
    Cousin,

    // Социальные
    Friend,
    CloseFriend,
    Acquaintance,
    Neighbor,
    Colleague,
    Classmate,

    // Деловые
    BusinessPartner,
    CoFounder,
    Mentor,
    Mentee,
    Employer,
    EmployeeOf,

    // Конфликтные / следственные
    Opponent,
    CoConspirator,
    Cohabitant,

    Other
}
~~~

`EventParticipantRole`:

~~~csharp
public enum EventParticipantRole
{
    Participant,
    Organizer,
    CoOrganizer,
    Speaker,
    Guest,
    Observer,
    Sponsor,
    Target,
    Witness,
    Victim,
    Suspect,
    Other
}
~~~

### 4.5. Справочники (хелперы)

`PersonRelationKinds`:

~~~csharp
public static class PersonRelationKinds
{
    public static PersonRelationKind InverseOf(PersonRelationKind kind) => kind switch
    {
        PersonRelationKind.Parent       => PersonRelationKind.Child,
        PersonRelationKind.Child        => PersonRelationKind.Parent,
        PersonRelationKind.Grandparent  => PersonRelationKind.Grandchild,
        PersonRelationKind.Grandchild   => PersonRelationKind.Grandparent,
        PersonRelationKind.UncleAunt    => PersonRelationKind.NephewNiece,
        PersonRelationKind.NephewNiece  => PersonRelationKind.UncleAunt,
        PersonRelationKind.Mentor       => PersonRelationKind.Mentee,
        PersonRelationKind.Mentee       => PersonRelationKind.Mentor,
        PersonRelationKind.Employer     => PersonRelationKind.EmployeeOf,
        PersonRelationKind.EmployeeOf   => PersonRelationKind.Employer,
        PersonRelationKind.Other        => PersonRelationKind.Other,
        _ => kind   // симметричные: Sibling, Spouse, Friend, Colleague, BusinessPartner, ...
    };

    public static string DisplayNameOfKind(PersonRelationKind kind) => kind switch
    {
        PersonRelationKind.Parent       => "Родитель",
        PersonRelationKind.Child        => "Ребёнок",
        PersonRelationKind.Sibling      => "Брат/сестра",
        PersonRelationKind.Spouse       => "Супруг(а)",
        PersonRelationKind.Grandparent  => "Дедушка/бабушка",
        PersonRelationKind.Grandchild   => "Внук/внучка",
        PersonRelationKind.UncleAunt    => "Дядя/тётя",
        PersonRelationKind.NephewNiece  => "Племянник(ца)",
        PersonRelationKind.Cousin       => "Двоюродный брат/сестра",
        PersonRelationKind.Friend       => "Друг",
        PersonRelationKind.CloseFriend  => "Близкий друг",
        PersonRelationKind.Acquaintance => "Знакомый",
        PersonRelationKind.Neighbor     => "Сосед",
        PersonRelationKind.Colleague    => "Коллега",
        PersonRelationKind.Classmate    => "Одноклассник",
        PersonRelationKind.BusinessPartner => "Партнёр",
        PersonRelationKind.CoFounder    => "Соучредитель",
        PersonRelationKind.Mentor       => "Наставник",
        PersonRelationKind.Mentee       => "Подопечный",
        PersonRelationKind.Employer     => "Работодатель",
        PersonRelationKind.EmployeeOf   => "Работник",
        PersonRelationKind.Opponent     => "Оппонент",
        PersonRelationKind.CoConspirator => "Соучастник",
        PersonRelationKind.Cohabitant   => "Сожитель",
        PersonRelationKind.Other        => "Другое",
        _ => kind.ToString()
    };

    public static string DisplayNameForRelated(PersonRelationKind kind, Gender relatedGender)
        => (kind, relatedGender) switch
        {
            (PersonRelationKind.Parent,      Gender.Male)   => "Отец",
            (PersonRelationKind.Parent,      Gender.Female) => "Мать",
            (PersonRelationKind.Parent,      _)             => "Родитель",
            (PersonRelationKind.Child,       Gender.Male)   => "Сын",
            (PersonRelationKind.Child,       Gender.Female) => "Дочь",
            (PersonRelationKind.Child,       _)             => "Ребёнок",
            (PersonRelationKind.Sibling,     Gender.Male)   => "Брат",
            (PersonRelationKind.Sibling,     Gender.Female) => "Сестра",
            (PersonRelationKind.Sibling,     _)             => "Брат/сестра",
            (PersonRelationKind.Spouse,      Gender.Male)   => "Муж",
            (PersonRelationKind.Spouse,      Gender.Female) => "Жена",
            (PersonRelationKind.Spouse,      _)             => "Супруг(а)",
            (PersonRelationKind.Grandparent, Gender.Male)   => "Дедушка",
            (PersonRelationKind.Grandparent, Gender.Female) => "Бабушка",
            (PersonRelationKind.Grandparent, _)             => "Дедушка/бабушка",
            (PersonRelationKind.Grandchild,  Gender.Male)   => "Внук",
            (PersonRelationKind.Grandchild,  Gender.Female) => "Внучка",
            (PersonRelationKind.Grandchild,  _)             => "Внук/внучка",
            (PersonRelationKind.UncleAunt,   Gender.Male)   => "Дядя",
            (PersonRelationKind.UncleAunt,   Gender.Female) => "Тётя",
            (PersonRelationKind.UncleAunt,   _)             => "Дядя/тётя",
            (PersonRelationKind.NephewNiece, Gender.Male)   => "Племянник",
            (PersonRelationKind.NephewNiece, Gender.Female) => "Племянница",
            (PersonRelationKind.NephewNiece, _)             => "Племянник(ца)",
            (PersonRelationKind.Cousin,      Gender.Male)   => "Двоюродный брат",
            (PersonRelationKind.Cousin,      Gender.Female) => "Двоюродная сестра",
            (PersonRelationKind.Cousin,      _)             => "Двоюродный брат/сестра",
            (PersonRelationKind.Mentor,      Gender.Female) => "Наставница",
            (PersonRelationKind.Mentee,      Gender.Female) => "Подопечная",
            (PersonRelationKind.EmployeeOf,  Gender.Female) => "Работница",
            (PersonRelationKind.Friend,      Gender.Female) => "Подруга",
            (PersonRelationKind.CloseFriend, Gender.Female) => "Близкая подруга",
            (PersonRelationKind.Neighbor,    Gender.Female) => "Соседка",
            (PersonRelationKind.Classmate,   Gender.Female) => "Одноклассница",
            (PersonRelationKind.Acquaintance,Gender.Female) => "Знакомая",
            (PersonRelationKind.Opponent,    Gender.Female) => "Оппонентка",
            (PersonRelationKind.CoConspirator,Gender.Female)=> "Соучастница",
            (PersonRelationKind.Cohabitant,  Gender.Female) => "Сожительница",
            (PersonRelationKind.CoFounder,   Gender.Female) => "Соучредительница",
            (PersonRelationKind.BusinessPartner, Gender.Female) => "Партнёрша",
            _ => DisplayNameOfKind(kind)
        };
}
~~~

`EventParticipantRoles`:

~~~csharp
public static class EventParticipantRoles
{
    public static string DisplayName(EventParticipantRole role) => role switch
    {
        EventParticipantRole.Participant  => "Участник",
        EventParticipantRole.Organizer    => "Организатор",
        EventParticipantRole.CoOrganizer  => "Соорганизатор",
        EventParticipantRole.Speaker      => "Докладчик",
        EventParticipantRole.Guest        => "Гость",
        EventParticipantRole.Observer     => "Наблюдатель",
        EventParticipantRole.Sponsor      => "Спонсор",
        EventParticipantRole.Target       => "Объект события",
        EventParticipantRole.Witness      => "Свидетель",
        EventParticipantRole.Victim       => "Потерпевший",
        EventParticipantRole.Suspect      => "Подозреваемый",
        EventParticipantRole.Other        => "Другое",
        _ => role.ToString()
    };
}
~~~

### 4.6. Конфигурация EF Core

`PersonConfiguration`:
- `LastName`, `FirstName`, `SecondName` — `HasMaxLength(100)`, коллация `NOCASE`.
- `Gender` — `HasConversion<int>()`.
- CHECK-constraint `CK_Person_Status_FullName` (см. §4.2).
- Индексы: `(LastName, FirstName)` фильтр `Status = 1`; `BirthDate` фильтр `IS NOT NULL`; `(Status, CreatedAt)`; `CountryCode`; `MergedIntoPersonId`.

`PersonRelationshipConfiguration`:
- `HasKey(e => e.Id)`.
- `Property(e => e.Kind).HasConversion<int>()`.
- `HasIndex(e => new { e.PersonId, e.RelatedPersonId, e.Kind }).IsUnique()` — `UQ_PersonRelationship_Natural`.
- Оба FK — `OnDelete(DeleteBehavior.Cascade)`.

`EventParticipantConfiguration`:
- `Property(e => e.Role).HasConversion<int>()`.
- `Notes` — `HasColumnType("TEXT")`.
- CHECK-constraint `CK_EventParticipant_OneOwner` — без изменений.
- Уникальные индексы `(EventId, PersonId)` и `(EventId, CompanyId)` — без изменений.

---

## 5. Функциональные требования

### 5.1. CRUD-операции

- Создание, чтение, обновление и удаление `Person`, `Company`, `Event`.
- Создание, чтение, обновление и удаление вложений `MediaAttachment`.
- Прикрепление одного и того же `MediaAttachment` к нескольким владельцам (M2M).
- Поиск по `Person` через стратегии поиска (`ISearchStrategy`): по ID, по ФИО, по паспорту, по телефону, по алиасам.
  - Поиск «по ФИО» выполняется по трём колонкам (`LastName` / `FirstName` / `SecondName`) через `Contains` по каждой.

### 5.2. Работа с медиафайлами

- При загрузке файл считывается в массив байтов и сохраняется в поле `Data` таблицы `MediaAttachments`.
- **Дедупликация:** перед сохранением вычисляется хэш SHA-256 (`ContentHash`). Если файл с таким хэшем уже существует в БД, создаётся только новая связь `MediaAttachmentLink`, дубликат BLOB-данных не создаётся.
- **Отображение картинок:** считывание `byte[]` из БД, создание `MemoryStream` и передача в `BitmapImage`. Файлы на диск не сохраняются.
- **Воспроизведение видео/аудио:** извлечение `byte[]` из БД во временный файл, воспроизведение через `MediaPlayer`, удаление временного файла после завершения.
- **Защита ОЗУ:** при работе с большими файлами (видео) — потоковое чтение из БД, чтобы не загружать весь BLOB в память.

### 5.3. Каскадное удаление

- `PersonRelationship` — `OnDelete(Cascade)` на обоих FK. Удаление `Person` автоматически чистит её связи на уровне БД. Ручной `RemoveRange` из `PersonService.DeleteAsync` удаляется.
- `EventParticipant` — `OnDelete(Cascade)` на `EventId`, `PersonId`, `CompanyId`. Удаление `Event`/`Person`/`Company` каскадно чистит участия.
- `MediaAttachmentLink` — `OnDelete(Cascade)` от `MediaAttachment`.
- При удалении `Person`/`Company`/`Event`:
  1. Удаляются строки `MediaAttachmentLink`.
  2. Если у `MediaAttachment` не осталось ссылок — запись удаляется из БД (вместе с BLOB-данными).
  3. Логика очистки медиа реализована **внутри** `PersonService.DeleteAsync`, `CompanyService.DeleteAsync`, `EventService.DeleteAsync`. `SearchViewModel` не вызывает `IMediaService.RemoveAllLinksForOwnerAsync` напрямую.

### 5.4. Стратегии сравнения (дубликаты)

Единый контракт:

~~~csharp
public interface IComparisonStrategy
{
    string Key { get; }
    string DisplayName { get; }
    double Compare(Person a, Person b);   // 0.0 … 1.0
}
~~~

Выбор стратегии — через `IComparisonStrategyFactory` в UI (`DuplicateFinderWindow`).

Оркестрация вынесена в сервис:

~~~csharp
public sealed record DuplicateCandidate(Person Person, double Score);

public interface IDuplicateFinderService
{
    Task<IReadOnlyList<DuplicateCandidate>> FindAsync(
        int sourcePersonId, string? strategyKey,
        double minScore = 0.0,
        CancellationToken ct = default);
}
~~~

`DuplicateFinderViewModel` — тонкий, вызывает сервис, заполняет `ObservableCollection<DuplicateCandidate>`, фильтр по `minScore` (по умолчанию 0.6).

**5.4.1. ExactMatchStrategy — точное совпадение**

Логика: только по идентифицирующим полям. Возвращает 0.0 или 1.0. Никаких «степеней похожести».

- `Passport` совпал, оба непустые → 1.0. Иначе, если оба непустые и разные → 0.0.
- `LastName + FirstName + SecondName + BirthDate` совпали, оба поля с каждой стороны непустые → 1.0.
- Анонимы (`FullName = null`) → 0.0. Анонимов точное совпадение не идентифицирует.
- `null == null` **не** считается совпадением.

**5.4.2. LevenshteinStrategy — нечёткое сравнение**

Логика: расстояние редактирования по имени с множителем по дате рождения.

- Имя для идентифицированного — `FullName`; для анонима — `PrimaryAlias`.
- Нормализация: `Trim()`, `ToLowerInvariant()`, сворачивание множественных пробелов.
- `nameScore = 1 - DamerauLevenshtein(a, b) / max(len(a), len(b))`.
- Множитель по `BirthDate`: обе пусто → 0.6; одна пусто → 0.7; разница 0 дней → 1.0; ≤ 365 дней → 0.9; иначе → 0.3.
- `score = nameScore * factor`.
- Порог UI: `score >= 0.6`. Для анонимов с коротким псевдонимом (< 5 символов) порог 0.75.

**5.4.3. MetadataStrategy — сравнение по метаданным**

Логика: взвешенная доля совпавших полей. Поле участвует только если у обеих сторон непустое.

Веса:

| Поле | Вес |
|---|---|
| `Passport` | 5.0 |
| `Phone` | 3.0 |
| `Mail` | 3.0 |
| `Address` | 1.5 |
| `SocialMedia` | 1.5 |
| `Car` | 1.0 |
| `City` | 0.5 |
| `Job` | 0.5 |
| `CountryCode` | 0.3 |

Формула: `score = Σ(weight × is_match) / Σ(weight)`, только по сравнимым полям. Без штрафов за малое число совпадений — если `Phone` и `Mail` совпали, а больше сравнивать нечего, `score = 1.0`.

Сопоставление значений:
- `Phone` — только цифры.
- `Mail` — `Trim().ToLowerInvariant()`.
- `Passport` — `Trim()`, регистронезависимо.
- `Address` — `Trim()`, регистронезависимо.
- Остальные — `Trim()`, регистронезависимо.

### 5.5. Фоновые службы

`StorageMaintenanceService` (`BackgroundService`):
- При старте проверяет целостность БД и удаляет осиротевшие `MediaAttachmentLink`.
- Раз в 10 минут очищает папку `%Temp%\FIBER\`.
- Работает напрямую с зашифрованной БД.

### 5.6. Связи

**5.6.1. Person ↔ Person**

Модель: `PersonRelationship` (см. §4.2). Виды — `PersonRelationKind` (§4.4).

`IRelationService`:

~~~csharp
Task<IReadOnlyList<PersonRelationship>> ListRelationshipsAsync(int personId);
Task AddPersonRelationshipAsync(int personId, int relatedPersonId,
    PersonRelationKind kind, string notes);
Task RemovePersonRelationshipAsync(int relationshipId);
~~~

Правила `AddPersonRelationshipAsync`:
1. `personId != relatedPersonId`, иначе `InvalidOperationException`.
2. Проверка существования обоих `Person` в БД.
3. Проверка дубликата `(personId, relatedPersonId, kind)` — если есть, выйти идемпотентно.
4. В одной транзакции: добавить прямую строку `(A, B, kind)`, затем — если `InverseOf(kind) != kind` и `InverseOf(kind) != Other` — добавить обратную `(B, A, InverseOf(kind))`.
5. Commit.

`ListRelationshipsAsync(personId)` — `WHERE PersonId == personId`. Обратные записи уже существуют явно, дублей не будет.

Подпись строки в UI — `PersonRelationKinds.DisplayNameForRelated(r.Kind, r.RelatedPerson.Gender)`.

`RemovePersonRelationshipAsync(relationshipId)` — удаляет одну строку (прямую). Обратную пользователь удаляет отдельным подтверждением.

**5.6.2. Person ↔ Event**

Модель: `EventParticipant` (см. §4.2). Виды — `EventParticipantRole` (§4.4).

`IRelationService`:

~~~csharp
Task<IReadOnlyList<EventParticipant>> ListEventParticipantsAsync(int personId);
Task<IReadOnlyList<EventParticipant>> ListParticipantsByEventAsync(int eventId);
Task AddEventParticipantAsync(int eventId, int? personId, int? companyId,
    EventParticipantRole role, string notes);
Task RemoveEventParticipantAsync(int participantId);
~~~

Правила `AddEventParticipantAsync`:
1. Ровно один из `personId`/`companyId` задан — иначе `InvalidOperationException`.
2. Проверка существования `Event` и (Person или Company).
3. Проверка дубликата `(eventId, personId|companyId, role)` — если есть, выйти идемпотентно.
4. Insert.

В 1.0.0 UI и сервисы реализуют только ветку Person (Person↔Event). Ветка Company сохраняется в модели для обратной совместимости.

`EventInput.Participants` расширяется:

~~~csharp
public record EventParticipantInput(
    int? PersonId,
    int? CompanyId,
    EventParticipantRole Role,
    string Notes = "");
~~~

`EventService.ValidateParticipantsAsync` — проверка уникальности по `(PersonId|CompanyId, Role)`.

### 5.7. Дубликаты — итоговый UX

- Кнопка «Дубликаты» доступна на вкладке «Личности» в `SearchView`.
- Открывается `DuplicateFinderWindow`: ComboBox стратегии, кнопка «Искать», список кандидатов (`ScoreText`, `DisplayName`, `BirthDate`, `Gender`, `Status`).
- Порог отсечения: `0.6`.
- «Точное совпадение» — быстрая проверка по паспорту/ФИО+дате.
- «Нечёткое сравнение» — для случаев с опечатками и для анонимов.
- «Сравнение по метаданным» — для случаев, где ФИО неизвестно или искажено, но есть контакты.

---

## 6. Нефункциональные требования

- **Асинхронность I/O:** все операции чтения/записи БД и временных файлов — через `async`-API. Отсутствуют блокирующие вызовы (`.Result` / `.Wait()`).
- **Потокобезопасность:** общее состояние между UI и фоновыми службами защищено.
- **Обработка ошибок:** ошибки I/O логируются через `ILogger`, приложение не падает. Пользователю показывается уведомление.
- **Отзывчивость UI:** длительные операции (импорт медиа, извлечение видео, поиск дубликатов) выполняются в фоновом режиме без блокировки интерфейса.
- **Чистая архитектура:** валидация и бизнес-правила — в сервисах (`PersonService`, `RelationService`, `CompanyService`, `EventService`), не в ViewModels и не в Views. ViewModels — тонкие, вызывают сервисы, обрабатывают UI-состояние. Views — только разметка.
- **SOLID:** стратегии поиска и сравнения — через фабрики, добавление нового вида не требует правки существующих классов. DRY — логика удаления сущности и её связей/медиа живёт только в соответствующем сервисе.
- **KISS:** без обобщённых `<T>` для стратегий сравнения в фазе 1.0.0; без универсального сериализатора; без лишних абстракций.

---

## 7. Список библиотек

| Назначение       | Пакет NuGet                                          | Версия    | Примечание                              |
| :--------------- | :--------------------------------------------------- | :-------- | :-------------------------------------- |
| EF Core + SQLite | `Microsoft.EntityFrameworkCore.Sqlite.Core`          | `10.0.13` | Провайдер EF Core.                      |
| Нативный SQLite  | `SQLitePCLRaw.bundle_e_sqlcipher`                    | `2.1.11`  | SQLite с поддержкой AES-256 шифрования. |
| MVVM             | `CommunityToolkit.Mvvm`                              | `8.4.2`   | Без изменений.                          |
| Хостинг / DI     | `Microsoft.Extensions.Hosting`                       | `10.0.13` | Без изменений.                          |
| Логирование      | `Microsoft.Extensions.Logging`, `.Debug`, `.Console` | `10.0.13` | Без изменений.                          |
| EF Core Design   | `Microsoft.EntityFrameworkCore.Design`               | `10.0.13` | Для миграций (`PrivateAssets="all"`).   |

---

