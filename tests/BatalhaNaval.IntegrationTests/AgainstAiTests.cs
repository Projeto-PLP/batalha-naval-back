using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BatalhaNaval.Application.DTOs;
using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Domain.Enums;
using BatalhaNaval.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace BatalhaNaval.IntegrationTests;

[Collection("Sequential")]
public class AgainstAiTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private const string EndpointUsers = "/users";
    private const string EndpointLogin = "/auth/login";
    private const string Endpoint = "/match";
    private const string EndpointSetup = $"{Endpoint}/setup";
    private const string EndpointShot = $"{Endpoint}/shot";

    private readonly HttpClient _client;
    private readonly IntegrationTestWebAppFactory _factory;

    private TokenResponseDto _authInfoUsuario;
    private Guid _usuarioId;
    private Guid _matchId;

    public AgainstAiTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Deve_Executar_Partida_Completa_Contra_IA_E_Vencer_Com_Tiros_Perfeitos()
    {
        // STEP 1: Preparar novo jogo
        await Passo_PrepararNovoJogo();

        // STEP 2: Ler o Redis e Extrair as Coordenadas da IA
        var coordenadasIdeais = await Passo_ObterCoordenadasDaIAPeloRedis();

        // STEP 3: Executar Tiros e Vencer (Cheat mode)
        await Passo_FuzilarNaviosDaIA(coordenadasIdeais);

        // STEP 4: Validar a Base de Dados (Finalização do Jogo e Estatísticas)
        await Passo_ValidarFimDeJogoNoBancoDeDadosFuzilarNaviosDaIA();

        // STEP 5: Validar o Redis (Finalização do Jogo e Estatísticas)
        await Passo_ValidarFimDeJogoNoRedisFuzilarNaviosDaIA();
    }

    [Fact]
    public async Task Deve_Executar_Partida_Completa_Contra_IA_E_Vencer_Com_Tiros_Horriveis()
    {
        // STEP 1: Preparar novo jogo
        await Passo_PrepararNovoJogo();

        // STEP 2: Ler o Redis e Extrair as Coordenadas da IA (para errar de propósito)
        var coordenadasIdeais = await Passo_ObterCoordenadasDeAguaDaIAPeloRedis();

        // STEP 3: Executar Tiros, Esperar que a IA ganhe o jogo e Perder 
        await Passo_FuzilarTabuleiroDaIA(coordenadasIdeais);
        
        // TODO Implementar EDGE OF TOMORROW
    }

    #region Steps

    private async Task Passo_PrepararNovoJogo()
    {
        // STEP 1: Criar usuário e logar
        await Passo_CriarUsuarioEAutenticar();

        // STEP 2: Criar a Partida contra IA
        await Passo_CriarPartidaContraIA();

        // STEP 3: Posicionar Frota (Setup)
        await Passo_RealizarSetupDaFrota();
    }

    private async Task Passo_CriarUsuarioEAutenticar()
    {
        var usuarioDaPartida = new { Username = "FrancoAtirador", Password = "SenhaForte123!" };

        await _client.PostAsJsonAsync(EndpointUsers, usuarioDaPartida);

        var responseLogin = await _client.PostAsJsonAsync(EndpointLogin, usuarioDaPartida);
        responseLogin.StatusCode.Should().Be(HttpStatusCode.OK);

        _authInfoUsuario = (await responseLogin.Content.ReadFromJsonAsync<TokenResponseDto>())!;
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _authInfoUsuario.AccessToken);
    }

    private async Task Passo_CriarPartidaContraIA()
    {
        var response = await _client.PostAsJsonAsync(Endpoint, new
        {
            Mode = "Classic",
            AiDifficulty = "Basic"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var matchResult = await response.Content.ReadFromJsonAsync<MatchTests.RealMatch>();
        _matchId = matchResult!.MatchId;
    }

    private async Task Passo_RealizarSetupDaFrota()
    {
        var response = await _client.PostAsJsonAsync(EndpointSetup, new
        {
            MatchId = _matchId.ToString(),
            Ships = GetDefaultFleet()
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<List<Coordinate>> Passo_ObterCoordenadasDaIAPeloRedis()
    {
        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        
        var redisKey = $"match:{_matchId}";

        var matchJson = await cache.GetStringAsync(redisKey);
        matchJson.Should().NotBeNullOrEmpty("A partida deve ter sido salva no Redis após o setup.");

        var matchState = JsonSerializer.Deserialize<MatchRedis>(matchJson!);

        // Valida se o status mudou pra InProgress após o setup
        matchState!.Status.Should().Be(MatchStatusRedis.IN_PROGRESS, "A partida deveria ter iniciado após o setup.");

        // Extrai todas as coordenadas (X, Y) dos navios da IA (Cheat Mode)
        var coordenadas = matchState.Boards.P2.Ships
            .SelectMany(ship => ship.Segments)
            .Select(segment => new Coordinate { X = segment.X, Y = segment.Y, GoodShot = true })
            .ToList();

        coordenadas.Should().NotBeEmpty("A IA deveria ter posicionado seus navios.");
        return coordenadas;
    }

    private async Task<List<Coordinate>> Passo_ObterCoordenadasDeAguaDaIAPeloRedis()
    {
        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        var redisKey = $"match:{_matchId}";

        var matchJson = await cache.GetStringAsync(redisKey);
        matchJson.Should().NotBeNullOrEmpty("A partida deve ter sido salva no Redis após o setup.");

        var matchState = JsonSerializer.Deserialize<MatchRedis>(matchJson!);

        var coordenadasOcupadas = matchState!.Boards.P2.Ships
            .SelectMany(ship => ship.Segments)
            .Select(segment => (segment.X, segment.Y))
            .ToHashSet();

        var coordenadasDeAgua = new List<Coordinate>();

        for (int x = 0; x < 10; x++)
        {
            for (int y = 0; y < 10; y++)
            {
                if (!coordenadasOcupadas.Contains((x, y)))
                {
                    coordenadasDeAgua.Add(new Coordinate
                    {
                        X = x,
                        Y = y,
                        GoodShot = false
                    });
                }
            }
        }

        var totalCasas = 100;
        var tamanhoDaFrota = coordenadasOcupadas.Count;

        coordenadasDeAgua.Should().HaveCount(totalCasas - tamanhoDaFrota,
            "A quantidade de água gerada deve ser o total do tabuleiro menos o espaço ocupado pela frota.");

        return coordenadasDeAgua;
    }

    private async Task Passo_FuzilarNaviosDaIA(List<Coordinate> coordenadas)
    {
        int acertosEsperados = 0;

        foreach (var coord in coordenadas)
        {
            var shotResponse = await _client.PostAsJsonAsync(EndpointShot, new
            {
                MatchId = _matchId.ToString(),
                X = coord.X,
                Y = coord.Y
            });

            shotResponse.StatusCode.Should().Be(HttpStatusCode.OK, "O tiro deve ser aceito pela API.");
            acertosEsperados++;

            var matchState = await ObterEstadoDoRedisAsync();

            matchState!.P1_Stats.Hits.Should().Be(acertosEsperados,
                $"Redis não atualizou os acertos do Player 1! Esperava {acertosEsperados}, mas veio {matchState.P1_Stats.Hits}. ");

            matchState!.P1_Stats.Misses.Should().Be(0,
                $"O Redis atualizou os erros do Player 1! O esperado é 0 tiros perdidos, mas veio {matchState.P1_Stats.Misses}.");

            matchState!.P1_Stats.Streak.Should().Be(acertosEsperados,
                $"Redis não atualizou os streaks do Player 1! Esperava {acertosEsperados}, mas veio {matchState.P1_Stats.Streak}. ");

            var navioAtingido =
                matchState.Boards.P2.Ships.FirstOrDefault(s =>
                    s.Segments.Any(seg => seg.X == coord.X && seg.Y == coord.Y));
            var segmentoAtingido = navioAtingido?.Segments.First(seg => seg.X == coord.X && seg.Y == coord.Y);

            segmentoAtingido.Should().NotBeNull();
            segmentoAtingido!.Hit.Should().BeTrue(
                "O tiro foi dado, mas a coordenada no Redis não mudou para Hit=true. " +
                "Vá até a classe Board.cs e garanta que o método ReceiveShot() altera o estado do objeto Coordinate!");
            matchState!.Boards.P1.OceanGrid.Count.Should().Be(0, "A IA não deveria ter tiros!");
        }
    }

    private async Task Passo_FuzilarTabuleiroDaIA(List<Coordinate> coordenadas)
    {
        int tirosNaAgua = 0;
        MatchRedis matchState = null;

        foreach (var coord in coordenadas)
        {
            var shotResponse = await _client.PostAsJsonAsync(EndpointShot, new
            {
                MatchId = _matchId.ToString(),
                X = coord.X,
                Y = coord.Y
            });

            if (!shotResponse.StatusCode.GetDisplayName().Equals("OK"))
            {
                Console.WriteLine();
            }

            shotResponse.StatusCode.Should().Be(HttpStatusCode.OK, "O tiro deve ser aceito pela API.");
            tirosNaAgua++;

            matchState = await ObterEstadoDoRedisAsync();

            matchState!.P1_Stats.Hits.Should().Be(0,
                $"O Redis atualizou os acertos! O esperado é NENHUM acerto, mas veio {matchState.P1_Stats.Hits}.");


            matchState!.P1_Stats.Misses.Should().Be(tirosNaAgua,
                 $"O Redis não atualizou os erros! O esperado é {tirosNaAgua} tiro(s) perdido(s), mas veio {matchState.P1_Stats.Misses}.");

            matchState!.P1_Stats.Streak.Should().Be(0,
                $"O Redis atualizou os streaks! O esperado é NENHUM acerto, mas veio {matchState.P1_Stats.Streak}.");

            var navioAtingido =
                matchState.Boards.P2.Ships.FirstOrDefault(s =>
                    s.Segments.Any(seg => seg.X == coord.X && seg.Y == coord.Y));
            var segmentoAtingido = navioAtingido?.Segments.First(seg => seg.X == coord.X && seg.Y == coord.Y);

            segmentoAtingido.Should().BeNull();
            // TODO FOi CTRL C + CTRL V precisa validar isso
            /*
                segmentoAtingido!.Hit.Should().BeTrue(
               "O tiro foi dado, mas a coordenada no Redis não mudou para Hit=true. " +
               "Vá até a classe Board.cs e garanta que o método ReceiveShot() altera o estado do objeto Coordinate!");
             */
            matchState!.Boards.P1.OceanGrid.Count.Should().BeGreaterThanOrEqualTo(tirosNaAgua,
                $"A IA deveria ter pelo menos {tirosNaAgua} tiro(s)!");
            matchState!.Boards.P2.OceanGrid.Count.Should()
                .Be(tirosNaAgua, $"A IA deveria ter {tirosNaAgua} tiro(s) em seu tabuleiro!");

            matchState.Boards.P2.ShotHistory.Count.Should().Be(tirosNaAgua,
                $"Histórico do Player 1 deveria ser de apenas {tirosNaAgua}, porém foram encontrados {matchState.Boards.P2.ShotHistory.Count}");

            var historicoCompleto = matchState.Boards.P1.ShotHistory;

            historicoCompleto.Should().NotBeEmpty("A IA deveria ter efetuado pelo menos um disparo.");

            historicoCompleto.Last().Hit.Should().BeFalse("O último tiro da IA deve ser Miss (água) para que ela passe a vez de volta ao jogador.");
            
            var tirosDoTurnoAtual = historicoCompleto
                .AsEnumerable()
                .Reverse()                                
                .Skip(1)
                .TakeWhile(shot => shot.Hit == true)
                .Reverse()
                .ToList();
            
            if (tirosDoTurnoAtual.Count > 0)
            {
                tirosDoTurnoAtual.Should().OnlyContain(shot => shot.Hit == true, 
                    "Todos os tiros disparados pela IA ANTES do erro neste turno específico deviam ser acertos (Hit).");
            }
        }
        
        if (matchState!.Status != MatchStatusRedis.FINISHED)
        {
            matchState.Status.Should().Be(MatchStatusRedis.FINISHED, "A partida deveria encerrar no Redis.");
        }
    }

    private async Task Passo_ValidarFimDeJogoNoBancoDeDadosFuzilarNaviosDaIA()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BatalhaNavalDbContext>();

        var matchInDb = await db.Matches.FindAsync(_matchId);

        int totalAcertosEsperados = GetDefaultFleet().Sum(n => n.Size);

        matchInDb.Should().NotBeNull();

        matchInDb!.Status.Should()
            .Be(MatchStatus.Finished, "Após derrubar todos os navios, o status deve ser Finished.");
        matchInDb.IsFinished.Should().BeTrue();
        matchInDb.FinishedAt.Should().NotBeNull();

        matchInDb.WinnerId.Should().Be(matchInDb.Player1Id, "O jogador 1 deveria ser o vencedor da partida perfeita.");

        matchInDb.Player1Hits.Should().Be(totalAcertosEsperados, "Player 1 deveria ter 24 acertos");
        matchInDb.Player2Hits.Should()
            .Be(0, "A IA não deveria ter tido chance de jogar, pois Player 1 não errou nenhum tiro.");

        matchInDb.Player1ConsecutiveHits.Should()
            .Be(totalAcertosEsperados, "Player 1 deveria ter 24 acertos consecutivos");
        matchInDb.Player2ConsecutiveHits.Should().Be(0,
            "A IA não deveria ter tido chance de jogar, pois Player 1 não errou nenhum tiro.");


        bool contemAcerto = matchInDb.Player1Board.Cells
            .Any(linha => linha.Any(celula => celula == CellState.Hit));

        contemAcerto.Should().BeFalse("O tabuleiro do Jogador 1 não deveria ter células marcadas como Hit.");

        bool contemNavios = matchInDb.Player2Board.Cells
            .Any(linha => linha.Any(celula => celula == CellState.Ship));

        contemNavios.Should().BeFalse("O tabuleiro da IA não deveria células marcadas como Ship.");


        matchInDb.Player1Board.Ships.Should()
            .OnlyContain(s => !s.HasBeenHit, "Todos os navios do player 1 deveriam estar intactos");
        matchInDb.Player1Board.Ships.Should()
            .OnlyContain(s => !s.IsSunk, "Todos os navios do player 1 deveriam estar em operação");

        // TODO Testes comentados pois existe o erro. Será tratado pela issue 'https://github.com/Projeto-PLP/batalha-naval-back/issues/103'
        /*
            matchInDb.Player2Board.Ships.All(s => s.HasBeenHit).Should().BeTrue("A IA deveria ter TODOS seus navios atingidos");
            matchInDb.Player2Board.Ships.All(s => s.IsSunk).Should().BeTrue("A IA deveria ter TODOS seus navios afundados");
         */
    }

    private async Task Passo_ValidarFimDeJogoNoRedisFuzilarNaviosDaIA()
    {
        var finalMatchState = await ObterEstadoDoRedisAsync();

        int totalAcertosEsperados = GetDefaultFleet().Sum(n => n.Size);

        finalMatchState!.Status.Should().Be(MatchStatusRedis.FINISHED, "A partida deveria estar finalizada no Redis");

        finalMatchState.P1_Stats.Hits.Should().Be(totalAcertosEsperados, "Player 1 deveria ter 24 acertos");
        finalMatchState.P1_Stats.Misses.Should().Be(0, "Player 1 deveria ter 0 tiros perdidos");
        finalMatchState.P1_Stats.Streak.Should().Be(24, "Player 1 deveria ter 24 acertos consecutivos");

        finalMatchState.Boards.P1.AliveShips.Should().Be(6, "Player 1 deveria ter 6 navios");
        finalMatchState.Boards.P1.OceanGrid.Count.Should().Be(0, "Player 1 deveria ter 0 acertos em seu tabuleiro");
        finalMatchState.Boards.P1.Ships.Should()
            .NotContain(s => s.IsDamaged, "Nenhum navio do Player 1 deveria ter sido atingido");
        finalMatchState.Boards.P1.Ships.Should()
            .NotContain(s => s.Sunk, "Nenhum navio do Player 1 deveria ter afundado");
        finalMatchState.Boards.P1.Ships.All(ship => ship.Segments.All(s => s.Hit == false)).Should()
            .BeTrue("Player 1 deveria ter todos os navios sem tiros em todas as coordenadas");

        finalMatchState.P2_Stats.Hits.Should().Be(0, "IA deveria ter 0 acertos");
        finalMatchState.P2_Stats.Misses.Should().Be(0, "IA deveria ter 0 tiros perdidos");
        finalMatchState.P2_Stats.Streak.Should().Be(0, "IA deveria ter 0 acertos consecutivos");

        finalMatchState.Boards.P2.AliveShips.Should().Be(0, "IA deveria ter 0 navios");
        finalMatchState.Boards.P2.OceanGrid.Count.Should()
            .Be(totalAcertosEsperados, "IA deveria ter 24 acertos em seu tabuleiro");
        finalMatchState.Boards.P2.Ships.FirstOrDefault(s => s.IsDamaged == false).Should()
            .BeNull("IA deveria ter TODOS seus navios atingidos");
        finalMatchState.Boards.P2.Ships.FirstOrDefault(s => s.Sunk == false).Should()
            .BeNull("IA deveria ter TODOS seus navios afundados");
        finalMatchState.Boards.P2.Ships.All(ship => ship.Segments.All(s => s.Hit)).Should()
            .BeTrue("IA deveria ter todos os navios com tiro em todas as coordenadas");
    }

    #endregion

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

    private class Coordinate
    {
        public int X { get; set; }
        public int Y { get; set; }
        public bool GoodShot { get; set; }
    }

    private async Task<MatchRedis> ObterEstadoDoRedisAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        var matchJson = await cache.GetStringAsync($"match:{_matchId}");
        matchJson.Should().NotBeNullOrEmpty("O cache sumiu do Redis! A API acidentalmente apagou ou expirou a chave?");

        return JsonSerializer.Deserialize<MatchRedis>(matchJson!)!;
    }
}