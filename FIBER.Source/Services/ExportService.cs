using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Helpers;
using FIBER.Source.Models.Joins;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services;

public sealed class ExportService : IExportService
{
    private const string DataFileNameWord = "data.docx";
    private const string DataFileNameTxt = "data.txt";
    private const string MissingText = "Сведения отсутствуют";

    private readonly IDbContextFactory<FiberSourceDbContext> _factory;

    public ExportService(IDbContextFactory<FiberSourceDbContext> factory) => _factory = factory;

    private static string OrMissing(string? value) =>
        string.IsNullOrWhiteSpace(value) ? MissingText : value;

    private static string BirthDateText(Person person) =>
        person.BirthDate?.ToString("dd.MM.yyyy") ?? MissingText;

    private static string EventDateText(Event ev) =>
        ev.Date?.ToString("dd.MM.yyyy") ?? MissingText;

    private static string GenderText(Gender gender) => gender switch
    {
        Gender.Male => "Мужской",
        Gender.Female => "Женский",
        _ => MissingText
    };

    private static string ParticipantDisplay(EventParticipant p)
    {
        if (p.Company != null) return p.Company.Name;
        if (p.Person != null)
        {
            return p.Person.FullName
                ?? p.Person.Aliases.FirstOrDefault(a => a.IsPrimary)?.Value
                ?? $"Аноним #{p.Person.Id}";
        }
        return "—";
    }

    private static string ParticipantDisplayWithRole(EventParticipant p)
    {
        var name = ParticipantDisplay(p);
        var role = EventParticipantRoles.DisplayName(p.Role);
        return $"{name} — {role}";
    }

    public async Task<string> ExportPersonAsync(
        int personId, string parentFolder, ExportFormat format,
        IReadOnlyList<int>? selectedMediaIds = null)
    {
        await using var context = await _factory.CreateDbContextAsync();
        var person = await context.People.Include(p => p.Country).Include(p => p.Aliases)
            .AsNoTracking().FirstOrDefaultAsync(p => p.Id == personId)
            ?? throw new InvalidOperationException($"Личность Id={personId} не найдена");

        var participations = await context.EventParticipants.Include(p => p.Event)
            .Where(p => p.PersonId == personId).AsNoTracking().ToListAsync();
        var organizedEvents = await context.Events
            .Where(e => e.OrganizerPersonId == personId).AsNoTracking().ToListAsync();
        var relationships = await context.PersonRelationships
            .Include(r => r.RelatedPerson)
            .Where(r => r.PersonId == personId).AsNoTracking().ToListAsync();

        var targetFolder = CreateUniqueFolder(parentFolder, $"Person_{person.Id}");
        await ExtractMediaAsync(context, MediaOwnerType.Person, personId, targetFolder, selectedMediaIds);

        var dataPath = Path.Combine(targetFolder, format == ExportFormat.Word ? DataFileNameWord : DataFileNameTxt);
        if (format == ExportFormat.Word)
            BuildPersonWord(dataPath, person, participations, organizedEvents, relationships);
        else
            await BuildPersonTxtAsync(dataPath, person, participations, organizedEvents, relationships);

        return targetFolder;
    }

    public async Task<string> ExportCompanyAsync(
        int companyId, string parentFolder, ExportFormat format,
        IReadOnlyList<int>? selectedMediaIds = null)
    {
        await using var context = await _factory.CreateDbContextAsync();
        var company = await context.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId)
            ?? throw new InvalidOperationException($"Организация Id={companyId} не найдена");

        var organizedEvents = await context.Events
            .Where(e => e.OrganizerCompanyId == companyId).AsNoTracking().ToListAsync();
        var participations = await context.EventParticipants.Include(p => p.Event)
            .Where(p => p.CompanyId == companyId).AsNoTracking().ToListAsync();
        var interactions = await context.CompanyInteractions.Include(ci => ci.TargetCompany)
            .Where(ci => ci.SourceCompanyId == companyId).AsNoTracking().ToListAsync();

        var targetFolder = CreateUniqueFolder(parentFolder, $"Company_{company.Id}");
        await ExtractMediaAsync(context, MediaOwnerType.Company, companyId, targetFolder, selectedMediaIds);

        var dataPath = Path.Combine(targetFolder, format == ExportFormat.Word ? DataFileNameWord : DataFileNameTxt);
        if (format == ExportFormat.Word)
            BuildCompanyWord(dataPath, company, organizedEvents, participations, interactions);
        else
            await BuildCompanyTxtAsync(dataPath, company, organizedEvents, participations, interactions);

        return targetFolder;
    }

    public async Task<string> ExportEventAsync(
        int eventId, string parentFolder, ExportFormat format,
        IReadOnlyList<int>? selectedMediaIds = null)
    {
        await using var context = await _factory.CreateDbContextAsync();
        var ev = await context.Events
            .Include(e => e.OrganizerCompany)
            .Include(e => e.OrganizerPerson!).ThenInclude(p => p.Aliases)
            .Include(e => e.Participants).ThenInclude(p => p.Person!).ThenInclude(p => p.Aliases)
            .Include(e => e.Participants).ThenInclude(p => p.Company)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new InvalidOperationException($"Событие Id={eventId} не найдено");

        var targetFolder = CreateUniqueFolder(parentFolder, $"Event_{ev.Id}");
        await ExtractMediaAsync(context, MediaOwnerType.Event, eventId, targetFolder, selectedMediaIds);

        var dataPath = Path.Combine(targetFolder, format == ExportFormat.Word ? DataFileNameWord : DataFileNameTxt);
        if (format == ExportFormat.Word)
            BuildEventWord(dataPath, ev);
        else
            await BuildEventTxtAsync(dataPath, ev);

        return targetFolder;
    }

    private static string CreateUniqueFolder(string parentFolder, string baseName)
    {
        if (string.IsNullOrWhiteSpace(parentFolder))
            throw new ArgumentException("Не указана родительская папка", nameof(parentFolder));

        Directory.CreateDirectory(parentFolder);
        var candidate = Path.Combine(parentFolder, baseName);
        int n = 2;
        while (Directory.Exists(candidate))
        {
            candidate = Path.Combine(parentFolder, $"{baseName} ({n})");
            n++;
        }
        Directory.CreateDirectory(candidate);
        return candidate;
    }

    private static async Task ExtractMediaAsync(
        FiberSourceDbContext context, MediaOwnerType ownerType, int ownerId,
        string targetFolder, IReadOnlyList<int>? selectedMediaIds)
    {
        var query = context.MediaAttachmentLinks
            .Where(l => l.OwnerType == ownerType && l.OwnerId == ownerId);

        if (selectedMediaIds is not null)
        {
            var ids = selectedMediaIds.ToArray();
            query = query.Where(l => ids.Contains(l.MediaAttachmentId));
        }

        var metas = await query
            .Select(l => new { l.MediaAttachment.Id, l.MediaAttachment.OriginalFileName })
            .ToListAsync();

        int idx = 0;
        foreach (var meta in metas)
        {
            idx++;
            var data = await context.MediaAttachments
                .Where(m => m.Id == meta.Id)
                .Select(m => m.Data)
                .FirstOrDefaultAsync();
            if (data is null || data.Length == 0) continue;

            var ext = Path.GetExtension(meta.OriginalFileName);
            if (string.IsNullOrEmpty(ext)) ext = ".bin";
            var safeBase = SanitizeFileName(Path.GetFileNameWithoutExtension(meta.OriginalFileName));
            if (string.IsNullOrWhiteSpace(safeBase)) safeBase = "media";
            var diskName = $"{idx:000}_{safeBase}{ext}";
            await File.WriteAllBytesAsync(Path.Combine(targetFolder, diskName), data);
        }
    }

    private static void BuildPersonWord(string path, Person person,
        List<EventParticipant> participations,
        List<Event> organizedEvents, List<PersonRelationship> relationships)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        AddHeading(body, "Личность: " + (person.FullName ?? $"Аноним #{person.Id}"), 1);
        AddField(body, "ID", person.Id.ToString());
        AddField(body, "Статус", person.Status == IdentityStatus.Identified ? "Идентифицирован" : "Не идентифицирован");
        AddField(body, "Фамилия", person.LastName);
        AddField(body, "Имя", person.FirstName);
        AddField(body, "Отчество", person.SecondName);
        AddField(body, "Пол", GenderText(person.Gender));
        AddField(body, "Дата рождения", BirthDateText(person));
        AddField(body, "Паспорт", person.Passport);
        AddField(body, "Профессия", person.Job);
        AddField(body, "Телефон", person.Phone);
        AddField(body, "Город", person.City);
        AddField(body, "Страна", person.Country?.NameRu ?? "—");
        AddField(body, "Транспорт", person.Car);
        AddField(body, "Email", person.Mail);
        AddField(body, "Соцсети", person.SocialMedia);
        AddField(body, "Адрес", person.Address);
        AddField(body, "Заметки", person.Notes);

        if (person.Aliases.Count > 0)
        {
            AddHeading(body, "Псевдонимы", 2);
            foreach (var a in person.Aliases)
                AddBullet(body, $"{a.Value} ({(a.IsPrimary ? "основной" : "дополнительный")})");
        }
        if (organizedEvents.Count > 0)
        {
            AddHeading(body, "Организованные события", 2);
            foreach (var ev in organizedEvents)
                AddBullet(body, $"{ev.Title} ({EventDateText(ev)})");
        }
        if (participations.Count > 0)
        {
            AddHeading(body, "Участие в событиях", 2);
            foreach (var p in participations)
                AddBullet(body, $"{p.Event?.Title ?? "—"} ({EventDateText(p.Event!)}) — {EventParticipantRoles.DisplayName(p.Role)}");
        }
        if (relationships.Count > 0)
        {
            AddHeading(body, "Связи с людьми", 2);
            foreach (var r in relationships)
                AddBullet(body, $"{r.RelatedPerson?.FullName ?? "?"}: {r.KindDisplay}");
        }

        mainPart.Document.Save();
    }

    private static async Task BuildPersonTxtAsync(string path, Person person,
        List<EventParticipant> participations,
        List<Event> organizedEvents, List<PersonRelationship> relationships)
    {
        await using var w = new StreamWriter(path, false, Encoding.UTF8);
        await w.WriteLineAsync($"=== Личность: {person.FullName ?? $"Аноним #{person.Id}"} ===");
        await w.WriteLineAsync();
        await w.WriteLineAsync($"ID: {person.Id}");
        await w.WriteLineAsync($"Статус: {(person.Status == IdentityStatus.Identified ? "Идентифицирован" : "Не идентифицирован")}");
        await w.WriteLineAsync($"Фамилия: {OrMissing(person.LastName)}");
        await w.WriteLineAsync($"Имя: {OrMissing(person.FirstName)}");
        await w.WriteLineAsync($"Отчество: {OrMissing(person.SecondName)}");
        await w.WriteLineAsync($"Пол: {GenderText(person.Gender)}");
        await w.WriteLineAsync($"Дата рождения: {BirthDateText(person)}");
        await w.WriteLineAsync($"Паспорт: {OrMissing(person.Passport)}");
        await w.WriteLineAsync($"Профессия: {OrMissing(person.Job)}");
        await w.WriteLineAsync($"Телефон: {OrMissing(person.Phone)}");
        await w.WriteLineAsync($"Город: {OrMissing(person.City)}");
        await w.WriteLineAsync($"Страна: {person.Country?.NameRu ?? "—"}");
        await w.WriteLineAsync($"Транспорт: {OrMissing(person.Car)}");
        await w.WriteLineAsync($"Email: {OrMissing(person.Mail)}");
        await w.WriteLineAsync($"Соцсети: {OrMissing(person.SocialMedia)}");
        await w.WriteLineAsync($"Адрес: {OrMissing(person.Address)}");
        await w.WriteLineAsync($"Заметки: {OrMissing(person.Notes)}");

        if (person.Aliases.Count > 0)
        {
            await w.WriteLineAsync();
            await w.WriteLineAsync("--- Псевдонимы ---");
            foreach (var a in person.Aliases)
                await w.WriteLineAsync($"  - {a.Value} ({(a.IsPrimary ? "основной" : "дополнительный")})");
        }
        if (organizedEvents.Count > 0)
        {
            await w.WriteLineAsync();
            await w.WriteLineAsync("--- Организованные события ---");
            foreach (var ev in organizedEvents)
                await w.WriteLineAsync($"  - {ev.Title} ({EventDateText(ev)})");
        }
        if (participations.Count > 0)
        {
            await w.WriteLineAsync();
            await w.WriteLineAsync("--- Участие в событиях ---");
            foreach (var p in participations)
                await w.WriteLineAsync($"  - {p.Event?.Title ?? "—"} ({EventDateText(p.Event!)}) — {EventParticipantRoles.DisplayName(p.Role)}");
        }
        if (relationships.Count > 0)
        {
            await w.WriteLineAsync();
            await w.WriteLineAsync("--- Связи с людьми ---");
            foreach (var r in relationships)
                await w.WriteLineAsync($"  - {r.RelatedPerson?.FullName ?? "?"}: {r.KindDisplay}");
        }
    }

    private static void BuildCompanyWord(string path, Company company,
        List<Event> organizedEvents,
        List<EventParticipant> participations, List<CompanyInteraction> interactions)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        AddHeading(body, "Организация: " + company.Name, 1);
        AddField(body, "ID", company.Id.ToString());
        AddField(body, "Регистрационный номер", company.RegistrationNumber);
        AddField(body, "Адрес", company.Address);
        AddField(body, "Телефон", company.Phone);
        AddField(body, "Email", company.Mail);
        AddField(body, "Сайт", company.Website);
        AddField(body, "Заметки", company.Notes);

        if (organizedEvents.Count > 0)
        {
            AddHeading(body, "Организованные события", 2);
            foreach (var ev in organizedEvents)
                AddBullet(body, $"{ev.Title} ({EventDateText(ev)})");
        }
        if (participations.Count > 0)
        {
            AddHeading(body, "Участие в событиях", 2);
            foreach (var p in participations)
                AddBullet(body, $"{p.Event?.Title ?? "—"} ({EventDateText(p.Event!)})");
        }
        if (interactions.Count > 0)
        {
            AddHeading(body, "Взаимодействия", 2);
            foreach (var ci in interactions)
                AddBullet(body, $"{ci.TargetCompany?.Name ?? "—"}: {ci.InteractionType}");
        }

        mainPart.Document.Save();
    }

    private static async Task BuildCompanyTxtAsync(string path, Company company,
        List<Event> organizedEvents,
        List<EventParticipant> participations, List<CompanyInteraction> interactions)
    {
        await using var w = new StreamWriter(path, false, Encoding.UTF8);
        await w.WriteLineAsync($"=== Организация: {company.Name} ===");
        await w.WriteLineAsync();
        await w.WriteLineAsync($"ID: {company.Id}");
        await w.WriteLineAsync($"Регистрационный номер: {OrMissing(company.RegistrationNumber)}");
        await w.WriteLineAsync($"Адрес: {OrMissing(company.Address)}");
        await w.WriteLineAsync($"Телефон: {OrMissing(company.Phone)}");
        await w.WriteLineAsync($"Email: {OrMissing(company.Mail)}");
        await w.WriteLineAsync($"Сайт: {OrMissing(company.Website)}");
        await w.WriteLineAsync($"Заметки: {OrMissing(company.Notes)}");
        if (organizedEvents.Count > 0)
        {
            await w.WriteLineAsync();
            await w.WriteLineAsync("--- Организованные события ---");
            foreach (var ev in organizedEvents)
                await w.WriteLineAsync($"  - {ev.Title} ({EventDateText(ev)})");
        }
        if (participations.Count > 0)
        {
            await w.WriteLineAsync();
            await w.WriteLineAsync("--- Участие в событиях ---");
            foreach (var p in participations)
                await w.WriteLineAsync($"  - {p.Event?.Title ?? "—"} ({EventDateText(p.Event!)})");
        }
        if (interactions.Count > 0)
        {
            await w.WriteLineAsync();
            await w.WriteLineAsync("--- Взаимодействия ---");
            foreach (var ci in interactions)
                await w.WriteLineAsync($"  - {ci.TargetCompany?.Name ?? "—"}: {ci.InteractionType}");
        }
    }

    private static void BuildEventWord(string path, Event ev)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        AddHeading(body, "Событие: " + ev.Title, 1);
        AddField(body, "ID", ev.Id.ToString());
        AddField(body, "Дата", EventDateText(ev));
        AddField(body, "Организатор", ev.OrganizerDisplay);
        AddField(body, "Описание", ev.Description);
        AddField(body, "Заметки", ev.Notes);

        if (ev.Participants.Count > 0)
        {
            AddHeading(body, "Участники", 2);
            foreach (var p in ev.Participants)
                AddBullet(body, ParticipantDisplayWithRole(p));
        }

        mainPart.Document.Save();
    }

    private static async Task BuildEventTxtAsync(string path, Event ev)
    {
        await using var w = new StreamWriter(path, false, Encoding.UTF8);
        await w.WriteLineAsync($"=== Событие: {ev.Title} ===");
        await w.WriteLineAsync();
        await w.WriteLineAsync($"ID: {ev.Id}");
        await w.WriteLineAsync($"Дата: {EventDateText(ev)}");
        await w.WriteLineAsync($"Организатор: {ev.OrganizerDisplay}");
        await w.WriteLineAsync($"Описание: {OrMissing(ev.Description)}");
        await w.WriteLineAsync($"Заметки: {OrMissing(ev.Notes)}");
        if (ev.Participants.Count > 0)
        {
            await w.WriteLineAsync();
            await w.WriteLineAsync("--- Участники ---");
            foreach (var p in ev.Participants)
                await w.WriteLineAsync($"  - {ParticipantDisplayWithRole(p)}");
        }
    }

    private static void AddHeading(Body body, string text, int level)
    {
        var para = new Paragraph(new ParagraphProperties(new SpacingBetweenLines { Before = "200", After = "100" }));
        var run = new Run();
        var props = new RunProperties(new Bold());
        props.AppendChild(new FontSize { Val = level == 1 ? "36" : "28" });
        run.AppendChild(props);
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        para.AppendChild(run);
        body.AppendChild(para);
    }

    private static void AddField(Body body, string label, string? value)
    {
        var para = new Paragraph();
        para.AppendChild(new Run(new RunProperties(new Bold()), new Text(label + ": ")));
        para.AppendChild(new Run(new Text(OrMissing(value)) { Space = SpaceProcessingModeValues.Preserve }));
        body.AppendChild(para);
    }

    private static void AddBullet(Body body, string text)
    {
        var para = new Paragraph(new ParagraphProperties(new Indentation { Left = "720" }));
        para.AppendChild(new Run(new Text("- " + text) { Space = SpaceProcessingModeValues.Preserve }));
        body.AppendChild(para);
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name) sb.Append(invalid.Contains(c) ? '_' : c);
        var result = sb.ToString().Trim();
        return result.Length > 80 ? result[..80] : result;
    }
}