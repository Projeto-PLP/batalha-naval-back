using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BatalhaNaval.Application.DTOs;
using BatalhaNaval.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace BatalhaNaval.IntegrationTests;

[Collection("Sequential")]
public class MatchTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private const string EndpointUsers = "/users";
    private const string EndpointLogin = "/auth/login";
    private const string Endpoint = "/match";
    private const string EndpointSetup = $"{Endpoint}/setup";

    private const string EndpointShot = $"{Endpoint}/shot";

    // private const string EndpointRanking = "/users/player_stats"; // TODO vai servir pra validar Wins e Losses
    private readonly HttpClient _client;
    private TokenResponseDto authInfoUsuarioDaPartida;
    private TokenResponseDto authInfoUsuarioExternoDaPartida;
    private RealMatch matchResult;

    public MatchTests(IntegrationTestWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Deve_Executar_Jornada_Completa_De_Jogo()
    {
        var usuarioDaPartida = new { Username = "SargentoTeste", Password = "Senha1234!" };
        var usuarioExternoDaPartida = new { Username = "PirataTeste", Password = "SenhaPirata123!" };
        // criando usuarios
        await _client.PostAsJsonAsync(EndpointUsers, usuarioDaPartida);
        await _client.PostAsJsonAsync(EndpointUsers, usuarioExternoDaPartida);
        // efetuando login
        var responseLoginUsuarioDaPartida = await _client.PostAsJsonAsync(EndpointLogin, usuarioDaPartida);
        var responseLoginUsuarioExternoDaPartida =
            await _client.PostAsJsonAsync(EndpointLogin, usuarioExternoDaPartida);
        // token na mão meu chapa
        authInfoUsuarioDaPartida = await responseLoginUsuarioDaPartida.Content.ReadFromJsonAsync<TokenResponseDto>();
        authInfoUsuarioExternoDaPartida =
            await responseLoginUsuarioExternoDaPartida.Content.ReadFromJsonAsync<TokenResponseDto>();

        // STEP 0: Verificar rotas protegidas
        await Passo_VerificarRotasProtegidas();

        // STEP 1: Cadastrar partida com IA sem payload
        await Passo_VerificarCadastroDePartidaSemPayload();

        // STEP 2: Cadastrar partida com IA com payload errado
        await Passo_VerificarCadastroDePartidaComPayloadErrado();

        // STEP 3: Cadastrar uma partida contra a IA (Classic) válida
        matchResult = await Passo_VerificarCadastroDePartida();

        // STEP 4: Cadastrar uma partida contra a IA (Classic) com partida em andamento
        await Passo_VerificarCadastroDePartidaComPartidaEmAndamento();

        // STEP 5: Cancelar Partida com usuario externo
        await Passo_VerificarCancelamentoDePartidaEmAndamentoComUsuarioExterno();

        // STEP 6: Cancelar Partida inexistente 
        await Passo_VerificarCancelamentoDePartidaInexistente();

        // STEP 7: Cancelar Partida 
        await Passo_VerificarCancelamentoDePartida();

        // Criar partida para avaliacao
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authInfoUsuarioDaPartida?.AccessToken);
        var response = await _client.PostAsJsonAsync(Endpoint, new
        {
            Mode = "Classic",
            AiDifficulty = "Basic"
        });
        matchResult = await response.Content.ReadFromJsonAsync<RealMatch>();
        // Criar partida para avaliacao

        // STEP 8: Setup com payload vazio
        await Passo_VerificarSetupDePartidaSemPayload();

        // STEP 9: Setup com payload errado
        await Passo_VerificarSetupDePartidaComPayloadErrado();

        // STEP 10: Setup com payload correto
        await Passo_VerificarSetupDePartida();

        // STEP 11: Atirar sem payload
        await Passo_VerificarShotSemPayload();

        // STEP 12: Atirar com payload errado
        await Passo_VerificarShotComPayloadErrado();

        // STEP 13: Tiro duplicado no mesmo local
        await Passo_VerificarShotDuplicado();

        // TODO o que vier por ai de partida
    }

    public record RealMatch(Guid MatchId);

    #region Steps

    private async Task Passo_VerificarRotasProtegidas()
    {
        var responseCreateAIMatch =
            await _client.PostAsJsonAsync(Endpoint, new { Mode = "Classic", AiDifficulty = "Basic" });
        responseCreateAIMatch.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Usuário não autenticado deve receber 401 Unauthorized ao acessar Create Match");

        var matchIdFake = "895993f0-3bfc-4730-9f17-1eb3457a2222";

        var responseSetupMatch = await _client.PostAsJsonAsync(EndpointSetup,
            new
            {
                matchId = matchIdFake,
                ships = new[]
                {
                    new { name = "Porta-Aviões", size = 5, startX = 0, startY = 0, orientation = "Vertical" },
                    new { name = "Encouraçado", size = 4, startX = 2, startY = 0, orientation = "Vertical" },
                    new { name = "Submarino", size = 3, startX = 4, startY = 0, orientation = "Vertical" },
                    new { name = "Destroyer", size = 3, startX = 6, startY = 0, orientation = "Vertical" },
                    new { name = "Patrulha", size = 2, startX = 8, startY = 0, orientation = "Vertical" }
                }
            }
        );
        responseSetupMatch.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Usuário não autenticado deve receber 401 Unauthorized ao acessar Setup Match");

        var responseShotMatch = await _client.PostAsJsonAsync(EndpointSetup,
            new
            {
                matchId = matchIdFake,
                x = 0,
                y = 0
            }
        );
        responseShotMatch.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Usuário não autenticado deve receber 401 Unauthorized ao acessar Shot Match");

        var responseCancelMatch = await _client.PostAsync($"{Endpoint}/{matchIdFake}/cancel", null);
        responseCancelMatch.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Usuário não autenticado deve receber 401 Unauthorized ao acessar Cancel Match");
    }

    private async Task Passo_VerificarCadastroDePartidaSemPayload()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authInfoUsuarioDaPartida.AccessToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType,
            "Usuário não enviou payload exigido no POST para criar Partida");
    }

    private async Task Passo_VerificarCadastroDePartidaComPayloadErrado()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authInfoUsuarioDaPartida.AccessToken);
        var payloadsErrados = new[]
        {
            new { Mode = "Classico", AiDifficulty = "Basic" },
            new { Mode = "Classic", AiDifficulty = "Basico" }
        };

        foreach (var payload in payloadsErrados)
        {
            var response = await _client.PostAsJsonAsync(Endpoint, payload);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "Usuário não enviou payload exigido no POST para criar Partida");
        }

        var conflictPayload = new
            { Mode = "Classic", AiDifficulty = "Basic", OpponentId = "292689b0-b34a-4c6c-9feb-83e468366a78" };
        var responseConflictPayload = await _client.PostAsJsonAsync(Endpoint, conflictPayload);
        responseConflictPayload.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Usuário não enviou payload exigido no POST para criar Partida");
        var errorDetails = await responseConflictPayload.Content.ReadFromJsonAsync<ProblemDetails>();
        errorDetails?.Detail.Should()
            .Contain("Não é possível definir um oponente humano e uma dificuldade de IA ao mesmo tempo.");
    }

    private async Task<RealMatch> Passo_VerificarCadastroDePartida()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authInfoUsuarioDaPartida.AccessToken);
        var response = await _client.PostAsJsonAsync(Endpoint, new
        {
            Mode = "Classic",
            AiDifficulty = "Basic"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "Criação de partida deveria ser OK");
        var matchResult = await response.Content.ReadFromJsonAsync<RealMatch>();
        return matchResult;
    }

    private async Task Passo_VerificarCadastroDePartidaComPartidaEmAndamento()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authInfoUsuarioDaPartida.AccessToken);
        var response = await _client.PostAsJsonAsync(Endpoint, new
        {
            Mode = "Classic",
            AiDifficulty = "Basic"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "Criação de partida com outra em andamento para o mesmo usuário");
        var errorResult = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        errorResult?.Detail.Should().Contain($"O usuário já possui uma partida ativa (ID: {matchResult.MatchId}).");
    }

    private async Task Passo_VerificarCancelamentoDePartidaEmAndamentoComUsuarioExterno()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authInfoUsuarioExternoDaPartida.AccessToken);
        var response = await _client.PostAsync($"{Endpoint}/{matchResult.MatchId}/cancel", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "Jogador fora da partida não pode cancelar a partida");
        var errorResult = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        errorResult?.Detail.Should().Contain("O jogador não participa desta partida.");
    }

    private async Task Passo_VerificarCancelamentoDePartidaInexistente()
    {
        var matchIdFake = Guid.NewGuid().ToString();

        // AMBOS tentam cancelar
        // Usuario pirata
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authInfoUsuarioExternoDaPartida.AccessToken);
        var responsePirata = await _client.PostAsync($"{Endpoint}/{matchIdFake}/cancel", null);
        responsePirata.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "Cancelou partida inexistente");
        var errorResultPirata = await responsePirata.Content.ReadFromJsonAsync<ProblemDetails>();
        errorResultPirata?.Detail.Should().Contain($"Partida {matchIdFake} não encontrada.");

        // Usuario oficial
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authInfoUsuarioDaPartida.AccessToken);
        var responseOficial = await _client.PostAsync($"{Endpoint}/{matchIdFake}/cancel", null);
        responseOficial.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "Cancelou partida inexistente");
        var errorResultOficial = await responseOficial.Content.ReadFromJsonAsync<ProblemDetails>();
        errorResultOficial?.Detail.Should().Contain($"Partida {matchIdFake} não encontrada.");
    }

    private async Task Passo_VerificarCancelamentoDePartida()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authInfoUsuarioDaPartida.AccessToken);
        var response = await _client.PostAsync($"{Endpoint}/{matchResult.MatchId}/cancel", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "Deveria cancelar partida");
    }

    private async Task Passo_VerificarSetupDePartidaSemPayload()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, EndpointSetup);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authInfoUsuarioDaPartida.AccessToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType,
            "Usuário não enviou payload exigido no POST para efetuar Setup da Partida");
    }

    private async Task Passo_VerificarSetupDePartidaComPayloadErrado()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authInfoUsuarioDaPartida.AccessToken);
        var matchIdFake = Guid.NewGuid().ToString();
        var matchIdReal = matchResult.MatchId.ToString();

        var scenarios = new List<TestCase>
        {
            new("Wrong matchId and Empty List",
                new { MatchId = matchIdFake, Ships = new List<ShipPlacementDto>() }, HttpStatusCode.BadRequest,
                "A lista de navios não pode estar vazia."),
            new("Correct matchId and wrong empty list",
                new { MatchId = matchIdReal, Ships = new List<ShipPlacementDto>() }, HttpStatusCode.BadRequest,
                "A lista de navios não pode estar vazia."),
            new("Wrong matchId and correct list", new { MatchId = matchIdFake, Ships = GetDefaultFleet() },
                HttpStatusCode.NotFound, "Partida não encontrada."),
            new("Correct matchId and list of ships with exceeding carriers",
                new
                {
                    MatchId = matchIdReal,
                    Ships = GetDefaultFleet().Concat([
                        new ShipPlacementDto("Porta-Aviões", 6, 0, 9, ShipOrientation.Horizontal)
                    ])
                }, HttpStatusCode.BadRequest,
                "A frota deve conter: 2 Porta-Aviões (6), 2 Navios de guerra (4), 1 Encouraçado (3), e 1 Submarino (1)."),
            new("Correct matchId and list of ships with absenting carriers",
                new { MatchId = matchIdReal, Ships = GetDefaultFleet().Where((ship, index) => index != 1) },
                HttpStatusCode.BadRequest,
                "A frota deve conter: 2 Porta-Aviões (6), 2 Navios de guerra (4), 1 Encouraçado (3), e 1 Submarino (1)."),
            new("Correct matchId and list of ships with exceeding destroyers",
                new
                {
                    MatchId = matchIdReal,
                    Ships = GetDefaultFleet()
                        .Concat([new ShipPlacementDto("Destroyer", 4, 0, 9, ShipOrientation.Horizontal)])
                }, HttpStatusCode.BadRequest,
                "A frota deve conter: 2 Porta-Aviões (6), 2 Navios de guerra (4), 1 Encouraçado (3), e 1 Submarino (1)."),
            new("Correct matchId and list of ships with absenting destroyers",
                new { MatchId = matchIdReal, Ships = GetDefaultFleet().Where((ship, index) => index != 3) },
                HttpStatusCode.BadRequest,
                "A frota deve conter: 2 Porta-Aviões (6), 2 Navios de guerra (4), 1 Encouraçado (3), e 1 Submarino (1)."),
            new("Correct matchId and list of ships with exceeding battleships",
                new
                {
                    MatchId = matchIdReal,
                    Ships = GetDefaultFleet().Concat([
                        new ShipPlacementDto("Encouraçado", 3, 0, 9, ShipOrientation.Horizontal)
                    ])
                }, HttpStatusCode.BadRequest,
                "A frota deve conter: 2 Porta-Aviões (6), 2 Navios de guerra (4), 1 Encouraçado (3), e 1 Submarino (1)."),
            new("Correct matchId and list of ships without battleships",
                new { MatchId = matchIdReal, Ships = GetDefaultFleet().Where((ship, index) => index != 4) },
                HttpStatusCode.BadRequest,
                "A frota deve conter: 2 Porta-Aviões (6), 2 Navios de guerra (4), 1 Encouraçado (3), e 1 Submarino (1)."),
            new("Correct matchId and list of ships with exceeding submarines observers",
                new
                {
                    MatchId = matchIdReal,
                    Ships = GetDefaultFleet()
                        .Concat([new ShipPlacementDto("Patrulha", 1, 0, 9, ShipOrientation.Horizontal)])
                }, HttpStatusCode.BadRequest,
                "A frota deve conter: 2 Porta-Aviões (6), 2 Navios de guerra (4), 1 Encouraçado (3), e 1 Submarino (1)."),
            new("Correct matchId and list of ships without submarines observers",
                new { MatchId = matchIdReal, Ships = GetDefaultFleet().Where((ship, index) => index != 5) },
                HttpStatusCode.BadRequest,
                "A frota deve conter: 2 Porta-Aviões (6), 2 Navios de guerra (4), 1 Encouraçado (3), e 1 Submarino (1)."),
            new("Correct matchId and list of ships with wrong carriers size",
                new
                {
                    MatchId = matchIdReal,
                    Ships = GetDefaultFleet().Where(s => s.Size != 6).Concat([
                        new ShipPlacementDto("Porta-Aviões", 5, 0, 0, ShipOrientation.Horizontal),
                        new ShipPlacementDto("Porta-Aviões", 6, 0, 3, ShipOrientation.Horizontal)
                    ])
                }, HttpStatusCode.BadRequest,
                "A frota deve conter: 2 Porta-Aviões (6), 2 Navios de guerra (4), 1 Encouraçado (3), e 1 Submarino (1)."),
            new("Correct matchId and list of ships with wrong destroyers size",
                new
                {
                    MatchId = matchIdReal,
                    Ships = GetDefaultFleet().Where(s => s.Size != 4).Concat([
                        new ShipPlacementDto("Destroyer", 5, 0, 3, ShipOrientation.Horizontal),
                        new ShipPlacementDto("Destroyer", 3, 6, 1, ShipOrientation.Horizontal)
                    ])
                }, HttpStatusCode.BadRequest,
                "A frota deve conter: 2 Porta-Aviões (6), 2 Navios de guerra (4), 1 Encouraçado (3), e 1 Submarino (1)."),
            new("Correct matchId and list of ships with wrong battleship size",
                new
                {
                    MatchId = matchIdReal,
                    Ships = GetDefaultFleet().Where(s => s.Size != 3).Concat([
                        new ShipPlacementDto("Encouraçado", 2, 0, 2, ShipOrientation.Horizontal)
                    ])
                }, HttpStatusCode.BadRequest,
                "A frota deve conter: 2 Porta-Aviões (6), 2 Navios de guerra (4), 1 Encouraçado (3), e 1 Submarino (1)."),
            new("Correct matchId and list of ships with wrong submarine observer size",
                new
                {
                    MatchId = matchIdReal,
                    Ships = GetDefaultFleet().Where(s => s.Size != 1).Concat([
                        new ShipPlacementDto("Patrulha", 2, 3, 2, ShipOrientation.Horizontal)
                    ])
                }, HttpStatusCode.BadRequest,
                "A frota deve conter: 2 Porta-Aviões (6), 2 Navios de guerra (4), 1 Encouraçado (3), e 1 Submarino (1)."),
            new("Correct matchId and list of ships with position overlay",
                new
                {
                    MatchId = matchIdReal,
                    Ships = GetDefaultFleet().Where((ship, index) => index != 1).Concat([
                        new ShipPlacementDto("Porta-Aviões", 6, 0, 0, ShipOrientation.Horizontal)
                    ])
                }, HttpStatusCode.Conflict, "colisão detectada com outro navio na posição (0, 0).")
        };

        await ExecuteScenarios(scenarios, EndpointSetup);
    }

    private async Task Passo_VerificarSetupDePartida()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authInfoUsuarioDaPartida.AccessToken);
        var response = await _client.PostAsJsonAsync(EndpointSetup, new
        {
            MatchId = matchResult.MatchId.ToString(),
            Ships = GetDefaultFleet()
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, "O setup deveria ser OK");
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Navios posicionados com sucesso. Aguardando início.");
    }

    private async Task Passo_VerificarShotSemPayload()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authInfoUsuarioDaPartida.AccessToken);
        var response = await _client.PostAsync(EndpointShot, null);
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType, "Tiro sem payload não deveria ser aceito");
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Unsupported Media Type");
    }

    private async Task Passo_VerificarShotComPayloadErrado()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authInfoUsuarioDaPartida.AccessToken);
        var matchIdFake = Guid.NewGuid().ToString();
        var matchIdReal = matchResult.MatchId.ToString();

        var scenarios = new List<TestCase>
        {
            new("Wrong matchId and correct coordinates", new { MatchId = matchIdFake, x = 0, y = 0 },
                HttpStatusCode.NotFound, "Partida não encontrada."),
            new("Correct matchId and wrong X coordinates", new { MatchId = matchIdReal, x = 10, y = 9 },
                HttpStatusCode.BadRequest, "A coordenada X deve estar entre 0 e 9."),
            new("Correct matchId and wrong Y coordinates", new { MatchId = matchIdReal, x = 9, y = 10 },
                HttpStatusCode.BadRequest, "A coordenada Y deve estar entre 0 e 9."),
            new("Correct matchId and without X coordinates", new { MatchId = matchIdReal, y = 9 },
                HttpStatusCode.BadRequest, "A coordenada X é obrigatória."),
            new("Correct matchId and without Y coordinates", new { MatchId = matchIdReal, x = 9 },
                HttpStatusCode.BadRequest, "A coordenada Y é obrigatória."),
            new("Correct matchId and no coordinates", new { MatchId = matchIdReal }, HttpStatusCode.BadRequest,
                "{\"x\":[\"a coordenada x é obrigatória.\"],\"y\":[\"a coordenada y é obrigatória.\"]}")
        };

        await ExecuteScenarios(scenarios, EndpointShot);
    }

    private async Task Passo_VerificarShotDuplicado()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authInfoUsuarioDaPartida.AccessToken);
        var matchIdReal = matchResult.MatchId.ToString();
        var firstResponse = await _client.PostAsJsonAsync(EndpointShot, new { MatchId = matchIdReal, x = 1, y = 1 });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Deveria dar bom");
        var secondResponse = await _client.PostAsJsonAsync(EndpointShot, new { MatchId = matchIdReal, x = 1, y = 1 });
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict, "Deveria dar ruim");
        var errorContent = await secondResponse.Content.ReadAsStringAsync();
        errorContent.Should().Contain("A posição (1, 1) já foi alvejada previamente.", "Deveria barrar tiro repetido");
    }

    private List<ShipPlacementDto> GetDefaultFleet()
    {
        return new List<ShipPlacementDto>
        {
            new("Porta-Aviões", 6, 0, 0, ShipOrientation.Horizontal),
            new("Porta-Aviões", 6, 0, 1, ShipOrientation.Horizontal),
            new("Destroyer", 4, 6, 0, ShipOrientation.Horizontal),
            new("Destroyer", 4, 6, 1, ShipOrientation.Horizontal),
            new("Encouraçado", 3, 0, 2, ShipOrientation.Horizontal),
            new("Patrulha", 1, 3, 2, ShipOrientation.Horizontal)
        };
    }

    private record TestCase(
        string Description,
        object Payload,
        HttpStatusCode ExpectedStatusCode,
        string? ExpectedErrorMessage = null,
        bool? Deactivated = false);

    private async Task ExecuteScenarios(List<TestCase> scenarios, string endpoint)
    {
        foreach (var scenario in scenarios)
        {
            if (scenario?.Deactivated is true) continue;

            if (scenario.ExpectedErrorMessage.Contains("Non ecziste")) Console.WriteLine();

            var response = await _client.PostAsJsonAsync(endpoint, scenario.Payload);

            response.StatusCode.Should().Be(scenario.ExpectedStatusCode,
                $"no cenário '{scenario.Description}' o status deveria ser {scenario.ExpectedStatusCode}");

            var errorContent = await response.Content.ReadAsStringAsync();

            if (!string.IsNullOrEmpty(scenario.ExpectedErrorMessage))
                errorContent.ToLower().Should().Contain(scenario.ExpectedErrorMessage.ToLower(),
                    $"no cenário '{scenario.Description}' a mensagem de erro deveria mencionar o problema específico");
        }
    }

    #endregion
}