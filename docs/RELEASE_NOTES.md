## Craquez 2.15.0

Jogo de futebol feito 100% com inteligência artificial, **gratuito**.

### Download
- **Celular Android**: `Craquez-v2.15.0.apk`
- **TV (Google TV / Android TV, ex.: TCL C6K)**: `Craquez-TV-v2.15.0.apk` — use este na TV (32 e 64 bits).

### Novidades
- **Modo on-line (preparado)**: partida rápida, sala com código e convite pelo WhatsApp, com a partida
  rodando igual nos dois aparelhos (só os comandos viajam pela internet, pelo Relay da Unity). O cartão
  ON-LINE liga assim que o projeto do jogo na Unity Cloud estiver configurado (ver docs/ONLINE.md).
- **Link do convite**: `craquez://sala/CODIGO` abre o jogo direto na sala.

Inclui tudo da 2.14.0 abaixo.

## Craquez 2.14.0

Jogo de futebol feito 100% com inteligência artificial, **gratuito**.

### Download
- **Celular Android**: `Craquez-v2.14.0.apk`
- **TV (Google TV / Android TV, ex.: TCL C6K)**: `Craquez-TV-v2.14.0.apk` — use este na TV (32 e 64 bits).

### Novidades
- **Placas do Craquez**: nos ginásios de futsal e nos estádios, as placas de publicidade agora mostram o
  nome do jogo, intercaladas com placas azuis, vermelhas e brancas (as cores das telas do jogo).

### Correções
- **Ginásio sem parede e sem teto**: as paredes, o teto e a estrutura do ginásio sumiam (o sistema que
  esconde o que tapa a câmera achava que eles estavam na frente, porque a câmera fica dentro do ginásio) e
  aparecia o chão de fora com a torcida dos fundos solta. Agora o ginásio fica inteiro.
- **Placas "arco-íris"**: a grade fina das placas de LED, vista de lado e de longe, virava faixas coloridas.
  As placas novas não têm a grade.
- **Vídeo de abertura** no celular: o vídeo não entrava no APK (o Unity do servidor de build não lia o
  formato). Agora ele é tocado direto pelo aparelho; se algum aparelho não conseguir abrir, o jogo vai
  para a capa em vez de ficar na tela preta.
- **Goleiro defendendo de longe com os pés**: o bloqueio com o pé e com o corpo (pernas na frente) agora
  é só reflexo em chute de perto; chute de longe ele se abaixa e encaixa com as mãos.

Inclui tudo da 2.13.0 abaixo (replay salvo em vídeo, controles menores, clima aleatório, telas de nylon).

## Craquez 2.13.0

Jogo de futebol feito 100% com inteligência artificial, **gratuito**.

### Download
- **Celular Android**: `Craquez-v2.13.0.apk`
- **TV (Google TV / Android TV, ex.: TCL C6K)**: `Craquez-TV-v2.13.0.apk` — use este na TV (32 e 64 bits).

### Novidades
- **Salvar o replay em vídeo** (celular e TV): botão "⬇ Salvar vídeo" no replay grava os últimos 30 s
  em MP4, na câmera e velocidade escolhidas, sem os botões na imagem, e salva na galeria
  (Filmes/Craquez). Toque na tela para cancelar a gravação.
- **Controles do replay menores** e centralizados, longe dos cantos de baixo, onde ficam os botões do
  Android (voltar/início), que antes cobriam um deles.
- **Tela de nylon nas quadras de rua**: laterais e fundos cercados para a galera não levar bolada (a
  Gaiola continua com o alambrado de ferro).

### Correções
- **Clima aleatório**: o sorteio só podia dar nublado, tempestade ou nevasca (a semente da partida era
  sempre ímpar). Agora os seis climas saem com a mesma chance.
- **Pilares na frente da câmera**: a tela alta acima das placas (Quadra do Conjunto e Gaiola) era uma
  peça só nos dois lados e nunca sumia; de perto virava faixas cinzas na frente do jogo. Agora tudo o que
  fica do lado da câmera some nas câmeras de lado, em todos os locais.

## Craquez 2.12.1

Jogo de futebol feito 100% com inteligência artificial, **gratuito**.

### Download
- **Celular Android**: `Craquez-v2.12.1.apk`
- **TV (Google TV / Android TV, ex.: TCL C6K)**: `Craquez-TV-v2.12.1.apk` — use este na TV (32 e 64 bits).

### Correções
- **Atualização por dentro do jogo**: a instalação agora usa o instalador oficial do Android (sessões
  do PackageInstaller) no lugar de abrir o APK, que no Android 14+/Samsung falhava com "app não
  instalado". Antes de instalar o jogo confere se o download veio inteiro e se pacote, versão e assinatura
  batem com o app instalado; se o Android recusar, aparece o motivo exato (cancelado, sem espaço, outra
  assinatura, bloqueado pelo Play Protect...).
- **Importante**: esta versão precisa ser instalada **uma vez pela página de versões** (baixe o APK e
  abra), porque quem faz a instalação é a versão já instalada; da próxima em diante o botão ATUALIZAR
  do jogo usa o instalador novo.

Inclui tudo da 2.12.0 abaixo.

## Craquez 2.12.0

Jogo de futebol feito 100% com inteligência artificial, **gratuito**.

### Download
- **Celular Android**: `Craquez-v2.12.0.apk`
- **TV (Google TV / Android TV, ex.: TCL C6K)**: `Craquez-TV-v2.12.0.apk` — use este na TV (32 e 64 bits).

### Novidades
- **Uniformes em 3D de verdade**: camisa com gola redonda, mangas com volume e caimento solto, e calção
  com barra, feitos das roupas do MakeHuman (gratuitas) e encaixados em cada biotipo — antes o uniforme
  era o próprio corpo pintado.
- **Lateral**: a bola fica entre as mãos do cobrador e o adversário respeita 2 m de distância.
- **Bola saindo pela lateral**: segue rolando para fora (bate na placa) e um gandula a devolve ao
  cobrador; nada de bola parada em cima da linha.
- **Marcadores de treino** (os "chapéus chineses") no lugar dos cones altos das bolas de reposição.
- **Futsal com piso duro**: a madeira da quadra virou réguas envernizadas retas (antes parecia areia) e a
  sombra da bola ficou pequena e nítida.

## Craquez 2.11.0

Jogo de futebol feito 100% com inteligência artificial, **gratuito**.

### Download
- **Celular Android**: `Craquez-v2.11.0.apk`
- **TV (Google TV / Android TV, ex.: TCL C6K)**: `Craquez-TV-v2.11.0.apk` — use este na TV (32 e 64 bits).

### Novidades
- **Nova abertura em vídeo**: o cinematográfico do Craquez abre o jogo e termina na logo, que vira a
  tela de "toque para iniciar" (preenche telas largas sem faixas pretas).
- **Replay com controles de verdade**: a barra de controles (linha do tempo, voltar/avançar 5 s,
  pausar, velocidade, câmeras, foco e zoom) aparecia fora da tela; agora aparece embaixo. O menu
  passou a se chamar só "Replay" e o selo REPLAY não cobre mais o placar.
- **Escolha de times unificada**: depois da modalidade, uma tela só para clubes e seleções; na lista
  de ligas há a aba das seleções (todas pelo ranking e por continente).
- **211 seleções**: o quadro completo da FIFA, com bandeiras, cores, uniformes e elencos.
- **Quedas e levantar com captura de movimento**: tropeço e queda, caído segurando a perna e levantar
  do chão; quem faz a falta estica a perna no adversário (a pancada aparece) e o contato precisa ser
  de verdade. Quase sempre um companheiro (ou o próprio adversário) vem dar a mão.

## Craquez 2.10.1

Jogo de futebol feito 100% com inteligência artificial, **gratuito**.

### Download
- **Celular Android**: `Craquez-v2.10.1.apk`
- **TV (Google TV / Android TV, ex.: TCL C6K)**: `Craquez-TV-v2.10.1.apk` — use este na TV (32 e 64 bits).

### Novidades
- **Controles de toque mais precisos** (ajustados para telas rápidas como a do Galaxy S24+): cada dedo é
  lido direto da tela, toques rápidos nos botões não se perdem mais, o analógico nasce onde o polegar
  encosta, acompanha o dedo sem travar na borda, tem o mesmo tamanho em centímetros em qualquer tela e
  responde com menos atraso.
- **Condução de bola**: a bola não escapa mais sozinha nas curvas; o corte segue o analógico na hora e,
  ao soltar o CORRER, o jogador segura a bola em vez de deixá-la ir.
- **Domínio de passe**: quem recebe entra na linha da bola e a espera (antes passava da bola com o
  embalo); com o analógico solto, vai sozinho ao encontro do passe.
- **Corpo dos atletas**: tronco um pouco mais curto (pernas proporcionalmente mais longas), braços mais
  compridos e quadril um pouco mais largo.
- **Redes e bola**: rede em malha de losango com caimento natural (teto cedendo no meio, sobra deitada
  na grama e laterais soltas); bolas com tom de cada gomo levemente diferente e marcas de uso.

## Craquez 2.10.0

Jogo de futebol feito 100% com inteligência artificial, **gratuito**.

### Download
- **Celular Android**: `Craquez-v2.10.0.apk`
- **TV (Google TV / Android TV, ex.: TCL C6K)**: `Craquez-TV-v2.10.0.apk` — use este na TV (32 e 64 bits).

### Novidades
- **Goleiro com movimentos de captura**: mergulho para cada lado, bloqueio com o corpo, pegadas
  (rasteira, na altura do peito e no alto), rolar a bola, colocar a bola no tiro de meta e organizar
  a barreira nas faltas e escanteios.
- **Carrinho, levantar do chão e reclamação** com captura de movimento: o carrinho entra deslizando e o
  jogador levanta no fim; quem cai de costas senta, apoia a mão e fica de pé; quem fez a falta balança
  a cabeça e abre os braços. O técnico assiste ao jogo de braços cruzados.
- **Cenários com modelos de verdade** (pacotes gratuitos do Quaternius): árvores, pinheiros, arbustos,
  flores, capim e pedras no entorno dos estádios, nos society, na chácara e no centro de treinamento;
  canteiros com plantas na esplanada; sobrados e casinhas na rua (Morro, Vila Colorida e quarteirões
  baixos), com as cores de cada lugar e janelas acesas à noite.

## Craquez 2.9.1

Jogo de futebol feito 100% com inteligência artificial, **gratuito**.

### Download
- **Celular Android**: `Craquez-v2.9.1.apk`
- **TV (Google TV / Android TV, ex.: TCL C6K)**: `Craquez-TV-v2.9.1.apk` — use este na TV (32 e 64 bits).

### Novidades
- **Braços ligados ao tronco**: saíram os ajustes antigos que baixavam e apertavam os ombros (a manga
  parecia solta do corpo com os movimentos novos).
- **Mãos menores**, na proporção do corpo.
- **Defesas do goleiro**: a bola tocada na ponta dos dedos mantém a força (sai rente à trave ou entra
  forte, nunca "devagarinho" no gol); a tirada por cima do travessão passa mesmo por cima.
- **Bola saindo pela linha de fundo**: segue rolando/voando lá fora, quica, bate na placa e para; só
  então o juiz apita o tiro de meta ou o escanteio, e o goleiro busca a bola onde ela parou.

## Craquez 2.9.0

Jogo de futebol feito 100% com inteligência artificial, **gratuito**.

### Download
- **Celular Android**: `Craquez-v2.9.0.apk`
- **TV (Google TV / Android TV, ex.: TCL C6K)**: `Craquez-TV-v2.9.0.apk` — use este na TV (32 e 64 bits).

### Novidades
- **Movimentos novos, de captura profissional (Mixamo)**: andar, trotar, correr, correr de costas e
  passo lateral na marcação refeitos; o corpo se move como gente de verdade, sem o peito empinado.
- **Chutes e passes novos**: passe de chapa, chute com corrida, chute forte com a perna de apoio
  plantada e cavadinha; o toque na bola cai no instante certo do movimento.
- **Cabeceio** com o golpe de pescoço na bola.
- **Goleiro**: posição de espera com base larga, passos laterais acompanhando o lance, arremesso por
  cima do ombro e chutão de bate-pronto.
- Saem as correções desenhadas por cima da animação antiga (passada, braços, postura e joelhos), que
  deixavam o movimento cada vez mais estranho.

## Craquez 2.8.0

Jogo de futebol feito 100% com inteligência artificial, **gratuito**.

### Download
- **Celular Android**: `Craquez-v2.8.0.apk`
- **TV (Google TV / Android TV, ex.: TCL C6K)**: `Craquez-TV-v2.8.0.apk` — use este na TV (32 e 64 bits).
  O APK de celular é só 64 bits e não instala em TVs com sistema 32 bits ("erro ao analisar o pacote").
  Passo a passo do pen drive em `docs/TV.md`.

### Novidades
- **Cenários de rua de verdade**: quarteirão com calçadas, rua de mão dupla, faixas de pedestre, meio-fio
  e prédios enfileirados com cores de cidade (concreto, tijolo, bege) e janelas em escala; viaduto ao lado
  da quadra, com os pilares fora dela.
- **Movimento em volta dos locais**: carros (faróis à noite), bicicletas e pedestres circulando na rua,
  na avenida do estádio e no estacionamento do ginásio.
- **Corpo dos atletas refeito**: ombros na largura natural, peitoral e tórax menos salientes e camisa
  mais justa (o peito não fica mais "estufado").
- **Uniformes mais nítidos** de perto (listras, número e escudo).
- **Faltas só com contato de verdade**; ninguém corre em volta do goleiro com a bola nas mãos.
- Replay com controles, telas de proteção nos ginásios, defesas com salto e com o pé, gandula devolvendo
  a bola, levantar com a mão de quem ajuda, saída para o menu no intervalo e volume da torcida.
- Seletor de locais com fotos tiradas dos cenários 3D.

## Craquez 2.7.0

Jogo de futebol feito 100% com inteligência artificial, **gratuito**. Por enquanto, só Android.

### Download
- **Android**: `Craquez-v2.7.0.apk` (instale permitindo "fontes desconhecidas")
- **Importante**: a partir desta versão o APK tem assinatura fixa. Para instalar a 2.7.0 por cima de
  uma versão antiga, desinstale a antiga uma vez (o progresso salvo no aparelho é apagado). Daí em
  diante o botão **ATUALIZAR** do jogo baixa e instala as versões novas sozinho.

### Novidades
- **Câmeras**: TV Bem Perto, Rente ao Lance e Ombro (bem perto); as partes do estádio do lado da
  câmera ficam escondidas de forma estável (sem piscar) e as outras só voltam depois de um tempo.
- **Dribles característicos** (elástico, pedalada, chapéu, caneta, corte) para meias e atacantes
  habilidosos dos clubes brasileiros, das grandes ligas e das seleções do topo: Colocado + analógico
  direito. Analógico esquerdo com a bola: empurrão firme adianta a bola; de lado, rola com a sola.
- **Bicicleta** (de costas para o gol, bola entre a cintura e a cabeça, perto da área).
- **Defesa**: bote, carrinho e puxão de camisa/agarrão (Proteger + Colocado); segurar muito vira falta.
- **Quedas**: o jogador cai, às vezes rola no chão, sente a pancada e um companheiro, um adversário
  ou o próprio juiz vem ajudar a levantar. Sem gemidos nas trombadas.
- **Comemorações** novas: com a torcida atrás do gol e no banco (técnico e reservas vêm abraçar);
  sem papel picado nem sinalizadores. A rede agora estufa e balança no gol.
- **Torcida** com rosto e corpo melhores; na chuva, capas, capuzes e guarda-chuvas (sem bandeiras).
- **Poças** azuladas, em três formatos, com a grama encharcada em volta e os pingos batendo na água.
- **Layouts de controle**: quem chuta com o B/Bola, com o X/Quadrado ou com o Y/Triângulo, ou
  personalizado (Configurações → Controles); correr no gatilho ou no botão de ombro.
- **Lateral da CPU** com calma e para o companheiro livre.
- **Controle de bola** mais colado, mas às vezes a bola escapa (pique, corte em velocidade,
  marcação, gramado molhado).
- **Rua**: golzinho em algumas ruas, formatos 6 x 6 e 7 x 7 e menos gols de longe.
- **Arena de treino**: escolha o atleta, o goleiro devolve a bola, só o CT (sem torcida, juiz,
  placar ou quadro do jogador).
- **Banco**: aquecimento variado (trote, lateral, polichinelos, alongamento, tiros) e técnico vibrando.
- Parado com a bola, o jogador não fica mais dando passinhos no lugar.
- Foto dos locais no seletor não fica mais preta. Bandeiras de escanteio no tamanho certo.
- Sem música por enquanto.

## Versão 2.6.0

Jogo de futebol feito 100% com inteligência artificial, **gratuito**. Por enquanto, só Android.

### Download
- **Android**: `Craquez-v2.6.0.apk` (instale permitindo "fontes desconhecidas")

### Novidades
- **Arena de treino** (novo cartão TREINO em Jogar): você conduz um atacante contra o goleiro, sem
  relógio; a cada gol, defesa ou bola fora, uma bola nova aparece no seu pé em outro ponto.
- **Sorteio do juiz**: lados e saída de bola decididos na moeda, com o juiz e os capitães no círculo
  central durante a cerimônia (opção "Lado do campo": sorteio ou fixo).
- **Seletor de locais com foto de verdade**, vista de fora (estádios, ginásios, society e ruas); os
  ginásios ganharam o lado de fora (paredes com a faixa do clube, janelas altas, telhado em arco,
  marquise, calçada e estacionamento).
- **Tiro de meta sem corte**: o goleiro olha a bola sair, vai buscá-la (ou pega outra no cone atrás
  do gol), traz nas mãos, coloca na marca e cobra. **Cones com bolas de reposição** em volta do campo.
- **Reposição com as mãos por combinação de botões**: Passe = rolada; Passe + Colocado = por cima,
  curto; Alto = arremesso longo; Enfiada = arremesso rápido (contra-ataque); Chute = chutão;
  Chute + Colocado = bate-pronto baixo; Proteger + Passe = solta a bola no pé (dica na tela). A CPU
  também usa o arremesso rápido no contra-ataque.
- **Goleiro no tempo certo**: arma a defesa em cima da hora conforme a distância da bola (bola no
  corpo, sem pular; salto só acima da cabeça) e reage ao chute como gente (cara a cara mantido em ~40%).
- **Goleiro se ajeitando** com passadas curtas e joelhos dobrados (as pernas não entortam mais).
- **Faltas mais reais**: a vítima sente a pancada e às vezes um companheiro (ou quem fez a falta) vem
  dar a mão para levantar.
- **Contato físico**: na trombada, quem perde o choque é jogado para longe do contato e se recupera;
  ombro a ombro em velocidade, os dois inclinam um contra o outro.
- **Jogadores fora da jogada** andam ou trotam até a posição e param sem ficar "acertando" o lugar
  (antes corriam o tempo todo e trocavam de direção a cada instante).
- **Banco vivo**: técnicos acompanhando o lance na área técnica e orientando o time; reservas de
  colete aquecendo atrás dos gols.
- **Traves refinadas** (tubos elípticos, suportes curvos por fora da rede no campo e no society).
- **Chuva, poças e nevasca**: poças só com chuva forte que persiste ou temporal (secam com sol;
  chuva comum só deixa a bola mais rápida), sem os "ladrilhos" sobre elas; a bola para dentro d'água;
  novo clima **Nevasca** com neve densa, vento e campo coberto.
- **HUD**: só o quadro de quem está com a bola (mandante à esquerda, visitante à direita), sem nomes
  sobre a cabeça; escalações no rodapé.
- **Bola no gramado**: sombra de contato do tamanho da bola (a mancha grande fazia a bola parecer
  afundada).
- **Sem riscos escuros na quadra/campo** (sombra de estruturas que somem para não tapar a câmera) e
  **sem papel picado** na comemoração.

## Versão 2.5.0

Jogo de futebol feito 100% com inteligência artificial, **gratuito**. Por enquanto, só Android.

### Download
- **Android**: `Craquez-v2.5.0.apk` (instale permitindo "fontes desconhecidas")

### Novidades
- **Corrida de verdade**: as pernas seguem curvas de quadril e joelho medidas em caminhada, trote e
  pique (no pique o apoio é curto, o quadril estende no impulso e o joelho recolhe alto); o ritmo das
  passadas acompanha a velocidade (~1,4 ciclo/s no trote, ~2,1 no pique) e o corpo sobe na fase de voo
  entre uma passada e outra. Antes as pernas balançavam em senoides, com ar de robô.
- **Goleiro encaixando nos chutes**: as mãos desenhadas vão exatamente para onde a simulação calcula
  as mãos dele e, com a bola chegando, para a própria bola.
- **Cara a cara como no futebol real (~40%)**: o atacante sozinho conduz até perto e finaliza rápido,
  colocado; o goleiro sai fechando o ângulo e abafa a bola rasteira (por cima dele, passa). Antes o
  atacante chutava de ~20 m ou o goleiro tomava quase tudo, conforme o caso. Medido em 120 lances por
  modalidade: campo 44%, society 34%, futsal 27% (gol menor). Referência: ~38% das chances claras
  viram gol nas grandes ligas.
- **Mira em gol pequeno**: no futsal o chute colocado não sai mais pela linha de fundo.
- **Estádios**: reservas sentados nos bancos com o agasalho do time, técnico de pé na área técnica
  (com as linhas tracejadas), mesa do 4º árbitro com a placa de substituição e o gramado gasto na
  pequena área, na marca do pênalti e no círculo central — nada disso tapa o campo.
- **Uniformes não somem mais**: num quadro mais longo (carregamento, troca de câmera) o tecido da
  camisa era calculado num passo só, a "mola" explodia e o atleta aparecia sem roupa ou com a camisa
  esticada; agora o tecido é calculado em passos pequenos e nunca sai do lugar.
- **Física da bola revisada**: quiques de bola baixa que se perdiam voltaram (no futsal a bola não
  quicava); o quique segue o piso (bola largada de 2 m sobe ~0,7 m na grama, como na norma da FIFA
  para gramado natural, menos na quadra e no society e mais no asfalto).
- **Chuva e neve no gramado**: a água vai se acumulando com a chuva e aparecem poças (já cheias se o
  jogo começar chovendo), com os pingos abrindo anéis e espirrando água quando a bola ou os atletas
  passam; na poça a bola freia de repente e quase não quica, e o atleta perde um pouco de velocidade.
  Com neve acumulada a bola rola menos (um passe que rodaria ~44 m para em ~16 m com neve alta) e
  quica bem menos.
- **Goleiro no tempo da bola**: o corpo chega esticado no mergulho e no encaixe exatamente quando a
  bola chega (antes ele pulava e o pico do salto vinha antes do chute chegar).
- **Saída de bola do goleiro**: tiro de meta com a bola no chão — passe para quem está livre e com a
  linha aberta (pelos lados, nunca cruzando na frente do gol) ou lançamento longo para o companheiro
  mais sozinho lá na frente, com o alcance calculado pela física da bola (antes ele pegava a bola com
  as mãos no tiro de meta). Com a bola nos pés (recuo), não sai mais driblando nem toca às cegas para
  a frente. No futsal o tiro de meta continua com as mãos, como na regra.
- **Lado de fora dos estádios**: fachada com brises, pilares e lajes, faixa na cor do clube (acesa à
  noite), portões com catracas, rampas em espiral nos cantos dos estádios grandes, esplanada com
  árvores, postes, bilheterias e torcedores chegando, avenida em volta e estacionamento com carros e
  ônibus; a cidade e a paisagem começam depois do entorno.
- **Escalações no rodapé**: faixa no pé da tela com número e nome dos atletas dos dois times, quem
  está com a bola em destaque e expulsos apagados (liga/desliga em Opções e na pausa).
- **Dois instaladores Android**: o normal (público, nomes fictícios) e a versão completa para uso
  pessoal, com clubes, estádios e jogadores reais já instalados (fica num rascunho privado, só para
  quem administra o repositório).
- **Só Android por enquanto**: Windows e Linux saem quando a versão for aprovada.

## Versão 2.4.1

- **Goleiro sem mergulho à toa**: bola perto do corpo (até ~1,25 braço) é defendida de pé, inclinando;
  o mergulho é decidido na hora, com a leitura já certa (antes às vezes ele mergulhava no ângulo para
  uma bola que vinha quase nele). Chutes de longe bem colocados voltam a entrar de vez em quando
  (~3%, como no futebol de verdade), mas não os "bobos".
- **Mergulho na tela gira pelo quadril**: o corpo desenhado fica onde estão as mãos na simulação
  (antes "voava" ~0,7 m além e parecia passar por cima da bola).
- **Cruzamento de verdade**: passe alto da ponta, perto da linha de fundo, apontando para a área,
  procura quem está na área (o melhor no cabeceio).

## Versão 2.4.0

- **Goleiro sem gols bobos de longe**: ele defende de onde está (antes, adiantado, corria de costas
  até a linha do gol enquanto a bola vinha e mergulhava voando para trás); só recua quando a bola vai
  passar por cima das mãos. No salto para a bola alta as mãos vão na bola (antes iam para o lado,
  como num mergulho), no encaixe as mãos alcançam a bola até o comprimento do braço, e a leitura do
  chute se corrige enquanto a bola viaja. Em partidas CPU x CPU, os gols de fora da área caíram de
  ~1 em 11 chutes para ~1 em 100.
- **Goleiro não some mais na queda**: deitado depois do mergulho ele não é mais puxado para dentro do
  gramado, e os pés não ficam presos ao chão durante a defesa.
- **Reposição do goleiro**: rolada firme no pé do companheiro, arremesso por cima em arco baixo e
  chutão num ângulo de verdade, com o alcance calculado pela física da bola (antes a bola subia
  quase na vertical e caía curta, no meio dos adversários); escolhe quem está sozinho. O goleiro não
  pega mais de novo a própria reposição (no futsal ele rolava a bola, corria atrás e agarrava outra vez).
- **Sem giros tentando passar para trás**: pressionado e sem linha de passe, o atleta protege a bola
  e sai para o lado contrário ao marcador; o passe para trás vem depois de girar o corpo com a bola;
  o passe nunca vai "por dentro" do marcador colado; ninguém dá voltas seguidas no mesmo sentido.
  No futsal, os passes perdidos caíram de 37% para 23% e os giros, quase pela metade.
- **Jogo um pouco mais rápido e passes mais fortes** (na quadra, o passe curto saía a ~4,5 m/s).
- **Rede de verdade**: a bola afunda a rede até ~60 cm, freia suave, a rede devolve devagar e a bola
  escorrega para baixo; o pano envolve a bola (malha mais fina) e volta balançando.
- **Traves completas**: barras de cima ligando o travessão aos apoios de trás e a barra de trás.
- **Túnel dos vestiários em 3D**: corredor de verdade atrás da porta (piso, paredes com a faixa do
  clube, luminárias, fundo iluminado); os times saem lá de dentro na cerimônia.
- **Câmera do campo mais perto do jogo**, de frente (TV e TV Próxima).
- **Ombros mais baixos**, acompanhando o fim do pescoço, em todas as situações (antes, na comemoração,
  nas quedas e nas defesas, voltavam a ficar altos).
- **Atletas do tamanho certo**: cada modelo agora é levado à altura sorteada, de 1,68 a 1,93 m
  (média ~1,80 m). Antes a escala era aplicada sobre modelos que já tinham 1,81–1,95 m e alguns
  passavam de 2 m, parecendo grandes demais para o campo.
- **Tiro de meta da CPU**: toca no companheiro livre ou lança para quem está sozinho lá na frente
  (antes tocava para a frente numa direção sorteada).
- **Torcedores 3D encorpados**: ombros, duas pernas, braços mais grossos, vistos de qualquer lado e
  sem sumir na sombra da cobertura.
- **Bandeiras sem "disparar"**: o ritmo muda aos poucos (antes, num lance de perigo, a bandeira
  acelerava de repente).
- **Coberturas dos cantos** na mesma altura das outras (não viram mais uma faixa na frente da torcida).
- **Corpo**: mãos ~32% menores que o modelo original.
- **Visão livre**: bandeiras, torcedores da rua e do society, e os elementos extras do cenário também
  somem quando tapam o jogo; ninguém mais fica do lado da câmera na rua.
- **Torcida 3D interativa**: palmas no ritmo quando o time ataca, mãos na cabeça no chute que passa
  perto (e alívio do outro lado), protesto na falta, os blocos perto do lance se agitam mais, braços
  para cima e pulos no gol e na ola.
- **Menos bandeiras** (26 no estádio, 11 nos ginásios, 6 no society).

## Versão 2.3.1

- **Jogadores sem rodar**: quem chega rápido no lugar freia antes de virar (antes ficava orbitando
  o ponto); devagar e sem a bola, o atleta fica de frente para o jogo e dá passos de lado em vez de
  girar a cada empurrão; a IA e a condução da CPU não giram mais em espiral; o corpo vira com
  velocidade de atleta de verdade.
- **Bandeiras dentro das arquibancadas**: cada uma numa fileira real da arquibancada (nunca além
  da última) e dentro da largura dela; no society, só na arquibancadinha.
- **Goleiro não some mais no gol**: além do mergulho, empurrões e colisões também não o jogam
  para trás da linha (teste automático confere em partidas inteiras).
- **Corrida**: braço mais para trás que para a frente, mão no máximo na altura do peito; ombros
  mais estreitos e mãos menores.

## Versão 2.3.0

- **Sem a faixa clara na arquibancada**: a cobertura ficou mais alta e recuada (vista por baixo ela
  virava uma faixa que acompanhava a câmera) e não há mais cobertura do lado da câmera.
- **Torcida toda em 3D**: todas as fileiras com torcedores modelados (no celular mais fraco, até 16
  fileiras), pulando e erguendo os braços nos lances, e **ola** em momentos aleatórios, dando a
  volta no estádio.
- **Mais bandeiras**, espalhadas por várias fileiras da arquibancada.
- **Ícone atual no Windows e no Linux** (todos os tamanhos no executável e no instalador; no Linux,
  o atalho aponta direto para o ícone novo).
- **Procurar atualizações**: nova aba em Configurações e aviso no menu principal sempre que houver
  uma versão nova, com botão para baixar.
- **Braços na corrida refeitos**: balanço a partir do ombro no ritmo da perna oposta, cruzando um
  pouco à frente do corpo, cotovelo perto de 90° correndo e quase solto andando, ombros acompanhando.

## Versão 2.2.0

- **Jogadores sem roupa em alguns celulares**: corrigido (em telas com pouca precisão de
  profundidade a pele "furava" o uniforme; o tecido agora fica sempre por cima).
- **Proporções do corpo**: ombros mais baixos e mãos menores.
- **Corrida e caminhada**: sem o movimento em círculo das pernas (a direção da passada só muda com
  movimento de verdade); passada também de lado e de costas.
- **Transições nas bolas paradas**: no lateral e nas cobranças rápidas o cobrador vai até a bola de
  onde estava, em vez de aparecer nela; nas cenas (falta, escanteio, tiro de meta, pênalti) ele
  espera os companheiros e caminha até a bola.
- **Portas nos vestiários**: batente, duas folhas abertas e o corredor iluminado lá dentro.
- **O mais perto vai na bola**: o companheiro mais próximo da bola solta vai buscá-la; a troca
  automática de jogador acontece mais cedo.
- **Goleiro não entra mais no gol**: o mergulho nunca o leva para trás da linha (antes ele sumia
  dentro da rede e a bola entrava junto).
- **Uniforme alternativo**: todos os times ganharam um 4º uniforme.
- **Torcedores com as camisas do time**, variando entre os 4 uniformes (60 : 30 : 20 : 10).

## Versão 2.1.0

- **Torcedores em 3D**: gente modelada (pernas, tronco com a camisa do time, braços, cabeça, cabelo
  ou boné) nas fileiras da frente dos estádios, em todas as fileiras dos ginásios, na
  arquibancadinha do society e na rua. Cada bloco se mexe no seu tempo: balança parado, pula e
  ergue os braços quando o jogo esquenta e explode no gol do seu time (a torcida rival fica quieta).
- **Passada refinada**: vale também andando de lado e de costas (a perna vai na direção do passo, o
  joelho dobra sempre para trás); braços com balanço mais contido e a mão no máximo na altura do
  peito, sempre no ritmo da perna oposta.
- **Corrida e caminhada desenhadas**: novo ciclo de passada feito à mão (apoio, impulso e balanço
  de cada perna, joelho alto no pique, braço acompanhando a perna oposta, giro da pelve e
  contragiro dos ombros, tronco inclinado conforme a velocidade), no lugar da captura antiga.
- **Goleiros**: postura de prontidão quando o adversário ataca (base larga, joelhos dobrados, mãos
  abertas) e mergulho que desliza e para aos poucos ao tocar o chão.
- **Cada jogador com o seu jeito**: driblador encara de mais longe e usa o seu drible preferido
  (arrancada, puxada, rolinho, pedalada ou giro), ginga mudando a direção de repente; armador
  procura mais o passe; finalizador arrisca de mais longe.
- **Músicas novas** com instrumentos reais (guitarra, baixo, bateria, piano, metais, cordas),
  humanização e mixagem de estúdio: pop-rock, funk, house, latino, indie, lo-fi, samba-rock,
  eletrônica, pop-punk e afro-pop.
- **Torcidas separadas**: o mandante ocupa quase todo o estádio e o visitante tem o seu setor (atrás
  de um dos gols), com bandeiras e faixas só nas cores dele; em jogos de seleções as torcidas se
  misturam. Vale também para ginásios e society.
- **Sem partículas da "ola"**: as bolinhas coloridas que davam a volta no campo (passando na
  frente da câmera) foram retiradas.
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
