# Batalha Naval - Back End
[Scalar API Reference](http://localhost:5205/scalar/v1)


# 🚢 Batalha Naval - PLP Project

Este repositório contém a implementação do clássico jogo **Batalha Naval**, desenvolvido como projeto prático para a disciplina de **Paradigmas de Linguagens de Programação** da **Universidade Federal do Agreste de Pernambuco (UFAPE)**.

O projeto vai além do jogo tradicional, implementando modos dinâmicos, diferentes níveis de inteligência artificial e um sistema robusto de persistência de dados.

## 📋 Sobre o Projeto

O objetivo é desenvolver uma versão funcional e competitiva do jogo, onde jogadores posicionam frotas em tabuleiros $10\times10$ e alternam turnos para derrubar os navios adversários. O sistema inclui mecânicas de *streak* (jogar novamente ao acertar), diferentes estratégias de IA e um modo de jogo dinâmico.

### 🚀 Funcionalidades Principais

* **Core do Jogo:**
    * Tabuleiro $10\times10$ com suporte a posicionamento horizontal/vertical.
    * Frota padrão: Porta-aviões (6 slots), Navios de Guerra (4 slots), Encouraçado (3 slots) e Submarino (1 slot).
    * Sistema de turnos com regra de repetição ao acertar um alvo.
    * Feedback visual de "Água", "Acerto" e "Afundado".

* **🤖 Modos de Inteligência Artificial (Campanha):**
    1.  **IA Básica:** Disparos totalmente aleatórios.
    2.  **IA Intermediária:** Estratégia de busca ao redor de acertos (*Hunt/Target*).
    3.  **IA Avançada:** Uso de heurísticas e mapas de probabilidade por célula.

* **⚡ Modo Dinâmico:**
    * Mecânica exclusiva onde o jogador pode mover um navio (uma casa) antes de realizar o disparo no turno.

* **🏆 Metajogo e Persistência:**
    * Sistema de Login e Perfis de Jogador.
    * Ranking Global (Leaderboard).
    * Sistema de Conquistas (Medalhas):
        * *Almirante:* Vencer sem perder navios.
        * *Capitão de Mar e Guerra:* Acertar 8 tiros seguidos.
        * *Capitão:* Acertar 7 tiros seguidos.
        * *Marinheiro:* Vencer dentro de um tempo limite.

## 🛠 Arquitetura e Tecnologias

A solução foi projetada utilizando uma arquitetura distribuída para separar regras de negócio, orquestração de dados e interface.

* **Core API (.NET / C#):** Responsável por toda a regra de negócio, validação de jogadas, lógica das IAs e gerenciamento de estado da partida.
* **BFF (Backend for Frontend) - JavaScript:** Camada intermediária para otimização da comunicação entre a interface e a API Core.
* **Banco de Dados (PostgreSQL):** Persistência relacional para perfis de usuários, históricos de partidas e estatísticas.
* **Frontend:** (Em definição).


## Autores 

* Nicolas Gabriel Vieira do Nascimento Gomes
* José Portela da Silva Neto
* Julio Antonio de Cerqueira Neto
*  --nome dos demais...