using System.ComponentModel.DataAnnotations.Schema;

namespace BatalhaNaval.Domain.Entities;

[Table("medals")]
public class Medal
{
    protected Medal()
    {
    }

    public Medal(string name, string description, string code)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Nome da medalha é obrigatório");
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Código da medalha é obrigatório");

        Name = name;
        Description = description;
        Code = code;
    }

    [Column("id")]
    public int Id { get; set; }
    
    [Column("name")]
    public string Name { get; set; }
    
    [Column("description")]
    public string Description { get; set; }

    [Column("code")]
    public string Code { get; set; }
}