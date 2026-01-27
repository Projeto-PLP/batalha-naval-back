namespace BatalhaNaval.Domain.Enums;

public enum CellState
{
    Water,      // Água (não atingida)
    Ship,       // Navio (não atingido)
    Hit,        // Navio atingido
    Missed      // Água atingida (tiro no vazio)
}