using FIBER.Source.Models.Enums;

namespace FIBER.Source.Contracts.Inputs;

// Входные данные одного участника при редактировании события.
// Id == null — новый участник; Id != null — существующая запись (роль/заметки могут быть изменены).
public record EventParticipantEditInput(
    int? Id,
    int? PersonId,
    int? CompanyId,
    EventParticipantRole Role,
    string Notes = "");
