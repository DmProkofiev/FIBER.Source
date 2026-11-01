using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Person
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("FullName")]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(50)]
    [Column("BirthDate", TypeName = "TEXT")]
    public string BirthDate { get; set; } = string.Empty;

    [MaxLength(100)] public string Job { get; set; } = string.Empty;
    [MaxLength(20)] public string Phone { get; set; } = string.Empty;
    [MaxLength(50)] public string Passport { get; set; } = string.Empty;
    [MaxLength(100)] public string City { get; set; } = string.Empty;
    [MaxLength(100)] public string Country { get; set; } = string.Empty;
    [MaxLength(100)] public string Car { get; set; } = string.Empty;
    [MaxLength(100)][EmailAddress] public string Mail { get; set; } = string.Empty;

    [Column(TypeName = "TEXT")] public string SocialMedia { get; set; } = string.Empty;
    [Column(TypeName = "TEXT")] public string MediaInformation { get; set; } = string.Empty;

    [MaxLength(500)][Column("Address")] public string Address { get; set; } = string.Empty;
    [Column(TypeName = "TEXT")] public string Notes { get; set; } = string.Empty;

    public Person() { }

    public Person(int id, string fullname, string birthdate, string job, string phone,
                  string passport, string city, string country, string car, string mail,
                  string socialmedia, string notes, string mediainformation, string address)
    {
        Id = id; FullName = fullname; BirthDate = birthdate; Job = job; Phone = phone;
        Passport = passport; City = city; Country = country; Car = car; Mail = mail;
        SocialMedia = socialmedia; Notes = notes; MediaInformation = mediainformation;
        Address = address;
    }
}