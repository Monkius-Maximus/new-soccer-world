> **Source document — design intent.** Authored by the project owner. Preserved
> verbatim as the record of what the project set out to build.
>
> Lowest authority of the three source documents. See
> [`CLAUDE.md`](../../CLAUDE.md) for the full authority order.

---

# Estrutura Canônica — SoccerDreamGame

> **Documento 1 de 2.** Registro da arquitetura conceitual do jogo.
>
> ⚠️ **Seções parcialmente substituídas** pelo documento `02-revisao-arquitetura.md`:
> - Seção 20 (persistência) → substituída pelo modelo `world_template.db` + save
> - Seção 23 (escopo 1.0) → substituída pelo escopo revisado
> - Seção 27 (ordem de desenvolvimento) → substituída pelo ROADMAP.md
>
> Em caso de conflito, o documento 2 e o ROADMAP prevalecem.

---

## 1. Identidade do jogo

Três camadas visíveis:

| Camada | Função | Referência |
| ------ | ------ | ---------- |
| ⚽ MATCH | Jogar e simular futebol | PES / FIFA |
| 🏆 FOOTBALL WORLD | Temporadas, clubes, táticas, mercado, competições | Master League / FM |
| 🌍 LIFE WORLD | Cidade, personagem, relações, rotina, bastidores | The Sims / RPG |

E uma fundação invisível:

| Fundação | Função |
| -------- | ------ |
| 🧠 SIMULATION CORE | Jogadores, atributos, IA, tempo, economia, banco, regras, seeds, persistência |

Regra arquitetural:

```
LIFE WORLD → FOOTBALL WORLD → MATCH
        todos consultam
        SIMULATION CORE
```

Uma partida rápida não carrega cidade, agentes, família ou patrocinadores.

**Decisão central:** o mundo aberto não é outro jogo acoplado ao futebol. Ele é uma **interface espacial para os sistemas de carreira**.

---

## 2. Menu principal

**⚽ JOGAR** — Partida rápida (clube vs clube, seleção vs seleção). Laboratório de gameplay; precisa existir cedo.

**🏆 COMPETIÇÕES** — Liga, copa, torneio personalizado. Sem carreira ou mercado obrigatórios.

**💼 CARREIRA** — Carreira de Clube primeiro. Carreira de Jogador depois. Mesmo mundo persistente, engines não duplicadas.

**🏃 TREINAMENTO** — Campo livre, pênaltis, faltas, escanteios, situações ofensivas/defensivas, tutorial, teste de jogadores. Também é ferramenta interna de teste.

**🛠️ CENTRAL DE CRIAÇÃO** — Jogadores, clubes, uniformes, escudos, estádios, competições, treinadores. Cidades e mundo depois.

**⚙️ CONFIGURAÇÕES** — Gameplay, controles, vídeo, áudio, acessibilidade, regras, banco de dados, créditos.

---

## 3. Simulation Core

O Godot não decide quem é bom jogador, quem se desmarca ou quem deve ser transferido. O jogo visual é **consumidor da simulação**.

### CORE-01 — Football Database

Fonte canônica de: continentes, países, regiões, cidades, competições, clubes, estádios, funcionários, treinadores, jogadores, árbitros, agentes, patrocinadores, relacionamentos.

JSON e CSV podem existir como formatos de intercâmbio e ferramentas de geração, nunca como segunda fonte da verdade.

---

## 4. Player Model

Um jogador não é `OVR = 84`.

```
PLAYER
│
├── Identity
│   ├── Name / Age / Nationality / Appearance / Biography
│
├── Football Attributes
│   ├── Technical / Mental / Physical / Goalkeeping
│
├── Position
│   ├── Primary / Secondary / Positional Familiarity
│
├── Archetype
├── Traits / Playstyles
├── Tactical Roles
│
├── Physical State
│   ├── Fitness / Fatigue / Injuries / Form
│
├── Development
│   ├── Potential / Development Curve / Aging
│
├── Personality
├── Reputation
└── Career History
```

**Posição ≠ função tática ≠ arquétipo ≠ trait.**

Exemplo: um ponta pode ser `RW`, com função `Inverted Winger`, arquétipo `Dribbler`, traits `Flair / Quick Step / Outside Foot`.

---

## 5. Overall e Quality Bands

OVR não manda na simulação. É interpretação para UI e scouting.

```
Attributes → Position weighting → Context → Derived Overall → Quality Band
```

Dois jogadores OVR 80 podem jogar de maneiras completamente diferentes. As bandas (Bronze/Prata/Ouro/Elite) são classificação visual, não classes internas rígidas.

---

## 6–8. Match Engine

**Sem tabuleiro visual.** Movimento contínuo em coordenadas 2D + campo dividido logicamente em zonas.

```
VISUAL                    SIMULAÇÃO

  jogador                 Zone 14
     ●                    Half Space L
    ↙↓↘                   Central Channel
                          Defensive Third
                          Pressure = 0.72
                          Passing Risk = 0.38
```

O usuário nunca vê as células. A IA vê.

### MATCH-01 — Spatial Simulation

Coordenadas normalizadas: `X = 0.00 → 1.00`, `Y = 0.00 → 1.00`.

Delas derivamos: corredor, terço do campo, half-space, zona de pressão, distância, cobertura, linhas de passe, influência, espaço disponível. Sem prisão a um grid específico.

### MATCH-02 — Decision Engine

```
Observe → Evaluate → Select intention → Move/Pass/Shoot/Press/Mark
       → Resolve action → Update match state
```

Atributos e traits alteram probabilidades **e escolhas**. `Dribbling + Flair + Agility` não é só `+15% de chance de drible` — faz a IA considerar situações que outro jogador evitaria. Isso é fundamental para jogadores parecerem diferentes.

---

## 9. MATCH-03 — Táticas

Formação **com posse** e formação **sem posse**, separadas. Representa melhor o futebol moderno que uma formação rígida.

Depois: largura, altura da linha, ritmo, pressão, compactação, construção, transição, marcação, funções individuais.

---

## 10. MATCH-04 — Rendering

**Runtime:** 2D / 2.5D isométrico. Não 3D.

**Pipeline artístico:** 3D → sprites 2D.

```
Character 3D Master → Animation → Lighting → Render directions
→ Sprite Master → LOD → Field Sprite / Replay Sprite / Portrait / UI Render
```

> ⚠️ Este pipeline foi revisado no documento 2. O sprite master **não** contém o uniforme final.

A simulação não conhece o renderer:

```
MatchSimulation → Match State → [2D Renderer | 3D Renderer]
```

---

## 11. FOOTBALL WORLD

| ID | Sistema |
| -- | ------- |
| FW-01 | Calendário |
| FW-02 | Competições |
| FW-03 | Clubes |
| FW-04 | Elencos |
| FW-05 | Mercado |
| FW-06 | Contratos |
| FW-07 | Scouting |
| FW-08 | Finanças |
| FW-09 | Staff |
| FW-10 | Desenvolvimento |
| FW-11 | Lesões e condicionamento |
| FW-12 | Reputação |
| FW-13 | Notícias |
| FW-14 | Rivalidades |
| FW-15 | História do mundo |

---

## 12. Club Model

```
CLUB
├── Identity / City / Stadium / Colors / Crest / Kits
├── Squad / Academy / Staff
├── Tactical Identity
├── Financial Profile
├── Reputation / Fanbase / Rivalries / History
└── Cultural DNA
```

O sistema DNA → Ficção não é ferramenta lateral. É parte da Content Generation Layer.

---

## 13. Competition Engine

Regras combináveis, não uma classe por campeonato.

```
Competition
├── Participants / Groups / Rounds / Scheduling
├── Points Rules / Tiebreak Rules
└── Qualification / Relegation / Registration Rules
```

Serve Brasileirão, copa, formato Champions, estaduais, torneios fictícios e ligas personalizadas com a mesma engine.

---

## 14. Carreira de Clube

Primeira carreira completa. Não desenvolver carreira de jogador em paralelo.

```
Inbox/World → Squad Management → Training → Scouting/Transfers
→ Tactical Preparation → Match → Results → Finances/Development → Next Day
```

---

## 15–17. LIFE WORLD

**Não haverá mapa mundial aberto contínuo.** Seria um poço de desenvolvimento.

Em vez disso: cidades → bairros → locais.

```
WORLD
└── Brazil
    └── Pernambuco
        └── Recife
            ├── Home
            ├── Club HQ
            ├── Training Ground
            ├── Stadium
            ├── Downtown
            ├── Commercial Area
            └── Airport
```

Cada local é uma cena pequena ou uma interface. O modelo de dados não muda quando isso crescer.

**Função de cada local:** Training Ground (treinar, conversar, fisioterapia) · Stadium (partidas, eventos, museu, torcida) · Home (descanso, customização, família, smartphone) · Commercial District (compras, patrocínio, barbearia, roupas) · Club HQ (contratos, diretoria, imprensa, transferências).

O mundo não é cenário. É **UI espacial**.

### Smartphone

```
PHONE
├── Messages / Social / Calendar / Contacts
├── Club / News / Agent / Sponsors
└── Shopping / Travel
```

Evita empurrar todos os sistemas para o HUD.

---

## 18. Player Career

Só depois da simulação de clube funcionar. Muda-se apenas **quem o jogador controla**:

- Carreira de clube: você controla a instituição.
- Carreira de jogador: você controla uma pessoa dentro da mesma instituição.

Mesmo mundo. Evita construir dois jogos.

---

## 19. Creation Center

Estrutural, não "bom de ter". Sem licenciamento amplo, o jogador precisa pensar *"posso criar qualquer universo futebolístico"* em vez de *"esse jogo não tem o Manchester United"*.

| Editor | Prioridade |
| ------ | ---------: |
| Jogador | Alta |
| Clube | Alta |
| Uniforme | Alta |
| Escudo | Alta |
| Competição | Alta |
| Treinador | Média |
| Estádio | Média |
| Torcida | Média |
| Cidade | Futura |
| Mundo | Futura |

---

## 20. Content Generation

```
SOURCE DATA → Normalization → Football DNA → Fiction Generator
→ Validation → SQL → GAME DATABASE
```

Exemplo: clube tipo Manchester → extração de DNA (cidade industrial do norte, identidade vermelha, torcida global, potência histórica) → seed → clube fictício.

Sem dependência de JSON durante o jogo.

---

## 21. Arquitetura técnica macro

```
SoccerDreamGame/
├── game/          # presentation, ui, match, world, scenes
├── core/          # football, match, tactics, competitions, career, world, simulation
├── sql/           # migrations, seeds
├── tools/         # import, generation, validation, exporters
├── data/          # definitions, source
├── assets/        # characters, kits, stadiums, clubs, world, ui
├── tests/
└── docs/
```

Arquitetura-alvo, não migração imediata.

---

## 22. Módulos canônicos

| Família | Escopo |
| ------- | ------ |
| CORE | dados, tempo, eventos, regras, saves |
| PLYR | jogador, atributos, posições, arquétipos, desenvolvimento |
| MATCH | física lógica, bola, ações, IA, árbitro, controles, câmera |
| TACT | formação, roles, instruções, transições |
| COMP | ligas, copas, calendário, classificação |
| CLUB | clubes, elenco, staff, instalações, cultura |
| CAREER | carreira, temporadas, objetivos, progressão |
| MARKET | scouting, transferências, contratos, agentes |
| WORLD | países, regiões, cidades, locais, viagens |
| LIFE | necessidades, relacionamentos, rotina, smartphone |
| CREATE | editores e conteúdo customizado |
| CONTENT | DNA→Ficção, seeds, geração procedural |
| ART | personagens, sprites, kits, estádios, animação |
| UI | HUD, menus, apresentação |
| AUDIO | torcida, ambiente, efeitos, música |
| TOOLS | importadores, validadores, ferramentas internas |

Não inventar categoria principal nova sem motivo forte.

---

## 25. Fora de escopo (permanente)

🔴 Online competitivo no núcleo · 🔴 Crossplay · 🔴 Modo tipo FUT/Dream Team · 🔴 Licenças como requisito · 🔴 Mundo aberto global contínuo · 🔴 Engine de partida 3D paralela no lançamento · 🔴 Dezenas de modos independentes

---

## 26. O produto resultante

```
                    SOCCER DREAM GAME
                         ┌───────┐
                         │ WORLD │
                         └───┬───┘
                           LIFE
                             │
                          CAREER
                             │
            ┌────────────────┼────────────────┐
         MARKET           CLUB            SEASON
            └────────────────┼────────────────┘
                          TACTICS
                             │
                           MATCH
                             │
             ┌───────────────┴───────────────┐
        SIMULATION                       RENDERER
     Attributes / AI                  2D Isometric
     Roles / Traits                      Sprites
     Archetypes                        Animation
```

---

## 28. Especificação definitiva

> **SoccerDreamGame é um simulador de ecossistema futebolístico offline-first, centrado em partidas 2D/isométricas sistêmicas, competições e carreira de clube, com forte criação de conteúdo e um mundo persistente que posteriormente suporta uma experiência de vida e carreira individual.**

O diferencial não é polígono, licença ou online. É **um mundo futebolístico profundamente simulável, editável e expansível, no qual o mesmo jogador existe simultaneamente como atleta dentro da partida, ativo de um clube, personalidade pública e habitante de uma cidade.**

Este é o esqueleto. Não redesenhar o jogo inteiro a cada conversa.
