## Craquez 2.1.0

Jogo de futebol feito 100% com inteligência artificial, **gratuito**. Android, Windows e Linux.

### Downloads
- **Android**: `Craquez-v2.1.0.apk` (instale permitindo "fontes desconhecidas")
- **Windows**: `Craquez-v2.1.0-Instalador-Windows.exe` (instalador) ou `Craquez-v2.1.0-Windows.zip` (portátil)
- **Linux**: `Craquez-v2.1.0-Linux.tar.gz` → extraia e rode `./Craquez` (ou `./instalar.sh` para o atalho no menu)

### Novidades
- **Nada na frente do campo**: tudo o que estiver entre a câmera e o gramado e cair sobre ele na
  tela (placas, letreiro e porta dos vestiários, túnel, arquibancada e tribuna da frente) some,
  em todos os estádios, ginásios, society e ruas.
- **Ninguém fora do campo**: o expulso vai para o vestiário (some do campo) e, em todo reinício,
  quem caiu ou correu para fora volta para dentro das linhas.
- **Visão livre**: placas, alambrados, guarda-corpos e arquibancadas do lado da câmera agora somem
  sempre que tapam qualquer pedaço do campo visível (antes só o ponto da bola era testado, e o que
  dava a volta no campo nunca sumia).
- **Pés retos**: a ponta do pé aponta para a frente do corpo e a sola fica plana no chão.
- **Fim da comemoração**: ninguém fica com os braços erguidos depois do gol.
- **Goleiro**: o lado do mergulho fica fixo durante a defesa e o movimento é contínuo (sem "quadros").
- **Redes** de náilon mais soltas (estufam, pesam e voltam devagar) e novo som da bola na rede.
- **Menus sem chiado**: o ambiente da torcida não toca mais nos menus.
- **Option file** de volta dentro de Configurações (aba própria); Mural de troféus de novo no menu.
- **Cobrança**: o cobrador espera parado os companheiros se posicionarem antes de ir para a bola.
- **Ginásios**: torcedores em pé (pulando nos lances) nas arquibancadas laterais.
- **Escanteios de verdade**: bola parada no canto; cobrança com cruzamento na área (força escolhe
  primeiro ou segundo pau) ou toque curto para o companheiro que vem buscar. A área já aparece
  ocupada (atacantes nos seus lugares, marcadores colados).
- **Cena da cobrança**: no escanteio, na falta, no tiro de meta e no pênalti o cobrador vai andando até a bola
  parada antes de cobrar (no escanteio, com câmera de fora do campo, atrás da bandeirinha).
- **Saída de bola na marca central**: a bola fica exatamente no centro até o toque.
- **Defesas do goleiro**: a ponta dos dedos desvia a bola na direção do contato (para fora, rente
  à trave ou por cima do travessão); a espalmada usa a posição real das mãos; agarrando no
  mergulho, a bola fica nas mãos esticadas durante o voo e a queda.
- **Braços que acompanham o corpo**: o balanço segue a perna oposta em qualquer ritmo (andando,
  trotando, correndo), o cotovelo dobra mais com o braço à frente, os braços abrem nas curvas e
  andando de lado/de costas; cada atleta com o seu jeito (abertura, cotovelo, assimetria).
- **Estádios completos**: arquibancadas nos quatro lados (a do lado da câmera some sozinha quando
  tapa a visão), cantos fechados, tribuna central com vestiários, camarotes e imprensa; ginásios
  com arquibancada dos dois lados e vão da porta do vestiário; society com cobertura na
  arquibancadinha e bancos de reservas.
- **Bandeiras da torcida agitadas** de um lado para o outro (cada uma no seu ritmo, mais forte nos
  lances de perigo e no gol), também atrás dos gols; bandeirões dos estádios ondulando no vento.
- **Option file**: times em ordem alfabética dentro de cada liga e escudos embutidos no próprio
  arquivo (aparecem na escolha dos times quando existem).
- **Menus reorganizados**: Configurações em submenus (Som, Câmera, Partida, Vídeo); Option file
  com tela própria no menu principal; Mural de troféus dentro de Competições; pausa com submenus.
- **Câmeras**: retiradas Drone, Atrás do Gol e Ombro.
- **Jogo destravado (CPU x CPU e contra você)**: a bola viajando num passe deixava os dois times
  "defendendo" e todos recuavam para trás dela — agora o time mantém a fase de ataque até o
  adversário dominar, os atacantes atacam a profundidade (até a linha da zaga) e o time se espalha
  atacando e fecha defendendo. A CPU procura o passe para frente para quem está livre e com a linha
  aberta (e, pressionada, toca para o mais livre, não para qualquer lado), chuta de um pouco mais
  longe e dá menos botes certeiros. Em teste: no campo, de 2 para ~20 chutes em 3 partidas.
- **Força dos passes**: a velocidade de saída agora considera o atrito e o arrasto da bola, e a
  força cresce com a distância (curto chega macio, longo chega firme) — o passe não morre no meio
  do caminho; a antecipação do companheiro usa o tempo de voo real.
- **Atletas mais diferentes**: luvas nos goleiros, braçadeira do capitão, fita nos punhos de alguns,
  cabeças raspadas e cabelos descoloridos, mais variação de altura e de tom de pele.
- **Entrada dos jogadores**: prédio dos vestiários com fachada, janelas, letreiro e boca do túnel,
  com túnel inflável na cor do time nos estádios; porta do vestiário nos ginásios e no society.
- **Redes da rua** com fio mais grosso e cor de nylon, visíveis sobre o chão claro.
- **Corpo menos robótico**: o tronco antecipa as curvas; parado, o atleta respira (mais ofegante
  cansado) e troca o peso de uma perna para a outra; cada um no seu ritmo.
- **Movimentação refeita**: cada atleta tem o seu jeito de correr (ritmo da passada, braços mais
  soltos ou contidos, giro dos ombros, joelho alto); no pique o braço bombeia com o cotovelo a ~90°
  e o joelho sobe; conduzindo a bola, passadas mais curtas e rápidas com os braços abertos; na
  freada, passos curtos; andando, braços relaxados. Parado esperando o reinício, de vez em quando
  mãos na cintura (cansado, mãos nos joelhos). A animação usa o deslocamento real do atleta:
  ninguém mais "corre sem sair do lugar".
- **Câmera "Aberta Aproximada"**: alta como a transmissão aberta (vê a jogada e o posicionamento),
  mas fechada na bola, com os atletas grandes o bastante para ver os detalhes.
- **Jogo mais rápido**: a partida roda 18% mais rápido que o tempo real (bola, atletas e animações).
- **Faltas e reinícios com a bola parada**: quem precisa respeitar a distância ou esbarra em alguém
  para de verdade (sem correr no lugar até a bola rolar).
- **Reposição do goleiro**: só entrega para quem está livre e com a linha do passe livre; sem
  opção, espera alguém se desmarcar (até 4 s no futsal) ou dá o chutão — não rola mais a bola no pé
  do adversário (em teste: 11 de 12 reposições chegaram ao companheiro).
- **Postura tática**: zaga e meio-campo alinhados em linhas no campo; companheiros desviam prevendo
  a corrida um do outro e mantêm mais distância (menos contato entre jogadores do mesmo time).
- **Lateral do futsal e do society**: com os pés, bola parada na linha e sempre um passe para um
  companheiro (chute vira passe); no futsal, 4 segundos para cobrar, senão a bola vai para o
  adversário. Os companheiros se oferecem para o passe em vez de correr para a área.
- **Regras do jogo**:
  - **recuo para o goleiro**: passe com o pé de um companheiro (ou lateral) não pode ser agarrado —
    o goleiro joga com os pés;
  - **cartões**: amarelo na falta imprudente (carrinho por trás, chegada forte), vermelho na
    violenta, no segundo amarelo ou ao parar o último homem; o juiz ergue o cartão; o expulso sai e
    o time joga com um a menos (no futsal completa depois de 2 minutos ou de sofrer um gol); na rua
    não há cartões;
  - **faltas acumuladas no futsal**: da 6ª falta do time no tempo em diante, tiro livre direto sem
    barreira da marca dos 10 m (ou do lugar da falta, se mais perto); zeram no intervalo;
  - quem já tem amarelo entra com mais cuidado nas divididas.
- **Postura dos atletas (jogadores, goleiros e juiz)**: a inclinação do tronco é medida depois da
  animação e corrigida — sem corcunda nem tronco caído de lado, peito aberto, ereto parado e
  inclinado na medida na corrida; goleiro à frente só na posição de espera.
- **Goleiros reformulados**: o goleiro lê a trajetória da bola em 3D na linha em que está (não só
  na linha do gol), com tempo de reação e erro de leitura conforme a qualidade e a força do chute,
  e escolhe a defesa certa: encaixe de frente, defesa baixa (cai sobre a bola), salto para agarrar
  no alto ou ponte rasteira, à meia-altura e no ângulo. Desloca-se em passadas laterais (não corre
  de lado) e a ponte é um impulso de verdade, com voo e queda. O alcance é o das mãos e do corpo na
  posição da defesa (ponte rasteira não pega bola no ângulo) e a bola não "atravessa" as mãos.
- **Animação das defesas**: corpo sai do chão de lado, esticado, com as duas mãos indo juntas na
  bola, pernas juntas, queda deitado e levantada; encaixe com as mãos atrás da bola, defesa baixa
  ajoelhando, salto com os braços no alto.
- **Domínio de bola**: ao receber, o pé mais perto vai ao encontro da bola e amortece (coxa sobe,
  parte de dentro do pé virada para a bola, corpo sobre ela).
- **Chutes e passes com o corpo**: braço contrário abre para equilibrar, o outro recua; tronco sobre
  a bola no rasteiro e inclinado para trás na bola alta.
- **Nomes e números sobre os atletas**: etiqueta com o número na cor do time e o sobrenome em cima
  do jogador controlado e de quem está com a bola (ou de todos, ou desligado — em Configurações e
  no menu de pausa).
- **12 músicas originais** (pop-rock, samba eletrônico, house, reggae, funk, baião, rock...) com
  produção atual: sintetizadores supersaw, piano elétrico em FM, baixo com sub, bateria com palmas e
  bumbo saturado, reverb, estéreo aberto e sidechain. Cada vez que o jogo abre começa uma sorteada
  e, ao fim, vem outra.
- **Locais mais vivos**: câmera aérea em cabos sobre o campo nos estádios, bandeirinhas de
  escanteio tremulando, bandeira do clube no mastro do society, pipas no céu e varal de roupas nas ruas.
- **Redes do society** sempre visíveis (desenhadas dos dois lados e por cima do alambrado).
- **Depois da queda numa falta** o atleta não fica mais com os braços erguidos: apoia as mãos no
  chão para levantar e os braços descem.
- **Atletas com corpo firme**: menos inclinação e balanço do tronco nas curvas e arrancadas (o
  tronco inclina a partir do quadril, sem envergar a coluna), camisa e calção sem ondular como
  gelatina e luz com forma mais definida nos músculos.
- **Gramados de TV**: faixas de corte com contraste e brilho diferentes conforme o sentido, grama
  mais nítida.
- **Society**: grama sintética de verdade, com fios, borracha preta entre eles e emendas dos rolos.
- **Futsal**: ginásios poliesportivos com as linhas de vôlei e de basquete pintadas por baixo.
- **Ruas**: tampas de bueiro, bocas de lobo, manchas de óleo, remendos de asfalto, meio-fio e sarjeta.
- **Intervalo de verdade**: apito, plano aberto do estádio, varredura de transmissão e a tela do
  intervalo com placar, posse de bola e finalizações; dali dá para abrir as **substituições**, as
  táticas e as configurações antes de começar o 2º tempo (os atletas voltam descansados).
- **Substituições** (no intervalo e na pausa): 5 trocas no campo; no futsal, society e rua as
  trocas são livres. Quem entra aparece com o próprio nome, número e cara.
- **Gol sem cortes, com a rede balançando**: a bola segue depois da linha, estufa o fundo da rede
  (que cede e devolve só um pouco) e cai lá dentro; a câmera sai da jogada, acompanha a bola na rede
  e desliza até o autor do gol comemorando. Replay e tomada final entram por varredura, sem corte seco.
- **Torcida mais viva**: torcedores de alturas e corpos diferentes (crianças também), bonés,
  camisas listradas quando o time joga de listras, cachecol esticado acima da cabeça; a arquibancada
  pula em grupos fora de sincronia e levanta os braços no gol do mandante; faixas de torcida
  penduradas na mureta.
- **Estádios mais bonitos e únicos**: escadas e vomitórios nas arquibancadas, corrimãos, cobertura
  com testeira na cor do clube (ou de LED), treliças e beirada translúcida. Cada modelo tem a sua
  marca: pira olímpica no Olímpico Nacional, torre de concreto no Centenário, fachada de LED na
  Arena Futuro, neve na cobertura do Alpino, coroa dourada na Imperial, velas brancas no Litoral,
  bandeirões no Caldeirão e na Metropolitana, marquise com nervuras no Estadual e painéis de sombra
  no Deserto.
- **Sair do jogo**: botão "SAIR DO JOGO" no menu principal (e no voltar do menu), com confirmação.
- **Sem o logo antigo** na barra dos menus.
- **Rua**: no lugar das placas de publicidade, placas brancas baixas e separadas.
- **Uniformes mais fiéis**: 64 clubes corrigidos (Série B, MLS, Uruguai, Peru, Chile, Equador,
  Arábia Saudita, Rússia e Ucrânia, além de Botafogo, Vasco, Santos e Mirassol na Série A).
- **Option file**: cada jogador aceita atributos (`a` = velocidade, aceleração, chute, passe,
  controle, defesa) e características (`traits`), além de nome, número, posição e geral.
- **Ruas mais realistas**: quadra pintada no chão com linhas completas, áreas em meia-lua e marcas;
  as muretas grafitadas das laterais viraram **placas de publicidade** de comércios fictícios do
  bairro; no porto, frades de amarração, paletes e boias no lugar das árvores.
- **Sem pontos brilhantes**: tirados os flashes de câmera da torcida (pontinhos brancos piscando);
  os pombos são foscos e voam para fora do campo; o brilho (bloom) só pega luzes de verdade.
- **Option file no Android**: botão "Permitir acesso aos arquivos" (Android 11+ exige liberar o
  acesso para ler o .json de Downloads/Documentos); listas sem texto cortado e configurações com
  rolagem para todas as opções aparecerem em qualquer tela.
- **Option file com as competições** (nome, descrição e troféu editáveis).
- **Cartão de Configurações** com arte própria (engrenagens e controles).
- **Menu principal** em quatro cartões: Jogar, Competições, Mural de Troféus e Configurações.
- **Mais competições**: além dos 15 torneios, cada liga do jogo é um campeonato em pontos corridos
  e cada país tem a sua copa nacional em mata-mata (campo e futsal), cada uma com o seu troféu.
- **Pernas corrigidas**: os ossos que dobram (coxa, canela, braço) não são mais deformados pelo
  biotipo — antes os joelhos entortavam para dentro ao dobrar.
- **Espaço entre companheiros**: quem não está com a bola desvia de quem está perto (e abre mais
  espaço para quem conduz ou é controlado pelo jogador); nada de trombadas no mesmo time.
- **Option file**: o navegador mostra só os arquivos compatíveis, com o texto dentro da margem;
  "Remover option file" volta aos dados originais do jogo.
- **Sem bandeiras** nas telas de escolha (clubes/seleções e times): ilustrações de estádio.
- **Society**: a marca dos 10 m (só do futsal) aparecia colada na marca do pênalti.
- **Modo Competições**: 15 torneios — Copa do Mundo, Copa das Américas, Eurocopa, Copa
  Afro-Asiática, Liga dos Campeões, Libertadores, Brasileirão (pontos corridos), Copa do Brasil,
  Sul-Americana, Mundial de Clubes, Supercopa, Copa do Mundo e Liga de Futsal, Copa Society e Torneio
  de Rua. Tabela, grupos e chave do mata-mata; jogue ou simule cada partida; empate no mata-mata vai
  aos pênaltis; a campanha fica salva.
- **Troféus e Mural de Troféus**: cada competição tem a sua taça (globo, orelhuda, estrela, bola,
  prato, planeta...); as conquistas ficam na estante com o time e a data.
- **+8 estádios** (15 no total), cada um com a sua identidade: bacia fechada ou só laterais,
  cobertura em anel, arco, faixa de camarotes ou de LED, cor das cadeiras, refletores em torres, na
  cobertura ou em postes, alambrado e entorno (cidade, mar, montanhas, neve, deserto, rio, campo). A
  imagem do seletor mostra essas diferenças.
- **Goleiro**: repõe a bola com as mãos (rolada rasteira no curto, por cima do ombro no médio) ou de
  chutão de bate-pronto para o ataque (no futsal só com as mãos); dá alguns passos antes de repor e
  fica em posição de espera quando o adversário se aproxima.
- **Física revista**: o quique da bola perde velocidade conforme a força do impacto (rasante quase
  não freia); o atleta arranca forte e perde aceleração perto da velocidade máxima, freia de forma
  realista e faz curvas mais abertas em velocidade.
- **Seleções** divididas por continente; **sem escudos** na escolha dos times.
- **Rua sem muretas nas linhas de fundo**: a bola sai e vira tiro de meta ou escanteio.
- **Option file**: "Carregar option file…" abre um navegador de pastas (Downloads, Documentos,
  unidades, a pasta do jogo) para escolher qualquer .json; a opção de exportar saiu.
- **Resoluções**: lista com 16:9, 16:10, 4:3 e 21:9; janela redimensionável; a interface se
  reajusta a qualquer formato e a câmera do jogo mostra a mesma largura de campo em telas mais
  estreitas.
- **Chutes e passes no tempo certo**: na preparação a bola é trazida ao pé de chute e o corpo vira
  para o alvo (o chute não sai mais torto por causa do corpo atravessado); o instante em que o pé
  encontra a bola é medido em cada animação e cai exatamente na saída da bola.
- **Atletas mais realistas**: luz suave com volume nos músculos, pele com tom quente na transição
  luz/sombra, brilho acetinado no uniforme, contorno mais fino; cintura mais fina, ombros e coxas
  mais definidos, quadril mais estreito, cabeça proporcional.
- **Pausa**: os atletas ficam parados exatamente na pose e no lugar em que estavam (e a pausa durante
  a comemoração do gol volta para a comemoração).
- **Menus de seleção sem bandeiras**: escudos desenhados com as cores de cada time (seleções também).
- **Torcedores na rua**: gente do lado de fora (em pé, sentada na mureta, crianças) que acompanha o
  lance com o olhar, se agita nos chutes e pula com os braços para cima no gol do seu time.
- **Cenários vivos**: pombos que levantam voo quando a bola ou os jogadores chegam perto (e voltam
  depois), vira-lata caramelo que acompanha o jogo e late no gol, placar eletrônico com o placar e o
  tempo reais (mesa do anotador no futsal e em postes no society), luzes de neon que piscam no gol,
  fotógrafos agachados atrás dos gols e gandulas no campo.
- **Novos locais**: mais 6 campos de society (praia, cobertura de prédio, arena neon, chácara,
  condomínio e gramado natural), mais 2 ginásios de futsal (poliesportivo e arena roxa) e mais 1 rua
  (linha do trem, com trilhos, trem parado e plataforma); bar com mesas e guarda-sóis nos society.
- **Ícone novo**: o jogador da capa fazendo uma bicicleta, com a bola e o estádio iluminado.
- **Pontos de luz corrigidos**: pixels inválidos não explodem mais no brilho (bloom limitado), e o
  reflexo especular só aparece em materiais bem lisos.
- **Chuva sem artefatos**: o emissor das gotas não gira mais com a câmera, e o gramado molhado fica
  mais escuro, sem a mancha de brilho que andava em volta do campo.
- **Modos**: a tela mostra só as modalidades; clubes ou seleções vêm na tela seguinte.
- **Sortear** times e local; prévia do uniforme sem as chuteiras.
- **Sem avisos de texto durante o jogo**.
- **Atletas**: número pintado na camisa (dobra com o tecido); camisa e calção com folga e movimento
  (balançam na arrancada e nas curvas); proporções mais atléticas; passada suavizada.
- **Redes** com tecido de molas: o balanço se espalha e a rede afunda onde a bola encosta.
- **Física**: lateral **com as mãos** no futebol de campo (com os pés nos outros modos); condução com
  a bola ao lado do pé e toque de corte na mudança de direção; espalmada que reflete na mão do
  goleiro (por cima do travessão nas bolas altas); choque de corpo por impulso.
- **Locais**: mais 6 ruas (beco, conjunto, estacionamento, campinho de terra, porto e vila colorida)
  e mais 5 ginásios de futsal.
- **Option file completo** no repositório e nos pacotes de PC (`option_file/`): times, elencos,
  estádios, bolas, chuteiras e locais.
- **Tela inicial em tela cheia**: a capa cobre a tela inteira em qualquer formato (16:9 sem corte
  nem laterais borradas; celulares e tablets sem faixas) e se reajusta se a janela mudar; vídeo de
  abertura refeito a partir da capa nova.
- **Menus reformulados**: fundo animado (arte da capa com feixes de luz e partículas), transição
  em faixas entre as telas, conteúdo entrando em sequência, trilha de navegação no topo e dicas
  de controle em "teclas" na barra de baixo; painéis e botões com novo acabamento.
- **Tela de modos** com quatro cartões grandes (Futebol, Futsal, Society, Rua) e ilustrações novas,
  sem bola: estádio à noite com refletores, ginásio de futsal, society com alambrado no fim de
  tarde e rua com muro grafitado. O cartão em foco cresce e acende na cor da modalidade.
- **Conquistas retiradas**.
- **Locais da partida** voltaram a ser desenhos 2D (escurecem à noite e esquentam no fim de tarde).
- **Society e rua sem som de torcida** (só bola, apito, rede e trave).
- **Câmeras redefinidas**: TV, TV Próxima, Transmissão Aberta, Alta (tática) e as novas próximas do
  jogador — **Perto do Jogador** (lateral e fechada no atleta), **Atrás do Jogador**, **Ombro (bem
  perto)** e **Rente ao Gramado** —, além de Dinâmica, Atrás do Gol e Drone.
- **Bolas refeitas**: corrigido o defeito que deixava todas acinzentadas com costuras claras;
  costura em sulco escuro, gomos estofados e desenhos alinhados aos gomos. Catálogo com
  **18 bolas brancas de campo** (e 5 de futsal), além das coloridas.

## Craquez 2.0.0 (anterior)

Jogo de futebol feito 100% com inteligência artificial, **gratuito**. Android, Windows e Linux.

### Downloads
- **Android**: `Craquez-v2.0.0.apk` (instale permitindo "fontes desconhecidas")
- **Windows**: `Craquez-v2.0.0-Instalador-Windows.exe` (instalador) ou `Craquez-v2.0.0-Windows.zip` (portátil)
- **Linux**: `Craquez-v2.0.0-Linux.tar.gz` → extraia e rode `./Craquez` (ou `./instalar.sh` para o atalho no menu)

### Jogo
- **Saída de bola** sempre com passe para o companheiro ao lado.
- **Faltas**: a vítima tropeça, cai, rola e levanta; quem fez reclama; o juiz corre e aponta.
  Bola parada no lugar, adversários a distância, barreira montada. Escolha a cobrança:
  passe, cruzamento na área e, perto do gol, **chute direto** (dica na tela). Opção de
  cobrança rápida ou cena curta antes da cobrança.
- **IA**: cada jogador tem personalidade e tempo de decisão próprios; linhas de passe diferentes;
  o time mantém o espaçamento (sem bolo em volta da bola); menos faltas bobas.
- **Condução, chute e goleiros**: toques que seguem a direção desejada; chute mira o canto livre;
  goleiro com reação individual, mergulho com alcance real, ponta dos dedos e saída no mano a mano.
- **Árbitro** no sistema diagonal, fora das linhas de passe.

### Visual e som
- Gramado novo (folhas, tufos, sem mosaico), pontos brilhantes do campo corrigidos, teto dos
  ginásios iluminado, fotógrafos, cinegrafistas, gandulas, telões e cidade em volta do estádio.
- **Bolas reformuladas** com gomos de verdade e relevo.
- **Bandeiras reais** de países (ligas, seleções, nacionalidades) e bandeira de cada clube;
  uniformes dos clubes mais parecidos com os oficiais.
- Nomes de estádios no estilo de cada país; foto 3D de cada local na escolha, com o seletor na imagem.
- **Torcida com gravações reais** (licenças livres) que reage ao lado de quem ataca.
- **Trilha sonora original** nos menus e **vídeo de abertura** que para na capa até apertar um botão.
- Capa em tela cheia.

### Opções
- Gráficos: Leve / Equilibrado / Alto / Ultra e, no PC, modo de tela, resolução, sincronia vertical,
  limite de quadros, escala de render, antisserrilhado, sombras e texturas.
- **Salvar / Cancelar** nas configurações; volume da música.
