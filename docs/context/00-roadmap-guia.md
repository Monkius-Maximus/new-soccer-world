> **Source document — design intent.** Authored by the project owner. Preserved
> verbatim as the record of what the project set out to build.
>
> This is **not** the implementation-status roadmap. That is
> [`docs/ROADMAP.md`](../ROADMAP.md), and it is the only place statuses are
> updated. See [`CLAUDE.md`](../../CLAUDE.md) for the authority order.

---

# SoccerDreamGame — Roadmap

Documento-guia do projeto. Serve para distinguir o que está **decidido**, **planejado**, **implementado** e **testado**, e para impedir que decisões fechadas sejam reabertas por engano — por você ou por um agente.

---

## Semântica de status

| Símbolo | Significado |
| ------- | ----------- |
| 🔴 | Descartado / fora de escopo |
| 🟠 | Iniciado |
| 🟡 | Em progresso / requer atenção |
| 🟢 | Concluído |
| 🔵 | Módulo consolidado (não mexer sem ADR) |

Regra: nada é 🟢 sem teste. Nada é 🔵 sem ADR.

---

## 1. Decisões fechadas

Estas não são reabertas sem uma nova ADR que substitua a anterior explicitamente.

| # | Decisão | ADR |
| - | ------- | --- |
| 1 | Monorepo único | ADR-0001 |
| 2 | Simulation Core não conhece o renderer nem o Godot | ADR-0002 |
| 3 | `world_template.db` é artefato compilado; carreira é cópia mutável | ADR-0003 |
| 4 | Determinismo com escopo: mesmo binário + mesma plataforma | ADR-0004 |
| 5 | Escopo do 1.0 congelado (seção 6 deste documento) | ADR-0005 |
| 6 | Zonas lógicas derivadas de coordenadas contínuas; sem tabuleiro visual | — |
| 7 | OVR é saída da simulação, nunca entrada | — |
| 8 | Posição ≠ role tático ≠ arquétipo ≠ trait | — |
| 9 | Simulação roda sobre modelo em memória; banco é escrito em checkpoints | ADR-0003 |
| 10 | Partida nasce headless; renderer é consumidor | ADR-0002 |

---

## 2. Decisões pendentes — bloqueiam a Fase 0

Estas são suas. Um agente não pode inventá-las.

| # | Decisão | Recomendação |
| - | ------- | ------------ |
| A | Plataformas-alvo do 1.0 | Windows + Linux desktop |
| B | Versão do .NET e do Godot | Confirmar a matriz de compatibilidade na documentação oficial do Godot antes de fixar |
| C | Formato de mod | Pasta com `manifest.json` + scripts `.sql` ordenados |
| D | Critério de benchmark | Definir o número antes de medir (ver Trilha B) |

---

## 3. Fase 0 — FOUNDATION

Objetivo: provar o caminho completo `migration → seed → template → save → repository → application → simulation` sem uma única linha de futebol de verdade.

| Passo | Entregável | Status |
| ----- | ---------- | ------ |
| 0.1 | Repositório privado vazio criado (manual) | ⬜ |
| 0.2 | Bootstrap do monorepo, solution e projetos | ⬜ |
| 0.3 | Fronteiras de dependência + teste que falha na violação | ⬜ |
| 0.4 | Primitivas de determinismo (`IRandomSource`, timestep, proibições) | ⬜ |
| 0.5 | Migrations + seed mínimo | ⬜ |
| 0.6 | Build do `world_template.db` | ⬜ |
| 0.7 | Arquitetura de save + `SaveMetadata` | ⬜ |
| 0.8 | Repositórios: carga em memória + escrita em checkpoint | ⬜ |
| 0.9 | Headless runner ponta a ponta | ⬜ |
| 0.10 | Projeto Godot + smoke test C# | ⬜ |
| 0.11 | CI (build + testes) | ⬜ |
| 0.12 | 5 ADRs + `ARCHITECTURE.md` + este `ROADMAP.md` | ⬜ |

**Ordem é obrigatória.** O passo 0.4 vem antes de qualquer lógica de jogo porque determinismo é retroativamente caríssimo.

### Dataset mínimo do passo 0.5

```
COUNTRY      Brasil
CITY         Recife
CLUB         Recife Azul, Recife Vermelho
PLAYER       ~30
STADIUM      Estádio Teste
COMPETITION  Amistoso
```

Pequeno de propósito. O objetivo é validar o caminho, não o conteúdo.

---

## 4. Fases seguintes

| Fase | Módulo | Objetivo | Critério de conclusão |
| ---- | ------ | -------- | --------------------- |
| 1 | `PLYR-00` | Modelo de jogador completo no schema | Atributos, posições, arquétipos e traits vindos do banco |
| 2 | `MATCH-00` | Loop de partida headless | 22 agentes, bola, tempo, sem IA de verdade |
| 3 | `MATCH-01` | Decision engine | Observe → Evaluate → Act → Resolve |
| 4 | `MATCH-02` | Renderer 2D/isométrico | Mesma simulação, agora visível |
| 5 | `TACT-00` | Formação com/sem posse + instruções | Duas táticas distintas produzem jogos distintos |
| 6 | `COMP-00` | Competition engine por regras combináveis | Liga e copa com a mesma engine |
| 7 | `CLUB-00` | Elenco, staff, finanças | Clube é mais que uma lista de jogadores |
| 8 | `MARKET-00` | Transferências, contratos, scouting | Mercado fecha uma janela sozinho |
| 9 | `CAREER-00` | Temporadas persistentes | Uma carreira sobrevive a 5 temporadas sem corromper o save |
| 10 | `CREATE-00` | Editor de jogador e clube | Conteúdo criado pelo usuário entra no mundo |

### Milestone crítico

Ao final da Fase 5:

> 11 jogadores fictícios contra outros 11, em um estádio fictício, partida completa, comandados por atributos e táticas vindos do banco.

Quando isso funcionar, existe um videogame de futebol. Tudo depois é construção sobre ele.

---

## 5. Trilhas paralelas

Não bloqueiam a Fase 0 e não devem ser misturadas à sequência principal.

### Trilha A — Spike do kit shader

Não é contrato, é experimento. Um frame, um jogador, uma camisa listrada.

Pergunta a responder: o UV lookup sobrevive à resolução real do sprite (80–120px de altura)?

- Se sim → vira contrato de pipeline e ganha ADR.
- Se não → alternativa é região + paleta com padrões desenhados por região.

Decidir **depois** do teste. Não antes.

### Trilha B — Critério de benchmark

Definir o número antes de medir, senão a medição não significa nada.

Formato: *avançar uma rodada completa deve custar menos de X segundos em thread única.*

Calibração: 90 minutos a 100ms = 54.000 ticks por partida. A variável que você controla é o número de competições simuladas em detalhe, não a engine.

`FastMatchSimulation` só existe se o benchmark provar necessidade.

### Trilha C — Launcher externo

Ver `ARCHITECTURE.md`. Pode começar como CLI na Fase 0 (é literalmente o passo 0.6) e ganhar UI depois.

---

## 6. Escopo do 1.0

| Sistema | 1.0 |
| ------- | --- |
| Partida rápida | ✅ |
| Liga | ✅ |
| Copa | ✅ |
| Carreira de Clube | ✅ |
| Táticas (com/sem posse) | ✅ |
| Transferências e contratos | ✅ |
| Progressão e envelhecimento | ✅ |
| Scouting básico | ✅ |
| Treino básico | ✅ |
| Editor de jogador | ✅ |
| Editor de clube (uniforme + escudo dentro) | ✅ |
| Launcher com seleção de mods | 🟡 |
| Editor completo de estádio | 🔴 |
| Editor completo de competições | 🔴 |
| Life World / cidade explorável | 🔴 |
| Smartphone / relacionamentos | 🔴 |
| Player Career | 🔴 |
| Online / crossplay / modo tipo FUT | 🔴 |
| Licenciamento | 🔴 |
| Engine 3D paralela | 🔴 |

O Life World não foi abandonado. Ele é pós-1.0 e vai encontrar um mundo já funcional quando chegar.

---

## 7. Como usar este documento

1. Antes de abrir uma conversa com um agente, diga qual fase e qual módulo.
2. Se um agente propuser algo que contradiz a seção 1, ele está errado — ou precisa escrever uma ADR que substitua a anterior.
3. Atualize os status a cada milestone. Um roadmap desatualizado é pior que nenhum.
