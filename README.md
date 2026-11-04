# ⚽ Craquez

Jogo de futebol em 3D com visual desenhado (cel-shading), jogabilidade no estilo EA FC / eFootball, para **Windows** e **Android**.
Modos **Campo (11×11)**, **Futsal (5×5)** e **Street (3×3 com tabela no muro)**.
PvP local, contra CPU e **PvP online** (por IP). Suporte a controles **Xbox / PlayStation**, teclado e toque.

![Faces vetoriais geradas pelo Craquez](docs/img/faces_sheet.png)

## Destaques

| | |
|---|---|
| **Visual** | 3D com sombreamento "desenhado" e contorno; atletas com esqueleto e músculos, rosto desenhado; estádio noturno com refletores, árbitro e assistentes |
| **Ligas 2026/27** | Série A Itália, Premier League, La Liga, Ligue 1, Série A e B do Brasil, MLS e Liga Argentina — 178 clubes com a composição real da temporada e as cores reais (nomes alterados, sem licença) + 5 ligas de futsal (Brasil, Espanha, Portugal, Itália, Argentina). Edite em `data/leagues.json` |
| **Uniformes** | 3 por time (principal, reserva, terceiro) em padrões originais; escolha automática evita cores parecidas |
| **Táticas** | Formação, mentalidade, pressão e escolha de titulares — tudo afeta a IA e a escalação |
| **Locais** | Estádios em 3 portes, 8 quadras de futsal fechadas com tela e 8 cenários de rua |
| **Clima** | Sol, nublado, chuva, tempestade, neve; dia, entardecer, noite; clima dinâmico |
| **Cerimônia** | Sobrevoo do estádio, entrada dos times e da arbitragem, apresentação das escalações |
| **Option file** | Personalize nomes, uniformes, elencos e escudos (PNG) no seu aparelho — veja [docs/OPTION_FILE.md](docs/OPTION_FILE.md) |
| **Jogabilidade** | Dribles no analógico direito, proteção/contenção (LT/L2), chute colocado (RB/R1), armação do chute, domínio orientado |
| **Física** | Bola 2.5D com gravidade, arrasto do ar, efeito Magnus (curva), quique por superfície (grama/madeira/asfalto), atrito de rolagem, traves e travessão |
| **Movimento** | Aceleração/frenagem, inércia em mudanças de direção, giro mais lento em velocidade, fôlego (stamina), condução de bola com toques |
| **Hardware modesto** | Renderer *GL Compatibility* (OpenGL 3.3 / GLES 3). Presets Baixa (854×480, 30 fps) → Ultra (nativo, MSAA 4x, 144 fps) e detecção automática |
| **Online** | Lockstep sobre ENet/UDP com entradas de 3 bytes, redundância e hash anti-dessincronia. Ver [docs/ONLINE.md](docs/ONLINE.md) |
| **Som** | Sintetizado em código: chute, apitos, trave, torcida que reage ao lance |
| **Regras** | Faltas, pênalti (campo/futsal), impedimento (campo), lateral, escanteio, tiro de meta |

## Como rodar

1. Instale o [Godot 4.4](https://godotengine.org/download) (versão padrão, não a .NET).
2. Abra o Godot → **Importar** → selecione `project.godot` desta pasta.
3. Aperte **F5**.

Builds automáticas: a cada push, o GitHub Actions roda os testes e gera o `.exe` (Windows) e um `.apk` de debug (Android) — baixe em **Actions → execução → Artifacts**.

## Controles

| Ação | Xbox | PlayStation | Teclado P1 | Teclado P2 |
|---|---|---|---|---|
| Mover | Analógico esq. / D-pad | Analógico esq. / D-pad | WASD | Setas |
| Passe (defesa: dividida) | A | ✕ | J | Num 1 |
| Chute — segure p/ força (defesa: carrinho) | B | ◯ | K | Num 2 |
| Passe alto / cruzamento | X | ▢ | L | Num 3 |
| Enfiada (defesa: chamar 2º marcador) | Y | △ | I | Num 5 |
| Correr | RT | R2 | Shift | Num 0 |
| Proteger bola / conter (defesa) | LT | L2 | C | Num 7 |
| Chute colocado (com curva) | RB | R1 | U | Num 9 |
| Dribles | Analógico direito | Analógico direito | E + direção | Num 6 + direção |
| Trocar jogador | LB | L1 | Q | Num 4 |
| Pausa | Start | Options | P / Esc | Backspace |

No Android aparece um controle virtual na tela; controles Bluetooth também funcionam.
Dica: soltar o botão de passe/chute *antes* da bola chegar faz a jogada **de primeira**.

## Estrutura

```
scripts/
  sim/        simulação pura (sem engine): partida, bola, jogador, CPU
  core/       modos de jogo, times, InputFrame, leitura de controles, RNG determinístico
  view/       renderização 2.5D, HUD, controle de toque
  match/      nó da partida (passo fixo, provedores de entrada)
  net/        contrato de rede, replay, hash de estado
  ui/         menu principal
  art/        biblioteca de rostos (SVG → textura na resolução do preset)
  autoload/   Settings (qualidade, tempo de jogo…)
assets/faces/ 48 rostos SVG + metadados
tools/        face_gen.py (gera rostos novos)
tests/        testes headless da simulação
docs/         arquitetura, online, roadmap
```

Mais detalhes: [docs/ARQUITETURA.md](docs/ARQUITETURA.md) · [docs/ROADMAP.md](docs/ROADMAP.md)

## Rostos

```bash
python3 tools/face_gen.py --count 96 --seed 7
```

Gera novos rostos procedurais (tom de pele, cabelo, barba, olhos, formato do rosto). São placeholders de qualidade — o pipeline aceita rostos desenhados à mão em SVG no mesmo formato (512×512).

## Ligas e times (2026/27)

`data/leagues.json` traz as 8 ligas com a composição real de clubes da temporada 2026/27
(promovidos e rebaixados incluídos) e as cores reais dos uniformes. Os **nomes dos clubes
são alterados** e os **jogadores são fictícios** — nomes, escudos e rostos de clubes e
atletas reais são protegidos e exigem licença. Assim como nos "option files" do PES, você
pode editar o arquivo à vontade (nome, sigla, cores, nome/número/atributos dos jogadores).

Para regenerar tudo do zero: `python3 tools/league_gen.py` (gera também os rostos 2D).

Dribles com o analógico direito (relativos ao corpo do jogador):

| Direção | Sem LT/L2 | Com LT/L2 |
|---|---|---|
| Frente | Toque longo + arrancada | Toque longo + arrancada |
| Trás | Puxada (giro de 180°) | Roleta |
| Lado | Corte lateral | Pedalada (pode desequilibrar o marcador) |
