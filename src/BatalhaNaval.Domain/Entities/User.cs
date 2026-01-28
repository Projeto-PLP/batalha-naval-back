using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BatalhaNaval.Domain.Entities;

[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    [Description("Identificador único do usuário")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("username")]
    [Description("Nome de usuário")]
    public string Username { get; set; } = string.Empty;

    [Column("password_hash")]
    [Description("Hash da senha do usuário")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("created_at")]
    [Description("Data de criação do usuário")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual PlayerProfile? Profile { get; set; }
}