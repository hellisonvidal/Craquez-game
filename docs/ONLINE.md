# Modo on-line

## Como funciona

- **Partida em lockstep**: cada aparelho roda a mesma simulação (determinística, 60 passos por segundo) e só
  os comandos dos controles viajam pela rede, alguns passos à frente para esconder a demora da internet. A
  cada segundo os dois lados comparam um resumo do estado; se divergirem, o jogo avisa.
- **Unity Gaming Services** (plano gratuito):
  - *Authentication*: login anônimo, sem cadastro;
  - *Sessions/Lobby*: sala de dois com código ou partida rápida (junta quem escolheu a mesma modalidade e
    tem a mesma versão do jogo);
  - *Relay*: o servidor que repassa os comandos entre os aparelhos (celular/TV não se conectam direto).
- **Aperto de mão**: os dois aparelhos conferem a versão do jogo e os dados dos dois times (se um tiver um
  option file diferente, a partida não começa e o jogo explica). Quem criou a sala sorteia local, horário,
  bola e semente, e os dois começam a mesma partida.
- **Durante a partida on-line**: sem pausa, sem replay e sem mexer no time no intervalo (o 2º tempo começa
  sozinho); clima seco (sol ou nublado), porque a água no campo depende do relógio de cada aparelho.
- **Convite**: na sala criada, "Convidar" abre o compartilhar do Android (WhatsApp, Telegram...) com o
  código e o link `https://hellisonvidal.github.io/Craquez-game/sala/?c=CODIGO`. A página tenta abrir o jogo
  na sala (`craquez://sala/CODIGO`) e tem o botão para baixar o jogo. Quem recebe também pode digitar o
  código ou tocar em "Colar".

Código: `Assets/Craquez/Scripts/Online` (módulo `Craquez.Online`), `Sim/Net/Lockstep.cs` (lockstep) e o modo
de rede em `MatchController` (`net`).

## O que falta (só você pode fazer — exige a sua conta)

1. Entre em **https://cloud.unity.com** com a mesma conta Unity do CI.
2. Crie o projeto **Craquez** e ative **Authentication**, **Lobby** e **Relay** (plano gratuito; a Unity
   pode pedir um cartão, mas só cobra acima da cota).
3. Me mande o **Project ID** (Project settings) e o **Organization ID** (Organization settings). Não são
   senhas: só identificam o projeto. Eu coloco no `ProjectSettings` e o cartão ON-LINE passa a funcionar.
4. (Para o link do convite abrir uma página) autorizar o **GitHub Pages** do repositório, servindo a pasta
   `docs` da branch `main` — eu ligo com um comando.

Enquanto o Project ID não estiver no projeto, o cartão ON-LINE mostra "em breve" e explica o motivo.

## Como testar

1. Instale a versão nova em **dois aparelhos** (dois celulares, ou celular e TV).
2. Os dois escolhem a mesma modalidade em ON-LINE. Um toca em **Criar sala** e manda o convite; o outro
   abre o link ou digita o código. Ou os dois tocam em **Partida rápida**.
3. Joguem alguns minutos. Se aparecer "As partidas dos dois aparelhos ficaram diferentes", me avise com o
   número do passo que aparece na mensagem.
