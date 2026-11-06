using System.Collections.Generic;
using System.Threading.Tasks;
using FIBER.Source.Models.Enums;
using FIBER.Source.Models.Joins;

namespace FIBER.Source.Interfaces;

public interface IRelationService
{
    Task<IReadOnlyList<EventParticipant>> ListEventParticipantsAsync(int personId);
    Task<IReadOnlyList<EventParticipant>> ListParticipantsByEventAsync(int eventId);
    Task<IReadOnlyList<EventParticipant>> ListParticipantsByCompanyAsync(int companyId);
    Task AddEventParticipantAsync(int eventId, int? personId, int? companyId,
        EventParticipantRole role, string notes);
    Task RemoveEventParticipantAsync(int participantId);

    Task<IReadOnlyList<PersonRelationship>> ListRelationshipsAsync(int personId);
    Task<IReadOnlyList<PersonRelationship>> ListReverseRelationshipsAsync(int personId);
    Task AddPersonRelationshipAsync(int personId, int relatedPersonId,
        PersonRelationKind kind, string notes);
    Task RemovePersonRelationshipAsync(int relationshipId);

    Task<IReadOnlyList<CompanyInteraction>> ListCompanyInteractionsAsync(int companyId);
    Task AddCompanyInteractionAsync(int sourceCompanyId, int targetCompanyId,
        string interactionType, string notes);
    Task RemoveCompanyInteractionAsync(int sourceCompanyId, int targetCompanyId);

    // ----- Связи «организация ↔ личность» -----
    Task<IReadOnlyList<CompanyPerson>> ListCompanyPeopleAsync(int companyId);
    Task<IReadOnlyList<CompanyPerson>> ListPersonCompaniesAsync(int personId);
    Task AddCompanyPersonAsync(int companyId, int personId, string role, string notes);
    Task RemoveCompanyPersonAsync(int companyPersonId);

    // ----- Связи «событие ↔ событие» -----
    Task<IReadOnlyList<EventEvent>> ListEventEventsAsync(int eventId);
    Task<IReadOnlyList<EventEvent>> ListReverseEventEventsAsync(int eventId);
    Task AddEventEventAsync(int sourceEventId, int targetEventId, string relationshipType, string notes);
    Task RemoveEventEventAsync(int eventEventId);
}