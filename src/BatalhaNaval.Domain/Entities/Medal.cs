namespace BatalhaNaval.Domain.Entities;

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

    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    public string Code { get; set; }
}