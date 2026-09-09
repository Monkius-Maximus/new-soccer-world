> **Source document — design intent.** Authored by the project owner. Preserved
> verbatim as the record of what the project set out to build.
>
> Highest authority of the three source documents — it overrides `01`. Accepted
> ADRs still override it. See [`CLAUDE.md`](../../CLAUDE.md) for the full order.

---

# Revisão de Arquitetura — SoccerDreamGame

> **Documento 2 de 2.** Prevalece sobre o documento 1 em caso de conflito.
>
> Resultado de uma revisão crítica que encontrou quatro pontos onde uma decisão conceitualmente correta ainda não havia sido levada até suas consequências técnicas.

---

## 1. Modelo de banco: template + snapshot de carreira

O termo "SQL é canônico" misturava duas coisas diferentes: o banco que descreve o mundo inicial e o banco que representa uma carreira em andamento.

```
FONTES DO PROJETO
      ├── migrations SQL
      ├── seeds
      ├── imports
      └── generators
              ↓
      world_template.db
        [ARTEFATO DE BUILD]
              │
        ┌─────┴─────┐
        ↓           ↓
 Quick Match    Nova Carreira
 read-only          │
                    ↓ COPY
              saves/{id}/world.db
                    ↓
              mundo mutável
```

### `world_template.db`

Produzido pelo build. Contém jogadores iniciais, clubes, cidades, competições, estádios, treinadores, atributos e relações iniciais.

**Nunca é editado à mão.** As fontes continuam sendo `sql/migrations/`, `sql/seeds/`, `data/source/`, `tools/import/`, `tools/generation/`. O `.db` é artefato compilado e não vai para o Git.

### Ao iniciar uma carreira

Cópia do template para `saves/{id}/world.db`. A partir daí, esse arquivo é a verdade daquela carreira. Aposentadorias, transferências, reformas de estádio — tudo vive ali.

**Não usar** base imutável + banco de deltas. Economizaria espaço ao custo de complexidade enorme em consultas, modding, migrations e debugging. Um banco de dados numéricos/textuais é pequeno; sprites, áudio e modelos não ficam dentro dele.

### Isso resolve atualizações do jogo

```
Game 1.2 → João no Clube A → você inicia carreira
Game 1.3 → Database Update → João no Clube B

NOVAS CARREIRAS   → world_template v1.3
CARREIRA EXISTENTE → seu world.db mantém sua própria história
```

Atualização só executa migration estrutural se o schema mudar. A continuidade da carreira é preservada.

### SaveMetadata

```
SaveId
GameVersion
SchemaVersion
ContentVersion
CreatedAt
LastPlayedAt
CareerSeed
ModList          # quais mods e versões geraram o template
```

### Memória vs. banco

Consultar SQLite tick a tick é inviável. O padrão é:

> **O modelo em memória é a autoridade durante a sessão. O banco é o limite de persistência, escrito em checkpoints definidos** (fim de partida, virada de dia, fim de rodada).

Um mundo futebolístico completo cabe folgadamente em memória.

Consequência de design a ser assumida deliberadamente: se o banco é mutado continuamente, **não existe "sair sem salvar"**. É a escolha do Football Manager e é defensável — mas precisa ser intencional.

---

## 2. SQLite e Godot

O Simulation Core é C#. Portanto o Godot não executa SQL.

```
              GODOT (Presentation)
                    ↓
          SoccerSim.Application
                    ↓
             SoccerSim.Core
                    ↑
        SoccerSim.Infrastructure
                    ↓
                 SQLite
```

Godot não pede `SELECT * FROM player`. Pede `GetTeamSquad(clubId)`, e a Application/Infrastructure resolve.

Godot 4 suporta pacotes NuGet em projetos C#, então `Microsoft.Data.Sqlite` pode ser usado na camada de Infrastructure sem depender de addon específico do Godot. A exportação em cada plataforma-alvo ainda precisa ser testada — confirmar na documentação oficial, não na memória.

**PostgreSQL está fora**: é cliente-servidor, exige daemon, porta e usuários. Não se distribui isso num jogo offline.

Ferramentas de inspeção: DB Browser for SQLite, `sqlite3` CLI.

---

## 3. Pipeline de sprites e uniformes

Renderizar `Blender → jogador de camisa vermelha → sprite vermelho` **mataria o editor de uniformes**.

O Blender gera **informação visual**, não a camisa final:

```
                   3D MASTER
                      │
             Rig + Animation
                      │
            ┌─────────┼─────────┐
        SHADING   REGION ID   UV MAP
            └─────────┼─────────┘
                      ↓
                SPRITE FRAME
                      │
              Runtime Shader ← KIT DATA
```

### KitDefinition

```
PrimaryColor / SecondaryColor / TertiaryColor
ShirtPattern / ShortsPattern / SockPattern
CollarVariant / SleeveVariant
Crest / Sponsor / Number / PlayerName
```

Com uma UV lookup map exportada pelo Blender, cada pixel do sprite sabe de qual parte da textura original do uniforme veio:

```
Rendered Sprite + UV Lookup + Material Mask + Shading
→ Runtime → Custom Kit Texture
```

Resultado: **sprite pré-renderizado + uniforme dinâmico**, sem renderizar todas as combinações.

> ⚠️ **Isto é um spike, não um contrato.** Riscos reais: UV em 8 bits dá 256 níveis (grosso para listras finas); filtragem bilinear atravessando costuras produz artefatos; num sprite de 80–120px o aliasing de padrão é severo.
>
> Testar com um frame, um jogador, uma camisa listrada. Se passar, vira contrato e ganha ADR. Se falhar, a alternativa é região + paleta com padrões desenhados por região.

### Limitação consciente do 1.0

| | 1.0 |
| --- | --- |
| Cores, listras, faixas, gradientes, padrões | ✅ |
| Escudo, patrocínio, nome, número | ✅ |
| Golas e mangas predefinidas | ✅ |
| Modelar uma camisa 3D nova | ❌ |

### Volume de assets

Não produzir `8 direções × 30 animações × 20 corpos × 15 peças × 500 jogadores`. Em vez disso:

```
RIGS              compartilhados
ANIMATIONS        compartilhadas
BODY ARCHETYPES   conjunto pequeno
KIT               shader/runtime
SKIN              palette/mask
HAIR              modular/layer
PLAYER IDENTITY   composição
```

Um jogador não ganha atlas exclusivo. Ele é **montado a partir do sistema**.

---

## 4. Simulação headless e determinismo

### Partida nasce headless

Não construir uma partida no Godot e depois tentar arrancar o Godot dela.

```
MatchSimulation
├── MatchState / MatchClock / BallState
├── PlayerState ×22 / TacticalState
├── DecisionSystem / ActionResolver / Rules

MatchSimulation
 ┌─────┴────────┐
Godot       HeadlessRunner
Visual      100% CPU
```

Isso vale desde `MATCH-00`.

### Timestep

**Não usar tick maior no headless.** Um timestep diferente altera o comportamento da simulação — eventos intermediários desaparecem:

```
tick 100ms:  A → B → C → D → E
tick 500ms:  A ───────────→ E
```

Regra: **mesmo timestep, sem renderer, o mais rápido que a CPU conseguir.**

### RNG determinístico

Nada de `Random.Shared` espalhado. `IRandomSource` com implementação de estado explícito (PCG ou xoshiro). Cada partida tem um `MatchSeed` e consome aquela sequência.

Proibido dentro do Core: `Random.Shared`, `DateTime.Now`, `Guid.NewGuid()`, paralelismo dentro do loop.

Também quebram determinismo, e precisam de atenção:
- ordem de iteração de `Dictionary` / `HashSet` (não garantida em .NET)
- ponto flutuante entre plataformas (x86 vs ARM, diferenças de JIT, `Math.Sin`/`Math.Pow` não são bit-exatos entre runtimes)

**Escopo da garantia:** mesmo binário + mesma plataforma = reprodução exata. Determinismo cross-platform exigiria matemática de ponto fixo — custo alto para benefício que o projeto não precisa.

**Corolário arquitetural:** partidas não compartilham estado mutável. Com `IRandomSource` e estado próprios por partida, simular uma rodada inteira em paralelo entre cores continua determinístico. É de graça se decidido agora, caro se descoberto depois.

### Por que isso importa

Sem determinismo, "quando um lateral com overlapping joga contra um winger invertido acontece um bug" é irreproduzível. Com determinismo:

```
save-test-932 · match seed 557921 · tick 13842
```

Executa de novo. O bug volta.

---

## 5. Fidelidade de simulação

Não criar uma segunda engine estatística antes de precisar. Duas regras diferentes de futebol é exatamente o problema que a arquitetura tenta evitar.

```
        IMatchSimulator
       ┌───────┴───────┐
DetailedMatch      FastMatch
Simulation         Simulation
       └───────┬───────┘
          MatchResult
```

Estado inicial: `DetailedMatchSimulation` ✅ · `FastMatchSimulation` inexistente.

**Critério de benchmark deve ser definido antes de medir**, senão a medição não significa nada. Formato: *avançar uma rodada completa deve custar menos de X segundos em thread única.*

Calibração: 90 minutos a 100ms = 54.000 ticks por partida. A variável sob controle é o número de competições simuladas em detalhe, não a engine.

---

## 6. Escopo revisado do 1.0

| Sistema | 1.0 |
| ------- | --- |
| Partida rápida | ✅ |
| Liga | ✅ |
| Copa | ✅ |
| Carreira de Clube | ✅ |
| Táticas | ✅ |
| Transferências | ✅ |
| Contratos | ✅ |
| Progressão / envelhecimento | ✅ |
| Scouting básico | ✅ |
| Treino básico | ✅ |
| Editor de jogador | ✅ |
| Editor de clube | ✅ |
| Uniformes dentro do Club Editor | ✅ |
| Escudos dentro do Club Editor | ✅ |
| Modding / data import básico | 🟡 |
| Editor completo de estádio | ❌ |
| Editor completo de competições | ❌ |
| Life World | ❌ |
| Cidade explorável | ❌ |
| Smartphone social | ❌ |
| Família / relacionamentos | ❌ |
| Player Career | ❌ |
| Online | ❌ |

O Life World não foi abandonado:

```
1.0 Football Game → 1.x Expanded Career → 2.0 Life World → Player Career
```

Quando `WORLD/LIFE` chegar, encontrará jogadores, clubes, contratos, calendários, partidas, cidades, reputação e economia já existentes. Ele **expande um mundo funcional**, não sustenta o jogo.

---

## 7. Ponto de partida

Nem modelar o banco inteiro, nem partida com dados hardcoded. **Vertical slice orientado por schema mínimo.**

```
ETAPA 0 — Architecture Foundation
  Minimal Player Model / Minimal Club Model / Minimal Database
  Deterministic RNG / Fixed timestep / Save architecture
        ↓
ETAPA 1 — Headless Match
        ↓
ETAPA 2 — Visual Match
```

Nada de `var player = new Player("Joao", 90);` em vinte lugares para depois substituir por banco.

### Primeiro dataset

```
COUNTRY      Brasil
CITY         Recife
CLUB         Recife Azul, Recife Vermelho
PLAYER       22 + reservas
STADIUM      Estádio Teste
COMPETITION  Amistoso
```

Pequeno, mas vindo do caminho real:

```
migration → seed → world_template.db → repository → application → simulation → Godot
```

---

## 8. Launcher externo

O launcher **é** a ferramenta que constrói o `world_template.db`.

```
migrations → seed base → mods (ordenados) → validação → world_template.db → jogo
```

Um mod é uma pasta com `manifest.json` + scripts `.sql` aplicados em sequência sobre a base. O launcher escolhe quais mods entram e em que ordem.

O `SaveMetadata` grava quais mods e versões geraram aquele template, então uma carreira sabe de onde veio e não quebra silenciosamente quando a lista de mods mudar.

Começa como CLI (é o passo 0.6 do roadmap) e ganha interface depois.

---

## 9. ADRs

Somente cinco, as que realmente carregam peso. Escrever ADR antes de a decisão existir transforma metade delas em roadmap disfarçado e destrói a autoridade que elas precisam ter contra agentes futuros.

```
ADR-0001-monorepo.md
ADR-0002-simulation-core-engine-agnostic.md
ADR-0003-base-world-and-career-save-model.md
ADR-0004-fixed-timestep-and-deterministic-rng.md
ADR-0005-scope-of-version-1.0.md
```

---

## 10. Decisões pendentes

Não podem ser inventadas por um agente:

| # | Decisão | Recomendação |
| - | ------- | ------------ |
| A | Plataformas-alvo do 1.0 | Windows + Linux desktop |
| B | Versão do .NET e do Godot | Confirmar matriz de compatibilidade na documentação oficial |
| C | Formato de mod | `manifest.json` + `.sql` ordenados |
| D | Critério de benchmark | Definir o número antes de medir |

---

## 11. Desenho final

```
                    SoccerDreamGame
                         CORE
               deterministic simulation
          ┌───────────────┼───────────────┐
       MATCH           FOOTBALL          SAVE
          │              WORLD            │
       GODOT           CAREER         world.db
          │               │
      Renderer       Competition
          │
    Dynamic Sprites + Dynamic Kits

              POST-1.0 EXPANSION
              ┌───────┴───────┐
            WORLD            LIFE
                              ↓
                       PLAYER CAREER
```
