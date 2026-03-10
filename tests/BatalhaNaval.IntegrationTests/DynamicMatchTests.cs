using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BatalhaNaval.Application.DTOs;
using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace BatalhaNaval.IntegrationTests;

[Collection("Sequential")]
public class DynamicMatchTests : IClassFixture<IntegrationTestWebAppFactory>,IAsyncLifetime
{
    private const string EndpointUsers = "/users";
    private const string EndpointLogin = "/auth/login";
    private const string Endpoint = "/match";
    private const string EndpointSetup = $"{Endpoint}/setup";
    private const string EndpointShot = $"{Endpoint}/shot";
    private const string EndpointMove = $"{Endpoint}/move";

    private readonly HttpClient _client;
    private readonly IntegrationTestWebAppFactory _factory;
    private readonly ITestOutputHelper _output;

    private TokenResponseDto _authInfoUsuario;
    private Guid _matchId;

    public DynamicMatchTests(IntegrationTestWebAppFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _output = output;
    }
    
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    // Chamado DEPOIS de cada teste
    public async Task DisposeAsync()
    {
        try
        {
            if (_matchId != Guid.Empty)
            {
                try
                {
                    var cancelResponse = await _client.PostAsync($"{Endpoint}/{_matchId}/cancel", null);
                    _output.WriteLine($"[Cleanup] Cancelamento via API: {cancelResponse.StatusCode}");
                }
                catch (Exception apiEx)
                {
                    _output.WriteLine($"[Cleanup] API cancel falhou: {apiEx.Message}");
                }
            }
            using var scope = _factory.Services.CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
            if (_matchId != Guid.Empty)
            {
                await cache.RemoveAsync($"match:{_matchId}");
            }
            
            _output.WriteLine($"[Cleanup] Redis limpo manualmente");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[Cleanup] Erro ao limpar: {ex.Message}");
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            _matchId = Guid.Empty;
            _authInfoUsuario = null;
        }
    }

    [Fact]
    public async Task Deve_Criar_Partida_Dinamica_E_Fazer_Setup_Com_Sucesso()
    {
        await Passo_CriarUsuarioEAutenticar("user", "123456");

        var response = await _client.PostAsJsonAsync(Endpoint, new
        {
            Mode = "Dynamic",
            AiDifficulty = "Basic"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, "Deve criar partida no modo dinamico");

        var matchResult = await response.Content.ReadFromJsonAsync<MatchTests.RealMatch>();
        _matchId = matchResult!.MatchId;

        var setupResponse = await _client.PostAsJsonAsync(EndpointSetup, new
        {
            MatchId = _matchId.ToString(),
            Ships = GetDefaultFleet()
        });

        setupResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Setup deve funcionar no modo dinamico");

        var redisState = await ObterEstadoDoRedisAsync();
        redisState.GameMode.Should().Be(GameModeRedis.DYNAMIC, "O Redis deve ter iniciado a partida");
        redisState.Status.Should().Be(MatchStatusRedis.IN_PROGRESS, "Partida deve estar em andamento após o setup");
    }

    [Theory]
    [InlineData("Basic")]
    [InlineData("Intermediate")]
    [InlineData("Advanced")]
    public async Task Deve_Mover_Navio_Com_Sucesso_No_Modo_Dinamico(string dificuldate)
    {
        await Passo_PrepararNovoJogoDinamico("user", "123456", dificuldate);

        var redisState = await ObterEstadoDoRedisAsync();
        var navio = ObterNavioComMovimentoLivre(redisState.Boards.P1.Ships);
        navio.Should().NotBeNull("Deve haver pelo menos um navio com espaço para mover sem colisão");
        var shipId = Guid.Parse(navio!.Id);
        var direction = ObterDirecaoSeguraParaNavio(navio, redisState.Boards.P1.Ships);

        var moveResponse = await _client.PostAsJsonAsync(EndpointMove, new
        {
            MatchId = _matchId.ToString(),
            ShipId = shipId.ToString(),
            Direction = direction.ToString()
        });

        moveResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Deve ser possível mover um navio no modo dinâmico");
        var redisStateAfter = await ObterEstadoDoRedisAsync();
        redisStateAfter.MovedThisTurn.Should().BeTrue("HasMovedThisTurn deve ser true após mover");
    }

    [Fact]
    public async Task Nao_Deve_Mover_Navio_No_Modo_Classico()
    {
        await Passo_CriarUsuarioEAutenticar("user", "123456");

        var createResponse = await _client.PostAsJsonAsync(Endpoint, new
        {
            Mode = "Classic",
            AiDifficulty = "Basic"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var matchResult = await createResponse.Content.ReadFromJsonAsync<MatchTests.RealMatch>();
        _matchId = matchResult!.MatchId;

        var setupResponse = await _client.PostAsJsonAsync(EndpointSetup, new
        {
            MatchId = _matchId.ToString(),
            Ships = GetDefaultFleet()
        });
        setupResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var redisState = await ObterEstadoDoRedisAsync();
        var navio = redisState.Boards.P1.Ships.First();
        var shipId = Guid.Parse(navio.Id);

        var moveResponse = await _client.PostAsJsonAsync(EndpointMove, new
        {
            MatchId = _matchId.ToString(),
            ShipId = shipId.ToString(),
            Direction = "North"
        });

        moveResponse.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "Não deve ser possível mover navio no modo Classic");
    }

    [Theory]
    [InlineData("Basic")]
    [InlineData("Intermediate")]
    [InlineData("Advanced")]
    public async Task Nao_Deve_Mover_Navio_Duas_Vezes_No_Mesmo_Turno(string dificulty)
    {
        await Passo_PrepararNovoJogoDinamico("user", "123456",dificulty);

        var redisState = await ObterEstadoDoRedisAsync();
        var navio = ObterNavioComMovimentoLivre(redisState.Boards.P1.Ships);
        navio.Should().NotBeNull("Deve haver pelo menos um navio com espaço para mover");
        var shipId = Guid.Parse(navio!.Id);
        var direction = ObterDirecaoSeguraParaNavio(navio, redisState.Boards.P1.Ships);

        // Primeiro movimento — deve funcionar
        var firstMove = await _client.PostAsJsonAsync(EndpointMove, new
        {
            MatchId = _matchId.ToString(),
            ShipId = shipId.ToString(),
            Direction = direction.ToString()
        });
        firstMove.StatusCode.Should().Be(HttpStatusCode.OK, "É possivel mover um navio no modo dinamico");

        // Segundo movimento no mesmo turno deve falhar
        var secondMove = await _client.PostAsJsonAsync(EndpointMove, new
        {
            MatchId = _matchId.ToString(),
            ShipId = shipId.ToString(),
            Direction = direction.ToString()
        });
        secondMove.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "Não deve ser possível mover duas vezes no mesmo turno ");
    }

    [Theory]
    [InlineData("Basic")]
    [InlineData("Intermediate")]
    [InlineData("Advanced")]
    public async Task Nao_Deve_Mover_Navio_Avariado(string dificulty)
    {
        await Passo_PrepararNovoJogoDinamico("user", "123456",dificulty);


        var redisState = await ObterEstadoDoRedisAsync();
        ShipRedis? navioDanificado;
        var jaAtiradas = new HashSet<(int, int)>(); //salvar onde ja atirou
    
        do
        {
            var aguaCoord = ObterCoordenadaDeAguaNaoAtirada(redisState.Boards.P2.Ships, jaAtiradas);
            if (aguaCoord == null)
            {
                _output.WriteLine("Não há coordenadas de água disponíveis. Teste ignorado.");
                return;
            }

            var shotResponse = await _client.PostAsJsonAsync(EndpointShot, new
            {
                MatchId = _matchId.ToString(),
                X = aguaCoord.Value.X,
                Y = aguaCoord.Value.Y
            });
            shotResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Tiro na água deve ser aceito");
            jaAtiradas.Add(aguaCoord.Value);

            var stateAfterIa = await ObterEstadoDoRedisAsync();

            if (stateAfterIa.Status == MatchStatusRedis.FINISHED)
            {
                _output.WriteLine("Partida encerrada pela IA antes de danificar navio. Teste ignorado.");
                return;
            }

            navioDanificado = stateAfterIa.Boards.P1.Ships.FirstOrDefault(s => s.IsDamaged);
        
            redisState = stateAfterIa;
        
        } while (navioDanificado == null);

        var shipId = Guid.Parse(navioDanificado.Id);

        var direcao = navioDanificado.Size == 1
            ? "North"
            : navioDanificado.Orientation == ShipOrientationRedis.HORIZONTAL
                ? "East"
                : "North";

        var moveResponse = await _client.PostAsJsonAsync(EndpointMove, new
        {
            MatchId = _matchId.ToString(),
            ShipId = shipId.ToString(),
            Direction = direcao
        });

        moveResponse.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "Não deve ser possível mover um navio avariado");
    }


    [Theory]
    [InlineData("Basic")]
    [InlineData("Intermediate")]
    [InlineData("Advanced")]
    public async Task Nao_Deve_Mover_Navio_Fora_Do_Tabuleiro(string dificulty)
    {
        await Passo_CriarUsuarioEAutenticar("user", "123456");

        var createResponse = await _client.PostAsJsonAsync(Endpoint, new
        {
            Mode = "Dynamic",
            AiDifficulty = dificulty
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var matchResult = await createResponse.Content.ReadFromJsonAsync<MatchTests.RealMatch>();
        _matchId = matchResult!.MatchId;

        //frota com submarina ja na borda 
        
        var frotaBorda = new List<ShipPlacementDto>
        {
            new("Porta-Aviões", 6, 0, 2, ShipOrientation.Horizontal),   
            new("Porta-Aviões", 6, 0, 4, ShipOrientation.Horizontal),   
            new("Destroyer", 4, 0, 6, ShipOrientation.Horizontal),      
            new("Destroyer", 4, 0, 8, ShipOrientation.Horizontal),      
            new("Encouraçado", 3, 6, 2, ShipOrientation.Horizontal),    
            new("Submarino", 1, 9, 0, ShipOrientation.Horizontal)       
        };

        var setupResponse = await _client.PostAsJsonAsync(EndpointSetup, new
        {
            MatchId = _matchId.ToString(),
            Ships = frotaBorda
        });
        setupResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Setup deve funcionar");

        var redisState = await ObterEstadoDoRedisAsync();
        var navioSize1 = redisState.Boards.P1.Ships.FirstOrDefault(s => s.Size == 1 && !s.Sunk);
        navioSize1.Should().NotBeNull("Submarino deve existir no tabuleiro");

        var shipId = Guid.Parse(navioSize1!.Id);

        var moveForaDoBorda = await _client.PostAsJsonAsync(EndpointMove, new
        {
            MatchId = _matchId.ToString(),
            ShipId = shipId.ToString(),
            Direction = "North"
        });

        moveForaDoBorda.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "Mover navio para fora do tabuleiro deve ser proibido");
        
    }

    [Theory]
    [InlineData("Basic")]
    [InlineData("Intermediate")]
    [InlineData("Advanced")]
    public async Task Nao_Deve_Mover_Navio_Horizontal_Para_Norte_Ou_Sul(string dificulty)
    {
        await Passo_PrepararNovoJogoDinamico("DynAxisH1", "SenhaDyn123!",dificulty);

        var redisState = await ObterEstadoDoRedisAsync();

        // Frota padrão é toda horizontal, pega qualquer navio de que nao seja subminarino
        var navioHorizontal = redisState.Boards.P1.Ships
            .FirstOrDefault(s => s.Orientation == ShipOrientationRedis.HORIZONTAL && s.Size > 1 && !s.Sunk);

        navioHorizontal.Should().NotBeNull("Deve haver navios horizontais na frota padrão");
        var shipId = Guid.Parse(navioHorizontal!.Id);

        var moveNorthResponse = await _client.PostAsJsonAsync(EndpointMove, new
        {
            MatchId = _matchId.ToString(),
            ShipId = shipId.ToString(),
            Direction = "North"
        });

        moveNorthResponse.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "Navio horizontal não pode mover para Norte");
    }

    [Fact]
    public async Task Nao_Deve_Mover_Navio_Vertical_Para_Leste_Ou_Oeste()
    {
        await Passo_CriarUsuarioEAutenticar("user", "123456");

        var createResponse = await _client.PostAsJsonAsync(Endpoint, new
        {
            Mode = "Dynamic",
            AiDifficulty = "Basic"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var matchResult = await createResponse.Content.ReadFromJsonAsync<MatchTests.RealMatch>();
        _matchId = matchResult!.MatchId;

        var frotaVertical = new List<ShipPlacementDto>
        {
            new("Porta-Aviões", 6, 0, 0, ShipOrientation.Vertical),
            new("Porta-Aviões", 6, 1, 0, ShipOrientation.Vertical),
            new("Destroyer", 4, 2, 0, ShipOrientation.Vertical),
            new("Destroyer", 4, 3, 0, ShipOrientation.Vertical),
            new("Encouraçado", 3, 4, 0, ShipOrientation.Vertical),
            new("Patrulha", 1, 5, 0, ShipOrientation.Horizontal)
        };

        var setupResponse = await _client.PostAsJsonAsync(EndpointSetup, new
        {
            MatchId = _matchId.ToString(),
            Ships = frotaVertical
        });
        setupResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var redisState = await ObterEstadoDoRedisAsync();

        var navioVertical = redisState.Boards.P1.Ships
            .FirstOrDefault(s => s.Orientation == ShipOrientationRedis.VERTICAL && s.Size > 1 && !s.Sunk);

        navioVertical.Should().NotBeNull("Deve haver navios verticais na frota configurada");
        var shipId = Guid.Parse(navioVertical!.Id);

        var moveEastResponse = await _client.PostAsJsonAsync(EndpointMove, new
        {
            MatchId = _matchId.ToString(),
            ShipId = shipId.ToString(),
            Direction = "East"
        });

        moveEastResponse.StatusCode.Should().Be(HttpStatusCode.Conflict,
            "Navio vertical não pode mover para Leste ");
    }
    
    [Theory]
    [InlineData("Basic")]
    [InlineData("Intermediate")]
    [InlineData("Advanced")]
    public async Task HasMovedThisTurn_Deve_Resetar_Apos_Acertar_Um_Tiro(string dificulty)
    {
        await Passo_PrepararNovoJogoDinamico("user", "123456",dificulty);

        // 1. Move um navio → HasMovedThisTurn = true
        var redisState = await ObterEstadoDoRedisAsync();
        var navio = ObterNavioComMovimentoLivre(redisState.Boards.P1.Ships);
        navio.Should().NotBeNull("Deve haver pelo menos um navio com espaço para mover");
        var shipId = Guid.Parse(navio!.Id);
        var direction = ObterDirecaoSeguraParaNavio(navio, redisState.Boards.P1.Ships);

        var moveResp = await _client.PostAsJsonAsync(EndpointMove, new
        {
            MatchId = _matchId.ToString(),
            ShipId = shipId.ToString(),
            Direction = direction.ToString()
        });
        moveResp.StatusCode.Should().Be(HttpStatusCode.OK, "Movimento deve ser aceito");

        var afterMove = await ObterEstadoDoRedisAsync();
        afterMove.MovedThisTurn.Should().BeTrue("HasMovedThisTurn deve ser true após movimento");

        var coordenadasIA = ObterCoordenadasDaIA(redisState.Boards.P2.Ships);
        var alvo = coordenadasIA.First();

        var shotResponse = await _client.PostAsJsonAsync(EndpointShot, new
        {
            MatchId = _matchId.ToString(),
            X = alvo.X,
            Y = alvo.Y
        });
        shotResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Tiro deve ser aceito");

        var afterShot = await ObterEstadoDoRedisAsync();

        if (afterShot.Status == MatchStatusRedis.FINISHED)
        {
            _output.WriteLine("Partida encerrada antes de validar reset Teste ignorado.");
            return;
        }

        afterShot.MovedThisTurn.Should().BeFalse(
            "HasMovedThisTurn deve resetar para false após acertar, permitindo novo movimento no mesmo turno encadeado");
    }

    [Theory]
    [InlineData("Basic")]
    [InlineData("Intermediate")]
    [InlineData("Advanced")]    public async Task HasMovedThisTurn_Deve_Resetar_Apos_Troca_De_Turno_Por_Tiro_Na_Agua(string dificulty)
    {
        await Passo_PrepararNovoJogoDinamico("user", "123456",dificulty);

        var redisState = await ObterEstadoDoRedisAsync();

        var navio = ObterNavioComMovimentoLivre(redisState.Boards.P1.Ships);
        navio.Should().NotBeNull("Deve haver pelo menos um navio com espaço para mover");
        var shipId = Guid.Parse(navio!.Id);
        var direction = ObterDirecaoSeguraParaNavio(navio, redisState.Boards.P1.Ships);

        var moveResp = await _client.PostAsJsonAsync(EndpointMove, new
        {
            MatchId = _matchId.ToString(),
            ShipId = shipId.ToString(),
            Direction = direction.ToString()
        });
        moveResp.StatusCode.Should().Be(HttpStatusCode.OK, "Movimento deve ser aceito");

        var afterMove = await ObterEstadoDoRedisAsync();
        afterMove.MovedThisTurn.Should().BeTrue();

        var aguaCoord = ObterCoordenadaDeAgua(redisState.Boards.P2.Ships);
        var shotResponse = await _client.PostAsJsonAsync(EndpointShot, new
        {
            MatchId = _matchId.ToString(),
            X = aguaCoord.X,
            Y = aguaCoord.Y
        });
        
        shotResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Tiro na água deve ser aceito");

        var afterIaTurn = await ObterEstadoDoRedisAsync();

        if (afterIaTurn.Status == MatchStatusRedis.FINISHED)
        {
            _output.WriteLine("Partida encerrada pela IA. Teste de reset inconclusivo.");
            return;
        }

        afterIaTurn.MovedThisTurn.Should().BeFalse(
            "HasMovedThisTurn deve ser false quando o turno volta para o player após erro");
    }

    [Theory]
    [InlineData("Basic")]
    [InlineData("Intermediate")]
    [InlineData("Advanced")]
    public async Task Pode_Atirar_Sem_Mover_No_Modo_Dinamico(string dificulty)
    {
        await Passo_PrepararNovoJogoDinamico("User", "123456",dificulty);

        var redisState = await ObterEstadoDoRedisAsync();
        var alvo = ObterCoordenadasDaIA(redisState.Boards.P2.Ships).First();

        // Atira SEM mover antes
        var shotResponse = await _client.PostAsJsonAsync(EndpointShot, new
        {
            MatchId = _matchId.ToString(),
            X = alvo.X,
            Y = alvo.Y
        });

        shotResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "No modo Dynamic é permitido atirar sem mover o navio primeiro");
    }

    [Theory]
    [InlineData("Basic")]
    [InlineData("Intermediate")]
    [InlineData("Advanced")]
    public async Task Deve_Completar_Jornada_Completa_No_Modo_Dinamico_Vencendo_A_IA(string dificulty)
    {
        await Passo_PrepararNovoJogoDinamico("user", "123456",dificulty);

        var redisState = await ObterEstadoDoRedisAsync();
        var coordenadasIA = ObterCoordenadasDaIA(redisState.Boards.P2.Ships);

        int tiro = 0;
        foreach (var coord in coordenadasIA)
        {
            var stateAtual = await ObterEstadoDoRedisAsync();
            if (stateAtual.Status == MatchStatusRedis.FINISHED) break;

            // A cada 2 tiros, tenta mover um navio intacto antes de atirar
            if (tiro % 2 == 0)
            {
                var navioLivre = ObterNavioComMovimentoLivre(stateAtual.Boards.P1.Ships);
                if (navioLivre != null)
                {
                    var dir = ObterDirecaoSeguraParaNavio(navioLivre, stateAtual.Boards.P1.Ships);
                    var moveResp = await _client.PostAsJsonAsync(EndpointMove, new
                    {
                        MatchId = _matchId.ToString(),
                        ShipId = navioLivre.Id,
                        Direction = dir.ToString()
                    });
                    // casoi falhe
                    _output.WriteLine($"[Move tiro {tiro}] status={moveResp.StatusCode}");
                }
            }

            var shotResponse = await _client.PostAsJsonAsync(EndpointShot, new
            {
                MatchId = _matchId.ToString(),
                X = coord.X,
                Y = coord.Y
            });

            // Tiro pode ser rejeitado se posição já alvejada 
            if (shotResponse.StatusCode == HttpStatusCode.BadRequest
                || shotResponse.StatusCode == HttpStatusCode.Conflict)
            {
                _output.WriteLine($"[Tiro {tiro}] ({coord.X},{coord.Y}) já alvejada ou posição inválida, pulando.");
                tiro++;
                continue;
            }

            shotResponse.StatusCode.Should().Be(HttpStatusCode.OK,
                $"Tiro {tiro} em ({coord.X},{coord.Y}) deve ser aceito");
            tiro++;
        }

        var finalState = await ObterEstadoDoRedisAsync();
        finalState.Status.Should().Be(MatchStatusRedis.FINISHED,
            "A partida deve ter sido finalizada após derrubar todos os navios da IA");
    }

    [Theory]
    [InlineData("Basic")]
    [InlineData("Intermediate")]
    [InlineData("Advanced")]
    public async Task Deve_Validar_Move_Com_Payload_Invalido(string dificulty)
    {
        await Passo_PrepararNovoJogoDinamico("user", "123456",dificulty);

        var matchIdReal = _matchId.ToString();
        var shipIdFake = Guid.NewGuid().ToString();

        // Sem autenticação — deve retornar 401
        _client.DefaultRequestHeaders.Authorization = null;
        var unauthResponse = await _client.PostAsJsonAsync(EndpointMove, new
        {
            MatchId = matchIdReal,
            ShipId = shipIdFake,
            Direction = "North"
        });
        unauthResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Endpoint de move deve exigir autenticação");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _authInfoUsuario.AccessToken);
        var noShipResponse = await _client.PostAsJsonAsync(EndpointMove, new
        {
            MatchId = matchIdReal,
            ShipId = shipIdFake,
            Direction = "North"
        });
        noShipResponse.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "Navio inexistente deve retornar NotFound, 404");
    }

    //auxiliares para os testes

    private async Task Passo_CriarUsuarioEAutenticar(string username, string password)
    {
        var usuario = new { Username = username, Password = password };
        await _client.PostAsJsonAsync(EndpointUsers, usuario);

        var responseLogin = await _client.PostAsJsonAsync(EndpointLogin, usuario);
        responseLogin.StatusCode.Should().Be(HttpStatusCode.OK);

        _authInfoUsuario = (await responseLogin.Content.ReadFromJsonAsync<TokenResponseDto>())!;
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _authInfoUsuario.AccessToken);
    }

    private async Task Passo_PrepararNovoJogoDinamico(string username, string password, String dificulty)
    {
        await Passo_CriarUsuarioEAutenticar(username, password);

        var createResponse = await _client.PostAsJsonAsync(Endpoint, new
        {
            Mode = "Dynamic",
            AiDifficulty = dificulty
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created,
            "Deve criar partida no modo Dynamic");

        var matchResult = await createResponse.Content.ReadFromJsonAsync<MatchTests.RealMatch>();
        _matchId = matchResult!.MatchId;

        var setupResponse = await _client.PostAsJsonAsync(EndpointSetup, new
        {
            MatchId = _matchId.ToString(),
            Ships = GetDefaultFleet()
        });
        setupResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "Setup deve funcionar no modo Dynamic");
    }

    private async Task<MatchRedis> ObterEstadoDoRedisAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        var matchJson = await cache.GetStringAsync($"match:{_matchId}");
        matchJson.Should().NotBeNullOrEmpty("O estado da partida deve estar no Redis");

        return JsonSerializer.Deserialize<MatchRedis>(matchJson!)!;
    }

    private List<ShipPlacementDto> GetDefaultFleet()
    {
        return new List<ShipPlacementDto>
        {
            new("Porta-Aviões", 6, 0, 0, ShipOrientation.Horizontal), // (0,0)→(5,0)
            new("Porta-Aviões", 6, 0, 2, ShipOrientation.Horizontal), // (0,2)→(5,2)
            new("Destroyer", 4, 0, 4, ShipOrientation.Horizontal), // (0,4)→(3,4)
            new("Destroyer", 4, 0, 6, ShipOrientation.Horizontal), // (0,6)→(3,6)
            new("Encouraçado", 3, 0, 8, ShipOrientation.Horizontal), // (0,8)→(2,8)
            new("Submarino", 1, 5, 4, ShipOrientation.Horizontal) // (5,4)
        };
    }

    private MoveDirection ObterDirecaoSeguraParaNavio(ShipRedis navio, List<ShipRedis> todosOsNavios)
    {
        var celulasOcupadas = todosOsNavios
            .Where(s => s.Id != navio.Id)
            .SelectMany(s => s.Segments)
            .Select(seg => (seg.X, seg.Y))
            .ToHashSet();
        var direcoesPossiveis = new List<MoveDirection>();
        if (navio.Size == 1)
        {
            direcoesPossiveis.AddRange(new[]
                { MoveDirection.North, MoveDirection.South, MoveDirection.East, MoveDirection.West });
        }
        else if (navio.Orientation == ShipOrientationRedis.HORIZONTAL)
        {
            direcoesPossiveis.AddRange(new[] { MoveDirection.East, MoveDirection.West });
        }
        else
        {
            direcoesPossiveis.AddRange(new[] { MoveDirection.South, MoveDirection.North });
        }

        foreach (var dir in direcoesPossiveis)
        {
            var (dx, dy) = dir switch
            {
                MoveDirection.North => (0, -1),
                MoveDirection.South => (0, 1),
                MoveDirection.East => (1, 0),
                MoveDirection.West => (-1, 0),
                _ => (0, 0)
            };

            var novasCoords = navio.Segments.Select(s => (X: s.X + dx, Y: s.Y + dy)).ToList();

            // Valida limites do tabuleiro
            if (novasCoords.Any(c => c.X < 0 || c.X >= 10 || c.Y < 0 || c.Y >= 10))
                continue;

            // Valida colisão com outros navios
            if (novasCoords.Any(c => celulasOcupadas.Contains((c.X, c.Y))))
                continue;

            return dir;
        }

        return MoveDirection.East;
    }

    private ShipRedis? ObterNavioComMovimentoLivre(List<ShipRedis> navios)
    {
        foreach (var navio in navios.Where(s => !s.Sunk && !s.IsDamaged))
        {
            var celulasOcupadas = navios
                .Where(s => s.Id != navio.Id)
                .SelectMany(s => s.Segments)
                .Select(seg => (seg.X, seg.Y))
                .ToHashSet();

            var direcoesPossiveis = new List<MoveDirection>();
            if (navio.Size == 1)
                direcoesPossiveis.AddRange(new[]
                    { MoveDirection.North, MoveDirection.South, MoveDirection.East, MoveDirection.West });
            else if (navio.Orientation == ShipOrientationRedis.HORIZONTAL)
                direcoesPossiveis.AddRange(new[] { MoveDirection.East, MoveDirection.West });
            else
                direcoesPossiveis.AddRange(new[] { MoveDirection.South, MoveDirection.North });

            foreach (var dir in direcoesPossiveis)
            {
                var (dx, dy) = dir switch
                {
                    MoveDirection.North => (0, -1),
                    MoveDirection.South => (0, 1),
                    MoveDirection.East => (1, 0),
                    MoveDirection.West => (-1, 0),
                    _ => (0, 0)
                };

                var novasCoords = navio.Segments.Select(s => (X: s.X + dx, Y: s.Y + dy)).ToList();

                if (novasCoords.Any(c => c.X < 0 || c.X >= 10 || c.Y < 0 || c.Y >= 10))
                    continue;

                if (novasCoords.Any(c => celulasOcupadas.Contains((c.X, c.Y))))
                    continue;

                return navio; // Este navio tem pelo menos uma direção livre
            }
        }

        return null;
    }


    private List<(int X, int Y)> ObterCoordenadasDaIA(List<ShipRedis> naviosIA)
    {
        return naviosIA
            .SelectMany(s => s.Segments)
            .Select(seg => (seg.X, seg.Y))
            .ToList();
    }

    private (int X, int Y) ObterCoordenadaDeAgua(List<ShipRedis> naviosIA)
    {
        var ocupadas = naviosIA
            .SelectMany(s => s.Segments)
            .Select(seg => (seg.X, seg.Y))
            .ToHashSet();

        for (var x = 0; x < 10; x++)
        for (var y = 0; y < 10; y++)
            if (!ocupadas.Contains((x, y)))
                return (x, y);

        throw new InvalidOperationException("Nenhuma coordenada de água encontrada.");
    }

    /// <summary>
    /// Retorna a primeira coordenada de água que ainda não foi atirada.
    /// </summary>
    private (int X, int Y)? ObterCoordenadaDeAguaNaoAtirada(List<ShipRedis> naviosIA, HashSet<(int, int)> jaAtiradas)
    {
        var ocupadas = naviosIA
            .SelectMany(s => s.Segments)
            .Select(seg => (seg.X, seg.Y))
            .ToHashSet();

        for (var x = 0; x < 10; x++)
        for (var y = 0; y < 10; y++)
            if (!ocupadas.Contains((x, y)) && !jaAtiradas.Contains((x, y)))
                return (x, y);

        return null;
    }
}