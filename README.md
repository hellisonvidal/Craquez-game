# ⚽ Craquez

Jogo de futebol **3D realista** feito em **Unity 6.3 LTS (URP)**, com jogabilidade no estilo EA FC / eFootball, para **Android**, **Windows** e **Linux**.
Modos **Campo (11×11)**, **Futsal (5×5)** e **Rua (3×3, bola na parede vale)**. Contra a CPU ou contra um amigo no mesmo aparelho/PC.
Controles **Xbox / PlayStation**, teclado e toque, detectados automaticamente.

Feito 100% com inteligência artificial, por isso é e sempre será **gratuito**.

> A versão anterior, em Godot, continua guardada na branch `godot-legacy`.

## Destaques

| | |
|---|---|
| **Atletas realistas** | Corpo humano MakeHuman (CC0) com 4 biotipos, rosto variável por jogador (10 formas), 5 cabelos, tons de pele, número na camisa |
| **Movimentos reais** | Animações de **captura de movimento** (CMU): corrida, trote, caminhada, de costas, marcação, cortes, fintas, giro, passe, chute, voleio — misturadas pela velocidade e direção do atleta |
| **Uniformes vestidos** | Camisa, calção e meião são peças próprias sobre o corpo: folga, barra por fora do calção, mangas, gola, dobras do tecido. 3 uniformes por time com listras, aros, faixa, metades, riscas, gola e punhos |
| **Chuteiras** | Modeladas (sola, travas, cano acolchoado, cadarço) com 16 desenhos originais; cada atleta usa a sua ou todos o mesmo modelo |
| **Bolas** | 30 bolas originais (clássica, espirais, faixas, estrelas, retrô de couro, alta visibilidade para neve...) |
| **Seleções** | 40 seleções de futebol e 20 de futsal, ordenadas pelo ranking do Craquez (inspirado no da FIFA), com bandeiras e elencos fictícios |
| **Estádios** | 3 portes (arquibancadas, cobertura, refletores), placas de LED, bancos, bandeirinhas, árbitro, rede que estufa no gol |
| **Locais** | 13 quadras de futsal fechadas com tela de proteção, 4 campos de society e 14 ruas (prédios, morro, praia, terraço, gaiola, telhado, viaduto, praça, beco, conjunto, estacionamento, terra, porto, vila) |
| **Clima e horário** | Sol, nublado, chuva, tempestade com raios, neve; dia, fim de tarde, noite; clima dinâmico que muda durante o jogo e altera a bola |
| **Imagem** | URP com sombras, bloom, tonemapping ACES e correção de cor; 3 níveis de qualidade (automático por aparelho) |
| **Ligas 2026/27** | 22 ligas (17 de campo + 5 de futsal), 400 clubes, 60 seleções e 8.120 jogadores com atributos e habilidades únicas. Nomes alterados, sem licença |
| **Táticas** | Formação, mentalidade, pressão e escalação (troca titular ↔ reserva) |
| **Cerimônia** | Sobrevoo do estádio, entrada dos times e apresentação das escalações |
| **Option file** | Personalize nomes, uniformes, estádios e escudos no seu aparelho — [docs/OPTION_FILE.md](docs/OPTION_FILE.md) |
| **IA** | Cada atleta tem personalidade (disposição, ousadia, disciplina tática, leitura de jogo): decide no seu ritmo, oferece linhas de passe diferentes dos companheiros e o time mantém o espaçamento (sem bolo em volta da bola) |
| **Goleiros** | Tempo de reação individual (mais lento com a visão encoberta), passo lateral ou mergulho com alcance real, defesa na ponta dos dedos, espalmada, saída no mano a mano |
| **Faltas** | A vítima tropeça, cai, rola segurando a perna e levanta; quem fez reclama. Cobrança rápida ou cena curta com barreira montada (opção) |
| **Saída de bola** | Sempre com um passe para o companheiro ao lado |
| **Bandeiras** | Bandeiras reais dos países em ligas, seleções e nacionalidades; bandeira de cada clube (cores + escudo) na escolha e na torcida |
| **Gráficos** | Predefinições Leve / Equilibrado / Alto / Ultra e, no PC, modo de tela, resolução, sincronia vertical, limite de quadros, escala de render, antisserrilhado (FXAA, SMAA, MSAA), sombras e texturas |
| **Física** | Determinística a 60 Hz: bola com arrasto e "crise do arrasto" em chutes fortes, efeito Magnus, quique por superfície com fase de deslize, desvio no corpo pelo ângulo de impacto; goleiro com tempo de reação, mergulho, encaixe ou espalmada; faltas, pênalti, impedimento, laterais, escanteios |

## Telas e modos

Apresentação Claude → aviso "feito com IA, gratuito" → título (com uma partida rolando ao fundo) → **Jogar**: Clubes · Seleções · Futsal · Futsal Seleções · Rua → configuração (duração, clima, horário, bola, chuteiras...) → escolha dos times/seleções (+ táticas) → jogo.

O jogo sempre abre em **tela cheia** (Windows: janela sem bordas na resolução nativa; Android: modo imersivo).

## Controles

| Ação | Xbox | PlayStation | Teclado (P1) | Teclado (P2) |
|---|---|---|---|---|
| Mover | Analógico esq. / D-pad | Analógico esq. / D-pad | WASD | Setas |
| Passe (defesa: bote) | A | ✕ | J | Num 1 |
| Chute, segure p/ força (defesa: carrinho) | B | ◯ | K | Num 2 |
| Passe alto / cruzamento | X | ▢ | L | Num 3 |
| Enfiada (defesa: 2º marcador) | Y | △ | I | Num 5 |
| Correr | RT | R2 | Shift esq. | Num 0 |
| Proteger / conter | LT | L2 | E | Num 6 |
| Chute colocado | RB | R1 | U | Num 9 |
| Dribles | Analógico direito | Analógico direito | Setas | Home/End/Del/PgDn |
| Trocar jogador | LB | L1 | Q | Num 4 |
| Pausa | Start | Options | Esc / P | Enter (num) |

No Android: analógico flutuante, botões de ação e deslizar o dedo para driblar.

## Como compilar

Veja [docs/BUILD.md](docs/BUILD.md). Resumo: a cada envio, o GitHub Actions compila e testa o código; com os segredos da licença Unity Personal (gratuita) configurados, também gera o **APK** e o **.exe**.

## Estrutura

```
Assets/
  Scenes/Main.unity                cena vazia: tudo é montado por código
  Craquez/Scripts/Sim/              simulação pura (partida, bola, jogador, CPU, ligas, JSON)
  Craquez/Scripts/Runtime/          Unity: telas, partida 3D, atletas, estádios, clima, áudio, controles
  Craquez/Editor/                   importação de modelos/animações, materiais URP, builds
  Craquez/Tests/                    testes EditMode da simulação
  Craquez/Resources/Athletes/       atletas (FBX), animações (FBX) e texturas
  Craquez/Resources/Data/           leagues.json
tools/blender/                      geração dos atletas e transferência da captura de movimento
tools/league_gen.py                 geração das ligas e elencos
tools/ci/check.sh                   compilação + testes sem licença Unity
```

Créditos e licenças: [CREDITS.md](CREDITS.md).
